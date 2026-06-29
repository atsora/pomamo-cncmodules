// Copyright (C) 2009-2023 Lemoine Automation Technologies
// Copyright (C) 2026 Atsora Solutions
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.XPath;

namespace Lemoine.Cnc
{
  /// <summary>
  /// MTConnect input module
  /// </summary>
  public partial class MTConnect
  {
    static readonly string DEFAULT_MTCONNECTASSETS_NAMESPACE_PREFIX = "a";
    static readonly string DEFAULT_MTCONNECTASSETS_NAMESPACE = "urn:mtconnect.org:MTConnectAssets:1.3";

    string m_mtconnectAssetsPrefix = DEFAULT_MTCONNECTASSETS_NAMESPACE_PREFIX;
    XPathNavigator m_assetsNavigator;
    XmlNamespaceManager m_assetsNs = null;
    bool m_assetsOk = false;
    bool m_toolLifeManagement = false;
    bool m_useToolNumberAsToolPot = false;

    /// <summary>
    /// True if tool life management is enabled
    /// (part "assets" will be read)
    /// </summary>
    public string ToolLifeManagement
    {
      get => m_toolLifeManagement ? "True" : "False";
      set {
        m_toolLifeManagement = string.Equals (value, "True", StringComparison.CurrentCultureIgnoreCase);
      }
    }

    /// <summary>
    /// MTConnectStreams namespace prefix to use for assets (tool life data)
    /// Default is "a"
    /// </summary>
    public string MTConnectAssetsPrefix
    {
      get => m_mtconnectAssetsPrefix;
      set { m_mtconnectAssetsPrefix = value; }
    }

    /// <summary>
    /// Get an object fully describing the life of all tools
    /// </summary>
    public ToolLifeData ToolLifeData
    {
      get {
        if (!m_assetsOk) {
          log.ErrorFormat ("ToolLifeData: assets were not read");
          throw new Exception ("ToolLifeData: assets were not read");
        }

        return GetToolManagementData ();
      }
    }

    /// <summary>
    /// if true, the pot number is overwritten by the tool number
    /// 
    /// </summary>
    public bool UseToolNumberAsToolPot
    {
      get => m_useToolNumberAsToolPot;
      set { m_useToolNumberAsToolPot = value; }
    }

    #region Private methods
    void StartAssets ()
    {
      m_assetsOk = false;

      // If tool life not needed, we return immediately
      if (!m_toolLifeManagement) {
        return;
      }

      try {
        // Create a navigator for the assets
        var document = new XPathDocument (Url.Replace ("current", "assets"));
        m_assetsNavigator = document.CreateNavigator ();

        // Namespace
        m_assetsNs = CreateNamespace (m_assetsNavigator, m_mtconnectAssetsPrefix, DEFAULT_MTCONNECTASSETS_NAMESPACE);

        m_assetsOk = true;
      }
      catch (Exception ex) {
        var path = Url.Replace ("current", "assets");
        log.Error ($"MTConnect: failed to create the asset document using path {path}", ex);
      }
    }

    ToolLifeData GetToolManagementData ()
    {
      var toolLifeData = new ToolLifeData ();

      // Get all cutting tool nodes
      string path = "//" + MTConnectAssetsPrefix + ":CuttingTool";
      XPathNodeIterator nodeIterator = (m_assetsNs == null) ?
        m_assetsNavigator.Select (path) :
        m_assetsNavigator.Select (path, m_assetsNs);

      // Process them
      log.DebugFormat ("MTConnect: got {0} CuttingTool from url {1}", nodeIterator.Count, m_assetsNavigator.BaseURI);
      while (nodeIterator.MoveNext ()) {
        ProcessCuttingTool (toolLifeData, nodeIterator.Current);
      }

      return toolLifeData;
    }

    void ProcessCuttingTool (ToolLifeData toolLifeData, XPathNavigator cuttingToolNavigator)
    {
      // Tool id
      string toolId = cuttingToolNavigator.GetAttribute ("serialNumber", "");

      // Get all CuttingToolLifeCycle nodes
      string path = MTConnectAssetsPrefix + ":CuttingToolLifeCycle";
      XPathNodeIterator nodeIterator = (m_assetsNs == null) ?
        cuttingToolNavigator.Select (path) :
        cuttingToolNavigator.Select (path, m_assetsNs);

      // Process them
      log.DebugFormat ("MTConnect: got {0} CuttingToolLifeCycle", nodeIterator.Count);
      int pos = 0;
      while (nodeIterator.MoveNext ()) {
        ProcessCuttingToolLifeCycle (toolLifeData, nodeIterator.Current, toolId +
                                    (pos > 0 ? "." + pos : ""));
        pos++; // Just in case there are several "CuttingToolLifeCycle"
      }
    }

