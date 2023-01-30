// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;

using Lemoine.Core.Log;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Get the machine activity (running property) from the feedrate and/or rapid traverse rate
  /// </summary>
  public sealed class MachineActivity : Lemoine.Cnc.BaseCncModule, Lemoine.Cnc.ICncModule, IDisposable
  {
    #region Members
    double m_feedrate = -1.0; // Not set
    double m_feedrateUS = -1.0; // Not set
    double m_feedrateThreshold = 1.0;
    double m_rapidTraverseRate = -1.0; // Not set
    double m_rapidTraverseRateUS = -1.0; // Not set
    double m_rapidTraverseRateThreshold = 1.0;
    double m_spindleSpeed = 0.0;
    bool m_spindleSpeedSet = false;
    double m_spindleSpeedThreshold = 0.0;
    #endregion

    #region Getters / Setters
    /// <summary>
    /// Feedrate threshold in mm
    /// 
    /// Default value is 1.0
    /// </summary>
    public double FeedrateThreshold
    {
      get { return m_feedrateThreshold; }
      set { m_feedrateThreshold = value; }
    }

    /// <summary>
    /// Rapid traverse rate threshold in mm
    /// 
    /// Default value is 1.0
    /// </summary>
    public double RapidTraverseRateThreshold
    {
      get { return m_rapidTraverseRateThreshold; }
      set { m_rapidTraverseRateThreshold = value; }
    }

    /// <summary>
    /// Spindle speed threshold in IPM
    /// 
    /// Default value is 0.0
    /// </summary>
    public double SpindleSpeedThreshold
    {
      get { return m_spindleSpeedThreshold; }
      set { m_spindleSpeedThreshold = value; }
    }

    /// <summary>
    /// Feedrate
    /// </summary>
    public double Feedrate
    {
      get {
        if (-1 == m_feedrate) {
          log.ErrorFormat ("Feedrate.get: " +
                           "the feedrate is unknown");
          throw new Exception ("Unknown feedrate");
        }
        return m_feedrate;
      }
      set { m_feedrate = value; }
    }

    /// <summary>
    /// Rapid traverse rate
    /// </summary>
    public double RapidTraverseRate
    {
      get {
        if (-1 == m_rapidTraverseRate) {
          log.ErrorFormat ("RapidTraverseRate.get: " +
                           "the rapid traverse rate is unknown");
          throw new Exception ("Unknown rapid traverse rate");
        }
        return m_rapidTraverseRate;
      }
      set { m_rapidTraverseRate = value; }
    }

    /// <summary>
    /// Feedrate US
    /// </summary>
    public double FeedrateUS
    {
      get {
        if (-1 == m_feedrateUS) {
          log.ErrorFormat ("FeedrateUS.get: " +
                           "the feedrate US is unknown");
          throw new Exception ("Unknown feedrate US");
        }
        return m_feedrateUS;
      }
      set { m_feedrateUS = value; }
    }

    /// <summary>
    /// Rapid traverse rate US
    /// </summary>
    public double RapidTraverseRateUS
    {
      get {
        if (-1 == m_rapidTraverseRateUS) {
          log.ErrorFormat ("RapidTraverseRateUS.get: " +
                           "the rapid traverse rate US is unknown");
          throw new Exception ("Unknown rapid traverse rate US");
        }
        return m_rapidTraverseRateUS;
      }
      set { m_rapidTraverseRateUS = value; }
    }

    /// <summary>
    /// Spindle speed
    /// </summary>
    public double SpindleSpeed
    {
      get {
        if (!m_spindleSpeedSet) {
          log.DebugFormat ("SpindleSpeed.get: " +
                           "the spindle speed is unknown");
          throw new Exception ("Unknown spindle speed");
        }
        return m_spindleSpeed;
      }
      set {
        m_spindleSpeed = value;
        m_spindleSpeedSet = true;
      }
    }

    /// <summary>
    /// Determine if the machine is running from the motion
    /// </summary>
    public bool Running
    {
      get {
        try {
          return Motion && (!m_spindleSpeedSet || SpindleMotion);
        }
        catch (Exception ex) {
          log.Error ($"Running: exception", ex);
          throw;
        }
      }
    }

    /// <summary>
    /// Determine if the machine is in motion from the feedrate
    /// </summary>
    public bool Motion
    {
      get {
        if ((m_feedrate < 0) && (m_rapidTraverseRate < 0) && (m_feedrateUS < 0) && (m_rapidTraverseRateUS < 0)) {
          log.Error ("Motion: the feedrate and rapid traverse rate are unknown => could not determine if the machine is running");
          throw new Exception ("Feedrate unknown");
        }
        if (m_feedrate > m_feedrateThreshold) {
          log.Debug ($"Motion: yes ! from feedrate {m_feedrate}");
          return true;
        }
        if (m_feedrateUS > Lemoine.Conversion.Converter.ConvertToInches (m_feedrateThreshold)) {
          log.Debug ($"Motion: yes ! from feedrate US {m_feedrateUS}");
          return true;
        }
        if (m_rapidTraverseRate > m_rapidTraverseRateThreshold) {
          log.Debug ($"Motion: yes ! from rapid traverse rate {m_rapidTraverseRate}");
          return true;
        }
        if (m_rapidTraverseRateUS > Lemoine.Conversion.Converter.ConvertToInches (m_rapidTraverseRateThreshold)) {
          log.Debug ($"Motion: yes ! from rapid traverse rate US {m_rapidTraverseRateUS}");
          return true;
        }
        log.Debug ($"Motion: no ! from feed {m_feedrate} / us:{m_feedrateUS} and rapid traverse rate {m_rapidTraverseRate} / us:{m_rapidTraverseRateUS}");
        return false;
      }
    }

    /// <summary>
    /// Determine if the spindle is in motion from the spindle speed
    /// </summary>
    public bool SpindleMotion
    {
      get {
        if (!m_spindleSpeedSet) {
          log.Debug ($"SpindleMotion: the feedrate and rapid traverse rate are unknown => could not determine if the machine is running");
          throw new Exception ("Spindle speed unknown");
        }
        if (m_spindleSpeed > m_spindleSpeedThreshold) {
          log.Debug ($"SpindleMotion: yes ! from spindle speed {m_spindleSpeed}");
          return true;
        }
        return false;
      }
    }
    #endregion

    #region Constructors / Destructor / ToString methods
    /// <summary>
    /// Description of the constructor
    /// </summary>
    public MachineActivity ()
      : base ("Lemoine.Cnc.InOut.MachineActivity")
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
      m_feedrate = -1;
      m_feedrateUS = -1;
      m_rapidTraverseRate = -1;
      m_rapidTraverseRateUS = -1;
      m_spindleSpeedSet = false;
      m_spindleSpeed = 0.0;
    }
    #endregion
  }
}
