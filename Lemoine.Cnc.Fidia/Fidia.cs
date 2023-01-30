// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Remoting;
using System.Text;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Fidia input module
  /// </summary>
  public class Fidia : Lemoine.Cnc.BaseCncModule, Lemoine.Cnc.ICncModule, IDisposable
  {
    static readonly int DEFAULT_PORT = 2048;
    static readonly int DEFAULT_TIMEOUT = 10;
    static readonly int DEFAULT_TCP_RETRY_SLEEP = 5000;

    /// <summary>
    /// FIDIA returned status
    /// </summary>
    enum RS
    {
      /// <summary>
      /// Accepted
      /// </summary>
      ACCEPTED = 0,
      /// <summary>
      /// Executed
      /// </summary>
      EXECUTED = 1,
      /// <summary>
      /// Refused
      /// </summary>
      REFUSED = 2,
      /// <summary>
      /// Aborted
      /// </summary>
      ABORTED = 3,
      /// <summary>
      /// Timeout
      /// </summary>
      TIMEOUT = 4
    };

    #region Members
    string m_hostname;
    int m_port = DEFAULT_PORT;
    int m_tcpRetrySleep = DEFAULT_TCP_RETRY_SLEEP;
    int m_timeOut = DEFAULT_TIMEOUT;

    bool m_connected = false;
    bool m_connectionError = false;
    DateTime? m_lastTcpConnectionError = null;
    CorbaServer m_corbaServer = null;
    bool m_executingBlockKnown = false;
    bool m_executingBlock = false;
    IDictionary<string, string> m_cache = new Dictionary<string, string> ();
    #endregion // Members

    #region Getters / Setters
    /// <summary>
    /// Hostname of the CNC
    /// </summary>
    public string Hostname
    {
      get { return m_hostname; }
      set { m_hostname = value; }
    }

    /// <summary>
    /// Port number used to connect to the CNC.
    /// Default is 2048.
    /// </summary>
    public int Port
    {
      get { return m_port; }
      set { m_port = value; }
    }

    /// <summary>
    /// The connection parameter can be one of the following values:
    /// <entry>host</entry>
    /// <entry>host:port</entry>
    /// where:
    /// <entry>host is either an IP address of a host name</entry>
    /// <entry>port is the used port number</entry>
    /// 
    /// In case the port number is not given, the default port number 2048 is used.
    /// </summary>
    public string ConnectionParameter
    {
      get
      {
        return String.Format ("{0}:{1}",
                              m_hostname,
                              m_port);
      }
      set
      {
        string[] ethernetParameters = value.Split (':');
        if (ethernetParameters.Length > 1) {
          int localPort;
          if (!int.TryParse (ethernetParameters[1], out localPort)) {
            log.WarnFormat ("ConnectionParameter.set: " +
                            "parsing the connection parameter {0} " +
                            "to retrieve the port number failed",
                            value);
          }
          else {
            m_port = localPort;
          }
          m_hostname = ethernetParameters[0];
          log.DebugFormat ("ConnectionParameter.set: " +
                           "use address={0} port={1}",
                           m_hostname, m_port);
        }
        else { // Only hostname
          m_hostname = value;
          log.DebugFormat ("ConnectionParameter.set: " +
                           "use address={0} and default port={1}",
                           m_hostname, m_port);
        }
      }
    }

    /// <summary>
    /// Time to sleep after a TCP connection failed
    /// (too many TCP fail connections may drive FapiCorbaServer to a time out
    ///  because Windows messages are not processed so fast)
    /// </summary>
    public int TcpRetrySleep
    {
      get { return m_tcpRetrySleep; }
      set { m_tcpRetrySleep = value; }
    }

    /// <summary>
    /// Time out for the requests in seconds.
    /// 
    /// Default is 10 s.
    /// </summary>
    public int TimeOut
    {
      get { return m_timeOut; }
      set { m_timeOut = value; }
    }

    /// <summary>
    /// Does a connection to the control end in error ?
    /// </summary>
    public bool ConnectionError
    {
      get
      {
        if (m_connectionError) {
          log.DebugFormat ("Connection.Error: " +
                           "a connection error was set");
          return true;
        }
        bool result = CheckConnection ();
        log.DebugFormat ("ConnectionError.get: " +
                         "connection result is {0}",
                         result);
        return !result;
      }
    }

    /// <summary>
    /// Position (X, Y, Z and W)
    /// </summary>
    public Position Position
    {
      get
      {
        if (false == CheckConnection ()) {
          log.ErrorFormat ("Position.get: " +
                           "connection to the CNC failed");
          throw new Exception ("No CNC connection");
        }

        var position = new Position ();
        const int nbElements = 4;
        const string objectName = "POSITION";
        var fidiaParameters = new string[nbElements];
        fidiaParameters[0] = "XM";
        fidiaParameters[1] = "YM";
        fidiaParameters[2] = "ZM";
        fidiaParameters[3] = "WV";
        DataElemContainer[] results;
        RS status;
        try {
          status = (RS)m_corbaServer.ObjectRead (objectName,
                                                  fidiaParameters,
                                                  out results,
                                                  m_timeOut);
        }
        catch (System.Net.Sockets.SocketException ex) {
          log.ErrorFormat ("Position.get: " +
                           "socket exception {0} " +
                           "=> set a connection error",
                           ex);
          m_connectionError = true;
          m_connected = false;
          throw;
        }
        catch (omg.org.CORBA.TRANSIENT ex) {
          log.ErrorFormat ("Position.get: " +
                           "ObjectRead threw TRANSIENT {0} " +
                           "=> disconnect",
                           ex);
          m_connected = false;
          throw;
        }
        catch (Exception ex) {
          log.ErrorFormat ("Position.get: " +
                           "ObjectRead threw {0} " +
                           "=> disconnect",
                           ex);
          m_connected = false;
          throw;
        }
        switch (status) {
          case RS.ACCEPTED:
          case RS.EXECUTED:
            log.DebugFormat ("Position.get: " +
                             "ObjectRead returned a good status {0}",
                             status);
            break;
          case RS.REFUSED:
          case RS.ABORTED:
            log.ErrorFormat ("Position.get: " +
                             "ObjectRead returned a bad status {0}",
                             status);
            throw new Exception (status.ToString ());
          case RS.TIMEOUT: // => Disconnect
            m_connected = false;
            log.ErrorFormat ("Position.get: " +
                             "ObjectRead returned TIMEOUT {0}",
                             status);
            throw new Exception (status.ToString ());
        }
        System.Diagnostics.Debug.Assert (nbElements == results.Length);
        if (results.Length < nbElements) {
          log.ErrorFormat ("Position.get: " +
                           "not enough results (array length is {0})",
                           results.Length);
          throw new Exception ("Empty results array");
        }
        if (results.Length > nbElements) {
          log.WarnFormat ("Position.get: " +
                          "more than {0} elements in results array (length={0})",
                          nbElements, results.Length);
        }
        for (int i = 0; i < results.Length; ++i) {
          log.InfoFormat ("Position.get: " +
                          "got name={0} value={1}",
                          results[0].name, results[0].value);
          double doubleValue = double.Parse (results[i].value);
          if (results[i].name.Equals ("XM")) {
            position.X = doubleValue;
          }
          else if (results[i].name.Equals ("YM")) {
            position.Y = doubleValue;
          }
          else if (results[i].name.Equals ("ZM")) {
            position.Z = doubleValue;
          }
          else if (results[i].name.Equals ("WV")) {
            position.W = doubleValue;
          }
        }

        log.DebugFormat ("Position.get: " +
                         "return {0}",
                         position);
        return position;
      }
    }

    /// <summary>
    /// Feedrate override
    /// </summary>
    public int FeedrateOverride
    {
      get
      {
        double programmedFeed = GetDouble ("F");
        if (programmedFeed <= 0.1) {
          log.ErrorFormat ("FeedrateOverride.get: " +
                           "the programmedFeed={0} is too small to get the feedrate override",
                           programmedFeed);
          throw new Exception ("Too small programmed feedrate");
        }
        double overriddenFeed = GetDouble ("FEED"); // this is not the real feed "FREAL"
        const double feedError = 50000.0; // FEED=50000 looks to be an error
        if ((feedError - 0.000001 <= overriddenFeed) && (overriddenFeed <= feedError + 0.000001)) {
          log.ErrorFormat ("FeedrateOverride.get: " +
                           "FEED=50000 returned => return an exception");
          throw new Exception ("FEED=50000 error");
        }
        int feedrateOverride = (int)(100.0 * overriddenFeed / programmedFeed);
        log.DebugFormat ("FeedrateOverride.get: " +
                         "got {0} from overriddenFeed={1} and programmedFeed={2}",
                         feedrateOverride, overriddenFeed, programmedFeed);
        return feedrateOverride;
      }
    }

    /// <summary>
    /// Spindle speed override
    /// </summary>
    public int SpindleSpeedOverride
    {
      get
      {
        double programmedSpindleSpeed = GetDouble ("S");
        if (programmedSpindleSpeed <= 0.1) {
          log.ErrorFormat ("SpindleSpeedOverride.get: " +
                           "the programmedSpindleSpeed={0} is too small " +
                           "to get the spindle speed override",
                           programmedSpindleSpeed);
          throw new Exception ("Too small programmed spindle speed");
        }
        double overriddenSpindleSpeed = GetDouble ("SPDL"); // this is not the real spindle speed
        int spindleSpeedOverride = (int)(100.0 * overriddenSpindleSpeed / programmedSpindleSpeed);
        log.DebugFormat ("SpindleSpeedOverride.get: " +
                         "got {0} from overriddenSpindleSpeed={1} and programmedSpindleSpeed={2}",
                         spindleSpeedOverride, overriddenSpindleSpeed, programmedSpindleSpeed);
        return spindleSpeedOverride;
      }
    }

    /// <summary>
    /// Is the CNC executing a block ?
    /// 
    /// Set to true if the CNB mode of the CNC is 9 (very busy)
    /// </summary>
    public bool ExecutingBlock
    {
      get
      {
        // keep the value in cache so that it could be used
        // also for BlockNumber
        if (m_executingBlockKnown) {
          log.DebugFormat ("ExecutingBlock.get: " +
                           "get executingBlock={0} from cache",
                           m_executingBlock);
          return m_executingBlock;
        }

        int cnbMode = GetInt ("CNBMODE");
        log.DebugFormat ("ExecutingBlock.get: " +
                         "got CNB mode={0}",
                         cnbMode);
        m_executingBlock = (9 == cnbMode);
        m_executingBlockKnown = true;
        return m_executingBlock;
      }
    }

    /// <summary>
    /// Block number
    /// </summary>
    public int BlockNumber
    {
      get
      {
        if (false == ExecutingBlock) {
          log.Error ("BlockNumber.get: " +
                     "the CNC is not executing a block " +
                     "=> could not get the block number");
          throw new Exception ("BlockNumber unknown if not executing block");
        }
        string v = GetString ("N");
        log.DebugFormat ("BlockNumber.get: " +
                         "got {0} for objectName=N",
                         v);
        if (v.StartsWith ("N", StringComparison.InvariantCultureIgnoreCase)) {
          return int.Parse (v.Substring (1));
        }
        else {
          return int.Parse (v);
        }
      }
    }
    #endregion // Getters / Setters

    #region Constructors / Destructor / ToString methods
    /// <summary>
    /// Constructor
    /// </summary>
    public Fidia ()
      : base ("Lemoine.Cnc.In.Fidia")
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
    #endregion

    #region Methods
    /// <summary>
    /// Start method
    /// </summary>
    public void Start ()
    {
      m_connectionError = false;
      m_executingBlockKnown = false;
    }

    /// <summary>
    /// Get a string value
    /// </summary>
    /// <param name="param">objectName (FIDIA object name) or objectName/param1</param>
    /// <returns></returns>
    public string GetString (string param)
    {
      return GetString (param, false);
    }

    /// <summary>
    /// Get a string parameter (which is cached)
    /// </summary>
    /// <param name="param">objectName (FIDIA object name) or objectName/param1</param>
    /// <returns></returns>
    public string GetStringParameter (string param)
    {
      return GetString (param, true);
    }

    /// <summary>
    /// Get a string value
    /// </summary>
    /// <param name="param">objectName (FIDIA object name) or objectName/param1</param>
    /// <param name="cache">Cache the value</param>
    /// <returns></returns>
    string GetString (string param, bool cache)
    {
      if (false == CheckConnection ()) {
        log.ErrorFormat ("GetStringValue: " +
                         "connection to the CNC failed");
        throw new Exception ("No CNC connection");
      }

      if (cache) {
        string v;
        if (m_cache.TryGetValue (param, out v)) {
          log.DebugFormat ("GetStringValue: " +
                           "get the value {0} from cache for param {1}",
                           v, param);
          return v;
        }
      }

      string[] parameters = param.Split ('/');
      string objectName;
      string[] fidiaParameters = new string[1];
      if (parameters.Length > 1) { // objectName and param1
        objectName = parameters[0];
        fidiaParameters[0] = parameters[1];
      }
      else { // only objectName
        objectName = param;
        fidiaParameters[0] = "";
      }
      DataElemContainer[] results;
      RS status;
      try {
        status = (RS)m_corbaServer.ObjectRead (objectName,
                                                fidiaParameters,
                                                out results,
                                                m_timeOut);
      }
      catch (System.Net.Sockets.SocketException ex) {
        log.Error ($"GetString: socket exception => set a connection error", ex);
        m_connectionError = true;
        m_connected = false;
        throw;
      }
      catch (omg.org.CORBA.TRANSIENT ex) {
        log.Error ($"GetString: ObjectRead threw TRANSIENT => disconnect", ex);
        m_connected = false;
        throw;
      }
      catch (Exception ex) {
        log.Error ("GetString: ObjectRead threw an exception => disconnect", ex);
        m_connected = false;
        throw;
      }
      switch (status) {
        case RS.ACCEPTED:
        case RS.EXECUTED:
          log.DebugFormat ("GetString: " +
                           "ObjectRead returned a good status {0} " +
                           "for param {1}",
                           status, param);
          break;
        case RS.REFUSED:
        case RS.ABORTED:
          log.ErrorFormat ("GetString: " +
                           "ObjectRead returned a bad status {0} " +
                           "for param {1}",
                           status, param);
          throw new Exception (status.ToString ());
        case RS.TIMEOUT: // => Disconnect
          m_connected = false;
          log.ErrorFormat ("GetString: " +
                           "ObjectRead returned TIMEOUT {0} " +
                           "for param {1}",
                           status, param);
          throw new Exception (status.ToString ());
      }
      System.Diagnostics.Debug.Assert (1 == results.Length);
      if (results.Length < 1) {
        log.ErrorFormat ("GetString: " +
                         "empty results array (length is {0}) " +
                         "for param {1}",
                         results.Length, param);
        throw new Exception ("Empty results array");
      }
      if (results.Length != 1) {
        log.WarnFormat ("GetString: " +
                        "more than one element in results array (length={0}) " +
                        "for param {1}",
                        results.Length, param);
      }
      log.InfoFormat ("GetString: " +
                      "got name={0} value={1} for param={2}",
                      results[0].name, results[0].value,
                      param);
      System.Diagnostics.Debug.Assert (results[0].name.Equals (fidiaParameters[0]));
      if (!results[0].name.Equals (fidiaParameters[0])) {
        log.WarnFormat ("GetString: " +
                        "retrieved name {0} is not equal the given parameter {1}",
                        results[0].name, fidiaParameters[0]);
      }

      if (cache) {
        log.DebugFormat ("GetString: " +
                         "cache value {0} for param {1}",
                         results[0].value, param);
        m_cache[param] = results[0].value;
      }

      return results[0].value;
    }

    /// <summary>
    /// Get an int value
    /// </summary>
    /// <param name="param">objectName (FIDIA object name) or objectName/param1</param>
    /// <returns></returns>
    public int GetInt (string param)
    {
      return int.Parse (this.GetString (param));
    }

    /// <summary>
    /// Get an int parameter (which is cached)
    /// </summary>
    /// <param name="param">objectName (FIDIA object name) or objectName/param1</param>
    /// <returns></returns>
    public int GetIntParameter (string param)
    {
      return int.Parse (this.GetStringParameter (param));
    }

    /// <summary>
    /// Get a long value
    /// </summary>
    /// <param name="param">objectName (FIDIA object name) or objectName/param1</param>
    /// <returns></returns>
    public long GetLong (string param)
    {
      return long.Parse (this.GetString (param));
    }

    /// <summary>
    /// Get a long parameter (which is cached)
    /// </summary>
    /// <param name="param">objectName (FIDIA object name) or objectName/param1</param>
    /// <returns></returns>
    public long GetLongParameter (string param)
    {
      return long.Parse (this.GetStringParameter (param));
    }

    /// <summary>
    /// Get a double value
    /// </summary>
    /// <param name="param">objectName (FIDIA object name) or objectName/param1</param>
    /// <returns></returns>
    public double GetDouble (string param)
    {
      var usCultureInfo = new CultureInfo ("en-US");
      return double.Parse (this.GetString (param),
                           usCultureInfo);
    }

    /// <summary>
    /// Get a double parameter (which is cached)
    /// </summary>
    /// <param name="param">objectName (FIDIA object name) or objectName/param1</param>
    /// <returns></returns>
    public double GetDoubleParameter (string param)
    {
      var usCultureInfo = new CultureInfo ("en-US");
      return double.Parse (this.GetStringParameter (param),
                           usCultureInfo);
    }

    /// <summary>
    /// Check if the connection with the CNC is up. If not, connect to it
    /// </summary>
    /// <returns>The connection was successful</returns>
    public bool CheckConnection ()
    {
      if (m_connectionError) {
        log.Error ("CheckConnection: a connection error has already been set");
        return false;
      }

      if (this.m_connected) {
        return true;
      }

      log.Info ("CheckConnection: " +
                "the CNC is not connected: try to connect");

      // 0. Do not try to connect to FapiCorbaServer too often
      //    (not more often than every m_tcpRetrySleep)
      //    because it can drive to some time out in FapiCorbaServer
      //    (windows messages are too slow)
      if (m_lastTcpConnectionError.HasValue
          && (DateTime.UtcNow < m_lastTcpConnectionError.Value.AddMilliseconds (m_tcpRetrySleep))) {
        log.Warn ("CheckConnection: do not try to connect again because FapiCorbaServer could not support it");
        return false;
      }
      m_lastTcpConnectionError = null;

      // 1. Get the IOR string
      string ior;
      try {
        ior = GetIORString ();
      }
      catch (IORException ex) {
        log.Error ("CheckConnection: IOR exception", ex);
        m_lastTcpConnectionError = DateTime.UtcNow;
        return false;
      }

      // 2. Initialize Corba
      try {
        // Register an IIOP channel if it has not already been done
        Lemoine.CorbaHelper.CorbaClientConnection.RegisterChannel ();
      }
      catch (Exception ex) {
        log.Warn ("CheckConnection: RegisterChannel failed", ex);
      }
      try {
        m_corbaServer =
          (CorbaServer)RemotingServices.Connect (typeof (CorbaServer), ior);
      }
      catch (Exception ex) {
        log.Error ("CheckConnection: Connect failed with ior=" + ior, ex);
        throw;
      }
      if (null == m_corbaServer) {
        log.Error ($"CheckConnection: could not connect to ior={ior}");
        return false;
      }

      m_connected = true;
      return true;
    }
    #endregion

    class IORException : Exception
    {
      /// <summary>
      /// Constructor
      /// </summary>
      /// <param name="message"></param>
      public IORException (string message)
        : base (message)
      { }
    }

    string GetIORString ()
    {
      var ipAddresses = Lemoine.Net.NetworkAddress.GetIPAddressesV4Only (m_hostname);
      foreach (var ipAddress in ipAddresses) {
        try {
          return GetIORString (ipAddress);
        }
        catch (Exception ex) {
          log.Warn ("GetIORString: exception for ipAddress " + ipAddress, ex);
        }
      }

      log.ErrorFormat ("GetIORString: no ipv4 address returned a valid ior");
      throw new IORException ("No valid IPv4 address");
    }

    string GetIORString (IPAddress ipAddress)
    {
      return GetIORString (new IPEndPoint (ipAddress, m_port));
    }

    /// <summary>
    /// Get the IOR string
    /// </summary>
    /// <param name="ipEndPoint"></param>
    /// <returns></returns>
    string GetIORString (IPEndPoint ipEndPoint)
    {
      using (var tcpClient = new TcpClient ()) {
        tcpClient.Connect (ipEndPoint);
        if (!tcpClient.Connected) {
          log.Error ($"GetIORString: Not connected to {ipEndPoint}");
          throw new IORException ("Not connected");
        }
        byte[] message = Encoding.ASCII.GetBytes ("1.0\n");
        tcpClient.Client.Send (message);
        var iorString = "";
        var response = new byte[1024];
        int nbBytes;
        do {
          Array.Clear (response, 0, response.Length);
          nbBytes = tcpClient.Client.Receive (response);
          var ior = Encoding.ASCII.GetString (response);
          ior = ior.TrimEnd (new char[] { '\0', ' ' });
          log.Debug ($"GetIORString: read {nbBytes} bytes, add {ior}");
          iorString += ior;
        }
        while (nbBytes > 0);
        log.Info ($"GetIORString: got IOR string {iorString}");
        if (iorString.Contains ("Error")) {
          log.Error ($"GetIORString: initialization TCP request returned {iorString}");
          throw new IORException ("IOR with error");
        }
        return iorString;
      }
    }
  }
}
