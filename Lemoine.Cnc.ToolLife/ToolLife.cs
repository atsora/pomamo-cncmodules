// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections.Generic;
using Lemoine.Core.SharedData;
using Lemoine.Model;
using Lemoine.ModelDAO;
using Lemoine.Core.Log;

namespace Lemoine.Cnc
{
  /// <summary>
  /// This class store all data related to the tool life in the database
  /// It locally stores the previous state and create tool life events if necessary
  /// </summary>
  public class ToolLife : BaseCncModule, ICncModule, IDisposable
  {
    // Offset for the new value allowed to detect reset instead of a life change
    static readonly string OFFSET_FOR_RESET_KEY = "Cnc.ToolLife.ResetDetection.Offset";
    static readonly double OFFSET_FOR_RESET_DEFAULT = 20;

    /// <summary>
    /// Public for the unit tests
    /// </summary>
    public double m_offsetForResetValue;

    // Percentage for the old value allowed to detect reset instead of a life change
    static readonly string PERCENT_FOR_RESET_KEY = "Cnc.ToolLife.ResetDetection.PercentToReach";
    static readonly double PERCENT_FOR_RESET_DEFAULT = 0;

    /// <summary>
    /// Public for the unit tests
    /// </summary>
    public double m_percentForResetValue;

    #region Members
    bool m_isInitialized = false;
    int m_machineId = 0;
    int m_machineModuleId = 0;
    bool m_error = false;
    bool m_previousStateOk = false;
    DateTime m_currentDateTime, m_previousDateTime;
    IMachineObservationState m_currentMachineObservationState = null;
    readonly IList<IEventToolLife> m_newEvents = new List<IEventToolLife> ();
    TimeSpan m_obsoleteDuration = TimeSpan.FromMinutes (1); // By default 1 minute
    readonly AntiDuplicate m_antiDuplicate = new AntiDuplicate ();

    // Translators toollifetype <-> unit
    readonly IDictionary<ToolUnit, IUnit> m_dicUnit = new Dictionary<ToolUnit, IUnit> ();

    // This member comprises all configurations possible for the current machine
    // The configurations are sorted by EventToolLifeType, the configuration whose
    // Machine Observation State (if any) being the first element of the list
    readonly IDictionary<EventToolLifeType, IList<IEventToolLifeConfig>> m_toolLifeEventConfigs =
      new Dictionary<EventToolLifeType, IList<IEventToolLifeConfig>> ();
    #endregion // Members

    #region Getters / Setters
    /// <summary>
    /// Machine Id
    /// </summary>
    public int MachineId
    {
      get { return m_machineId; }
      set {
        if (m_machineId != value) {
          m_machineId = value;
          UpdateLogger ();
        }
      }
    }

    /// <summary>
    /// Machine module id
    /// </summary>
    public int MachineModuleId
    {
      get { return m_machineModuleId; }
      set {
        if (value != m_machineModuleId) {
          m_machineModuleId = value;
          UpdateLogger ();
        }
      }
    }

    /// <summary>
    /// Error?
    /// </summary>
    public bool Error
    {
      get { return m_error; }
    }

    /// <summary>
    /// If true, tool data will be kept in the database even if the tool is not accessible
    /// This is useful in the case where the machine removes the tool from the pot, uses it
    /// and then reassign the tool (possibly in another pot)
    /// Or if we read only 2 tools at a time
    /// Default is false;
    /// </summary>
    public bool KeepRemovedTools { get; set; }
    #endregion // Getters / Setters

    #region Constructors
    /// <summary>
    /// Description of the constructor
    /// </summary>
    public ToolLife () : base ("Lemoine.Cnc.Out.ToolLife")
    {
      KeepRemovedTools = false;
      m_offsetForResetValue = Lemoine.Info.ConfigSet.LoadAndGet<double> (OFFSET_FOR_RESET_KEY, OFFSET_FOR_RESET_DEFAULT);
      m_percentForResetValue = Lemoine.Info.ConfigSet.LoadAndGet<double> (PERCENT_FOR_RESET_KEY, PERCENT_FOR_RESET_DEFAULT);
    }
    #endregion // Constructors

    #region Methods
    /// <summary>
    /// Start method
    /// </summary>
    public bool Start ()
    {
      m_currentDateTime = DateTime.UtcNow;
      m_error = false;

      if (m_machineModuleId == 0) {
        log.Error ("Start: machineModuleId is still 0 => return false");
        return false;
      }

      try {
        Initialize ();
      }
      catch (Exception ex) {
        log.Error ("Start: exception in Initialize, return false", ex);
        m_error = true;
        return false;
      }

      return true;
    }

