// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections.Generic;
using System.IO;
using Lemoine.Core.Log;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Description of AlarmReader.
  /// </summary>
  internal class AlarmReader
  {
    #region Members
    readonly ILog m_log;
    readonly TagManager m_tagManager;
    bool m_isValid = true;
    readonly IList<AlarmTag> m_alarmTags = new List<AlarmTag> ();
    #endregion Members

    #region Constructors
    /// <summary>
    /// Description of the constructor
    /// </summary>
    /// <param name="tagManager"></param>
    /// <param name="logger"></param>
    /// <param name="filePath">path of the file specifying the alarms to read</param>
    public AlarmReader (TagManager tagManager, ILog logger, string filePath)
    {
      m_tagManager = tagManager;
      m_log = logger;

      ParseCsv (filePath);
    }
    #endregion // Constructors

    #region Methods
    void ParseCsv (string filePath)
    {
      if (!File.Exists (filePath)) {
        m_isValid = false;
        return;
      }

      // Load the content of the file
      var lines = File.ReadAllLines (filePath);

      // Parse all lines
      foreach (var line in lines) {
        if (!line.StartsWith ("#", StringComparison.InvariantCulture)) {
          var parts = line.Split (',');
          try {
            // Extract data
            var parameter = parts[0];
            var condition = (AlarmTag.Condition)Enum.Parse (typeof (AlarmTag.Condition), parts[2]);
            var message = parts[3];

            // Create a tag
            Tag tag = null;
            switch (parts[1]) {
            case "UINT32":
              tag = m_tagManager.GetTag (parameter, 1, 4, typeof (UInt32));
              break;
            case "INT32":
              tag = m_tagManager.GetTag (parameter, 1, 4, typeof (Int32));
              break;
            case "UINT16":
              tag = m_tagManager.GetTag (parameter, 1, 2, typeof (UInt16));
              break;
            case "INT16":
              tag = m_tagManager.GetTag (parameter, 1, 2, typeof (Int16));
              break;
            case "UINT8":
              tag = m_tagManager.GetTag (parameter, 1, 1, typeof (byte));
              break;
            case "INT8":
              tag = m_tagManager.GetTag (parameter, 1, 1, typeof (sbyte));
              break;
            case "BOOL":
              tag = m_tagManager.GetTag (parameter, 1, 1, typeof (bool));
              break;
            default:
              throw new Exception ("EthernetIP.AlarmReader - unknown type " + parts[1]);
            }

            // Create an alarm tag and store it
            m_alarmTags.Add (new AlarmTag (tag, condition, parameter, message));
          }
          catch (Exception ex) {
            m_log.ErrorFormat ("EthernetIP.AlarmReader - Cannot parse line '{0}': {1}", line, ex.Message);
          }
        }
      }
    }

    /// <summary>
    /// Get the list of alarms
    /// </summary>
    /// <returns></returns>
    public IList<CncAlarm> GetAlarms ()
    {
      if (!m_isValid) {
        throw new Exception ("EthernetIP - AlarmReader is not valid");
      }

      // Scan all alarms
      var alarms = new List<CncAlarm> ();
      foreach (var alarmTag in m_alarmTags) {
        try {
          var alarm = alarmTag.GetAlarm ();
          if (alarm != null) {
            m_log.InfoFormat ("EthernetIP.AlarmReader - received alarm {0}", alarm);
            alarms.Add (alarm);
          }
        }
        catch (Exception ex) {
          var errorCodeException = ex as ErrorCodeException;
          if (errorCodeException != null) {
            m_log.ErrorFormat ("EthernetIP.AlarmReader - couldn't read the alarm related to {0}: {1}",
                              alarmTag.Parameter, errorCodeException.Message);
          }
          else {
            m_log.ErrorFormat ("EthernetIP.AlarmReader - couldn't read the alarm related to {0}: {1}",
                              alarmTag.Parameter, ex.Message);
          }
        }
      }
      return alarms;
    }
    #endregion // Methods
  }
}
