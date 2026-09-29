// Copyright (C) 2026 Atsora Solutions
//
// SPDX-License-Identifier: GPL-2.0

using System;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Arguments of the event that is raised once an OPC UA session has been automatically reconnected
  /// </summary>
  sealed class ReconnectedEventArgs : EventArgs
  {
    /// <summary>
    /// Was a new session created?
    ///
    /// If true, the previous session could not be re-activated, which usually means the server was restarted:
    /// the namespace table may have changed and all the node ids that reference a namespace index
    /// must be resolved again
    /// </summary>
    public bool NewSession { get; }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="newSession">was a new session created?</param>
    public ReconnectedEventArgs (bool newSession)
    {
      this.NewSession = newSession;
    }
  }
}
