// Copyright (C) 2009-2023 Lemoine Automation Technologies
// Copyright (C) 2025 Atsora Solutions
//
// SPDX-License-Identifier: GPL-2.0-only
// SPDX-License-Identifier: MIT

/* ========================================================================
 * Copyright (c) 2005-2020 The OPC Foundation, Inc.
 * 2009-2023 Lemoine Automation Technologies
 * All rights reserved.
 *
 * OPC Foundation MIT License 1.00
 * 
 * Permission is hereby granted, free of charge, to any person
 * obtaining a copy of this software and associated documentation
 * files (the "Software"), to deal in the Software without
 * restriction, including without limitation the rights to use,
 * copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the
 * Software is furnished to do so, subject to the following
 * conditions:
 * 
 * The above copyright notice and this permission notice shall be
 * included in all copies or substantial portions of the Software.
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
 * EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
 * OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
 * NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
 * HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
 * WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
 * FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
 * OTHER DEALINGS IN THE SOFTWARE.
 *
 * The complete license agreement can be found here:
 * http://opcfoundation.org/License/MIT/1.00/
 * ======================================================================*/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using Opc.Ua;
using Opc.Ua.Client;

namespace Lemoine.Cnc
{
  /// <summary>
  /// NodeManager
  /// </summary>
  sealed class NodeManager
  {
    ILog log = LogManager.GetLogger (typeof (NodeManager).FullName);

    readonly ReadValueIdCollection m_readNodes = new ReadValueIdCollection ();
    readonly IDictionary<string, string> m_parametersWithNodeId = new Dictionary<string, string> ();
    readonly IDictionary<string, object> m_resultsByNodeId = new ConcurrentDictionary<string, object> ();
    int m_cncAcquisitionId = 0;

    /// <summary>
    /// Constructor
    /// </summary>
    public NodeManager (int cncAcquisitionId)
    {
      m_cncAcquisitionId = cncAcquisitionId;
      log = LogManager.GetLogger ($"Lemoine.Cnc.In.OpcUaClient.{cncAcquisitionId}.NodeManager");
    }

    /// <summary>
    /// Set the cnc acquisition id
    /// </summary>
    /// <param name="cncAcquisitionId"></param>
    public int CncAcquisitionId {
      get => m_cncAcquisitionId;
      set { 
        m_cncAcquisitionId = value;
        log = LogManager.GetLogger ($"Lemoine.Cnc.In.OpcUaClient.{m_cncAcquisitionId}.NodeManager");
      }
    }

    /// <summary>
    /// Are there nodes to read?
    /// </summary>
    /// <returns></returns>
    public bool IsNodesToRead () => m_readNodes.Any ();

    /// <summary>
    /// Read a list of nodes from Server
    /// </summary>
    public async Task ReadNodesAsync (Opc.Ua.Client.ISession session, CancellationToken cancellationToken = default)
    {
      m_resultsByNodeId.Clear ();

      if (session == null || session.Connected == false) {
        log.Error ($"ReadNodesAsync: session not connected");
        // TODO: exception or not
        return;
      }

      if (0 == m_readNodes.Count) {
        log.Error ($"ReadNodesAsync: no node to read");
        return;
      }

      try {
        if (log.IsDebugEnabled) {
          log.Debug ($"ReadNodesAsync: reading {m_readNodes.Count} nodes");
        }

        // Call Read Service
        var readResponse = await session.ReadAsync (
          null,
          0,
          TimestampsToReturn.Both,
          m_readNodes, cancellationToken);

        // Validate the results
        ClientBase.ValidateResponse (readResponse.Results, m_readNodes);

        if (log.IsDebugEnabled) {
          foreach (var result in readResponse.Results) {
            log.Debug ($"ReadNodesAsync: Value={result.Value} StatusCode={result.StatusCode} Type={result.Value.GetType ()} TypeInfo={result.WrappedValue.TypeInfo}");
          }
        }

        ProcessResults (readResponse.Results, readResponse.DiagnosticInfos);
      }
      catch (Exception ex) {
        log.Error ($"ReadNodesAsync: exception", ex);
        throw;
      }
    }

    /// <summary>
    /// Write a list of nodes to the Server
    /// </summary>
    public async Task WriteNodes (Session session, WriteValueCollection writeNodes, CancellationToken cancellationToken = default)
    {
      if (session == null || session.Connected == false) {
        log.Error ($"WriteNodes: session not connected");
        // TODO: exception or not
        return;
      }

      try {
        if (log.IsDebugEnabled) {
          log.Debug ($"WriteNodes: reading {writeNodes.Count} nodes");
        }

        // Call Write Service
        var writeResponse = await session.WriteAsync (null,
                        writeNodes, cancellationToken);

        // Validate the response
        ClientBase.ValidateResponse (writeResponse.Results, writeNodes);

        if (log.IsDebugEnabled) {
          log.Debug ("WriteNodes: results:");
          foreach (StatusCode writeResult in writeResponse.Results) {
            log.Debug ($"  {writeResult}");
          }
        }
      }
      catch (Exception ex) {
        log.Error ($"WriteNodes: exception", ex);
        throw;
      }
    }

