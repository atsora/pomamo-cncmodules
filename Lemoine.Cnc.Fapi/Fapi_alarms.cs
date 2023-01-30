// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections.Generic;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Fidia input module - alarm part
  /// </summary>
  public partial class Fapi
  {
    #region Members
    // Warning: accessed by different threads (use 'lock')
    readonly IList<CncAlarm> m_alarms = new List<CncAlarm>();
    #endregion // Members
    
    #region Get methods / Properties
    /// <summary>
    /// Get the current alarms
    /// </summary>
    /// <returns></returns>
    public IList<CncAlarm> Alarms {
      get {
        // Move the alarms in another list, clear the first list
        IList<CncAlarm> alarms = new List<CncAlarm>();
        lock (m_alarms) {
          foreach (var alarm in m_alarms)
            alarms.Add(alarm);
          m_alarms.Clear();
        }
        return alarms;
      }
    }
    #endregion // Get methods / Properties
    
    #region Private methods
    /// <summary>
    /// Function called when a message arrives
    /// Executed in another thread
    /// </summary>
    /// <param name="code"></param>
    /// <param name="userData"></param>
    void MsgCallback(string code, FapiCorbaLib.UserData userData)
    {
      // Example of an alarm:
      // 00000 00:00:00 ICS000 Message connection OK.
      
      // How the code Works:
      // Codes normally are composed of"<ONE DIGIT TYPE><TWO DIGITs KEY OF THE SOURCE>XXXX"
      // where XXXX is often but not always "_<number>" and it is used internally.
      // The <ONE DIGIT TYPE> is a letter with the following meaning: I, D, R, E, W, F, H, P, L
      // ->  Information, Debug Message, Request Message, Emergency, Warning, Fatal Error, Hold Message, Custom Message, Log Message (similar to Information)
      // The <TWO DIGITs KEY OF THE SOURCE> is the components or process that has generated the message, see the excel attached for a complete list.
      
      log.InfoFormat("Fapi.MsgCallback - received alarm '{0}'", code);
      
      // Prepare the alarm
      CncAlarm alarm = null;
      
      // Parse the code
      var codeSplit = code.Split(' ');
      if (codeSplit.Length >= 4) {
        // Code
        var alarmNumber = String.Join("", codeSplit[2].Split('_')); // remove all "_"
        if (alarmNumber == "IEX330") {
          log.WarnFormat("Fapi.MsgCallback - ignored IEX330 alarm '{0}'", code);
          return; // This is the last comment => not an alarm
        }
        if (alarmNumber.StartsWith("D", StringComparison.InvariantCultureIgnoreCase)) {
          log.WarnFormat("Fapi.MsgCallback - ignored debug message '{0}'", code);
          return; // No debug message
        }
        
        // Message
        var alarmMessage = codeSplit[3];
        for (int i = 4; i < codeSplit.Length; i++)
          alarmMessage += " " + codeSplit[i];
        
        // Severity and source
        var alarmSeverity = "unknown";
        var alarmSource = "unknown";
        if (alarmNumber.Length > 3) {
          alarmSeverity = GetSeverity(alarmNumber[0]);
          alarmSource = GetSource(alarmNumber.Substring(1, 2));
        } else {
          log.WarnFormat("Fapi.MsgCallback - cannot parse the alarm number '{0}'", alarmNumber);
        }
        
        alarm = new CncAlarm("Fidia (fapi)", alarmSource, alarmNumber);
        alarm.Properties["severity"] = alarmSeverity;
        alarm.Message = alarmMessage;
      } else {
        // Impossible to parse the alarm
        log.Warn("Fapi.MsgCallback - cannot parse the alarm code");
        alarm = new CncAlarm("Fidia (fapi)", "unknown", code);
      }
      alarm.Properties["local time"] = DateTime.Now.ToString();
      
      lock (m_alarms) {
        m_alarms.Add(alarm);
        
        // Protection if the list is too big
        while (m_alarms.Count > 100) {
          log.WarnFormat("Fapi.MsgCallback - too many alarms, removed '{0}' occuring at {1}",
            m_alarms[0].Number, m_alarms[0].Properties["local time"]);
          m_alarms.RemoveAt(0);
        }
      }
    }
    
    string GetSeverity(char oneDigit)
    {
      string result = "unknown";
      switch (oneDigit) {
        case 'I':
          result = "information";
          break;
        case 'D':
          result = "debug message";
          break;
        case 'R':
          result = "request message";
          break;
        case 'E':
          result = "emergency";
          break;
        case 'W':
          result = "warning";
          break;
        case 'F':
          result = "fatal error";
          break;
        case 'H':
          result = "hold message";
          break;
        case 'P':
          result = "custom message";
          break;
        case 'L':
          result = "log message";
          break;
        default:
          log.WarnFormat("Fapi.MsgCallback - unknown severity '{0}'", oneDigit);
          result = oneDigit + " (unknown)";
          break;
      }
      return result;
    }
    
    string GetSource(string twoDigits)
    {
      string result = "unknown";
      switch (twoDigits.ToUpper()) {
        case "CN":
          result = "CNC";
          break;
        case "EX":
          result = "Programming";
          break;
        case "PL":
          result = "Copying";
          break;
        case "DG":
          result = "Digitizing";
          break;
        case "SY":
          result = "Synchronous axis";
          break;
        case "AU":
          result = "Aucol";
          break;
        case "MQ":
          result = "Tracer, MQR option";
          break;
        case "UI":
          result = "Graphical interface";
          break;
        case "UT":
          result = "Utility module";
          break;
        case "TM":
          result = "Tool measurement";
          break;
        case "PS":
          result = "Operator's actions";
          break;
        case "DS":
          result = "DNCSER";
          break;
        case "IA":
          result = "ILVA";
          break;
        case "LN":
          result = "Transmission line - file transfer service of NCOS";
          break;
        case "LU":
          result = "ILVU";
          break;
        case "LX":
          result = "IoLux, IoLine";
          break;
        case "SM":
          result = "External PLC Siemens 3964R";
          break;
        case "TC":
          result = "Transmission via TCP/IP";
          break;
        case "SE":
          result = "Severe error, bug";
          break;
        case "IE":
          result = "IEC1131";
          break;
        case "FM":
          result = "Transmission line - file transfer service of NCOS";
          break;
        case "SD":
          result = "Siemens drives";
          break;
        case "ID":
          result = "Indramat drives";
          break;
        case "FD":
          result = "Fidia drives";
          break;
        case "PB":
          result = "Profibus";
          break;
        case "UP":
          result = "File uploader";
          break;
        case "AT":
          result = "Actual file task manager";
          break;
        case "CY":
          result = "Cycles (c--)";
          break;
        case "NE":
          result = "Transmission line - file transfer service of NCOS";
          break;
        case "BG":
          result = "Debug";
          break;
        case "HM":
          result = "HiMill";
          break;
        case "WP":
          result = "Pushbutton HPW";
          break;
        case "ST":
          result = "Management dynamic files";
          break;
        case "ME":
          result = "Screw correction";
          break;
        case "AC":
          result = "Adative control (OMATIVE software)";
          break;
        case "WS":
          result = "WS32";
          break;
        case "FC":
          result = "Fapi control";
          break;
        case "FP":
          result = "Fapi";
          break;
        case "W5":
          result = "WS5";
          break;
        case "QM":
          result = "Question manager";
          break;
        case "P1":
          result = "1st Aucol module";
          break;
        case "P2":
          result = "2nd Aucol module";
          break;
        case "P3":
          result = "3rd Aucol module";
          break;
        case "P4":
          result = "4th Aucol module";
          break;
        case "P5":
          result = "5th Aucol module";
          break;
        case "P6":
          result = "6th Aucol module";
          break;
        case "P7":
          result = "7th Aucol module";
          break;
        case "P8":
          result = "8th Aucol module";
          break;
        case "P9":
          result = "9th Aucol module";
          break;
        case "PA":
          result = "10th Aucol module";
          break;
        case "PZ":
          result = "IEC module";
          break;
        case "CS":
          result = "Client software"; // Not documented but I suppose this is related to us (davy)
          break;
        default:
          log.WarnFormat("Fapi.MsgCallback - unknown source '{0}'", twoDigits);
          result = twoDigits + " (unknown)";
          break;
      }
      return result;
    }
    #endregion // Private methods
  }
}