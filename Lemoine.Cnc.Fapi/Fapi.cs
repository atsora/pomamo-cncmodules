// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Globalization;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Fidia input module
  /// </summary>
  public partial class Fapi: Lemoine.Cnc.BaseCncModule, Lemoine.Cnc.ICncModule, IDisposable
  {
    #region Members
    int m_connectionId = -1;
    bool m_connectionError = false;
    readonly FapiReader m_reader = new FapiReader();
    #endregion // Members
    
    #region Constructors / Destructor
    /// <summary>
    /// Constructor
    /// </summary>
    public Fapi() : base("Lemoine.Cnc.In.Fapi")
    {
      // Default values
      Port = FapiCorbaLib.FCL_DEFAULT_SERVER_PORT;
      ListCommandsInLog = false;
    }

    /// <summary>
    /// <see cref="IDisposable.Dispose" />
    /// </summary>
    public void Dispose()
    {
      Disconnect();
      GC.SuppressFinalize(this);
    }
    #endregion // Constructors / Destructor
    
    #region Parameters
    /// <summary>
    /// Hostname of the CNC
    /// </summary>
    public string Hostname { get; set; }
    
    /// <summary>
    /// Port number used to connect to the CNC
    /// Default is 2048
    /// </summary>
    public int Port { get; set; }
    
    /// <summary>
    /// If true, list all commands in logs (as fatal)
    /// => only for a debug reason or for completing the ILMA API documentation
    /// </summary>
    public bool ListCommandsInLog {
      get { return m_reader.ListCommandsInLog; }
      set { m_reader.ListCommandsInLog = value; }
    }
    #endregion // Parameters
    
    #region Connection / Disconnection
    /// <summary>
    /// Start method
    /// </summary>
    public bool Start()
    {
      log.Info("Fapi.Start");
      
      m_connectionError = false;
      CheckConnection();
      
      if (!m_connectionError)
        m_reader.Start();
      
      // The acquisition must go on so that we have all the parameters
      return true;
    }
    
    /// <summary>
    /// Check if the connection with the CNC is up. If not, connect to it
    /// </summary>
    /// <returns>The connection was successful</returns>
    public bool CheckConnection()
    {
      if (m_connectionError) {
        log.ErrorFormat("Fapi.CheckConnection - a connection error has already been set");
        return false;
      }
      
      if (m_reader.ConnectionRequired) {
        // Reader asks for a (re)connection
        Disconnect();
        
        log.Info("Fapi.CheckConnection - try to connect");
        
        // For any exception that might occur
        m_connectionError = true;
        
        // Connection to the machine
        var error = FapiCorbaLib.FapiConnect(Hostname, Port, ref m_connectionId);
        
        if (error == FapiError.FCL_RET_EXECUTED) {
          log.InfoFormat("Fapi.CheckConnection - successfully connected, ConnectionId = '{0}'", m_connectionId);
          m_reader.ConnectionId = m_connectionId;
        } else {
          m_connectionId = -1;
          string txt = "Fapi.CheckConnection - failed to connect";
          if (error == FapiError.FCL_RET_FAILED)
            txt += ", wrong IP?";
          else if (error == FapiError.FCL_RET_INVALID_LICENSE)
            txt += ", invalid license?";
          else
            txt += " (code: " + error + ")";
          log.Error(txt);
          
          return false;
        }
        
        // Callback for the messages
        try {
          var userData = new FapiCorbaLib.UserData();
          int listenerNumber = FapiCorbaLib.AddMessageListener(m_connectionId, MsgCallback, userData);
          if (listenerNumber <= 0)
            throw new Exception("FapiCorbaLib.AddMessageListener returned " + listenerNumber);
        } catch (Exception e) {
          log.ErrorFormat("Fapi.CheckConnection - couldn't initialize the message listener: {0}", e);
        }
      }
      
      // Everything is ok
      m_reader.ConnectionRequired = false;
      m_connectionError = false;
      
      return true;
    }
    
    void Disconnect()
    {
      if (m_connectionId != -1) {
        try {
          var result = FapiCorbaLib.FapiClose(m_connectionId);
          if (result != FapiError.FCL_RET_EXECUTED) {
            log.ErrorFormat("Fapi.Disconnect - couldn't disconnect: FapiError '{0}'", result);
            
            // Try to close all active connections
            FapiCorbaLib.FapiClose(FapiCorbaLib.FCL_INVALID_CONNECTION_ID);
          }
        } catch (Exception e) {
          log.ErrorFormat("Fapi.Disconnect - couldn't disconnect: {0}", e);
        }
      }
      
      m_connectionId = -1;
      m_reader.ConnectionId = -1;
      m_reader.ConnectionRequired = true;
    }
    
    /// <summary>
    /// Does a connection to the control end in error?
    /// </summary>
    public bool ConnectionError { get { return m_connectionError; } }
    #endregion // Connection / Disconnection
    
    #region Get simple values
    /// <summary>
    /// Get a string value
    /// </summary>
    /// <param name="param">objectName (FAPI object name) or objectName/param1</param>
    /// <param name="cache">Cache the value</param>
    /// <returns></returns>
    string GetString(string param, bool cache)
    {
      if (m_connectionError) {
        log.ErrorFormat("Fapi.GetString - connection error");
        throw new Exception("Fapi.GetString - connection error");
      }
      if (m_connectionId == -1) {
        log.ErrorFormat("Fapi.GetString - no connection");
        throw new Exception("Fapi.GetString - no connection");
      }
      
      // Get the value or throw an exception if not read yet
      string result = m_reader.GetValue(param, cache);
      log.DebugFormat("Fapi.GetString - the result of the command {0} is {1}", param, result);
      return result;
    }
    
    /// <summary>
    /// Get a string value
    /// </summary>
    /// <param name="param">objectName (FAPI object name) or objectName/param1</param>
    /// <returns></returns>
    public string GetString(string param)
    {
      return GetString(param, false);
    }
    
    /// <summary>
    /// Get a string parameter (which is cached)
    /// </summary>
    /// <param name="param">objectName (FAPI object name) or objectName/param1</param>
    /// <returns></returns>
    public string GetStringParameter(string param)
    {
      return GetString(param, true);
    }
    
    /// <summary>
    /// Get an int value
    /// </summary>
    /// <param name="param">objectName (FAPI object name) or objectName/param1</param>
    /// <returns></returns>
    public int GetInt(string param)
    {
      return int.Parse(this.GetString(param));
    }
    
    /// <summary>
    /// Get an int parameter (which is cached)
    /// </summary>
    /// <param name="param">objectName (FAPI object name) or objectName/param1</param>
    /// <returns></returns>
    public int GetIntParameter(string param)
    {
      return int.Parse(this.GetStringParameter(param));
    }
    
    /// <summary>
    /// Get a long value
    /// </summary>
    /// <param name="param">objectName (FAPI object name) or objectName/param1</param>
    /// <returns></returns>
    public long GetLong(string param)
    {
      return long.Parse(this.GetString(param));
    }
    
    /// <summary>
    /// Get a long parameter (which is cached)
    /// </summary>
    /// <param name="param">objectName (FAPI object name) or objectName/param1</param>
    /// <returns></returns>
    public long GetLongParameter(string param)
    {
      return long.Parse(this.GetStringParameter(param));
    }
    
    /// <summary>
    /// Get a double value
    /// </summary>
    /// <param name="param">objectName (FAPI object name) or objectName/param1</param>
    /// <returns></returns>
    public double GetDouble(string param)
    {
      return double.Parse(this.GetString(param), new CultureInfo("en-US"));
    }
    
    /// <summary>
    /// Get a double parameter (which is cached)
    /// </summary>
    /// <param name="param">objectName (FAPI object name) or objectName/param1</param>
    /// <returns></returns>
    public double GetDoubleParameter(string param)
    {
      return double.Parse(this.GetStringParameter(param), new CultureInfo("en-US"));
    }
    #endregion // Get simple values
    
    #region Get complex values
    /// <summary>
    /// Position (X, Y, Z and W)
    /// </summary>
    public Position Position {
      get {
        // Store keys, so that everything will be read
        m_reader.StoreKeys(new [] {
          "POSITION/XM",
          "POSITION/YM",
          "POSITION/ZM",
          "POSITION/WV"
        }, false, true);
        
        // Build a position and return it
        var position = new Position();
        position.X = GetDouble("POSITION/XM");
        position.Y = GetDouble("POSITION/YM");
        position.Z = GetDouble("POSITION/ZM");
        position.W = GetDouble("POSITION/WV");
        return position;
      }
    }
    
    /// <summary>
    /// Feedrate override
    /// </summary>
    public int FeedrateOverride {
      get {
        // Store keys, so that everything will be read
        m_reader.StoreKeys(new []{ "F", "FEED" }, false, true);
        
        // Get the programmed feed
        double programmedFeed = GetDouble("F");
        if (programmedFeed <= 0.1) {
          log.ErrorFormat("Fapi.FeedrateOverride - the programmedFeed={0} is too small " +
          "to get the feedrate override", programmedFeed);
          throw new Exception("Fapi.FeedrateOverride - too small programmed feedrate");
        }
        
        // Get the overriden feed (not the real feed "FREAL")
        double overridenFeed = GetDouble("FEED");
        
        // FEED=50000 looks to be an error
        if (Math.Abs(overridenFeed - 50000.0) <= 0.000001) {
          log.ErrorFormat("Fapi.FeedrateOverride - FEED=50000 returned => return an exception");
          throw new Exception("FEED=50000 error");
        }
        
        // Compute the feedrate override and return it
        int feedrateOverride = (int)(100.0 * overridenFeed / programmedFeed);
        log.DebugFormat("Fapi.FeedrateOverride - got {0} from overriddenFeed={1} and programmedFeed={2}",
          feedrateOverride, overridenFeed, programmedFeed);
        return feedrateOverride;
      }
    }
    
    /// <summary>
    /// Spindle speed override
    /// </summary>
    public int SpindleSpeedOverride {
      get {
        // Store keys, so that everything will be read
        m_reader.StoreKeys(new []{ "S", "SPDL" }, false, true);
        
        // Get the programmed spindle speed
        double programmedSpindleSpeed = GetDouble("S");
        if (programmedSpindleSpeed <= 0.1) {
          log.ErrorFormat("Fapi.SpindleSpeedOverride - the programmedSpindleSpeed={0} is too small " +
          "to get the spindle speed override", programmedSpindleSpeed);
          throw new Exception("Fapi.SpindleSpeedOverride - too small programmed spindle speed");
        }
        
        // Get the overriden spindle speed (not the real spindle speed)
        double overridenSpindleSpeed = GetDouble("SPDL");
        
        // Compute the spindle speed override and return it
        int spindleSpeedOverride = (int)(100.0 * overridenSpindleSpeed / programmedSpindleSpeed);
        log.DebugFormat("Fapi.SpindleSpeedOverride - got {0} from overriddenSpindleSpeed={1} and programmedSpindleSpeed={2}",
          spindleSpeedOverride, overridenSpindleSpeed, programmedSpindleSpeed);
        return spindleSpeedOverride;
      }
    }
    
    /// <summary>
    /// Is the CNC executing a block ?
    /// Set to true if the CNB mode of the CNC is 9 (very busy)
    /// </summary>
    public bool ExecutingBlock {
      get { return (GetInt("CNBMODE") == 9); }
    }
    
    /// <summary>
    /// Block number
    /// </summary>
    public int BlockNumber {
      get {
        // Store keys, so that everything will be read
        m_reader.StoreKeys(new []{ "CNBMODE", "N" }, false, true);
        
        if (!ExecutingBlock) {
          log.Error("Fapi.BlockNumber - the CNC is not executing a block " +
          "=> could not get the block number");
          throw new Exception("Fapi.BlockNumber - BlockNumber unknown if not executing block");
        }
        
        // Extract the block number
        string v = GetString("N");
        return v.StartsWith("N", StringComparison.InvariantCultureIgnoreCase) ?
          int.Parse(v.Substring(1)) : int.Parse(v);
      }
    }
    #endregion // Get complex values
  }
}