    /// <summary>
    /// Browse Server nodes
    /// </summary>
    public void Browse (ISession session)
    {
      if (session == null || session.Connected == false) {
        log.Error ($"Browse: session not connected");
        // TODO: exception or not
        return;
      }

      try {
        // Create a Browser object
        Browser browser = new Browser (session);

        // Set browse parameters
        browser.BrowseDirection = BrowseDirection.Forward;
        browser.NodeClassMask = (int)NodeClass.Object | (int)NodeClass.Variable;
        browser.ReferenceTypeId = ReferenceTypeIds.HierarchicalReferences;

        NodeId nodeToBrowse = ObjectIds.Server;

        // Call Browse service
        if (log.IsDebugEnabled) {
          log.Debug ($"Browse: browsing {nodeToBrowse} node");
        }
        ReferenceDescriptionCollection browseResults = browser.Browse (nodeToBrowse);

        // Display the results
        if (log.IsInfoEnabled) {
          log.Debug ($"Browse: returned {browseResults.Count} results");
          foreach (ReferenceDescription result in browseResults) {
            log.Info ($"Browse: DisplayName={result.DisplayName.Text}, NodeClass={result.NodeClass}");
          }
        }
      }
      catch (Exception ex) {
        log.Error ($"Browse: exception", ex);
        throw;
      }
    }

    /// <summary>
    /// Prepare the query with all parameters required
    /// </summary>
    /// <param name="session">Session to test a node</param>
    /// <param name="parameters"></param>
    /// <param name="defaultNamespaceIndex">Default namespace index to consider if not set in parameter</param>
    /// <returns>success</returns>
    public async Task<bool> PrepareQueryAsync (Opc.Ua.Client.ISession session, IList<string> parameters, int defaultNamespaceIndex = 0)
    {
      if (!parameters.Any ()) {
        log.Error ($"PrepareQueryAsync: no parameter");
        return false;
      }

      // Clear existing parameters and nodes
      m_readNodes.Clear ();
      m_parametersWithNodeId.Clear ();

      if (log.IsDebugEnabled) {
        log.Debug ($"PrepareQueryAsync: adding {parameters.Count} parameters under monitoring...");
      }
      int count = 0;
      var allNodeIdentifiers = new HashSet<string> ();
      foreach (var parameter in parameters) {
        // Possibly extract indexes
        string indexes = "";
        if (parameter.Contains ("|")) {
          string[] parts = parameter.Split ('|');
          if (parts.Length == 2) {
            indexes = parts[1];
          }
          else {
            log.Warn ($"PrepareQueryAsync: bad parameter {parameter}, cannot extract indexes");
          }
        }

        // Convert to a valid node id
        string nodeId = await GetNodeIdFromParamAsync (session, parameter, defaultNamespaceIndex);
        if (string.IsNullOrEmpty (nodeId)) {
          continue;
        }

        // Create a ReadValueId and validate it
        var readValueId = new ReadValueId () { NodeId = nodeId, AttributeId = Attributes.Value, IndexRange = indexes };
        var validationResult = ReadValueId.Validate (readValueId);
        if (validationResult != null) {
          log.Error ($"PrepareQueryAsync: Invalid node id '{parameter}': {validationResult}");
          continue;
        }

        // Associate the node id to the parameter
        string identifier = nodeId.ToString () + (indexes != "" ? ("|" + indexes) : "");
        m_parametersWithNodeId[parameter] = identifier;

        // Add a ReadValueId
        if (allNodeIdentifiers.Contains (identifier)) {
          log.Info ($"PrepareQueryAsync: Id='{identifier}' already in the set of nodes to read");
        }
        else {
          if (log.IsDebugEnabled) {
            log.Debug ($"PrepareQueryAsync: add id={identifier}");
          }
          allNodeIdentifiers.Add (identifier);
          m_readNodes.Add (readValueId);
        }

        count++;
      }

      if (log.IsErrorEnabled) {
        if (count == parameters.Count) {
          log.Info ($"PrepareQueryAsync: successfully added {count}/{parameters.Count} parameters under monitoring");
        }
        else if (0 == count) {
          log.Error ($"PrepareQueryAsync: no node was added while {parameters.Count} parameters should be monitor");
        }
        else {
          log.Warn ($"PrepareQueryAsync: successfully added {count}/{parameters.Count} parameters under monitoring");
        }
      }

      return (0 != count);
    }

