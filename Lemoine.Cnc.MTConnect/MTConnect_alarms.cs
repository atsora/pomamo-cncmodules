// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.XPath;
using Lemoine.Collections;

namespace Lemoine.Cnc
{
  /// <summary>
  /// MTConnect input module
  /// </summary>
  public partial class MTConnect
  {
    IEnumerable<string> m_excludeAlarmProperties = new List<string> ();

    /// <summary>
    /// List string to exclude some alarm properties
    /// </summary>
    public string ExcludeAlarmProperties
    {
      get { return m_excludeAlarmProperties.ToListString (); }
      set { m_excludeAlarmProperties = EnumerableString.ParseListString (value); }
    }

    /// <summary>
    /// Get all alarms
    /// </summary>
    /// <param name="param">xpath to get alarm nodes (fault conditions)</param>
    /// <returns></returns>
    public IList<CncAlarm> GetAlarms (string param)
    {
      if (m_error) {
        log.ErrorFormat ("GetAlarms: Error while loading the XML");
        throw new Exception ("Error while loading XML");
      }

      // Get all corresponding nodes
      XPathNodeIterator nodeIterator = (m_streamsNs == null) ?
        m_streamsNavigator.Select (param) :
        m_streamsNavigator.Select (param, m_streamsNs);

      // Create alarms
      var alarms = new List<CncAlarm> ();
      while (nodeIterator.MoveNext ()) {
        var alarm = CreateAlarm (nodeIterator.Current);
        if (alarm != null) {
          alarms.Add (alarm);
        }
      }

      return alarms;
    }

    CncAlarm CreateAlarm (XPathNavigator node)
    {
      if (log.IsDebugEnabled) {
        log.Debug ($"CreateAlarm: creating an alarm from {node.InnerXml}");
      }

      // Message of the alarm
      string message = "";
      try {
        message = GetValidValue (node);
      }
      catch {
        return null;
      }

      // Properties of the alarm
      var properties = new Dictionary<string, string> ();
      properties["severity"] = node.Name;
      if (node.MoveToFirstAttribute ()) {
        do {
          properties[node.Name] = node.Value;
          if (log.IsDebugEnabled) {
            log.Debug ($"CreateAlarm: found property {node.Name} = {node.Value}");
          }
        } while (node.MoveToNextAttribute ());
      }

      // Try to find a number
      string number = properties.ContainsKey ("nativeCode") ? properties["nativeCode"] : "unknown";
      if (log.IsDebugEnabled) {
        log.Debug ($"CreateAlarm: number is {number}");
      }

      // Create an alarm
      if (!properties.ContainsKey ("type")) {
        log.Error ($"CreateAlarm: no type for alarm {node.InnerXml}");
        throw new Exception ("CreateAlarm with no type");
      }
      var alarm = new CncAlarm ("MTConnect", properties["type"], number);
      alarm.Message = message;
      var exclude = m_excludeAlarmProperties.Concat (new string[] { "type" });
      foreach (var elt in properties.Where (x => !exclude.Contains (x.Key))) {
        alarm.Properties[elt.Key] = elt.Value;
      }

      return alarm;
    }

    /// <summary>
    /// Get all alarms
    /// </summary>
    /// <param name="param">xpath to get alarms</param>
    /// <returns></returns>
    public IList<CncAlarm> GetHaasAlarms (string param)
    {
      if (m_error) {
        log.Error ("GetHaasAlarms: Error while loading the XML");
        throw new Exception ("Error while loading XML");
      }

      // Get all corresponding nodes
      XPathNodeIterator nodeIterator = (m_streamsNs is null) ?
        m_streamsNavigator.Select (param) :
        m_streamsNavigator.Select (param, m_streamsNs);

      // Create alarms
      var alarms = new List<CncAlarm> ();
      while (nodeIterator.MoveNext ()) {
        var alarm = CreateHaasAlarm (nodeIterator.Current);
        if (alarm != null) {
          alarms.Add (alarm);
        }
      }

      return alarms;
    }

    CncAlarm CreateHaasAlarm (XPathNavigator node)
    {
      if (log.IsDebugEnabled) {
        log.Debug ($"CreateHaasAlarm: creating an alarm from {node.InnerXml}");
      }

      // Properties of the alarm
      var properties = new Dictionary<string, string> ();
      if (node.MoveToFirstAttribute ()) {
        do {
          properties[node.Name] = node.Value;
          if (log.IsDebugEnabled) {
            log.Debug ($"CreateHaasAlarm: found property {node.Name} = {node.Value}");
          }
        } while (node.MoveToNextAttribute ());
      }

      // Try to find a number
      string number = properties.ContainsKey ("alarmNumber") ? properties["alarmNumber"] : "unknown";
      if (log.IsDebugEnabled) {
        log.Debug ($"CreateHaasAlarm: number is {number}");
      }

      // Message of the alarm
      string message;
      try {
        message = GetValidValue (node);
      }
      catch (Exception ex) {
        log.Error ($"CreateHaasAlarm: invalid node", ex);
        message = "";
      }
      var numberString = number.ToString ();
      if (message.StartsWith (numberString)) {
        message = message.Substring (numberString.Length);
      }
      message = message.Trim ();

      // Create an alarm
      var alarm = new CncAlarm ("MTConnect", null, number);
      alarm.Message = message;
      var exclude = m_excludeAlarmProperties;
      foreach (var elt in properties.Where (x => !exclude.Contains (x.Key))) {
        alarm.Properties[elt.Key] = elt.Value;
      }

      return alarm;
    }
  }
}
