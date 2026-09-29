// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using Pomamo.CncModule;
using System.Collections.Generic;
using System.Linq;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Description of AlarmMerger.
  /// </summary>
  public sealed class AlarmMerger : BaseCncModule, ICncModule, IDisposable
  {
    enum MergeTypes
    {
      OP_MESSAGE_TEXT_WITH_MACHINE_ALARM_NUMBER
    }

    #region Members
    IList<MergeTypes> m_merges = new List<MergeTypes> ();
    #endregion // Members

    #region Getters / Setters
    /// <summary>
    /// Type of merges that will be done, all separated with a ","
    /// * OP_MESSAGE_TEXT_WITH_MACHINE_ALARM_NUMBER
    /// </summary>
    public string MergeType
    {
      get
      {
        var str = "";
        foreach (var mergeType in m_merges) {
          str += (String.IsNullOrEmpty (str) ? "" : ",") + mergeType.ToString ();
        }

        return str;
      }
      set
      {
        var split = value.Split (',');
        foreach (var splitPart in split) {
          m_merges.Add ((MergeTypes)Enum.Parse (typeof (MergeTypes), splitPart));
        }
      }
    }
    #endregion // Getters / Setters

    #region Constructors
    /// <summary>
    /// Description of the constructor
    /// </summary>
    public AlarmMerger () : base (typeof (AlarmMerger).FullName)
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

    #region Public methods
    /// <summary>
    /// Translate a list of CncAlarm
    /// </summary>
    /// <param name="data">Data to translate, must be an IList of CncAlarm</param>
    public void Process (object data)
    {
      // Check if data is null
      if (data == null) {
        log.Warn ("AlarmMerger: cannot process, the input is null");
        return;
      }

      // Check if data has a right type
      // IEnumerable is covariant, unlike IList, so this accepts the alarms of any module.
      // The non generic IList is kept as well, since the merge removes the alarms it merged
      // from the list of the caller.
      var alarms = (data as IEnumerable<ICncAlarm>)?.ToList ();
      var callerList = data as System.Collections.IList;
      if (alarms == null || callerList == null) {
        log.Error ("AlarmMerger: cannot process, wrong type");
        return;
      }

      try {
        // Merge according to the rules that have been enabled
        foreach (var merge in m_merges) {
          switch (merge) {
            case MergeTypes.OP_MESSAGE_TEXT_WITH_MACHINE_ALARM_NUMBER:
              MergeMessageTextWithMachineAlarmNumber (alarms, callerList);
              break;
          }
        }
      }
      catch (Exception e) {
        log.ErrorFormat ("AlarmProcessing.Process: got exception {0}", e);
      }
    }
    #endregion // Public methods

    #region Merge method
    /// <summary>
    /// Merge the operator messages into the machine alarms
    /// </summary>
    /// <param name="alarms">alarms to process</param>
    /// <param name="callerList">list of the caller, from which the merged alarms are removed</param>
    void MergeMessageTextWithMachineAlarmNumber (IList<ICncAlarm> alarms, System.Collections.IList callerList)
    {
      // List of machine alarms
      var machineAlarms = new List<ICncAlarm> ();
      foreach (var alarm in alarms) {
        if (string.Equals (alarm.Type, "machine alarm")) {
          machineAlarms.Add (alarm);
        }
      }

      // List of operator messages
      var operatorMessages = new List<ICncAlarm> ();
      foreach (var alarm in alarms) {
        if (string.Equals (alarm.Type, "Operator message")) {
          operatorMessages.Add (alarm);
        }
      }

      // Foreach operator message
      foreach (var operatorMessage in operatorMessages) {
        // Compare with all machine alarms
        foreach (var machineAlarm in machineAlarms) {
          // Test if the number of the machine alarm is in the operator message
          if (operatorMessage.Message != null && machineAlarm.Number != null &&
              operatorMessage.Message.Contains (machineAlarm.Number)) {
            // In that case, the operator message number is stored as an attribute in the machine alarm
            if (machineAlarm.Properties.ContainsKey ("operator message")) {
              machineAlarm.Properties["operator message"] += ", " + operatorMessage.Number;
            }
            else {
              machineAlarm.Properties["operator message"] = operatorMessage.Number;
            }

            log.InfoFormat ("AlarmMerger.MergeMessageTextWithMachineAlarmNumber: merged {0} in {1}",
                           operatorMessage, machineAlarm);

            // The operator message is then removed
            callerList.Remove (operatorMessage);
          }
        }
      }
    }
    #endregion // Merge method
  }
}