    void UpdateLogger ()
    {
      if (0 != m_machineModuleId) {
        log = LogManager.GetLogger ($"Lemoine.Cnc.Out.ToolLife.{this.CncAcquisitionId}.{m_machineId}.{m_machineModuleId}");
      }
      else {
        log = LogManager.GetLogger ($"Lemoine.Cnc.Out.ToolLife.{this.CncAcquisitionId}.{m_machineId}");
      }
    }

    void Initialize ()
    {
      if (!m_isInitialized) {
        using (var session = ModelDAOHelper.DAOFactory.OpenSession ()) {
          using (var transaction = session.BeginReadOnlyTransaction ("Cnc.ToolLife.Initialize")) {
            InitializeUnits ();

            var timeSpan = ModelDAOHelper.DAOFactory.ConfigDAO.GetCncConfigValue (
              CncConfigKey.ToolLifeDataObsoleteTime) as TimeSpan?;
            if (timeSpan != null && timeSpan.HasValue) {
              m_obsoleteDuration = timeSpan.Value;
            }
          }
        }

        m_isInitialized = true;
      }
    }

    void InitializeUnits ()
    {
      m_dicUnit.Clear ();

      // For each tool life type, fill with the corresponding unit
      // And for each unit, fill with the corresponding tool life type
      using (IDAOSession session = ModelDAOHelper.DAOFactory.OpenSession ()) {
        var units = ModelDAOHelper.DAOFactory.UnitDAO.FindAll ();
        foreach (var unit in units) {
          SetActive ();
          switch (unit.Id) {
            case 14:
              m_dicUnit[ToolUnit.Unknown] = unit;
              break;
            case 11:
              m_dicUnit[ToolUnit.TimeSeconds] = unit;
              break;
            case 5:
              m_dicUnit[ToolUnit.Parts] = unit;
              break;
            case 15:
              m_dicUnit[ToolUnit.NumberOfTimes] = unit;
              break;
            case 16:
              m_dicUnit[ToolUnit.Wear] = unit;
              break;
            case 7:
              m_dicUnit[ToolUnit.DistanceMillimeters] = unit;
              break;
            case 8:
              m_dicUnit[ToolUnit.DistanceInch] = unit;
              break;
            case 17:
              m_dicUnit[ToolUnit.NumberOfCycles] = unit;
              break;
          }
        }
      }
    }

    void LoadInitialData ()
    {
      // Find all tool life event configs related to the current machine
      m_toolLifeEventConfigs.Clear ();
      using (IDAOSession session = ModelDAOHelper.DAOFactory.OpenSession ()) {
        var machineModule = ModelDAOHelper.DAOFactory.MachineModuleDAO
          .FindByIdWithMonitoredMachine (m_machineModuleId);
        IList<IEventToolLifeConfig> listTmp = ModelDAOHelper.DAOFactory.EventToolLifeConfigDAO.FindAllForConfig ();
        foreach (var item in listTmp) {
          SetActive ();
          if (item.MachineFilter == null || item.MachineFilter.IsMatch (machineModule.MonitoredMachine)) {
            if (!m_toolLifeEventConfigs.ContainsKey (item.Type)) {
              m_toolLifeEventConfigs[item.Type] = new List<IEventToolLifeConfig> ();
            }

            if (item.MachineObservationState == null) {
              m_toolLifeEventConfigs[item.Type].Insert (0, item);
            }
            else {
              m_toolLifeEventConfigs[item.Type].Add (item);
            }
          }
        }
      }

      // Update current MOS
      using (IDAOSession session = ModelDAOHelper.DAOFactory.OpenSession ()) {
        var machineModule = ModelDAOHelper.DAOFactory.MachineModuleDAO
          .FindByIdWithMonitoredMachine (m_machineModuleId);
        var oss = ModelDAOHelper.DAOFactory.ObservationStateSlotDAO.FindAt (
          machineModule.MonitoredMachine, DateTime.UtcNow);
        if (oss != null) {
          m_currentMachineObservationState = oss.MachineObservationState;
        }
        else {
          log.WarnFormat ("UpdateCurrentMOS: not defined for machine module {0}", machineModule);
        }
      }
    }

    /// <summary>
    /// Finish method
    /// </summary>
    public void Finish ()
    {
      m_antiDuplicate.Finish ();
    }

