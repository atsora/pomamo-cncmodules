// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Build alarms from alarm numbers
  /// </summary>
  public sealed class AddAlarmNumber : BaseCncModule, ICncModule, IDisposable
  {
    #region Members
    IList<CncAlarm> m_alarms = new List<CncAlarm> ();
    #endregion // Members

    #region Getters / Setters
    /// <summary>
    /// Alarms
    /// 
    /// Default: empty list
    /// </summary>
    public IList<CncAlarm> Alarms
    {
      get
      {
        if (log.IsDebugEnabled) {
          log.Debug ($"Alarms.get: {m_alarms.Count} elements");
        }
        return m_alarms;
      }
      set { m_alarms = value; }
    }

    /// <summary>
    /// Cnc info
    /// 
    /// Default: ""
    /// </summary>
    public string CncInfo { get; set; } = "";

    /// <summary>
    /// Alarm type
    /// 
    /// Default: ""
    /// </summary>
    public string AlarmType { get; set; } = "";
    #endregion // Getters / Setters

    #region Constructors
    /// <summary>
    /// Description of the constructor
    /// </summary>
    public AddAlarmNumber () : base ("Lemoine.Cnc.InOut.AddAlarmNumber")
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

    /// <summary>
    /// Start method: reset the different values
    /// </summary>
    public void Start ()
    {
      m_alarms.Clear ();
    }

    /// <summary>
    /// Finish method
    /// </summary>
    public void Finish ()
    {

    }

    /// <summary>
    /// Add a number
    /// </summary>
    /// <param name="param"></param>
    /// <param name="number"></param>
    public void AddNumber (string param, object number)
    {
      var newAlarm = new CncAlarm (this.CncInfo, this.AlarmType, number.ToString ());
      m_alarms.Add (newAlarm);
    }

    /// <summary>
    /// Add numbers
    /// </summary>
    /// <param name="param"></param>
    /// <param name="numbers"></param>
    public void AddNumbers (string param, IEnumerable<string> numbers)
    {

      foreach (var number in numbers) {
        AddNumber (param, number);
      }
    }
  }
}
