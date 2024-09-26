// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Sockets;
using Lemoine.Core.Log;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Machine Data Collection for Haas NGC controls
  /// 
  /// https://www.haascnc.com/service/troubleshooting-and-how-to/how-to/machine-data-collection---ngc.html
  /// </summary>
  public sealed class HaasMDC
    : Lemoine.Cnc.BaseCncModule, Lemoine.Cnc.ICncModule, IDisposable
  {
    TcpClient m_tcpClient = null;
    NetworkStream m_stream = null;
    string m_previousMotionTime = null;
    bool m_error = false;

    #region Getters / Setters
    /// <summary>
    /// CNC address, host name or IP address
    /// </summary>
    public string Address { get; set; }

    /// <summary>
    /// Port number
    /// 
    /// Default: 5051
    /// </summary>
    public int PortNumber { get; set; } = 5051;

    /// <summary>
    /// Time out in ms
    /// </summary>
    public int TimeOutMs { get; set; } = 200;

    /// <summary>
    /// Number of connection attempts during the initialization phase
    /// </summary>
    public int ConnectionInitializationAttempts { get; set; } = 2;

    /// <summary>
    /// An error occurred
    /// </summary>
    public bool Error => m_error;

    /// <summary>
    /// Reset the TCP Client each iteration
    /// </summary>
    public bool ResetTcpClient { get; set; } = false;
    #endregion // Getters / Setters

    #region Constructors / Destructor / ToString methods
    /// <summary>
    /// Description of the constructor
    /// </summary>
    public HaasMDC ()
      : base ("Lemoine.Cnc.In.HaasMDC")
    {
    }

    /// <summary>
    /// <see cref="IDisposable.Dispose" />
    /// </summary>
    public void Dispose ()
    {
      if (null != m_stream) {
        try {
          m_stream.Dispose ();
        }
        catch (Exception ex) {
          log.Fatal ($"Dispose: dispose of stream raised an exception", ex);
        }
        finally {
          m_stream = null;
        }
      }
      if (null != m_tcpClient) {
        try {
          m_tcpClient.Close ();
        }
        catch (Exception ex) {
          log.Fatal ("Dispose: unexpected exception since TcpClient.Close is not supposed to raise any exception", ex);
        }
        finally {
          m_tcpClient = null;
        }
      }

      // Do nothing special here
      GC.SuppressFinalize (this);
    }
    #endregion // Constructors / Destructors / ToString methods

    /// <summary>
    /// Start method
    /// </summary>
    /// <returns>success</returns>
    public bool Start ()
    {
      m_error = false;

      try {
        if (string.IsNullOrEmpty (this.Address)) {
          log.Error ($"Start: address is not set => return false");
          return false;
        }

        if ((null != m_tcpClient) && !m_tcpClient.Connected) {
          if (log.IsInfoEnabled) {
            log.Info ($"Start: not connected to {this.Address}:{this.PortNumber} => reset tcp client");
          }
          m_tcpClient?.Close ();
          m_tcpClient = null;
        }
        if (m_tcpClient is null) {
          if (log.IsDebugEnabled) {
            log.Debug ($"Start: create tcp client {this.Address}:{this.PortNumber}");
          }
          m_tcpClient = new TcpClient (this.Address, this.PortNumber);
          if (log.IsWarnEnabled && !m_tcpClient.Connected) {
            log.Warn ($"Start: not connected to {this.Address}:{this.PortNumber}");
          }
          if (!m_tcpClient.Connected) {
            log.Error ($"Start: not connected to {this.Address}:{this.PortNumber}");
            return false;
          }
          m_tcpClient.Client.SetSocketOption (SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
          m_tcpClient.ReceiveTimeout = m_tcpClient.SendTimeout = this.TimeOutMs;
        }
        if (!m_tcpClient.Connected) {
          log.Error ($"Start: not connected to {this.Address}:{this.PortNumber} after Connect");
          return false;
        }

        for (int i = 0; i < this.ConnectionInitializationAttempts; ++i) {
          var q100 = GetQCommand ("Q100");
          if (q100.StartsWith ("SERIAL NUMBER")) {
            if (log.IsDebugEnabled) {
              log.Debug ($"Start: Q100 returned {q100} in iteration #{i} => connected");
            }
            return true;
          }
          else if (q100.StartsWith ("?")) {
            if (log.IsDebugEnabled) {
              log.Debug ($"Start: {q100} received in iteration #{i} => initialization");
            }
          }
          else {
            log.Error ($"Start: unexpected result={q100}");
            throw new Exception ("Invalid response for Q100");
          }
        }
        log.Error ($"Start: max connection initialization attempts {this.ConnectionInitializationAttempts} reached => return false");
        return false;
      }
      catch (SocketException ex) {
        log.Error ($"Start: socket exception with code {ex.SocketErrorCode}", ex);
        throw;
      }
      catch (Exception ex) {
        log.Error ($"Start: exception", ex);
        throw;
      }
    }

    /// <summary>
    /// Finish method
    /// </summary>
    public void Finish ()
    {
      if (null != m_stream) {
        try {
          m_stream.Dispose ();
          m_stream = null;
        }
        catch (Exception ex) {
          log.Fatal ($"Finish: Dispose of stream ended in exception", ex);
        }
      }

      if (this.ResetTcpClient && (null != m_tcpClient)) {
        try {
          m_tcpClient?.Dispose ();
        }
        catch (Exception ex) {
          log.Error ($"Finish: dipose of tcp client failed", ex);
        }
        m_tcpClient = null;
      }
    }

    /// <summary>
    /// Result of a Q command
    /// 
    /// If the response starts with ? then an exception is raised
    /// </summary>
    /// <param name="param">QXXX</param>
    /// <returns></returns>
    public string GetValidQCommand (string param)
    {
      var result = GetQCommand (param);
      if (result.StartsWith ("?")) {
        log.Error ($"GetQCommand: {param} is not a valid command");
        throw new Exception ("Invalid command");
      }
      return result;
    }

    /// <summary>
    /// Raw result of a Q command
    /// </summary>
    /// <param name="param">QXXX</param>
    /// <returns></returns>
    public string GetQCommand (string param)
    {
      if (log.IsDebugEnabled) {
        log.Debug ($"GetQCommand: param={param}");
      }

      if (!m_tcpClient.Connected) {
        log.Error ($"GetQCommand: not connected => give up");
        throw new Exception ("Not connected");
      }

      try {
        var request = $"?{param}\r\n";
        string response;
        if (null == this.m_stream) {
          m_stream = m_tcpClient.GetStream ();
          m_stream.ReadTimeout = m_stream.WriteTimeout = this.TimeOutMs;
        }
        response = SendRequest (m_stream, request);

        if (log.IsDebugEnabled) {
          log.Debug ($"GetQCommand: returned string was {response}, connected={m_tcpClient.Connected}");
        }

        if (!response.StartsWith (">")) {
          log.Error ($"GetQCommand: result does not start with >: {response}");
          throw new Exception ("Invalid response");
        }

        var result = response.Substring (1, response.Length - 4);
        if (result.StartsWith (">")) { // There may be a second > at start
          result = result.Substring (1);
        }
        if (log.IsInfoEnabled) {
          log.Info ($"GetQCommand: result of {param} is {result}");
        }
        return result;
      }
      catch (Exception ex) {
        log.Error ($"GetQCommand: exception", ex);
        throw;
      }
    }

    string SendRequest (NetworkStream stream, string request)
    {
      if (!stream.CanWrite) {
        log.Error ($"SendRequest: CanWrite of stream is false");
        throw new InvalidOperationException ("CanWrite=false");
      }
      var data = System.Text.Encoding.ASCII.GetBytes (request);
      stream.Write (data, 0, data.Length);

      if (!stream.CanRead) {
        log.Error ($"SendRequest: CanRead of stream is false");
        throw new InvalidOperationException ("CanRead=false");
      }
      string response = "";
      for (int i = 0; ; ++i) {
        byte[] bytes = new byte[m_tcpClient.ReceiveBufferSize];
        var bytesRead = stream.Read (bytes, 0, (int)m_tcpClient.ReceiveBufferSize);
        response += System.Text.Encoding.ASCII.GetString (bytes, 0, bytesRead);
        if (response.EndsWith ("\r\n>")) {
          break;
        }
        log.Warn ($"SendRequest: response is not complete yet in iteration #{i}, response={response}");
      }

      return response;
    }

    /// <summary>
    /// Set a variable
    /// </summary>
    /// <param name="param">variable name</param>
    /// <param name="data"></param>
    public void SetVariable (string param, double data)
    {
      var result = GetQCommand ($"E{param} {data}");
      if (log.IsDebugEnabled) {
        log.Debug ($"SetVariable: result={result} for {param}={data}");
      }
    }

    /// <summary>
    /// Result of a valid Q command without the header/title
    /// 
    /// If the response starts with ? then an exception is raised
    /// </summary>
    /// <param name="param">QXXX</param>
    /// <returns></returns>
    public string GetNoHeader (string param)
    {
      var full = GetValidQCommand (param);
      var split = full.Split (new string[] { ", " }, 2, StringSplitOptions.None);
      if (split.Length < 2) {
        log.Error ($"GetNoHeader: no header in {full}");
        throw new Exception ("Missing header");
      }
      if (log.IsDebugEnabled) {
        log.Debug ($"GetNoHeader: return {split[1]} for {param}, full={full}");
      }
      return split[1];
    }

    /// <summary>
    /// Make a request to the Machine Data Collection module and get a part of it.
    /// Consider ', ' for the CSV separator.
    /// 
    /// parameter is made of the request (Qxxx), followed by a separator (:) and the position (from 0, which is the header)
    /// 
    /// For example: Q500:1
    /// </summary>
    /// <param name="parameter">request:x where request is the request and x the position in the CSV response</param>
    /// <returns></returns>
    public string GetSubString (string parameter)
    {
      string[] parameters = parameter.Split (new char[] { ':' }, 2);
      if (1 == parameters.Length) { // The separator is not found, consider here we want the full response
        if (log.IsDebugEnabled) {
          log.Debug ($"GetSubString ({parameter}): no separator ':' => consider {parameter} for the request and return the full string");
        }
        return GetValidQCommand (parameter);
      }
      else if (2 != parameters.Length) {
        log.Fatal ($"GetSubString ({parameter}): unexpected parameters length");
        throw new InvalidOperationException ("invalid parameters length");
      }
      else { // 2 == parameters.Length
        string request = parameters[0];
        int position;
        try {
          position = int.Parse (parameters[1]);
        }
        catch (Exception) {
          log.Error ($"GetSubString ({parameter}): invalid column number {parameters[1]}");
          throw;
        }
        string response = GetValidQCommand (request);
        string[] items = response.Split (new string[] { ", " }, StringSplitOptions.None);
        return items[position];
      }
    }

    /// <summary>
    /// Get the string value of a corresponding request without the header and trim it
    /// </summary>
    /// <param name="param">request</param>
    /// <returns></returns>
    public string GetString (string param)
    {
      return GetNoHeader (param).Trim ();
    }

    /// <summary>
    /// Get the string value of a corresponding request and trim it
    /// </summary>
    /// <param name="param">request:x where x is the position in the CSV response</param>
    /// <returns></returns>
    public string GetStringX (string param)
    {
      return this.GetSubString (param).Trim ();
    }

    /// <summary>
    /// Get the int value of a corresponding request without the header
    /// </summary>
    /// <param name="param">request</param>
    /// <returns></returns>
    public int GetInt (string param)
    {
      return int.Parse (this.GetNoHeader (param).Trim ());
    }

    /// <summary>
    /// Get the int value of a corresponding request
    /// </summary>
    /// <param name="param">request:x where x is the position in the CSV response</param>
    /// <returns></returns>
    public int GetIntX (string param)
    {
      return int.Parse (this.GetSubString (param).Trim ());
    }

    /// <summary>
    /// Get a double value and round it to the closest integer without the header
    /// </summary>
    /// <param name="param">request</param>
    /// <returns></returns>
    public int GetRounded (string param)
    {
      return (int)Math.Round (GetDouble (param));
    }

    /// <summary>
    /// Get a double value and round it to the closest integer
    /// </summary>
    /// <param name="param">request:x where x is the position in the CSV response</param>
    /// <returns></returns>
    public int GetRoundedX (string param)
    {
      return (int)Math.Round (GetDoubleX (param));
    }

    /// <summary>
    /// Get the long value of a corresponding request with no header
    /// </summary>
    /// <param name="param">request</param>
    /// <returns></returns>
    public long GetLong (string param)
    {
      return long.Parse (this.GetNoHeader (param).Trim ());
    }

    /// <summary>
    /// Get the long value of a corresponding request
    /// </summary>
    /// <param name="param">request:x where x is the position in the CSV response</param>
    /// <returns></returns>
    public long GetLongX (string param)
    {
      return long.Parse (this.GetSubString (param).Trim ());
    }

    /// <summary>
    /// Get the double value of a corresponding request with no header
    /// </summary>
    /// <param name="param">request</param>
    /// <returns></returns>
    public double GetDouble (string param)
    {
      return double.Parse (this.GetNoHeader (param).Trim (), CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Get the double value of a corresponding request
    /// </summary>
    /// <param name="param">request:x where x is the position in the CSV response</param>
    /// <returns></returns>
    public double GetDoubleX (string param)
    {
      return double.Parse (this.GetSubString (param).Trim (), CultureInfo.InvariantCulture);
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
      string motionTime = GetNoHeader ("Q301");
      if (null == m_previousMotionTime) {
        if (log.IsDebugEnabled) {
          log.Debug ($"GetRunningFromMotionTime: initial value is {motionTime}");
        }
        m_previousMotionTime = motionTime;
        throw new Exception ("GetRunningFromMotionTime: initialization");
      }
      else if (object.Equals (m_previousMotionTime, motionTime)) {
        if (log.IsDebugEnabled) {
          log.Debug ($"GetRunningFromMotionTime: same motion time {motionTime} => return false");
        }
        return false;
      }
      else {
        if (log.IsDebugEnabled) {
          log.Debug ($"GetRunningFromMotionTime: the motion time changed from {m_previousMotionTime} to {motionTime} => return true");
        }
        m_previousMotionTime = motionTime;
        return true;
      }
    }

    /// <summary>
    /// Get a set of cnc variables.
    /// </summary>
    /// <param name="param">ListString (first character is the separator)</param>
    /// <returns></returns>
    public IDictionary<string, object> GetCncVariableSet (string param)
    {
      var macroVariableNumbers = Lemoine.Collections.EnumerableString.ParseListString (param)
        .Distinct ();
      var result = new Dictionary<string, object> ();
      foreach (var macroVariableNumber in macroVariableNumbers) {
        try {
          result[macroVariableNumber] = GetDouble ($"Q600 {macroVariableNumber}");
        }
        catch (Exception ex) {
          log.Error ($"GetCncVariableSet: exception for macro {macroVariableNumber}", ex);
        }
      }
      return result;
    }

    /// <summary>
    /// Get a boolean value from a system variable
    /// </summary>
    /// <param name="param">system variable</param>
    /// <returns></returns>
    public bool GetBoolSystemVariable (string param)
    {
      var rounded = GetRounded ($"Q600 {param}");
      return 1 == rounded;
    }
  }
}
