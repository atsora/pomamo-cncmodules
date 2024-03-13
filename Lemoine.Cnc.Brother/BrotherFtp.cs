// Copyright (C) 2009-2023 Lemoine Automation Technologies
// Copyright (C) 2024 Atsora Solutions
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using FluentFTP;
using FluentFTP.Exceptions;
using Lemoine.Cnc.Module.Brother;
using Lemoine.Core.Log;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Module to read data in a Brother machine with the FTP protocol
  /// </summary>
  public sealed class BrotherFtp
    : Lemoine.Cnc.BaseCncModule, Lemoine.Cnc.ICncModule, IDisposable
  {
    readonly IDictionary<string, FileParser> m_parsers = new Dictionary<string, FileParser> ();
    readonly ISet<string> m_fileInErrors = new HashSet<string> ();
    FtpClient m_ftpClient = null;
    string m_hostOrIp = null;
    string m_login = null;
    string m_password = null;
    bool m_dataRequested = false;
    DateTime m_datetimeWait = DateTime.UtcNow;

    readonly CncAlarmBuilder m_cncAlarmBuilder = new CncAlarmBuilder ();
    readonly ToolLifeBuilder m_toolLifeBuilder = new ToolLifeBuilder ();

    #region Getters / Setters
    /// <summary>
    /// Host name or IP
    /// </summary>
    public string HostOrIP
    {
      get => m_hostOrIp;
      set {
        if (!string.Equals (m_hostOrIp, value)) {
          if (null != m_ftpClient) {
            m_ftpClient.Dispose ();
            m_ftpClient = null;
          }
          m_hostOrIp = value;
        }
      }
    }

    /// <summary>
    /// FTP login
    /// </summary>
    public string Login
    {
      get => m_login;
      set {
        if (!string.Equals (m_login, value)) {
          if (null != m_ftpClient) {
            m_ftpClient.Dispose ();
            m_ftpClient = null;
          }
          m_login = value;
        }
      }
    }

    /// <summary>
    /// FTP password
    /// </summary>
    public string Password
    {
      get => m_password;
      set {
        if (!string.Equals (m_password, value)) {
          if (null != m_ftpClient) {
            m_ftpClient.Dispose ();
            m_ftpClient = null;
          }
        }
        m_password = value;
      }
    }

    /// <summary>
    /// Time out
    /// Default is 200 ms
    /// </summary>
    public int TimeOutMs { get; set; } = 200;

    /// <summary>
    /// "B" for B00 machines or "C" for C00 machines
    /// </summary>
    public string MachineType { get; set; }

    /// <summary>
    /// ProgramName
    /// </summary>
    public string ProgramName { get; set; }

    /// <summary>
    /// Acquisition error
    /// </summary>
    public bool AcquisitionError => m_dataRequested && !m_parsers.Any ();
    #endregion // Getters / Setters

    #region Constructors / Destructor / ToString methods
    /// <summary>
    /// Description of the constructor
    /// </summary>
    public BrotherFtp ()
      : base ("Lemoine.Cnc.In.BrotherFtp")
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
    #endregion // Constructors / Destructors / ToString methods

    /// <summary>
    /// Start method
    /// </summary>
    /// <returns>success</returns>
    public bool Start ()
    {
      m_dataRequested = false;

      if (String.IsNullOrEmpty (Login)) {
        log.WarnFormat ("Start: Cannot initialize a FTP connection with no login");
        return false;
      }

      // Clear data
      m_parsers.Clear ();
      m_fileInErrors.Clear ();

      if (m_ftpClient is null) {
        m_ftpClient = new FtpClient (m_hostOrIp, m_login, m_password);
        m_ftpClient.Config.ReadTimeout = this.TimeOutMs;
      }
      m_ftpClient.AutoConnect ();

      return true;
    }

    /// <summary>
    /// Finish method
    /// </summary>
    public void Finish ()
    {
      m_ftpClient.Disconnect ();
    }

    FileParser GetFileParser (string fileName)
    {
      m_dataRequested = true;

      if (m_parsers.TryGetValue (fileName, out FileParser fileParser)) {
        if (log.IsDebugEnabled) {
          log.Debug ($"GetFileParser: {fileName} already in cache");
        }
        return fileParser;
      }

      if (m_ftpClient is null) {
        log.Error ($"GetFileParser: ftp client is null");
        throw new Exception ($"No ftp client");
      }
      if (!m_ftpClient.IsConnected) {
        log.Error ($"GetFileParser: ftp client is not connected");
        throw new Exception ($"FTP client not connected");
      }

      if (string.IsNullOrEmpty (fileName)) {
        log.Error ($"GetFileParser: invalid fileName");
        throw new ArgumentNullException ($"Invalid filename", "param");
      }

      if (m_fileInErrors.Contains (fileName)) {
        log.Error ($"GetFileParser: file {fileName} was already in error");
        throw new Exception ($"File previously in error");
      }

      if (DateTime.UtcNow < m_datetimeWait) {
        log.Info ($"GetFileParser: wait {m_datetimeWait}");
        throw new Exception ("Wait");
      }

      try {
        log.Info ($"GetFileParser: read ftp://{HostOrIP}/{fileName}");
        string fileContent;
        using (var stream = m_ftpClient.OpenRead (fileName)) {
          using (var reader = new StreamReader (stream)) {
            fileContent = reader.ReadToEnd ();
          }
        }
        fileParser = new FileParser (log, fileName, fileContent);
        m_parsers[fileName] = fileParser;
        log.Info ($"GetFileParser: {fileName} successfully read");
        return fileParser;
      }
      catch (System.TimeoutException ex) {
        m_fileInErrors.Add (fileName);
        log.Error ($"GetFileParser: TimeoutException status when trying to read {fileName} of machine {HostOrIP}", ex);
        // Wait 1 minute
        m_datetimeWait = DateTime.UtcNow.AddMinutes (1);
        log.Warn ($"GetFileParser: no acquisition until {m_datetimeWait}");
        throw;
      }
      catch (FtpMissingSocketException ex) {
        m_fileInErrors.Add (fileName);
        log.Error ($"GetFileParser: FtpMissingSocketException status when trying to read {fileName} of machine {HostOrIP}", ex);
        // Wait 1 minute
        m_datetimeWait = DateTime.UtcNow.AddMinutes (1);
        log.Warn ($"GetFileParser: no acquisition until {m_datetimeWait}");
        throw;
      }
      catch (FtpException ex) {
        m_fileInErrors.Add (fileName);
        log.Error ($"GetFileParser: FtpException when trying to read {fileName} of machine {HostOrIP}", ex);
        throw;
      }
    }

    /// <summary>
    /// Get a string in a file, associated to a symbol and having a specific position
    /// </summary>
    /// <param name="fileName"></param>
    /// <param name="symbol"></param>
    /// <param name="position"></param>
    /// <returns></returns>
    public string GetString (string fileName, string symbol, int position)
    {
      var fileParser = GetFileParser (fileName);
      return fileParser.GetString (symbol, position);
    }

    /// <summary>
    /// Get a string list in a file, associated to a symbol
    /// </summary>
    /// <param name="fileName"></param>
    /// <param name="symbol"></param>
    /// <returns></returns>
    public IList<string> GetStringList (string fileName, string symbol)
    {
      var fileParser = GetFileParser (fileName);
      return fileParser.GetStringList (symbol);
    }

    /// <summary>
    /// Get the full content of a file
    /// All symbols associated to a string list
    /// </summary>
    /// <param name="fileName"></param>
    /// <returns></returns>
    public IDictionary<string, IList<string>> GetSymbolListContent (string fileName)
    {
      var fileParser = GetFileParser (fileName);
      return fileParser.GetFullContent ();
    }

    /// <summary>
    /// Get a string contained in a file (FTP connection required)
    /// </summary>
    /// <param name="param">format: {file}|{symbol}|{position}</param>
    /// <returns></returns>
    public string GetString (string param)
    {
      try {
        var parts = param.Split ('|');
        if (parts.Length != 3) {
          log.Error ($"GetString: invalid param {param}: no 3 elements");
          throw new ArgumentException ("No 3 elements", "param");
        }

        if (string.IsNullOrEmpty (parts[0])) {
          log.Error ($"GetString: first element Filename is empty in {param}");
          throw new Exception ("Filename cannot be empty");
        }

        if (string.IsNullOrEmpty (parts[1])) {
          log.Error ($"GetString: second element Symbol is empty in {param}");
          throw new Exception ("Symbol cannot be empty");
        }

        int position = -1;
        if (!int.TryParse (parts[2], out position)) {
          log.Error ($"GetString: 3rd element is not a number in {param}");
          throw new Exception (parts[2] + "' is not a number");
        }

        return GetString (parts[0], parts[1], position);
      }
      catch (Exception ex) {
        log.Error ($"GetString: param={param} exception", ex);
        throw;
      }
    }

    /// <summary>
    /// Get an int contained in a file (FTP connection required)
    /// </summary>
    /// <param name="param">format: {file}|{symbol}|{position}</param>
    /// <returns></returns>
    public int GetInt (string param)
    {
      try {
        return int.Parse (GetString (param));
      }
      catch (Exception ex) {
        log.Error ($"GetInt: param={param} exception", ex);
        throw;
      }
    }

    /// <summary>
    /// Get a double contained in a file (FTP connection required)
    /// </summary>
    /// <param name="param">format: {file}|{symbol}|{position}</param>
    /// <returns></returns>
    public double GetDouble (string param)
    {
      try {
        return double.Parse (GetString (param));
      }
      catch (Exception ex) {
        log.Error ($"GetDouble: param={param} exception", ex);
        throw;
      }
    }

    /// <summary>
    /// Get a boolean contained in a file (FTP connection required)
    /// </summary>
    /// <param name="param">format: {file}|{symbol}|{position}</param>
    /// <returns></returns>
    public bool GetBool (string param)
    {
      try {
        string tmp = GetString (param);
        if (tmp == "0" || tmp == "OFF") {
          return false;
        }
        else if (tmp == "1" || tmp == "ON") {
          return true;
        }
        else {
          log.Error ($"GetBool: unexpected value {tmp}");
          throw new Exception ("Cannot convert '" + tmp + "' into a boolean");
        }
      }
      catch (Exception ex) {
        log.Error ($"GetBool: param={param} exception", ex);
        throw;
      }
    }

    /// <summary>
    /// Get all elements of a specific line
    /// </summary>
    /// <param name="param">format: {file}|{symbol}</param>
    /// <returns></returns>
    public IList<string> GetStringList (string param)
    {
      try {
        var parts = param.Split ('|');
        if (parts.Length != 2) {
          throw new Exception ("Wrong param '" + param + "'");
        }

        return GetStringList (parts[0], parts[1]);
      }
      catch (Exception ex) {
        log.Error ($"GetStringList: param={param} exception", ex);
        throw;
      }
    }

    /// <summary>
    /// Get all elements of a specific line, as a list of int
    /// </summary>
    /// <param name="param">format: {file}|{symbol}</param>
    /// <returns></returns>
    public IList<int> GetIntList (string param)
    {
      try {
        IList<int> result = new List<int> ();

        var resultTmp = GetStringList (param);
        foreach (var val in resultTmp) {
          if (!string.IsNullOrEmpty (val)) {
            result.Add (int.Parse (val));
          }
        }

        return result;
      }
      catch (Exception ex) {
        log.Error ($"GetIntList: param={param} exception", ex);
        throw;
      }
    }

    /// <summary>
    /// Read the maintenance notice in a file
    /// Equivalent to GetTCPMaintenanceNotice
    /// </summary>
    /// <returns></returns>
    public IList<CncAlarm> MaintenanceNotice
    {
      get {
        try {
          IList<CncAlarm> result = new List<CncAlarm> ();

          var fileContent = GetSymbolListContent ("MAINTC.NC");
          foreach (var symbol in fileContent.Keys) {
            var parts = fileContent[symbol];
            if (parts.Count != 6) {
              log.WarnFormat ("Wrong number of parts: {0} instead of 6", parts.Count);
            }
            else {
              var alarm = m_cncAlarmBuilder.CreateMaintenanceAlarm (parts[0], parts[1], parts[2], parts[3], parts[4], parts[5]);
              if (alarm != null) {
                result.Add (alarm);
              }
            }
          }

          return result;
        }
        catch (Exception ex) {
          log.Error ($"MaintenanceNotice.get: exception", ex);
          throw;
        }
      }
    }

    /// <summary>
    /// Read the alarms
    /// Equivalent to GetTCPAlarms
    /// </summary>
    public IList<CncAlarm> Alarms
    {
      get {
        try {
          IList<CncAlarm> result = new List<CncAlarm> ();

          switch (MachineType) {
            case "B":
              // B00 machines
              var alarmNumbers = GetStringList ("MEM.NC|E01");
              foreach (var alarmNumber in alarmNumbers) {
                var alarm = m_cncAlarmBuilder.CreateAlarmB (alarmNumber);
                if (alarm != null) {
                  result.Add (alarm);
                }
              }
              break;
            case "C":
              // C00 machines
              var fileContent = GetSymbolListContent ("ALARM.NC");
              foreach (var symbol in fileContent.Keys) {
                var parts = fileContent[symbol];
                foreach (var part in parts) {
                  try {
                    var alarm = m_cncAlarmBuilder.CreateAlarm (part);
                    if (alarm != null) {
                      result.Add (alarm);
                    }
                  }
                  catch (Exception ex) {
                    log.Error ($"Alarms: skip alarm {part} because of exception", ex);
                  }
                }
              }
              break;
            default:
              log.Error ($"Alarms: machine type {this.MachineType} is not supported");
              throw new Exception ("Not supported machine type");
          }

          return result;
        }
        catch (Exception ex) {
          log.Error ($"Alarms.get: exception", ex);
          throw;
        }
      }
    }

    /// <summary>
    /// Read a set of macros with the metric units
    /// Equivalent to GetTCPMacroSet
    /// </summary>
    /// <param name="param">format: {separator}{macro number 1}{separator}{macro number 2}... For example ",C500,C673,C999"</param>
    /// <returns></returns>
    public IDictionary<string, double> GetMacroSetMetric (string param)
    {
      return GetFTPMacroSet ("M", param);
    }

    /// <summary>
    /// Read a set of macros with the imperial units
    /// Equivalent to GetTCPMacroSet
    /// </summary>
    /// <param name="param">format: {separator}{macro number 1}{separator}{macro number 2}... For example ",C500,C673,C999"</param>
    /// <returns></returns>
    public IDictionary<string, double> GetMacroSetInches (string param)
    {
      return GetFTPMacroSet ("I", param);
    }

    /// <summary>
    /// Read a set of macros
    /// Equivalent to GetTCPMacroSet
    /// </summary>
    /// <param name="unitLetter">M for metric, I for inch, D for current unit system</param>
    /// <param name="param">format: {separator}{macro number 1}{separator}{macro number 2}... For example ",C500,C673,C999"</param>
    /// <returns></returns>
    IDictionary<string, double> GetFTPMacroSet (string unitLetter, string param)
    {
      try {
        var macroNames = Lemoine.Collections.EnumerableString.ParseListString (param);

        var result = new Dictionary<string, double> ();

        var fileContent = GetSymbolListContent ("MCRN" + unitLetter + "1.NC");
        foreach (var macroName in macroNames) {
          if (fileContent.ContainsKey (macroName)) {
            var parts = fileContent[macroName];
            double value = 0;
            if (parts.Count == 0 || string.IsNullOrEmpty (parts[0])) {
              log.WarnFormat ("Macro {0} is empty", macroName);
            }
            else if (!double.TryParse (parts[0], out value)) {
              log.WarnFormat ("GetFTPMacroSet: Value {0} of macro {1} cannot be parsed as a double", parts[0], macroName);
            }
            else {
              result[macroName] = value;
            }
          }
        }

        return result;
      }
      catch (Exception ex) {
        log.Error ($"GetFTPMacroSet: param={param} exception", ex);
        throw;
      }
    }

    /// <summary>
    /// Get the tool life data
    /// Equivalent to GetTCPToolLifeData
    /// </summary>
    /// <param name="param">File to read (TOLNI1.NC for inches or TOLNM1.NC for metrics)</param>
    /// <returns></returns>
    public ToolLifeData GetToolLifeData (string param)
    {
      var tld = new ToolLifeData ();

      try {
        var fileContent = GetSymbolListContent (param);

        // For each input, add a tool definition
        foreach (var symbol in fileContent.Keys) {
          if (fileContent[symbol].Count >= 10) {
            m_toolLifeBuilder.AddToolLife (ref tld, symbol, fileContent[symbol]);
          }
        }
      }
      catch (Exception ex) {
        log.Error ($"GetToolLifeData: param={param} exception", ex);
        throw;
      }

      return tld;
    }
  }
}
