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
using Microsoft.Extensions.Logging;
using Opc.Ua;
using Opc.Ua.Client;

namespace Lemoine.Cnc
{
  /// <summary>
  /// NodeManager
  /// </summary>
  sealed class NodeManager
  {
    ILogger log = OpcUaClientLogging.CreateLogger (typeof (NodeManager).FullName);

    readonly ReadValueIdCollection m_readNodes = new ReadValueIdCollection ();
    readonly IDictionary<string, string> m_parametersWithNodeId = new Dictionary<string, string> ();
    readonly IDictionary<string, object> m_resultsByNodeId = new ConcurrentDictionary<string, object> ();
    readonly IDictionary<string, StatusCode> m_statusCodesByNodeId = new ConcurrentDictionary<string, StatusCode> ();
    int m_cncAcquisitionId = 0;

    /// <summary>
    /// Constructor
    /// </summary>
    public NodeManager (int cncAcquisitionId)
    {
      m_cncAcquisitionId = cncAcquisitionId;
      log = OpcUaClientLogging.CreateLogger ($"Lemoine.Cnc.In.OpcUaClient.{cncAcquisitionId}.NodeManager");
    }

    /// <summary>
    /// Set the cnc acquisition id
    /// </summary>
    /// <param name="cncAcquisitionId"></param>
    public int CncAcquisitionId {
      get => m_cncAcquisitionId;
      set { 
        m_cncAcquisitionId = value;
        log = OpcUaClientLogging.CreateLogger ($"Lemoine.Cnc.In.OpcUaClient.{m_cncAcquisitionId}.NodeManager");
      }
    }

    /// <summary>
    /// Number of consecutive read operations in which not a single node returned a valid value
    ///
    /// This is reset as soon as at least one valid value is read.
    ///
    /// A value that keeps increasing means the prepared query is not valid any more, for example
    /// because the node ids were resolved with a namespace index that does not correspond
    /// to the namespace table of the current session
    /// </summary>
    public int ConsecutiveInvalidReadCount { get; private set; } = 0;

    /// <summary>
    /// Reset the prepared query and all the associated results
    ///
    /// To be used as soon as the node ids may not be valid any more, for example after a reconnection:
    /// a namespace index is only valid for a given session
    /// </summary>
    public void Reset ()
    {
      if (log.IsEnabled (LogLevel.Information)) {
        log.LogInformation ($"Reset: clear {m_readNodes.Count} nodes to read and {m_parametersWithNodeId.Count} parameters");
      }
      m_readNodes.Clear ();
      m_parametersWithNodeId.Clear ();
      m_resultsByNodeId.Clear ();
      m_statusCodesByNodeId.Clear ();
      this.ConsecutiveInvalidReadCount = 0;
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
        log.LogError ($"ReadNodesAsync: session not connected");
        // TODO: exception or not
        return;
      }

      if (0 == m_readNodes.Count) {
        log.LogError ($"ReadNodesAsync: no node to read");
        return;
      }

      try {
        if (log.IsEnabled (LogLevel.Debug)) {
          log.LogDebug ($"ReadNodesAsync: reading {m_readNodes.Count} nodes");
        }

        // Call Read Service
        var readResponse = await session.ReadAsync (
          requestHeader: null,
          maxAge: 0,
          TimestampsToReturn.Neither,
          m_readNodes, cancellationToken);

        // Validate the results
        ClientBase.ValidateResponse (readResponse.Results, m_readNodes);

        if (log.IsEnabled (LogLevel.Debug)) {
          foreach (var result in readResponse.Results) {
            log.LogDebug ($"ReadNodesAsync: Value={result?.Value} StatusCode={result?.StatusCode} Type={result?.Value?.GetType ()} TypeInfo={result?.WrappedValue.TypeInfo}");
          }
        }

        ProcessResults (readResponse.Results, readResponse.DiagnosticInfos);
      }
      catch (Exception ex) {
        log.LogError (ex, $"ReadNodesAsync: exception");
        throw;
      }
    }

