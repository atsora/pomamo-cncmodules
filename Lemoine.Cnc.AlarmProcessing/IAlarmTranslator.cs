// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections.Generic;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Description of IAlarmTranslator.
  /// </summary>
  public interface IAlarmTranslator
  {
    /// <summary>
    /// Initialize the alarm translator
    /// </summary>
    /// <param name="parameters">Parameters for the initialization</param>
    /// <returns>True if success</returns>
    bool Initialize (IDictionary<string, string> parameters);

    /// <summary>
    /// Process an alarm and complete the information
    /// Must not be called if the initialization failed
    /// </summary>
    /// <param name="alarm"></param>
    void ProcessAlarm (CncAlarm alarm);
  }
}
