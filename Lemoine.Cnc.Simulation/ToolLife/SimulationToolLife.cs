// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using Lemoine.Cnc;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Description of SimulationToolLife.
  /// </summary>
  public partial class SimulationScenario
  {
    #region Getters / Setters
    ScenarioReaderToolLife ReaderToolLife
    {
      get { return m_readers['T'] as ScenarioReaderToolLife; }
    }
    #endregion // Getters / Setters

    #region Methods
    /// <summary>
    /// Current tool life data
    /// </summary>
    /// <returns></returns>
    public ToolLifeData ToolLifeData
    {
      get {
        ToolLifeData data = null;
        lock (m_readers) {
          data = ReaderToolLife.GetToolLifeData ().Clone ();
        }
        return data;
      }
    }
    #endregion // Methods
  }
}
