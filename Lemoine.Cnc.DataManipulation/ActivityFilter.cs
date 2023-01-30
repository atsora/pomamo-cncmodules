// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Diagnostics;
using Lemoine.Core.Log;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Module to filter the raw activity
  /// from the Perle units for example
  /// </summary>
  public sealed class ActivityFilter: Lemoine.Cnc.BaseCncModule, Lemoine.Cnc.ICncModule, IDisposable
  {
    static readonly double DEFAULT_MINIMUM_VALID_DURATION = 20.0;
    static readonly double DEFAULT_SHORTEST_RETURNED_ACTIVITY = 2.0;
    
    #region Members
    TimeSpan m_minimumValidDuration = TimeSpan.FromSeconds (DEFAULT_MINIMUM_VALID_DURATION);
    TimeSpan m_shortestReturnedActivity = TimeSpan.FromSeconds (DEFAULT_SHORTEST_RETURNED_ACTIVITY);
    
    bool m_input = false;
    bool m_returnedActivity = false;
    /// <summary>
    /// Last activity
    /// </summary>
    bool m_lastActivity;
    DateTime m_lastActivityDateTime = DateTime.UtcNow;
    /// <summary>
    /// Begin date/time of the recorded period
    /// </summary>
    DateTime m_begin = DateTime.UtcNow;
    /// <summary>
    /// End date/time of the recorded period
    /// </summary>
    DateTime m_end = DateTime.UtcNow;
    TimeSpan m_activityDuration = TimeSpan.FromTicks (0);
    TimeSpan m_inactivityDuration = TimeSpan.FromTicks (0);
    #endregion // Members

    #region Getters / Setters    
    /// <summary>
    /// Minimum duration of an activity period in seconds
    /// so that it is taken directly into account
    /// 
    /// Default is 20s
    /// </summary>
    public double MinimumValidDuration {
      get { return m_minimumValidDuration.TotalSeconds; }
      set { m_minimumValidDuration = TimeSpan.FromSeconds (value); }
    }
    
    /// <summary>
    /// Shortest time in seconds
    /// to meet so that an activity period is returned
    /// 
    /// Default is 2s
    /// </summary>
    public double ShortestReturnedActivity {
      get { return m_shortestReturnedActivity.TotalSeconds; }
      set { m_shortestReturnedActivity = TimeSpan.FromSeconds (value); }
    }
    
    /// <summary>
    /// Raw activity status from the machine
    /// </summary>
    public bool InputActivity {
      set
      {
        m_input = true;
        
        if (m_lastActivity == value) { // Unchanged
          if (m_minimumValidDuration <=
              DateTime.UtcNow.Subtract (m_lastActivityDateTime)) {
            // Current activity is longer than MinimumDuration
            // => take it into account
            //    and reset the recorded period
            m_returnedActivity = value;
            ResetPeriodWithLastActivity ();
            return;
          }

          // Update the activity and inactivity durations
          if (value) { // active
            m_activityDuration +=
              DateTime.UtcNow.Subtract (m_end);
          }
          else {
            m_inactivityDuration +=
              DateTime.UtcNow.Subtract (m_end);
          }
          m_end = DateTime.UtcNow;

          // Check the activity and inactivity durations
          // are still coherent with the returned value
          if ((m_inactivityDuration < m_activityDuration) // => should be active
              != m_returnedActivity) {
            if (m_shortestReturnedActivity
                < DateTime.UtcNow.Subtract (m_lastActivityDateTime)) {
              m_returnedActivity = !m_returnedActivity;
              ResetPeriodWithLastActivity ();
            }
          }
        }
        else { // New value
          m_lastActivity = value;
          m_lastActivityDateTime = DateTime.UtcNow;
          m_end = DateTime.UtcNow;
        }
      }
    }
    
    /// <summary>
    /// Filtered activity
    /// </summary>
    public bool FilteredActivity {
      get
      {
        if (m_input) {
          return m_returnedActivity;
        }
        else {
          throw new Exception ();
        }
      }
    }
    #endregion // Getters / Setters

    #region Constructors / Destructor / ToString methods
    /// <summary>
    /// Description of the constructor
    /// </summary>
    public ActivityFilter ()
      : base("Lemoine.Cnc.InOut.ActivityFilter")
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
    
    /// <summary>
    /// <see cref="Object.ToString" />
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
      return String.Format ("CNC module {0}.{1} [{2}]",
                            this.GetType ().FullName,
                            this.CncAcquisitionId,
                            this.CncAcquisitionName);
    }
    #endregion // Constructors / Destructor / ToString methods

    #region Methods
    void Start ()
    {
      m_input = false;
    }
    
    /// <summary>
    /// Reset the recorded period with the last activity
    /// </summary>
    void ResetPeriodWithLastActivity ()
    {
      m_begin = m_lastActivityDateTime;
      m_end = DateTime.UtcNow;
      if (m_lastActivity) { // active
        m_activityDuration =
          m_end.Subtract (m_begin);
        m_inactivityDuration = TimeSpan.FromTicks (0);
      }
      else { // inactive
        m_inactivityDuration =
          m_end.Subtract (m_begin);
        m_activityDuration = TimeSpan.FromTicks (0);
      }
    }    
    #endregion // Methods
  }
}
