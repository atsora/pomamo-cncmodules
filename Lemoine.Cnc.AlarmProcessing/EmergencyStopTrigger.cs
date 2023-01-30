// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Module that analyzes all alarms and possibly triggers an emergency stop status
  /// The trigger is based on either an alarm number or an alarm message
  /// A series of regex can be used to trigger the emergency status based on the alarm message,
  /// another series will trigger the emergency based on the alarm number.
  /// These series can be written in a file containing lines like:
  ///   message:EMERGENCY STOP
  ///   number:188[0-9]
  /// The right part of the first ":" being a regex used to analyze alarm numbers or messages
  /// Or the series can be written in a property, all regex being concatenated and separated by a semicolon.
  /// For example: 
  ///   message:EMERGENCY STOP;number:188[0-9]
  /// </summary>
  public sealed class EmergencyStopTrigger : BaseCncModule, ICncModule, IDisposable
  {
    #region Members
    readonly IList<string> m_messageRegex = new List<string> ();
    readonly IList<string> m_numberRegex = new List<string> ();
    bool m_triggerRulesAnalyzed = false;
    #endregion // Members

    #region Getters / Setters
    /// <summary>
    /// True if the emergency stop status must be triggered
    /// </summary>
    public bool IsInEmergency { get; private set; }

    /// <summary>
    /// Series of rules used to trigger the emergency status
    /// For example:
    ///   message:EMERGENCY STOP;number:188[0-9]
    /// Means: message contains "EMERGENCY STOP" or number is 188 followed by another number
    /// Alternatively, TriggerFilePath can be specified
    /// </summary>
    public string TriggerRules { get; set; }

    /// <summary>
    /// Path of the file that defines how to trigger the emergency stop status
    /// It contains lines like:
    /// * message:EMERGENCY STOP
    /// * number:188[0-9]
    /// The right part of the first ":" being a regex used to analyze alarm numbers or messages
    /// </summary>
    public string TriggerFilePath { get; set; }
    #endregion // Getters / Setters

    #region Constructors
    /// <summary>
    /// Default constructor
    /// </summary>
    public EmergencyStopTrigger () : base ("Lemoine.Cnc.InOut.EmergencyStopTrigger")
    {
      this.IsInEmergency = false;
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
      if (!m_triggerRulesAnalyzed) {
        if (!string.IsNullOrEmpty (TriggerFilePath)) {
          try {
            string content = "";
            using (var file = new StreamReader (TriggerFilePath)) {
              content = file.ReadToEnd ();
            }

            // Analyze the content if it's not empty
            if (!String.IsNullOrEmpty (content)) {
              var lines = content.Split (new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
              AnalyzeTriggerRules (lines);
            }
          }
          catch (Exception e) {
            log.ErrorFormat ("Couldn't analyze the trigger rules in file {0}: {1}", TriggerFilePath, e);
            return false;
          }
        }
        else if (!string.IsNullOrEmpty (TriggerRules)) {
          try {
            AnalyzeTriggerRules (TriggerRules.Split (new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));
          }
          catch (Exception e) {
            log.ErrorFormat ("Couldn't analyze the trigger rules in {0}: {1}", TriggerRules, e);
            return false;
          }
        }
        else {
          m_triggerRulesAnalyzed = true; // Nothing to analyze
        }
      }

      return true;
    }

    /// <summary>
    /// Determine if the emergency status must be triggered based on a series of alarms
    /// </summary>
    /// <param name="alarms"></param>
    public void ProcessAlarms (IList<CncAlarm> alarms)
    {
      if (!m_triggerRulesAnalyzed) {
        throw new Exception ("Trigger rules not analyzed");
      }

      // Default is: no emergency
      IsInEmergency = false;

      // Return immediatly if there are no alarms
      if (alarms == null || alarms.Count == 0) {
        return;
      }

      // Or check all alarms
      foreach (var alarm in alarms) {
        if (IsAnEmergencyAlarm (alarm)) {
          IsInEmergency = true;
          return;
        }
      }
    }
    #endregion Public methods

    #region Private methods
    void AnalyzeTriggerRules (string[] lines)
    {
      foreach (string line in lines) {
        // Parse the line
        if (line.StartsWith("message:")) {
          var messageRegex = line.Substring (8);
          if (!string.IsNullOrEmpty (messageRegex)) {
            m_messageRegex.Add (messageRegex);
          }
        }
        else if (line.StartsWith ("number:")) {
          var numberRegex = line.Substring (7);
          if (!string.IsNullOrEmpty (numberRegex)) {
            m_numberRegex.Add (numberRegex);
          }
        }
        else if (!line.StartsWith ("#")) {
          log.WarnFormat ("Cannot process line {0} in the trigger file {1}", line, TriggerFilePath);
        }
      }

      m_triggerRulesAnalyzed = true;
    }

    bool IsAnEmergencyAlarm (CncAlarm alarm)
    {
      // Test the number
      if (!string.IsNullOrEmpty(alarm.Number) && m_numberRegex.Count > 0) {
        string number = alarm.Number;
        foreach (var strRegex in m_numberRegex) {
          if (Regex.IsMatch(number, strRegex)) {
            log.InfoFormat ("Alarm {0} ({1}) matches regex 'number:{2}' => emergency status triggered",
              alarm.Number, alarm.Message, strRegex);
            return true;
          }
        }
      }

      // Test the message
      if (!string.IsNullOrEmpty(alarm.Message) && m_messageRegex.Count > 0) {
        string message = alarm.Message;
        foreach (var strRegex in m_messageRegex) {
          if (Regex.IsMatch (message, strRegex, RegexOptions.IgnoreCase)) {
            log.InfoFormat ("Alarm {0} ({1}) matches regex 'message:{2}' => emergency status triggered",
              alarm.Number, alarm.Message, strRegex);
            return true;
          }
        }
      }

      // No emergency detected
      return false;
    }
    #endregion Private methods
  }
}
