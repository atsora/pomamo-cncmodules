// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;

using Lemoine.Core.Log;

namespace Lemoine.Cnc
{
  /// <summary>
  /// From the running time, determine if the machine is running or not
  /// </summary>
  public sealed class RunningTime: Lemoine.Cnc.BaseCncModule, Lemoine.Cnc.ICncModule, IDisposable
  {
    #region Members
    double? m_previousTime = null;
    double? m_currentTime = null;
    #endregion // Members

    #region Getters / Setters
    /// <summary>
    /// New running time in seconds
    /// </summary>
    public double TimeInSeconds {
      set { m_currentTime = value; }
    }
    
    /// <summary>
    /// Running status from the running time
    /// </summary>
    public bool Running {
      get
      {
        if (!m_previousTime.HasValue) {
          log.DebugFormat ("Running.get: " +
                           "no previous running time " +
                           "=> Running could not be determined, throw an exception");
          throw new Exception ("No previous running time");
        }
        else if (!m_currentTime.HasValue) {
          log.DebugFormat ("Running.get: " +
                           "no current running time " +
                           "=> Running could not be determined, throw an exception");
          throw new Exception ("No current running time");
        }
        else { // m_previousTime.HasValue && m_currentTime.HasValue
          log.DebugFormat ("Running.get: " +
                           "previous={0} VS current={1}",
                           m_previousTime, m_currentTime);
          if (m_currentTime.Value < m_previousTime.Value) {
            m_previousTime = null;
            throw new Exception ("Reset of the times");
          }
          return m_previousTime < m_currentTime;
        }
      }
    }
    #endregion // Getters / Setters

    #region Constructors
    /// <summary>
    /// Description of the constructor
    /// </summary>
    public RunningTime ()
      : base("Lemoine.Cnc.InOut.RunningTime")
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
    #endregion // Constructors

    #region Methods
    /// <summary>
    /// Start method
    /// </summary>
    /// <returns></returns>
    public bool Start ()
    {
      m_currentTime = null;
      return true;
    }
    
    /// <summary>
    /// Finish method
    /// </summary>
    public void Finish ()
    {
      m_previousTime = m_currentTime;
    }
    
    /// <summary>
    /// Alternative method to get the running status
    /// </summary>
    /// <returns></returns>
    public bool GetRunning ()
    {
      return this.Running;
    }
    #endregion // Methods
  }
}
