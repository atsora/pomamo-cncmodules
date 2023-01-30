// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections.Generic;
using System.Linq;
using Lemoine.Conversion;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Module to add or modify information in alarms
  /// </summary>
  public sealed class AlarmTranslator : BaseCncModule, ICncModule, IDisposable
  {
    readonly IAutoConverter m_autoConverter = new DefaultAutoConverter ();

    #region Members
    bool m_initialized = false;
    IAlarmTranslator m_alarmTranslator = null;
    readonly IDictionary<string, string> m_parameters = new Dictionary<string, string> ();
    #endregion // Members

    #region Getters / Setters
    /// <summary>
    /// True if an error occured during the initialization
    /// </summary>
    public bool InitializationError { get; private set; }

    /// <summary>
    /// Directly set the translator
    /// 
    /// Alternative: set the type (TranslatorType)
    /// </summary>
    public IAlarmTranslator Translator
    {
      get { return m_alarmTranslator; }
      set
      {
        m_alarmTranslator = value;
        m_initialized = true;
        this.InitializationError = false;
      }
    }

    /// <summary>
    /// Translator type
    /// </summary>
    public string TranslatorType
    {
      get
      {
        return m_alarmTranslator == null ? "" : m_alarmTranslator.GetType ().ToString ();
      }
      set
      {
        // Find the corresponding type and create an IAlarmTranslator
        Type type = Type.GetType ("Lemoine.Cnc." + value, false, true);
        if (type == null) {
          log.ErrorFormat ("AlarmTranslator: couldn't find the type {0}", value);
        }
        else {
          m_alarmTranslator = Activator.CreateInstance (type) as IAlarmTranslator;
          if (m_alarmTranslator == null) {
            log.ErrorFormat ("AlarmTranslator: couldn't create an instance of the type {0}", value);
          }
        }

        // The initialization must be done
        m_initialized = false;
      }
    }

    /// <summary>
    /// Parameters used for the initialization of the alarm translator
    /// </summary>
    public string Parameters
    {
      get
      {
        // Concatenate all parameters
        string result = "";
        foreach (var keyValue in m_parameters) {
          result += keyValue.Key + "=" + keyValue.Value + ";";
        }

        return result;
      }
      set
      {
        m_parameters.Clear ();
        var keyValues = value.Split (new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var keyValue in keyValues) {
          var split = keyValue.Split (new[] { '=' }, 2, StringSplitOptions.None);
          if (split.Length == 2 && !string.IsNullOrEmpty (split[0])) {
            m_parameters[split[0].ToLower ()] = split[1];
            log.InfoFormat ("AlarmTranslator - found parameter '{0}' -> '{1}'", split[0].ToLower (), split[1]);
          }
          else {
            log.ErrorFormat ("AlarmTranslator - invalid parameter {0}", keyValue);
          }
        }
      }
    }

    /// <summary>
    /// New CncInfo to specify in the alarm
    /// For example, "MTConnect" can be changed into "MTConnect - Okuma"
    /// </summary>
    public string CncInfoReplacement { get; set; }
    #endregion // Getters / Setters

    #region Constructor / Destructor
    /// <summary>
    /// Description of the constructor
    /// </summary>
    public AlarmTranslator () : base ("Lemoine.Cnc.InOut.AlarmTranslator")
    {
    }

    /// <summary>
    /// <see cref="IDisposable.Dispose" />
    /// </summary>
    public void Dispose ()
    {
      // Do nothing special here
      GC.SuppressFinalize (this);
    }
    #endregion // Constructor / Destructor

    #region Initialization
    /// <summary>
    /// Start method: reset the different values
    /// </summary>
    public void Start ()
    {
      // Initialization only if it is not done before
      if (!m_initialized) {
        if (log.IsDebugEnabled) {
          log.Debug ("Start: beginning initialization");
        }
        InitializationError = (m_alarmTranslator == null) || !m_alarmTranslator.Initialize (m_parameters);
        if (InitializationError) {
          log.Error ("Start: the initialization failed");
        }

        m_initialized = true;
      }
    }
    #endregion // Initialization

    #region Translation
    /// <summary>
    /// Translate a list of CncAlarm
    /// </summary>
    /// <param name="data">Data to translate, must be an IList of CncAlarm</param>
    public void Translate (object data)
    {
      log.Debug ("Translate: beginning translation");

      // Check if data is null
      if (data == null) {
        log.Warn ("Translate: translation cannot be done, the input is null");
        return;
      }

      // Check if data has a right type
      IList<CncAlarm> alarms;
      try {
        alarms = m_autoConverter.ConvertAuto<IList<CncAlarm>> (data);
      }
      catch (Exception ex) {
        log.Error ($"Translate: {data} was not a list of cnc alarms", ex);
        throw;
      }

      // Check if the translator is initialized
      if (!m_initialized) {
        log.Error ("Translate: the translator is not initialized");
        return;
      }

      if (InitializationError || m_alarmTranslator == null) {
        log.Error ("Translate: error during the creation of an alarm translator");
        return;
      }

      // Process each alarm
      foreach (var alarm in alarms) {
        m_alarmTranslator.ProcessAlarm (alarm);
      }

      if (!string.IsNullOrEmpty (CncInfoReplacement)) {
        foreach (var alarm in alarms) {
          alarm.CncInfo = CncInfoReplacement;
        }
      }
    }
    #endregion // Translation
  }
}
