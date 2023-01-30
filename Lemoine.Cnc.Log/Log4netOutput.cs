// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using log4net;
using log4net.Core;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Output CncModule to log with log4net
  /// </summary>
  public sealed class Log4netOutput : Lemoine.Cnc.BaseCncModule, Lemoine.Cnc.ICncModule, IDisposable
  {
    #region Members
    int m_machineId = 0;
    int m_machineModuleId = 0;
    ILog m_dataLog;
    string m_category;
    bool m_disposed = false;
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
          m_dataLog = LogManager.GetLogger (Assembly.GetCallingAssembly (), log.Name);
        }
        else {
          m_dataLog = LogManager.GetLogger (Assembly.GetCallingAssembly (), m_category);
        }
      }
    }
    #endregion // Getters / Setters

    #region Constructors / Destructor / ToString methods
    /// <summary>
    /// Description of the constructor
    /// </summary>
    public Log4netOutput ()
      : base ("Lemoine.Cnc.Out.Log")
    {
      m_dataLog = LogManager.GetLogger (Assembly.GetCallingAssembly (), log.Name);
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
        level = ParseLevel (param);
      }
      LogFormat (level, "LogCncVariableSet: Start");

      IDictionary dictionary = v as IDictionary;
      if (v != null) {
        foreach (DictionaryEntry item in dictionary) {
          LogFormat (level, "{0}={1}", item.Key, item.Value);
        }
      }
      else {
        LogFormat (level, "couldn't parse object {0} as a dictionary", v);
      }

      Log (level, "LogCncVariableSet: End");
    }

    Level ParseLevel (string level)
    {
      switch (level) {
        case "Debug":
          return log4net.Core.Level.Debug;
        case "Info":
          return log4net.Core.Level.Info;
        case "Notice":
          return log4net.Core.Level.Notice;
        case "Warn":
          return log4net.Core.Level.Warn;
        case "Error":
          return log4net.Core.Level.Error;
        case "Fatal":
          return log4net.Core.Level.Fatal;
        default:
          var allLevels = new log4net.Core.LevelMap ().AllLevels;
          foreach (var l in allLevels) {
            if (l.Name.Equals (level.ToString (), StringComparison.InvariantCultureIgnoreCase)) {
              return l;
            }
          }
          throw new Exception ("log4net exception not found for " + level);
      }
    }

    bool Log (Level level, string message)
    {
      return Log (level, message, null);
    }

    bool Log (Level level, string message, Exception exception)
    {

      var logger = m_dataLog.Logger;
      if (logger.IsEnabledFor (level)) {
        logger.Log (logger.GetType (), level, message, exception);
        return true;
      }

      return false;
    }

    bool LogFormat (Level level, string messageFormat, params object[] messageArguments)
    {
      var logger = m_dataLog.Logger;
      if (logger.IsEnabledFor (level)) {
        var message = string.Format (messageFormat, messageArguments);
        logger.Log (logger.GetType (), level, message, null);

        return true;
      }

      return false;
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
      LogFormat (level, "LogCncAlarms: {0} items", cncAlarms.Count);
      foreach (var cncAlarm in cncAlarms) {
        LogFormat (level, "{0}:{1}", cncAlarm.Number, cncAlarm.Message);
      }
      Log (level, "LogCncAlarms: End");
    }

    void UpdateLogger ()
    {
      if (0 != m_machineModuleId) {
        log = Lemoine.Core.Log.LogManager.GetLogger (String.Format ("{0}.{1}.{2}.{3}",
                                                     typeof (Log4netOutput).FullName,
                                                     this.CncAcquisitionId,
                                                     m_machineId,
                                                     m_machineModuleId));
      }
      else {
        log = Lemoine.Core.Log.LogManager.GetLogger (String.Format ("{0}.{1}.{2}",
                                                     typeof (Log4netOutput).FullName,
                                                     this.CncAcquisitionId,
                                                     m_machineId));
      }
    }
    #endregion // Methods

    #region IDisposable
    /// <summary>
    /// Dispose method to free resources
    /// Do not make this method virtual.
    /// A derived class should not be able to override this method.
    /// <see cref="IDisposable.Dispose" />
    /// 
    /// Dispose all the modules, if they have one Dispose method
    /// </summary>
    public void Dispose ()
    {
      Dispose (true);
      // This object will be cleaned up by the Dispose method.
      // Therefore, you should call GC.SupressFinalize to
      // take this object off the finalization queue
      // and prevent finalization code for this object
      // from executing a second time.
      GC.SuppressFinalize (this);
    }

    /// <summary>
    /// Dispose(bool disposing) executes in two distinct scenarios.
    /// If disposing equals true, the method has been called directly
    /// or indirectly by a user's code. Managed and unmanaged resources
    /// can be disposed.
    /// If disposing equals false, the method has been called by the
    /// runtime from inside the finalizer and you should not reference
    /// other objects. Only unmanaged resources can be disposed.
    /// 
    /// Note all the variables are not really needed here.
    /// But they are nevertheless here because this class could be used an example
    /// for other classes that could need them.
    /// </summary>
    /// <param name="disposing">Dispose also the managed resources</param>
    void Dispose (bool disposing)
    {
      if (log.IsDebugEnabled) {
        log.Debug ("Dispose");
      }
      // Check to see if Dispose has already been called.
      if (!this.m_disposed) {
        // If disposing equals true, dispose all managed
        // and unmanaged resources.
        if (disposing) {
          // Dispose managed resources
        }

        // Call the appropriate methods to clean up
        // unmanaged resources here.
        // If disposing is false,
        // only the following code is executed.
      }
      m_disposed = true;
    }
    #endregion // IDisposable
  }
}
