// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using Lemoine.Core.Log;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Module to get data from a Brother machine using the HTTP protocol
  /// </summary>
  public sealed class BrotherHttp
    : Lemoine.Cnc.BaseCncModule, Lemoine.Cnc.ICncModule, IDisposable
  {
    readonly IDictionary<string, string> m_pages = new Dictionary<string, string> ();
    readonly ISet<string> m_pathInErrors = new HashSet<string> ();
    TimeOutWebRequest m_request = null;
    bool m_dataRequested = false;
    DateTime m_datetimeWait = DateTime.UtcNow;

    IDictionary<string, BrotherHttpAlarmData> m_httpAlarmData = null;

    #region Getters / Setters
    /// <summary>
    /// Host name or IP
    /// </summary>
    public string HostOrIP { get; set; }

    /// <summary>
    /// Time out
    /// Default is 200 ms
    /// </summary>
    public int TimeOutMs { get; set; } = 200;

    /// <summary>
    /// Acquisition error
    /// </summary>
    public bool AcquisitionError => m_dataRequested && !m_pages.Any ();

    /// <summary>
    /// By default, this is disable in .Net Core. Force it in .Net Core
    /// </summary>
    public bool ForceNetCore { get; set; } = false;
    #endregion // Getters / Setters

    #region Constructors / Destructor / ToString methods
    /// <summary>
    /// Description of the constructor
    /// </summary>
    public BrotherHttp ()
      : base ("Lemoine.Cnc.In.BrotherHttp")
    {
      // We need it otherwise we get the "ProtocolViolationException"
      ToggleAllowUnsafeHeaderParsing (true);
    }

    /// <summary>
    /// <see cref="IDisposable.Dispose" />
    /// </summary>
    public void Dispose ()
    {
      // Do nothing special here
      GC.SuppressFinalize (this);
    }
    #endregion // Constructors / Destructors / ToString methods

    /// <summary>
    /// Start method
    /// </summary>
    /// <returns>success</returns>
    public bool Start ()
    {
#if NETCOREAPP
      if (!this.ForceNetCore) {
        return false;
      }
#endif // NETCOREAPP

      m_dataRequested = false;
      m_httpAlarmData = null;
      this.Alarms = null;

      // Clear data
      m_pages.Clear ();
      m_pathInErrors.Clear ();

      // Initialize a webclient
      if (m_request == null) {
        m_request = new TimeOutWebRequest (TimeOutMs);
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
    /// Enable/disable useUnsafeHeaderParsing.
    /// See http://o2platform.wordpress.com/2010/10/20/dealing-with-the-server-committed-a-protocol-violation-sectionresponsestatusline/
    /// </summary>
    /// <param name="enable"></param>
    /// <returns></returns>
    public static bool ToggleAllowUnsafeHeaderParsing (bool enable)
    {
#if NETFRAMEWORK
      // Note: this hack only works with .NET Framework. I did not find any alternative for .NET Core yet
      // Get the assembly that contains the internal class
      var assembly = Assembly.GetAssembly (typeof (System.Net.Configuration.SettingsSection));
      if (assembly != null) {
        // Use the assembly in order to get the internal type for the internal class
        Type settingsSectionType = assembly.GetType ("System.Net.Configuration.SettingsSectionInternal");
        if (settingsSectionType != null) {
          // Use the internal static property to get an instance of the internal settings class.
          // If the static instance isn't created already invoking the property will create it for us.
          object anInstance = settingsSectionType.InvokeMember ("Section",
                                                               BindingFlags.Static | BindingFlags.GetProperty | BindingFlags.NonPublic,
                                                               null, null, new object[] { });
          if (anInstance != null) {
            // Locate the private bool field that tells the framework if unsafe header parsing is allowed
            FieldInfo aUseUnsafeHeaderParsing = settingsSectionType.GetField ("useUnsafeHeaderParsing", BindingFlags.NonPublic | BindingFlags.Instance);
            if (aUseUnsafeHeaderParsing != null) {
              aUseUnsafeHeaderParsing.SetValue (anInstance, enable);
              return true;
            }
          }
        }
      }
#endif // NETFRAMEWORK
      return false;
    }

    string ReadPage (string path)
    {
      m_dataRequested = true;

      if (m_pages.TryGetValue (path, out string page)) {
        if (log.IsDebugEnabled) {
          log.Debug ($"ReadPage: {path} already in cache");
        }
        return page;
      }

      if (string.IsNullOrEmpty (path)) {
        log.Error ("ReadPage: empty path");
        throw new ArgumentNullException ("Emtpy path", "path");
      }

      if (m_pathInErrors.Contains (path)) {
        log.Error ($"ReadPage: {path} was already in error");
        throw new Exception ($"Path previously in error");
      }

      if (DateTime.UtcNow < m_datetimeWait) {
        log.Info ($"ReadPage: wait {m_datetimeWait} not reached");
        throw new Exception ("Wait");
      }

      try {
        string url = "http://" + HostOrIP + "/" + path;
        log.Info ($"ReadPage: Reading {url}");

        page = m_request.DownloadString (url);
        m_pages[path] = page;
        log.Info ($"ReadPage: {url} successfully read");
        return page;
      }
      catch (WebException ex) {
        m_pathInErrors.Add (path);
        log.Error ($"ReadPage: Exception status {ex.Status} when trying to read {path} of machine {HostOrIP}", ex);

        if (ex.Status == WebExceptionStatus.Timeout || ex.Status == WebExceptionStatus.ConnectFailure) {
          // Wait 1 minute
          m_datetimeWait = DateTime.UtcNow.AddMinutes (1);
          log.Warn ($"ReadPage: no acquisition until {m_datetimeWait}");
        }
        throw;
      }
    }

    /// <summary>
    /// Read a html page
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public string GetPage (string path)
    {
      return ReadPage (path);
    }

    /// <summary>
    /// Get / set the alarms
    /// </summary>
    public IList<CncAlarm> Alarms { get; set; } = null;

    /// <summary>
    /// Complete the alarms that were previously set with the following information from the http page Alarms:
    /// <item>Message</item>
    /// <item>level</item>
    /// <item>program</item>
    /// <item>block</item>
    /// </summary>
    public IList<CncAlarm> CompleteWithHttpAlarms (string _)
    {
#if NETCOREAPP
      if (!this.ForceNetCore) {
        throw new NotSupportedException (".Net Core not supported");
      }
#endif // NETCOREAPP

      if (this.Alarms is null) {
        log.Error ($"CompleteWithHttpAlarms: Alarms is null");
        throw new Exception ("Alarms were not set before");
      }

      if (this.Alarms.Any ()) {
        if (log.IsDebugEnabled) {
          log.Debug ($"CompleteWithHttpAlarms: no alarm => nothing to do");
        }
        return this.Alarms;
      }

      var httpAlarms = this.GetHttpAlarms ();
      foreach (var cncAlarm in this.Alarms) {
        if (cncAlarm.Properties.TryGetValue ("key", out string key)) {
          if (httpAlarms.TryGetValue (key, out BrotherHttpAlarmData httpAlarm)) {
            if (log.IsDebugEnabled) {
              log.Debug ($"CompleteWithHttpAlarms: complete {key} with {httpAlarm}");
            }
            cncAlarm.Message = httpAlarm.Message;
            cncAlarm.Properties["level"] = httpAlarm.Level;
            cncAlarm.Properties["program"] = httpAlarm.Program;
            cncAlarm.Properties["block number"] = httpAlarm.BlockNumber;
          }
        }
      }
      return this.Alarms;
    }

    /// <summary>
    /// Read complementary information for the alarms
    /// </summary>
    IDictionary<string, BrotherHttpAlarmData> GetHttpAlarms ()
    {
      if (m_httpAlarmData == null) {
        m_httpAlarmData = new Dictionary<string, BrotherHttpAlarmData> ();
        try {
          // Http request to download the page "ALARM"
          string pageContent = GetPage ("alarm_log");

          // Extraction of the alarm data
          var regex = new Regex ("alarm_level_(\\d+)\">" +
                                "[^<]*<[^>]*>([^<]*)</td>" +
                                "[^<]*<[^>]*>([^<]*)</td>" +
                                "[^<]*<[^>]*>([^<]*)</td>" +
                                "[^<]*<[^>]*>([^<]*)</td>");
          foreach (Match match in regex.Matches (pageContent)) {
            // Create an alarm
            var alarmData = new BrotherHttpAlarmData {
              Level = match.Groups[1].Value,
              Message = match.Groups[3].Value.Trim (' '),
              Program = match.Groups[4].Value.Trim (' '),
              BlockNumber = match.Groups[5].Value.Trim (' ')
            };
            var alarmNumber = match.Groups[2].Value.Trim (' ');
            m_httpAlarmData[alarmNumber] = alarmData;
            log.InfoFormat ("GetHttpAlarms: Found the alarm {0} with attributes {1}", alarmNumber, alarmData);
          }
        }
        catch (Exception ex) {
          log.Error ("GetHttpAlarms: exception ", ex);
          throw;
        }
      }

      return m_httpAlarmData;
    }
  }

  /// <summary>
  /// Data associated to an alarm number
  /// </summary>
  internal class BrotherHttpAlarmData
  {
    /// <summary>
    /// Message associated to the alarm
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Level of the alarm (numerical value)
    /// </summary>
    public string Level { get; set; }

    /// <summary>
    /// Program name
    /// </summary>
    public string Program { get; set; }

    /// <summary>
    /// Block number
    /// </summary>
    public string BlockNumber { get; set; }

    /// <summary>
    /// String version of the alarm data
    /// </summary>
    /// <returns></returns>
    public override string ToString ()
    {
      return $"[Message={Message}, Level={Level}, Program={Program}, BlockNumber={BlockNumber}]";
    }
  }
}
