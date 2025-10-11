// Copyright (C) 2009-2023 Lemoine Automation Technologies
// Copyright (C) 2023-2025 Atsora Solutions
//
// SPDX-License-Identifier: GPL-2.0

using log4net;
using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace Lemoine.Cnc
{
  /// <summary>
  /// OPC UA client module
  /// </summary>
  public sealed class OpcUaClient
    : Pomamo.CncModule.ICncModule, IDisposable
  {
    static readonly int INITIAL_TIMEOUT_SLEEP = 1000; // ms

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
    string m_cncAlarmNamespace = "Sinumerik";
    int m_cncAlarmNamespaceIndex = 2;
    int m_timeoutSleep = 1000; // ms
    Subscription m_eventSubscription = null;
    IList<CncAlarm> m_cncAlarms = new List<CncAlarm> ();

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
    /// Subscribe to CNC Alarms (Sinumerik)
    /// </summary>
    public bool CncAlarmSubscription { get; set; } = false;

    /// <summary>
    /// Cnc alarm namespace (default: Sinumerik, ns=2)
    /// </summary>
    public string CncAlarmNamespace
    {
      get => m_cncAlarmNamespace;
      set {
        m_cncAlarmNamespace = value;
        m_cncAlarmNamespaceIndex = -1;
      }
    }

    /// <summary>
    /// Cnc alarms
    /// </summary>
    public IList<CncAlarm> CncAlarms => m_cncAlarms;

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
    public string Password
    {
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
    /// Connection timeout in seconds
    /// </summary>
    public double TimeoutSeconds { get; set; } = 10.0;

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

    public async ValueTask DisposeAsync ()
    {
      if (m_client != null) {
        log.Info ($"DisposeAsync: disconnect");
        await m_client.DisconnectAsync ();
      }

      GC.SuppressFinalize (this);
    }

    /// <summary>
    /// <see cref="IDisposable.Dispose" />
    /// </summary>
    public void Dispose ()
    {
      if (m_client != null) {
        log.Info ($"Dispose: disconnect");
        m_client.DisconnectAsync ().RunSynchronously ();
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
        log.Debug ("StartAsync");
      }

      // Initialize the library the first time
      if (m_configuration is null) {
        log.Info ("StartAsync: Initializing the OPC UA configuration");
        var configuration = GetConfiguration ();
        var application = GetApplication (configuration);

        if (this.RenewCertificate) {
          if (log.IsDebugEnabled) {
            log.Debug ("StartAsync: about to renew the certificate");
          }
          try {
            await application.DeleteApplicationInstanceCertificate ().ConfigureAwait (false);
          }
          catch (Exception ex) {
            log.Error ($"StartAsync: DeleteApplicationInstanceCertificate failed with an exception, but continue", ex);
          }
        }

        try {
          if (log.IsDebugEnabled) {
            log.Debug ("StartAsync: about to check the certificate");
          }
          bool certificateValidation = await application.CheckApplicationInstanceCertificates (false).ConfigureAwait (false);
          if (!certificateValidation) {
            log.Error ($"StartAsync: couldn't validate the certificate");
          }
          else if (log.IsDebugEnabled) {
            log.Debug ($"StartAsync: certificate is ok");
          }
        }
        catch (Exception ex) {
          log.Error ($"StartAsync: CheckApplicationInstanceCertificate failed with an exception, but continue", ex);
        }

        m_configuration = configuration;
      }

      if (m_client is null) {
        if (log.IsDebugEnabled) {
          log.Debug ("StartAsync: about to create the OPC UA Client");
        }
        try {
          m_client = new UAClient (this.CncAcquisitionId, m_configuration);//, Namespace, Username, Password, Encryption, SecurityMode);
          m_client.SecurityMode = this.SecurityMode;
          m_client.Username = this.Username;
          m_client.Password = this.Password;
        }
        catch (Exception ex) {
          log.Error ($"StartAsync: creating the new UA Client for CncAcquisitionid={CncAcquisitionId} failed", ex);
          throw;
        }
      }

      // Connection to the machine (if needed)
      var timeout = TimeSpan.FromSeconds (this.TimeoutSeconds);
      using (var timeoutCts = new CancellationTokenSource (timeout)) {
        try {
          if (log.IsDebugEnabled) {
            log.Debug ("StartAsync: about to connect");
          }
          var connectTask = m_client.ConnectAsync (this.ServerUrl, useSecurity: this.UseSecurity);
          var completed = await Task.WhenAny (connectTask, Task.Delay (-1, timeoutCts.Token));
          if (completed == connectTask) {
            ConnectionError = !connectTask.Result;
          }
          else {
            log.Error ($"StartAsync: timeout={timeout} reached");
            throw new TimeoutException ("OPC UA connection timeout");
          }
        }
        catch (TimeoutException ex) {
          log.Error ($"StartAsync: timeout exception", ex);
          ConnectionError = true;
          await DisconnectAsync ();
          await Task.Delay (m_timeoutSleep);
          m_timeoutSleep *= 2;
        }
        catch (Exception ex) {
          log.Error ("StartAsync: Connect returned an exception", ex);
          ConnectionError = true;
          await CheckDisconnectionFromExceptionAsync ("StartAsync.Connect", ex);
        }
      }
      if (this.ConnectionError) {
        log.Error ($"StartAsync: ConnectionError => return {!m_listParameters.Any () && !m_queryReady}");
        return !m_listParameters.Any () && !m_queryReady;
      }
      else if (log.IsDebugEnabled) {
        log.Debug ("StartAsync: connect is successful");
        m_timeoutSleep = INITIAL_TIMEOUT_SLEEP;
      }

      if (this.CncAlarmSubscription) {
        try {
          await SubscribeToCncAlarmsAsync ();
          log.Info ("StartAsync: CNC Alarm subscription started successfully.");
        }
        catch (Exception ex) {
          log.Error ("StartAsync: Failed to subscribe to CNC Alarms", ex);
        }
      }

      if (this.BrowseAndLog) {
        log.Debug ($"StartAsync: browse requested");
        try {
          m_nodeManager.Browse (m_client.Session);
        }
        catch (Exception ex) {
          log.Error ("StartAsync: exception in Browse", ex);
          if (!await CheckDisconnectionFromExceptionAsync ("Start.Browse", ex)) {
            log.Error ($"StartAsync: Disconnect after Browse");
            return false;
          }
        }
      }

      if (m_listParameters.Any () && !m_queryReady) {
        log.Info ($"StartAsync: listParameters is already not empty => try to prepare the query now");
        await PrepareQueryAsync ();
      }

      // Possibly launch the query now
      if (!m_queryReady) {
        if (log.IsDebugEnabled) {
          log.Debug ($"StartAsync: query not ready => return true at once");
        }
        return true;
      }
      else { // !AsyncQuery && m_libOpc.QueryReady
        if (log.IsDebugEnabled) {
          log.Debug ($"StartAsync: about to launch query since ready");
        }
        try {
          await m_nodeManager.ReadNodesAsync (m_client.Session);
          return true;
        }
        catch (Exception ex) {
          log.Error ("StartAsync: ReadNodesAsync failed", ex);
          if (!await CheckDisconnectionFromExceptionAsync ("StartAsync.ReadNodesAsync", ex)) {
            log.Error ($"StartAsync: disconnect after ReadNodesAsync");
          }
          return false;
        }
      }
    }

    async Task DisconnectAsync (CancellationToken cancellationToken = default)
    {
      try {
        if (null != m_eventSubscription) {
          try {
            log.Debug ($"DisconnectAsync: deleting event subscription");
            await m_eventSubscription.DeleteAsync (true, cancellationToken);
          }
          catch (Exception ex1) {
            log.Error ($"DisconnectAsync: deleting the subscription failed", ex1);
          }
          finally {
            m_eventSubscription = null;
          }
        }
        await m_client?.DisconnectAsync ();
      }
      catch (Exception ex) {
        log.Error ("DisconnectAsync: couldn't disconnect", ex);
      }
      finally {
        m_client = null;
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

    /// <summary>
    /// Get the Cnc alarm (default Sinumerik) namespace index
    /// </summary>
    /// <returns></returns>
    ushort GetCncAlarmNamespaceIndex ()
    {
      if (-1 != m_cncAlarmNamespaceIndex) {
        return (ushort)m_cncAlarmNamespaceIndex;
      }
      else if (!string.IsNullOrEmpty (this.CncAlarmNamespace)) {
        try {
          m_cncAlarmNamespaceIndex = m_nodeManager.GetNamespaceIndex (m_client.Session, this.CncAlarmNamespace);
        }
        catch (Exception ex) {
          log.Error ("GetSinumerikNamespaceIndex: GetNamespaceIndex failed => return 2", ex);
          return 2;
        }
        return (ushort)m_cncAlarmNamespaceIndex;
      }
      else {
        return 2;
      }
    }

    async Task PrepareQueryAsync ()
    {
      if (!m_listParameters.Any ()) {
        if (log.IsDebugEnabled) {
          log.Debug ($"PrepareQueryAsync: listParameters is empty => nothing to do");
        }
        return;
      }

      if (!m_queryReady) {
        if (log.IsDebugEnabled) {
          log.Debug ($"PrepareQueryAsync: prepare the query since not ready");
        }
        try {
          var defaultNamespaceIndex = GetDefaultNamespaceIndex ();
          if (!await m_nodeManager.PrepareQueryAsync (m_client.Session, m_listParameters, defaultNamespaceIndex)) {
            log.Error ($"PrepareQueryAsync: PrepareQueryAsync failed");
            return;
          }
        }
        catch (Exception ex) {
          log.Error ("PrepareQueryAsync: PrepareQueryAsync returned an exception", ex);
          await CheckDisconnectionFromExceptionAsync ("PrepareQueryAsync.PrepareQueryAsync", ex);
          throw;
        }
        m_queryReady = true;
      }
    }

    async Task SubscribeToCncAlarmsAsync ()
    {
      var cncAlarmNamespaceIndex = GetCncAlarmNamespaceIndex ();

      if (m_client?.Session is null) {
        log.Error ("SubscribeToCncAlarmsAsync: Client session is null. Cannot subscribe.");
        return;
      }

      var eventSourceNodeId = new NodeId ("Sinumerik", cncAlarmNamespaceIndex);

      // Standard NodeId for BaseEventType in NS=0 (i=2041) - Used to reference the event fields
      NodeId baseEventTypeNodeId = new NodeId (2041, 0);

      m_eventSubscription = new Subscription (m_client.Session.DefaultSubscription) {
        PublishingInterval = 1000,
        DisplayName = $"Atsora Cnc Alarms ({CncAcquisitionId})"
      };
      m_client.Session.AddSubscription (m_eventSubscription);

      await m_eventSubscription.CreateAsync ();

      EventFilter filter = new EventFilter ();

      // Select Clauses (Fields we want to receive)
      // Using numeric values for Attribute IDs to avoid dependence on the static Attributes class.
      filter.SelectClauses.AddRange (new SimpleAttributeOperand[]
      {
        // SimpleAttributeOperand(uint attributeId, QualifiedName alias)
        new SimpleAttributeOperand(1, new QualifiedName("EventId")),
        new SimpleAttributeOperand(2, new QualifiedName("Time")),
        new SimpleAttributeOperand(4, new QualifiedName("Message")),
        new SimpleAttributeOperand(5, new QualifiedName("Severity")), // UInt16 (100–1000)
        new SimpleAttributeOperand(8, new QualifiedName("ConditionId")),
        new SimpleAttributeOperand(9, new QualifiedName("SourceName")),// String Origine (NCK, PLC, HMI, etc.)
        // TODO: AlarmId UInt32 ID interne unique de l’alarme dans le runtime
        // TODO: ActiveState Boolean TRUE = active, FALSE = acquittée
      });

      var contentFilter = new ContentFilter ();

      // Filter 1: OfType CNCAlarmType (NS=2)
      var ofTypeElement = new ContentFilterElement {
        FilterOperator = FilterOperator.OfType,
        FilterOperands = new ExtensionObjectCollection
          {
            new ExtensionObject (new LiteralOperand(new Variant(new QualifiedName("CNCAlarmType", cncAlarmNamespaceIndex))))
          }
      };

      // Filter 2: Severity > 0
      var severityElement = new ContentFilterElement {
        FilterOperator = FilterOperator.GreaterThan,
        // Encapsulation de SimpleAttributeOperand et LiteralOperand dans ExtensionObject
        FilterOperands = new ExtensionObjectCollection
          {
              new ExtensionObject(new SimpleAttributeOperand(5, new QualifiedName("Severity"))),
              new ExtensionObject(new LiteralOperand(new Variant((uint)0)))
          }
      };

      var filterElements = new ContentFilterElementCollection
      {
          ofTypeElement,
          severityElement
      };

      contentFilter.Elements = filterElements;
      filter.WhereClause = contentFilter;

      var eventItem = new MonitoredItem (m_eventSubscription.MonitoredItemCount + 1) {
        StartNodeId = eventSourceNodeId,
        AttributeId = Attributes.EventNotifier,
        MonitoringMode = MonitoringMode.Reporting,
        Filter = filter,
        DisplayName = "CNC Alarm Event"
      };

      eventItem.Notification += OnCncAlarmNotification;

      m_eventSubscription.AddItem (eventItem);
      await m_eventSubscription.ApplyChangesAsync ();
    }

    void OnCncAlarmNotification (MonitoredItem monitoredItem, MonitoredItemNotificationEventArgs e)
    {
      try {
        DataValue notificationDataValue = e.NotificationValue as DataValue;
        if (notificationDataValue == null) return;
        EventNotificationList eventNotificationList = notificationDataValue.Value as EventNotificationList;

        if (eventNotificationList == null) {
          log.Warn ($"OnCncAlarmNotification: Received notification value is not an EventNotificationList. Type received: {notificationDataValue?.GetType ().FullName ?? "NULL"}");
          return;
        }

        m_cncAlarms.Clear ();
        foreach (EventFieldList eventField in eventNotificationList.Events) {
          // Fields order matches the SelectClauses order (Time is at index 1, Message at index 2, etc.)
          if (eventField.EventFields.Count >= 6) {
            DateTime timestamp = (DateTime)((Variant)eventField.EventFields[1]).Value;
            LocalizedText message = (LocalizedText)((Variant)eventField.EventFields[2]).Value;
            uint severity = (uint)((Variant)eventField.EventFields[3]).Value;
            string sourceName = ((Variant)eventField.EventFields[5]).Value.ToString ();
            var alarmNumber = "0"; // TODO: alarmId or other
            // TODO: acquitée ou pas ?
            if (log.IsDebugEnabled) { 
              log.Debug ($"OnCncAlarmNotification: Received CNC Alarm - Time: {timestamp}, Source: {sourceName}, Severity: {severity}, Message: {message.Text}");
            }
            var cncAlarm = new CncAlarm ("OpcUa", m_cncAlarmNamespace, sourceName, alarmNumber, message.Text);
            m_cncAlarms.Add (cncAlarm);
          }
        }
      }
      catch (Exception ex) {
        log.Error ("OnCncAlarmNotification: Error processing event notification.", ex);
      }
    }

    /// <summary>
    /// Finish method
    /// </summary>
    public void Finish ()
    {
      FinishAsync ().RunSynchronously ();
    }

    public async Task FinishAsync ()
    {
      await PrepareQueryAsync ();
    }

    async Task<bool> CheckDisconnectionFromExceptionAsync (string methodName, Exception ex)
    {
      log.Error ($"CheckDisconnectionFromException: {methodName} returned an exception", ex);
      return await CheckDisconnectionFromExceptionAsync (ex);
    }

    async Task<bool> CheckDisconnectionFromExceptionAsync (Exception ex)
    {
      if (m_client is null) {
        if (log.IsDebugEnabled) {
          log.Debug ($"CheckDisconnectionFromExceptionAsync: OPC UA client is null, nothing to do", ex);
        }
        return true;
      }

      var messagesRequireRestart = new List<string> { "BadSessionIdInvalid", "BadConnectionClosed" };
      if (messagesRequireRestart.Contains (ex.Message)) {
        if (log.IsInfoEnabled) {
          log.Info ($"CheckDisconnectionFromExceptionAsync: disconnect since {ex.Message}", ex);
        }
        ConnectionError = true;
        await DisconnectAsync ();
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
      var result = DirectReadAsync (address);
      return m_converter.ConvertAuto<double> (result);
    }

    /// <summary>
    /// Directly read and return a value
    /// The address is not stored for a bulk reading
    /// Do not use it along with the other queries that store the addresses
    /// </summary>
    /// <param name="address"></param>
    /// <returns></returns>
    public async Task<object> DirectReadAsync (string address)
    {
      if (m_client is null) {
        log.Error ($"DirectReadAsync: opc ua client is null => give up");
        throw new Exception ("Opc ua client not initialized");
      }

      // Prepare the query
      try {
        if (!await m_nodeManager.PrepareQueryAsync (m_client.Session, new List<string> () { address })) {
          log.Error ($"DirectReadAsync: PrepareQueryAsync returned false for {address}");
          throw new Exception ("Couldn't prepare a query with address " + address);
        }
      }
      catch (Exception ex) {
        log.Error ($"DirectReadAsync: PrepareQueryAsync returned an exception for {address}", ex);
        await CheckDisconnectionFromExceptionAsync ("DirectReadAsync.PrepareQueryAsync", ex);
        throw;
      }

      // Launch it
      try {
        await m_nodeManager.ReadNodesAsync (m_client.Session);
      }
      catch (Exception ex) {
        log.Error ($"DirectReadAsync: ReadNodesAsync returned an exception for {address}", ex);
        await CheckDisconnectionFromExceptionAsync ("DirectReadAsync.ReadNodesAsync", ex);
        throw;
      }

      // Get the result
      try {
        return m_nodeManager.Get (address);
      }
      catch (Exception ex) {
        log.Error ($"DirectReadAsync: Get returned an exception for {address}", ex);
        await CheckDisconnectionFromExceptionAsync ("DirectReadAsync.Get", ex);
        throw;
      }
    }

    /// <summary>
    /// Write a value at the specified address
    /// </summary>
    /// <param name="parameter"></param>
    /// <param name="value"></param>
    public async Task WriteAsync (string parameter, object value, CancellationToken cancellationToken = default)
    {
      // Possibly extract indexes
      var indexes = "";
      if (parameter.Contains ("|")) {
        string[] parts = parameter.Split ('|');
        if (parts.Length == 2) {
          indexes = parts[1];
        }
        else {
          log.Warn ($"WriteAsync: bad parameter {parameter}: cannot extract indexes");
        }
      }

      // Convert to a valid node id
      string nodeId = await m_nodeManager.GetNodeIdFromParamAsync (m_client.Session, parameter);
      if (string.IsNullOrEmpty (nodeId)) {
        log.Error ($"WriteAsync: no valid node id for parameter {parameter}");
        throw new Exception ($"WriteAsync: no valid node id");
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
        log.Error ($"WriteAsync: {value} is not a VariableNode => give up");
        throw new Exception ($"WriteAsync: invalid value");
      }

      // Write the value
      var writeResponse = await m_client.Session.WriteAsync (null, nodesToWrite, cancellationToken);

      // Log what happened
      if (writeResponse?.DiagnosticInfos != null) {
        foreach (var diagnostic in writeResponse.DiagnosticInfos) {
          log.Error ($"WriteAsync: diagnostic when writing data: {diagnostic}");
        }
      }

      if (writeResponse?.Results != null) {
        foreach (var result in writeResponse.Results) {
          if (StatusCode.IsNotGood (result)) {
            log.Error ($"Write: status {result} not good when writing data");
          }
        }
      }
    }

  }
}
