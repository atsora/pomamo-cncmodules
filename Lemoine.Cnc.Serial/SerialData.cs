// Copyright (C) 2009-2023 Lemoine Automation Technologies
// Copyright (C) 2025 Atsora Solutions
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO.Ports;
using System.Threading;

using Lemoine.Threading;
using Lemoine.Core.Log;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Class to get some serial data, for example to get the operation ID
  /// </summary>
  public class SerialData: AbstractSerial, Lemoine.Cnc.ICncModule
  {
    /// <summary>
    /// Default possible separators between the key and the value
    /// </summary>
    static readonly string DEFAULT_SEPARATORS = "=:/ ";
    /// <summary>
    /// Default suffix: no suffix
    /// </summary>
    static readonly string DEFAULT_SUFFIX = "";
    /// <summary>
    /// Should a space be considered as a new data ?
    /// </summary>
    static readonly bool DEFAULT_IS_SPACE_NEW_DATA = true;
    /// <summary>
    /// Default prefix for a specific line
    /// </summary>
    static readonly string DEFAULT_SPECIFIC_LINE_PREFIX = "";
    /// <summary>
    /// Default suffix for a specific line
    /// </summary>
    static readonly string DEFAULT_SPECIFIC_LINE_SUFFIX = "";
    /// <summary>
    /// Default key/value separators for a specific line
    /// </summary>
    static readonly string DEFAULT_SPECIFIC_LINE_KEY_VALUE_SEPARATORS = " ";
    /// <summary>
    /// Default data separators for a specific line
    /// </summary>
    static readonly string DEFAULT_SPECIFIC_LINE_DATA_SEPARATORS = "/";
    
    string m_serialData = ""; // Data in dataSerialPort to get a full line
    ReaderWriterLock m_dataLock = new ReaderWriterLock ();
    Hashtable m_data = new Hashtable ();
    readonly Hashtable m_events = new Hashtable ();
    readonly List<string> m_eventKeys = new List<string> ();
    string m_separators = DEFAULT_SEPARATORS;
    string m_suffix = DEFAULT_SUFFIX;
    bool m_isSpaceNewData = DEFAULT_IS_SPACE_NEW_DATA;
    string m_specificLinePrefix = DEFAULT_SPECIFIC_LINE_PREFIX;
    string m_specificLineSuffix = DEFAULT_SPECIFIC_LINE_SUFFIX;
    string m_specificLineKeyValueSeparators = DEFAULT_SPECIFIC_LINE_KEY_VALUE_SEPARATORS;
    string m_specificLineDataSeparators = DEFAULT_SPECIFIC_LINE_DATA_SEPARATORS;
    
    bool m_specificLine = false;
    bool m_error = false;

    /// <summary>
    /// String with the possible separator characters between the key and the value
    /// </summary>
    public string Separators {
      get { return m_separators; }
      set { m_separators = value; }
    }
    
    /// <summary>
    /// Optional suffix to remove in the acquired data.
    /// 
    /// Default is the empty string
    /// </summary>
    public string Suffix {
      get { return m_suffix; }
      set { m_suffix = value; }
    }
    
    /// <summary>
    /// Should a space character be considered as a separator for some new data ?
    /// 
    /// Default is true
    /// </summary>
    public bool IsSpaceNewData {
      get { return m_isSpaceNewData; }
      set { m_isSpaceNewData = value; }
    }
    
    /// <summary>
    /// Prefix for a specific line
    /// 
    /// If empty, no specific line is defined
    /// 
    /// For the moment, only a single character is accepted
    /// </summary>
    public string SpecificLinePrefix {
      get { return m_specificLinePrefix; }
      set
      {
        Debug.Assert (value.Length <= 1);
        m_specificLinePrefix = value;
      }
    }
    
    /// <summary>
    /// Suffix for a specific line
    /// 
    /// To be defined only if a specific line prefix is set
    /// 
    /// For the moment, only a single character is accepted
    /// </summary>
    public string SpecificLineSuffix {
      get { return m_specificLineSuffix; }
      set
      {
        Debug.Assert (value.Length <= 1);
        m_specificLineSuffix = value;
      }
    }
    
    /// <summary>
    /// String with the possible separator characters between the key and the value in a specific line
    /// </summary>
    public string SpecificLineKeyValueSeparators {
      get { return m_specificLineKeyValueSeparators; }
      set { m_specificLineKeyValueSeparators = value; }
    }
    
    /// <summary>
    /// String with the possible separator characters between different data in a specific line
    /// </summary>
    public string SpecificLineDataSeparators {
      get { return m_specificLineDataSeparators; }
      set { m_specificLineDataSeparators = value; }
    }
    
    /// <summary>
    /// Error ?
    /// </summary>
    public bool Error {
      get { return m_error; }
    }

    /// <summary>
    /// Description of the constructor
    /// </summary>
    public SerialData ()
      : base ("Lemoine.Cnc.In.SerialData")
    {
      SerialPort.DataReceived += new SerialDataReceivedEventHandler (ReadSerialData);
    }
    
    // Note: the Dispose method is implemented in
    //       the base class AbstractSerial

    /// <summary>
    /// Start method: open the connection
    /// </summary>
    public void Start ()
    {
      m_error = false;
      if ( (null != SerialPort)
          && (!SerialPort.IsOpen)) {
        log.Info ($"Start: Serial port {SerialPort.PortName} was not opened, try to open it");
        try {
          SerialPort.Open ();
        }
        catch (Exception ex) {
          log.Error ($"Start: Serial port {SerialPort.PortName} could not be opened", ex);
          m_error = true;
        }
      }
    }
    
    /// <summary>
    /// Get the string value of a corresponding key
    /// </summary>
    /// <param name="param">key value</param>
    /// <returns></returns>
    public string GetString (string param)
    {
      using (var holder = new ReadLockHolder (m_dataLock)) {
        if (m_data.Contains (param)) {
          if (log.IsDebugEnabled) {
            log.Debug ($"GetString: get {m_data [param]} for key {param}");
          }
          return m_data [param] as string;
        }
        else {
          log.Error ($"GetString: could not get key {param}");
          throw new Exception ("Unknown key");
        }
      }
    }

    /// <summary>
    /// Get the int value of a corresponding key
    /// </summary>
    /// <param name="param">key value</param>
    /// <returns></returns>
    public int GetInt (string param)
    {
      return int.Parse (this.GetString (param));
    }
    
    /// <summary>
    /// Get the long value of a corresponding key
    /// </summary>
    /// <param name="param">key value</param>
    /// <returns></returns>
    public long GetLong (string param)
    {
      return long.Parse (this.GetString (param));
    }
    
    /// <summary>
    /// Get the double value of a corresponding key
    /// </summary>
    /// <param name="param">key value</param>
    /// <returns></returns>
    public double GetDouble (string param)
    {
      var usCultureInfo = new CultureInfo ("en-US");
      return double.Parse (this.GetString (param),
                           usCultureInfo);
    }
    
    /// <summary>
    /// Get the list of events for a given key
    /// </summary>
    /// <param name="param">key value</param>
    /// <returns></returns>
    public Queue GetEvents (string param)
    {
      if (!m_eventKeys.Contains (param)) {
        if (log.IsDebugEnabled) {
          log.Debug ($"GetEvents: add event key {param}");
        }
        m_eventKeys.Add (param);
      }
      var queue = (Queue) m_events [param];
      if (null == queue) {
        if (log.IsDebugEnabled) {
          log.Debug ("GetEvents: returned queue is null (may happen the first time)");
        }
      }
      return queue;
    }
    
    /// <summary>
    /// Method to read the data in the serial port
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    void ReadSerialData (object sender, SerialDataReceivedEventArgs e)
    {
      System.Diagnostics.Debug.Assert (null != SerialPort);
      try {
        string readData = SerialPort.ReadExisting ();
        if (log.IsDebugEnabled) {
          log.Debug ($"ReadSerialData: got <{readData}>");
        }
        foreach (char c in readData.ToCharArray ()) {
          if (m_specificLine) {
            if ( (1 == m_specificLineSuffix.Length) && (m_specificLineSuffix[0] == c)) {
              m_specificLine = false;
            }
            else if (m_specificLineDataSeparators.Contains (c.ToString ())) {
              if (!string.IsNullOrEmpty (m_serialData)) {
                AnalyzeData (m_serialData, m_specificLineKeyValueSeparators, "");
                m_serialData = "";
              }
            }
            else if ( (c < ' ') || ('~' < c)) { // Specific characters, e.g.: NUL, STX, CR, DEL...
              log.Error ($"ReadSerialData: specific character #{(int)c} in a specific line");
            }
            else {
              m_serialData += c;
            }
          }
          else { // !m_specificLine
            if ( (1 == m_specificLinePrefix.Length) && (m_specificLinePrefix[0] == c)) {
              m_specificLine = true;
            }
            else if ( (c >= '#') && (c <= 'z')) {
              m_serialData += c;
            }
            else if (!m_isSpaceNewData && (c == ' ')) {
              m_serialData += c;
            }
            else if (m_serialData.Length > 0) {
              AnalyzeData (m_serialData, m_separators, m_suffix);
              m_serialData = "";
            }
          }
        }
      }
      catch (Exception ex) {
        log.Error ("ReadSerialData: exception, => reset the stored values", ex);
        m_error = true;
        using (var holder = new WriteLockHolder (m_dataLock)) {
          m_data.Clear ();
        }
      }
    }
    
    /// <summary>
    /// Analyze the data
    /// </summary>
    /// <param name="serialData"></param>
    /// <param name="keyValueSeparators"></param>
    /// <param name="suffix"></param>
    void AnalyzeData (string serialData, string keyValueSeparators, string suffix)
    {
      string withoutSuffix = serialData;
      
      // - Remove the suffix
      if (0 < suffix.Length) {
        if (!serialData.EndsWith (suffix, StringComparison.CurrentCultureIgnoreCase)) {
          log.Info ($"AnalyzeData: acquired string {serialData} does not end with suffix {suffix}");
          return;
        }
        withoutSuffix = serialData.Substring (0, serialData.Length - suffix.Length);
      }
      if (log.IsDebugEnabled) {
        log.Debug ($"AnalyzeData: acquired string is {withoutSuffix} without suffix ({serialData} with the suffix)");
      }
      
      string [] values = withoutSuffix.Split (keyValueSeparators.ToCharArray (),
                                              2,
                                              StringSplitOptions.RemoveEmptyEntries);
      if (values.Length < 1) {
        log.Info ("AnalyzeData: got an empty data");
      }
      else if (values.Length < 2) {
        log.Warn ($"AnalyzeData: no key/value pair, got only {values [0]}");
        using (var holder = new WriteLockHolder (m_dataLock)) {
          m_data [""] = values [0];
          PushEvent ("", values [0]);
        }
      }
      else {
        if (log.IsDebugEnabled) {
          log.Debug ($"AnalyzeData: got {values [0]}={values [1]}");
        }
        using (var holder = new WriteLockHolder (m_dataLock)) {
          m_data [values [0]] = values [1];
          PushEvent (values [0], values [1]);
        }
      }
    }
    
    /// <summary>
    /// Push an event
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    void PushEvent (string key, string value)
    {
      if (m_eventKeys.Contains (key)) {
        if (!m_events.ContainsKey (key)) {
          m_events [key] = new Queue ();
        }
        var queue = (Queue) m_events [key];
        Debug.Assert (null != queue);
        if (log.IsDebugEnabled) {
          log.Debug ($"PushEvent: push key {key} value {value}");
        }
        // Because the public members of queue are thread safe
        // there is no need to use a lock here
        queue.Enqueue (value);
      }
    }
  }
}
