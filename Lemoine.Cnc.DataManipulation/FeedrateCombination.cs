// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;

using System.Diagnostics;
using Lemoine.Core.Log;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Get the feedrate or rapid traverse from a detected feedrate and/or a computed feedrate and/or a programmed feedrate
  /// </summary>
  public sealed class FeedrateCombination : Lemoine.Cnc.BaseCncModule, Lemoine.Cnc.ICncModule, IDisposable
  {
    #region Members
    double? m_feedrate = null; // Not set
    double? m_computedFeedrate = null; // Not set
    bool m_activeProgrammedFeedrate = false;
    double? m_programmedFeedrate = null; // Not set
    double? m_feedrateOverride = null; // Not set
    bool m_isComputed = false;
    bool m_isFromProgrammed = false;
    #endregion

    #region Getters / Setters
    /// <summary>
    /// Feedrate
    /// </summary>
    public double Feedrate
    {
      get {
        if (!m_feedrate.HasValue) {
          log.Error ("Feedrate.get: the feedrate is unknown");
          throw new Exception ("Unknown feedrate");
        }
        else {
          return m_feedrate.Value;
        }
      }
      set {
        if (value < 0) {
          log.Error ($"Feedrate.set: negative value {value}, store a positive value instead");
          m_feedrate = -value;
        }
        else {
          m_feedrate = value;
        }
      }
    }

    /// <summary>
    /// Computed feedrate
    /// </summary>
    public double ComputedFeedrate
    {
      get {
        if (!m_computedFeedrate.HasValue) {
          log.ErrorFormat ("ComputedFeedrate.get: " +
                           "the computed feedrate is unknown");
          throw new Exception ("Unknown computed feedrate");
        }
        else {
          return m_computedFeedrate.Value;
        }
      }
      set {
        if (value < 0) {
          log.Fatal ($"ComputedFeedrate.set: negative value {value}, store a positive value instead");
          m_computedFeedrate = -value;
        }
        else {
          m_computedFeedrate = value;
        }
      }
    }

    /// <summary>
    /// The programmed feedrate may be considered
    /// </summary>
    public bool ActiveProgrammedFeedrate
    {
      get { return m_activeProgrammedFeedrate; }
      set { m_activeProgrammedFeedrate = value; }
    }

    /// <summary>
    /// Programmed feedrate
    /// </summary>
    public double ProgrammedFeedrate
    {
      get {
        if (!m_programmedFeedrate.HasValue) {
          log.Error ("ProgrammedFeedrate.get: the programmed feedrate is unknown");
          throw new Exception ("Unknown programmed feedrate");
        }
        else {
          return m_programmedFeedrate.Value;
        }
      }
      set {
        if (value < 0) {
          log.Error ($"ProgrammedFeedrate.set: negative value {value}, store a positive value instead");
          m_programmedFeedrate = -value;
        }
        else {
          m_programmedFeedrate = value;
        }
      }
    }

    /// <summary>
    /// Feedrate override (default 100%)
    /// </summary>
    public double FeedrateOverride
    {
      get {
        if (!m_feedrateOverride.HasValue) {
          log.Error ("FeedrateOverride.get: the feedrate override is unknown");
          throw new Exception ("Unknown feedrate override");
        }
        else {
          return m_feedrateOverride.Value;
        }
      }
      set {
        if (value < 0) {
          log.Fatal ($"FeedrateOverride.set: about to set a negative feedrate override {value} => not expected");
          throw new ArgumentOutOfRangeException ("value");
        }
        m_feedrateOverride = value;
      }
    }
    #endregion

    #region Constructors / Destructor / ToString methods
    /// <summary>
    /// Description of the constructor
    /// </summary>
    public FeedrateCombination ()
      : base ("Lemoine.Cnc.InOut.FeedrateCombination")
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
    /// Start method: reset the internal value
    /// </summary>
    public void Start ()
    {
      m_feedrate = null;
      m_computedFeedrate = null;
      m_activeProgrammedFeedrate = false;
      m_programmedFeedrate = null;
      m_feedrateOverride = null;
      m_isComputed = false;
      m_isFromProgrammed = false;
    }

    /// <summary>
    /// Combine the feedrates
    /// </summary>
    /// <param name="param">unused</param>
    /// <returns></returns>
    public double GetFeedrate (string param)
    {
      if (m_feedrate.HasValue) {
        if (log.IsDebugEnabled) {
          log.Debug ($"GetFeedrate: detected feedrate {m_feedrate}");
        }
        return this.Feedrate;
      }
      if (!m_activeProgrammedFeedrate || !m_feedrateOverride.HasValue || !m_programmedFeedrate.HasValue) {
        if (m_computedFeedrate.HasValue) {
          if (log.IsDebugEnabled) {
            log.Debug ($"GetFeedrate: from computed {m_computedFeedrate}");
          }
          m_isComputed = true;
          return this.ComputedFeedrate;
        }
      }
      else { // m_activeProgrammedFeedrate
        Debug.Assert (m_activeProgrammedFeedrate);
        Debug.Assert (m_feedrateOverride.HasValue);
        Debug.Assert (m_programmedFeedrate.HasValue);
        double fromProgrammed = m_feedrateOverride.Value / 100.0 * m_programmedFeedrate.Value;
        if (log.IsDebugEnabled) {
          log.Debug ($"GetFeedrate: from programmed {m_programmedFeedrate} => {fromProgrammed}");
        }
        m_isFromProgrammed = true;
        return fromProgrammed;
      }

      log.DebugFormat ("GetFeedrate: no feedrate could be determined");
      throw new Exception ("No feedrate");
    }

    /// <summary>
    /// Is the feedrate computed
    /// 
    /// To be called after GetFeedrate
    /// </summary>
    /// <param name="param"></param>
    /// <returns></returns>
    public bool IsComputed (string param)
    {
      if (m_isComputed) {
        return true;
      }
      else {
        throw new Exception ("Not computed");
      }
    }

    /// <summary>
    /// Is the return feedrate from the programmed feedrate
    /// 
    /// To be called after GetFeedrate
    /// </summary>
    /// <param name="param"></param>
    /// <returns></returns>
    public bool IsFromProgrammed (string param)
    {
      if (m_isFromProgrammed) {
        return true;
      }
      else {
        throw new Exception ("Not from programmed feedrate");
      }
    }
    #endregion
  }
}
