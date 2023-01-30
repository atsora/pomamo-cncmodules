// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;

using Lemoine.Core.Log;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Consider a rapid traverse if the feedrate is greater than a standard rapid traverse rate - tolerance
  /// </summary>
  public sealed class RapidTraverseFromStandard: Lemoine.Cnc.BaseCncModule, Lemoine.Cnc.ICncModule, IDisposable
  {
    #region Members
    double? m_feedrate;
    double? m_standardRapidTraverseRate;
    int m_tolerance = 5; // 5%
    #endregion // Members

    #region Getters / Setters
    /// <summary>
    /// Tolerance in %
    /// 
    /// Default is 5%
    /// </summary>
    public int Tolerance
    {
      get { return m_tolerance; }
      set { m_tolerance = value; }
    }
    
    /// <summary>
    /// Feedrate
    /// </summary>
    public double Feedrate
    {
      get
      {
        if (m_feedrate.HasValue) {
          return m_feedrate.Value;
        }
        else {
          throw new Exception ("Feedrate is not set");
        }
      }
      set { m_feedrate = value; }
    }
    
    /// <summary>
    /// Standard rapid traverse rate
    /// </summary>
    public double StandardRapidTraverseRate
    {
      get
      {
        if (m_standardRapidTraverseRate.HasValue) {
          return m_standardRapidTraverseRate.Value;
        }
        else {
          throw new Exception ("StandardRapidTraverseRate is not set");
        }
      }
      set { m_standardRapidTraverseRate = value; }
    }
    
    /// <summary>
    /// Rapid traverse
    /// </summary>
    public bool RapidTraverse
    {
      get
      {
        if (!m_feedrate.HasValue) {
          log.ErrorFormat ("RapidTraverse: " +
                           "no feedrate value");
          throw new Exception ("No feedrate");
        }
        if (!m_standardRapidTraverseRate.HasValue) {
          log.ErrorFormat ("RapidTraverse: " +
                           "no standard rapid traverse rate");
          throw new Exception ("No standard rapid traverse rate");
        }
        if (m_standardRapidTraverseRate.Value * (100.0-m_tolerance) / 100.0 <= m_feedrate.Value) {
          log.DebugFormat ("RapidTraverse: " +
                           "rapid traverse");
          return true;
        }
        else {
          return false;
        }
      }
    }
    
    /// <summary>
    /// Rapid traverse rate from the feedrate in case it is considered as is
    /// </summary>
    public double RapidTraverseRate
    {
      get
      {
        if (!RapidTraverse) {
          log.ErrorFormat ("RapidTraverseRate: " +
                           "not a rapid traverse");
          throw new Exception ("Not a rapid traverse");
        }
        return m_feedrate.Value;
      }
    }
    #endregion // Getters / Setters

    #region Constructors / Destructor / ToString methods
    /// <summary>
    /// Description of the constructor
    /// </summary>
    public RapidTraverseFromStandard ()
      : base("Lemoine.Cnc.InOut.RapidTraverseFromStandard")
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

    #endregion // Constructors / Destructor / ToString methods

    #region Methods
    /// <summary>
    /// Start method to reset the fact there is no new position for the moment
    /// </summary>
    public void Start ()
    {
      m_feedrate = null;
      m_standardRapidTraverseRate = null;
    }
    #endregion // Methods
  }
}
