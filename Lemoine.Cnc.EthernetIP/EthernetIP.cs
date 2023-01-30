// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Type of the PLC protocol
  /// </summary>
  public enum Protocol
  {
    /// <summary>
    /// Allen-Bradley PLC
    /// </summary>
    AB_EIP,

    /// <summary>
    /// Modbus TCP PLC
    /// </summary>
    MODBUS_TCP,
  }

  /// <summary>
  /// Cpu type
  /// </summary>
  public enum PlcType
  {
    /// <summary>
    /// Control Logix-class PLC
    /// </summary>
    CONTROLLOGIX,

    /// <summary>
    /// PLC/5 PLC
    /// </summary>
    PLC5,

    /// <summary>
    /// SLC 500 PLC
    /// </summary>
    SLC500,

    /// <summary>
    /// Control Logix-class PLC using the PLC/5 protocol
    /// </summary>
    LOGIXPCCC,

    /// <summary>
    /// Micro800-class PLC
    /// </summary>
    MICRO800,

    /// <summary>
    /// Micrologix PLC
    /// </summary>
    MICROLOGIX,

    /// <summary>
    /// Omron NJ/NX PLC
    /// </summary>
    OMRON_NJNX,

    /// <summary>
    /// Control / Compact Logix
    /// 
    /// Deprecated
    /// </summary>
    LGX = CONTROLLOGIX,

    /// <summary>
    /// SLC
    /// 
    /// Deprecated
    /// </summary>
    SLC = SLC500,
  }

  /// <summary>
  /// EthernetIP input module
  /// </summary>
  public sealed partial class EthernetIP : BaseCncModule, ICncModule, IDisposable
  {
    static readonly int TIMEOUT_MS_DEFAULT = 500;

    #region Members
    readonly TagManager m_tagManager = new TagManager ();
    bool m_acquisitionError = false;
    int m_operationErrors = 0;
    int m_invalidTags = 0;
    int m_successfulReadAttempts = 0; // Note: it can be approximative
    readonly IDictionary<string, object> m_cache = new Dictionary<string, object> ();
    #endregion

    #region Getters / Setters
    /// <summary>
    /// Gateway (previously IPAddress)
    /// </summary>

    public string Gateway
    {
      get {
        return m_tagManager.Gateway;
      }
      set {
        m_tagManager.Gateway = value;
      }
    }

    /// <summary>
    /// Protocol
    /// 
    /// Default: 
    /// </summary>
    public string Protocol
    {
      get => m_tagManager.Protocol.ToString ();
      set {
        m_tagManager.Protocol = (Lemoine.Cnc.Protocol)Enum.Parse (typeof (Lemoine.Cnc.Protocol), value);
      }
    }

    /// <summary>
    /// Plc type:
    /// <item>controllogix</item>
    /// <item>plc5</item>
    /// <item>slc500</item>
    /// <item>logixpccc</item>
    /// <item>micro800</item>
    /// <item>micrologix</item>
    /// <item>omron-njnx</item>
    /// 
    /// Previously Cpu
    /// </summary>
    public string Plc
    {
      get {
        return m_tagManager.Plc.ToString ();
      }
      set {
        m_tagManager.Plc = (PlcType)Enum.Parse (typeof (PlcType), value);
      }
    }

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
    public string Path
    {
      get {
        return m_tagManager.Path;
      }
      set {
        m_tagManager.Path = value;
      }
    }

    /// <summary>
    /// Get/Set the debug level
    /// </summary>
    public string DebugLevel
    {
      get {
        return m_tagManager.DebugLevel.ToString ();
      }
      set {
        m_tagManager.DebugLevel = (PlcTagDebugLevel)Enum.Parse (typeof (PlcTagDebugLevel), value);
      }
    }

    /// <summary>
    /// Maximum time for reading or writing a node, in ms
    /// Default is 500 ms
    /// </summary>
    public int TimeOut
    {
      get {
        return m_tagManager.TimeOut;
      }
      set {
        if (value <= 0) {
          log.Info ($"TimeOut.set: value {value} is negative, skip this value and use the default value {this.TimeOut}");
        }
        else {
          m_tagManager.TimeOut = value;
        }
      }
    }

    /// <summary>
    /// Should the thread be restarted in case some of some errors:
    /// <item>ERR_TIMEOUT</item>
    /// <item>ERR_WRITE</item>
    /// </summary>
    public bool ErrorRequiresRestart { get; set; } = false;

    /// <summary>
    /// Global acquisition error
    /// </summary>
    public bool AcquisitionError => m_acquisitionError;

    /// <summary>
    /// Number of tag operations in (remote) error
    /// </summary>
    public int OperationErrors => m_operationErrors;

    /// <summary>
    /// Number of invalid tags
    /// </summary>
    public int InvalidTags => m_invalidTags;

    /// <summary>
    /// Return true if no data could be read
    /// </summary>
    public bool NoDataAcquired => 0 == m_successfulReadAttempts;
    #endregion

    #region Constructors, destructor
    /// <summary>
    /// Default constructor
    /// </summary>
    public EthernetIP () : base ("Lemoine.Cnc.In.EthernetIP")
    {
      TimeOut = TIMEOUT_MS_DEFAULT;
    }

    /// <summary>
    /// <see cref="IDisposable.Dispose" />
    /// </summary>
    public void Dispose ()
    {
      m_tagManager.Dispose ();

      GC.SuppressFinalize (this);
    }
    #endregion // Constructor, destructor

    #region Methods
    /// <summary>
    /// Start method, start of the acquisition
    /// </summary>
    public void Start ()
    {
      // Initialize the logger here
      m_tagManager.Logger = log;

      if (log.IsDebugEnabled) {
        log.Debug ("Start");
      }
      m_invalidTags = 0;
      m_acquisitionError = false;
      m_successfulReadAttempts = 0;
      m_operationErrors = 0;
      m_tagManager.Start ();
    }

    /// <summary>
    /// Finish method, end of the acquisition
    /// </summary>
    public void Finish ()
    {
      if (log.IsDebugEnabled) {
        log.Debug ("Finish");
      }
      m_tagManager.Finish ();
    }

    /// <summary>
    /// Push in cache
    /// </summary>
    /// <param name="data"></param>
    /// <param name="param">tag name (not null or empty)</param>
    public void PushInCache (object data, string param)
    {
      Debug.Assert (!string.IsNullOrEmpty (param));

      m_cache[param] = data;
    }

    /// <summary>
    /// Try to read a value in cache
    /// </summary>
    /// <param name="key"></param>
    /// <param name="v">value in cache</param>
    /// <returns>success</returns>
    internal bool TryInCache (string key, out object v)
    {
      if (m_cache.ContainsKey (key)) {
        log.Info ($"TryInCache: get value for {key} in cache");
        v = m_cache[key];
        return true;
      }
      else {
        v = null;
        return false;
      }
    }

    /// <summary>
    /// Try to read a value in cache
    /// </summary>
    /// <param name="key"></param>
    /// <param name="v">value in cache</param>
    /// <returns>success</returns>
    internal bool TryInCache<T> (string key, out T v)
    {
      if (m_cache.ContainsKey (key)) {
        log.Info ($"TryInCache: get value for {key} in cache");
        v = (T)m_cache[key];
        return true;
      }
      else {
        v = default;
        return false;
      }
    }

    void ProcessException (Exception ex)
    {
       if (ex is ErrorCodeException errorCodeException) {
        if (log.IsDebugEnabled) {
          log.Debug ("ProcessException: error code exception", errorCodeException);
        }

        // Possibly performs things depending on the ErrorCode of the exception
        switch (errorCodeException.ErrorCode) {
        // Device errors
        case LibPlcTag.StatusCode.ERR_BAD_CONFIG: // Usually on the remote device
        case LibPlcTag.StatusCode.ERR_BAD_STATUS:
        case LibPlcTag.StatusCode.ERR_OPEN:
        case LibPlcTag.StatusCode.ERR_BAD_DEVICE:
        case LibPlcTag.StatusCode.ERR_BAD_GATEWAY:
          log.Error ($"ProcessException: device error {errorCodeException.ErrorCode} for key {errorCodeException.Key} => skip further readings and turn on the acquisition error flag", errorCodeException);
          m_acquisitionError = true;
          break;

        // Invalid tag (or wrong parameter)
        case LibPlcTag.StatusCode.ERR_BAD_PARAM:
        case LibPlcTag.StatusCode.ERR_NOT_FOUND:
          log.Fatal ($"ProcessException: invalid tag error {errorCodeException.ErrorCode} for key {errorCodeException.Key}", errorCodeException);
          ++m_invalidTags;
          break;

        // Normal states (Waiting/Ok) for a specific tag
        case LibPlcTag.StatusCode.STATUS_OK:
        case LibPlcTag.StatusCode.INITIALIZING:
          break;

        // Pending
        case LibPlcTag.StatusCode.STATUS_PENDING:
          log.Info ($"ProcessException: status pending with code {errorCodeException.ErrorCode} for key {errorCodeException.Key}", errorCodeException);
          break;

        // Internal library error
        // For most of them, it would be very unusual to see them
        case LibPlcTag.StatusCode.ERR_MUTEX_DESTROY:
        case LibPlcTag.StatusCode.ERR_MUTEX_INIT:
        case LibPlcTag.StatusCode.ERR_MUTEX_LOCK:
        case LibPlcTag.StatusCode.ERR_MUTEX_UNLOCK:
        case LibPlcTag.StatusCode.ERR_NULL_PTR:
        case LibPlcTag.StatusCode.ERR_THREAD_CREATE:
        case LibPlcTag.StatusCode.ERR_THREAD_JOIN:
          log.Fatal ($"ProcessException: internal library error {errorCodeException.ErrorCode} for key {errorCodeException.Key} => abort (sleep)", errorCodeException);
          System.Threading.Thread.Sleep (System.Threading.Timeout.Infinite);
          break;

        // Resource problem
        // An error occurred trying to close/create some (internal) resource
        case LibPlcTag.StatusCode.ERR_CLOSE:
        case LibPlcTag.StatusCode.ERR_CREATE:
        case LibPlcTag.StatusCode.ERR_NO_MEM:
        case LibPlcTag.StatusCode.ERR_NO_RESOURCES:
          log.Fatal ($"ProcessException: resource error {errorCodeException.ErrorCode} for key {errorCodeException.Key} => abort (sleep)", errorCodeException);
          System.Threading.Thread.Sleep (System.Threading.Timeout.Infinite);
          break;

        // Operation error probably related to a connection problem (temporary)
        case LibPlcTag.StatusCode.ERR_BAD_CONNECTION:
        case LibPlcTag.StatusCode.ERR_BAD_REPLY:
        case LibPlcTag.StatusCode.ERR_READ:
        case LibPlcTag.StatusCode.ERR_WINSOCK:
        case LibPlcTag.StatusCode.ERR_PARTIAL:
        case LibPlcTag.StatusCode.ERR_BUSY:
          log.Error ($"ProcessException: connection error {errorCodeException.ErrorCode} for key {errorCodeException.Key} => skip further readings and turn on the acquisition error flag",
            errorCodeException);
          m_acquisitionError = true;
          break;

        case LibPlcTag.StatusCode.ERR_WRITE:
        case LibPlcTag.StatusCode.ERR_TIMEOUT:
          log.Error ($"ProcessException: connection error {errorCodeException.ErrorCode} for key {errorCodeException.Key} => skip further readings and turn on the acquisition error flag",
            errorCodeException);
          m_acquisitionError = true;
          if (this.ErrorRequiresRestart) {
            log.Fatal ($"ProcessException: connection error requires restart on key {errorCodeException.Key} => abort (sleep)", errorCodeException);
            m_tagManager?.Dispose ();
            System.Threading.Thread.Sleep (System.Threading.Timeout.Infinite);
          }
          break;

        // Operation error (it may be on a valid tag) with probably a valid connection
        case LibPlcTag.StatusCode.ERR_ABORT:
        case LibPlcTag.StatusCode.ERR_BAD_DATA:
        case LibPlcTag.StatusCode.ERR_DUPLICATE:
        case LibPlcTag.StatusCode.ERR_ENCODE:
        case LibPlcTag.StatusCode.ERR_NO_DATA:
        case LibPlcTag.StatusCode.ERR_OUT_OF_BOUNDS:
        case LibPlcTag.StatusCode.ERR_TOO_LARGE:
        case LibPlcTag.StatusCode.ERR_TOO_SMALL:
        case LibPlcTag.StatusCode.ERR_REMOTE_ERR:
          // Tag in error
          log.Error ($"ProcessException: operation error {errorCodeException.ErrorCode} for key {errorCodeException.Key}", errorCodeException);
          ++m_operationErrors;
          break;

        // Permanent operation error (on a valid tag)
        case LibPlcTag.StatusCode.ERR_NOT_ALLOWED:
        case LibPlcTag.StatusCode.ERR_NOT_IMPLEMENTED:
        case LibPlcTag.StatusCode.ERR_UNSUPPORTED:
          // Tag in error
          log.Fatal ($"ProcessException: permanent operation error {errorCodeException.ErrorCode} for key {errorCodeException.Key}", errorCodeException);
          ++m_operationErrors;
          break;

        // Unknown
        default:
          log.Fatal ($"ProcessException: unknown  error {errorCodeException.ErrorCode} for key {errorCodeException.Key} but continue", errorCodeException);
          ++m_operationErrors;
          break;
        }
      }
      else {
        log.Error ($"ProcessException: not an ErrorCodeException, {ex.Message}", ex);
      }
    }
    #endregion // Methods
  }
}
