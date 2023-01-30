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
  public class HaasMachineDataCollection: AbstractSerial, Lemoine.Cnc.ICncModule
  {
    #region Members
    bool m_error = false;
    string m_previousMotionTime = null;
    bool m_echo = true;
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
    public bool Error {
      get { return m_error; }
    }
    
    /// <summary>
    /// Is the machine running ?
    /// 
    /// Get this property from the motion time
    /// </summary>
    public bool Running {
      get { return GetRunningFromMotionTime (""); }
    }
    
    /// <summary>
    /// Program name
    /// 
    /// This is only known when the machine is idle
    /// </summary>
    public string ProgramName {
      get
      {
        string threeInOne = GetString ("Q500");
        if (threeInOne.StartsWith ("PROGRAM")) {
          return threeInOne.Split (new string[] { ", " }, StringSplitOptions.None) [1];
        }
        else {
          throw new Exception ("ProgramName: busy");
        }
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
          SerialPort.Open();
        }
        catch (Exception ex) {
          log.ErrorFormat ("GetString ({0}): " +
                           "Open IO COM Exception {1}",
                           request,
                           ex.Message);
          m_error = true;
          throw ex;
        }
        if (SerialPort.IsOpen == false) {
          log.FatalFormat ("GetString ({0}): " +
                           "IO COM is not opened " +
                           "even after successfully opening the port",
                           request);
          m_error = true;
          throw new Exception ("IO COM is not opened");
        }
      }

      log.DebugFormat ("GetString /B request={0}",
                       request);

      try {
        SerialPort.WriteLine (request);
        log.DebugFormat ("GetString ({0}): " +
                         "request was written",
                         request, request);
      }
      catch (Exception ex) {
        log.ErrorFormat ("GetString ({0}): " +
                         "Write exception {1}",
                         request, ex.Message);
        SerialPort.Close ();
        m_error = true;
        throw ex;
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
          log.ErrorFormat ("GetString ({0}): " +
                           "Read exception {1}",
                           request, ex.Message);
          SerialPort.Close ();
          m_error = true;
          throw ex;
        }
        log.DebugFormat ("GetString ({0}): " +
                         "Got line {1}",
                         request, response);
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
            log.ErrorFormat ("GetString ({0}): " +
                             "Read exception after INVALID NUMBER {1}",
                             request, ex.Message);
            SerialPort.Close ();
            throw ex;
          }
          throw new Exception ("Invalid number");
        }
        if (!requestLineReadIfEcho) {
          if (response.Contains (request)) { // The request was read
            log.DebugFormat ("GetString ({0}): " +
                             "request was read",
                             request);
            requestLineReadIfEcho = true;
            continue;
          }
          else {
            log.DebugFormat ("GetString ({0}): " +
                             "request was not read yet, " +
                             "try to read another line",
                             request);
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
              log.DebugFormat ("GetString ({0}): " +
                               "no STX=0x02 characher in {1} " +
                               "=> try to read another line",
                               request,
                               response);
              continue;
            }
          }
          else { // STX found
            response = response.Substring (indexOfStx+1); // There should be at least one character after STX, else this is ok to raise an exception
            response = response.TrimEnd (new char[] {(char)0x17}); // Trim ETP = 0x17 (ctrl-W) at end
            log.DebugFormat ("GetString ({0}): " +
                             "Got response {1}",
                             request, response);
            
            if (response.Equals ("UNKNOWN")) { // Error !
              log.ErrorFormat ("GetString ({0}): " +
                               "got error {1}",
                               request,
                               response);
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
      string[] parameters = parameter.Split (new char[] {':'}, 2);
      if (1 == parameters.Length) { // The separator is not found, consider here we want the full response
        log.DebugFormat ("GetSubString ({0}): " +
                         "no separator ':' " +
                         "=> consider {0} for the request and return the full string",
                         parameter);
        return GetString (parameter);
      }
      else if (2 != parameters.Length) {
        log.ErrorFormat ("GetSubString ({0}): " +
                         "invalid parameter {0}",
                         parameter);
        throw new ArgumentException ("invalid parameter");
      }
      else { // 2 == parameters.Length
        string request = parameters [0];
        int position;
        try {
          position = int.Parse (parameters [1]);
        }
        catch (Exception) {
          log.ErrorFormat ("GetSubString ({0}): " +
                           "invalid column number {1}",
                           parameter, parameters [1]);
          throw;
        }
        string response = GetString (request);
        string[] items = response.Split (new string[] {", "}, StringSplitOptions.None);
        return items [position];
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
      return (int) Math.Round (GetDouble (param));
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
        log.DebugFormat ("GetRunningFromMotionTime: " +
                         "initial value is {0}",
                         motionTime);
        m_previousMotionTime = motionTime;
        throw new Exception ("GetRunningFromMotionTime: initialization");
      }
      else if (object.Equals (m_previousMotionTime, motionTime)) {
        log.DebugFormat ("GetRunningFromMotionTime: " +
                         "same motion time {0} => return false",
                         motionTime);
        return false;
      }
      else {
        log.DebugFormat ("GetRunningFromMotionTime: " +
                         "the motion time changed from {0} to {1} " +
                         "=> return true",
                         m_previousMotionTime, motionTime);
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
    }
    #endregion
  }
}