    /// <summary>
    /// Process ToolLifeData received
    /// => store the new state in the Database
    /// => compare with the previous state and possible stores EventToolLife
    /// </summary>
    /// <param name="data"></param>
    public void ProcessData (ToolLifeData data)
    {
      if (data == null) {
        log.Info ("ProcessData: cannot process null data");
        return;
      }
      SetActive ();
      log.InfoFormat ("ProcessData: about to process {0} tool positions", data.ToolNumber);

      LoadInitialData ();
      m_newEvents.Clear ();
      data = data.Clone ();
      data.Filter ();

      // Previous state usable for a comparison?
      m_previousStateOk = false;
      using (IDAOSession session = ModelDAOHelper.DAOFactory.OpenSession ()) {
        using (IDAOTransaction transaction = session.BeginReadOnlyTransaction ("PreviousStateToolDataAcquisition")) {
          var machineModule = ModelDAOHelper.DAOFactory.MachineModuleDAO
            .FindByIdWithMonitoredMachine (m_machineModuleId);
          var acquisitionState = ModelDAOHelper.DAOFactory.AcquisitionStateDAO.GetAcquisitionState (machineModule, AcquisitionStateKey.Tools);
          if (acquisitionState == null) {
            log.InfoFormat ("ProcessData: no existing data for machine module {0} in the database => no comparison", m_machineModuleId);
            m_previousStateOk = false;
          }
          else {
            m_previousDateTime = acquisitionState.DateTime;
            if (m_currentDateTime.Subtract (m_previousDateTime) > m_obsoleteDuration) {
              log.InfoFormat ("ProcessData: existing data for machine module {0} in the database is obsolete => no comparison", m_machineModuleId);
              m_previousStateOk = false;
            }
            else {
              log.Info ("ProcessData: ok for comparing based on the previous values");
              m_previousStateOk = true;
            }
          }
        }
      }

      // Delete all existing tools if obsolete or tools that don't match
      try {
        if (!m_previousStateOk) {
          using (IDAOSession session = ModelDAOHelper.DAOFactory.OpenSession ()) {
            using (IDAOTransaction transaction = session.BeginTransaction ("DeleteAllToolData")) {
              // Delete everything
              var machineModule = ModelDAOHelper.DAOFactory.MachineModuleDAO
                .FindByIdWithMonitoredMachine (m_machineModuleId);
              var toolLifes = ModelDAOHelper.DAOFactory.ToolLifeDAO.FindAllByMachineModule (machineModule);
              foreach (var toolLife in toolLifes) {
                ModelDAOHelper.DAOFactory.ToolLifeDAO.MakeTransient (toolLife);
              }

              var toolPositions = ModelDAOHelper.DAOFactory.ToolPositionDAO.FindByMachineModule (machineModule);
              foreach (var toolPosition in toolPositions) {
                ModelDAOHelper.DAOFactory.ToolPositionDAO.MakeTransient (toolPosition);
              }

              transaction.Commit ();
            }
          }
        }
        else {
          // Delete tools that don't match the new ones, if KeepRemovedTools is not on
          using (IDAOSession session = ModelDAOHelper.DAOFactory.OpenSession ()) {
            using (IDAOTransaction transaction = session.BeginTransaction ("DeleteSomeToolData")) {
              var machineModule = ModelDAOHelper.DAOFactory.MachineModuleDAO
                .FindByIdWithMonitoredMachine (m_machineModuleId);
              IList<IToolPosition> positions = ModelDAOHelper.DAOFactory.ToolPositionDAO.FindByMachineModule (machineModule);
              foreach (var position in positions) {
                SetActive ();
                bool found = false;
                for (int i = 0; i < data.ToolNumber; i++) {
                  if (string.Equals (position.ToolId, data[i].ToolId, StringComparison.CurrentCultureIgnoreCase)) {
                    found = true;
                    break;
                  }
                }
                if (!found) {
                  // A tool position is removed
                  ProcessRemovedTool (machineModule, position);
                }
              }
              transaction.Commit ();
            }
          }
        }
      }
      catch (Exception ex) {
        log.Error ("ProcessData: couldn't delete tools", ex);
      }

      using (IDAOSession session = ModelDAOHelper.DAOFactory.OpenSession ()) {
        using (IDAOTransaction transaction = session.BeginTransaction ("ProcessData")) {
          var machineModule = ModelDAOHelper.DAOFactory.MachineModuleDAO
            .FindByIdWithMonitoredMachine (m_machineModuleId);
          // For each tool in the database
          IList<IToolPosition> positions = ModelDAOHelper.DAOFactory.ToolPositionDAO.FindByMachineModule (machineModule);
          foreach (var position in positions) {
            // Find the index of the corresponding incoming tool
            for (int i = 0; i < data.ToolNumber; i++) {
              SetActive ();
              if (string.Equals (position.ToolId, data[i].ToolId, StringComparison.CurrentCultureIgnoreCase)) {
                // A tool position is updated
                ProcessUpdatedTool (machineModule, position, data[i]);
                data.RemoveTool (i);
                break;
              }
            }
          }

          // Remaining tools are added
          for (int i = 0; i < data.ToolNumber; i++) {
            SetActive ();
            ProcessNewTool (machineModule, data[i]);
          }

          // Update the date time of the acquisition
          var acquisitionState = ModelDAOHelper.DAOFactory.AcquisitionStateDAO.GetAcquisitionState (machineModule, AcquisitionStateKey.Tools) ??
            ModelDAOHelper.ModelFactory.CreateAcquisitionState (machineModule, AcquisitionStateKey.Tools);
          acquisitionState.DateTime = m_currentDateTime;
          ModelDAOHelper.DAOFactory.AcquisitionStateDAO.MakePersistent (acquisitionState);

          transaction.Commit ();
        }
      }

      // Commit new events
      if (m_newEvents.Count > 0) {
        log.InfoFormat ("CommitNewEvents: about to commit {0} new tool life event(s)", m_newEvents.Count);
        using (IDAOSession session = ModelDAOHelper.DAOFactory.OpenSession ()) {
          using (IDAOTransaction transaction = session.BeginTransaction ("CreateNewToolEvent")) {
            foreach (var newEvent in m_newEvents) {
              SetActive ();
              ModelDAOHelper.DAOFactory.EventToolLifeDAO.MakePersistent (newEvent);
            }
            transaction.Commit ();
          }
        }
      }
    }

