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
  /// Module to add a notion of time to a value
  /// 
  /// If no value is pushed with the <see cref="SetIn(object)"/> method, then it is reset
  /// </summary>
  public sealed class Timer : Lemoine.Cnc.BaseCncModule, Lemoine.Cnc.ICncModule, IDisposable
  {
    #region Members
    object m_value;
    DateTime? m_changeDateTime = null;
    object m_newValue;
    #endregion

    #region Getters / Setters
    /// <summary>
    /// Trigger duration
    /// </summary>
    public TimeSpan TriggerDuration { get; set; }

    /// <summary>
    /// Trigger duration in string format
    /// </summary>
    public string TriggerString
    {
      get { return this.TriggerDuration.ToString (); }
      set { this.TriggerDuration = TimeSpan.Parse (value); }
    }

    /// <summary>
    /// Trigger duration in seconds
    /// </summary>
    public double TriggerTotalSeconds
    {
      get { return this.TriggerDuration.TotalSeconds; }
      set { this.TriggerDuration = TimeSpan.FromSeconds (value); }
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
          m_changeDateTime = null;
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
        if (m_changeDateTime.HasValue) {
          return DateTime.UtcNow.Subtract (m_changeDateTime.Value);
        }
        else {
          return TimeSpan.FromTicks (0);
        }
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
        return m_changeDateTime.HasValue;
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
        return m_changeDateTime.HasValue
          && (this.TriggerDuration <= this.ACC);
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
        return m_changeDateTime.HasValue
          && (this.ACC < this.TriggerDuration);
      }
    }
    #endregion // Getters / Setters

    #region Constructors / Destructor / ToString methods
    /// <summary>
    /// Description of the constructor
    /// </summary>
    public Timer ()
      : base ("Lemoine.Cnc.InOut.Timer")
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
      m_newValue = null;
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
      if (null == m_newValue) { // Reset
        if (log.IsDebugEnabled) {
          log.Debug ("ValidateInput: no new value was set: reset everything");
        }
        m_value = null;
        m_changeDateTime = null;
        return;
      }

      if (!object.Equals (m_value, m_newValue)) {
        m_value = m_newValue;
        m_changeDateTime = DateTime.UtcNow;
      }
    }

    /// <summary>
    /// Set the new value (not null)
    /// </summary>
    /// <param name="data"></param>
    public void SetIn (object data)
    {
      if (null == data) {
        log.Error ("SetIn: data is null");
      }

      m_newValue = data;
    }

    /// <summary>
    /// Return a value only if the set value is longer than the trigger duration in parameter
    /// </summary>
    /// <param name="param"></param>
    /// <returns></returns>
    public object GetOut (string param)
    {
      if (log.IsDebugEnabled) {
        log.Debug ($"GetOut: param={param}");
      }

      ValidateInput ();

      if (null == m_value) {
        throw new Exception ();
      }

      if (this.DN) {
        return m_value;
      }
      else {
        throw new Exception ();
      }
    }
    #endregion // Methods
  }
}
