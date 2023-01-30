// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Lemoine.Core.Log;

namespace Lemoine.Cnc.CncCoreClient.Okuma
{
  /// <summary>
  /// Represents different alarm levels of current OSP alarm 
  /// </summary>
  public enum OSPAlarmLevelEnum
  {
    /// <summary>
    /// 
    /// </summary>
    ALARM_P,
    /// <summary>
    /// 
    /// </summary>
    ALARM_A,
    /// <summary>
    /// 
    /// </summary>
    ALARM_B,
    /// <summary>
    /// 
    /// </summary>
    ALARM_C,
    /// <summary>
    /// 
    /// </summary>
    ALARM_D,
    /// <summary>
    /// 
    /// </summary>
    None
  }

  /// <summary>
  /// CCurrentAlarm
  /// 
  /// Alarm number (4)-Object number (2) Alarm level Alarm message Object message Alarm code 'Alarm charcter string'
  /// </summary>
  public class CCurrentAlarm
  {
    readonly ILog log = LogManager.GetLogger (typeof (CCurrentAlarm).FullName);

    /// <summary>
    /// 
    /// </summary>
    public string AlarmCharacterString { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public string AlarmCode { get; set; }

    /// <summary>
    /// Alarms related with the OSP are classified into five types such as Alarm P, A, B, C and D. 
    /// </summary>
    public OSPAlarmLevelEnum AlarmLevel { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public string AlarmMessage { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public int AlarmNumber { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public string ObjectMessage { get; set; }

    /// <summary>
    /// The object number and the object message show the programming system and the control axis where the alarm has occurred by the number and message as shown in the table below
    /// </summary>
    public int ObjectNumber { get; set; }

    /// <summary>
    /// Convert to a <see cref="CncAlarm"/>
    /// </summary>
    /// <returns></returns>
    public CncAlarm ConvertToCncAlarm ()
    {
      if ((0 == this.AlarmNumber) && string.IsNullOrEmpty (this.AlarmMessage)) {
        if (log.IsDebugEnabled) {
          log.Debug ("Convert: no alarm number and message => no alarm");
        }
        throw new Exception ("No alarm");
      }

      const string alarmType = "";
      var result = new CncAlarm ("Okuma - ThincApi", alarmType, this.AlarmNumber.ToString ());
      result.Message = this.AlarmMessage;
      if (!string.IsNullOrEmpty (this.AlarmCharacterString)) {
        result.Properties["CharacterString"] = this.AlarmCharacterString;
      }
      if (!string.IsNullOrEmpty (this.AlarmCode)) {
        result.Properties["Code"] = this.AlarmCode;
      }
      result.Properties["Level"] = this.AlarmLevel.ToString ();
      if (0 != this.ObjectNumber) {
        result.Properties["ObjectNumber"] = this.ObjectNumber.ToString ();
      }
      if (!string.IsNullOrEmpty (this.ObjectMessage)) {
        result.Properties["ObjectMessage"] = this.ObjectMessage;
      }
      return result;
    }
  }
}
