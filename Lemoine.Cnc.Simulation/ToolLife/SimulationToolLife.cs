// Copyright (C) 2009-2023 Lemoine Automation Technologies
// Copyright (C) 2026 Atsora Solutions
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
    ScenarioReaderToolLife ReaderToolLife => m_readers['T'] as ScenarioReaderToolLife;

    /// <summary>
    /// Current tool life data
    /// </summary>
    /// <returns></returns>
    public ToolLifeData ToolLifeData
    {
      get {
        lock (m_readers) {
          return ReaderToolLife.GetToolLifeData ().Clone ();
        }
      }
    }
  }
}