    void ProcessNewTool (IMachineModule machineModule, ToolLifeData.ToolLifeDataItem dataItem)
    {
      // Create a new position
      var position = ModelDAOHelper.ModelFactory.CreateToolPosition (machineModule, dataItem.ToolId);
      position.ToolNumber = dataItem.ToolNumber;
      position.Pot = dataItem.PotNumber;
      position.Magazine = dataItem.MagazineNumber;
      position.ToolState = dataItem.ToolState;
      position.LeftDateTime = null;
      foreach (var key in dataItem.Properties.Keys) {
        if (key == "GeometryUnit") {
          position.SetProperty (key, m_dicUnit[(ToolUnit)dataItem.Properties[key]].Id);
        }
        else {
          position.SetProperty (key, dataItem.Properties[key]);
        }
      }
      ModelDAOHelper.DAOFactory.ToolPositionDAO.MakePersistent (position);

      // Add tool life description
      for (int i = 0; i < dataItem.LifeDescriptionNumber; i++) {
        SetActive ();
        ToolLifeData.ToolLifeDataItem.LifeDescription ld = dataItem[i];
        IToolLife tl = ModelDAOHelper.ModelFactory.CreateToolLife (
          machineModule, position,
          m_dicUnit[ld.LifeType],
          ld.LifeDirection);
        tl.Value = ld.LifeValue;
        tl.Limit = ld.LifeLimit;

        // Warnings are converted into an absolute value
        if (ld.LifeWarningOffset != null && ld.LifeWarningOffset.HasValue) {
          if (ld.LifeDirection == ToolLifeDirection.Down) {
            tl.Warning = ld.LifeWarningOffset;
          }
          else if (ld.LifeDirection == ToolLifeDirection.Up && ld.LifeLimit != null && ld.LifeLimit.HasValue) {
            tl.Warning = ld.LifeLimit.Value - ld.LifeWarningOffset.Value;
          }
        }

        ModelDAOHelper.DAOFactory.ToolLifeDAO.MakePersistent (tl);
      }

      // Last change of the tool life
      if (dataItem.LifeDescriptionNumber > 0) {
        position.LifeChangedDateTime = m_currentDateTime;
      }

      /**********
       * EVENTS *
       *********/

      if (m_previousStateOk) {
        // New event: tool registration
        CreateNewToolEvent (machineModule, EventToolLifeType.ToolRegistration,
                           "A new tool has been registered.",
                           null, null,
                           dataItem,
                           dataItem.LifeDescriptionNumber > 0 ? dataItem[0] : null);
      }
    }

