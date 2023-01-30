// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Lemoine.Model;
using Lemoine.ModelDAO;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Description of GDBCurrent
  /// </summary>
  public partial class GDBCurrent
  {
    #region Getters / Setters
    /// <summary>
    /// Update the current cnc alarm list
    /// </summary>
    public IList<CncAlarm> CurrentCncAlarms
    {
      set
      {
        Debug.Assert (value != null);
        var now = DateTime.UtcNow;

        // Create an alarm key for each received alarm
        var receivedAlarms = new Dictionary<AlarmKey, CncAlarm> ();
        foreach (var alarm in value) {
          receivedAlarms[new AlarmKey (alarm)] = alarm;
        }

        try {
          using (var transaction = this.Session.BeginTransaction ("Cnc.Current.CurrentCncAlarms")) {
            var alarms = ModelDAOHelper.DAOFactory.CurrentCncAlarmDAO.FindByMachineModule (m_machineModule);

            // Create an alarm key for each stored alarm
            var storedAlarms = new Dictionary<AlarmKey, ICurrentCncAlarm> ();
            foreach (var alarm in alarms) {
              storedAlarms[new AlarmKey (alarm.CncInfo, alarm.CncSubInfo, alarm.Type, alarm.Number, alarm.Properties)] = alarm;
            }

            // Process all stored alarms => update or delete
            foreach (var key in storedAlarms.Keys) {
              if (receivedAlarms.ContainsKey (key)) {
                receivedAlarms.Remove (key);
                storedAlarms[key].DateTime = now;
                ModelDAOHelper.DAOFactory.CurrentCncAlarmDAO.MakePersistent (storedAlarms[key]);
              }
              else {
                ModelDAOHelper.DAOFactory.CurrentCncAlarmDAO.MakeTransient (storedAlarms[key]);
              }
            }

            // Remaining received alarms are created
            foreach (var key in receivedAlarms.Keys) {
              var alarm = receivedAlarms[key];
              var currentAlarm = ModelDAOHelper.ModelFactory.CreateCurrentCncAlarm (
                m_machineModule, now, alarm.CncInfo, alarm.CncSubInfo, alarm.Type, alarm.Number);
              currentAlarm.Message = alarm.Message;
              foreach (var propKey in alarm.Properties.Keys) {
                currentAlarm.Properties[propKey] = alarm.Properties[propKey];
              }

              ModelDAOHelper.DAOFactory.CurrentCncAlarmDAO.MakePersistent (currentAlarm);
            }

            transaction.Commit ();
          }
        }
        catch (ArgumentException) {
          throw;
        }
        catch (Exception ex) {
          log.ErrorFormat ("CurrentCncAlarms.set: exception {0}", ex);
          CloseSession ();
          throw;
        }
      }
    }
    #endregion // Getters / Setters
  }
}
