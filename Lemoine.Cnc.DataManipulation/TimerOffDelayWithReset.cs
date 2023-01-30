// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Lemoine.Core.Log;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Implements the Timer Off Delay with Reset (TOFR) instruction
  /// 
  /// The TOFR instruction is non-retentive timer that accumulates time when TimerEnable is cleared
  /// </summary>
  public sealed class TimerOffDelayWithReset : Lemoine.Cnc.BaseCncModule, Lemoine.Cnc.ICncModule, IDisposable
  {
    #region Members
    bool m_enableIn = true;
    bool m_timerEnable = false;
    DateTime? m_timerEnableDateTime = null;

    bool m_enableInSet = false;
    bool m_timerEnableSet = false;
    #endregion

    #region Getters / Setters
    /// <summary>
    /// Set if the EnableIn property is required at each execution or it can be ommitted
    /// (and its default value true used)
    /// </summary>
    public bool EnableInRequired { get; set; } = false;

    /// <summary>
    /// If cleared, the instruction does not execute and outputs are not updated.
    /// If set, the instruction executes
    /// 
    /// Default is set (true).
    /// </summary>
    public bool EnableIn
    {
      get { return m_enableIn; }
      set
      {
        m_enableInSet = true;
        if (m_enableIn != value) { // Change
          m_enableIn = value;
          m_timerEnableDateTime = null;
        }
      }
    }

    /// <summary>
    /// If set, this enables the timer to  run and accumulate time.
    /// 
    /// Default is cleared (false).
    /// </summary>
    public bool TimerEnable
    {
      get { return m_timerEnableDateTime.HasValue; }
      set
      {
        m_timerEnableSet = true;
        if (m_timerEnable != value) { // On change only
          m_timerEnable = value;

          if (value) {
            m_timerEnableDateTime = null;
          }
          else if (m_enableIn && !m_timerEnableDateTime.HasValue) { // && !value
            m_timerEnableDateTime = DateTime.UtcNow;
          }
        }
      }
    }

    /// <summary>
    /// PRE: Timer preset value in TimeSpan
    /// </summary>
    public TimeSpan PREDuration { get; set; }

    /// <summary>
    /// PRE: Timer preset value in string
    /// </summary>
    public string PREString
    {
      get { return this.PREDuration.ToString (); }
      set { this.PREDuration = TimeSpan.Parse (value); }
    }

    /// <summary>
    /// PRE: Timer preset value in seconds
    /// </summary>
    public double PRETotalSeconds
    {
      get { return this.PREDuration.TotalSeconds; }
      set { this.PREDuration = TimeSpan.FromSeconds (value); }
    }

    /// <summary>
    /// Request to reset the timer. When set, the timer resets.
    /// Default is cleared.
    /// </summary>
    public bool Reset
    {
      set
      {
        if (value) {
          m_timerEnableDateTime = null;
        }
      }
    }

    /// <summary>
    /// Accumulated time
    /// </summary>
    public TimeSpan ACC
    {
      get
      {
        ValidateInput ();
        if (m_timerEnableDateTime.HasValue) {
          return DateTime.UtcNow.Subtract (m_timerEnableDateTime.Value);
        }
        else {
          return TimeSpan.FromTicks (0);
        }
      }
    }

    /// <summary>
    /// Timing done output.
    /// 
    /// Indicates when accumulated time is greater than or equal to preset
    /// </summary>
    public bool DN
    {
      get
      {
        ValidateInput ();
        return !(m_enableIn && m_timerEnableDateTime.HasValue
          && (this.PREDuration <= this.ACC));
      }
    }

    /// <summary>
    /// Timer timeing output. When set, a timing operation is in progress.
    /// </summary>
    public bool TT
    {
      get
      {
        ValidateInput ();
        return m_enableIn && m_timerEnableDateTime.HasValue
          && (this.ACC < this.PREDuration);
      }
    }

    /// <summary>
    /// Timer enabled output. Indicates the timer instruction is enabled
    /// </summary>
    public bool EN
    {
      get
      {
        ValidateInput ();
        return m_enableIn && m_timerEnableDateTime.HasValue;
      }
    }
    #endregion // Getters / Setters

    #region Constructors / Destructor / ToString methods
    /// <summary>
    /// Description of the constructor
    /// </summary>
    public TimerOffDelayWithReset ()
      : base ("Lemoine.Cnc.InOut.TimerOffDelayWithReset")
    {
    }

    /// <summary>
    /// <see cref="IDisposable.Dispose" />
    /// </summary>
    public void Dispose ()
    {
      // Do nothing special here
      GC.SuppressFinalize (this);
    }
    #endregion

    #region Methods
    /// <summary>
    /// Start method: reset the different values
    /// </summary>
    public void Start ()
    {
      m_enableInSet = false;
      m_timerEnableSet = false;
    }

    /// <summary>
    /// Finish method
    /// </summary>
    public void Finish ()
    {
      ValidateInput ();
    }

    void ValidateInput ()
    {
      if (!this.m_enableInSet && this.EnableInRequired) {
        this.EnableIn = false;
      }
      if (!this.m_timerEnableSet) {
        this.TimerEnable = false;
      }
    }
    #endregion // Methods
  }
}