    void ProcessRemovedTool (IMachineModule machineModule, IToolPosition position)
    {
      if (KeepRemovedTools) {
        // Add a left time if needed
        if (position.LeftDateTime == null) {
          position.LeftDateTime = m_currentDateTime;
          ModelDAOHelper.DAOFactory.ToolPositionDAO.MakePersistent (position);
        }
      }
      else {
        if (m_previousStateOk) {
          // New event: tool removal
          CreateNewToolEvent (machineModule, EventToolLifeType.ToolRemoval,
                             "An old tool has been removed.",
                             position, null, null, null);
        }

        // Remove a tool (and all its corresponding tool lifes)
        var toolLifes = ModelDAOHelper.DAOFactory.ToolLifeDAO.FindAllByMachinePosition (machineModule, position);
        foreach (var toolLife in toolLifes) {
          SetActive ();
          ModelDAOHelper.DAOFactory.ToolLifeDAO.MakeTransient (toolLife);
        }
        ModelDAOHelper.DAOFactory.ToolPositionDAO.MakeTransient (position);
      }
    }

    void ProcessUpdatedTool (IMachineModule machineModule, IToolPosition position, ToolLifeData.ToolLifeDataItem dataItem)
    {
      // For each tool life in the database
      IList<IToolLife> lifes = ModelDAOHelper.DAOFactory.ToolLifeDAO.FindAllByMachinePosition (machineModule, position);
      foreach (var life in lifes) {
        SetActive ();
        // Find the index of the corresponding incoming tool life
        int index = -1;
        for (int i = 0; i < dataItem.LifeDescriptionNumber; i++) {
          var ld = dataItem[i];
          if (((life.Unit == null) ? (int)UnitId.Unknown : life.Unit.Id) == m_dicUnit[ld.LifeType].Id &&
              life.Direction == ld.LifeDirection) {
            index = i;
            break;
          }
        }

        if (index != -1) {
          // A tool life is updated
          ProcessUpdatedLife (machineModule, position, life, dataItem, dataItem[index]);
          dataItem.RemoveLifeDescription (index);
        }
        else {
          // A tool life is removed
          ProcessRemovedLife (life);

          // Last change of the tool life
          position.LifeChangedDateTime = m_currentDateTime;
        }
      }

      // New tool life
      for (int i = 0; i < dataItem.LifeDescriptionNumber; i++) {
        SetActive ();
        ProcessNewLife (machineModule, position, dataItem[i]);

        // Last change of the tool life
        position.LifeChangedDateTime = m_currentDateTime;
      }

      /**********
       * EVENTS *
       *********/

      if (m_previousStateOk) {
        // Tool moved?
        if (!AreEqual (position.Magazine, dataItem.MagazineNumber) ||
            !AreEqual (position.Pot, dataItem.PotNumber)) {
          CreateNewToolEvent (machineModule, EventToolLifeType.ToolMoved,
                             string.Format ("Tool moved from mag. {0} pot {1} to mag. {2} pot {3}.",
                                           position.Magazine, position.Pot,
                                           dataItem.MagazineNumber, dataItem.PotNumber),
                             position, null,
                             dataItem, null);
        }

        // Status change => available?
        if (!position.ToolState.IsAvailable () &&
            dataItem.ToolState.IsAvailable ()) {
          CreateNewToolEvent (machineModule, EventToolLifeType.StatusChangeToAvailable,
                             string.Format ("Status is now available: {0} => {1}",
                                           position.ToolState.Name (true),
                                           dataItem.ToolState.Name (true)),
                             position, null,
                             dataItem, null);
        }

        // Status change => temporary unavailable?
        if (!position.ToolState.IsTemporaryUnavailable () &&
            dataItem.ToolState.IsTemporaryUnavailable ()) {
          CreateNewToolEvent (machineModule, EventToolLifeType.StatusChangeToTemporaryUnavailable,
                             string.Format ("Status is now temporary unavailable: {0} => {1}",
                                           position.ToolState.Name (true),
                                           dataItem.ToolState.Name (true)),
                             position, null,
                             dataItem, null);
        }

        // Status change => definitely unavailable?
        if (!position.ToolState.IsDefinitelyUnavailable () &&
            dataItem.ToolState.IsDefinitelyUnavailable ()) {
          CreateNewToolEvent (machineModule, EventToolLifeType.StatusChangeToDefinitelyUnavailable,
                             string.Format ("Status is now definitely unavailable: {0} => {1}",
                                           position.ToolState.Name (true),
                                           dataItem.ToolState.Name (true)),
                             position, null,
                             dataItem, null);
        }
      }

      // Update the position
      position.ToolNumber = dataItem.ToolNumber;
      position.Pot = dataItem.PotNumber;
      position.Magazine = dataItem.MagazineNumber;
      position.ToolState = dataItem.ToolState;
      position.LeftDateTime = null;
      position.Properties.Clear ();
      foreach (var key in dataItem.Properties.Keys) {
        if (key == "GeometryUnit") {
          position.SetProperty (key, m_dicUnit[(ToolUnit)dataItem.Properties[key]].Id);
        }
        else {
          position.SetProperty (key, dataItem.Properties[key]);
        }
      }
      ModelDAOHelper.DAOFactory.ToolPositionDAO.MakePersistent (position);
    }

