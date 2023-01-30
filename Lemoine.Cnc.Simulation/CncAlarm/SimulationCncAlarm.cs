// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections.Generic;
using Lemoine.Cnc;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Description of SimulationToolLife.
  /// </summary>
  public partial class SimulationScenario
  {
    #region Getters / Setters
    ScenarioReaderCncAlarm ReaderCncAlarm
    {
      get { return m_readers['A'] as ScenarioReaderCncAlarm; }
    }
    #endregion // Getters / Setters

    #region Methods
    /// <summary>
    /// Get cnc alarms
    /// </summary>
    /// <returns></returns>
    public IList<CncAlarm> CncAlarms
    {
      get {
        IList<CncAlarm> data = new List<CncAlarm> ();
        lock (m_readers) {
          var dataTmp = ReaderCncAlarm.GetCncAlarms ();
          if (dataTmp != null) {
            foreach (var elt in dataTmp) {
              data.Add (elt.Clone ());
            }
          }
        }
        return data;
      }
    }
    #endregion // Methods
  }
}