    /// <summary>
    /// Get a node id from a config parameter
    /// </summary>
    /// <param name="session"></param>
    /// <param name="parameter"></param>
    /// <param name="defaultNamespaceIndex"></param>
    /// <returns></returns>
    public async Task<string> GetNodeIdFromParamAsync (Opc.Ua.Client.ISession session, string parameter, int defaultNamespaceIndex = 0)
    {
      // Possibly remove the indexes
      var parameterTmp = parameter;
      if (parameter.Contains ("|")) {
        string[] parts = parameter.Split ('|');
        if (parts.Length == 2) {
          parameterTmp = parts[0];
        }
        else {
          log.Warn ($"GetNodeIdFromParamAsync: bad parameter {parameter}: cannot extract indexes");
        }
      }

      // Possibly prepend with "s="
      parameterTmp = (parameterTmp.Contains ("=") ? parameterTmp : ("s=" + parameterTmp));

      // Convert to a valid node id
      string nodeId;
      try {
        if (parameter.Contains ("ns=")) {
          // Namespace already specified
          nodeId = parameterTmp;
          if (!await TestNodeIdAsync (session, nodeId)) {
            log.Error ($"GetNodeIdFromParamAsync: invalid node id {nodeId}");
            return "";
          }
        }
        else {
          // Add the current namespace
          nodeId = "ns=" + defaultNamespaceIndex + ";" + parameterTmp;
          if (!await TestNodeIdAsync (session, nodeId)) {
            if (defaultNamespaceIndex != 0) {
              // Test with the namespace index #0
              nodeId = "ns=0;" + parameterTmp;
              if (!await TestNodeIdAsync (session, nodeId)) {
                log.Error ($"GetNodeIdFromParamAsync: invalid node id {nodeId}");
                return "";
              }
            }
            else {
              log.Error ($"GetNodeIdFromParamAsync: invalid node id {nodeId}");
              return ""; // Not possible to test something else
            }
          }
        }
      }
      catch (Exception ex) {
        // We may have "Cannot parse node id text: ..."
        log.Error ($"GetNodeIdFromParamAsync: invalid node id {parameter}", ex);
        return "";
      }

      return nodeId;
    }

    async Task<bool> TestNodeIdAsync (Opc.Ua.Client.ISession session, string nodeId)
    {
      try {
        // Remove the possible array index (otherwise there is an error)
        nodeId = nodeId.Split ('[')[0];
        var node = await session.ReadNodeAsync (nodeId);
        if (node is null) {
          log.Warn ($"TestNodeIdAsync: {nodeId} not found");
          return false; // Node id not found
        }
      }
      catch (ServiceResultException ex) {
        // Message could be BadUserAccessDenied / BadNodeIdUnknown
        log.Error ($"TestNodeIdAsync: OPC UA Service exception for {nodeId}: {ex.Message}", ex);
        return false;
      }
      catch (Exception ex) {
        log.Error ($"TestNodeIdAsync: exception for {nodeId}: {ex.Message}", ex);
        return false;
      }
      return true;
    }

    /// <summary>
    /// Get the namespace index from a namespace name
    /// </summary>
    /// <param name="session"></param>
    /// <param name="namespaceName"></param>
    /// <returns></returns>
    public int GetNamespaceIndex (Opc.Ua.Client.ISession session, string namespaceName)
    {
      // First list all possible namespaces
      var namespaces = session.NamespaceUris;
      if (log.IsInfoEnabled) {
        for (uint i = 0; i < namespaces.Count; i++) {
          log.Info ($"GetNamespaceIndex: Namespace #{i} => '{namespaces.GetString (i)}'");
        }
      }

      if (int.TryParse (namespaceName, out var namespaceIndex)) {
        if (log.IsDebugEnabled) {
          log.Debug ($"GetNamespaceIndex: specified namespace name {namespaceName} is an integer, try to consider it as an index directly");
        }
        if (namespaceIndex < 0 || namespaceIndex >= namespaces.Count) {
          log.Error ($"GetNamespaceIndex: specified int namespace #{namespaceIndex} is out of range, return 0 instead");
          return 0;
        }
        else {
          if (log.IsInfoEnabled) {
            log.Info ($"GetNamespaceIndex: specified int namespace #{namespaceIndex}={namespaces.GetString ((uint)namespaceIndex)}");
          }
          return namespaceIndex;
        }
      }
      else {
        // Try to recognize an existing namespace
        namespaceIndex = -1;
        for (var i = 0; i < namespaces.Count; i++) {
          if (namespaces.GetString ((uint)i).ToLower ().CompareTo ((object)namespaceName.ToLower ()) == 0) {
            namespaceIndex = (int)i;
            break;
          }
        }

        if (namespaceIndex == -1) {
          log.Error ($"GetNamespaceIndex: namespace={namespaceName} not found, return #0");
          return 0;
        }
        else {
          if (log.IsInfoEnabled) {
            log.Info ($"GetNamespaceIndex: return #{namespaceIndex} for {namespaceName}");
          }
          return namespaceIndex;
        }
      }
    }

