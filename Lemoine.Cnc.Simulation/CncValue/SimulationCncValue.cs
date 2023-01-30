// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using Lemoine.Cnc;
using System.Collections.Generic;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Description of SimulationToolLife.
  /// </summary>
  public partial class SimulationScenario
  {
    #region Getters / Setters
    ScenarioReaderCncValue ReaderCncValue
    {
      get { return m_readers['V'] as ScenarioReaderCncValue; }
    }
    #endregion // Getters / Setters

    #region Methods
    /// <summary>
    /// Get a cnc value
    /// </summary>
    /// <param name="param">name of the cnc value</param>
    /// <returns></returns>
    public object GetCncValue (string param)
    {
      object data;
      lock (m_readers) {
        data = ReaderCncValue.GetCncValue (param); // Base type => automatic copy
      }
      return data;
    }

    /// <summary>
    /// Get a CNC Variable set
    /// </summary>
    /// <param name="param"></param>
    /// <returns></returns>
    public object GetCncVariableSet (string param)
    {
      IDictionary<string, object> result = new Dictionary<string, object> ();
      var keys = Lemoine.Collections.EnumerableString.ParseListString (param);
      foreach (var key in keys) {
        result[key] = GetCncValue (key);
      }
      return result;
    }
    #endregion // Methods
  }
}