    void ProcessCuttingToolLifeCycle (ToolLifeData toolLifeData, XPathNavigator ctlcNavigator, string toolId)
    {
      try {
        // Tool number
        string val = GetValidValue (
          m_assetsNs == null ?
          ctlcNavigator.SelectSingleNode (MTConnectAssetsPrefix + ":ProgramToolNumber") :
          ctlcNavigator.SelectSingleNode (MTConnectAssetsPrefix + ":ProgramToolNumber", m_assetsNs));
        int toolNumber = int.Parse (val);

        // Pot number
        int toolPot = 0;
        int toolMagazine = 0;
        try {
          val = GetValidValue (
            m_assetsNs == null ?
            ctlcNavigator.SelectSingleNode (MTConnectAssetsPrefix + ":Location[@type='POT' or @type='STATION']") :
            ctlcNavigator.SelectSingleNode (MTConnectAssetsPrefix + ":Location[@type='POT' or @type='STATION']", m_assetsNs));
          toolPot = int.Parse (val);

          // Magazine number
          val = GetValidValue (
            m_assetsNs == null ?
            ctlcNavigator.SelectSingleNode (MTConnectAssetsPrefix + ":ProgramToolGroup") :
            ctlcNavigator.SelectSingleNode (MTConnectAssetsPrefix + ":ProgramToolGroup", m_assetsNs));
          toolMagazine = int.Parse (val);
        }
        catch (Exception ex) {
          log.Error ($"MTConnect toollife: couldn't parse the position of T{toolNumber}", ex);
        }

        // Status
        var status = GetStatus (ctlcNavigator);

        // Store the position
        int toolIndex = toolLifeData.ToolNumber;
        toolLifeData.AddTool ();
        toolLifeData[toolIndex].ToolNumber = toolNumber.ToString ();
        toolLifeData[toolIndex].ToolId = toolId;
        if (m_useToolNumberAsToolPot) {
          toolLifeData[toolIndex].PotNumber = toolNumber;
        }
        else {
          toolLifeData[toolIndex].PotNumber = toolPot;
        }
        toolLifeData[toolIndex].MagazineNumber = toolMagazine;
        toolLifeData[toolIndex].ToolState = status;

        // Find "ToolLife" elements and process them
        string path = MTConnectAssetsPrefix + ":ToolLife";
        XPathNodeIterator nodeIterator = (m_assetsNs == null) ?
          ctlcNavigator.Select (path) :
          ctlcNavigator.Select (path, m_assetsNs);
        try {
          log.DebugFormat ("MTConnect: got {0} ToolLife", nodeIterator.Count);
          while (nodeIterator.MoveNext ()) {
            ProcessToolLife (toolLifeData, toolIndex, nodeIterator.Current);
          }
        }
        catch (Exception ex) {
          log.Error ("MTConnect.ProcessCuttingToolLifeCycle: couldn't read tool life", ex);
        }
      }
      catch (Exception e) {
        log.ErrorFormat ("MTConnect.ProcessCuttingToolLifeCycle: couldn't read: {0}", e);
      }
    }

    Lemoine.Core.SharedData.ToolState GetStatus (XPathNavigator ctlcNavigator)
    {
      // Retrieve the different status
      string path = MTConnectAssetsPrefix + ":CutterStatus/" + MTConnectAssetsPrefix + ":Status";
      XPathNodeIterator nodeIterator = (m_assetsNs == null) ?
        ctlcNavigator.Select (path) :
        ctlcNavigator.Select (path, m_assetsNs);

      var status = new List<string> ();
      while (nodeIterator.MoveNext ()) {
        status.Add (nodeIterator.Current.Value);
      }

      // Translate into a ToolState
      var translatedStatus = Lemoine.Core.SharedData.ToolState.Unknown;
      foreach (string stat in status) {
        switch (stat) {
          case "NEW":
            translatedStatus = Lemoine.Core.SharedData.ToolState.New;
            break;
          case "AVAILABLE":
            translatedStatus = Lemoine.Core.SharedData.ToolState.Available;
            break;
          case "UNAVAILABLE":
            translatedStatus = Lemoine.Core.SharedData.ToolState.DefinitelyUnavailable;
            break;
          case "ALLOCATED":
            translatedStatus = Lemoine.Core.SharedData.ToolState.Reserved;
            break;
          case "UNALLOCATED":
            // Nothing
            break;
          case "MEASURED":
            translatedStatus = Lemoine.Core.SharedData.ToolState.Measurement;
            break;
          case "RECONDITIONED":
            translatedStatus = Lemoine.Core.SharedData.ToolState.Reconditioning;
            break;
          case "NOT_REGISTERED":
            translatedStatus = Lemoine.Core.SharedData.ToolState.NotRegistered;
            break;
          case "USED":
            translatedStatus = Lemoine.Core.SharedData.ToolState.Used;
            break;
          case "EXPIRED":
            translatedStatus = Lemoine.Core.SharedData.ToolState.Expired;
            break;
          case "BROKEN":
            break;
          default:
          case "UNKNOWN":
            translatedStatus = Lemoine.Core.SharedData.ToolState.Unknown;
            break;
        }
      }

      return translatedStatus;
    }