    void ProcessResults (DataValueCollection results, DiagnosticInfoCollection diagnostics)
    {
      // Log what happened
      if (diagnostics != null) {
        foreach (var diagnostic in diagnostics) {
          log.Error ($"ProcessResults: diagnostic when receiving data: {diagnostic}");
        }
      }

      // Clear previous results
      m_resultsByNodeId.Clear ();
      if (results is null) {
        log.Error ("ProcessResults: results is null");
        return;
      }

      if (results.Count != m_readNodes.Count) {
        log.Error ($"ProcessResults: number of results ({results.Count}) is not the same than number of nodes to read ({m_readNodes.Count})");
        return;
      }

      // Store the new values
      for (var i = 0; i < m_readNodes.Count; i++) {
        var identifier = m_readNodes[i].NodeId.ToString ();
        if (m_readNodes[i].IndexRange != "") {
          identifier += "|" + m_readNodes[i].IndexRange;
        }
        var v = results[i].Value;
        if (log.IsDebugEnabled) {
          log.Debug ($"ProcessResults: {identifier} => {v}");
        }
        if (v is byte[] byteString) {
          var s = System.Text.Encoding.UTF8.GetString (byteString);
          if (log.IsDebugEnabled) {
            log.Debug ($"ProcessResults: byte[] {identifier} => {s}");
          }
          m_resultsByNodeId[identifier] = s;
        }
        else {
          m_resultsByNodeId[identifier] = v;
        }

        if (log.IsErrorEnabled) {
          LogResult (results[i], identifier);
        }
      }
    }

    void LogResult (DataValue result, string identifier)
    {
      try {
        if (result.Value == null) {
          log.Warn ($"LogResult: received a null value for node id {identifier}");
        }
        else if (result.Value.GetType ().IsArray) {
          // Convert as array and display the first element if possible
          if (!(result.Value is object[] resultArray)) {
            log.Error ($"LogResult: conversion error to array for {result.Value}, id={identifier} ({result.StatusCode})");
          }
          else { // Not null
            switch (resultArray.Length) {
            case 0:
              log.Error ($"LogResult: empty array {result.Value}, id={identifier} ({result.StatusCode})");
              break;
            case 1:
              if (log.IsDebugEnabled) {
                log.Debug ($"LogResult: array with a unique element {resultArray[0]} for id={identifier} ({result.StatusCode}");
              }
              break;
            default:
              log.Error ($"LogResult: too many values in {result.Value}, id={identifier} ({result.StatusCode})");
              break;
            }
          }
        }
        else {
          log.Info ($"LogResult: read {identifier}={result.Value} ({result.StatusCode})");
        }
      }
      catch (Exception ex) {
        log.Error ($"LogResult: log error, {ex.Message}", ex);
      }
    }

    /// <summary>
    /// Get a value
    /// </summary>
    /// <param name="parameter"></param>
    /// <returns></returns>
    public object Get (string parameter)
    {
      // Corresponding node identifier
      if (!m_parametersWithNodeId.TryGetValue (parameter, out var nodeIdentifier)) {
        log.Error ($"Get: no valid not identifier for {parameter}");
        throw new Exception ($"{parameter} has no corresponding valid node identifier");
      }

      // Get the result
      if (!m_resultsByNodeId.TryGetValue (nodeIdentifier, out var result)) {
        log.Info ($"Get: {parameter} id={nodeIdentifier} is not ready yet");
        throw new Exception ("Value is not ready yet");
      }

      if (result is null) {
        log.Error ($"Get: value for {parameter} id={nodeIdentifier} is null");
        throw new Exception ("Null value");
      }

      // Convert the value as string
      if (result.GetType ().IsArray) {
        // Convert as array and take the first element if possible
        if (!(result is object[] resultArray)) {
          log.Error ($"Get: conversion error to array for {result}, parameter={parameter} id={nodeIdentifier}");
          throw new InvalidCastException ("Array conversion error");
        }

        switch (resultArray.Length) {
        case 0:
          log.Error ($"Get: empty array {result}, parameter={parameter} id={nodeIdentifier}");
          throw new Exception ("Empty array");
        case 1:
          if (log.IsDebugEnabled) {
            log.Debug ($"Get: return first array element {resultArray[0]} for parameter{parameter} id={nodeIdentifier}");
          }
          return resultArray[0];
        default:
          log.Error ($"Get: too many values in {result}, parameter={parameter} id={nodeIdentifier}");
          throw new Exception ("Too many values in array");
        }
      }
      else {
        if (log.IsDebugEnabled) {
          log.Debug ($"Get: return {result} for parameter{parameter} id={nodeIdentifier}");
        }
        return result;
      }
    }

  }
}
