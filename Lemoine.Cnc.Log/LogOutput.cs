// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections;
using System.Collections.Generic;
using Lemoine.Core.Log;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Output CncModule to log with the Lemoine logger
  /// </summary>
  public sealed class LogOutput
    : Lemoine.Cnc.BaseCncModule, Lemoine.Cnc.ICncModule, IDisposable
  {
    #region Members
    int m_machineId = 0;
    int m_machineModuleId = 0;
    ILog m_dataLog;
    string m_category;
    #endregion // Members

    #region Getters / Setters
    /// <summary>
    /// Machine Id
    /// </summary>
    public int MachineId
    {
      get { return m_machineId; }
      set
      {
        m_machineId = value;
        UpdateLogger ();
      }
    }

    /// <summary>
    /// Machine Module Id
    /// </summary>
    public int MachineModuleId
    {
      get { return m_machineModuleId; }
      set
      {
        m_machineModuleId = value;
        UpdateLogger ();
      }
    }

    /// <summary>
    /// Alternative log category
    /// 
    /// Default is Lemoine.Cnc.Log4netOutput.x.y.z where:
    /// <item>x is the acquisition id</item>
    /// <item>y is the machine id</item>
    /// <item>z is the machine module id (if not null)</item>
    /// </summary>
    public string Category
    {
      get { return m_category; }
      set
      {
        m_category = value;
        if (string.IsNullOrEmpty (m_category)) {
          m_dataLog = log;
        }
        else {
          m_dataLog = LogManager.GetLogger (m_category);
        }
      }
    }
    #endregion // Getters / Setters

    #region Constructors / Destructor / ToString methods
    /// <summary>
    /// Description of the constructor
    /// </summary>
    public LogOutput ()
      : base ("Lemoine.Cnc.Out.Log")
    {
      m_dataLog = log;
    }

    /// <summary>
    /// <see cref="IDisposable.Dispose" />
    /// </summary>
    public void Dispose ()
    {
      GC.SuppressFinalize (this);
    }
    #endregion // Constructors / Destructor / ToString methods

    #region Methods
    /// <summary>
    /// Start method
    /// </summary>
    public bool Start ()
    {
      return true;
    }

    /// <summary>
    /// Finish method
    /// </summary>
    public void Finish ()
    {
    }

    /// <summary>
    /// Log a CncVariableSet or CncVariableRange
    /// </summary>
    /// <param name="param">Log level (Debug, Info, Fatal)</param>
    /// <param name="v"></param>
    public void LogCncVariableSetMultiLogs (string param, object v)
    {
      Level level = Level.Info;
      if (!string.IsNullOrEmpty (param)) {
        level = (Level)Enum.Parse (typeof (Level), param);
      }
      m_dataLog.Log (level, "LogCncVariableSet: Start");

      IDictionary dictionary = v as IDictionary;
      if (v != null) {
        foreach (DictionaryEntry item in dictionary) {
          m_dataLog.LogFormat (level, "{0}={1}", item.Key, item.Value);
        }
      }
      else {
        m_dataLog.LogFormat (level, "couldn't parse object {0} as a dictionary", v);
      }

      m_dataLog.Log (level, "LogCncVariableSet: End");
    }

    /// <summary>
    /// Log a list of Cnc Alarms
    /// </summary>
    /// <param name="param">Log level (Debug, Info, Fatal)</param>
    /// <param name="v"></param>
    public void LogCncAlarms (string param, object v)
    {
      IList<CncAlarm> cncAlarms = (IList<CncAlarm>)v;

      Level level = Level.Info;
      if (!string.IsNullOrEmpty (param)) {
        level = (Level)Enum.Parse (typeof (Level), param);
      }
      m_dataLog.LogFormat (level, "LogCncAlarms: {0} items", cncAlarms.Count);
      foreach (var cncAlarm in cncAlarms) {
        m_dataLog.LogFormat (level, "CncInfo={0},CncSubInfo={1},Type={2},Number={3},Message={4},Properties={5}",
          cncAlarm.CncInfo, cncAlarm.CncSubInfo, cncAlarm.Number, cncAlarm.Message, FormatAlarmProperties(cncAlarm.Properties));
      }
      m_dataLog.Log (level, "LogCncAlarms: End");
    }

    string FormatAlarmProperties (IDictionary<string, string> properties)
    {
      string txt = "[";
      bool first = true;
      foreach (var key in properties.Keys) {
        if (first)
          first = false;
        else
          txt += ",";
        txt += key + "=" + properties[key];
      }
      return txt + "]";
    }

    void UpdateLogger ()
    {
      if (0 != m_machineModuleId) {
        log = LogManager.GetLogger (String.Format ("{0}.{1}.{2}.{3}",
                                                   typeof (Log4netOutput).FullName,
                                                   this.CncAcquisitionId,
                                                   m_machineId,
                                                   m_machineModuleId));
      }
      else {
        log = LogManager.GetLogger (String.Format ("{0}.{1}.{2}",
                                                   typeof (Log4netOutput).FullName,
                                                   this.CncAcquisitionId,
                                                   m_machineId));
      }
    }
    #endregion // Methods
  }
}
