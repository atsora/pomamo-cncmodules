// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections.Generic;
using Lemoine.Core.Log;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Description of TagManager.
  /// </summary>
  internal sealed class TagManager : IDisposable
  {
    #region Members
    readonly IDictionary<string, Tag> m_tags = new Dictionary<string, Tag> ();
    PlcTagDebugLevel m_debugLevel = PlcTagDebugLevel.None;
    ILog m_logger = LogManager.GetLogger (typeof (EthernetIP).FullName);
    ILog m_plcLogger = LogManager.GetLogger (typeof (EthernetIP).FullName + ".PlcTag");
    #endregion // Members

    #region Getters / Setters
    /// <summary>
    /// Type of the PLC protocol
    /// </summary>
    public Protocol Protocol { get; set; } = Protocol.AB_EIP;

    /// <summary>
    /// IP address or host name (and optional port for Modbus)
    /// 
    /// Required
    /// 
    /// AB: This tells the library what host name or IP address to use for the PLC or the gateway to the PLC (in the case that the PLC is remote).
    /// 
    /// Modbus: This tells the library what host name or IP address to use for the PLC.
    /// Can have an optional port at the end, e.g. gateway=10.1.2.3:502 where the :502 part specifies the port.
    /// </summary>
    public string Gateway { get; set; }

    /// <summary>
    /// CPU type, can be "LGX", "SLC" or "PLC5"
    /// </summary>
    public PlcType Plc { get; set; }

    /// <summary>
    /// AB: Required/Optional Only for PLCs with additional routing
    /// 
    /// Modbus: Required The server/unit ID. Must be an integer value between 0 and 255.
    /// 
    /// AB: This attribute is required for CompactLogix/ControlLogix tags and for tags using a DH+ protocol bridge (i.e. a DHRIO module) to get to a PLC/5, SLC 500, or MicroLogix PLC on a remote DH+ link.
    /// The attribute is ignored if it is not a DH+ bridge route, but will generate a warning if debugging is active.
    /// Note that Micro800 connections must not have a path attribute.
    /// 
    /// Modbus: Servers may support more than one unit or may bridge to other units. Example: path=4 for accessing device unit ID 4.
    /// 
    /// Required for LGX, Optional for PLC/SLC/MLGX IOI path to access the PLC from the gateway.
    /// Communication Port Type: 1- Backplane, 2- Control Net/Ethernet, DH+ Channel A, DH+ Channel B, 3- Serial
    /// Slot number where cpu is installed: 0,1..
    /// </summary>
    public string Path { get; set; }

    /// <summary>
    /// Get/set the debug level
    /// </summary>
    public PlcTagDebugLevel DebugLevel
    {
      get => m_debugLevel;
      set {
        m_debugLevel = value;
        if (this.Logger.IsDebugEnabled) {
          this.Logger.Debug ($"DebugLevel.set: about to set {value}");
        }
        try {
          LibPlcTag.SetDebugLevel (value);
        }
        catch (Exception ex) {
          this.Logger.Error ($"DebugLevel.set: SetDebugLevel {value} failed", ex);
        }
      }
    }

    /// <summary>
    /// Maximum time for reading or writing a node, in ms
    /// Default is 500 ms
    /// </summary>
    public int TimeOut { get; set; }

    /// <summary>
    /// Logger
    /// </summary>
    public ILog Logger
    {
      get => m_logger;
      set {
        m_logger = value;
        m_plcLogger = LogManager.GetLogger (m_logger.Name + ".PlcTag");
      }
    }
    #endregion // Getters / Setters

    /// <summary>
    /// Constructor
    /// </summary>
    public TagManager ()
    {
      if (this.Logger.IsDebugEnabled) {
        this.Logger.Debug ($"TagManager: about to register the logger");
      }
      try {
        var plcTagLogger = new LibPlcTag.LogCallbackFunc (LogPlcTag);
        var result = LibPlcTag.RegisterLogger (plcTagLogger);
        if (LibPlcTag.StatusCode.STATUS_OK != result) {
          this.Logger.Error ($"TagManager: RegisterLogger failed with error {result}");
        }
      }
      catch (Exception ex) {
        this.Logger.Error ($"TagManager: exception in registering the logger", ex);
      }
    }

    /// <summary>
    /// <see cref="IDisposable"/>
    /// </summary>
    public void Dispose ()
    {
      if (this.Logger.IsDebugEnabled) {
        this.Logger.Debug ($"Dispose: about to shutdown LibPlcTag");
      }
      LibPlcTag.Shutdown ();
    }

    /// <summary>
    /// Log
    /// </summary>
    /// <param name="tag"></param>
    /// <param name="debugLevel"></param>
    /// <param name="message"></param>
    public void LogPlcTag (int tag, PlcTagDebugLevel debugLevel, string message)
    {
      switch (debugLevel) {
      case PlcTagDebugLevel.None:
        break;
      case PlcTagDebugLevel.Error:
        m_plcLogger.Error ($"{tag}: {message}");
        break;
      case PlcTagDebugLevel.Warn:
        m_plcLogger.Warn ($"{tag}: {message}");
        break;
      case PlcTagDebugLevel.Info:
        m_plcLogger.Info ($"{tag}: {message}");
        break;
      case PlcTagDebugLevel.Detail:
        m_plcLogger.Debug ($"{tag}: {message}");
        break;
      case PlcTagDebugLevel.Spew:
        m_plcLogger.Debug ($"[Spew] {tag}: {message}");
        break;
      }
    }

    #region Methods
    /// <summary>
    /// Start of a new acquisition
    /// </summary>
    public void Start ()
    {
      foreach (var tag in m_tags.Values) {
        tag.Start ();
      }
    }

    /// <summary>
    /// End of the acquisition: a new reading is ordered
    /// </summary>
    public void Finish ()
    {
      foreach (var tag in m_tags.Values) {
        tag.Finish ();
      }
    }

    /// <summary>
    /// Get a tag
    /// Possibly create a tag first if it's not stored yet
    /// </summary>
    /// <param name="tagName">tag name</param>
    /// <param name="elementCount">number of element to read</param>
    /// <param name="elementSize">size of an element (1 for byte, 2 for int16, ...)</param>
    /// <param name="type">Type of the value to read</param>
    /// <returns></returns>
    public Tag GetTag (string tagName, int elementCount, int elementSize, Type type)
    {
      if (tagName is null) {
        this.Logger.Fatal ($"GetTag: tagName is null");
        throw new ArgumentNullException ("tagName");
      }
      if (!m_tags.ContainsKey (tagName)) {
        var tag = CreateTag (tagName, elementCount, elementSize, type);
        m_tags[tagName] = tag;
        return tag;
      }
      else {
        var tag = m_tags[tagName];
        // Check that the type is the same than the existing tag
        if (tag.Type != type) {
          this.Logger.Error ($"GetTag: the type must be unique within a tag");
          throw new Exception ("EthernetIP - The type must be unique within a tag");
        }
        return tag;
      }
    }

    string BuildKey (string tagName, int elementCount, int elementSize = -1, int debugLevel = 0)
    {
      if (this.Protocol.Equals (Protocol.AB_EIP)) {
        if (this.Plc.Equals (PlcType.CONTROLLOGIX) && string.IsNullOrEmpty (this.Path)) {
          this.Logger.Error ($"BuildKey: control logix without path");
        }
        if (this.Plc.Equals (PlcType.MICRO800) && !string.IsNullOrEmpty (this.Path)) {
          this.Logger.Error ($"BuildKey: micro800 with a path");
        }
      }

      var key = $"protocol={this.Protocol.ToString ().ToLowerInvariant ()}&gateway={this.Gateway}";
      if (!string.IsNullOrEmpty (this.Path)) {
        key += "&path=" + this.Path;
      }

      var plcString = this.Plc.ToString ().Replace ('_', '-').ToLowerInvariant ();
      key += $"&plc={plcString}";
      if (1 <= elementSize) {
        key += $"&elem_size={elementSize}";
      }
      key += $"&name={tagName}";
      if (debugLevel > 0) {
        key += $"&debug={debugLevel}";
      }

      // Result can be "protocol=ab_eip&gateway=192.168.0.100&cpu=SLC&elem_size={ELEM_SIZE}&elem_count={ELEM_COUNT}&name=F8:0&debug=1"
      return key;
    }

    Tag CreateTag (string tagName, int elementCount, int elementSize, Type type)
    {
      // Compute the key
      var key = BuildKey (tagName, elementCount, elementSize);

      // Create a new tag
      var tag = new Tag (Logger, tagName, key) {
        Type = type,
        ElementSize = elementSize,
        TimeOut = TimeOut
      };

      return tag;
    }

    /// <summary>
    /// Set a tag as a read-once tag
    /// </summary>
    /// <param name="tagName"></param>
    /// <param name="elementCount"></param>
    /// <param name="elementSize"></param>
    /// <param name="type"></param>
    public void SetReadOnce (string tagName, int elementCount, int elementSize, Type type)
    {
      var tag = GetTag (tagName, elementCount, elementSize, type);
      tag.ReadOnce = true;
    }

    #endregion // Methods
  }
}
