// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

using Lemoine.Core.Log;
using omg.org.CosNaming;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Description of Corba.
  /// </summary>
  public sealed class Corba: Lemoine.Cnc.BaseCncModule, Lemoine.Cnc.ICncModule, IDisposable
  {
    static readonly int RESET_CHANNEL_FREQUENCY = 10;
    
    #region Members
    NameComponent m_namingContext;
    NameComponent m_objectReference;
    string m_connectionParameter;
    string m_requiredProcesses = "";
    
    int m_successSleepTime = 0;
    int m_errorSleepTime = 0;
    
    bool m_isConnected = false;
    CorbaModule.Cnc m_corbaObject;
    bool m_useWideStrings = false;
    bool m_useStart = false;
    bool m_useFinish = false;
    bool m_startOk = true;
    
    int m_corbaErrorCounter = 0;
    #endregion

    #region Getters / Setters
    /// <summary>
    /// Corba naming context
    /// 
    /// This syntax of the naming context is one of the following:
    /// <item>id(kind)</item>
    /// <item>id:kind</item>
    /// <item>id.kind</item>
    /// <item>idsameaskind</item>
    /// </summary>
    public string NamingContext {
      get { return String.Format ("{0}({1})",
                                  m_namingContext.id,
                                  m_namingContext.kind); }
      set
      {
        string[] values = value.Split (new char [] {'(', ')', '/', ':', '.'});
        if (values.Length > 1) {
          log.DebugFormat ("NamingContext.set: " +
                           "set id={0}, kind={1} from {2}",
                           values[0], values[1], value);
          m_namingContext = new NameComponent (values [0], values [1]);
        }
        else {
          log.DebugFormat ("NamingContext.set: " +
                           "only one value, use id=kind={0}",
                           value);
          m_namingContext = new NameComponent (value, value);
        }
      }
    }
    
    /// <summary>
    /// Corba object reference
    /// 
    /// This syntax of the object reference is one of the following:
    /// <item>id(kind)</item>
    /// <item>id:kind</item>
    /// <item>id.kind</item>
    /// <item>idsameaskind</item>
    /// </summary>
    public string ObjectReference {
      get { return String.Format ("{0}({1})",
                                  m_objectReference.id,
                                  m_objectReference.kind); }
      set
      {
        string[] values = value.Split (new char [] {'(', ')', '/', ':', '.'});
        if (values.Length > 1) {
          log.DebugFormat ("NamingContext.set: " +
                           "set id={0}, kind={1} from {2}",
                           values[0], values[1], value);
          m_objectReference = new NameComponent (values [0], values [1]);
        }
        else {
          log.DebugFormat ("NamingContext.set: " +
                           "only one value, use id=kind={0}",
                           value);
          m_objectReference = new NameComponent (value, value);
        }
      }
    }
    
    
    /// <summary>
    /// Connection parameter used in the machine (or Corba server)
    /// and to send to it
    /// </summary>
    public string ConnectionParameter {
      get { return m_connectionParameter; }
      set { m_connectionParameter = value; }
    }
    
    /// <summary>
    /// Required processes for the remote CORBA server.
    /// 
    /// The first character is the separator used to separate
    /// the different required processes.
    /// 
    /// Example:
    /// <item>/maschine.exe</item>
    /// <item>/maschine.exe/OpcEnum.exe</item>
    /// </summary>
    public string RequiredProcesses {
      get { return m_requiredProcesses; }
      set { m_requiredProcesses = value; }
    }
    
    /// <summary>
    /// Sleep time in ms after a method is successful.
    /// 
    /// Default is 0 ms.
    /// </summary>
    public int SuccessSleepTime {
      get { return m_successSleepTime; }
      set { m_successSleepTime = value; }
    }
    
    /// <summary>
    /// Sleep time in ms after a method is in error
    /// 
    /// Default is 0 ms.
    /// </summary>
    public int ErrorSleepTime {
      get { return m_errorSleepTime; }
      set { m_errorSleepTime = value; }
    }
    
    /// <summary>
    /// Use wide strings in the server
    /// </summary>
    public bool UseWideStrings {
      get { return m_useWideStrings; }
      set { m_useWideStrings = value; }
    }
    
    /// <summary>
    /// Call the Start remote method at start (default is false)
    /// </summary>
    public bool UseStart {
      get { return m_useStart; }
      set { m_useStart = value; }
    }

    /// <summary>
    /// Call the Finish remote method (default is false)
    /// </summary>
    public bool UseFinish {
      get { return m_useFinish; }
      set { m_useFinish = value; }
    }
    
    /// <summary>
    /// Corba Connection error ?
    /// </summary>
    public bool ConnectionError {
      get { return !m_isConnected; }
    }

    /// <summary>
    /// Remote requirements error ?
    /// </summary>
    public bool RemoteRequirementsError {
      get
      {
        bool result = false;
        try {
          result = m_corbaObject.GetRequirementsError ();
        }
        catch (Exception ex) {
          log.ErrorFormat ("RemoteRequirementsError: " +
                           "corbaObject raised {0}" +
                           "=> disconnect and reset the CORBA channel",
                           ex);
          m_isConnected = false;
          ManageCorbaError ();
          throw;
        }
        log.ErrorFormat ("RemoteRequirementsError.get: " +
                         "GetRequirementsError returned {0}",
                         result);
        return result;
      }
    }

    /// <summary>
    /// Remote connection error ?
    /// </summary>
    public bool RemoteConnectionError {
      get
      {
        bool result = false;
        try {
          result = m_corbaObject.GetConnectionError ();
        }
        catch (Exception ex) {
          log.ErrorFormat ("RemoteConnectionError: " +
                           "corbaObject raised {0}" +
                           "=> disconnect and reset the CORBA channel",
                           ex);
          m_isConnected = false;
          ManageCorbaError ();
          throw;
        }
        log.ErrorFormat ("RemoteConnectionError.get: " +
                         "GetConnectionError returned {0}",
                         result);
        return result;
      }
    }
    #endregion

    #region Constructors / Destructor / ToString methods
    /// <summary>
    /// Description of the constructor
    /// </summary>
    public Corba ()
      : base("Lemoine.Cnc.In.Corba")
    {
    }
    
    /// <summary>
    /// <see cref="IDisposable.Dispose" />
    /// </summary>
    public void Dispose ()
    {
      try {
        bool success = m_corbaObject.Close ();
        if (!success) {
          log.ErrorFormat ("Dispose: " +
                           "remote method Close returned false ");
        }
      }
      catch (Exception ex) {
        log.Error ("Dispose: " +
                   "corbaObject.Close failed with {0}",
                   ex);
      }
      finally {
        GC.SuppressFinalize (this);
      }
    }
    #endregion

    #region Methods
    /// <summary>
    /// Start method
    /// </summary>
    public void Start ()
    {
      if (!m_isConnected) {
        // - Get the Corba object
        try {
          m_corbaObject =
            (CorbaModule.Cnc) Lemoine.CorbaHelper.CorbaClientConnection.GetObject (m_namingContext,
                                                                                   m_objectReference);
        }
        catch (Exception ex) {
          log.ErrorFormat ("Start: " +
                           "resolving {0}/{1} failed with {2}",
                           m_namingContext, m_objectReference, ex);
          ManageCorbaError ();
          throw;
        }
        if (null == m_corbaObject) {
          log.ErrorFormat ("Start: " +
                           "Corba object {0}/{1} could not be initialized",
                           m_namingContext, m_objectReference);
          return;
        }
        
        // - Set the remote connection parameters
        try {
          if (m_connectionParameter.Length > 0) {
            bool result;
            if (m_useWideStrings) {
              result = m_corbaObject.SetConnectionParameter2 (m_connectionParameter);
            }
            else {
              result = m_corbaObject.SetConnectionParameter (m_connectionParameter);
            }
            if (!result) {
              log.ErrorFormat ("Start: " +
                               "SetConnectionParameter failed in the server");
              return;
            }
          }
          
          // - Check if some processes are required there
          if (m_requiredProcesses.Length > 0) {
            bool result;
            if (m_useWideStrings) {
              result = m_corbaObject.SetRequiredProcesses2 (m_requiredProcesses);
            }
            else {
              result = m_corbaObject.SetRequiredProcesses (m_requiredProcesses);
            }
            if (!result) {
              log.Error ("Start: SetRequiredProcesses failed in the server");
              return;
            }
          }
        }
        catch (Exception ex) {
          log.Error ("Start: corbaObject for parameters exception => disconnect and reset the CORBA channel", ex);
          ManageCorbaError ();
          throw;
        }
        m_isConnected = true;
      }
      
      // - If applicable, run the remote Start method
      if (m_useStart) {
        try {
          m_startOk = m_corbaObject.Start ();
        }
        catch (Exception ex) {
          log.Error ("Start: corbaObject for Start exception => disconnect and reset the CORBA channel", ex);
          m_isConnected = false;
          m_startOk = false;
          ManageCorbaError ();
        }
        if (!m_startOk) {
          log.ErrorFormat ("Start: Start failed in the server");
        }
      }
    }
    
    /// <summary>
    /// Finish method
    /// </summary>
    public void Finish ()
    {
      if (m_useFinish) {
        // - Corba status check
        if (!m_isConnected) {
          log.Info ("Finish: " +
                    "not connected => skip the finish part");
          return;
        }
        if (!m_startOk) {
          log.Info ("Finish: " +
                    "start was not ok => skip the finish part");
          return;
        }
        
        if (null == m_corbaObject) {
          log.Fatal ("Finish: " +
                     "the corba object is null although start was ok");
          Debug.Assert (false);
          return;
        }
        
        // - Finish method
        bool result = false;
        try {
          result = m_corbaObject.Finish ();
        }
        catch (Exception ex) {
          log.Error ("Finish: corbaObject for Finish exception => disconnect and reset the CORBA channel", ex);
          m_isConnected = false;
          ManageCorbaError ();
        }
        if (!result) {
          log.ErrorFormat ("Finish: " +
                           "Finish failed in the server");
        }
      }
    }
    
    /// <summary>
    /// Get a string value
    /// </summary>
    /// <param name="param">parameter</param>
    /// <returns></returns>
    public string GetString (string param)
    {
      if (!m_isConnected) {
        log.Error ("GetString: " +
                   "not connected");
        throw new Exception ("Not connected");
      }
      if (!m_startOk) {
        log.Error ("GetString: " +
                   "start was not ok");
        throw new Exception ("Start failed");
      }
      
      string result = "";
      bool success;
      try {
        if (m_useWideStrings) {
          success = m_corbaObject.GetString2 (param, out result);
        }
        else {
          success = m_corbaObject.GetString (param, out result);
        }
      }
      catch (Exception ex) {
        log.Error ("GetString: corbaObject.GetString failed => disconnect and reset the CORBA channel", ex);
        m_isConnected = false;
        ManageCorbaError ();
        throw;
      }
      if (!success) {
        log.Error ("GetString: remote method GetString returned false (not a connection problem, do not disconnect)");
        if (m_successSleepTime > 0) {
          Thread.Sleep (m_successSleepTime);
        }
        throw new Exception ("Remote GetString failed");
      }
      
      log.DebugFormat ("GetString: " +
                       "remote method GetString returned {0}",
                       result);
      if (m_successSleepTime > 0) {
        Thread.Sleep (m_successSleepTime);
      }
      return result;
    }

    /// <summary>
    /// Get a string value from a wstring
    /// </summary>
    /// <param name="param">parameter</param>
    /// <returns></returns>
    public string GetWString (string param)
    {
      if (!m_isConnected) {
        log.Error ("GetWString: " +
                   "not connected");
        throw new Exception ("Not connected");
      }
      if (!m_startOk) {
        log.Error ("GetWString: " +
                   "start was not ok");
        throw new Exception ("Start failed");
      }
      
      string result = "";
      bool success;
      try {
        if (m_useWideStrings) {
          success = m_corbaObject.GetWString2 (param, out result);
        }
        else {
          success = m_corbaObject.GetWString (param, out result);
        }
      }
      catch (Exception ex) {
        log.Error ("GetWString: corbaObject.GetWString failed with => disconnect and reset the CORBA channel", ex);
        m_isConnected = false;
        ManageCorbaError ();
        throw;
      }
      if (!success) {
        log.ErrorFormat ("GetWString: " +
                         "remote method GetWString returned false " +
                         "(not a connection problem, do not disconnect)");
        if (m_successSleepTime > 0) {
          Thread.Sleep (m_successSleepTime);
        }
        throw new Exception ("Remote GetWString failed");
      }
      
      log.DebugFormat ("GetWString: " +
                       "remote method GetString returned {0}",
                       result);
      if (m_successSleepTime > 0) {
        Thread.Sleep (m_successSleepTime);
      }
      return result;
    }

    /// <summary>
    /// Get an int value
    /// </summary>
    /// <param name="param">parameter</param>
    /// <returns></returns>
    public int GetInt (string param)
    {
      if (!m_isConnected) {
        log.Error ("GetInt: " +
                   "not connected");
        throw new Exception ("Not connected");
      }
      if (!m_startOk) {
        log.Error ("GetInt: " +
                   "start was not ok");
        throw new Exception ("Start failed");
      }
      
      int result = 0;
      bool success;
      try {
        if (m_useWideStrings) {
          success = m_corbaObject.GetInt2 (param, out result);
        }
        else {
          success = m_corbaObject.GetInt (param, out result);
        }
      }
      catch (Exception ex) {
        log.ErrorFormat ("GetInt: " +
                         "corbaObject.GetInt failed with {0} " +
                         "=> disconnect and reset the CORBA channel",
                         ex);
        m_isConnected = false;
        ManageCorbaError ();
        throw;
      }
      if (!success) {
        log.ErrorFormat ("GetInt: " +
                         "remote method GetInt returned false " +
                         "(not a connection problem, do not disconnect)");
        if (m_successSleepTime > 0) {
          Thread.Sleep (m_successSleepTime);
        }
        throw new Exception ("Remote GetInt failed");
      }
      
      log.DebugFormat ("GetInt: " +
                       "remote method GetInt returned {0}",
                       result);
      if (m_successSleepTime > 0) {
        Thread.Sleep (m_successSleepTime);
      }
      return result;
    }

    /// <summary>
    /// Get a long value
    /// </summary>
    /// <param name="param">parameter</param>
    /// <returns></returns>
    public long GetLong (string param)
    {
      if (!m_isConnected) {
        log.Error ("GetLong: not connected");
        throw new Exception ("Not connected");
      }
      if (!m_startOk) {
        log.Error ("GetLong: start was not ok");
        throw new Exception ("Start failed");
      }
      
      long result = 0;
      bool success;
      try {
        if (m_useWideStrings) {
          success = m_corbaObject.GetLong2 (param, out result);
        }
        else {
          success = m_corbaObject.GetLong (param, out result);
        }
      }
      catch (Exception ex) {
        log.Error ("GetLong: corbaObject.GetLong failed => disconnect and reset the CORBA channel", ex);
        m_isConnected = false;
        ManageCorbaError ();
        throw;
      }
      if (!success) {
        log.Error ("GetLong: remote method GetLong returned false (not a connection problem, do not disconnect)");
        if (m_successSleepTime > 0) {
          Thread.Sleep (m_successSleepTime);
        }
        throw new Exception ("Remote GetLong failed");
      }
      
      log.DebugFormat ("GetLong: " +
                       "remote method GetLong returned {0}",
                       result);
      if (m_successSleepTime > 0) {
        Thread.Sleep (m_successSleepTime);
      }
      return result;
    }
    
    /// <summary>
    /// Get a double value
    /// </summary>
    /// <param name="param">parameter</param>
    /// <returns></returns>
    public double GetDouble (string param)
    {
      if (!m_isConnected) {
        log.Error ("GetDouble: " +
                   "not connected");
        throw new Exception ("Not connected");
      }
      if (!m_startOk) {
        log.Error ("GetDouble: " +
                   "start was not ok");
        throw new Exception ("Start failed");
      }
      
      double result = 0.0;
      bool success;
      try {
        if (m_useWideStrings) {
          success = m_corbaObject.GetDouble2 (param, out result);
        }
        else {
          success = m_corbaObject.GetDouble (param, out result);
        }
      }
      catch (Exception ex) {
        log.Error ("GetDouble: corbaObject.GetDouble failed => disconnect and reset the CORBA channel",
                         ex);
        m_isConnected = false;
        ManageCorbaError ();
        throw;
      }
      if (!success) {
        log.ErrorFormat ("GetDouble: " +
                         "remote method GetDouble returned false " +
                         "(not a connection problem, do not disconnect)");
        if (m_successSleepTime > 0) {
          Thread.Sleep (m_successSleepTime);
        }
        throw new Exception ("Remote GetDouble failed");
      }
      
      log.DebugFormat ("GetDouble: " +
                       "remote method GetDouble returned {0}",
                       result);
      if (m_successSleepTime > 0) {
        Thread.Sleep (m_successSleepTime);
      }
      return result;
    }
    
    
    /// <summary>
    /// Get a bool value from an integer
    /// <item>1: True</item>
    /// <item>0: False</item>
    /// </summary>
    /// <param name="param">parameter</param>
    /// <returns></returns>
    public bool GetBoolFromInt (string param)
    {
      return (1 == GetInt (param));
    }

    /// <summary>
    /// Get a set of values for the keys in a list string
    /// </summary>
    /// <param name="param">separator, template with {0}, list string of values to get</param>
    /// <returns></returns>
    public IDictionary<string, double> GetDoubleSet (string param) => GetSet (param, GetDouble);

    /// <summary>
    /// Get a set of values for the keys in a list string
    /// </summary>
    /// <param name="param">separator, template with {0}, list string of values to get</param>
    /// <returns></returns>
    IDictionary<string, T> GetSet<T> (string param, Func<string, T> get)
    {
      var p = Lemoine.Collections.EnumerableString.ParseListString (param, 2);
      var template = p[0];
      try {
        var keys = Lemoine.Collections.EnumerableString.ParseListString (p[1]);
        return GetEnumerable<T> (template, keys, get)
          .ToDictionary (x => x.Key, x => x.Value);
      }
      catch (Exception ex) {
        log.Error ($"GetSet: param={param}", ex);
        throw;
      }
    }

    IEnumerable<KeyValuePair<string, T>> GetEnumerable<T> (string template, IEnumerable<string> keys, Func<string, T> get)
    {
      foreach (var key in keys) {
        T v;
        try {
          v = get (string.Format (template, key));
        }
        catch (Exception ex) {
          log.Error ($"GetEnumerable: exception for key={key}", ex);
          continue;
        }
        yield return new KeyValuePair<string, T> (key, v);
      }
    }

    void ManageCorbaError ()
    {
      ++m_corbaErrorCounter;
      if (0 == (m_corbaErrorCounter % RESET_CHANNEL_FREQUENCY)) { // Reset the channel only every 10 Corba errors
        log.WarnFormat ("ManageCorbaError: " +
                        "reset the channel, corba error counter = {0}",
                        m_corbaErrorCounter);
        Lemoine.CorbaHelper.CorbaClientConnection.ResetChannel ();
      }
      if (m_errorSleepTime > 0) {
        log.InfoFormat ("ManageCorbaError: " +
                        "about to sleep {0} ms",
                        m_errorSleepTime);
        Thread.Sleep (m_errorSleepTime);
      }
    }
    #endregion
  }
}
