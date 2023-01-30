// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Description of Lemoine_Cnc_Ping.
  /// </summary>
  public class PingTest: Lemoine.Cnc.BaseCncModule, Lemoine.Cnc.ICncModule, IDisposable
  {
    static readonly int TIMEOUT_DEFAULT = 500;
    static readonly string HOST_REGEX = "^[/\\\\:]*(?<host>[a-zA-Z\\d-\\.]+)[/\\\\:]*";

    #region Members
    bool m_initialized = false;
    string m_host = null;
    bool m_pingOk = false;
    bool m_addressNotValid = false;
    bool m_error = false;
    Regex m_hostRegex;
    #endregion // Members

    #region Getters / Setters
    /// <summary>
    /// Address
    /// 
    /// <item>IP address</item>
    /// <item>IP address:port</item>
    /// <item>Name</item>
    /// <item>Name:port</item>
    /// </summary>
    public string Address { get; set; }
    
    /// <summary>
    /// Host that is extracted from the address
    /// </summary>
    public string Host {
      get
      {
        Initialize ();
        return m_host;
      }
    }
    
    /// <summary>
    /// Timeout in ms
    /// </summary>
    public int Timeout { get; set; }
    
    /// <summary>
    /// Ping Ok property
    /// </summary>
    public bool PingOk {
      get
      {
        Initialize ();
        return m_pingOk;
      }
    }
    
    /// <summary>
    /// Address not valid property
    /// </summary>
    public bool AddressNotValid {
      get
      {
        Initialize ();
        return m_addressNotValid;
      }
    }
    
    /// <summary>
    /// Other error
    /// </summary>
    public bool Error {
      get
      {
        try {
          Initialize ();
        }
        catch (Exception) {
          return true;
        }
        return m_error;
      }
    }
    #endregion // Getters / Setters

    #region Constructors / Destructor / ToString methods
    /// <summary>
    /// Description of the constructor
    /// </summary>
    public PingTest ()
      : base("Lemoine.Cnc.Test.PingTest")
    {
      this.Timeout = TIMEOUT_DEFAULT;
      m_hostRegex = new Regex (HOST_REGEX, RegexOptions.Compiled);
    }
    
    /// <summary>
    /// <see cref="IDisposable.Dispose" />
    /// </summary>
    public void Dispose ()
    {
      // Do nothing special here
      GC.SuppressFinalize (this);
    }
    
    #endregion // Constructors / Destructor / ToString methods

    #region Methods
    /// <summary>
    /// Start method
    /// </summary>
    public void Start ()
    {
      m_initialized = false;
      m_host = null;
      m_pingOk = false;
      m_addressNotValid = false;
      m_error = false;
    }
    
    void Initialize ()
    {
      if (m_initialized) {
        return;
      }
      
      if (m_error) {
        log.Debug ("Initialize: error flag is set");
        throw new Exception ("Error flag");
      }
      
      if (string.IsNullOrEmpty (this.Address)) {
        log.Debug ("Initialize: Address is not defined, give up");
        m_error = true;
        throw new Exception ("Address not defined");
      }
      
      Debug.Assert (0 < this.Timeout);
      if (this.Address.Contains ("://")) {
        m_host = new Uri (this.Address).Host;
      }
      else {
        var match = m_hostRegex.Match (this.Address);
        if (!match.Success) {
          log.Debug ($"Initialize: bad address format {this.Address}");
          m_error = true;
          throw new Exception ("Bad address format");
        }
        else {
          if (match.Groups["host"].Success) {
            m_host = match.Groups["host"].Value.Trim ();
            if (string.IsNullOrEmpty (m_host)) {
              log.Debug ($"Initialize: bad address format {this.Address}");
              m_error = true;
              throw new Exception ("Bad address format");
            }
          }
        }
      }

      Ping ping = new Ping ();
      try {
        PingReply reply = ping.Send (m_host, this.Timeout);
        m_pingOk = (IPStatus.Success == reply.Status);
        log.Debug ($"Initialize: ping answer is {reply.Status}");
      }
      catch (ArgumentNullException) {
        log.Error ($"Initialize: empty address {m_host}");
        m_addressNotValid = true;
      }
      catch (System.Net.Sockets.SocketException ex) {
        log.Error ($"Initialize: socket exception, the address {m_host} is not valid", ex);
        m_addressNotValid = true;
      }
      catch (ObjectDisposedException ex) {
        log.Error ($"Initialize: Object disposed exception ", ex);
        m_error = true;
        throw;
      }
      catch (InvalidOperationException ex) {
        log.Error ($"Initialize: invalid operation exception ", ex);
        if (ex.InnerException is System.Net.Sockets.SocketException) {
          log.Error ($"Initialize: inner socket exception, the address {m_host} is not valid ", ex.InnerException);
          m_addressNotValid = true;
        }
        else {
          m_error = true;
          throw;
        }
      }
      catch (Exception ex) {
        log.Error ($"Initialize: unexpected error ", ex);
        m_error = true;
        throw;
      }
      
      m_initialized = true;
    }
    #endregion // Methods
  }
}