    void ProcessToolLife (ToolLifeData toolLifeData, int toolIndex, XPathNavigator ctlcNavigator)
    {
      // Direction (mandatory)
      string directionStr = ctlcNavigator.GetAttribute ("countDirection", "");
      if (String.IsNullOrEmpty (directionStr)) {
        log.Warn ("MTConnect.ProcessToolLife: no direction provided");
        return;
      }
      var direction = Lemoine.Core.SharedData.ToolLifeDirection.Unknown;
      switch (directionStr) {
        case "DOWN":
          direction = Lemoine.Core.SharedData.ToolLifeDirection.Down;
          break;
        case "UP":
          direction = Lemoine.Core.SharedData.ToolLifeDirection.Up;
          break;
        default:
          log.WarnFormat ("MTConnect.ProcessToolLife: unknown direction '{0}' provided", directionStr);
          return;
      }

      // Type
      string typeStr = ctlcNavigator.GetAttribute ("type", "");
      var type = Lemoine.Core.SharedData.ToolUnit.Unknown;
      if (!String.IsNullOrEmpty (typeStr)) {
        switch (typeStr) {
          case "MINUTES":
            type = Lemoine.Core.SharedData.ToolUnit.TimeSeconds; // then multiplication by 60
            break;
          case "PART_COUNT":
            type = Lemoine.Core.SharedData.ToolUnit.Parts;
            break;
          case "WEAR":
            type = Lemoine.Core.SharedData.ToolUnit.Wear;
            break;
          default:
            log.WarnFormat ("MTConnect.ProcessToolLife: unknown type '{0}' provided", typeStr);
            break;
        }
      }

      // Limit
      double? limit = null;
      if (direction == Lemoine.Core.SharedData.ToolLifeDirection.Down) {
        string limitStr = ctlcNavigator.GetAttribute ("initial", "");
        if (string.IsNullOrEmpty (limitStr)) {
          limitStr = ctlcNavigator.GetAttribute ("limit", "");
        }

        if (!string.IsNullOrEmpty (limitStr)) {
          limit = double.Parse (limitStr);
        }
      }
      else {
        string limitStr = ctlcNavigator.GetAttribute ("limit", "");
        if (!string.IsNullOrEmpty (limitStr)) {
          limit = double.Parse (limitStr);
        }
      }

      // Warning
      string warningStr = ctlcNavigator.GetAttribute ("warning", "");
      double? warning = null;
      if (!string.IsNullOrEmpty (warningStr)) {
        warning = double.Parse (warningStr);
      }

      // Determine the offset for the warning
      if (direction == Lemoine.Core.SharedData.ToolLifeDirection.Up &&
          limit.HasValue && warning.HasValue) {
        warning = limit.Value - warning.Value;
      }

      // Current
      string currentStr = ctlcNavigator.InnerXml;
      double current = double.Parse (ctlcNavigator.Value);
      if (type == Lemoine.Core.SharedData.ToolUnit.TimeSeconds) {
        current *= 60;
      }

      int toolLifeIndex = toolLifeData[toolIndex].LifeDescriptionNumber;
      toolLifeData[toolIndex].AddLifeDescription ();
      toolLifeData[toolIndex][toolLifeIndex].LifeDirection = direction;
      toolLifeData[toolIndex][toolLifeIndex].LifeType = type;
      toolLifeData[toolIndex][toolLifeIndex].LifeWarningOffset = warning;
      toolLifeData[toolIndex][toolLifeIndex].LifeLimit = limit;
      toolLifeData[toolIndex][toolLifeIndex].LifeValue = current;
    }
    #endregion // Private methods
  }
}
