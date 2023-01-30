// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Diagnostics;
using Lemoine.Model;
using Lemoine.ModelDAO;
using Lemoine.Core.Log;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Class to store the current data directly into the database
  /// </summary>
  public sealed partial class GDBCurrent : Lemoine.Cnc.BaseCncModule, Lemoine.Cnc.ICncModule, IDisposable
  {
    static readonly TimeSpan FREQUENCY_DEFAULT = TimeSpan.FromSeconds (4);
    static readonly string FREQUENCY_KEY = "Cnc.GDBCurrent.Frequency";

    #region Members
    bool m_disposed = false;
    int m_machineId = 0;
    int m_machineModuleId = 0;

    IDAOSession m_session = null;
    IMonitoredMachine m_machine = null;
    IMachineModule m_machineModule = null;

    DateTime m_last = DateTime.UtcNow.Subtract (TimeSpan.FromMinutes (1));
    TimeSpan m_frequency = FREQUENCY_DEFAULT;
    #endregion // Members

    #region Getters / Setters
    /// <summary>
    /// Session
    /// </summary>
    IDAOSession Session
    {
      get
      {
        if (null == m_session) {
          m_session = ModelDAOHelper.DAOFactory.OpenSession ();
        }
        return m_session;
      }
    }

    /// <summary>
    /// Machine Id
    /// </summary>
    public int MachineId
    {
      get { return m_machineId; }
      set
      {
        if (m_machineId != value) {
          m_machineId = value;
          UpdateLogger ();
        }
      }
    }

    /// <summary>
    /// Machine Module Id
    /// </summary>
    public int MachineModuleId
    {
      get { return m_machineModuleId; }
      set
      {
        if (m_machineModuleId != value) {
          m_machineModuleId = value;
          UpdateLogger ();
        }
      }
    }
    #endregion // Getters / Setters

    #region Constructors / Destructor / ToString methods
    /// <summary>
    /// Description of the constructor
    /// </summary>
    public GDBCurrent () : base ("Lemoine.Cnc.Out.GDBCurrent") { }
    #endregion // Constructors / Destructor / ToString methods

    #region Methods
    /// <summary>
    /// Start method
    /// </summary>
    public bool Start ()
    {
      if (0 == m_machineId) {
        log.Error ("Start: " +
                   "machineId is still 0 " +
                   "=> return false");
        return false;
      }

      // Check if the process should be skipped or not because of the frequency parameter
      Debug.Assert (m_last <= DateTime.UtcNow);
      TimeSpan lastAge = DateTime.UtcNow.Subtract (m_last);
      if (lastAge < m_frequency) {
        log.DebugFormat ("Start: " +
                         "last execution was done {0} ago, " +
                         "less than frequency {1} " +
                         "=> skip the process",
                         lastAge,
                         m_frequency);
        return false;
      }
      else {
        log.DebugFormat ("Start: " +
                         "last execution {0} is old enough " +
                         "compare to frequency {1} " +
                         "=> process",
                         m_last,
                         m_frequency);
      }

      // Initialize m_machine and m_moduleMachine
      Debug.Assert (0 != m_machineId);
      try {
        using (var transaction = this.Session.BeginReadOnlyTransaction ("Cnc.Current.Start")) {
          // Overwrite m_frequency by the configuration value in database, if any
          m_frequency = Lemoine.Info.ConfigSet.LoadAndGet<TimeSpan> (FREQUENCY_KEY,
                                                                     FREQUENCY_DEFAULT);

          m_machine = ModelDAOHelper.DAOFactory.MonitoredMachineDAO.FindById (m_machineId);
          m_machineModule = ModelDAOHelper.DAOFactory.MachineModuleDAO.FindById (m_machineModuleId);
        }
      }
      catch (Exception ex) {
        log.Error ("Start: exception in initializing the machine and the machine module", ex);
        CloseSession ();
        return false;
      }

      return true;
    }

    /// <summary>
    /// Finish method
    /// </summary>
    public void Finish ()
    {
      CloseSession ();
      m_last = DateTime.UtcNow;
    }

    void CloseSession ()
    {
      if (null != m_session) {
        try {
          m_session.Dispose ();
        }
        catch (Exception ex) {
          log.Error ("CloseSession: disposing the current session failed", ex);
        }
        finally {
          m_session = null;
        }
      }
    }

    void UpdateLogger ()
    {
      if (0 != m_machineModuleId) {
        log = LogManager.GetLogger ($"Lemoine.Cnc.Out.GDBCurrent.{this.CncAcquisitionId}.{m_machineId}.{m_machineModuleId}");
      }
      else {
        log = LogManager.GetLogger ($"Lemoine.Cnc.Out.GDBCurrent.{this.CncAcquisitionId}.{m_machineId}");
      }
    }
    #endregion // Methods

    #region IDisposable implementation
    void Dispose (bool disposing) {
      if (!m_disposed) {
        if (disposing) {
          // Dispose any managed objects
          CloseSession ();
        }
      }

      // Disposed any unamanged objects

      m_disposed = true;
    }

    /// <summary>
    /// <see cref="IDisposable"/>
    /// </summary>
    public void Dispose ()
    {
      Dispose (true);
      GC.SuppressFinalize (this);
    }

    /// <summary>
    /// <see cref="IDisposable"/>
    /// </summary>
    ~GDBCurrent ()
    {
      Dispose (false);
    }
    #endregion // IDisposable implementation
  }
}