    void ProcessNewLife (IMachineModule machineModule, IToolPosition position, ToolLifeData.ToolLifeDataItem.LifeDescription ld)
    {
      IToolLife life = ModelDAOHelper.ModelFactory.CreateToolLife (
        machineModule, position,
        m_dicUnit[ld.LifeType],
        ld.LifeDirection);

      // We update the ToolLife
      life.Value = ld.LifeValue;
      life.Limit = ld.LifeLimit;

      // Warning are converted into an absolute value
      if (ld.LifeWarningOffset.HasValue) {
        if (ld.LifeDirection == ToolLifeDirection.Down) {
          life.Warning = ld.LifeWarningOffset;
        }
        else if (ld.LifeDirection == ToolLifeDirection.Up && ld.LifeLimit != null && ld.LifeLimit.HasValue) {
          if (ld.LifeLimit.Value > ld.LifeWarningOffset.Value) {
            life.Warning = ld.LifeLimit.Value - ld.LifeWarningOffset.Value;
          }
          else {
            life.Warning = null;
          }
        }
      }
      else {
        life.Warning = null;
      }

      ModelDAOHelper.DAOFactory.ToolLifeDAO.MakePersistent (life);
    }

    void ProcessRemovedLife (IToolLife life)
    {
      ModelDAOHelper.DAOFactory.ToolLifeDAO.MakeTransient (life);
    }

