// Copyright (C) 2009-2023 Lemoine Automation Technologies
// Copyright (C) 2025 Atsora Solutions
//
// SPDX-License-Identifier: GPL-2.0-only
// SPDX-License-Identifier: MIT

/* ========================================================================
 * Copyright (c) 2005-2020 The OPC Foundation, Inc.
 * 2009-2023 Lemoine Automation Technologies
 * All rights reserved.
 *
 * OPC Foundation MIT License 1.00
 * 
 * Permission is hereby granted, free of charge, to any person
 * obtaining a copy of this software and associated documentation
 * files (the "Software"), to deal in the Software without
 * restriction, including without limitation the rights to use,
 * copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the
 * Software is furnished to do so, subject to the following
 * conditions:
 * 
 * The above copyright notice and this permission notice shall be
 * included in all copies or substantial portions of the Software.
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
 * EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
 * OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
 * NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
 * HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
 * WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
 * FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
 * OTHER DEALINGS IN THE SOFTWARE.
 *
 * The complete license agreement can be found here:
 * http://opcfoundation.org/License/MIT/1.00/
 * ======================================================================*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using log4net;
using Opc.Ua;
using Opc.Ua.Client;

namespace Lemoine.Cnc
{
  /// <summary>
  /// UAClient
  /// </summary>
  sealed class UAClient
  {
    ILog log = LogManager.GetLogger ("Lemoine.Cnc.In.OpcUaClient.UAClient");

    readonly ApplicationConfiguration m_configuration;
    Opc.Ua.Client.ISession m_session;
    SessionReconnectHandler m_reconnectHandler;
    object m_lock = new object ();
    int m_cncAcquisitionId = 0;

    /// <summary>
    /// Cnc acquisition id
    /// </summary>
    public int CncAcquisitionId
    {
      get => m_cncAcquisitionId;
      set {
        m_cncAcquisitionId = value;
        log = LogManager.GetLogger ($"Lemoine.Cnc.In.OpcUaClient.UAClient.{value}");
      }
    }

    /// <summary>
    /// Security mode: "" (Default) / SignAndEncrypt / Sign / None
    /// </summary>
    public string SecurityMode { get; set; } = "";

    /// <summary>
    /// User name if required
    /// </summary>
    public string Username { get; set; }

    /// <summary>
    /// Password if required
    /// </summary>
    public string Password { get; set; }

    /// <summary>
    /// Gets the client session.
    /// </summary>
    public Opc.Ua.Client.ISession Session => m_session;

    /// <summary>
    /// The session keepalive interval to be used in ms.
    /// </summary>
    public int KeepAliveInterval { get; set; } = 5000;

    /// <summary>
    /// The reconnect period to be used in ms.
    /// </summary>
    public int ReconnectPeriod { get; set; } = 10000;

    /// <summary>
    /// Auto accept untrusted certificates.
    /// </summary>
    public bool AutoAccept { get; set; } = true;

    /// <summary>
    /// The session lifetime.
    /// </summary>
    public uint SessionLifeTime { get; set; } = 30 * 1000;

    /// <summary>
    /// Constructor
    /// </summary>
    public UAClient (int cncAcquisitionId, ApplicationConfiguration configuration)
    {
      this.CncAcquisitionId = cncAcquisitionId;
      m_configuration = configuration;
      m_configuration.CertificateValidator.CertificateValidation += CertificateValidation;
    }

    #region IDisposable
    /// <summary>
    /// Dispose objects.
    /// </summary>
    public void Dispose ()
    {
      Utils.SilentDispose (m_session);
      m_configuration.CertificateValidator.CertificateValidation -= CertificateValidation;
    }
    #endregion

    /// <summary>
    /// Creates a session with the UA server
    /// </summary>
    public async Task<bool> ConnectAsync (string serverUrl, bool useSecurity = true)
    {
      if (serverUrl is null) {
        throw new ArgumentNullException (nameof (serverUrl));
      }

      try {
        if (m_session != null && m_session.Connected == true) {
          if (log.IsDebugEnabled) {
            log.Debug ("ConnectAsync: session already connected");
          }
        }
        else {
          log.Info ($"ConnectAsync: connecting to {serverUrl}");

          // Get the endpoint by connecting to server's discovery endpoint.
          // Try to find the first endopint with security.
          var endpointDescription = CoreClientUtils.SelectEndpoint (m_configuration, serverUrl, useSecurity);
          switch (this.SecurityMode) {
            case "SignAndEncrypt":
              endpointDescription.SecurityMode = MessageSecurityMode.SignAndEncrypt;
              break;
            case "Sign":
              endpointDescription.SecurityMode = MessageSecurityMode.Sign;
              break;
            case "None":
              endpointDescription.SecurityMode = MessageSecurityMode.None;
              break;
            default:
              break;
          }
          var endpointConfiguration = EndpointConfiguration.Create (m_configuration);
          var endpoint = new ConfiguredEndpoint (null, endpointDescription, endpointConfiguration);

          var userIdentity = string.IsNullOrEmpty (this.Username)
            ? new UserIdentity ()
            : new UserIdentity (this.Username, this.Password);
          if (log.IsDebugEnabled) {
            log.Debug ($"ConnectAsync: username={this.Username} password={this.Password}");
          }

          // Create the session
          var session = await Opc.Ua.Client.Session.Create (
              m_configuration,
              endpoint,
              false,
              false,
              m_configuration.ApplicationName,
              SessionLifeTime,
              userIdentity,
              null
          );

          // Assign the created session
          if (session != null && session.Connected) {
            m_session = session;

            // override keep alive interval
            m_session.KeepAliveInterval = KeepAliveInterval;

            // set up keep alive callback.
            m_session.KeepAlive += new KeepAliveEventHandler (SessionKeepAlive);
          }

          // Session created successfully.
          if (log.IsInfoEnabled) {
            log.Info ($"ConnectAsync: new session created with name={m_session.SessionName}");
          }
        }

        return true;
      }
      catch (Exception ex) {
        // Log Error
        log.Error ("ConnectAsync: exception", ex);
        return false;
      }
    }

    /// <summary>
    /// Disconnects the session.
    /// </summary>
    public void Disconnect ()
    {
      try {
        if (m_session != null) {
          if (log.IsDebugEnabled) {
            log.Debug ("Disconnect");
          }

          m_session.Close ();
          m_session.Dispose ();
          m_session = null;

          // Log Session Disconnected event
          log.Info ("Disconnect: session disconnected");
        }
        else {
          log.Debug ("Disconnect: session not created");
        }
      }
      catch (Exception ex) {
        log.Error ("Disconnect: exception", ex);
      }
    }

    /// <summary>
    /// Handles a keep alive event from a session and triggers a reconnect if necessary.
    /// </summary>
    private void SessionKeepAlive (Opc.Ua.Client.ISession session, KeepAliveEventArgs e)
    {
      try {
        // check for events from discarded sessions.
        if (!Object.ReferenceEquals (session, m_session)) {
          return;
        }

        // start reconnect sequence on communication error.
        if (ServiceResult.IsBad (e.Status)) {
          if (ReconnectPeriod <= 0) {
            log.Warn ($"KeepAlive status {e.Status}, but reconnect is disabled.");
            return;
          }

          lock (m_lock) {
            if (m_reconnectHandler == null) {
              log.Info ($"SessionKeepAlive: status={e.Status}, reconnecting in {ReconnectPeriod}ms");
              m_reconnectHandler = new SessionReconnectHandler (true);
              m_reconnectHandler.BeginReconnect (m_session, ReconnectPeriod, ClientReconnectComplete);
            }
            else {
              log.Info ($"SessionKeepAlive: status={e.Status}, reconnect in progress");
            }
          }

          return;
        }
      }
      catch (Exception ex) {
        log.Error ("SessionKeepAlive: exception", ex);
      }
    }

    /// <summary>
    /// Called when the reconnect attempt was successful.
    /// </summary>
    private void ClientReconnectComplete (object sender, EventArgs e)
    {
      // ignore callbacks from discarded objects.
      if (!Object.ReferenceEquals (sender, m_reconnectHandler)) {
        return;
      }

      lock (m_lock) {
        // if session recovered, Session property is null
        if (m_reconnectHandler.Session != null) {
          m_session = m_reconnectHandler.Session;
        }

        m_reconnectHandler.Dispose ();
        m_reconnectHandler = null;
      }

      log.Info ("ClientReconnectComplete: reconnected");
    }

    /// <summary>
    /// Handles the certificate validation event.
    /// This event is triggered every time an untrusted certificate is received from the server.
    /// </summary>
    void CertificateValidation (CertificateValidator sender, CertificateValidationEventArgs e)
    {
      ServiceResult error = e.Error;
      if (error.StatusCode == StatusCodes.BadCertificateUntrusted && AutoAccept) {
        log.Warn ($"Untrusted Certificate accepted. Subject={e.Certificate.Subject}");
        e.Accept = true;
      }
      else {
        log.Error ($"Untrusted Certificate rejected. Subject={e.Certificate.Subject}");
      }
    }
  }
}
