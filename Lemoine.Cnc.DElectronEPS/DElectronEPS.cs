// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Lemoine.Cnc
{
  /// <summary>
  /// DElectronEPS input module
  /// </summary>
  public class DElectronEPS : BaseCncModule, ICncModule, IDisposable
  {
    #region Members
    bool m_firstRun = true;
    DElectronCommand m_delectronCommand = null;
    CommandStorage m_storage = null;
    #endregion // Members

    #region Getters / Setters
    /// <summary>
    /// Ip address of the machine
    /// </summary>
    public string IpAddress { get; set; }

    /// <summary>
    /// Port of the machine
    /// </summary>
    public int Port { get; set; }

    /// <summary>
    /// Emergency stop status
    /// </summary>
    public bool ESTOP { get; set; }

    /// <summary>
    /// Alarms
    /// </summary>
    public IList<CncAlarm> Alarms { get; set; }

    /// <summary>
    /// Current cnc mode
    /// </summary>
    public string CncModes
    {
      get {
        int cncModeNum = GetInt ("2:ZZSTATOTAS[0]");
        string cncMode = "";
        switch (cncModeNum) {
        case 0:
          cncMode = "Reset";
          break;
        case 1:
          cncMode = "Automatic state";
          break;
        case 2:
          cncMode = "Program selection";
          break;
        case 3:
          cncMode = "Block search";
          break;
        case 4:
          cncMode = "Jog";
          break;
        case 5:
          cncMode = "Edit state";
          break;
        case 7:
          cncMode = "Homing procedure";
          break;
        case 8:
          cncMode = "Service state 1";
          break;
        case 9:
          cncMode = "Service state 2";
          break;
        case 10:
          cncMode = "MDI";
          break;
        default:
          cncMode = "Unknown";
          break;
        }
        return cncMode;
      }
    }

    /// <summary>
    /// Error status
    /// </summary>
    public bool Error { get; private set; }
    #endregion // Getters / Setters

    #region Constructors / Destructor
    /// <summary>
    /// Description of the constructor
    /// </summary>
    public DElectronEPS () : base ("Lemoine.Cnc.In.DElectronEPS")
    {
      ESTOP = false;
      Error = false;
    }

    /// <summary>
    /// <see cref="IDisposable.Dispose" />
    /// </summary>
    public void Dispose ()
    {
      m_delectronCommand.Disconnect ();

      // Do nothing special here
      GC.SuppressFinalize (this);
    }
    #endregion // Constructors / Destructor

    #region Methods
    /// <summary>
    /// Start method
    /// </summary>
    public bool Start ()
    {
      // Executed only the first time
      if (m_delectronCommand == null) {
        // Preparation of the command storage, with mandatory commands
        m_storage = new CommandStorage (log);
        m_storage.StoreCommand (2, "ZZERCODE"); // CNC alarms
        m_storage.StoreCommand (2, "2E"); // PLC alarms

        m_delectronCommand = new DElectronCommand (IpAddress, Port, log);
      }

      // Remove all values
      m_storage.ClearValues ();

      // Check the connexion and load new values
      Error = !m_delectronCommand.CheckConnection ();
      if (!Error && !m_firstRun) {
        Error = !InitValues ();

        // Emergency stop and alarms
        InitializeAlarms ();
      }

      return true;
    }

    bool InitValues ()
    {
      bool ret = true;
      foreach (int commandNumber in m_storage.GetCommands ()) {
        try {
          var values = m_delectronCommand.GetValues (
            commandNumber, m_storage.GetCommands (commandNumber));
          m_storage.StoreValues (commandNumber, values);
        }
        catch (NotSupportedException ex) {
          // A command doesn't exist
          m_storage.RemoveCommand (commandNumber);
          log.Error ($"DElectronEPS: command {commandNumber} has been removed (not supported)", ex);
        }
        catch (ArgumentException ex) {
          // The command exist but the arguments are wrong
          m_storage.RemoveCommand (commandNumber);
          log.Error ($"DElectronEPS: command {commandNumber} has been removed (bad arguments)", ex);
        }
        catch (Exception ex) {
          log.Error ($"DElectronEPS: Couldn't initialize values", ex);
          ret = false;
        }
      }

      return ret;
    }

    /// <summary>
    /// Finish method
    /// </summary>
    public void Finish ()
    {
      if (m_firstRun) {
        m_firstRun = false;
      }
      else {
        m_storage.ClearValues ();
      }
    }

    /// <summary>
    /// Get a string value
    /// 
    /// Command# is mandatory
    /// 
    /// Arguments are separated by |
    /// 
    /// Optionally, you can extract one of the returned value. Then use Item#
    /// </summary>
    /// <param name="request">Command#:Arg1|Arg2[:Item#]</param>
    /// <returns></returns>
    public string GetString (string request)
    {
      // Check if the request is valid
      var splitRequest = request.Split (':');
      if (splitRequest.Length < 2 && 3 < splitRequest.Length) {
        log.Error ($"GetString: invalid request {request}");
        throw new Exception ("DElectronEPS: invalid request " + request);
      }

      int commandNumber = int.Parse (splitRequest[0]);

      // Store the variable during the first run, connected or not
      if (m_storage.StoreCommand (commandNumber, splitRequest[1])) {
        throw new Exception ("DElectronEPS: Values are not ready yet");
      }

      var itemNumber = 0;
      if (3 == splitRequest.Length) {
        itemNumber = int.Parse (splitRequest[2]);
      }

      // Return what has been stored
      return m_storage.GetValue (commandNumber, splitRequest[1], itemNumber);
    }

    /// <summary>
    /// Get an short value
    /// </summary>
    /// <param name="variable">variable</param>
    /// <returns></returns>
    public short GetShort (string variable)
    {
      return short.Parse (GetString (variable));
    }

    /// <summary>
    /// Get an int value
    /// </summary>
    /// <param name="variable">variable</param>
    /// <returns></returns>
    public int GetInt (string variable)
    {
      return int.Parse (GetString (variable));
    }

    /// <summary>
    /// Get a string coming from an ascii value
    /// </summary>
    /// <param name="variable"></param>
    /// <returns></returns>
    public string GetStringFromAscii (string variable)
    {
      char ascii = (char)GetInt (variable);
      return (ascii == 0) ? "-" : ascii.ToString ();
    }

    /// <summary>
    /// Get a long value
    /// </summary>
    /// <param name="variable">variable</param>
    /// <returns></returns>
    public long GetLong (string variable)
    {
      return long.Parse (GetString (variable));
    }

    /// <summary>
    /// Get a double value
    /// </summary>
    /// <param name="variable">variable</param>
    /// <returns></returns>
    public double GetDouble (string variable)
    {
      // No feedrate and no spindle speed in case of alarms
      if (Alarms.Count > 0 && (variable.Equals ("16:8|0|0") || variable.Equals ("16:10|0|0"))) {
        return 0;
      }

      return double.Parse (GetString (variable), new CultureInfo ("en-US"));
    }

    /// <summary>
    /// Get a boolean
    /// Format : "X:Y" or "X:"
    /// * X = GetString request
    /// * Y = bit number to extract a specific bit or an empty string to consider the whole string
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    public bool GetBool (string request)
    {
      // No RapidTraverse in case of alarms
      if (Alarms.Count > 0 && request == "16:29|0|0:0") {
        return false;
      }

      var splitRequest = request.Split (':');
      if (splitRequest.Length == 1 || splitRequest.Length > 4) {
        throw new Exception ($"DElectronEPS: Invalid format for the variable {request} in GetBool");
      }

      var bitNumberString = splitRequest.Last ();
      var stringRequest = string.Join (":", splitRequest.Take (splitRequest.Length - 1));
      var str = GetString (stringRequest);
      if (string.IsNullOrEmpty (bitNumberString)) {
        switch (str.ToLower ()) {
        case "1":
        case "true":
          return true;
        case "0":
        case "false":
          return false;
        default:
          throw new Exception ($"DElectronEPS: Value {str} of the request {request} is not a boolean");
        }
      }
      else {
        var bitNumber = int.Parse (bitNumberString);
        int value = int.Parse (str);
        return (value & (1 << bitNumber)) != 0;
      }
    }

    void InitializeAlarms ()
    {
      ESTOP = false;
      Alarms = new List<CncAlarm> ();

      var cncAlarm = (UInt32)GetInt ("2:ZZERCODE");
      var plcAlarm = (UInt32)GetInt ("2:2E");

      if (cncAlarm != 0) {
        Alarms.Add (new CncAlarm ("DElectronEPS", GetNCAlarmType (cncAlarm), "NC" + FormatNumber (cncAlarm)));
      }

      if (plcAlarm != 0) {
        var alarm = new CncAlarm ("DElectronEPS", "PLC", "MU" + FormatNumber (plcAlarm));
        if (plcAlarm == 1) {
          alarm.Message = "ESTOP";
          ESTOP = true;
        }
        Alarms.Add (alarm);
      }
    }

    string FormatNumber (uint number)
    {
      string ret = number.ToString ("X4");

      // At least 4 characters
      while (ret.Length < 4) {
        ret = "0" + ret;
      }

      // Limit to 4 characters (not always the case with X4, sometimes with "FFFF")
      if (ret.Length > 4) {
        ret = ret.Substring (ret.Length - 4);
      }

      return ret;
    }

    string GetNCAlarmType (UInt32 number)
    {
      string type = (number & 0x00ff).ToString ("X4");

      switch (number & 0x00ff) {
      case 0x02:
        type = "Initialization";
        break;
      case 0x07:
        type = "FlorenZ system";
        break;
      case 0x08:
        type = "Z-Star";
        break;
      case 0x09:
        type = "Link";
        break;
      case 0x10:
        type = "CMOS";
        break;
      case 0x12:
        type = "Axis detector";
        break;
      case 0x13:
        type = "Axis movement";
        break;
      case 0x14:
        type = "Machine programming and usage";
        break;
      }

      return type;
    }
    #endregion // Methods
  }
}
