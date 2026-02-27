// Copyright (C) 2009-2023 Lemoine Automation Technologies
// Copyright (C) 2025 Atsora Solutions
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Lemoine.Core.Log;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Description of ScenarioReaderCncAlarm.
  /// </summary>
  public class ScenarioReaderCncAlarm : IScenarioReader
  {
    readonly IDictionary<int, CncAlarm> m_currentCncAlarms = new Dictionary<int, CncAlarm> ();

    static readonly Regex CHECK_CREATE = new Regex ("^{ ?(.*) ?; ?(.*) ?; ?(.*) ?; ?(.*) ?; ?(.*) ?} ?(.*)$");

    ILog log = LogManager.GetLogger ("Lemoine.Cnc.In.Simulation.ScenarioReader.CncAlarm");

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="l"></param>
    public ScenarioReaderCncAlarm (ILog l)
    {
      log = l;
    }

    /// <summary>
    /// Get cnc alarms
    /// </summary>
    /// <returns></returns>
    public ICollection<CncAlarm> GetCncAlarms ()
    {
      return m_currentCncAlarms.Values;
    }

    /// <summary>
    /// <see cref="IScenarioReader"/>
    /// </summary>
    /// <param name="l"></param>
    public void UpdateLog (ILog l)
    {
      log = l;
    }

    /// <summary>
    /// Process a command
    /// </summary>
    /// <param name="command"></param>
    /// <returns>true if success</returns>
    public bool ProcessCommand (string command)
    {
      try {
        var split = command.ToUpper ().Split (new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (split.Length < 2) {
          return false;
        }
        else {
          int index = int.Parse (split[1]);
          string otherPart = String.Join (" ", split, 2, split.Length - 2);
          switch (split[0]) {
            case "CREATE":
              return CreateAlarm (index, otherPart);
            case "DELETE":
              return DeleteAlarm (index);
            case "UPDATE":
              return UpdateAlarmProperties (index, otherPart);
            default:
              return false;
          }
        }
      }
      catch (Exception ex) {
        log.Error ("ScenarioReaderCncAlarm: ProcessCommand in CncAlarm failed", ex);
        return false;
      }
    }

    bool CreateAlarm (int index, string txt)
    {
      // Already created?
      if (m_currentCncAlarms.ContainsKey (index)) {
        return false;
      }

      // Get the identification of the alarm
      var match = CHECK_CREATE.Match (txt);
      if (match.Success) {
        // Create a new cnc alarm
        m_currentCncAlarms[index] = new CncAlarm (
          match.Groups[1].Value, // Cnc info
          match.Groups[2].Value, // Cnc sub info
          match.Groups[3].Value, // Type
          match.Groups[4].Value); // Number
        m_currentCncAlarms[index].Message = match.Groups[5].Value;

        // Process the properties
        var propertiesTxt = match.Groups[6].Value.Trim ();
        if (!string.IsNullOrEmpty (propertiesTxt)) {
          try {
            UpdateAlarmProperties (index, propertiesTxt);
          }
          catch (Exception ex) {
            log.Error ($"CreateAlarm: ParseProperties for {propertiesTxt} in txt={txt} index={index} failed", ex);
          }
        }

        return true;
      }
      else {
        return false;
      }
    }

    bool UpdateAlarmProperties (int index, string txt)
    {
      // Not created?
      if (!m_currentCncAlarms.ContainsKey (index)) {
        log.Error ($"UpdateAlarm: index={index} not active => return false");
        return false;
      }

      // Parse the properties
      IDictionary<string, string> properties = null;
      try {
        properties = ParseProperties (txt);
      }
      catch (Exception ex) {
        log.Error ($"UpdateAlarm: ParseProperties for {txt} index={index} failed", ex);
        return false;
      }
      if (properties == null) {
        return false;
      }

      // Clear properties
      m_currentCncAlarms[index].Properties.Clear ();
      m_currentCncAlarms[index].Message = "";

      // Add the new properties
      foreach (var key in properties.Keys) {
        m_currentCncAlarms[index].Properties[key] = properties[key];
      }

      return true;
    }

    bool DeleteAlarm (int index)
    {
      return m_currentCncAlarms.Remove (index);
    }

    IDictionary<string, string> ParseProperties (string txt)
    {
      var properties = new Dictionary<string, string> ();

      var split = txt.Split (';');
      foreach (var elt in split) {
        // Extract the key and the value
        var split2 = elt.Split ('=');
        if (split2.Length != 2) {
          log.Error ($"ParseProperties: wrong property {elt} in txt={txt}, omit it");
          continue;
        }

        string key = split2[0].Trim (' ');
        string value = split2[1].Trim (' ');

        // Check that the key is valid
        if (String.IsNullOrEmpty (key)) {
          log.Error ($"ParseProperties: wrong key {elt} in property txt={txt}, omit it");
          continue;
        }

        if (properties.ContainsKey (key)) {
          log.Error ($"ParseProperties: key {key} is duplicated in txt={txt}, omit it");
          continue;
        }

        // Add the property
        properties[key] = value;
      }

      return properties;
    }
  }
}
