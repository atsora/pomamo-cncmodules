// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using Lemoine.Core.Log;
using System.Diagnostics;
using System.Text;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Description of Tag.
  /// </summary>
  internal class Tag : IDisposable
  {
    static readonly TimeSpan STATUS_OK_TIMEOUT_DEFAULT = TimeSpan.FromMilliseconds (300);
    static readonly int PENDING_TIME_OUT_DEFAULT = 3 * 60; // 3 minutes

    readonly IntPtr m_tag;
    readonly string m_name;
    readonly string m_key;
    LibPlcTag.StatusCode m_errorCode = LibPlcTag.StatusCode.STATUS_OK;
    bool m_writeRequired = false;
    bool m_readDone = false;
    readonly ILog log;
    TimeSpan m_statusOkTimeout = STATUS_OK_TIMEOUT_DEFAULT;
    DateTime? m_pendingStartDateTime = null;

    /// <summary>
    /// Read the data only once (keep it in cache)
    /// 
    /// If ReadOnce is true, SetValue is not supported
    /// </summary>
    public bool ReadOnce { get; set; }

    /// <summary>
    /// Type of the value to read
    /// </summary>
    public Type Type { get; set; }

    /// <summary>
    /// Element size
    /// 1 for byte, 2 for int16, ...
    /// </summary>
    public int ElementSize { get; set; }

    /// <summary>
    /// Timeout in ms to read or write a tag
    /// </summary>
    public int TimeOut { get; set; }

    /// <summary>
    /// Pending time out in seconds before self-destruction
    /// </summary>
    public int PendingTimeOut { get; set; } = PENDING_TIME_OUT_DEFAULT;

    #region Constructors, destructor
    /// <summary>
    /// Description of the constructor
    /// </summary>
    /// <param name="logger">Logger</param>
    /// <param name="name">param name</param>
    /// <param name="key">Full key</param>
    /// <param name="timeoutMs">Time out in ms</param>
    public Tag (ILog logger, string name, String key, int timeoutMs = 1000)
    {
      // Create a new tag
      log = LogManager.GetLogger (logger.Name + ".Tag." + name);
      if (key is null) {
        log.Error ($"Tag: key is null");
        throw new ArgumentNullException ("key");
      }
      m_name = name;
      m_key = key;
      m_tag = LibPlcTag.Create (m_key, timeoutMs);
      this.ReadOnce = false;
    }

    /// <summary>
    /// Destroy a tag
    /// </summary>
    public void Dispose ()
    {
      LibPlcTag.Destroy (m_tag);
    }
    #endregion // Constructors, destructor

    #region Methods
    /// <summary>
    /// Start of a new acquisition. If the value is readable:
    /// * the value is stored
    /// * a new reading is required for the next time
    /// </summary>
    public void Start ()
    {
      if (!this.ReadOnce) {
        m_readDone = false;
      }
      m_writeRequired = false;
    }

    /// <summary>
    /// Write if needed
    /// </summary>
    public void Finish ()
    {
      if (m_writeRequired) {
        CheckPendingStatus ();
        m_errorCode = LibPlcTag.Write (m_tag, TimeOut);
        if (m_errorCode == LibPlcTag.StatusCode.STATUS_OK) {
          log.Info ($"Finish: Successfully wrote in tag {m_key}");
        }
        else {
          log.ErrorFormat ($"Finish: error {m_errorCode} while trying to write in {m_key}");
        }
      }
    }

    void CheckPendingStatus ()
    {
      DateTime startDateTime = DateTime.UtcNow;
      while (DateTime.UtcNow < startDateTime.Add (m_statusOkTimeout)) {
        m_errorCode = LibPlcTag.GetStatus (m_tag);
        if (LibPlcTag.StatusCode.STATUS_PENDING == m_errorCode) {
          if (m_pendingStartDateTime.HasValue) {
            if (DateTime.UtcNow < m_pendingStartDateTime.Value.AddSeconds (this.PendingTimeOut)) {
              log.Fatal ($"CheckStatus: pending time out since {m_pendingStartDateTime} => self-destruction (sleep forever)");
              System.Threading.Thread.Sleep (System.Threading.Timeout.Infinite);
            }
          }
          else {
            m_pendingStartDateTime = DateTime.UtcNow;
          }
        }
        else {
          if ((LibPlcTag.StatusCode.STATUS_OK != m_errorCode) && log.IsWarnEnabled) {
            log.Warn ($"CheckStatus: error code was {m_errorCode} not ok but return true since not PENDING");
          }
          m_pendingStartDateTime = null;
          return;
        }
      }

      log.Error ($"CheckStatus: status OK timeout (still PENDING) after {DateTime.UtcNow.Subtract (startDateTime)}, pending start={m_pendingStartDateTime}");
      throw new ErrorCodeException (m_errorCode, m_key);
    }

    /// <summary>
    /// Get the value of a tag
    /// </summary>
    /// <param name="elementNumber"></param>
    /// <returns></returns>
    public object GetValue (int elementNumber)
    {
      return GetValue (elementNumber, Get);
    }

    /// <summary>
    /// Get the bit of a tag
    /// </summary>
    /// <param name="elementNumber"></param>
    /// <returns></returns>
    public object GetBit (int elementNumber)
    {
      return GetValue (elementNumber, (offset) => 0 < LibPlcTag.GetBit (m_tag, offset));
    }

    public object GetValue (int elementNumber, Func<int, object> getMethod)
    {
      // Read if needed
      if (m_readDone) {
        log.Debug ("GetValue: read done");
      }
      else {
        CheckPendingStatus ();
        m_errorCode = LibPlcTag.Read (m_tag, TimeOut);
        if (m_errorCode == LibPlcTag.StatusCode.STATUS_OK) {
          log.Info ($"GetValue: Successfully read {m_key}");
          m_readDone = true;
        }
        else {
          log.Error ($"GetValue: error {m_errorCode} while trying to read {m_key}");
          throw new ErrorCodeException (m_errorCode, m_key);
        }
      }

      if (log.IsDebugEnabled) {
        log.Debug ($"GetValue: about to get {m_key}#{elementNumber}");
      }
      var result = getMethod (elementNumber * ElementSize);
      if (log.IsDebugEnabled) {
        log.Debug ($"GetValue: got {result} for {m_key}#{elementNumber}]");
      }
      return result;
    }

    object Get (int offset)
    {
      switch (Type.GetTypeCode (Type)) {
      case TypeCode.Boolean:
        return LibPlcTag.GetUInt8 (m_tag, offset) > 0;
      case TypeCode.Byte:
        return LibPlcTag.GetUInt8 (m_tag, offset);
      case TypeCode.SByte:
        return LibPlcTag.GetInt8 (m_tag, offset);
      case TypeCode.UInt16:
        return LibPlcTag.GetUInt16 (m_tag, offset);
      case TypeCode.Int16:
        return LibPlcTag.GetInt16 (m_tag, offset);
      case TypeCode.UInt32:
        return LibPlcTag.GetUInt32 (m_tag, offset);
      case TypeCode.Int32:
        return LibPlcTag.GetInt32 (m_tag, offset);
      case TypeCode.Single:
        return LibPlcTag.GetFloat (m_tag, offset);
      case TypeCode.String:
        return GetString (offset);
      default:
        // Should not happen (or error in the code)
        Debug.Assert (false);
        log.FatalFormat ("Get: not supported type {0}", this.Type);
        throw new Exception ("EthernetIP: cannot read type '" + Type + "'");
      }
    }

    string GetString (int offset)
    {
      var stringLength = LibPlcTag.GetStringLength (m_tag, offset);
      var sb = new StringBuilder (stringLength);
      var statusCode = LibPlcTag.GetString (m_tag, offset, sb, stringLength);
      if (statusCode is LibPlcTag.StatusCode.STATUS_OK) {
        var s = sb.ToString ().Substring (0, stringLength);
        if (log.IsDebugEnabled) {
          log.Debug ($"GetString: got {s} for {m_key} offset={offset}");
        }
        return s;
      }
      else {
        log.Error ($"GetString: error {m_errorCode} while trying to get string for  {m_key}");
        throw new ErrorCodeException (m_errorCode, m_key);
      }
    }

    /// <summary>
    /// Set the value of a tag
    /// </summary>
    /// <param name="elementNumber"></param>
    /// <param name="elementSize"></param>
    /// <param name="value"></param>
    public void SetValue (int elementNumber, int elementSize, object value)
    {
      if (this.ReadOnce) {
        log.ErrorFormat ("SetValue: not supported because ReadOnce is set");
        throw new NotSupportedException ("SetValue with ReadOnce");
      }

      // Able to write?
      if (m_errorCode != LibPlcTag.StatusCode.STATUS_OK) {
        log.ErrorFormat ("SetValue: current error code was not ok {0}", m_errorCode);
        throw new ErrorCodeException (m_errorCode, m_key);
      }
      else {
        m_errorCode = Set (elementNumber * elementSize, value);
        if (m_errorCode != LibPlcTag.StatusCode.STATUS_OK) {
          log.ErrorFormat ("SetValue: new error code is not ok {0}", m_errorCode);
          throw new ErrorCodeException (m_errorCode, m_key);
        }
      }

      m_writeRequired = true;
    }

    LibPlcTag.StatusCode Set (int offset, object value)
    {
      switch (Type.GetTypeCode (Type)) {
      case TypeCode.Byte:
        return LibPlcTag.SetUInt8 (m_tag, offset, (byte)value);
      case TypeCode.SByte:
        return LibPlcTag.SetInt8 (m_tag, offset, (sbyte)value);
      case TypeCode.UInt16:
        return LibPlcTag.SetUInt16 (m_tag, offset, (UInt16)value);
      case TypeCode.Int16:
        return LibPlcTag.SetInt16 (m_tag, offset, (Int16)value);
      case TypeCode.UInt32:
        return LibPlcTag.SetUInt32 (m_tag, offset, (UInt32)value);
      case TypeCode.Int32:
        return LibPlcTag.SetInt32 (m_tag, offset, (Int32)value);
      case TypeCode.Single:
        return LibPlcTag.SetFloat (m_tag, offset, (float)value);
      case TypeCode.String:
        return LibPlcTag.SetString (m_tag, offset, (string)value);
      default:
        // Should not happen (or error in the code)
        Debug.Assert (false);
        log.Fatal ($"Set: not supported type {this.Type}");
        throw new Exception ("EthernetIP: cannot write type '" + Type + "'");
      }
    }
    #endregion // Methods
  }
}