    void ProcessUpdatedLife (IMachineModule machineModule, IToolPosition position,
                            IToolLife life,
                            ToolLifeData.ToolLifeDataItem dataItem,
                            ToolLifeData.ToolLifeDataItem.LifeDescription ld)
    {
      if (m_previousStateOk) {
        // Total life changed?
        if (ld.LifeLimit != null && life.Limit != null &&
            ld.LifeLimit.HasValue && life.Limit.HasValue) {
          if (ld.LifeLimit.Value + 0.1 < life.Limit.Value) {
            CreateNewToolEvent (machineModule, EventToolLifeType.TotalLifeDecreased,
                               "Total life decreased.",
                               position, life,
                               dataItem, ld);
          }
          else if (ld.LifeLimit.Value - 0.1 > life.Limit.Value) {
            CreateNewToolEvent (machineModule, EventToolLifeType.TotalLifeIncreased,
                               "Total life increased.",
                               position, life,
                               dataItem, ld);
          }
        }

        // Warning changed?
        if (ld.LifeWarningOffset != null && ld.LifeLimit != null && life.Warning != null &&
            ld.LifeWarningOffset.HasValue && ld.LifeLimit.HasValue && life.Warning.HasValue) {
          double oldAbsolute = (ld.LifeDirection == ToolLifeDirection.Up ?
                                ld.LifeLimit.Value - ld.LifeWarningOffset.Value :
                                ld.LifeWarningOffset.Value);

          if (Math.Abs (oldAbsolute - life.Warning.Value) > 0.1) {
            CreateNewToolEvent (machineModule, EventToolLifeType.WarningChanged,
                               "Warning changed.",
                               position, life,
                               dataItem, ld);
          }
        }

        // Current life decreased?
        if (ld.LifeDirection == ToolLifeDirection.Up &&
            ld.LifeValue < life.Value) {

          // Rules to detect a reset
          double oldTrigger = life.Limit.HasValue ?
            life.Limit.Value * (100.0 - m_percentForResetValue) / 100.0 : 0;
          double newTrigger = m_offsetForResetValue;

          if ((life.Value > oldTrigger || m_percentForResetValue <= 0) && (ld.LifeValue < newTrigger || m_offsetForResetValue <= 0)) {
            CreateNewToolEvent (machineModule, EventToolLifeType.CurrentLifeReset,
                               "Current life reset.",
                               position, life,
                               dataItem, ld);
          }
          else {
            CreateNewToolEvent (machineModule, EventToolLifeType.CurrentLifeDecreased,
                               "Current life decreased.",
                               position, life,
                               dataItem, ld);
          }
        }

        // Rest life increased?
        if (ld.LifeDirection == ToolLifeDirection.Down &&
            ld.LifeValue > life.Value) {

          // Rules to detect a reset
          double oldTrigger = life.Limit.HasValue ?
            life.Limit.Value * m_percentForResetValue / 100.0 :
            life.Value + 1;
          double newTrigger = ld.LifeLimit.HasValue ?
            ld.LifeLimit.Value - m_offsetForResetValue :
            ld.LifeValue;

          if ((life.Value < oldTrigger || m_percentForResetValue <= 0) && (ld.LifeValue > newTrigger || m_offsetForResetValue <= 0)) {
            CreateNewToolEvent (machineModule, EventToolLifeType.RestLifeReset,
                               "Rest life reset.",
                               position, life,
                               dataItem, ld);
          }
          else {
            CreateNewToolEvent (machineModule, EventToolLifeType.RestLifeIncreased,
                               "Rest life increased.",
                               position, life,
                               dataItem, ld);
          }
        }

        // Expiration reached?
        bool expirationReached = false;
        if (ld.LifeDirection == ToolLifeDirection.Up &&
            ld.LifeLimit != null && life.Limit != null &&
            ld.LifeLimit.HasValue && life.Limit.HasValue) {
          // Current life is increasing, a limit has been set
          expirationReached = life.Value < life.Limit.Value &&
            ld.LifeValue >= ld.LifeLimit.Value;
        }
        else if (ld.LifeDirection == ToolLifeDirection.Down) {
          // Current life is decreasing, the limit is not important
          expirationReached = life.Value > 0 &&
            ld.LifeValue <= 0;
        }
        if (expirationReached) {
          CreateNewToolEvent (machineModule, EventToolLifeType.ExpirationReached,
                             "The tool has expired.",
                             position, life,
                             dataItem, ld);
        }
        else if (ld.LifeWarningOffset != null && life.Warning != null &&
                 ld.LifeWarningOffset.HasValue && life.Warning.HasValue) {
          // Warning reached?
          bool warningReached = false;
          if (ld.LifeDirection == ToolLifeDirection.Up &&
              ld.LifeLimit != null && life.Limit != null &&
              ld.LifeLimit.HasValue && life.Limit.HasValue) {
            // Current life is increasing, a limit has been set
            warningReached = life.Value < life.Warning.Value &&
              ld.LifeValue >=
              ld.LifeLimit.Value - ld.LifeWarningOffset.Value;
          }
          else if (ld.LifeDirection == ToolLifeDirection.Down) {
            // Current life is decreasing, the limit is not important
            warningReached = life.Value > life.Warning.Value &&
              ld.LifeValue <= ld.LifeWarningOffset.Value;
          }
          if (warningReached) {
            CreateNewToolEvent (machineModule, EventToolLifeType.WarningReached,
                               "Warning reached: the tool will expire soon.",
                               position, life,
                               dataItem, ld);
          }
        }
      }

      // Last change of the tool life
      if (life.Value != ld.LifeValue) {
        position.LifeChangedDateTime = m_currentDateTime;
      }

      // We update the ToolLife
      life.Value = ld.LifeValue;
      life.Limit = ld.LifeLimit;

      // Warning are converted into an absolute value
      if (ld.LifeWarningOffset.HasValue) {
        if (ld.LifeDirection == ToolLifeDirection.Down) {
          life.Warning = ld.LifeWarningOffset;
        }
        else if (ld.LifeDirection == ToolLifeDirection.Up && ld.LifeLimit != null && ld.LifeLimit.HasValue) {
          if (ld.LifeLimit.Value > ld.LifeWarningOffset.Value) {
            life.Warning = ld.LifeLimit.Value - ld.LifeWarningOffset.Value;
          }
          else {
            life.Warning = null;
          }
        }
      }
      else {
        life.Warning = null;
      }

      ModelDAOHelper.DAOFactory.ToolLifeDAO.MakePersistent (life);
    }

