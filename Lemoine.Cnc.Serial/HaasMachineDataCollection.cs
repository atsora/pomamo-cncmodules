// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Diagnostics;
using System.Globalization;
using Lemoine.Core.Log;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Class to use the Haas Machine Data Collection module on RS-232
  /// 
  /// The default serial configuration values are:
  /// <item>Baud rate: 9600</item>
  /// <item>Parity: None</item>
  /// <item>Stop bits: 1</item>
  /// <item>Handshake: XOn/XOff</item>
  /// <item>Data bits: 8</item>
  /// </summary>
  public class HaasMachineDataCollection : AbstractSerial, Lemoine.Cnc.ICncModule
  {
    #region Members
    bool m_error = false;
    string m_previousMotionTime = null;
    bool m_echo = true;
    string m_threeInOne = null;
    bool m_threeInOneRequested = false;
    string m_programName = null;
    #endregion

    #region Getters / Setters
    /// <summary>
    /// MachineDataCollection echoes the request
    /// 
    /// Default is true but some machines directly return the result
    /// </summary>
    public bool Echo
    {
      get { return m_echo; }
      set { m_echo = value; }
    }

    /// <summary>
    /// Error ?
    /// </summary>
    public bool Error
    {
      get { return m_error; }
    }

    /// <summary>
    /// Is the machine running ?
    /// 
    /// Get this property from the motion time
    /// </summary>
    public bool Running
    {
      get { return GetRunningFromMotionTime (""); }
    }

    /// <summary>
    /// Three in one
    /// 
    /// This is only known when the machine is idle
    /// </summary>
    public string ThreeInOne
    {
      get {
        if (m_threeInOne is null) {
          if (m_threeInOneRequested) {
            if (log.IsDebugEnabled) {
              log.Debug ($"ThreeInOne: already requested");
            }
            throw new Exception ("ThreeInOne: already requested");
          }
          var q500 = GetString ("Q500");
          m_threeInOneRequested = true;
          if (q500.StartsWith ("PROGRAM")) {
            m_threeInOne = q500;
          }
          else {
            if (log.IsDebugEnabled) {
              log.Debug ($"ThreeInOne: busy, {q500}");
            }
            throw new Exception ("ThreeInOne: busy");
          }
        }
        return m_threeInOne;
      }
    }

    /// <summary>
    /// Program name
    /// 
    /// If the machine is running, then get the program name from cache
    /// </summary>
    public string ProgramName
    {
      get {
        try {
          m_programName = this.ThreeInOne.Split (new string[] { ", ", "," }, StringSplitOptions.None)[1];
          return m_programName;
        }
        catch (Exception) {
          if (m_programName is null) {
            throw;
          }
          else {
            return m_programName;
          }
        }
      }
    }


    /// <summary>
    /// Part count from ThreeInOne
    /// </summary>
    public string ThreeInOneParts {
      get {
        var threeInOne = this.ThreeInOne;
        var partsPosition = threeInOne.IndexOf ("PARTS,");
        if (-1 == partsPosition) {
          log.Error ($"ThreeInOneParts: no PARTS in {threeInOne}");
          throw new Exception ("No PARTS in three in one");
        }
        var subString = threeInOne.Substring (partsPosition + "PARTS,".Length).Trim ();
        var parts = subString.Split (new string[] { ", ", "," }, StringSplitOptions.None)[0];
        if (log.IsDebugEnabled) {
          log.Debug ($"ThreeInOneParts: return {parts} from {threeInOne}");
        }
        return parts;
      }
    }
    #endregion

    #region Constructors / Destructor / ToString methods
    /// <summary>
    /// Constructor
    /// 
    /// Set the following RS-232 default values:
    /// <item>Baud rate: 38400</item>
    /// <item>Parity: None</item>
    /// <item>Stop bits: 1</item>
    /// <item>Handshake: XOn/XOff</item>
    /// <item>Data bits: 8</item>
    /// </summary>
    public HaasMachineDataCollection ()
      : base ("Lemoine.Cnc.In.HaasMachineDataCollection")
    {
      // Default values
      this.BaudRate = 9600;
      this.Parity = "None";
      this.StopBits = 1;
      this.Handshake = "XOnXOff";
      this.DataBits = 8;
    }

    // Note: the Dispose method is implemented in
    //       the base class AbstractSerial
    #endregion

    #region Methods
    /// <summary>
    /// Make a request to the Machine Data Collection module
    /// 
    /// Usually request is like Qxxx
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    public string GetString (string request)
    {
      if (SerialPort.IsOpen == false) {
        log.WarnFormat ("GetString ({0}): " +
                        "IO Com port {1} is not opened, try to open it",
                        request,
                        this.SerialPort.PortName);
        try {
          SerialPort.Open ();
        }
        catch (Exception ex) {
          log.Error ($"GetString ({request}): Open IO COM Exception", ex);
          m_error = true;
          throw;
        }
        if (SerialPort.IsOpen == false) {
          log.Fatal ($"GetString ({request}): IO COM is not opened even after successfully opening the port");
          m_error = true;
          throw new Exception ("IO COM is not opened");
        }
      }

      if (log.IsDebugEnabled) {
        log.Debug ($"GetString /B request={request}");
      }

      try {
        SerialPort.WriteLine (request);
        if (log.IsDebugEnabled) {
          log.Debug ($"GetString ({request}): request was written");
        }
      }
      catch (Exception ex) {
        log.Error ($"GetString ({request}): Write exception", ex);
        SerialPort.Close ();
        m_error = true;
        throw;
      }

      string response = "";
      bool requestLineReadIfEcho = !m_echo;
      for (int i = 0; i < 4; i++) { // Try several times to read the right line
                                    // If echo is on:
                                    // 1st line: request (Qxxx)
                                    // 2nd line: response
                                    // else:
                                    // 1st line: response
        try {
          response = SerialPort.ReadLine ();
        }
        catch (Exception ex) {
          log.Error ($"GetString ({request}): exception in ReadLine", ex);
          SerialPort.Close ();
          m_error = true;
          throw;
        }
        if (log.IsDebugEnabled) {
          log.Debug ($"GetString ({request}): got line {response}");
        }
        if (response.Contains ("INVALID NUMBER")) { // Error !
          log.ErrorFormat ("GetString ({0}): " +
                           "got error {1}",
                           request,
                           response);
          // Read one more line here because there is a carriage return after INVALID NUMBER
          try {
            response = SerialPort.ReadLine ();
            log.DebugFormat ("GetString ({0}): " +
                             "read line {1} after INVALID NUMBER",
                             request, response);
          }
          catch (Exception ex) {
            log.Error ($"GetString ({request}): read exception after INVALID NUMBER", ex);
            SerialPort.Close ();
            throw;
          }
          throw new Exception ("Invalid number");
        }
        if (!requestLineReadIfEcho) {
          if (response.Contains (request)) { // The request was read
            if (log.IsDebugEnabled) {
              log.Debug ($"GetString ({request}): request was read");
            }
            requestLineReadIfEcho = true;
            continue;
          }
          else {
            if (log.IsDebugEnabled) {
              log.Debug ($"GetString ({request}): request was not read yet, try to read another line");
            }
            continue;
          }
        }
        else { // true == requestLineReadIfEcho (or false == m_echo)
          int indexOfStx = response.IndexOf ((char)0x02); // Get the position of STX = 0x02 (ctrl-B)
          if (-1 == indexOfStx) { // STX not found
            if (m_echo) {
              // We should get the right line now ! Because the line with STX / ETP
              // follows the request line
              log.ErrorFormat ("GetString ({0}): " +
                               "no STX=0x02 character in {1} " +
                               "just after the request line " +
                               "=> give up",
                               request,
                               response);
              throw new Exception ("invalid response after request output");
            }
            else { // false == m_echo => try another line
              if (log.IsDebugEnabled) {
                log.DebugFormat ("GetString ({0}): " +
                                 "no STX=0x02 characher in {1} " +
                                 "=> try to read another line",
                                 request,
                                 response);
              }
              continue;
            }
          }
          else { // STX found
            response = response.Substring (indexOfStx + 1); // There should be at least one character after STX, else this is ok to raise an exception
            response = response.TrimEnd (new char[] { (char)0x17 }); // Trim ETP = 0x17 (ctrl-W) at end
            if (log.IsDebugEnabled) {
              log.Debug ($"GetString ({request}): Got response {response}");
            }
            if (response.Equals ("UNKNOWN")) { // Error !
              log.Error ($"GetString ({request}): got error {response}");
              m_error = true;
              throw new Exception ("Unknown command");
            }

            return response;
          }
        }
      }

      // The data was not processed in the loop: the request line or STX was not found
      log.ErrorFormat ("GetString ({0}): " +
                       "request line or STX not found. " +
                       "Last response was {1}",
                       request, response);
      m_error = true;
      throw new Exception ("request line or STX not found");
    }

    /// <summary>
    /// Make a request to the Machine Data Collection module and get a part of it.
    /// Consider ', ' for the CSV separator.
    /// 
    /// parameter is made of the request (Qxxx), followed by a separator (:) and the position (from 0)
    /// 
    /// For example: Q500:1
    /// </summary>
    /// <param name="parameter">request:x where request is the request and x the position in the CSV response</param>
    /// <returns></returns>
    public string GetSubString (string parameter)
    {
      string[] parameters = parameter.Split (new char[] { ':' }, 2);
      if (1 == parameters.Length) { // The separator is not found, consider here we want the full response
        if (log.IsDebugEnabled) {
          log.Debug ($"GetSubString ({parameter}): no separator ':' => consider {parameter} for the request and return the full string");
        }
        return GetString (parameter);
      }
      else if (2 != parameters.Length) {
        log.ErrorFormat ("GetSubString ({0}): " +
                         "invalid parameter {0}",
                         parameter);
        throw new ArgumentException ("invalid parameter");
      }
      else { // 2 == parameters.Length
        string request = parameters[0];
        int position;
        try {
          position = int.Parse (parameters[1]);
        }
        catch (Exception ex) {
          log.Error ($"GetSubString ({parameter}): invalid column number {parameters[1]}", ex);
          throw;
        }
        string response = GetString (request);
        string[] items = response.Split (new string[] { ", ", "," }, StringSplitOptions.None);
        return items[position].Trim ();
      }
    }

    /// <summary>
    /// Get the int value of a corresponding request
    /// </summary>
    /// <param name="param">request or request:x where x is the position in the CSV response</param>
    /// <returns></returns>
    public int GetInt (string param)
    {
      return int.Parse (this.GetSubString (param).Trim ());
    }

    /// <summary>
    /// Get a double value and round it to the closest integer
    /// </summary>
    /// <param name="param">request or request:x where x is the position in the CSV response</param>
    /// <returns></returns>
    public int GetRounded (string param)
    {
      return (int)Math.Round (GetDouble (param));
    }

    /// <summary>
    /// Get the long value of a corresponding request
    /// </summary>
    /// <param name="param">request or request:x where x is the position in the CSV response</param>
    /// <returns></returns>
    public long GetLong (string param)
    {
      return long.Parse (this.GetSubString (param).Trim ());
    }

    /// <summary>
    /// Get the double value of a corresponding request
    /// </summary>
    /// <param name="param">request or request:x where x is the position in the CSV response</param>
    /// <returns></returns>
    public double GetDouble (string param)
    {
      CultureInfo usCultureInfo = new CultureInfo ("en-US");
      return double.Parse (this.GetSubString (param).Trim (),
                           usCultureInfo);
    }

    /// <summary>
    /// Check if the machine is running from the motion time
    /// 
    /// If the motion time was updated, consider the machine is running
    /// </summary>
    /// <param name="param"></param>
    /// <returns></returns>
    public bool GetRunningFromMotionTime (string param)
    {
      string motionTime = GetSubString ("Q301:1");
      if (null == m_previousMotionTime) {
        if (log.IsDebugEnabled) {
          log.Debug ($"GetRunningFromMotionTime: initial value is {motionTime}");
        }
        m_previousMotionTime = motionTime;
        throw new Exception ("GetRunningFromMotionTime: initialization");
      }
      else if (object.Equals (m_previousMotionTime, motionTime)) {
        if (log.IsDebugEnabled) {
          log.Debug ($"GetRunningFromMotionTime: same motion time {motionTime} => return false");
        }
        return false;
      }
      else {
        if (log.IsDebugEnabled) {
          log.Debug ($"GetRunningFromMotionTime: the motion time changed from {m_previousMotionTime} to {motionTime} => return true");
        }
        m_previousMotionTime = motionTime;
        return true;
      }
    }

    /// <summary>
    /// Start method: reset the error property
    /// </summary>
    public void Start ()
    {
      m_error = false;
      m_threeInOne = null;
      m_threeInOneRequested = false;
    }
    #endregion
  }
}
