// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections.Generic;
using System.Globalization;
using Lemoine.Core.Log;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Description of ScenarioReaderCncValue.
  /// </summary>
  public class ScenarioReaderCncValue : IScenarioReader
  {
    #region Members
    IDictionary<string, object> m_cncValues = new Dictionary<string, object> ();
    #endregion // Members

    ILog log = LogManager.GetLogger ("Lemoine.Cnc.In.Simulation.ScenarioReader.CncValue");

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="l"></param>
    public ScenarioReaderCncValue (ILog l)
    {
      log = l;
    }

    /// <summary>
    /// Get a cnc value
    /// </summary>
    /// <param name="param">key</param>
    /// <returns></returns>
    public object GetCncValue (string param)
    {
      if (m_cncValues.ContainsKey (param)) {
        return m_cncValues[param];
      }
      else {
        string message = $"GetCncValue: key {param} is unknown";
        log.Info (message);
        throw new Exception (message);
      }
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
      var keyValue = command.Split ('=');
      if (keyValue.Length < 2) {
        return false;
      }
      else if (keyValue[0] == "") {
        return false;
      }
      else {
        m_cncValues[keyValue[0]] = ParseValue (keyValue[1]);
        return true;
      }
    }

    object ParseValue (string v)
    {
      // Bool?
      if (v.Equals ("True", StringComparison.InvariantCultureIgnoreCase)) {
        return true;
      }

      if (v.Equals ("False", StringComparison.InvariantCultureIgnoreCase)) {
        return false;
      }

      // Double?
      if (v.Contains (".")) {
        try {
          return double.Parse (v, CultureInfo.InvariantCulture);
        }
        catch (Exception ex) {
          log.Debug ($"ParseValue: parsing a double for {v} failed", ex);
        }
      }

      // Int?
      try {
        return int.Parse (v);
      }
      catch (Exception ex) {
        log.Debug ($"ParseValue: parsing an int for {v} failed", ex);
      }

      // Fallback: string
      return v;
    }
  }
}
