// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

using Lemoine.Cnc.Data;
using Lemoine.Info.ConfigReader;
using Lemoine.Model;
using Lemoine.Threading;
using Lemoine.Core.Log;
using Lemoine.Cnc.DataQueue;
using System.Linq;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Output CncModule to insert the data into SQLiteCncQueue
  /// </summary>
  public sealed class CncDataQueueOutput : Lemoine.Cnc.BaseCncModule, Lemoine.Cnc.ICncModule, IDisposable
  {
    static readonly int END_OFFSET = 10000000;
    static readonly int MAX_QUANTITY = 9;

    #region Members
    int m_machineId = 0;
    int m_machineModuleId = 0;
    string m_operationCycleBeginKey = null;
    string m_operationCycleEndKey = null;

    internal ICncDataQueue m_queue = null; // internal = access for DataQueue.UnitTests
    VariableChangeTracker m_stampVariableChangeTracker = new VariableChangeTracker ("Stamp");
    VariableChangeTracker m_variableChangeTracker = new VariableChangeTracker ("Variable");
    DateTime m_detectionTimeStamp = DateTime.MinValue;
    int m_operationCycleVariable = int.MinValue;
    int m_startEndVariable = int.MinValue;
    DetectionMethod m_detectionMethod = DetectionMethod.Default;
    ISet<string> m_stoppedCncValues = new HashSet<string> ();

    // Members to skip machine modes that occur only once
    ExchangeData m_previousMachineMode = null;
    string m_currentMachineModeKey = null;
    object m_currentMachineModeValue = null;
    Queue<ExchangeData> m_pausedQueue = new Queue<ExchangeData> ();
    bool m_paused = false;
    #endregion // Members

    #region Getters / Setters
    /// <summary>
    /// Machine Id
    /// </summary>
    public int MachineId
    {
      get { return m_machineId; }
      set {
        m_machineId = value;
        m_stampVariableChangeTracker.MachineId = value;
        m_variableChangeTracker.MachineId = value;
        UpdateLogger ();
      }
    }

    /// <summary>
    /// Machine Module Id
    /// </summary>
    public int MachineModuleId
    {
      get { return m_machineModuleId; }
      set {
        m_machineModuleId = value;
        m_stampVariableChangeTracker.MachineModuleId = value;
        m_variableChangeTracker.MachineModuleId = value;
        UpdateLogger ();
      }
    }

    /// <summary>
    /// Queue configuration
    /// </summary>
    public string QueueConfiguration { get; set; }

    /// <summary>
    /// Detection method
    /// </summary>
    public int DetectionMethodValue
    {
      get { return (int)m_detectionMethod; }
      set { m_detectionMethod = (Lemoine.Model.DetectionMethod)value; }
    }

    /// <summary>
    /// Begin cycle key
    /// </summary>
    public string OperationCycleBeginKey
    {
      get { return m_operationCycleBeginKey; }
      set { m_operationCycleBeginKey = value; }
    }

    /// <summary>
    /// End cycle key
    /// </summary>
    public string OperationCycleEndKey
    {
      get { return m_operationCycleEndKey; }
      set { m_operationCycleEndKey = value; }
    }

    /// <summary>
    /// Number of objects in the queue
    /// </summary>
    public int Count
    {
      get { return m_queue.Count; }
    }

    /// <summary>
    /// Machine Mode Id
    /// </summary>
    public int MachineModeId
    {
      set {
        log.DebugFormat ("MachineModeId.set: " +
                         "machine mode id to {0}",
                         value);
        ExchangeData data =
          ExchangeData.BuildMachineModeIDExchangeData (m_machineId,
                                                      m_machineModuleId,
                                                      DateTime.UtcNow,
                                                      value);
        AddMachineModeData (data);
      }
    }

    /// <summary>
    /// Machine Mode Translation Key Or Name
    /// </summary>
    public string MachineModeTranslationKeyOrName
    {
      set {
        Debug.Assert (null != value);
        log.DebugFormat ("MachineModeTranslationKeyOrName.set: " +
                         "machine mode translation key or name to {0}",
                         value);

        ExchangeData data =
          ExchangeData.BuildMachineModeTranslationKeyOrNameExchangeData (m_machineId,
                                                                        m_machineModuleId,
                                                                        DateTime.UtcNow,
                                                                        value);
        AddMachineModeData (data);
      }
    }

    void AddMachineModeData (ExchangeData data)
    {
      if ((null == m_previousMachineMode)
          || (m_previousMachineMode.Value.Equals (data.Value) && m_previousMachineMode.Key.Equals (data.Key))) {
        // First machine mode or same machine mode at least two consecutive times,
        // - empty the paused queue...
        log.DebugFormat ("AddMachineModeData: " +
                         "first or same machine mode {0}, empty the paused buffer first of size {1}",
                         data.Value,
                         m_pausedQueue.Count);
        while (0 < m_pausedQueue.Count) {
          SetActive ();
          m_queue.Enqueue (m_pausedQueue.Dequeue ());
        }
        // - consider this machine mode
        log.DebugFormat ("AddMachineModeData: " +
                         "current machine mode is {0}",
                         data.Value);
        m_currentMachineModeValue = data.Value;
        m_currentMachineModeKey = data.Key;
        m_paused = false;
      }
      else if (object.Equals (m_currentMachineModeValue, data.Value)
               && object.Equals (m_currentMachineModeKey, data.Key)) {
        // Discard previousMachineMode, because it occurred only once
        log.DebugFormat ("AddMachineModeData: " +
                         "discard previous machine mode {0} " +
                         "because it occurred only once",
                         m_previousMachineMode);
        EmptyPausedQueueDiscardingMachineMode ();
        m_paused = false;
      }
      else { // New machine mode, and first time it happens, add it to the paused queue after emptying it
        // - empty the queue first discarding the previous machine modes
        EmptyPausedQueueDiscardingMachineMode ();
        // - make a pause on data acquisition that depend on machine mode
        log.DebugFormat ("AddMachineModeData: " +
                         "new machine mode {0}, add it to a paused buffer first",
                         data.Value);
        m_paused = true;
      }
      AddData (data);
      m_previousMachineMode = data;
    }

    void EmptyPausedQueueDiscardingMachineMode ()
    {
      while (0 < m_pausedQueue.Count) {
        SetActive ();
        // Insert the data in queue that are not MachineMode and StopCncValue
        ExchangeData pausedData = m_pausedQueue.Dequeue ();
        if (!pausedData.Command.Equals (ExchangeDataCommand.MachineMode)
            && !pausedData.Command.Equals (ExchangeDataCommand.StopCncValue)) {
          m_queue.Enqueue (pausedData);
        }
        else {
          log.InfoFormat ("EmptyPausedQueueDiscardingMachineMode: " +
                          "discard {0}",
                          pausedData, pausedData.Value);
        }
      }
    }

    /// <summary>
    /// Add a data considering the pause parameter
    /// </summary>
    /// <param name="data"></param>
    void AddData (ExchangeData data)
    {
      if (m_paused) {
        log.DebugFormat ("AddData: " +
                         "enqueue in the buffer {0} (paused)",
                         data);
        m_pausedQueue.Enqueue (data);
      }
      else {
        log.DebugFormat ("AddData: " +
                         "enqueue in the file fifo {0}",
                         data);
        m_queue.Enqueue (data);
      }
    }

    /// <summary>
    /// Machine Module Activity Machine Mode Id
    /// </summary>
    public int MachineModuleActivityModeId
    {
      set {
        log.DebugFormat ("MachineModuleActivityModeId.set: " +
                         "machine mode id to {0}",
                         value);
        ExchangeData data =
          ExchangeData.BuildMachineModuleActivityIDExchangeData (m_machineId,
                                                                 m_machineModuleId,
                                                                 DateTime.UtcNow,
                                                                 value);
        AddData (data);
      }
    }

    /// <summary>
    /// Machine Module Activity Mode Translation Key Or Name
    /// </summary>
    public string MachineModuleActivityModeTranslationKeyOrName
    {
      set {
        Debug.Assert (null != value);
        log.DebugFormat ("MachineModuleActivityModeTranslationKeyOrName.set: " +
                         "machine mode translation key or name to {0}",
                         value);

        ExchangeData data =
          ExchangeData.BuildMachineModuleActivityTranslationKeyOrNameExchangeData (m_machineId,
                                                                                   m_machineModuleId,
                                                                                   DateTime.UtcNow,
                                                                                   value);
        AddData (data);
      }
    }

    /// <summary>
    /// Process the operation cycle begin/end events
    /// and insert the corresponding data in the Fifo
    /// </summary>
    public System.Collections.Queue OperationCycleEvents
    {
      set {
        if (0 == value.Count) {
          AddDetectionTimeStamp ();
        }
        while (value.Count > 0) {
          // Note on thread safety:
          // Public members are thread safe
          // Because no other thread is dequeuing the items in the same time,
          // there is no need to put a lock here
          string eventItem = (string)value.Peek ();
          if (eventItem.Equals (this.m_operationCycleBeginKey)) {
            StartCycle ();
          }
          else if (eventItem.Equals (this.m_operationCycleEndKey)) {
            StopCycle ();
          }
          // Remove the item only once it was processed
          value.Dequeue ();
        }
      }
    }

    /// <summary>
    /// Set an operation cycle variable
    /// and if it changed, create the corresponding
    /// OperationCycleInformation data
    /// </summary>
    public int OperationCycleVariable
    {
      set {
        if (value != m_operationCycleVariable) {
          log.DebugFormat ("OperationCycleVariable.set: " +
                           "operation cycle variable from {0} to  {1}",
                           m_operationCycleVariable, value);

          if (value.ToString ().Equals (this.m_operationCycleBeginKey)) {
            StartCycle ();
          }
          else if (value.ToString ().Equals (this.m_operationCycleEndKey)) {
            StopCycle ();
          }

          m_operationCycleVariable = value;
        }
        else {
          AddDetectionTimeStamp ();
        }
      }
    }

    /// <summary>
    /// Set a start/end operation cycle variable
    /// and if it changed, create the corresponding
    /// OperationCycleInformation data
    /// 
    /// This is only a temporary solution before migrating
    /// all ISO files to the OperationCycleVariable
    /// 
    /// This variable corresponds to an operation cycle begin
    /// if less than END_OFFSET (10 000 000),
    /// else to an operation cycle end
    /// </summary>
    public int StartEndVariable
    {
      set {
        if (value != m_startEndVariable) {
          log.DebugFormat ("StartEndVariable.set: " +
                           "start/end variable from {0} to  {1}",
                           m_startEndVariable, value);

          if (value < END_OFFSET) {
            StartCycle ();
          }
          else { // >= END_OFFSET
            StopCycle ();
          }

          m_startEndVariable = value;
        }
        else {
          AddDetectionTimeStamp ();
        }
      }
    }
    #endregion // Getters / Setters

    #region Constructors / Destructor / ToString methods
    /// <summary>
    /// Description of the constructor
    /// </summary>
    public CncDataQueueOutput ()
      : base ("Lemoine.Cnc.Out.CncDataQueueOutput")
    {
      this.QueueConfiguration = "";
    }

    /// <summary>
    /// <see cref="IDisposable.Dispose" />
    /// </summary>
    public void Dispose ()
    {
      if (null != m_queue) {
        m_queue.Dispose ();
        m_queue = null;
      }

      GC.SuppressFinalize (this);
    }
    #endregion // Constructors / Destructor / ToString methods

    #region Methods
    /// <summary>
    /// Start method
    /// </summary>
    public bool Start ()
    {
      if (null == m_queue) {
        // Configuration of the queue
        var defaultConfigReader = new MemoryConfigReader ();
        defaultConfigReader.Add ("ReceiveOnly", false);

        try {
          var queueConfigurations = this.QueueConfiguration.Split (new char[] { ':' }, 2);
          var remoteQueueConfigurationPath = queueConfigurations[0];
          string localSubDirectory = "";
          if (2 == queueConfigurations.Length) {
            localSubDirectory = queueConfigurations[1];
          }
          QueueConfigurationFull queueConfFull = new QueueConfigurationFull (remoteQueueConfigurationPath, localSubDirectory, true, false);
          m_queue = queueConfFull.CreateQueue (this.MachineId,
                                               this.MachineModuleId,
                                               defaultConfigReader);
        }
        catch (Exception ex) {
          log.ErrorFormat ("Start: the queue could not be created, the configuration could not be loaded probably, {0}, StackTrace={1}", ex, ex.StackTrace);
          Debug.Assert (null == m_queue);
          return false;
        }
        if (m_queue is ICheckedCaller) {
          ((ICheckedCaller)m_queue).SetCheckedCaller (this);
        }
      }
      return true;
    }

    /// <summary>
    /// Finish method
    /// </summary>
    public void Finish ()
    {
      Debug.Assert (null != m_queue);
    }

    /// <summary>
    /// Enqueue a collection of alarms
    /// </summary>
    /// <param name="v"></param>
    public void EnqueueCncAlarm (object v)
    {
      if (v == null) {
        return;
      }

      // Check if v is a collection
      var collection = v as ICollection;
      if (collection == null) {
        log.ErrorFormat ("EnqueueCncAlarm: v is not a collection");
        return;
      }

      // Store the collection
      ExchangeData data = ExchangeData.BuildCncAlarmExchangeData (
        m_machineId, m_machineModuleId, DateTime.UtcNow, v);
      AddData (data);
    }

    /// <summary>
    /// Enqueue a Cnc value
    /// </summary>
    /// <param name="param">Field key</param>
    /// <param name="v">Field value</param>
    public void EnqueueCncValue (string param, object v)
    {
      // - Remove the data from the set of already stopped Cnc values
      m_stoppedCncValues.Remove (param);

      // - Enqueue the value
      ExchangeData data = ExchangeData.BuildCncValueExchangeData (m_machineId,
                                                                 m_machineModuleId,
                                                                 DateTime.UtcNow,
                                                                 param,
                                                                 v);
      AddData (data);
    }

    /// <summary>
    /// Stop the Cnc Value if v is true
    /// </summary>
    /// <param name="param">List of field keys separated by a comma (,)</param>
    /// <param name="v"></param>
    public void StopCncValue (string param, object v)
    {
      try {
        if ((bool)v) {
          log.DebugFormat ("StopCncValue: stop the Cnc Values {0}", param);
          foreach (string fieldKey in param.Split (new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries)) {
            if (m_stoppedCncValues.Contains (fieldKey)) {
              log.DebugFormat ("StopCncValue: " +
                               "{0} has already been enqueued " +
                               "=> continue with the next field",
                               fieldKey);
              continue;
            }
            ExchangeData data = ExchangeData.BuildStopCncValueExchangeData (
              m_machineId, m_machineModuleId, DateTime.UtcNow, fieldKey);
            try {
              log.DebugFormat ("StopCncValue: enqueue {0}", data);
              AddData (data);
              // Enqueue was successful: add the data in the set of already stopped cnc values
              // not to enqueue it too much
              m_stoppedCncValues.Add (fieldKey);
            }
            catch (Exception ex) {
              log.ErrorFormat ("StopCncValue: enqueueing {0} failed with {1}", data, ex);
            }
          }
        }
        // Else do nothing
      }
      catch (Exception ex) {
        log.ErrorFormat ("StopCncValue: {0}", ex);
        throw ex;
      }
    }

    /// <summary>
    /// Stop the Cnc Value if v is false
    /// </summary>
    /// <param name="param">Field key</param>
    /// <param name="v"></param>
    public void StopCncValueIfNot (string param, object v)
    {
      try {
        StopCncValue (param, !((bool)v));
      }
      catch (Exception ex) {
        log.ErrorFormat ("StopCncValueIfNot: {0}", ex);
        throw ex;
      }
    }

    /// <summary>
    /// Enqueue a set of Cnc variables
    /// </summary>
    /// <param name="param">not used</param>
    /// <param name="v">Set of cnc variables (dictionary)</param>
    public void EnqueueCncVariableSet (string param, object v)
    {
      if (null == v) {
        log.ErrorFormat ("EnqueueCncVariableSet: v is null");
        throw new ArgumentNullException ();
      }

      IDictionary<string, object> filtered = GetFilteredCncVariableSet (v);

      // - Let's consider this is not necessary to stop a cnc variable
      //   because as far as I know a variable remains on the system

      if (filtered.Any ()) {
        // - Enqueue the value
        ExchangeData data = ExchangeData.BuildCncVariableSetExchangeData (m_machineId,
                                                                          m_machineModuleId,
                                                                          DateTime.UtcNow,
                                                                          filtered);
        AddData (data);
        foreach (var i in filtered) {
          m_variableChangeTracker.StoreNewVariable (i.Key, i.Value);
        }
      }
      else {
        AddDetectionTimeStamp ();
      }
    }

    IDictionary<string, object> GetFilteredCncVariableSet (object cncVariableSet)
    {
      IDictionary<string, object> filtered;
      if (cncVariableSet is IDictionary<string, object>) {
        filtered = GetFilteredCncVariableSet<object> ((IDictionary<string, object>)cncVariableSet);
      }
      else if (cncVariableSet is IDictionary<string, int>) {
        filtered = GetFilteredCncVariableSet<int> ((IDictionary<string, int>)cncVariableSet);
      }
      else if (cncVariableSet is IDictionary<string, double>) {
        filtered = GetFilteredCncVariableSet<double> ((IDictionary<string, double>)cncVariableSet);
      }
      else if (cncVariableSet is IDictionary<string, long>) {
        filtered = GetFilteredCncVariableSet<long> ((IDictionary<string, long>)cncVariableSet);
      }
      else {
        log.ErrorFormat ("GetFilteredCncVariableSet: not a supported IDictionary type {0}", cncVariableSet.GetType ());
        throw new ArgumentException ("Not a supported IDictionary type");
      }
      return filtered;
    }

    IDictionary<string, object> GetFilteredCncVariableSet<T> (IDictionary<string, T> cncVariableSet)
    {
      return cncVariableSet
        .Where (i => m_variableChangeTracker.IsNewVariableValue (i.Key, i.Value))
        .ToDictionary (i => i.Key, i => (object)i.Value);
    }

    void AddDetectionTimeStamp ()
    {
      DateTime effectiveDateTime = TruncateToSeconds (DateTime.UtcNow);
      if (m_detectionTimeStamp < effectiveDateTime) {
        log.DebugFormat ("AddDetectionTimeStamp: " +
                         "date/time {0}",
                         effectiveDateTime);
        ExchangeData data =
          ExchangeData.BuildDetectionTimeStampExchangeData (m_machineId,
                                                           m_machineModuleId,
                                                           effectiveDateTime);
        AddData (data);
        m_detectionTimeStamp = effectiveDateTime;
      }
    }

    /// <summary>
    /// Add a stamp or sequence milestone
    /// </summary>
    /// <param name="variableName">Variable name to use</param>
    /// <param name="variableValue">Variable value / Stamp Id</param>
    /// <param name="withIsoFileEnd">If true, in case stampId is 0, then consider this is the end of the Iso file</param>
    /// <param name="supportSequenceMilestone"></param>
    void AddStampAndSequenceMilestone (string variableName, double variableValue, bool withIsoFileEnd = false, bool supportSequenceMilestone = true)
    {
      // TODO: check later if it is a good way to send only the sequence milestone
      // Today it works just fine sending the stamp with the milestone
      /*
      if (supportSequenceMilestone && !m_stampVariableChangeTracker.IsNewVariableIntValue (variableName, variableValue)) {
        if (log.IsDebugEnabled) {
          log.Debug ($"SetStampId: only the decimal part changed in stamp {variableName}={variableValue} => only milestone change");
        }
        var sequenceMilestone = TimeSpan.FromMinutes (10000 * (variableValue - Math.Floor (variableValue)));
        if (0 == sequenceMilestone.Ticks) {
          log.Error ($"SetStampId: sequence milestone is 0, although there was no change of stamp, unexpected");
          AddDetectionTimeStamp ();
          m_stampVariableChangeTracker.StoreNewVariable (variableName, variableValue);
          return;
        }
        AddSequenceMilestone (variableName, sequenceMilestone);
        return;
      }
      */

      if (withIsoFileEnd) {
        AddStamp (variableName, variableValue);
      }
      else {
        AddStampIfNotNull (variableName, variableValue);
      }
    }

    /// <summary>
    /// Set a stamp Id
    /// 
    /// If the value did not change for a given variable name, do nothing.
    /// Else store the stamp Id in the Fifo (if not null)
    /// </summary>
    /// <param name="param">Variable name to use</param>
    /// <param name="variableValue">Variable value / Stamp Id</param>
    /// <param name="withIsoFileEnd">If true, in case stampId is 0, then consider this is the end of the Iso file</param>
    /// <param name="supportSequenceMilestone"></param>
    public void SetStampId (string param, double variableValue, bool withIsoFileEnd = false, bool supportSequenceMilestone = true)
    {
      if (!m_stampVariableChangeTracker.IsNewVariableValue (param, variableValue)) {
        if (log.IsDebugEnabled) {
          log.Debug ($"SetStampId: stamp {param}={variableValue} has already been processed => do nothing except updating the detection time stamp");
        }
        AddDetectionTimeStamp ();
      }
      else {
        AddStampAndSequenceMilestone (param, variableValue, withIsoFileEnd, supportSequenceMilestone);
        m_stampVariableChangeTracker.StoreNewVariable (param, variableValue);
      }
    }

    /// <summary>
    /// Set a stamp Id
    /// 
    /// If the value did not change for a given variable name, do nothing.
    /// Else store the stamp Id in the Fifo
    /// 
    /// In case stampId is 0, then consider this is the end of the Iso file
    /// </summary>
    /// <param name="param">Variable name to use</param>
    /// <param name="variableValue">Variable value / Stamp Id</param>
    public void SetStampIdWithIsoFileEnd (string param, double variableValue)
    {
      SetStampId (param, variableValue, withIsoFileEnd: true);
    }

    /// <summary>
    /// Send a sequence variable to a detection method
    /// 
    /// If the value did not change for a given variable name, do nothing.
    /// Else process the variable with the detection method in parameter
    /// 
    /// If not detection method is set, the default Stamp detection method is considered
    /// </summary>
    /// <param name="variableName">Variable name</param>
    /// <param name="variableValue">Variable value</param>
    public void SendSequenceToDetectionMethod (string variableName, int variableValue)
    {
      SendVariableToDetectionMethod (variableName, variableValue, m_detectionMethod & DetectionMethod.SequenceFilter);
    }

    /// <summary>
    /// Send a variable to a detection method with the Iso file end option
    /// 
    /// If the value did not change for a given variable name, do nothing.
    /// Else process the variable with the detection method in parameter
    /// 
    /// If not detection method is set, the default Stamp detection method is considered
    /// 
    /// If the variable is reset to 0, consider it is the end of the ISO file
    /// </summary>
    /// <param name="variableName">Variable name</param>
    /// <param name="variableValue">Variable value</param>
    public void SendSequenceToDetectionMethodWithIsoFileEnd (string variableName, double variableValue)
    {
      SendVariableToDetectionMethod (variableName, variableValue, m_detectionMethod & DetectionMethod.SequenceFilter, true);
    }

    /// <summary>
    /// Send a cycle variable to a detection method
    /// 
    /// If the value did not change for a given variable name, do nothing.
    /// Else process the variable with the detection method in parameter
    /// 
    /// If not detection method is set, the default Stamp detection method is considered
    /// </summary>
    /// <param name="variableName">Variable name</param>
    /// <param name="variableValue">Variable value</param>
    public void SendCycleToDetectionMethod (string variableName, int variableValue)
    {
      SendVariableToDetectionMethod (variableName, variableValue, m_detectionMethod & DetectionMethod.EndCycleFilter);
    }

    /// <summary>
    /// Send a start cycle variable to a detection method
    /// 
    /// If the value did not change for a given variable name, do nothing.
    /// Else process the variable with the detection method in parameter
    /// 
    /// If not detection method is set, the default Stamp detection method is considered
    /// </summary>
    /// <param name="variableName">Variable name</param>
    /// <param name="variableValue">Variable value</param>
    public void SendStartCycleToDetectionMethod (string variableName, int variableValue)
    {
      SendVariableToDetectionMethod (variableName, variableValue, m_detectionMethod & DetectionMethod.StartCycleFilter);
    }

    /// <summary>
    /// Send a variable to a detection method
    /// 
    /// If the value did not change for a given variable name, do nothing.
    /// Else process the variable with the detection method in parameter
    /// 
    /// If not detection method is set, the default Stamp detection method is considered
    /// </summary>
    /// <param name="variableName">Variable name</param>
    /// <param name="variableValue">Variable value</param>
    /// <param name="detectionMethod"></param>
    void SendVariableToDetectionMethod (string variableName, double variableValue, Lemoine.Model.DetectionMethod detectionMethod)
    {
      SendVariableToDetectionMethod (variableName, variableValue, detectionMethod, false);
    }

    /// <summary>
    /// Send a variable to a detection method
    /// 
    /// If the value did not change for a given variable name, do nothing.
    /// Else process the variable with the detection method in parameter
    /// 
    /// If not detection method is set, the default Stamp detection method is considered
    /// </summary>
    /// <param name="variableName">Variable name</param>
    /// <param name="variableValue">Variable value</param>
    /// <param name="detectionMethod"></param>
    /// <param name="withIsoFileEnd">if the variable value is reset to 0, consider it is the end of the ISO file</param>
    void SendVariableToDetectionMethod (string variableName, double variableValue, Lemoine.Model.DetectionMethod detectionMethod, bool withIsoFileEnd)
    {
      if (!m_stampVariableChangeTracker.IsNewVariableValue (variableName, variableValue)) {
        if (log.IsDebugEnabled) {
          log.Debug ($"SendVariableToDetectionMethod: variable {variableName}={variableValue} has already been processed => do nothing except updating the detection time stamp");
        }
        AddDetectionTimeStamp ();
      }
      else { // Else push it in the Fifo
        if (log.IsDebugEnabled) {
          log.Debug ($"SendVariableToDetectionMethod: new value {variableName}={variableValue} detectionMethod global={m_detectionMethod} specific={detectionMethod}");
        }
        if (detectionMethod.HasFlag (DetectionMethod.SequenceStamp)) {
          if (log.IsDebugEnabled) {
            log.Debug ($"SendVariableToDetectionMethod: AddStampAndSequenceMilestone {variableName}={variableValue}");
          }
          AddStampAndSequenceMilestone (variableName, variableValue, withIsoFileEnd, detectionMethod.HasFlag (DetectionMethod.SequenceMilestoneWithStamp));
        }
        else if (detectionMethod.HasFlag (Lemoine.Model.DetectionMethod.EndCycleStamp)
            || detectionMethod.HasFlag (Lemoine.Model.DetectionMethod.StartCycleStamp)) {
          if (log.IsDebugEnabled) {
            log.Debug ($"SendVariableToDetectionMethod: AddStampAndSequenceMilestone {variableName}={variableValue}");
          }
          AddStampAndSequenceMilestone (variableName, variableValue, withIsoFileEnd, false);
        }
        if (detectionMethod.HasFlag (Lemoine.Model.DetectionMethod.ChangeIsStartCycle)) {
          if (m_stampVariableChangeTracker.IsVariableValueChange (variableName, variableValue)) { // return false in case of a new variable
            log.DebugFormat ("SendVariableToDetectionMethod: " +
                             "ChangeIsStartCycle");
            ExchangeData data =
              ExchangeData.BuildStartCycleExchangeData (m_machineId,
                                                        m_machineModuleId,
                                                        DateTime.UtcNow);
            AddData (data);
          }
        }
        if (detectionMethod.HasFlag (Lemoine.Model.DetectionMethod.ChangeIsStopCycle)) {
          if (m_stampVariableChangeTracker.IsVariableValueChange (variableName, variableValue)) { // return false in case of a new variable
            log.DebugFormat ("SendVariableToDetectionMethod: " +
                             "ChangeIsStopCycle");
            ExchangeData data = ExchangeData.BuildStopCycleExchangeData (m_machineId,
                                                                        m_machineModuleId,
                                                                        DateTime.UtcNow);
            AddData (data);
          }
        }
        if (detectionMethod.HasFlag (Lemoine.Model.DetectionMethod.StartCycleStrictlyPositive)) {
          if (0 < variableValue) {
            log.DebugFormat ("SendVariableToDetectionMethod: " +
                             "StartCycleStrictlyPositive");
            ExchangeData data =
              ExchangeData.BuildStartCycleExchangeData (m_machineId,
                                                        m_machineModuleId,
                                                        DateTime.UtcNow);
            AddData (data);
          }
        }
        if (detectionMethod.HasFlag (Lemoine.Model.DetectionMethod.StartCycleOperationCode)) {
          if (0 != variableValue) {
            log.DebugFormat ("SendVariableToDetectionMethod: " +
                             "StartCycleOperationCode operationCode={0}",
                             variableValue);
            ExchangeData data = ExchangeData.BuildStartCycleExchangeDataWithOperationCode (
              m_machineId, m_machineModuleId, DateTime.UtcNow, variableValue.ToString ());
            AddData (data);
          }
        }
        if (detectionMethod.HasFlag (Lemoine.Model.DetectionMethod.EndCycleStrictlyPositive)) {
          if (0 < variableValue) {
            log.DebugFormat ("SendVariableToDetectionMethod: " +
                             "EndCycleStrictlyPositive");
            ExchangeData data = ExchangeData.BuildStopCycleExchangeData (
              m_machineId, m_machineModuleId, DateTime.UtcNow);
            AddData (data);
          }
        }
        if (detectionMethod.HasFlag (Lemoine.Model.DetectionMethod.EndCycleOperationCode)) {
          if (0 != variableValue) {
            log.DebugFormat ("SendVariableToDetectionMethod: " +
                             "EndCycleOperationCode operationCode={0}",
                             variableValue);
            ExchangeData data =
              ExchangeData.BuildStopCycleExchangeDataWithOperationCode (m_machineId,
                                                                        m_machineModuleId,
                                                                        DateTime.UtcNow,
                                                                        variableValue.ToString ());
            AddData (data);
          }
        }
        if (detectionMethod.HasFlag (Lemoine.Model.DetectionMethod.EndCycleZero)) {
          if (0 == variableValue) {
            log.DebugFormat ("SendVariableToDetectionMethod: " +
                             "EndCycleZero");
            ExchangeData data =
              ExchangeData.BuildStopCycleExchangeData (m_machineId,
                                                       m_machineModuleId,
                                                       DateTime.UtcNow);
            AddData (data);
          }
        }
        if (detectionMethod.HasFlag (Lemoine.Model.DetectionMethod.EndCycleQuantityOrStamp)) {
          if (0 != variableValue) {
            if ((0 < variableValue) && (variableValue <= MAX_QUANTITY)) {
              log.DebugFormat ("SendVariableToDetectionMethod: " +
                               "EndCycleQuantityOrStamp with quantity={0}",
                               variableValue);
              ExchangeData data =
                ExchangeData.BuildQuantityExchangeData (m_machineId,
                                                        m_machineModuleId,
                                                        DateTime.UtcNow,
                                                        (int)variableValue);
              AddData (data);
            }
            else { // Stamp
              log.DebugFormat ("SendVariableToDetectionMethod: " +
                               "EndCycleQuantityOrStamp with stamp {0}={1}",
                               variableName, variableValue);
              AddStamp (variableName, variableValue);
            }
          }
        }
        if (detectionMethod.HasFlag (Lemoine.Model.DetectionMethod.EndCycleOperationCodeQuantity)) {
          if (0 != variableValue) {
            log.DebugFormat ("SendVariableToDetectionMethod: " +
                             "EndCycleOperationCodeQuantity with {0}",
                             variableValue);
            ExchangeData data =
              ExchangeData.BuildStopCycleExchangeDataWithOperationCodeQuantity (m_machineId,
                                                                                m_machineModuleId,
                                                                                DateTime.UtcNow,
                                                                                variableValue.ToString ());
            AddData (data);
          }
        }
        if (detectionMethod.HasFlag (Lemoine.Model.DetectionMethod.EndCycleStampQuantity)) {
          throw new NotImplementedException ();
        }
        if (detectionMethod.HasFlag (DetectionMethod.SequenceMilestone)) {
          var milestone = TimeSpan.FromMinutes (variableValue);
          AddSequenceMilestone (variableName, milestone);
        }

        m_stampVariableChangeTracker.StoreNewVariable (variableName, variableValue);
      }
    }

    void AddStampIfNotNull (string variableName, double variableValue)
    {
      if (0 == variableValue) {
        log.InfoFormat ("AddStampIfNotNull: variable was reset to 0 => do not add the stamp in the queue");
      }
      else { // 0 != variableValue
        ExchangeData data = ExchangeData.BuildStampExchangeData (
          m_machineId, m_machineModuleId, DateTime.UtcNow, variableName, variableValue);
        AddData (data);
      }
    }

    void AddStamp (string variableName, double variableValue)
    {
      ExchangeData data = ExchangeData.BuildStampExchangeData (
        m_machineId, m_machineModuleId, DateTime.UtcNow, variableName, variableValue);
      AddData (data);
    }

    void AddSequenceMilestone (string variableName, TimeSpan sequenceMilestone)
    {
      // TODO: check the data processing is really effective in the CncDataService
      var data = ExchangeData
        .BuildSequenceMilestoneExchangeData (m_machineId, m_machineModuleId, DateTime.UtcNow, variableName, sequenceMilestone);
      AddData (data);
    }

    /// <summary>
    /// Set the auto-sequence status
    /// 
    /// Deprecated: do nothing
    /// </summary>
    /// <param name="param">Not used</param>
    /// <param name="autoSequence">auto-sequence status</param>
    public void SetAutoSequence (string param, object autoSequence)
    {
      log.WarnFormat ("SetAutoSequence: deprecated");
      return;
    }

    void StartCycle ()
    {
      ExchangeData data = ExchangeData.BuildStartCycleExchangeData (
        m_machineId, m_machineModuleId, DateTime.UtcNow);
      AddData (data);
    }

    void StopCycle ()
    {
      ExchangeData data =
        ExchangeData.BuildStopCycleExchangeData (m_machineId,
                                                m_machineModuleId,
                                                DateTime.UtcNow);
      AddData (data);
    }

    /// <summary>
    /// Dequeue a data from the queue (mainly for tests)
    /// </summary>
    /// <param name="param"></param>
    /// <returns></returns>
    public ExchangeData Dequeue (string param)
    {
      return m_queue.Dequeue ();
    }

    /// <summary>
    /// Unsafe dequeue (for tests)
    /// </summary>
    /// <param name="param"></param>
    /// <returns></returns>
    public bool UnsafeDequeue (string param)
    {
      int n = int.Parse (param);
      try {
        m_queue.UnsafeDequeue (n);
      }
      catch (Exception) {
        return false;
      }
      return true;
    }

    void UpdateLogger ()
    {
      if (0 != m_machineModuleId) {
        log = LogManager.GetLogger ($"Lemoine.Cnc.Out.CncDataQueueOutput.{this.CncAcquisitionId}.{m_machineId}.{m_machineModuleId}");
      }
      else {
        log = LogManager.GetLogger ($"Lemoine.Cnc.Out.CncDataQueueOutput.{this.CncAcquisitionId}.{m_machineId}");
      }
    }

    /// <summary>
    /// Truncate the date/time to a second precision
    /// </summary>
    /// <param name="t"></param>
    /// <returns></returns>
    DateTime TruncateToSeconds (DateTime t)
    {
      return new DateTime (t.Year, t.Month, t.Day,
                          t.Hour, t.Minute, t.Second, t.Kind);
    }
    #endregion // Methods
  }
}
