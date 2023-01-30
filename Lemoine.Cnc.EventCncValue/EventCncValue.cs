// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections.Generic;
using System.Diagnostics;

using Lemoine.Model;
using Lemoine.ModelDAO;
using Lemoine.Core.Log;
using Microsoft.CodeAnalysis.Scripting;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Scripting;

namespace Lemoine.Cnc
{
  class EventCncValueStatus
  {
    public IEventCncValueConfig Config { get; private set; }
    public DateTime? CheckedBeginDateTime { get; set; }
    public object LastValue { get; set; }
    public object ChangeTracker { get; set; }
    public bool Sent { get; set; }
    public Func<object, bool> ConditionFunction { get; set; }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="config"></param>
    public EventCncValueStatus (IEventCncValueConfig config)
    {
      this.Config = config;
      this.CheckedBeginDateTime = null;
      this.LastValue = null;
      this.ChangeTracker = null;
      this.Sent = false;
      this.ConditionFunction = null;
    }

    /// <summary>
    /// To run when the condition is not checked any more
    /// or just to clear the data
    /// </summary>
    public void Clear ()
    {
      this.CheckedBeginDateTime = null;
      this.LastValue = null;
      this.Sent = false;
    }
  }

  /// <summary>
  /// Class to store the current data directly into the database
  /// </summary>
  public sealed class EventCncValue : Lemoine.Cnc.BaseCncModule, Lemoine.Cnc.ICncModule
  {
    #region Members
    int m_machineModuleId = 0;
    IMachineModule m_machineModule = null;
    bool m_error = false;
    IDictionary<string, EventCncValueStatus> m_status = new Dictionary<string, EventCncValueStatus> ();
    readonly ScriptOptions m_scriptOptions;
    #endregion // Members

    #region Getters / Setters
    /// <summary>
    /// Machine Module Id
    /// </summary>
    public int MachineModuleId
    {
      get { return m_machineModuleId; }
      set { m_machineModuleId = value; }
    }

    /// <summary>
    /// Error ?
    /// </summary>
    public bool Error
    {
      get { return m_error; }
    }
    #endregion // Getters / Setters

    #region Constructors / Destructor / ToString methods
    /// <summary>
    /// Description of the constructor
    /// </summary>
    public EventCncValue ()
      : base ("Lemoine.Cnc.Out.EventCncValue")
    {
      m_scriptOptions = ScriptOptions.Default
        .AddReferences (new System.Reflection.Assembly[] {
          typeof (EventCncValue).Assembly,
          typeof (Convert).Assembly
        })
        .AddImports (new string[] { "System" });
    }
    #endregion // Constructors / Destructor / ToString methods

    #region Methods
    /// <summary>
    /// Start method
    /// </summary>
    public bool Start ()
    {
      m_error = false;

      if (0 == m_machineModuleId) {
        log.Error ("Start: " +
                   "machineId is still 0 " +
                   "=> return false");
        m_error = true;
        return false;
      }

      if (null == m_machineModule) {
        using (IDAOSession session = ModelDAOHelper.DAOFactory.OpenSession ()) {
          m_machineModule = ModelDAOHelper.DAOFactory.MachineModuleDAO
            .FindByIdWithMonitoredMachine (m_machineModuleId);
        }
        if (null == m_machineModule) {
          log.ErrorFormat ("Start: " +
                           "Machine module with ID {0} does not exist",
                           m_machineModuleId);
          m_error = true;
          return false;
        }
        UpdateLogger ();
      }

      return true;
    }

    /// <summary>
    /// Finish method
    /// </summary>
    public void Finish ()
    {
    }

