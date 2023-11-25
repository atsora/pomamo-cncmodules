// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Lemoine.Core.Log;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Description of DElectronCommand.
  /// </summary>
  public class DElectronCommand
  {
    static readonly string HAND_SHAKE_MAX_ERRORS_KEY = "Cnc.DElectronEPS.HandShakeMaxErrors"; // Before reconnection
    static readonly int HAND_SHAKE_MAX_ERRORS_DEFAULT = 3;

    #region Members
    readonly IPEndPoint m_ipEndPoint;
    readonly ILog m_log;
    Socket m_socket = null;
    bool m_handShakeOk = false;
    int m_handShakeErrors = 0;
    #endregion // Members

    /// <summary>
    /// Default of the constructor
    /// </summary>
    /// <param name="ipAddress"></param>
    /// <param name="port"></param>
    /// <param name="log"></param>
    public DElectronCommand (string ipAddress, int port, ILog log)
    {
      m_log = log;
      m_ipEndPoint = new IPEndPoint (IPAddress.Parse (ipAddress), port);
    }

    #region Methods
    /// <summary>
    /// Return true if the connexion is successful
    /// </summary>
    /// <returns></returns>
    public bool CheckConnection ()
    {
      if (m_socket == null || !m_socket.Connected) {
        m_handShakeOk = false;
        try {
          // Disconnect the old socket if any and prepare a new socket
          Disconnect ();
          m_socket = new Socket (AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
          m_socket.ReceiveTimeout = 500;
          m_socket.SendTimeout = 500;
          m_socket.Connect (m_ipEndPoint);

          if (m_socket.Connected) {
            if (m_log.IsInfoEnabled) {
              m_log.Info ($"DElectronCommand.CheckConnection: successfully connected to {m_ipEndPoint}");
            }
          }
          else {
            m_log.Error ($"DElectronCommand.CheckConnection: couldn't connect to {m_ipEndPoint}");
            return false;
          }
        }
        catch (Exception ex) {
          m_log.Error ($"DElectronCommand.CheckConnection: Couldn't connect to {m_ipEndPoint}", ex);
          return false;
        }
      }

      if (!m_handShakeOk) {
        // Initial handshake
        try {
          var answer = Query (0, null); // Command number 0: connection request
          if (answer.Count () != 1) {
            m_log.Error ($"DElectronCommand.CheckConnection: Initial handshake doesn't return 1 value but {answer.Count ()}");
            return false;
          }
          if (m_log.IsInfoEnabled) {
            var protocolVersion = answer.First ();
            m_log.Info ($"DElectronCommand.CheckConnection: protocol version is {protocolVersion}");
          }
          m_handShakeOk = true;
          m_handShakeErrors = 0;
        }
        catch (Exception ex) {
          m_log.Error ($"DElectronCommand.CheckConnection: Couldn't handshake {m_ipEndPoint}", ex);
          ++m_handShakeErrors;
          var handShakeMaxErrors = Lemoine.Info.ConfigSet.LoadAndGet (HAND_SHAKE_MAX_ERRORS_KEY, HAND_SHAKE_MAX_ERRORS_DEFAULT);
          if (handShakeMaxErrors <= m_handShakeErrors) {
            m_log.Error ($"DElectronCommand.CheckConnection: max hand shake error {handShakeMaxErrors} reached => disconnect");
            Disconnect ();
          }
          return false;
        }
      }

      return true;
    }

    /// <summary>
    /// Disconnect the socket
    /// </summary>
    public void Disconnect ()
    {
      if (m_socket != null && m_socket.Connected) {
        if (m_log.IsDebugEnabled) {
          m_log.Debug ("DElectronCommand.Disconnect");
        }
        m_socket.Disconnect (false);
      }
    }

    /// <summary>
    /// Get the values from the variable names
    /// Possible commands:
    /// -  2: read PLC values
    /// - 16: read special values format X|Y|Z
    ///       X = query
    ///       Y = attribute
    ///       Z = sub attribute
    /// - 17: ?
    /// - 21: overrides, possible variables
    ///       1|0|0 = feedrate
    ///       2|0|0 = speed
    ///       3|0|0 = rapid traverse
    /// </summary>
    /// <param name="command"></param>
    /// <param name="variableNames"></param>
    /// <returns></returns>
    public IEnumerable<string> GetValues (int command, ICollection<string> variableNames)
    {
      // Prepare the messages
      var messages = new List<string> ();
      foreach (var variableName in variableNames) {
        messages.Add (variableName);
        messages.Add ("0"); // Process: should be 0 according to the documentation
      }

      // Answers
      return Query (command, messages);
    }

    readonly byte[] m_buffer = new byte[1024]; // buffer for incoming data
    IEnumerable<string> Query (int commandNumber, ICollection<string> messages)
    {
      // The socket must be connected
      if (m_socket == null || !m_socket.Connected) {
        string txt = $"Cannot query {commandNumber} because the socket is not connected!";
        m_log.Error ($"DElectronCommand.Query: {txt}");
        throw new Exception (txt);
      }

      // Prepare the query
      string message = (messages == null || messages.Count == 0) ?
        $"{commandNumber}||\n" :
        $"{commandNumber}|{string.Join ("|", messages.ToArray ())}||\n";

      // Send the message
      byte[] msg = Encoding.UTF8.GetBytes (message);
      if (m_socket.Send (msg, msg.Length, SocketFlags.None) == 0) {
        m_log.Info ($"DElectronCommand.Query: nothing was sent");
        throw new Exception ("DElectronCommand.Query: no data has been sent");
      }

      return GetResponse (commandNumber);
    }

    IEnumerable<string> GetResponse (int commandNumber)
    {
      // Receive the response from the server
      int bytesRec = m_socket.Receive (m_buffer);
      string answer = Encoding.ASCII.GetString (m_buffer, 0, bytesRec);
      if (m_log.IsInfoEnabled) {
        m_log.Info ($"DElectronCommand.GetResponse: successfully received {answer.TrimEnd ('\n')} for {commandNumber}");
      }

      // Cut the answer
      var answers = answer.Split ('|');

      if (answers.Length < 3) {
        m_log.Error ($"DElectronCommand.GetResponse: length of {answer.TrimEnd ('\n')} is too short (< 3)");
        throw new Exception ("DElectronCommand.GetResponse: bad response, not enough element");
      }

      var returnedCommandNumber = answers[0];
      if (returnedCommandNumber.Equals ("-1")) {
        m_log.Error ($"DElectronCommand.GetResponse: -1 (error) with message {answers[1]} is returned for command {commandNumber}, response is {answer.TrimEnd ('\n')}");
        ProcessError (answers[1]); // Throws an exception
        throw new Exception ($"DElectronEPS: {answers[1]}");
      }
      else if (0 == commandNumber) { // 1 is expected, Command 0 returns 1
        if (returnedCommandNumber.Equals ("1")) {
          if (m_log.IsInfoEnabled) {
            m_log.Info ($"DElectronCommand.GetResponse: got {answer} for command 0");
          }
          return FilterResponse (answers);
        }
        else { // Not 1 
          m_log.Error ($"DElectronCommand.GetResponse: invalid returned command number {returnedCommandNumber} for command 0");
          throw new Exception ($"DElectronCommand.GetResponse: invalid returned command number for command 0");
        }
      }
      else if (returnedCommandNumber.Equals (commandNumber.ToString ())) {
        if (m_log.IsInfoEnabled) {
          m_log.Info ($"DElectronCommand.GetResponse: got {answer} for command {commandNumber}");
        }
        return FilterResponse (answers);
      }
      else { // wrong command number => retry
        m_log.Warn ($"DElectronCommand.GetResponse: returned command number {returnedCommandNumber} does not match {commandNumber} => retry");
        return GetResponse (commandNumber);
      }
    }

    IEnumerable<string> FilterResponse (string[] response)
    {
      // Remove 3 elements at the end: the comment, an empty string and '\n'
      // and remove the command number
      // In c#8: return response[1..^3];
      var length = response.Length;
      return response.Take (length - 3).Skip (1);
    }

    string ConvertErrorToString (string errorNumber)
    {
      switch (errorNumber) {
      case "0":
        return "invalid packet";
      case "1":
        return "invalid command";
      case "2":
        return "invalid parameter";
      case "3":
        return "network error when connecting to Z32";
      case "4":
        return "Z32 command error";
      case "5":
        return "CMOS error";
      case "6":
        return "file not found";
      case "7":
        return "tool not found";
      default:
        return $"unknown error {errorNumber}";
      }
    }

    void ProcessError (string error)
    {
      string errorString = ConvertErrorToString (error);
      m_log.Error ($"DElectronCommand.ProcessError: {error} => {errorString}");
      throw new Exception ($"DElectronEPS: {error} - {errorString}");
    }
    #endregion // Methods
  }
}
