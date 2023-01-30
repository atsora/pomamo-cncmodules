// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Lemoine.Core.Log;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Debug level
  /// </summary>
  public enum PlcTagDebugLevel
  {
    /// <summary>
    /// Disables debugging output.
    /// </summary>
    None = 0,

    /// <summary>
    /// Only output errors. Generally these are fatal to the functioning of the library.
    /// </summary>
    Error = 1,

    /// <summary>
    /// Outputs warnings such as error found when checking a malformed tag attribute string or when unexpected problems are reported from the PLC.
    /// </summary>
    Warn = 2,

    /// <summary>
    /// Outputs diagnostic information about the internal calls within the library.
    /// Includes some packet dumps.
    /// </summary>
    Info = 3,

    /// <summary>
    /// Outputs detailed diagnostic information about the code executing within the library including packet dumps.
    /// </summary>
    Detail = 4,

    /// <summary>
    /// Outputs extremely detailed information.
    /// Do not use this unless you are trying to debug detailed information about every mutex lock and release.
    /// Will output many lines of output per millisecond.
    /// You have been warned!
    /// </summary>
    Spew = 5
  }
}
