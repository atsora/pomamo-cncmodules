// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using Lemoine.Cnc;
using Lemoine.Threading;
using System;
using System.Collections.Generic;
using System.IO;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Brother input module
  /// 
  /// Deprecated: use instead <see cref="BrotherTcp"/>, <see cref="BrotherFtp"/>, <see cref="BrotherHttp"/>
  /// </summary>
  public sealed partial class Brother : BaseCncModule, ICncModule, IDisposable
  {
    #region Members
    string m_machineType = "";
    readonly BrotherTcp m_brotherTcp = new BrotherTcp ();
    readonly BrotherFtp m_brotherFtp = new BrotherFtp ();
    readonly BrotherHttp m_brotherHttp = new BrotherHttp ();
    #endregion Members

    #region Getters / Setters
    /// <summary>
    /// Cnc Acquisition ID
    /// </summary>
    public override int CncAcquisitionId
    {
      get { return base.CncAcquisitionId; }
      set {
        base.CncAcquisitionId = value;
        m_brotherTcp.CncAcquisitionId = value;
        m_brotherFtp.CncAcquisitionId = value;
        m_brotherHttp.CncAcquisitionId = value;
      }
    }


    /// <summary>
    /// Host name or IP
    /// </summary>
    public string HostOrIP
    {
      get { return m_brotherFtp.HostOrIP; }
      set {
        m_brotherFtp.HostOrIP = value;
        m_brotherTcp.HostOrIP = value;
        m_brotherHttp.HostOrIP = value;
      }
    }

    /// <summary>
    /// Can be "B" for B00 machines or "C" for C00 machines
    /// </summary>
    public string MachineType
    {
      get { return m_machineType; }
      set {
        m_machineType = value;
        m_brotherTcp.MachineType = m_machineType;
        m_brotherFtp.MachineType = m_machineType;
      }
    }

    /// <summary>
    /// TCP port
    /// Default is 10000
    /// </summary>
    public int TCPPort
    {
      get { return m_brotherTcp.Port; }
      set { m_brotherTcp.Port = value; }
    }

    /// <summary>
    /// Timeout for reading data with the TCP protocol
    /// Default is 200 ms
    /// </summary>
    public int TCPTimeOutMs
    {
      get { return m_brotherTcp.TimeOutMs; }
      set { m_brotherTcp.TimeOutMs = value; }
    }

    /// <summary>
    /// FTP login
    /// </summary>
    public string FTPLogin
    {
      get { return m_brotherFtp.Login; }
      set { m_brotherFtp.Login = value; }
    }

    /// <summary>
    /// FTP password
    /// </summary>
    public string FTPPassword
    {
      get { return m_brotherFtp.Password; }
      set { m_brotherFtp.Password = value; }
    }

    /// <summary>
    /// FTP timeout
    /// Default is 200 ms
    /// </summary>
    public int FTPTimeOutMs
    {
      get { return m_brotherFtp.TimeOutMs; }
      set { m_brotherFtp.TimeOutMs = value; }
    }

    /// <summary>
    /// Timeout for reading data with the HTTP protocol
    /// Default is 200 ms
    /// </summary>
    public int HTTPTimeOutMs
    {
      get { return m_brotherHttp.TimeOutMs; }
      set { m_brotherHttp.TimeOutMs = value; }
    }

    /// <summary>
    /// True is nothing can be read
    /// </summary>
    public bool AcquisitionError
    {
      get {
        return m_brotherTcp.AcquisitionError || m_brotherFtp.AcquisitionError || m_brotherHttp.AcquisitionError;
      }
    }
    #endregion Getters / Setters

    #region Constructors, destructor
    /// <summary>
    /// Default constructor
    /// </summary>
    public Brother () : base ("Lemoine.Cnc.In.Brother")
    {
    }

    /// <summary>
    /// <see cref="IDisposable.Dispose" />
    /// </summary>
    public void Dispose ()
    {
      m_brotherTcp.Dispose ();
      m_brotherFtp.Dispose ();
      m_brotherHttp.Dispose ();
      GC.SuppressFinalize (this);
    }
    #endregion Constructor, destructor

    #region Methods
    /// <summary>
    /// Start method
    /// </summary>
    public bool Start ()
    {
      log.Info ("Start");

      // Reset elements
      bool ftpStart;
      try {
        ftpStart = m_brotherFtp.Start ();
      }
      catch (Exception ex) {
        log.Error ("Start: start of FTP failed", ex);
        ftpStart = false;
      }
      bool tcpStart;
      try {
        tcpStart = m_brotherTcp.Start ();
      }
      catch (Exception ex) {
        log.Error ("Start: start of TCP failed", ex);
        tcpStart = false;
      }
      bool httpStart;
      try {
        httpStart = m_brotherHttp.Start ();
      }
      catch (Exception ex) {
        log.Error ("Start: start of HTTP failed", ex);
        httpStart = false;
      }

      return tcpStart || ftpStart || httpStart;
    }

    /// <summary>
    /// Implements <see cref="ICncModule" />
    /// </summary>
    /// <param name="dataHandler"></param>
    public override void SetDataHandler (IChecked dataHandler)
    {
      base.SetDataHandler (dataHandler);
      m_brotherTcp.SetDataHandler (dataHandler);
      m_brotherFtp.SetDataHandler (dataHandler);
      m_brotherHttp.SetDataHandler (dataHandler);
    }

    /// <summary>
    /// Implements <see cref="IChecked" />
    /// </summary>
    public override void SetActive ()
    {
      base.SetActive ();
      m_brotherTcp.SetActive ();
      m_brotherHttp.SetActive ();
      m_brotherHttp.SetActive ();
    }
    #endregion // Methods

    #region tcp
    /// <summary>
    /// Read a string with the TCP protocol type 2
    /// </summary>
    /// <param name="param">format can be
    ///    {command type: 3 characters}|{function: max 4 characters}|{message: max 8 characters}
    /// or {command type: 3 characters}|{function: max 4 characters}|{message: max 8 characters}|{data}
    /// possibly followed by ~{position}-{length} for extracting a substring</param>
    /// <returns></returns>
    public string GetTCPString (string param)
    {
      return m_brotherTcp.GetString (param);
    }

    /// <summary>
    /// Read an int with the TCP protocol type 2
    /// </summary>
    /// <param name="param">cf GetTCPString</param>
    /// <returns></returns>
    public int GetTCPInt (string param)
    {
      return m_brotherTcp.GetInt (param);
    }

    /// <summary>
    /// Read a double with the TCP protocol type 2
    /// </summary>
    /// <param name="param">cf GetTCPString</param>
    /// <returns></returns>
    public double GetTCPDouble (string param)
    {
      return m_brotherTcp.GetDouble (param);
    }

    /// <summary>
    /// Read a boolean with the TCP protocol type 2
    /// </summary>
    /// <param name="param">cf GetTCPString</param>
    /// <returns></returns>
    public bool GetTCPBool (string param)
    {
      return m_brotherTcp.GetBool (param);
    }

    /// <summary>
    /// Read PrograName with the TCP protocol type 2 ans store it for part and operation detection
    /// </summary>
    /// <param name="param">cf GetTCPString</param>
    /// <returns></returns>
    public string GetTCPProgramName (string param)
    {
      return m_brotherTcp.GetProgramName (param);
    }

    /// <summary>
    /// Read PrograName from a file with the TCP protocol type 2 ans store it for part and operation detection
    /// </summary>
    /// <param name="param">cf GetTCPStringFromFile</param>
    /// <returns></returns>
    public string GetTCPProgramNameFromFile (string param)
    {
      return m_brotherTcp.GetProgramNameFromFile (param);
    }

    /// <summary>
    ///Read the line in program operation comment with the TCP protocol type 2
    ///Note: must be called after getting ProgramName
    /// </summary> 
    /// <param name="param">cf GetTCPString</param>
    /// <returns></returns>
    public string GetTCPProgramOperationComment (string param)
    {
      return m_brotherTcp.GetProgramOperationComment (param);
    }

    /// <summary>
    /// Read a list of string from a file with the TCP protocol type 2
    /// </summary>
    /// <param name="param">format: {file with no extension}|{symbol}</param>
    /// <returns></returns>
    public IList<string> GetTCPStringListFromFile (string param)
    {
      return m_brotherTcp.GetStringListFromFile (param);
    }

    /// <summary>
    /// Read a list of int from a file with the TCP protocol type 2
    /// </summary>
    /// <param name="param">format: {file with no extension}|{symbol}</param>
    /// <returns></returns>
    public IList<int> GetTCPIntListFromFile (string param)
    {
      return m_brotherTcp.GetIntListFromFile (param);
    }

    /// <summary>
    /// Read a string from a file with the TCP protocol type 2
    /// </summary>
    /// <param name="param">format: {file with no extension}|{symbol}|{position}</param>
    /// <returns></returns>
    public string GetTCPStringFromFile (string param)
    {
      return m_brotherTcp.GetStringFromFile (param);
    }

    /// <summary>
    /// Read an int from a file with the TCP protocol type 2
    /// </summary>
    /// <param name="param">cf GetTCPStringFromFile</param>
    /// <returns></returns>
    public int GetTCPIntFromFile (string param)
    {
      return m_brotherTcp.GetIntFromFile (param);
    }

    /// <summary>
    /// Read a double from a file with the TCP protocol type 2
    /// </summary>
    /// <param name="param">cf GetTCPStringFromFile</param>
    /// <returns></returns>
    public double GetTCPDoubleFromFile (string param)
    {
      return m_brotherTcp.GetDoubleFromFile (param);
    }

    /// <summary>
    /// Read a boolean from a file with the TCP protocol type 2
    /// </summary>
    /// <param name="param">cf GetTCPStringFromFile</param>
    /// <returns></returns>
    public bool GetTCPBoolFromFile (string param)
    {
      return m_brotherTcp.GetBoolFromFile (param);
    }

    /// <summary>
    /// Read the maintenance notice in a file with the TCP protocol type 2
    /// Equivalent to GetFTPMaintenanceNotice
    /// </summary>
    /// <returns></returns>
    public IList<CncAlarm> GetTCPMaintenanceNotice => m_brotherTcp.MaintenanceNotice;

    /// <summary>
    /// Read the alarms
    /// Equivalent to GetFTPAlarms
    /// </summary>
    public IList<CncAlarm> GetTCPAlarms => m_brotherTcp.Alarms;

    /// <summary>
    /// Read a set of macros with the metric units
    /// Equivalent to GetFTPMacroSet
    /// </summary>
    /// <param name="param">format: {separator}{macro number 1}{separator}{macro number 2}... For example ",C500,C673,C999"</param>
    /// <returns></returns>
    public IDictionary<string, double> GetTCPMacroSetMetric (string param)
    {
      return m_brotherTcp.GetMacroSetMetric (param);
    }

    /// <summary>
    /// Read a set of macros with the imperial units
    /// Equivalent to GetFTPMacroSet
    /// </summary>
    /// <param name="param">format: {separator}{macro number 1}{separator}{macro number 2}... For example ",C500,C673,C999"</param>
    /// <returns></returns>
    public IDictionary<string, double> GetTCPMacroSetInches (string param)
    {
      return m_brotherTcp.GetMacroSetInches (param);
    }

    /// <summary>
    /// Get the tool life data
    /// Equivalent to GetFTPToolLifeData
    /// </summary>
    /// <param name="param">File to read (TOLNI1 for inches or TOLNM1 for metrics)</param>
    public ToolLifeData GetTCPToolLifeData (string param)
    {
      return m_brotherTcp.GetToolLifeData (param);
    }
    #endregion // tcp

    #region ftp
    /// <summary>
    /// Get a string contained in a file (FTP connection required)
    /// </summary>
    /// <param name="param">format: {file}|{symbol}|{position}</param>
    /// <returns></returns>
    public string GetFTPString (string param)
    {
      return m_brotherFtp.GetString (param);
    }

    /// <summary>
    /// Get an int contained in a file (FTP connection required)
    /// </summary>
    /// <param name="param">format: {file}|{symbol}|{position}</param>
    /// <returns></returns>
    public int GetFTPInt (string param)
    {
      return m_brotherFtp.GetInt (param);
    }

    /// <summary>
    /// Get a double contained in a file (FTP connection required)
    /// </summary>
    /// <param name="param">format: {file}|{symbol}|{position}</param>
    /// <returns></returns>
    public double GetFTPDouble (string param)
    {
      return m_brotherFtp.GetDouble (param);
    }

    /// <summary>
    /// Get a boolean contained in a file (FTP connection required)
    /// </summary>
    /// <param name="param">format: {file}|{symbol}|{position}</param>
    /// <returns></returns>
    public bool GetFTPBool (string param)
    {
      return m_brotherFtp.GetBool (param);
    }

    /// <summary>
    /// Get all elements of a specific line
    /// </summary>
    /// <param name="param">format: {file}|{symbol}</param>
    /// <returns></returns>
    public IList<string> GetFTPStringList (string param)
    {
      return m_brotherFtp.GetStringList (param);
    }

    /// <summary>
    /// Get all elements of a specific line, as a list of int
    /// </summary>
    /// <param name="param">format: {file}|{symbol}</param>
    /// <returns></returns>
    public IList<int> GetFTPIntList (string param)
    {
      return m_brotherFtp.GetIntList (param);
    }

    /// <summary>
    /// Read the maintenance notice in a file
    /// Equivalent to GetTCPMaintenanceNotice
    /// </summary>
    /// <returns></returns>
    public IList<CncAlarm> GetFTPMaintenanceNotice => m_brotherFtp.MaintenanceNotice;

    /// <summary>
    /// Read the alarms
    /// Equivalent to GetTCPAlarms
    /// </summary>
    public IList<CncAlarm> GetFTPAlarms => m_brotherFtp.Alarms;

    /// <summary>
    /// Read a set of macros with the metric units
    /// Equivalent to GetTCPMacroSet
    /// </summary>
    /// <param name="param">format: {separator}{macro number 1}{separator}{macro number 2}... For example ",C500,C673,C999"</param>
    /// <returns></returns>
    public IDictionary<string, double> GetFTPMacroSetMetric (string param)
    {
      return m_brotherFtp.GetMacroSetMetric (param);
    }

    /// <summary>
    /// Read a set of macros with the imperial units
    /// Equivalent to GetTCPMacroSet
    /// </summary>
    /// <param name="param">format: {separator}{macro number 1}{separator}{macro number 2}... For example ",C500,C673,C999"</param>
    /// <returns></returns>
    public IDictionary<string, double> GetFTPMacroSetInches (string param)
    {
      return m_brotherFtp.GetMacroSetInches (param);
    }
    /// <summary>
    /// Get the tool life data
    /// Equivalent to GetTCPToolLifeData
    /// </summary>
    /// <param name="param">File to read (TOLNI1.NC for inches or TOLNM1.NC for metrics)</param>
    /// <returns></returns>
    public ToolLifeData GetFTPToolLifeData (string param)
    {
      return m_brotherFtp.GetToolLifeData (param);
    }
    #endregion // ftp
  }
}
