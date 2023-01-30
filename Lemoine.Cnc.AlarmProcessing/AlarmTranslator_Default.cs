// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections.Generic;
using Lemoine.Core.Log;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Alarm translator by default
  /// - based on a file (parameter "filepath")
  /// - that can be embedded in the dll (parameter "embedded")
  /// - containing lines with the template {alarm number}\t{alarm description}\t{attribute=value}
  /// The attribute can by the type of the alarm or a property
  /// </summary>
  public class AlarmTranslator_Default : IAlarmTranslator
  {
    #region Members
    readonly FileDictionary m_fileDictionary = new FileDictionary ();
    #endregion // Members

    static readonly ILog log = LogManager.GetLogger (typeof (AlarmTranslator_Default).FullName);

    #region Methods
    /// <summary>
    /// Initialize the alarm translator
    /// </summary>
    /// <param name="parameters">Parameters for the initialization</param>
    /// <returns>True if success</returns>
    public bool Initialize (IDictionary<string, string> parameters)
    {
      if (!parameters.ContainsKey ("filepath")) {
        log.Error ("AlarmTranslator: couldn't find the parameter 'filepath'");
        return false;
      }

      bool embedded = false;
      if (parameters.ContainsKey ("embedded")) {
        bool result = bool.TryParse (parameters["embedded"], out embedded);
        if (!result) {
          log.ErrorFormat ("AlarmTranslator: couldn't parse {0} as bool", parameters["embedded"]);
        }
      }

      try {
        m_fileDictionary.ParseFile (parameters["filepath"], embedded);
        if (m_fileDictionary.Error) {
          log.ErrorFormat ("AlarmTranslator: couldn't initialize the dictionary with the path {0}",
                          parameters["filepath"]);
          return false;
        }
      }
      catch (Exception e) {
        log.ErrorFormat ("AlarmTranslator: an error occured during the initialization of a dictionary with the path {0}: {1}",
                        parameters["filepath"], e);
        return false;
      }

      return true;
    }

    /// <summary>
    /// Directly initialize the translator with content
    /// (useful when this class is used directly inside another module such as MML3)
    /// </summary>
    /// <param name="content"></param>
    /// <returns></returns>
    public bool InitializeWithContent (string content)
    {
      try {
        m_fileDictionary.ParseContent (content);
      }
      catch (Exception e) {
        log.ErrorFormat ("AlarmTranslator: an error occured during the initialization of a dictionary: {0}", e);
        return false;
      }

      return true;
    }

    /// <summary>
    /// Clear all translations
    /// (useful when this class is used directly inside another module such as MML3)
    /// </summary>
    public void ClearTranslations ()
    {
      m_fileDictionary.Clear ();
    }

    /// <summary>
    /// Process an alarm and complete the information
    /// Must not be called if the initialization failed
    /// </summary>
    /// <param name="alarm"></param>
    public void ProcessAlarm (CncAlarm alarm)
    {
      // Check the dictionary
      if (m_fileDictionary.Error) {
        log.Error ("AlarmTranslator: dictionary error is true");
        return;
      }

      // Process alarm
      string alarmCode = alarm.Number;

      string translation = m_fileDictionary.GetTranslation (alarmCode);
      if (!string.IsNullOrEmpty (translation)) {
        alarm.Message = translation;

        // A type is specified?
        string translatedType = m_fileDictionary.GetType (alarmCode);
        if (!string.IsNullOrEmpty (translatedType)) {
          alarm.Type = translatedType;
        }

        // Attributes are specified?
        IDictionary<string, string> attributes = m_fileDictionary.GetAttributes (alarmCode);
        if (attributes != null) {
          foreach (var key in attributes.Keys) {
            alarm.Properties[key] = attributes[key];
          }
        }
      }
    }
    #endregion // Methods
  }
}