    /// <summary>
    /// Write a list of nodes to the Server
    /// </summary>
    public async Task WriteNodes (Session session, WriteValueCollection writeNodes, CancellationToken cancellationToken = default)
    {
      if (session == null || session.Connected == false) {
        log.LogError ($"WriteNodes: session not connected");
        // TODO: exception or not
        return;
      }

      try {
        if (log.IsEnabled (LogLevel.Debug)) {
          log.LogDebug ($"WriteNodes: reading {writeNodes.Count} nodes");
        }

        // Call Write Service
        var writeResponse = await session.WriteAsync (null,
                        writeNodes, cancellationToken);

        // Validate the response
        ClientBase.ValidateResponse (writeResponse.Results, writeNodes);

        if (log.IsEnabled (LogLevel.Debug)) {
          log.LogDebug ("WriteNodes: results:");
          foreach (StatusCode writeResult in writeResponse.Results) {
            log.LogDebug ($"  {writeResult}");
          }
        }
      }
      catch (Exception ex) {
        log.LogError (ex, $"WriteNodes: exception");
        throw;
      }
    }

    /// <summary>
    /// Browse Server nodes
    /// </summary>
    public void Browse (ISession session)
    {
      if (session == null || session.Connected == false) {
        log.LogError ($"Browse: session not connected");
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
        if (log.IsEnabled (LogLevel.Debug)) {
          log.LogDebug ($"Browse: browsing {nodeToBrowse} node");
        }
        ReferenceDescriptionCollection browseResults = browser.Browse (nodeToBrowse);

        // Display the results
        if (log.IsEnabled (LogLevel.Information)) {
          log.LogDebug ($"Browse: returned {browseResults.Count} results");
          foreach (ReferenceDescription result in browseResults) {
            log.LogInformation ($"Browse: DisplayName={result.DisplayName.Text}, NodeClass={result.NodeClass}");
          }
        }
      }
      catch (Exception ex) {
        log.LogError (ex, $"Browse: exception");
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
        log.LogError ($"PrepareQueryAsync: no parameter");
        return false;
      }

      // Clear existing parameters and nodes
      m_readNodes.Clear ();
      m_parametersWithNodeId.Clear ();

      if (log.IsEnabled (LogLevel.Debug)) {
        log.LogDebug ($"PrepareQueryAsync: adding {parameters.Count} parameters under monitoring...");
      }
      int count = 0;
      var allNodeIdentifiers = new HashSet<string> ();
      foreach (var parameter in parameters) {
        // Convert to a valid node id
        var nodeId = await GetNodeIdFromParamAsync (session, parameter, defaultNamespaceIndex);
        if (string.IsNullOrEmpty (nodeId)) {
          continue;
        }
        var identifier = nodeId.ToString ();

        // Create a ReadValueId and validate it
        var readValueId = new ReadValueId () { NodeId = nodeId, AttributeId = Attributes.Value };
        if (parameter.Contains ("|")) { // Check if there is an index range 
          var parameterItems = parameter.Split ('|');
          if (parameterItems.Length == 2) {
            readValueId.IndexRange = parameterItems[1];
            identifier += "|" + readValueId.IndexRange;
          }
          else {
            log.LogWarning ($"PrepareQueryAsync: bad parameter {parameter}, cannot extract indexes");
          }
        }
        var validationResult = ReadValueId.Validate (readValueId);
        if (validationResult != null) {
          log.LogError ($"PrepareQueryAsync: Invalid node id '{parameter}': {validationResult}");
          continue;
        }

        // Associate the node id to the parameter
        m_parametersWithNodeId[parameter] = identifier;

        // Add a ReadValueId
        if (allNodeIdentifiers.Contains (identifier)) {
          log.LogInformation ($"PrepareQueryAsync: Id='{identifier}' already in the set of nodes to read");
        }
        else {
          if (log.IsEnabled (LogLevel.Debug)) {
            log.LogDebug ($"PrepareQueryAsync: add id={identifier}");
          }
          allNodeIdentifiers.Add (identifier);
          m_readNodes.Add (readValueId);
        }

        count++;
      }

      if (log.IsEnabled (LogLevel.Error)) {
        if (count == parameters.Count) {
          log.LogInformation ($"PrepareQueryAsync: successfully added {count}/{parameters.Count} parameters under monitoring");
        }
        else if (0 == count) {
          log.LogError ($"PrepareQueryAsync: no node was added while {parameters.Count} parameters should be monitor");
        }
        else {
          log.LogWarning ($"PrepareQueryAsync: successfully added {count}/{parameters.Count} parameters under monitoring");
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
          log.LogWarning ($"GetNodeIdFromParamAsync: bad parameter {parameter}: cannot extract indexes");
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
            log.LogError ($"GetNodeIdFromParamAsync: invalid node id {nodeId}");
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
                log.LogError ($"GetNodeIdFromParamAsync: invalid node id {nodeId}");
                return "";
              }
            }
            else {
              log.LogError ($"GetNodeIdFromParamAsync: invalid node id {nodeId}");
              return ""; // Not possible to test something else
            }
          }
        }
      }
      catch (Exception ex) {
        // We may have "Cannot parse node id text: ..."
        log.LogError (ex, $"GetNodeIdFromParamAsync: invalid node id {parameter}");
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
          log.LogWarning ($"TestNodeIdAsync: {nodeId} not found");
          return false; // Node id not found
        }
      }
      catch (ServiceResultException ex) {
        // Message could be BadUserAccessDenied / BadNodeIdUnknown
        log.LogError (ex, $"TestNodeIdAsync: OPC UA Service exception for {nodeId}: {ex.Message}");
        return false;
      }
      catch (Exception ex) {
        log.LogError (ex, $"TestNodeIdAsync: exception for {nodeId}: {ex.Message}");
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
      if (log.IsEnabled (LogLevel.Information)) {
        for (uint i = 0; i < namespaces.Count; i++) {
          log.LogInformation ($"GetNamespaceIndex: Namespace #{i} => '{namespaces.GetString (i)}'");
        }
      }

      if (int.TryParse (namespaceName, out var namespaceIndex)) {
        if (log.IsEnabled (LogLevel.Debug)) {
          log.LogDebug ($"GetNamespaceIndex: specified namespace name {namespaceName} is an integer, try to consider it as an index directly");
        }
        if (namespaceIndex < 0 || namespaceIndex >= namespaces.Count) {
          log.LogError ($"GetNamespaceIndex: specified int namespace #{namespaceIndex} is out of range, return 0 instead");
          return 0;
        }
        else {
          if (log.IsEnabled (LogLevel.Information)) {
            log.LogInformation ($"GetNamespaceIndex: specified int namespace #{namespaceIndex}={namespaces.GetString ((uint)namespaceIndex)}");
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
          log.LogError ($"GetNamespaceIndex: namespace={namespaceName} not found, return #0");
          return 0;
        }
        else {
          if (log.IsEnabled (LogLevel.Information)) {
            log.LogInformation ($"GetNamespaceIndex: return #{namespaceIndex} for {namespaceName}");
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
          log.LogError ($"ProcessResults: diagnostic when receiving data: {diagnostic}");
        }
      }

      // Clear previous results
      m_resultsByNodeId.Clear ();
      m_statusCodesByNodeId.Clear ();
      if (results is null) {
        ++this.ConsecutiveInvalidReadCount;
        log.LogError ($"ProcessResults: results is null ({this.ConsecutiveInvalidReadCount} consecutive invalid read operations)");
        return;
      }

      if (results.Count != m_readNodes.Count) {
        ++this.ConsecutiveInvalidReadCount;
        log.LogError ($"ProcessResults: number of results ({results.Count}) is not the same than number of nodes to read ({m_readNodes.Count}) ({this.ConsecutiveInvalidReadCount} consecutive invalid read operations)");
        return;
      }

      // Store the new values
      var invalidCount = 0;
      for (var i = 0; i < m_readNodes.Count; i++) {
        var identifier = m_readNodes[i].NodeId.ToString ();
        if (!string.IsNullOrEmpty (m_readNodes[i].IndexRange)) {
          identifier += "|" + m_readNodes[i].IndexRange;
        }
        var statusCode = results[i].StatusCode;
        m_statusCodesByNodeId[identifier] = statusCode;
        var v = results[i].Value;
        if (v is null || StatusCode.IsBad (statusCode)) {
          ++invalidCount;
        }
        if (log.IsEnabled (LogLevel.Debug)) {
          log.LogDebug ($"ProcessResults: {identifier} => {v} ({statusCode})");
        }
        if (v is byte[] byteString) {
          var s = System.Text.Encoding.UTF8.GetString (byteString);
          if (log.IsEnabled (LogLevel.Debug)) {
            log.LogDebug ($"ProcessResults: byte[] {identifier} => {s}");
          }
          m_resultsByNodeId[identifier] = s;
        }
        else {
          m_resultsByNodeId[identifier] = v;
        }

        if (log.IsEnabled (LogLevel.Error)) {
          LogResult (results[i], identifier);
        }
      }

      // Check the whole read operation is still valid: if not a single node returns a valid value,
      // the prepared query is probably not valid any more (stale node ids or namespace indexes)
      if ((0 < m_readNodes.Count) && (invalidCount == m_readNodes.Count)) {
        ++this.ConsecutiveInvalidReadCount;
        log.LogError ($"ProcessResults: none of the {m_readNodes.Count} nodes returned a valid value ({this.ConsecutiveInvalidReadCount} consecutive invalid read operations), first status code is {results[0].StatusCode}");
      }
      else {
        if ((0 < invalidCount) && log.IsEnabled (LogLevel.Warning)) {
          log.LogWarning ($"ProcessResults: {invalidCount}/{m_readNodes.Count} nodes did not return a valid value");
        }
        this.ConsecutiveInvalidReadCount = 0;
      }
    }

    void LogResult (DataValue result, string identifier)
    {
      try {
        if (result.Value == null) {
          log.LogWarning ($"LogResult: received a null value for node id {identifier} ({result.StatusCode})");
        }
        else if (result.Value.GetType ().IsArray) {
          // Convert as array and display the first element if possible
          if (!(result.Value is object[] resultArray)) {
            log.LogError ($"LogResult: conversion error to array for {result.Value}, id={identifier} ({result.StatusCode})");
          }
          else { // Not null
            switch (resultArray.Length) {
            case 0:
              log.LogError ($"LogResult: empty array {result.Value}, id={identifier} ({result.StatusCode})");
              break;
            case 1:
              if (log.IsEnabled (LogLevel.Debug)) {
                log.LogDebug ($"LogResult: array with a unique element {resultArray[0]} for id={identifier} ({result.StatusCode}");
              }
              break;
            default:
              log.LogError ($"LogResult: too many values in {result.Value}, id={identifier} ({result.StatusCode})");
              break;
            }
          }
        }
        else {
          log.LogInformation ($"LogResult: read {identifier}={result.Value} ({result.StatusCode})");
        }
      }
      catch (Exception ex) {
        log.LogError (ex, $"LogResult: log error, {ex.Message}");
      }
    }

    /// <summary>
    /// Get a description of the status code that was returned by the server for a node identifier
    /// </summary>
    /// <param name="nodeIdentifier"></param>
    /// <returns></returns>
    string GetStatusCodeDescription (string nodeIdentifier)
    {
      if (m_statusCodesByNodeId.TryGetValue (nodeIdentifier, out var statusCode)) {
        return statusCode.ToString ();
      }
      else {
        return "UnknownStatusCode";
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
        log.LogError ($"Get: no valid not identifier for {parameter}");
        throw new Exception ($"{parameter} has no corresponding valid node identifier");
      }

      // Get the result
      if (!m_resultsByNodeId.TryGetValue (nodeIdentifier, out var result)) {
        log.LogInformation ($"Get: {parameter} id={nodeIdentifier} is not ready yet");
        throw new Exception ("Value is not ready yet");
      }

      if (result is null) {
        var statusCode = GetStatusCodeDescription (nodeIdentifier);
        log.LogError ($"Get: value for {parameter} id={nodeIdentifier} is null with the status code {statusCode}");
        throw new Exception ($"Null value with the status code {statusCode}");
      }

      // Convert the value as string
      if (result.GetType ().IsArray) {
        // Convert as array and take the first element if possible
        if (!(result is object[] resultArray)) {
          log.LogError ($"Get: conversion error to array for {result}, parameter={parameter} id={nodeIdentifier}");
          throw new InvalidCastException ("Array conversion error");
        }

        switch (resultArray.Length) {
        case 0:
          log.LogError ($"Get: empty array {result}, parameter={parameter} id={nodeIdentifier}");
          throw new Exception ("Empty array");
        case 1:
          if (log.IsEnabled (LogLevel.Debug)) {
            log.LogDebug ($"Get: return first array element {resultArray[0]} for parameter{parameter} id={nodeIdentifier}");
          }
          return resultArray[0];
        default:
          log.LogError ($"Get: too many values in {result}, parameter={parameter} id={nodeIdentifier}");
          throw new Exception ("Too many values in array");
        }
      }
      else {
        if (log.IsEnabled (LogLevel.Debug)) {
          log.LogDebug ($"Get: return {result} for parameter{parameter} id={nodeIdentifier}");
        }
        return result;
      }
    }

  }
}
