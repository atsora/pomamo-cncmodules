// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections;
using System.Collections.Generic;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Build alarms from bool arrays
  /// </summary>
  public sealed class AlarmFromArrays : BaseCncModule, ICncModule, IDisposable
  {
    #region Members
    IList<CncAlarm> m_alarms = new List<CncAlarm> ();
    #endregion // Members

    #region Getters / Setters
    /// <summary>
    /// Alarms
    /// </summary>
    public IList<CncAlarm> Alarms {
      get
      {
        log.DebugFormat ("Alarms.get: {0} elements", m_alarms.Count);
        return m_alarms;
      }
      set { m_alarms = value; }
    }

    /// <summary>
    /// Cnc info
    /// </summary>
    public string CncInfo { get; set; }

    /// <summary>
    /// Cnc sub-info
    /// </summary>
    public string CncSubInfo { get; set; } = "";

    /// <summary>
    /// Alarm type
    /// </summary>
    public string AlarmType { get; set; }

    /// <summary>
    /// Current severity (optional)
    /// 
    /// If null or empty, do not set any severity
    /// </summary>
    public string Severity { get; set; }

    /// <summary>
    /// Pending message array
    /// </summary>
    public string[] MessageArray { get; set; }
    #endregion // Getters / Setters

    #region Constructors
    /// <summary>
    /// Description of the constructor
    /// </summary>
    public AlarmFromArrays ()
      : base ("Lemoine.Cnc.InOut.AlarmFromArrays")
    {
      this.CncInfo = "";
      this.AlarmType = "";
    }

    /// <summary>
    /// <see cref="IDisposable.Dispose" />
    /// </summary>
    public void Dispose ()
    {
      // Do nothing special here
      GC.SuppressFinalize (this);
    }
    #endregion // Constructors

    #region Public methods
    /// <summary>
    /// Start method
    /// </summary>
    /// <returns></returns>
    public bool Start ()
    {
      this.Alarms.Clear ();
      return true;
    }

    /// <summary>
    /// Set the current severity using a get instruction
    /// </summary>
    /// <param name="param"></param>
    public void SetSeverity (string param)
    {
      this.Severity = param;
    }

    /// <summary>
    /// Clear a message array
    /// </summary>
    /// <param name="param"></param>
    public void ClearMessageArray (string param)
    {
      this.MessageArray = null;
    }

    /// <summary>
    /// Add an integer as an array of bools
    /// </summary>
    /// <param name="param">prefix</param>
    /// <param name="data">integer</param>
    public void AddInt32 (string param, object data)
    {
      string prefix = (null == param) ? "" : param;
      int n = (int)data;
      log.DebugFormat ("AddInt32: data is {0}", n);
      var bitArray = new BitArray (new int[] { n });
      for (int i = 0; i < bitArray.Count; ++i) {
        if (bitArray[i]) {
          log.DebugFormat ("AddInt32: item {0} is on in {1}", i, n);
          var alarm = new CncAlarm (this.CncInfo, this.AlarmType, prefix + i.ToString ());
          alarm.CncSubInfo = this.CncSubInfo;
          if ((null != this.MessageArray) && (i < this.MessageArray.Length)) {
            var message = this.MessageArray[i];
            if (null != message) {
              log.DebugFormat ("AddIn32: associate message {0} to {1}", message, i);
              alarm.Message = this.MessageArray[i];
            }
          }
          if (!string.IsNullOrEmpty (this.Severity)) {
            alarm.Properties["Severity"] = this.Severity;
          }
          this.Alarms.Add (alarm);
        }
      }
    }
    #endregion // Public methods
  }
}
