// Copyright (C) 2009-2023 Lemoine Automation Technologies
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
    #region Members
    readonly IDictionary<int, CncAlarm> m_currentCncAlarms = new Dictionary<int, CncAlarm> ();
    #endregion // Members

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

    #region Get methods
    /// <summary>
    /// Get cnc alarms
    /// </summary>
    /// <returns></returns>
    public ICollection<CncAlarm> GetCncAlarms ()
    {
      return m_currentCncAlarms.Values;
    }
    #endregion // Get methods

    #region Process methods
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
            return UpdateAlarm (index, otherPart);
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
        UpdateAlarm (index, match.Groups[4].Value);
        return true;
      }
      else {
        return false;
      }
    }

    bool UpdateAlarm (int index, string txt)
    {
      // Not created?
      if (!m_currentCncAlarms.ContainsKey (index)) {
        return false;
      }

      // Parse the properties
      IDictionary<string, string> properties = null;
      try {
        properties = ParseProperties (txt);
      }
      catch {
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
          throw new Exception ("Wrong property [" + elt + "]");
        }

        string key = split2[0].Trim (' ');
        string value = split2[1].Trim (' ');

        // Check that the key is valid
        if (String.IsNullOrEmpty (key)) {
          throw new Exception ("Wrong key in property [" + elt + "]");
        }

        if (properties.ContainsKey (key)) {
          throw new Exception ("Duplicated key [" + key + "]");
        }

        // Add the property
        properties[key] = value;
      }

      return properties;
    }

    #endregion // Process methods
  }
}
