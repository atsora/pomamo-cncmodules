// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Text.RegularExpressions;
using System.Globalization;
using Lemoine.Collections;

using Lemoine.Core.Log;
using System.Collections.Concurrent;
using Lemoine.Threading;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Description of CncLogPlayer.
  /// </summary>
  public class CncLogPlayer : BaseCncModule, ICncModule, IDisposable, IChecked
  {
    static readonly Regex DICTIONARY_REGEX = new Regex ("^<[^,]+,[^,]+>.*$", RegexOptions.Compiled);

    #region Members
    /// <summary>
    /// Standard RegExp used for detecting CNC key/value pairs lines in log file
    /// </summary>
    // public static readonly string STANDARD_REGEXP1 = "^.*Lemoine\\.Cnc\\.Data\\.([0-9]+):([^[]*)\\[.*Lemoine\\.Cnc\\.Data\\.[0-9]+[^:]*: ([^=]*)=([^(]*).*$";
    public static readonly string STANDARD_REGEXP = "^([^\\s]*)\\s([^\\s]*)\\s([^=]*)=([^(]*).*$";
    DateTime? m_currentDateTime = null;
    string m_logFilePath;
    string[] m_logFilePathArray;
    ConcurrentDictionary<string, string> m_data = new ConcurrentDictionary<string, string> ();
    bool m_started = false;
    volatile bool m_stop = false;
    int m_nbLinesRead = 0;
    int m_nbLinesMatch = 0;
    double m_accelerationFactor = 1.0;
    bool m_doWait = true;
    int m_fixInterval = 0;
    int m_maxLines = 0;
    Regex m_regex = new Regex (STANDARD_REGEXP, RegexOptions.Compiled);
    bool m_processError = false;
    #endregion // Members

    #region Getters / Setters
    /// <summary>
    /// Current Date Time in Log file
    /// </summary>
    public DateTime? CurrentDateTime
    {
      get { return m_currentDateTime; }
      set { m_currentDateTime = value; }
    }

    /// <summary>
    /// Path to the log files (separator = semicolon ;)
    /// </summary>
    public string LogFilePath
    {
      get { return m_logFilePath; }
      set {
        m_logFilePath = value;
        LogFilePathArray = m_logFilePath.Split (new char[] { ';' });
        if (log.IsWarnEnabled) {
          foreach (var p in LogFilePathArray) {
            if (!File.Exists (p)) {
              log.Warn ($"LogFilePath.set: file {p} does not exist");
            }
          }
        }
      }
    }

    /// <summary>
    /// Paths to the log files (computed from LogFilePath)
    /// </summary>
    string[] LogFilePathArray
    {
      get { return m_logFilePathArray; }
      set { m_logFilePathArray = value; }
    }

    /// <summary>
    /// Acceleration factor
    /// Log replay will be performed faster for factor > 1.0
    /// </summary>
    public double AccelerationFactor
    {
      get { return m_accelerationFactor; }
      set { m_accelerationFactor = (value > 0.0 ? value : 1.0); }
    }

    /// <summary>
    /// If set to false, replay goes as fast as possible
    /// without taking care of real-time interval between log events
    /// </summary>
    public bool DoWait
    {
      get { return m_doWait; }
      set { m_doWait = value; }
    }

    /// <summary>
    /// set a fix interval in milliseconds between lines not dependent on timetag in line
    /// </summary>
    public int FixInterval
    {
      get { return m_fixInterval; }
      set { m_fixInterval = value; }
    }

    /// <summary>
    /// if MaxLines > 0, no more than MaxLines will be read in log file
    /// </summary>
    public int MaxLines
    {
      get { return m_maxLines; }
      set { m_maxLines = value; }
    }

    /// <summary>
    /// Number of lines of logfiles that have been read yet
    /// </summary>
    public int NbLinesRead
    {
      get { return m_nbLinesRead; }
      private set { m_nbLinesRead = value; }
    }

    /// <summary>
    /// Number of lines of logfiles that have been read yet
    /// that match the CNC with ID equal to CncAcquisitionId
    /// </summary>
    public int NbLinesMatch
    {
      get { return m_nbLinesMatch; }
      private set { m_nbLinesMatch = value; }
    }

    /// <summary>
    /// RegExp used for detecting CNC key/value pairs lines in log file
    /// </summary>
    public string RegExp
    {
      get { return m_regex.ToString (); }
      set {
        if (!object.Equals (value, m_regex.ToString ())) {
          m_regex = new Regex (value, RegexOptions.Compiled);
        }
      }
    }

    /// <summary>
    /// Were there a process error in the program ?
    /// </summary>
    public bool ProcessError
    {
      get { return m_processError; }
    }
    #endregion // Getters / Setters

    #region Constructors
    /// <summary>
    /// Description of the constructor
    /// </summary>
    public CncLogPlayer ()
      : base ("Lemoine.Cnc.In.CncLogPlayer")
    {
    }

    /// <summary>
    /// <see cref="IDisposable.Dispose" />
    /// </summary>
    public void Dispose ()
    {
      log.DebugFormat ("# lines read = {0} # lines match = {1}", m_nbLinesRead, m_nbLinesMatch);

      m_stop = true;

      GC.SuppressFinalize (this);
    }
    #endregion // Constructors

    #region Methods
    /// <summary>
    /// Start method
    /// </summary>
    public void Start ()
    {
      if (!m_started) {
        log.DebugFormat ("Start: begin to start reading the logs");
        var thread = new Thread (ReadLog);
        thread.Start ();
        m_started = true;
      }
    }

    /// <summary>
    /// Finish method (do nothing)
    /// </summary>
    public void Finish ()
    {
    }

    /// <summary>
    /// store key/value pair (erase previous value if any)
    /// </summary>
    /// <param name="key"></param>
    /// <param name="v"></param>
    void StoreKeyValue (string key, string v)
    {
      // object d = ParseValue (v);
      m_data[key] = v;
      log.Debug ($"StoreKeyValue: Storing key {key} = {v}");
    }

    /// <summary>
    /// retrieve value associated with a key if any
    /// otherwise raises an Exception
    /// 
    /// returns an int32 if possible, then a double if possible, then a boolean if possible,
    /// otherwise a string
    /// </summary>
    /// <param name="param">key</param>
    /// <returns></returns>
    public object GetData (string param)
    {
      log.Debug ($"GetData: param={param}");

      SetActive ();
      if (m_data.ContainsKey (param)) {
        try {
          if (m_data.TryGetValue (param, out var stringData)) {
            if (log.IsDebugEnabled) {
              log.Debug ($"GetData: got {stringData} for param {param}");
            }
            if (param.Equals ("CncVariableSet", StringComparison.InvariantCultureIgnoreCase)) {
              return Lemoine.Collections.EnumerableString.ParseDictionaryString<string, object> (":;" + stringData, s => s, ConvertData);
            }
            else if (DICTIONARY_REGEX.IsMatch (stringData)) {
              return (System.Collections.IDictionary)Lemoine.Collections.EnumerableString.ParseAuto (stringData);
            }
            else {
              return ConvertData (stringData);
            }
          }
          else {
            log.Error ($"GetData: TryGetValue returned false");
            throw new Exception ();
          }
        }
        catch (Exception ex) {
          log.Error ($"GetData: exception", ex);
          throw;
        }
      }
      else {
        string errorMsg = $"GetData: key {param} is unknown";
        log.Info (errorMsg);
        throw new ArgumentException (errorMsg);
      }
    }

    object ConvertData (string stringData)
    {
      try {
        return Convert.ChangeType (stringData, System.TypeCode.Int32, CultureInfo.InvariantCulture);
      }
      catch (Exception) {
        try {
          return Convert.ChangeType (stringData, System.TypeCode.Double, CultureInfo.InvariantCulture);
        }
        catch (Exception) {
          try {
            return Convert.ChangeType (stringData, System.TypeCode.Boolean, CultureInfo.InvariantCulture);
          }
          catch (Exception) {
            return stringData;
          }
        }
      }
    }

    /// <summary>
    /// Read a position that is in the following format:
    /// param=[Position X=0.195609 Y=-0.217557 Z=0.00085] 
    /// </summary>
    /// <param name="param"></param>
    /// <returns></returns>
    public Position GetPosition (string param)
    {
      object value = GetData (param);
      if (!(value is string)) {
        throw new Exception ("GetPosition: Param '" + param + "' is not a string that can converted into a position");
      }

      // Position to return and check to be done (at least X, Y and Z must be present)
      var position = new Position (0, 0, 0);
      bool xOk = false;
      bool yOk = false;
      bool zOk = false;

      // Remove brackets and split the value
      string valueStr = value as string;
      var parts = valueStr.Replace ("[", "").Replace ("]", "").Split (' ');
      foreach (var part in parts) {
        var split = part.Split ('=');
        if (split.Length != 2) {
          continue; // Skip it
        }

        string letter = split[0];
        double? coord = null;
        try {
          coord = (double)Convert.ChangeType (split[1], System.TypeCode.Double, CultureInfo.InvariantCulture);
        }
        catch (Exception) {
          log.WarnFormat ("GetPosition: couldn't convert '{0}' into a double", split[1]);
        }
        if (!coord.HasValue) {
          continue; // We skip the data
        }

        switch (letter) {
          case "A":
            position.A = coord.Value;
            break;
          case "B":
            position.B = coord.Value;
            break;
          case "C":
            position.C = coord.Value;
            break;
          case "U":
            position.U = coord.Value;
            break;
          case "V":
            position.V = coord.Value;
            break;
          case "W":
            position.W = coord.Value;
            break;
          case "X":
            position.X = coord.Value;
            xOk = true;
            break;
          case "Y":
            position.Y = coord.Value;
            yOk = true;
            break;
          case "Z":
            position.Z = coord.Value;
            zOk = true;
            break;
          default:
            log.WarnFormat ("GetPosition: unknown coordinate '{0}'", split[0]);
            break;
        }
      }

      if (!xOk) {
        throw new Exception ("GetPosition: x not found in '" + valueStr + "'");
      }

      if (!yOk) {
        throw new Exception ("GetPosition: y not found in '" + valueStr + "'");
      }

      if (!zOk) {
        throw new Exception ("GetPosition: z not found in '" + valueStr + "'");
      }

      return position;
    }

    /// <summary>
    /// Read an object of type "ToolLifeData"
    /// </summary>
    /// <param name="param"></param>
    /// <returns></returns>
    public ToolLifeData GetToolLifeData (string param)
    {
      object value = GetData (param);
      if (!(value is string)) {
        throw new Exception ("GetToolLifeData: Param '" + param + "' is not a string");
      }

      return new ToolLifeData ((string)value);
    }

    /// <summary>
    /// Read a list of alarms
    /// </summary>
    /// <param name="param"></param>
    /// <returns></returns>
    public IList<CncAlarm> GetAlarms (string param)
    {
      object value = GetData (param);
      if (!(value is string)) {
        throw new Exception ("GetAlarms: Param '" + param + "' is not a string");
      }

      // Split the list
      var elts = EnumerableString.ParseListString ((string)value);

      // Convert each element into an alarm
      IList<CncAlarm> alarms = new List<CncAlarm> ();
      foreach (var elt in elts) {
        try {
          alarms.Add (new CncAlarm (elt));
        }
        catch (Exception e) {
          log.ErrorFormat ("GetAlarms: couldn't convert element '{0}' into an alarm: {1}", elt, e.ToString ());
        }
      }

      return alarms;
    }

    /// <summary>
    /// Read the log file in m_logFilePath
    /// </summary>
    void ReadLog ()
    {
      Debug.Assert (LogFilePathArray != null);
      int fileIndex = 0;
      while (!m_stop) {
        try {
          if (LogFilePathArray.Length < 1) {
            throw new ArgumentException ("ReadLog: no input log file");
          }
          if (fileIndex == LogFilePathArray.Length) {
            fileIndex = 0;
          }
          string logFilePath = LogFilePathArray[fileIndex];
          fileIndex++;
          using (TextReader reader = new StreamReader (logFilePath)) {
            string line;
            while ((null != (line = reader.ReadLine ())) && !m_stop
                  && ((this.MaxLines <= 0) || (this.NbLinesRead < this.MaxLines))) {
              try {
                ProcessLine (line);
              }
              catch (Exception ex) {
                log.Error ($"ReadLog: ProcessLine {line} failed", ex);
                throw;
              }
            }
          }
          // reset CurrentDateTime and go to next file (or start again with first)
          this.CurrentDateTime = null;
        }
        catch (Exception ex) {
          m_processError = true;
          log.Error ("ReadLog: exception", ex);
        }
      }
    }

    /// <summary>
    /// used for unit testing only (and also inefficient)
    /// </summary>
    /// <param name="n"></param>
    void ProcessLines (int n)
    {
      Debug.Assert (n >= 0);
      Debug.Assert (LogFilePathArray != null);
      int fileIndex = 0;
      try {
        int nbLines = 0;
        while (nbLines < n) {
          if (LogFilePathArray.Length < 1) {
            throw new ArgumentException ("ReadLog: no input log file");
          }
          if (fileIndex == LogFilePathArray.Length) {
            fileIndex = 0;
          }
          string logFilePath = LogFilePathArray[fileIndex];
          fileIndex++;
          using (TextReader reader = new StreamReader (logFilePath)) {
            string line;
            while ((nbLines < this.NbLinesRead) && (null != (line = reader.ReadLine ()))) {
              nbLines++;
            }
            if (nbLines == this.NbLinesRead) {
              nbLines = 0;
              while ((null != (line = reader.ReadLine ())) && (nbLines < n)) {
                ProcessLine (line);
                nbLines++;
              }
            }
          }
          // reset CurrentDateTime and go to next file (or start again with first)
          this.CurrentDateTime = null;
        }
      }
      catch (Exception ex) {
        log.Error ("ProcessLines: exception", ex);
      }
    }

    DateTime ParseDateTime (string dateString)
    {
      DateTime dateTime;
      string[] dateTimeFormats = { "yyyy-MM-dd HH:mm:ss,fff" }; // TODO: add other formats

      if (DateTime.TryParseExact (dateString, dateTimeFormats, CultureInfo.InvariantCulture,
                                 DateTimeStyles.AllowWhiteSpaces, out dateTime)) {
        return dateTime;
      }
      else {
        throw new FormatException (String.Format ("String {0} is not recognized as a valid dateTime",
                                                dateString));
      }
    }

    /// <summary>
    /// Process a log file line
    /// </summary>
    /// <param name="line"></param>
    void ProcessLine (string line)
    {
      this.NbLinesRead++;
      // also: first compute start date of log
      // to infer how much time elapses between two successive lines
      Match m = m_regex.Match (line);

      if ((m.Success) && (m.Groups.Count == 5)) {
        try {
          DateTime dateTime = ParseDateTime ((m.Groups[1].Value + " " + m.Groups[2].Value).Trim ());
          if (!this.CurrentDateTime.HasValue) {
            this.CurrentDateTime = dateTime;
          }
          this.NbLinesMatch++;
          log.DebugFormat ("ProcessLine: read matching line {0}", line);
          string key = m.Groups[3].Value.Trim ();
          string val = m.Groups[4].Value.Trim ();

          if (this.DoWait) {
            int waitInMs;
            if (0 != m_fixInterval) {
              waitInMs = m_fixInterval;
            }
            else {
              TimeSpan waitTime = dateTime.Subtract (this.CurrentDateTime.Value);
              waitInMs = (int)(waitTime.TotalMilliseconds / m_accelerationFactor);
            }
            if (waitInMs > 0) {
              log.DebugFormat ("ProcessLine: Must wait for {0} ms: from {1} to {2}", waitInMs,
                              this.CurrentDateTime, dateTime);
              DateTime restartDateTime = DateTime.UtcNow.AddMilliseconds (waitInMs);
              this.SleepUntil (restartDateTime);
            }
          }

          this.CurrentDateTime = dateTime;

          StoreKeyValue (key, val);
        }
        catch (FormatException ex) {
          log.Error ($"ProcessLine: exception processing {line}", ex);
        }
      }
    }
    #endregion // Methods
  }
}