    /// <summary>
    /// Associate a CNC value to a config
    /// </summary>
    /// <param name="param"></param>
    /// <param name="v"></param>
    public void AssociateCncValueToConfig (string param, object v)
    {
      log.DebugFormat ("AssociateCncValueToConfig /B: " +
                       "config={0}",
                       param);

      // - Get or create the status
      EventCncValueStatus status;
      if (false == m_status.TryGetValue (param, out status)) {
        // - Get the config
        log.DebugFormat ("AssociateCncValueToConfig: " +
                         "initialize the config for {0}",
                         param);
        IEventCncValueConfig config;
        using (IDAOSession session = ModelDAOHelper.DAOFactory.OpenSession ()) {
          config = ModelDAOHelper.DAOFactory.EventCncValueConfigDAO.FindByName (param);
          // - Check the machine filter
          if ((null != config) && (null != config.MachineFilter)) {
            if (false == config.MachineFilter.IsMatch (m_machineModule.MonitoredMachine)) {
              log.InfoFormat ("AssociateCncValueToConfig: " +
                              "machine {0} does not match with the config {1}",
                              m_machineModule.MonitoredMachine, param);
              config = null;
            }
          }
        }
        status = new EventCncValueStatus (config);
        if (null == status.Config) {
          log.ErrorFormat ("AssociateCncValueToConfig: " +
                           "configuration with name {0} does not exist or does not match the machine, " +
                           "skip the configuration",
                           param);
          m_status[param] = null;
          return;
        }
        { // Set ConditionFunction
          try {
            status.ConditionFunction = Task.Run (() => CSharpScript.EvaluateAsync<Func<object, bool>> (status.Config.Condition, m_scriptOptions)).Result;
            Debug.Assert (null != status.ConditionFunction);
          }
          catch (Exception ex1) {
            log.Error ($"AssociateCncValueToConfig: expression {status.ConditionFunction} could not be interpreted, skip config {param}", ex1);
            m_status[param] = null;
            return;
          }
        }
        Debug.Assert (null != status);
        m_status[param] = status;
      }
      if (null == status) {
        log.Debug ($"AssociateCncValueToConfig: configuration with name {param} was invalid and is now skipped");
        return;
      }
      Debug.Assert (null != status);
      Debug.Assert (null != status.Config);
      Debug.Assert (null != status.ConditionFunction);

      // Check the condition
      {
        try {
          if (!status.ConditionFunction.Invoke (v)) { // Condition is not checked => clear the active status and return
            log.DebugFormat ("AssociateCncValueToConfig: " +
                             "condition is not checked in {0} with value {1}, return",
                             status.Config.Condition, v);
            status.Clear ();
            return;
          }
        }
        catch (Exception ex2) {
          log.ErrorFormat ("AssociateCncValueToConfig: " +
                           "error in expression evaluation {0} with value {1}, " +
                           "{2}",
                           status.Config.Condition, v,
                           ex2);
          throw;
        }
      }

      // Update LastValue
      status.LastValue = v;

      // Check now the duration and update CheckedBeginDateTime
      TimeSpan age;
      if (status.CheckedBeginDateTime.HasValue) {
        age = DateTime.UtcNow.Subtract (status.CheckedBeginDateTime.Value);
      }
      else {
        age = TimeSpan.FromSeconds (0);
        status.CheckedBeginDateTime = DateTime.UtcNow;
      }
      if (status.Config.MinDuration <= age) {
        log.InfoFormat ("AssociateCncValueToConfig: " +
                        "min duration is checked => store the event");
        StoreEvent (status, v, age);
      }
      else {
        log.DebugFormat ("AssociateCncValueToConfig: " +
                         "condition is checked but age {0} < {1}",
                         age, status.Config.MinDuration);
      }
    }

    void StoreEvent (EventCncValueStatus status, object v, TimeSpan age)
    {
      if (status.Sent) {
        log.DebugFormat ("StoreEvent: " +
                         "the event has already been sent");
        return;
      }

      IEventCncValueConfig config = status.Config;
      IEventCncValue eventCncValue = ModelDAOHelper.ModelFactory.CreateEventCncValue (config.Level,
                                                                                      DateTime.UtcNow,
                                                                                      config.Message,
                                                                                      m_machineModule,
                                                                                      config.Field,
                                                                                      v,
                                                                                      age,
                                                                                      config);
      using (IDAOSession session = ModelDAOHelper.DAOFactory.OpenSession ())
      using (IDAOTransaction transaction = session.BeginTransaction ()) {
        ModelDAOHelper.DAOFactory.EventCncValueDAO.MakePersistent (eventCncValue);
        transaction.Commit ();
      }

      status.Sent = true;
    }

    /// <summary>
    /// Send the event if it is pending and the value changed
    /// 
    /// For example if a stamp changes
    /// </summary>
    /// <param name="param"></param>
    /// <param name="v"></param>
    public void SendIfChanged (string param, object v)
    {
      // Get the status
      EventCncValueStatus status;
      if (false == m_status.TryGetValue (param, out status)) {
        log.DebugFormat ("SendIfChanged: " +
                         "no status skip it");
        return;
      }
      if (null == status.Config) {
        log.ErrorFormat ("SendIfChanged: " +
                         "configuration with name {0} does not exist or does not match the machine",
                         param);
        return;
      }

      // Check if the value changed
      if (object.Equals (status.ChangeTracker, v)) {
        return;
      }
      else {
        status.ChangeTracker = v;
      }

      // Check if it has already been sent and there is something to send
      if (!status.Sent && status.CheckedBeginDateTime.HasValue) {
        StoreEvent (status, status.LastValue, DateTime.UtcNow.Subtract (status.CheckedBeginDateTime.Value));
      }

      status.Clear ();
    }

    void UpdateLogger ()
    {
      if (null != m_machineModule?.MonitoredMachine) {
        log = LogManager.GetLogger ($"Lemoine.Cnc.Out.EventCncValue.{this.CncAcquisitionId}.{m_machineModule.MonitoredMachine.Id}.{m_machineModuleId}");
      }
      else {
        log = LogManager.GetLogger ($"Lemoine.Cnc.Out.EventCncValue.{this.CncAcquisitionId}.{m_machineModuleId}");
      }
    }
    #endregion // Methods

    #region IDisposable implementation
    /// <summary>
    /// <see cref="IDisposable.Dispose" />
    /// </summary>
    public void Dispose ()
    {
      GC.SuppressFinalize (this);
    }
    #endregion // IDisposable implementation
  }
}
