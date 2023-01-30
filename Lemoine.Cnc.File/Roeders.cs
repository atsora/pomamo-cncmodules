// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;

using Lemoine.Core.Log;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Röders acquisition: override FileXml to consider the /erp/data/time node
  /// </summary>
  public class Roeders: FileXml
  {
    #region Members
    TimeSpan m_timeLag;
    bool m_timeLagInitialized = false;
    TimeSpan m_timeLagShift;
    #endregion // Members

    #region Getters / Setters
    /// <summary>
    /// Last write age
    /// </summary>
    public override TimeSpan LastWriteAge {
      get { return m_timeLagShift; }
    }
    #endregion // Getters / Setters

    #region Constructors
    /// <summary>
    /// Description of the constructor
    /// </summary>
    public Roeders ()
      : base ("Lemoine.Cnc.In.Roeders")
    {
    }
    #endregion // Constructors

    #region Methods
    /// <summary>
    /// Start method
    /// </summary>
    /// <returns></returns>
    public override bool Start ()
    {
      if (!base.Start ()) {
        log.ErrorFormat ("Start: " +
                         "FileXml.Start returned false");
        return false;
      }

      try {
        DateTime now = DateTime.Now;
        string roedersTimeString = GetString ("/erp/data/time");
        DateTime roedersTime = DateTime.Parse (roedersTimeString);
        TimeSpan newLag = now.Subtract (roedersTime);
        if (!m_timeLagInitialized) {
          m_timeLag = newLag;
          m_timeLagInitialized = true;
        }
        else {
          if (newLag < m_timeLag) { // Better time lag: store it
            m_timeLag = newLag;
            m_timeLagShift = TimeSpan.FromSeconds (0);
          }
          else {
            m_timeLagShift = newLag.Subtract (m_timeLag);
          }
        }
      }
      catch (Exception ex) {
        log.ErrorFormat ("Start: " +
                         "error while processing the time lag " +
                         "but dismiss it " +
                         "{0}",
                         ex);
      }
      
      return true;
    }
    #endregion // Methods
  }
}
