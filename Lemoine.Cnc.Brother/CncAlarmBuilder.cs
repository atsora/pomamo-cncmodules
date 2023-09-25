// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Lemoine.Core.Log;

namespace Lemoine.Cnc.Module.Brother
{
  /// <summary>
  /// CncAlarmBuilder
  /// </summary>
  internal class CncAlarmBuilder
  {
    readonly ILog log = LogManager.GetLogger (typeof (CncAlarmBuilder).FullName);

    #region Getters / Setters
    #endregion // Getters / Setters

    #region Constructors
    /// <summary>
    /// Constructor
    /// </summary>
    public CncAlarmBuilder ()
    {
    }
    #endregion // Constructors

    internal CncAlarm CreateMaintenanceAlarm (string typeStr, string message, string function, string notification, string current, string end)
    {
      // Reading the type
      if (!int.TryParse (typeStr, out int type) || type < 0 || type > 23) {
        log.ErrorFormat ("Brother.CreateMaintenanceAlarm - invalid type: {0}", typeStr);
        return null;
      }

      var alarm = new CncAlarm ("Brother", "Maintenance notice", type.ToString ()) {
        Message = message
      };

      switch (type) {
      case 0: // None
        return null;
      case 1:
        alarm.Properties["type"] = "Spindle speed (x 1000 revs.)";
        break;
      case 2:
        alarm.Properties["type"] = "X-axis travel distance (m or 100 inch)";
        break;
      case 3:
        alarm.Properties["type"] = "Y-axis travel distance (m or 100 inch)";
        break;
      case 4:
        alarm.Properties["type"] = "Z-axis travel distance (m or 100 inch)";
        break;
      case 5:
        alarm.Properties["type"] = "axis 4 speed (rotation)";
        break;
      case 6:
        alarm.Properties["type"] = "axis 5 speed (rotation)";
        break;
      case 7:
        alarm.Properties["type"] = "axis 6 speed (rotation)";
        break;
      case 8:
        alarm.Properties["type"] = "axis 7 speed (rotation)";
        break;
      case 9:
        alarm.Properties["type"] = "axis 8 speed (rotation)";
        break;
      case 10:
        alarm.Properties["type"] = "Tool change times (No. of times)";
        break;
      case 11:
        alarm.Properties["type"] = "Magazine turn pitches (Pitch)";
        break;
      case 12:
        alarm.Properties["type"] = "Center-through-coolant ON time (Hours, minutes, seconds)";
        break;
      case 13:
        alarm.Properties["type"] = "Center-through-coolant ON times (No. of times)";
        break;
      case 14:
        alarm.Properties["type"] = "Outer/Front door closings (No. of times)";
        break;
      case 15:
        alarm.Properties["type"] = "Side door closings (No. of times)";
        break;
      case 16:
        alarm.Properties["type"] = "Inner door closings (No. of times)";
        break;
      case 17:
        alarm.Properties["type"] = "Servo ON times (No. of times)";
        break;
      case 18:
        alarm.Properties["type"] = "Power ON times (No. of times)";
        break;
      case 19:
        alarm.Properties["type"] = "Power ON time (Hours, minutes, seconds)";
        break;
      case 20:
        alarm.Properties["type"] = "P1 axis travel amount";
        break;
      case 21:
        alarm.Properties["type"] = "P2 axis travel amount";
        break;
      case 22:
        alarm.Properties["type"] = "P3 axis travel amount";
        break;
      case 23:
        alarm.Properties["type"] = "P4 axis travel amount";
        break;
      }

      // Other properties
      alarm.Properties["function"] = function;
      alarm.Properties["notification"] = notification;
      alarm.Properties["current"] = current;
      alarm.Properties["end"] = end;

      return alarm;
    }

    public CncAlarm CreateAlarmB (string txt)
    {
      // If not defined => return null
      if (String.IsNullOrEmpty (txt)) {
        return null;
      }

      return new CncAlarm ("Brother", "Alarm", txt);
    }

    public CncAlarm CreateAlarm (string txt)
    {
      // If not defined => return null
      if (String.IsNullOrEmpty (txt)) {
        return null;
      }

      // Check the length
      if (txt.Length != 6 && txt.Length != 10) {
        log.Error ($"CreateAlarm: invalid number (length) in parameter {txt}");
      }

      // Type of the alarm
      var type = txt.Substring (0, 2).Trim (' ');
      var typeDescription = "unknown";
      switch (type) {
      case "01":
        typeDescription = "EX";
        break;
      case "02":
        typeDescription = "EC";
        break;
      case "03":
        typeDescription = "SV";
        break;
      case "04":
        typeDescription = "NC";
        break;
      case "05":
        typeDescription = "IO";
        break;
      case "06":
        typeDescription = "SP";
        break;
      case "07":
        typeDescription = "SM";
        break;
      case "08":
        typeDescription = "SL";
        break;
      case "09":
        typeDescription = "CM";
        break;
      case "10":
        typeDescription = "ES";
        break;
      case "11":
        typeDescription = "FC";
        break;
      case "90": // typeDescription "OM" or empty with the HTTP protocol
                 // Information => not usefull
        return null;
      }

      var alarmNumber = txt.Substring (2, 4).Trim (' ');
      var alarm = new CncAlarm ("Brother", "Alarm", alarmNumber);
      alarm.Properties["type"] = typeDescription + " (" + type + ")";
      if (txt.Length == 10) {
        alarm.Properties["auxiliary number"] = txt.Substring (6, 4).Trim (' ');
      }
      alarm.Properties["key"] = typeDescription + alarmNumber;

      return alarm;
    }
  }
}