    void CreateNewToolEvent (IMachineModule machineModule, EventToolLifeType type,
                            string message,
                            IToolPosition position,
                            IToolLife life,
                            ToolLifeData.ToolLifeDataItem dataItem,
                            ToolLifeData.ToolLifeDataItem.LifeDescription lifeDescription)
    {
      string toolId = (position != null ? position.ToolId : (dataItem != null ? dataItem.ToolId : "")) ?? "";
      if (!m_antiDuplicate.IsAllowed (toolId, type)) {
        log.WarnFormat ("ToolLife.CreateNewToolEvent: storing event of type {0} for tool {1} is not allowed", type, toolId);
        return;
      }

      // Find a configuration corresponding to the event tool life type
      IEventToolLifeConfig config = null;
      if (m_toolLifeEventConfigs.ContainsKey (type)) {
        foreach (var item in m_toolLifeEventConfigs[type]) {
          if (item.MachineObservationState == null || m_currentMachineObservationState == null ||
              m_currentMachineObservationState.Id == item.MachineObservationState.Id) {
            config = item;
          }
        }
      }

      if (config != null) {
        log.InfoFormat ("CreateNewToolEvent: new event processed '{0}' " +
                       "for {1}, machine module {2}", message, dataItem, machineModule.Id);

        // Creation of the event
        IEventToolLife etl = ModelDAOHelper.ModelFactory.CreateEventToolLife (
          config.Level, type, m_currentDateTime, machineModule);

        // Fill information in the event
        PrepareEventToolLife (ref etl, config, message,
                             position, life,
                             dataItem, lifeDescription);

        // Store in a list and commit later
        m_newEvents.Add (etl);
      }
      else {
        log.InfoFormat ("CreateNewToolEvent: new event NOT stored '{0}' " +
                       "for {1}, machine module {2}", message, dataItem, machineModule.Id);
      }
    }

    void PrepareEventToolLife (ref IEventToolLife etl,
                              IEventToolLifeConfig config, // Not null!
                              string message,
                              IToolPosition position,
                              IToolLife life,
                              ToolLifeData.ToolLifeDataItem dataItem,
                              ToolLifeData.ToolLifeDataItem.LifeDescription lifeDescription)
    {
      etl.Config = config;
      etl.Message = message;
      etl.MachineObservationState = m_currentMachineObservationState;
      etl.ElapsedTime = (int)m_currentDateTime.Subtract (m_previousDateTime).TotalMilliseconds;
      etl.ToolId = "";

      if (position != null) {
        etl.OldMagazine = position.Magazine;
        etl.OldPot = position.Pot;
        etl.ToolNumber = position.ToolNumber;
        etl.ToolId = position.ToolId;
        etl.OldToolState = position.ToolState;
      }

      if (life != null) {
        etl.Direction = life.Direction;

        etl.Unit = life.Unit;
        etl.OldValue = life.Value;
        etl.OldLimit = life.Limit;

        // Warning
        etl.OldWarning = life.Warning;
      }

      if (dataItem != null) {
        etl.NewMagazine = dataItem.MagazineNumber;
        etl.NewPot = dataItem.PotNumber;
        etl.ToolNumber = dataItem.ToolNumber;
        etl.ToolId = dataItem.ToolId;
        etl.NewToolState = dataItem.ToolState;
      }

      if (lifeDescription != null) {
        etl.Direction = lifeDescription.LifeDirection; // should be the same than old
        if (etl.Unit == null) {
          etl.Unit = m_dicUnit[lifeDescription.LifeType]; // should be the same than old
        }

        etl.NewValue = lifeDescription.LifeValue;
        etl.NewWarning = lifeDescription.LifeWarningOffset;
        etl.NewLimit = lifeDescription.LifeLimit;

        // Warning
        if (lifeDescription.LifeWarningOffset != null &&
            lifeDescription.LifeWarningOffset.HasValue) {
          if (etl.Direction == ToolLifeDirection.Up) {
            if (etl.NewLimit != null && etl.NewLimit.HasValue) {
              etl.NewWarning = etl.NewLimit.Value - lifeDescription.LifeWarningOffset.Value;
            }
          }
          else if (etl.Direction == ToolLifeDirection.Down) {
            etl.NewWarning = lifeDescription.LifeWarningOffset.Value;
          }
        }
      }
    }

    static bool AreEqual (int? valA, int? valB)
    {
      bool ok = true;

      // Null
      if (valA != null || valB != null) {
        if (valA == null || valB == null) {
          ok = false; // mismatch
        }
        else {
          // Both are not null, check if a value is set
          if (valA.HasValue || valB.HasValue) {
            if (!valA.HasValue || !valB.HasValue) {
              ok = false; // mismatch
            }
            else {
              ok = (valA.Value == valB.Value);
            }
          }
        }
      }

      return ok;
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
