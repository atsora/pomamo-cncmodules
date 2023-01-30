// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections.Generic;
using Lemoine.Core.Log;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Alarm translator for Okuma:
  /// - based on a file (parameter "filepath")
  /// - that can be embedded in the dll (parameter "embedded")
  /// - containing lines with the template {alarm number}\t{alarm description}
  /// The code of the alarm is found in the original message or in the number
  /// </summary>
  public class AlarmTranslator_Okuma : IAlarmTranslator
  {
    #region Members
    readonly FileDictionary m_fileDictionary = new FileDictionary ();
    #endregion // Members

    static readonly ILog log = LogManager.GetLogger (typeof (AlarmTranslator_Okuma).FullName);

    #region Methods
    /// <summary>
    /// Initialize the alarm translator
    /// </summary>
    /// <param name="parameters">Parameters for the initialization</param>
    /// <returns>True if success</returns>
    public bool Initialize (IDictionary<string, string> parameters)
    {
      if (!parameters.ContainsKey ("filepath")) {
        log.Error ("AlarmTranslator_Okuma: couldn't find the parameter 'filepath'");
        return false;
      }

      bool embedded = false;
      if (parameters.ContainsKey ("embedded")) {
        bool result = bool.TryParse (parameters["embedded"], out embedded);
        if (!result) {
          log.ErrorFormat ("AlarmTranslator_Okuma: couldn't parse {0} as bool", parameters["embedded"]);
        }
      }

      try {
        m_fileDictionary.ParseFile (parameters["filepath"], embedded);
        if (m_fileDictionary.Error) {
          log.ErrorFormat ("AlarmTranslator_Okuma: couldn't initialize the dictionary with the path {0}",
                          parameters["filepath"]);
          return false;
        }
      }
      catch (Exception e) {
        log.ErrorFormat ("AlarmTranslator_Okuma: an error occured during the creation of a dictionary with the path {0}: {1}",
                        parameters["filepath"], e);
        return false;
      }

      return true;
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
        log.Error ("AlarmTranslator_Okuma: dictionary error is true");
        return;
      }

      // Process alarm
      string initialMessage = alarm.Message; // Can be in the form {CODE} {ALARM_X} {ADDITIONAL DATA}
      var split = initialMessage.Split (new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
      if (split.Length > 0) {
        var translatedMessage = m_fileDictionary.GetTranslation (split[0]);
        if (String.IsNullOrEmpty (translatedMessage)) {
          translatedMessage = m_fileDictionary.GetTranslation (alarm.Number);
        }

        if (!String.IsNullOrEmpty (translatedMessage)) {
          // Translated message + the additional message if any
          alarm.Message = translatedMessage + (
            (split.Length > 2) ? " (" + String.Join (" ", split, 2, split.Length - 2) + ")" : ""
           );
        }
      }
    }
    #endregion // Methods
  }
}
