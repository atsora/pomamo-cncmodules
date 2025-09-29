// Copyright (C) 2009-2023 Lemoine Automation Technologies
// Copyright (C) 2023-2025 Atsora Solutions
//
// SPDX-License-Identifier: GPL-2.0

using log4net;
using Opc.Ua;
using Opc.Ua.Configuration;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Lemoine.Cnc
{
  /// <summary>
  /// OPC UA client module
  /// </summary>
  public sealed class OpcUaClient
    : Pomamo.CncModule.ICncModule, IDisposable
  {
    ILog log = LogManager.GetLogger ("Lemoine.Cnc.In.OpcUaClient");
    readonly Lemoine.Cnc.OpcUaConverter m_converter = new Lemoine.Cnc.OpcUaConverter ();
    int m_cncAcquisitionId = 0;
    string m_securityMode;
    string m_username;
    string m_password;
    readonly IList<string> m_listParameters = new List<string> ();
    bool m_queryReady = false;
    readonly NodeManager m_nodeManager;
    ApplicationConfiguration m_configuration;
    UAClient m_client = null;
    string m_defaultNamespace;
    int m_defaultNamespaceIndex = -1;

    /// <summary>
    /// <see cref="Pomamo.CncModule.ICncModule"/>
    /// </summary>
    public int CncAcquisitionId
    {
      get { return m_cncAcquisitionId; }
      set {
        m_cncAcquisitionId = value;
        log = LogManager.GetLogger ($"Lemoine.Cnc.In.OpcUaClient.{value}");
        if (null != m_client) {
          m_client.CncAcquisitionId = value;
        }
        if (null != m_nodeManager) { 
          m_nodeManager.CncAcquisitionId = value;
        }
      }
    }

    /// <summary>
    /// <see cref="Pomamo.CncModule.ICncModule"/>
    /// </summary>
    public string CncAcquisitionName { get; set; }

    /// <summary>
    /// OPC UA Server Url
    /// 
    /// For example: opc.tcp://address:port
    /// </summary>
    public string ServerUrl { get; set; }

    /// <summary>
    /// Use security in OPC UA Client
    /// </summary>
    public bool UseSecurity { get; set; } = true;

    /// <summary>
    /// Security mode: "" (Default) / SignAndEncrypt / Sign / None
    /// </summary>
    public string SecurityMode
    {
      get => m_securityMode;
      set {
        m_securityMode = value;
        if (null != m_client) {
          m_client.SecurityMode = value;
        }
      }
    }

    /// <summary>
    /// Default Namespace
    /// </summary>
    public string DefaultNamespace
    {
      get => m_defaultNamespace;
      set {
        m_defaultNamespace = value;
        m_defaultNamespaceIndex = -1;
      }
    }

    /// <summary>
    /// Certificate password if required
    /// </summary>
    public string CertificatePassword { get; set; }

    /// <summary>
    /// User name if required
    /// </summary>
    public string Username
    {
      get => m_username;
      set {
        m_username = value;
        if (null != m_client) {
          m_client.Username = value;
        }
      }
    }

    /// <summary>
    /// Password if required
    /// </summary>
    public string Password {
      get => m_password;
      set {
        m_password = value;
        if (null != m_client) {
          m_client.Password = value;
        }
      }
    }

    /// <summary>
    /// Renew the certificate ?
    /// </summary>
    public bool RenewCertificate { get; set; } = true;

    /// <summary>
    /// Set it to true if you want every BrowseName to be written in the logs
    /// (just after the session is created)
    /// Default is false
    /// </summary>
    public bool BrowseAndLog { get; set; } = false;

    /// <summary>
    /// Return true if an error occured during a connection
    /// If connected, the boolean is reset to false
    /// </summary>
    public bool ConnectionError { get; private set; } = false;

    /// <summary>
    /// Description of the constructor
    /// </summary>
    public OpcUaClient ()
    {
      m_nodeManager = new NodeManager (this.CncAcquisitionId);
    }

    /// <summary>
    /// <see cref="IDisposable.Dispose" />
    /// </summary>
    public void Dispose ()
    {
      if (m_client != null) {
        log.Info ($"Dispose: disconnect");
        m_client.Disconnect ();
      }

      GC.SuppressFinalize (this);
    }

    string GetPkiDirectory ()
    {
      var localApplicationData = Environment
        .GetFolderPath (Environment.SpecialFolder.LocalApplicationData);
      if (string.IsNullOrEmpty (localApplicationData)) {
        log.Error ($"GetPkiDirectory: LocalApplicationData {localApplicationData} is not defined");
        var home = Environment.GetEnvironmentVariable ("HOME");
        if (string.IsNullOrEmpty (home)) {
          log.Error ($"GetPkiDirectory: HOME {home} is not defined");
        }
        else {
          localApplicationData = Path.Combine (home, ".local", "share");
          log.Info ($"GetPkiDirectory: fallback localApplicationData to {localApplicationData} from home {home}");
        }
      }
      return Path.Combine (localApplicationData, "opcua", "pki");
    }

    ApplicationConfiguration GetConfiguration ()
    {
      var configuration = new ApplicationConfiguration {
        ApplicationName = "Pomamo",
        ApplicationType = ApplicationType.Client,
        ApplicationUri = "urn:" + Utils.GetHostName () + ":Pomamo", // Required ? ProductUri is probably not required
        TransportConfigurations = new TransportConfigurationCollection (), // Required ?
        TransportQuotas = new TransportQuotas {
          OperationTimeout = 120000,
          MaxStringLength = 1048576,
          MaxByteStringLength = 1048576,
          MaxArrayLength = ushort.MaxValue, // 65535
          MaxMessageSize = 4194304,
          MaxBufferSize = ushort.MaxValue, // 65535
          ChannelLifetime = 300000, // 5 min
          SecurityTokenLifetime = 3600000, // 1 hour
        },
        ClientConfiguration = new ClientConfiguration {
          DefaultSessionTimeout = 60000, // 1 min
          WellKnownDiscoveryUrls = { "opc.tcp://{0}:4840", "http://{0}:52601/UADiscovery", "http://{0}/UADiscovery/Default.svc" },
          MinSubscriptionLifetime = 10000,
        },
      };
      var pkiDirectory = GetPkiDirectory ();
      configuration.SecurityConfiguration = new SecurityConfiguration {
        AutoAcceptUntrustedCertificates = true,
        RejectSHA1SignedCertificates = false,
        AddAppCertToTrustedStore = true,
        MinimumCertificateKeySize = 1024,
        NonceLength = 32,
        ApplicationCertificate = new CertificateIdentifier {
          StoreType = CertificateStoreType.Directory,
          StorePath = pkiDirectory + "own",
          SubjectName = Utils.Format (@"CN={0}, DC={1}", configuration.ApplicationName, System.Net.Dns.GetHostName ())
        },
        TrustedIssuerCertificates = new CertificateTrustList {
          StoreType = CertificateStoreType.Directory,
          StorePath = pkiDirectory + "issuer"
        },
        TrustedPeerCertificates = new CertificateTrustList {
          StoreType = CertificateStoreType.Directory,
          StorePath = pkiDirectory + "trusted"
        },
        RejectedCertificateStore = new CertificateTrustList {
          StoreType = CertificateStoreType.Directory,
          StorePath = pkiDirectory + "rejected"
        },
      };
      return configuration;
    }

    ApplicationInstance GetApplication (ApplicationConfiguration configuration) => new ApplicationInstance {
      ApplicationName = "Pomamo",
      ApplicationType = ApplicationType.Client,
      CertificatePasswordProvider = new CertificatePasswordProvider (this.CertificatePassword),
      ApplicationConfiguration = configuration,
    };

    /// <summary>
    /// Start method
    /// </summary>
    /// <returns>success</returns>
    public bool Start ()
    {
      return StartAsync ().Result;
    }

    async Task<bool> StartAsync ()
    {
      if (log.IsDebugEnabled) {
        log.Debug ("Start");
      }

      // Initialize the library the first time
      if (m_configuration is null) {
        log.Info ("Start: Initializing the OPC UA configuration");
        var configuration = GetConfiguration ();
        var application = GetApplication (configuration);

        if (this.RenewCertificate) {
          if (log.IsDebugEnabled) {
            log.Debug ("Start: about to renew the certificate");
          }
          try {
            await application.DeleteApplicationInstanceCertificate ().ConfigureAwait (false);
          }
          catch (Exception ex) {
            log.Error ($"Start: DeleteApplicationInstanceCertificate failed with an exception, but continue", ex);
          }
        }

        try {
          if (log.IsDebugEnabled) {
            log.Debug ("Start: about to check the certificate");
          }
          bool certificateValidation = await application.CheckApplicationInstanceCertificate (false, minimumKeySize: 0).ConfigureAwait (false);
          if (!certificateValidation) {
            log.Error ($"Start: couldn't validate the certificate");
          }
          else if (log.IsDebugEnabled) {
            log.Debug ($"Start: certificate is ok");
          }
        }
        catch (Exception ex) {
          log.Error ($"Start: CheckApplicationInstanceCertificate failed with an exception, but continue", ex);
        }

        m_configuration = configuration;
      }

      if (m_client is null) {
        if (log.IsDebugEnabled) {
          log.Debug ("Start: about to create the OPC UA Client");
        }
        try {
          m_client = new UAClient (this.CncAcquisitionId, m_configuration);//, Namespace, Username, Password, Encryption, SecurityMode);
          m_client.SecurityMode = this.SecurityMode;
          m_client.Username = this.Username;
          m_client.Password = this.Password;
        }
        catch (Exception ex) {
          log.Error ($"Start: creating the new UA Client for CncAcquisitionid={CncAcquisitionId} failed", ex);
          throw;
        }
      }

      // Connection to the machine (if needed)
      try {
        if (log.IsDebugEnabled) {
          log.Debug ("Start: about to connect");
        }
        ConnectionError = !await m_client.ConnectAsync (this.ServerUrl, useSecurity: this.UseSecurity);
      }
      catch (Exception ex) {
        log.Error ("Start: Connect returned an exception", ex);
        ConnectionError = true;
        CheckDisconnectionFromException ("Start.Connect", ex);
      }
      if (this.ConnectionError) {
        log.Error ($"Start: ConnectionError => return {!m_listParameters.Any () && !m_queryReady}");
        return !m_listParameters.Any () && !m_queryReady;
      }
      else if (log.IsDebugEnabled) {
        log.Debug ("Start: connect is successful");
      }

      if (this.BrowseAndLog) {
        log.Debug ($"Start: browse requested");
        try {
          m_nodeManager.Browse (m_client.Session);
        }
        catch (Exception ex) {
          log.Error ("Start: exception in Browse", ex);
          if (!CheckDisconnectionFromException ("Start.Browse", ex)) {
            log.Error ($"Start: Disconnect after Browse");
            return false;
          }
        }
      }

      if (m_listParameters.Any () && !m_queryReady) {
        log.Info ($"Start: listParameters is already not empty => try to prepare the query now");
        PrepareQuery ();
      }

      // Possibly launch the query now
      if (!m_queryReady) {
        if (log.IsDebugEnabled) {
          log.Debug ($"Start: query not ready => return true at once");
        }
        return true;
      }
      else { // !AsyncQuery && m_libOpc.QueryReady
        if (log.IsDebugEnabled) {
          log.Debug ($"Start: about to launch query since ready");
        }
        try {
          m_nodeManager.ReadNodes (m_client.Session);
          return true;
        }
        catch (Exception ex) {
          log.Error ("Start: ReadNodes failed", ex);
          if (!CheckDisconnectionFromException ("Start.ReadNodes", ex)) {
            log.Error ($"Start: disconnect after ReadNodes");
          }
          return false;
        }
      }
    }

    int GetDefaultNamespaceIndex ()
    {
      if (-1 != m_defaultNamespaceIndex) {
        return m_defaultNamespaceIndex;
      }
      else if (!string.IsNullOrEmpty (this.DefaultNamespace)) {
        try {
          m_defaultNamespaceIndex = m_nodeManager.GetNamespaceIndex (m_client.Session, this.DefaultNamespace);
        }
        catch (Exception ex) {
          log.Error ("GetDefaultNamespaceIndex: GetNamespaceIndex failed => return 0", ex);
          return 0;
        }
        return m_defaultNamespaceIndex;
      }
      else {
        return 0;
      }
    }

    void PrepareQuery ()
    {
      if (!m_listParameters.Any ()) {
        if (log.IsDebugEnabled) {
          log.Debug ($"PrepareQuery: listParameters is empty => nothing to do");
        }
        return;
      }

      if (!m_queryReady) {
        if (log.IsDebugEnabled) {
          log.Debug ($"PrepareQuery: prepare the query since not ready");
        }
        try {
          var defaultNamespaceIndex = GetDefaultNamespaceIndex ();
          if (!m_nodeManager.PrepareQuery (m_client.Session, m_listParameters, defaultNamespaceIndex)) {
            log.Error ($"PrepareQuery: PrepareQuery failed");
            return;
          }
        }
        catch (Exception ex) {
          log.Error ("PrepareQuery: PrepareQuery returned an exception", ex);
          CheckDisconnectionFromException ("PrepareQuery.PrepareQuery", ex);
          throw;
        }
        m_queryReady = true;
      }
    }


    /// <summary>
    /// Finish method
    /// </summary>
    public void Finish ()
    {
      PrepareQuery ();
    }

    bool CheckDisconnectionFromException (string methodName, Exception ex)
    {
      log.Error ($"CheckDisconnectionFromException: {methodName} returned an exception", ex);
      return CheckDisconnectionFromException (ex);
    }

    bool CheckDisconnectionFromException (Exception ex)
    {
      if (m_client is null) {
        if (log.IsDebugEnabled) {
          log.Debug ($"CheckDisconnectionFromException: OPC UA client is null, nothing to do", ex);
        }
        return true;
      }

      var messagesRequireRestart = new List<string> { "BadSessionIdInvalid", "BadConnectionClosed" };
      if (messagesRequireRestart.Contains (ex.Message)) {
        if (log.IsInfoEnabled) {
          log.Info ($"CheckDisconnectionFromException: disconnect since {ex.Message}", ex);
        }
        ConnectionError = true;
        try {
          m_client.Disconnect ();
        }
        catch (Exception ex1) {
          log.Error ("CheckDisconnectionFromException: couldn't disconnect", ex1);
        }
        finally {
          m_client = null;
        }
        return false;
      }
      else {
        return true;
      }
    }

    /// <summary>
    /// Get a bool
    /// The parameter will be registered and available after a Start()
    /// </summary>
    /// <param name="parameter"></param>
    /// <returns></returns>
    public bool GetBool (string parameter)
    {
      var result = Get (parameter);
      return m_converter.ConvertAuto<bool> (result);
    }

    /// <summary>
    /// Get a char
    /// The parameter will be registered and available after a Start()
    /// </summary>
    /// <param name="parameter"></param>
    /// <returns></returns>
    public char GetChar (string parameter)
    {
      var result = Get (parameter);
      return m_converter.ConvertAuto<char> (result);
    }

    /// <summary>
    /// Get a byte
    /// The parameter will be registered and available after a Start()
    /// </summary>
    /// <param name="parameter"></param>
    /// <returns></returns>
    public byte GetByte (string parameter)
    {
      var result = Get (parameter);
      return m_converter.ConvertAuto<byte> (result);
    }

    /// <summary>
    /// Get an int 16
    /// The parameter will be registered and available after a Start()
    /// </summary>
    /// <param name="parameter"></param>
    /// <returns></returns>
    public Int16 GetInt16 (string parameter)
    {
      var result = Get (parameter);
      return m_converter.ConvertAuto<Int16> (result);
    }

    /// <summary>
    /// Get an unsigned int 16
    /// The parameter will be registered and available after a Start()
    /// </summary>
    /// <param name="parameter"></param>
    /// <returns></returns>
    public UInt16 GetUInt16 (String parameter)
    {
      var result = Get (parameter);
      return m_converter.ConvertAuto<UInt16> (result);
    }

    /// <summary>
    /// Get an int 32
    /// The parameter will be registered and available after a Start()
    /// </summary>
    /// <param name="parameter"></param>
    /// <returns></returns>
    public Int32 GetInt32 (String parameter)
    {
      var result = Get (parameter);
      return m_converter.ConvertAuto<Int32> (result);
    }

    /// <summary>
    /// Get an unsigned int 32
    /// The parameter will be registered and available after a Start()
    /// </summary>
    /// <param name="parameter"></param>
    /// <returns></returns>
    public UInt32 GetUInt32 (string parameter)
    {
      var result = Get (parameter);
      return m_converter.ConvertAuto<UInt32> (result);
    }

    /// <summary>
    /// Get an int 32 (same than GetInt32)
    /// </summary>
    /// <param name="parameter"></param>
    /// <returns></returns>
    public Int32 GetInt (string parameter)
    {
      var result = Get (parameter);
      return m_converter.ConvertAuto<Int32> (result);
    }

    /// <summary>
    /// Get an unsigned int 32 (same than GetUInt32)
    /// The parameter will be registered and available after a Start()
    /// </summary>
    /// <param name="parameter"></param>
    /// <returns></returns>
    public UInt32 GetUInt (string parameter)
    {
      var result = Get (parameter);
      return m_converter.ConvertAuto<UInt32> (result);
    }

    /// <summary>
    /// Get an int 64
    /// The parameter will be registered and available after a Start()
    /// </summary>
    /// <param name="parameter"></param>
    /// <returns></returns>
    public Int64 GetInt64 (string parameter)
    {
      var result = Get (parameter);
      return m_converter.ConvertAuto<Int64> (result);
    }

    /// <summary>
    /// Get an unsigned int 64
    /// The parameter will be registered and available after a Start()
    /// </summary>
    /// <param name="parameter"></param>
    /// <returns></returns>
    public UInt64 GetUInt64 (string parameter)
    {
      var result = Get (parameter);
      return m_converter.ConvertAuto<UInt64> (result);
    }

    /// <summary>
    /// Get a float
    /// The parameter will be registered and available after a Start()
    /// </summary>
    /// <param name="parameter"></param>
    /// <returns></returns>
    public float GetFloat (string parameter)
    {
      var result = Get (parameter);
      return m_converter.ConvertAuto<float> (result);
    }

    /// <summary>
    /// Get a double
    /// The parameter will be registered and available after a Start()
    /// </summary>
    /// <param name="parameter"></param>
    /// <returns></returns>
    public double GetDouble (string parameter)
    {
      var result = Get (parameter);
      return m_converter.ConvertAuto<double> (result);
    }

    /// <summary>
    /// Get a string
    /// The parameter will be registered and available after a Start()
    /// </summary>
    /// <param name="parameter"></param>
    /// <returns></returns>
    public string GetString (string parameter)
    {
      var result = Get (parameter);
      return m_converter.ConvertAuto<string> (result);
    }

    /// <summary>
    /// Get the raw value
    /// </summary>
    /// <param name="parameter"></param>
    /// <returns></returns>
    public object Get (string parameter)
    {
      if (!m_queryReady) {
        if (!m_listParameters.Contains (parameter)) {
          m_listParameters.Add (parameter);
        }

        log.Info ($"Get: {parameter} not available yet (first start)");
        throw new Exception ($"Query not ready yet");
      }

      if (m_client is null) {
        log.Info ($"Get: the library is not initialized => give up for {parameter}");
        throw new Exception ("Library not initialized");
      }

      if (this.ConnectionError) {
        log.Info ($"Get: connection error => give up for {parameter}");
        throw new Exception ("Connection error");
      }

      try {
        return m_nodeManager.Get (parameter);
      }
      catch (Exception ex) {
        log.Error ($"Get: libopc returned an exception for {parameter}", ex);
        throw;
      }
    }

    /// <summary>
    /// Directly read and return a value
    /// The address is not stored for a bulk reading
    /// Do not use it along with the other queries that store the addresses
    /// </summary>
    /// <param name="address"></param>
    /// <returns></returns>
    public double DirectReadDouble (string address)
    {
      var result = DirectRead (address);
      return m_converter.ConvertAuto<double> (result);
    }

    /// <summary>
    /// Directly read and return a value
    /// The address is not stored for a bulk reading
    /// Do not use it along with the other queries that store the addresses
    /// </summary>
    /// <param name="address"></param>
    /// <returns></returns>
    public object DirectRead (string address)
    {
      if (m_client is null) {
        log.Error ($"DirectRead: opc ua client is null => give up");
        throw new Exception ("Opc ua client not initialized");
      }

      // Prepare the query
      try {
        if (m_nodeManager.PrepareQuery (m_client.Session, new List<string> () { address })) {
          log.Error ($"DirectRead: PrepareQuery returned false for {address}");
          throw new Exception ("Couldn't prepare a query with address " + address);
        }
      }
      catch (Exception ex) {
        log.Error ($"DirectRead: PrepareQuery returned an exception for {address}", ex);
        CheckDisconnectionFromException ("DirectRead.PrepareQuery", ex);
        throw;
      }

      // Launch it
      try {
        m_nodeManager.ReadNodes (m_client.Session);
      }
      catch (Exception ex) {
        log.Error ($"DirectRead: ReadNodes returned an exception for {address}", ex);
        CheckDisconnectionFromException ("DirectRead.ReadNodes", ex);
        throw;
      }

      // Get the result
      try {
        return m_nodeManager.Get (address);
      }
      catch (Exception ex) {
        log.Error ($"DirectRead: Get returned an exception for {address}", ex);
        CheckDisconnectionFromException ("DirectRead.Get", ex);
        throw;
      }
    }

    /// <summary>
    /// Write a value at the specified address
    /// </summary>
    /// <param name="parameter"></param>
    /// <param name="value"></param>
    public void Write (string parameter, object value)
    {
      // Possibly extract indexes
      var indexes = "";
      if (parameter.Contains ("|")) {
        string[] parts = parameter.Split ('|');
        if (parts.Length == 2) {
          indexes = parts[1];
        }
        else {
          log.Warn ($"Write: bad parameter {parameter}: cannot extract indexes");
        }
      }

      // Convert to a valid node id
      string nodeId = m_nodeManager.GetNodeIdFromParam (m_client.Session, parameter);
      if (string.IsNullOrEmpty (nodeId)) {
        log.Error ($"Write: no valid node id for parameter {parameter}");
        throw new Exception ($"Write: no valid node id");
      }

      // Build a list of values to write.
      var nodesToWrite = new WriteValueCollection ();

      // Get the first value to write.
      if (value is VariableNode variable) {
        nodesToWrite.Add (new WriteValue () {
          NodeId = nodeId,
          AttributeId = Attributes.Value,
          Value = new DataValue () { WrappedValue = variable.Value }
        });
      }
      else {
        log.Error ($"Write: {value} is not a VariableNode => give up");
        throw new Exception ($"Write: invalid value");
      }

      // Write the value
      m_client.Session.Write (null, nodesToWrite, out var results, out var diagnostics);

      // Log what happened
      if (diagnostics != null) {
        foreach (var diagnostic in diagnostics) {
          log.Error ($"Write: diagnostic when writing data: {diagnostic}");
        }
      }

      if (results != null) {
        foreach (var result in results) {
          if (StatusCode.IsNotGood (result)) {
            log.Error ($"Write: status {result} not good when writing data");
          }
        }
      }
    }

  }
}
