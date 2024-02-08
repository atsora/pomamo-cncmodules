// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using Lemoine.Cnc.Module.Brother;
using Lemoine.Core.Log;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Module to get data from a Brother machine using the TCP protocol
  /// </summary>
  public sealed class BrotherTcp
    : Lemoine.Cnc.BaseCncModule, Lemoine.Cnc.ICncModule, IDisposable
  {
    TcpClient m_tcpClient = null;
    NetworkStream m_stream = null;
    readonly IDictionary<string, IList<string>> m_responseByQuery = new Dictionary<string, IList<string>> ();
    bool m_dataRequested = false;
    bool m_couldReadOne = true;
    IList<string> m_programHeaderCache = null;
    string m_programHeaderProgramName = null;

    readonly CncAlarmBuilder m_cncAlarmBuilder = new CncAlarmBuilder ();
    readonly ToolLifeBuilder m_toolLifeBuilder = new ToolLifeBuilder ();

    #region Getters / Setters
    /// <summary>
    /// Host name or IP
    /// </summary>
    public string HostOrIP { get; set; }

    /// <summary>
    /// Port
    /// </summary>
    public int Port { get; set; } = 10000;

    /// <summary>
    /// Duration in which a response can come, following a query
    /// By default 200 ms
    /// </summary>
    public int TimeOutMs { get; set; } = 200;

    /// <summary>
    /// "B" for B00 machines or "C" for C00 machines
    /// </summary>
    public string MachineType { get; set; }

    /// <summary>
    /// ProgramName
    /// </summary>
    public string ProgramName { get; set; }

    /// <summary>
    /// Acquisition error
    /// </summary>
    public bool AcquisitionError => m_dataRequested && !m_couldReadOne;

    /// <summary>
    /// Reset the TCP Client each iteration
    /// </summary>
    public bool ResetTcpClient { get; set; } = false;
    #endregion // Getters / Setters

    #region Constructors / Destructor / ToString methods
    /// <summary>
    /// Description of the constructor
    /// </summary>
    public BrotherTcp ()
      : base ("Lemoine.Cnc.In.BrotherTcp")
    {
    }

    /// <summary>
    /// <see cref="IDisposable.Dispose" />
    /// </summary>
    public void Dispose ()
    {
      if (null != m_stream) {
        try {
          m_stream?.Dispose ();
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
          log.Fatal ($"Dispose: Close of tcpclient ended with an exception", ex);
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
      if (string.IsNullOrEmpty (this.HostOrIP)) {
        log.Info ($"Start: no host or ip => do nothing");
        return false;
      }

      m_dataRequested = false;
      m_couldReadOne = false;

      // Reset the stored responses
      m_responseByQuery.Clear ();

      // Reset the TCP Connection if needed
      try {
        if ((null != m_tcpClient) && !m_tcpClient.Connected) {
          if (log.IsInfoEnabled) {
            log.Info ($"Start: not connected to {this.HostOrIP}:{this.Port} => reset tcp client");
          }
          m_tcpClient?.Dispose ();
          m_tcpClient = null;
        }
      }
      catch (Exception ex) {
        log.Error ($"Start: exception while trying to reset the connection", ex);
      }

      // Check the TCP connection
      try {
        if (m_tcpClient is null) {
          if (log.IsDebugEnabled) {
            log.Debug ($"Start: create tcp client {this.HostOrIP}:{this.Port}");
          }
          m_tcpClient = new TcpClient (this.HostOrIP, this.Port);
        }
        if (log.IsWarnEnabled && !m_tcpClient.Connected) {
          log.Warn ($"Start: not connected to {this.HostOrIP}:{this.Port}");
        }

        return true;
      }
      catch (SocketException ex) {
        log.Error ($"Start: socket exception with code {ex.SocketErrorCode}", ex);
        throw;
      }
      catch (Exception ex) {
        log.Error ($"Start: Cannot open a connection", ex);
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
          log.Fatal ($"Finish: dispose of stream ended in exception", ex);
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

    string FormatPacket (string command, string function, string message, string data)
    {
      // Start the packet with "C" for "command", and the command on 3 characters
      string packet = "C" + command;
      for (int i = 0; i < 3 - command.Length; i++) {
        packet += " ";
      }

      // Add the function on 4 characters
      packet += function;
      for (int i = 0; i < 4 - function.Length; i++) {
        packet += " ";
      }

      // Add the message on 8 characters
      packet += message;
      for (int i = 0; i < 8 - message.Length; i++) {
        packet += " ";
      }

      // Add the completion code and a new line
      packet += "00\r";

      // Possibly add data
      if (!string.IsNullOrEmpty (data)) {
        packet += "\n" + data + "\r";
      }

      // Compute the footer (\r is inside the checksum but not the \n)
      packet += "\n" + ComputeChecksum (packet);

      // Add "%" before and after and return the finalized packet
      if (log.IsDebugEnabled) {
        log.Debug ($"FormatPacket: full packet is %{FormatEndOfLine (packet)}%");
      }
      return "%" + packet + "%";
    }

    string FormatEndOfLine (string packet)
    {
      return packet.Replace ("\r", "{CR}").Replace ("\n", "{LF}");
    }

    string ComputeChecksum (string msg)
    {
      int sum = 0;
      for (int i = 0; i < msg.Length; i++) {
        sum += msg[i];
      }

      var result = (sum % 16).ToString ("00");
      if (log.IsDebugEnabled) {
        log.Debug ($"ComputeChecksum: checksum is {result} for {msg}");
      }
      return result;
    }

    string Query (string packet)
    {
      m_dataRequested = true;

      if ((m_tcpClient is null) || !m_tcpClient.Connected) {
        log.Error ($"Query: not connected => error");
        throw new Exception ("Not connected");
      }

      var encoding = Encoding.ASCII;
      var totalTime = 0;
      try {
        var result = "";

        // Encode the data string into a byte array.
        byte[] msg = encoding.GetBytes (packet);
        if (log.IsDebugEnabled) {
          log.Debug ($"Query: Sending the ASCII packet: {FormatEndOfLine (packet)}");
        }

        // Prepare a stream
        if (null == m_stream) {
          m_stream = m_tcpClient.GetStream ();
          m_stream.ReadTimeout = m_stream.WriteTimeout = TimeOutMs;
        }

        // Flush remaining data if any
        var inputBuffer = new byte[m_tcpClient.ReceiveBufferSize];

        // Send the packet through the TCP client
        m_stream.Write (msg, 0, msg.Length);

        // Receive the response from the remote device
        var partialTime = 0;
        var bytesRead = 0;
        bool firstTime = true;
        do {
          if (firstTime) {
            firstTime = false;
          }
          else {
            System.Threading.Thread.Sleep (10);
            totalTime += 10;
            partialTime += 0;
          }
          if (m_stream.DataAvailable) {
            bytesRead = m_stream.Read (inputBuffer, 0, inputBuffer.Length);
            var stringTmp = encoding.GetString (inputBuffer, 0, bytesRead);
            result += stringTmp;

            if (bytesRead > 0) {
              partialTime = 0; // Reset the timeout
              m_couldReadOne = true;
            }
          }
          else {
            bytesRead = 0;
          }

          // Continue while:
          // there is data OR
          // we are in the timeout and the result doesn't end by a '%'
        }
        while (bytesRead > 0 || ((string.IsNullOrEmpty (result) || result[result.Length - 1] != '%') && partialTime < TimeOutMs));
        if (log.IsDebugEnabled) {
          log.Debug ($"Query: response is {result} before parsing");
        }

        // Extract the answer (the result can contain the last part of the previous answer)
        int lastPos = result.LastIndexOf ('%');
        int firstPos = result.IndexOf ('%');
        if (firstPos == lastPos) {
          log.Error ($"Query: After {totalTime}ms came the machine answer with wrong '%' number: {FormatEndOfLine (result)}");
          return "";
        }
        else {
          // Find the '%' before the last one
          int nextPos;
          while ((nextPos = result.IndexOf ('%', firstPos + 1)) != lastPos) {
            firstPos = nextPos;
          }

          // Possibly trim the result
          if (firstPos != 0 || lastPos != result.Length - 1) {
            log.Warn ($"Query: After {totalTime}ms came a machine answer with too many '%': {FormatEndOfLine (result)}");
            result = result.Substring (firstPos, lastPos - firstPos + 1);
            if (log.IsInfoEnabled) {
              log.Info ($"Query: result after extraction (too many '%') is {result}");
            }
          }
        }

        if (log.IsDebugEnabled) {
          log.Debug ($"Query: After {totalTime}ms came the machine answer: {FormatEndOfLine (result)}");
        }
        return result;
      }
      catch (ArgumentNullException ex) {
        log.Error ("Query: ArgumentNullException", ex);
        throw;
      }
      catch (SocketException ex) {
        log.Error ("Query: SocketException", ex);
        throw;
      }
      catch (Exception ex) {
        log.Error ("Query: Unexpected exception", ex);
        throw;
      }
    }

    IList<string> ProcessResponse (string response)
    {
      if (string.IsNullOrEmpty (response)) {
        log.Error ($"ProcessResponse: empty response argument");
        throw new ArgumentNullException ("Empty response argument", "response");
      }

      var parts = response.Replace ("\r", "").Split ('\n');

      // Check the completion code
      if (parts[0].Length != 19) {
        log.Error ($"ProcessResponse: invalid length of the first element {parts[0]} (should be 19)");
        throw new ArgumentException ("Invalid length of the first element", "response");
      }

      if (!int.TryParse (parts[0].Substring (17), out int completionCode)) {
        log.Error ($"ProcessResponse: invalid completion code {parts[0].Substring (17)} in {parts[0]}");
        throw new Exception ("Invalid completion code");
      }

      if (completionCode != 0) {
        log.Error ($"ProcessResponse: completion code {0} is no 0 => raise an exception");
        throw new TCPCompletionCodeException (completionCode);
      }

      var result = new List<string> ();
      for (int i = 1; i < parts.Length - 1; i++) {
        result.Add (parts[i].Trim (' '));
      }

      return result;
    }

    /// <summary>
    /// Read a string with the TCP protocol type 2
    /// </summary>
    /// <param name="command"></param>
    /// <param name="function"></param>
    /// <param name="message"></param>
    /// <param name="data"></param>
    /// <param name="lineNumber"></param>
    /// <returns></returns>
    public string GetString (string command, string function, string message, string data = "", int lineNumber = 0)
    {
      // Get the corresponding string list
      var result = GetLines (command, function, message, data);

      // Extract the single result
      if (result.Count == 0) {
        log.Error ($"GetString: empty response");
        throw new Exception ("Empty response");
      }

      if (lineNumber > result.Count) {
        log.Error ($"GetString: invalid line number {lineNumber}, {result.Count} lines available");
        throw new Exception ("Invalid line number");
      }

      return result[lineNumber];
    }

    /// <summary>
    /// Read the first string containing the specified string with the TCP protocol type 2
    /// </summary>
    /// <param name="command"></param>
    /// <param name="function"></param>
    /// <param name="message"></param>
    /// <param name="filter"></param>
    /// <returns></returns>
    string GetFiltered (string command, string function, string message, string filter)
    {
      var lines = GetLines (command, function, message, "");
      return lines.First (x => x.Contains (filter));
    }

    /// <summary>
    /// Read content like the method ManagerFTP.GetSymbolListContent
    /// Read all symbols associated to a string list
    /// </summary>
    /// <param name="command"></param>
    /// <param name="function"></param>
    /// <param name="message"></param>
    /// <param name="data"></param>
    /// <returns></returns>
    public IDictionary<string, IList<string>> GetSymbolListContent (string command, string function, string message, string data)
    {
      // Get the corresponding string list
      var lines = GetLines (command, function, message, data);

      // Process the lines
      var result = new Dictionary<string, IList<string>> ();
      foreach (var line in lines) {
        var parts = line.Split (',');
        if (parts.Length > 1 && !string.IsNullOrEmpty (parts[0])) {
          if (!result.ContainsKey (parts[0])) {
            result[parts[0]] = new List<string> ();
          }

          for (int i = 1; i < parts.Length; i++) {
            result[parts[0]].Add (parts[i].Trim (' '));
          }
        }
      }
      return result;
    }

    /// <summary>
    /// Read a list of strings with the TCP protocol type 2
    /// (each element being on a new line)
    /// </summary>
    /// <param name="command"></param>
    /// <param name="function"></param>
    /// <param name="message"></param>
    /// <param name="data"></param>
    /// <returns></returns>
    IList<string> GetLines (string command, string function, string message, string data)
    {
      if ((m_tcpClient is null) || !m_tcpClient.Connected) {
        log.Error ($"GetLines: currently disconnected");
        throw new Exception ("Disconnected");
      }

      // Build a packet
      string packet = FormatPacket (command, function, message, data);

      // Ask the machine only if the answer is not here yet
      if (!m_responseByQuery.ContainsKey (packet)) {
        string response = Query (packet);
        m_responseByQuery[packet] = ProcessResponse (response);
        log.Info ($"GetLines: Successful query with '{command}|{function}|{message}|{data}'");
      }

      return m_responseByQuery[packet];
    }

    /// <summary>
    /// Read a string with the TCP protocol type 2
    /// </summary>
    /// <param name="param">format can be
    ///    {command type: 3 characters}|{function: max 4 characters}|{message: max 8 characters}
    /// or {command type: 3 characters}|{function: max 4 characters}|{message: max 8 characters}|{data}
    /// possibly followed by ~{position}-{length} for extracting a substring</param>
    /// <returns></returns>
    public string GetString (string param)
    {
      string result;

      try {
        // Initialize parameters for searching a string
        var substringPosition = 0;
        var substringLength = 0;

        // Search required?
        var lastIndex = param.LastIndexOf ('~');
        if (lastIndex != -1) {
          var paramPart2 = param.Substring (lastIndex + 1).Split ('-');

          if (paramPart2.Length != 2) {
            log.Error ($"GetString: invalid length in {param.Substring (lastIndex + 1)}");
          }
          else {
            if (!int.TryParse (paramPart2[0], out substringPosition) || !int.TryParse (paramPart2[1], out substringLength)) {
              log.Error ($"GetString: one of the substring instruction is not an integer in {param}");
              throw new ArgumentException ("Invalid substring instruction", "param");
            }
          }
          param = param.Substring (0, lastIndex);
        }

        var parts = param.Split ('|');
        if (parts.Length < 3 || parts.Length > 4 || parts[0].Length > 3 || parts[1].Length > 4 || parts[2].Length > 8) {
          log.Error ($"GetString: invalid length in {param}");
          throw new ArgumentException ("Invalid length in one element", "param");
        }

        result = GetString (parts[0], parts[1], parts[2], parts.Length == 4 ? parts[3] : "", 0);

        // Substring?
        if (substringLength != 0) {
          if (substringLength + substringPosition > result.Length) {
            log.Error ($"GetString: Couldn't extract a substring in {result} because position is {substringPosition} and length is {substringLength}");
            throw new Exception ($"Invalid substring extraction");
          }
          else {
            return result.Substring (substringPosition, substringLength);
          }
        }

        return result;
      }
      catch (Exception ex) {
        log.Error ($"GetString: param={param}, exception", ex);
        throw;
      }
    }

    /// <summary>
    /// Read an int with the TCP protocol type 2
    /// </summary>
    /// <param name="param">cf GetTCPString</param>
    /// <returns></returns>
    public int GetInt (string param)
    {
      try {
        return int.Parse (GetString (param));
      }
      catch (Exception ex) {
        log.Error ($"GetInt: param={param}, exception", ex);
        throw;
      }
    }

    /// <summary>
    /// Read a double with the TCP protocol type 2
    /// </summary>
    /// <param name="param">cf GetTCPString</param>
    /// <returns></returns>
    public double GetDouble (string param)
    {
      try {
        return double.Parse (GetString (param));
      }
      catch (Exception ex) {
        log.Error ($"GetDouble: param={param}, exception", ex);
        throw;
      }
    }

    /// <summary>
    /// Read a boolean with the TCP protocol type 2
    /// </summary>
    /// <param name="param">cf GetTCPString</param>
    /// <returns></returns>
    public bool GetBool (string param)
    {
      try {
        string tmp = GetString (param);
        if (tmp == "0" || tmp == "OFF") {
          return false;
        }
        else if (tmp == "1" || tmp == "ON") {
          return true;
        }
        else {
          log.Error ($"GetBool: invalid boolean value {tmp}");
          throw new InvalidCastException ("Invalid boolean value");
        }
      }
      catch (Exception ex) {
        log.Error ($"GetBool: param={param}, exception", ex);
        throw;
      }
    }

    /// <summary>
    /// Read PrograName with the TCP protocol type 2 ans store it for part and operation detection
    /// </summary>
    /// <param name="param">cf GetTCPString</param>
    /// <returns></returns>
    public string GetProgramName (string param)
    {
      var result = GetString (param);
      ProgramName = result;
      return result;
    }

    /// <summary>
    /// Read PrograName from a file with the TCP protocol type 2 ans store it for part and operation detection
    /// </summary>
    /// <param name="param">cf GetTCPStringFromFile. If not set, MEM|A01|0 is used</param>
    /// <returns></returns>
    public string GetProgramNameFromFile (string param)
    {
      var result = GetStringFromFile (string.IsNullOrEmpty (param) ? "MEM|A01|0" : param);
      this.ProgramName = result;
      return result;
    }

    /// <summary>
    /// Get the program content after:
    /// <item>setting ProgramName</item>
    /// <item>or calling GetProgramName or GetProgramNameFromFile</item>
    /// </summary>
    /// <param name="param"></param>
    /// <returns></returns>
    public IList<string> GetProgramContentLines (string _)
    {
      try {
        if (string.IsNullOrEmpty (this.ProgramName)) {
          log.Error ($"GetProgramCommentLines: ProgramName is unknown");
          throw new Exception ($"Unknown program name");
        }
        else {
          return GetLines ("LOD", "", $"O{this.ProgramName}", "");
        }
      }
      catch (Exception ex) {
        log.Error ($"GetProgramCommentLines: exception", ex);
        throw;
      }
    }

    /// <summary>
    /// Get the program content after:
    /// <item>setting ProgramName</item>
    /// <item>or calling GetProgramName or GetProgramNameFromFile</item>
    /// </summary>
    /// <param name="param"></param>
    /// <returns></returns>
    public string GetProgramContent (string param) => string.Join ("\n", GetProgramContentLines (param));

    /// <summary>
    /// Get the program header (this is cached while the program name remains the same)
    /// </summary>
    /// <param name="param">number of lines to get. If empty, consider 10 lines</param>
    /// <returns></returns>
    public IList<string> GetProgramHeader (string param)
    {
      if ((null != m_programHeaderProgramName) && (null != m_programHeaderCache)
        && !string.IsNullOrEmpty (this.ProgramName)
        && string.Equals (m_programHeaderProgramName, this.ProgramName, StringComparison.InvariantCultureIgnoreCase)) {
        if (log.IsDebugEnabled) {
          log.Debug ($"GetProgramHeader: get the header in cache for program name={this.ProgramName}");
        }
        return m_programHeaderCache;
      }
      else {
        int lines;
        if (string.IsNullOrEmpty (param)) {
          log.Debug ($"GetProgramHeader: param is empty, consider 10 lines");
          lines = 10;
        }
        else {
          lines = int.Parse (param);
        }
        m_programHeaderProgramName = null;
        m_programHeaderCache = GetProgramContentLines ("").Take (lines).ToList ();
        m_programHeaderProgramName = this.ProgramName;
        return m_programHeaderCache;
      }
    }

    /// <summary>
    /// Read a list of string from a file with the TCP protocol type 2
    /// </summary>
    /// <param name="param">format: {file with no extension}|{symbol}</param>
    /// <returns></returns>
    public IList<string> GetStringListFromFile (string param)
    {
      try {
        // Check the parameters
        var parts = param.Split ('|');
        if (parts.Length != 2) {
          log.Error ($"GetTCPStringListFromFile: invalid number of elements in {param} (not 2)");
          throw new ArgumentException ("Invalid number of elements", "param");
        }

        if (string.IsNullOrEmpty (parts[0])) {
          log.Error ($"GetTCPStringListFromFile: 1st element FileName is empty in {param}");
          throw new ArgumentException ("Invalid first element Filename", "param");
        }

        if (string.IsNullOrEmpty (parts[1])) {
          log.Error ($"GetTCPStringListFromFile: 2nd element symbol is empty in {param}");
          throw new ArgumentException ("Invalid second element symbol", "param");
        }

        // Get the content of the file
        var fileContent = GetSymbolListContent ("LOD||" + parts[0]);

        // Check that the symbol and position are ok
        if (!fileContent.ContainsKey (parts[1])) {
          log.Error ($"GetTCPStringListFromFile: symbol {parts[1]} not found in {parts[0]}");
          throw new Exception ("Symbol not found in file");
        }

        // Load the result
        return fileContent[parts[1]];
      }
      catch (Exception ex) {
        log.Error ($"GetTCPStringListFromFile: param={param}, exception", ex);
        throw;
      }
    }

    /// <summary>
    /// Read a list of int from a file with the TCP protocol type 2
    /// </summary>
    /// <param name="param">format: {file with no extension}|{symbol}</param>
    /// <returns></returns>
    public IList<int> GetIntListFromFile (string param)
    {
      try {
        return GetStringListFromFile (param)
          .Where (x => !string.IsNullOrEmpty (x))
          .Select (x => int.Parse (x))
          .ToList ();
      }
      catch (Exception ex) {
        log.Error ($"GetTCPIntListFromFile: param={param}, exception", ex);
        throw;
      }
    }

    /// <summary>
    /// Read a string from a file with the TCP protocol type 2
    /// </summary>
    /// <param name="param">format: {file with no extension}|{symbol}|{position}</param>
    /// <returns></returns>
    public string GetStringFromFile (string param)
    {
      try {
        // Check the parameters
        var parts = param.Split ('|');
        if (parts.Length != 3) {
          log.Error ($"GetTCPStringFromFile: invalid number of elements in param {param} (not 3)");
          throw new ArgumentException ($"Invalid number of elements", "param");
        }

        int position = -1;
        if (!int.TryParse (parts[2], out position)) {
          log.Error ($"GetTCPStringFromFile: 3rd element is not a number in {param}");
          throw new ArgumentException ($"Third element is not a number", "param");
        }

        // Get the string list
        var stringList = GetStringListFromFile (parts[0] + "|" + parts[1]);

        // Check that the position is ok
        if (position > stringList.Count) {
          log.Error ($"GetTCPStringFile: invalid position element in {param}, position={position} stringList.Count={stringList.Count}");
          throw new Exception ("Symbol '" + parts[1] + "' has only " + stringList.Count + " position(s) and position " + position + " is required");
        }

        // Load the result
        if (log.IsInfoEnabled) {
          log.Info ($"GetTCPStringFromFile: return {stringList[position]} for {param}");
        }
        return stringList[position];
      }
      catch (Exception ex) {
        log.Error ($"GetTCPStringFromFile: param={param}, exception", ex);
        throw;
      }
    }

    /// <summary>
    /// Read an int from a file with the TCP protocol type 2
    /// </summary>
    /// <param name="param">cf GetTCPStringFromFile</param>
    /// <returns></returns>
    public int GetIntFromFile (string param)
    {
      try {
        return int.Parse (GetStringFromFile (param));
      }
      catch (Exception ex) {
        log.Error ($"GetTCPIntFromFile: param={param}, exception", ex);
        throw;
      }
    }

    /// <summary>
    /// Read a double from a file with the TCP protocol type 2
    /// </summary>
    /// <param name="param">cf GetTCPStringFromFile</param>
    /// <returns></returns>
    public double GetDoubleFromFile (string param)
    {
      try {
        return double.Parse (GetStringFromFile (param));
      }
      catch (Exception ex) {
        log.Error ($"GetTCPDoubleFromFile: param={param}, exception", ex);
        throw;
      }
    }

    /// <summary>
    /// Read a boolean from a file with the TCP protocol type 2
    /// </summary>
    /// <param name="param">cf GetTCPStringFromFile</param>
    /// <returns></returns>
    public bool GetBoolFromFile (string param)
    {
      try {
        var tmp = GetIntFromFile (param);
        if (tmp == 0) {
          return false;
        }
        else if (tmp == 1) {
          return true;
        }
        else {
          log.Error ($"GetTCPBoolFromFile: invalid boolean value {tmp}");
          throw new InvalidCastException ("Invalid boolean value");
        }
      }
      catch (Exception ex) {
        log.Error ($"GetTCPBoolFromFile: param={param}, exception", ex);
        throw;
      }
    }

    IDictionary<string, IList<string>> GetSymbolListContent (string param)
    {
      var parts = param.Split ('|');
      if (parts.Length < 3 || parts.Length > 4 || parts[0].Length > 3 || parts[1].Length > 4 || parts[2].Length > 8) {
        log.Error ($"GetSymbolListContent: invalid length in one element in {param}");
        throw new ArgumentException ($"Invlaid length in one element", "param");
      }

      return GetSymbolListContent (parts[0], parts[1], parts[2], parts.Length == 4 ? parts[3] : "");
    }

    /// <summary>
    /// Read the maintenance notice in a file with the TCP protocol type 2
    /// Equivalent to GetFTPMaintenanceNotice
    /// </summary>
    /// <returns></returns>
    public IList<CncAlarm> MaintenanceNotice
    {
      get {
        IList<CncAlarm> result = new List<CncAlarm> ();

        try {
          var fileContent = GetSymbolListContent ("LOD||MAINTC");
          foreach (var symbol in fileContent.Keys) {
            var parts = fileContent[symbol];
            if (parts.Count != 6) {
              log.Warn ($"GetTCPMaintenanceNotice: Wrong number of parts: {parts.Count} instead of 6 in {parts}");
              break;
            }
            else {
              var alarm = m_cncAlarmBuilder.CreateMaintenanceAlarm (parts[0], parts[1], parts[2], parts[3], parts[4], parts[5]);
              if (alarm != null) {
                result.Add (alarm);
              }
            }
          }
        }
        catch (Exception ex) {
          log.Error ($"GetTCPMaintenanceNotice: exception", ex);
          throw;
        }

        return result;
      }
    }

    /// <summary>
    /// Read the alarms
    /// Equivalent to GetFTPAlarms
    /// </summary>
    public IList<CncAlarm> Alarms
    {
      get {
        IList<CncAlarm> result = new List<CncAlarm> ();

        try {
          switch (this.MachineType) {
            case "B":
              // B00 machines
              var alarmNumbers = GetStringListFromFile ("MEM|E01");
              foreach (var alarmNumber in alarmNumbers) {
                try {
                  var alarm = m_cncAlarmBuilder.CreateAlarmB (alarmNumber);
                  if (alarm != null) {
                    result.Add (alarm);
                  }
                }
                catch (Exception ex) {
                  log.Error ($"GetTCPAlarms.get: exception creating alarm B {alarmNumber} => skip it", ex);
                }
              }
              break;
            case "C":
              // C00 machines
              var fileContent = GetSymbolListContent ("LOD||ALARM");
              foreach (var symbol in fileContent.Keys) {
                var parts = fileContent[symbol];
                foreach (var part in parts) {
                  try {
                    var alarm = m_cncAlarmBuilder.CreateAlarm (part);
                    if (alarm != null) {
                      result.Add (alarm);
                    }
                  }
                  catch (Exception ex) {
                    log.Error ($"GetTCPAlarms.get: skip alarm {part} because of exception", ex);
                  }
                }
              }
              break;
            default:
              log.Error ($"GetTCPAlarms.get: invalid machine type {MachineType}");
              throw new Exception ("Invalid machine type");
          }
        }
        catch (Exception ex) {
          log.Error ($"GetTCPAlarms.get: exception", ex);
          throw;
        }

        return result;
      }
    }

    /// <summary>
    /// Read a set of macros with the metric units
    /// Equivalent to GetFTPMacroSet
    /// </summary>
    /// <param name="param">format: {separator}{macro number 1}{separator}{macro number 2}... For example ",C500,C673,C999"</param>
    /// <returns></returns>
    public IDictionary<string, double> GetMacroSetMetric (string param)
    {
      return GetTCPMacroSet ("M", param);
    }

    /// <summary>
    /// Read a set of macros with the imperial units
    /// Equivalent to GetFTPMacroSet
    /// </summary>
    /// <param name="param">format: {separator}{macro number 1}{separator}{macro number 2}... For example ",C500,C673,C999"</param>
    /// <returns></returns>
    public IDictionary<string, double> GetMacroSetInches (string param)
    {
      return GetTCPMacroSet ("I", param);
    }

    /// <summary>
    /// Read a set of macros
    /// Equivalent to GetFTPMacroSet
    /// </summary>
    /// <param name="unitLetter">M for metric, I for inch, D for current unit system</param>
    /// <param name="param">format: {separator}{macro number 1}{separator}{macro number 2}... For example ",C500,C673,C999"</param>
    /// <returns></returns>
    IDictionary<string, double> GetTCPMacroSet (string unitLetter, string param)
    {
      try {
        var macroNames = Lemoine.Collections.EnumerableString.ParseListString (param);
        return GetTCPMacroSet (unitLetter, macroNames);
      }
      catch (Exception ex) {
        log.Error ($"GetTCPMacroSet: param={param}", ex);
        throw;
      }
    }

    IDictionary<string, double> GetTCPMacroSet (string unitLetter, IEnumerable<string> macroNames)
    {
      var result = new Dictionary<string, double> ();
      try {
        var fileContent = GetSymbolListContent ("LOD||MCR" + (MachineType == "C" ? "S" : "N") + unitLetter + "1");
        foreach (var macroName in macroNames) {
          // warning, variable name returned is prefixed by "C". E.g. "C800" instead of "800"
          if (fileContent.ContainsKey ("C" + macroName)) {
            var parts = fileContent["C" + macroName];
            double value = 0;
            if (parts.Count == 0 || string.IsNullOrEmpty (parts[0])) {
              log.WarnFormat ("Macro {0} is empty", "C" + macroName);
            }
            else if (!double.TryParse (parts[0], out value)) {
              log.WarnFormat ("Value {0} of macro {1} cannot be parsed as a double", parts[0], "C" + macroName);
            }
            else {
              result[macroName] = value;
            }
          }
        }
      }
      catch (Exception ex) {
        log.Error ($"GetTCPMacroSet: exception", ex);
        throw;
      }
      return result;
    }

    /// <summary>
    /// Get a set of cnc variables.
    /// </summary>
    /// <param name="param">ListString (first character is the separator)</param>
    /// <returns></returns>
    public IDictionary<string, double> GetCncVariableSet (string param)
    {
      var macroVariableNumbers = Lemoine.Collections.EnumerableString.ParseListString (param)
        .Distinct ();
      return GetTCPMacroSet ("D", macroVariableNumbers);
    }

    /// <summary>
    /// Get the tool life data
    /// Equivalent to GetFTPToolLifeData
    /// </summary>
    /// <param name="param">File to read (TOLNI1 for inches or TOLNM1 for metrics)</param>
    public ToolLifeData GetToolLifeData (string param)
    {
      var tld = new ToolLifeData ();

      try {
        var fileContent = GetSymbolListContent ("LOD||" + param);

        // For each input, add a tool definition
        foreach (var symbol in fileContent.Keys) {
          if (fileContent[symbol].Count >= 10) {
            m_toolLifeBuilder.AddToolLife (ref tld, symbol, fileContent[symbol]);
          }
        }
      }
      catch (Exception ex) {
        log.Error ($"GetTCPToolLifeData: param={param}, exception", ex);
        throw;
      }

      return tld;
    }


    /// <summary>
    /// Set a string with the TCP protocol type 2
    /// </summary>
    /// <param name="command"></param>
    /// <param name="function"></param>
    /// <param name="message"></param>
    /// <param name="data"></param>
    public void SetData (string command, string function, string message, string data)
    {
      // Get the corresponding string list
      var result = GetLines (command, function, message, data);

      // Extract the single result
      if (result.Count == 0) {
        log.Warn ($"SetData: invalid number of response lines {result.Count}");
      }
    }

    /// <summary>
    /// Save some data using the SAV command
    /// </summary>
    /// <param name="param">data name</param>
    /// <param name="data">value</param>
    public void SaveData (string param, string data)
    {
      try {
        SetData ("SAV", "", param, data);
      }
      catch (Exception ex) {
        log.Error ($"SaveData: {param}={data}", ex);
        throw;
      }
    }

    /// <summary>
    /// Set a variable
    /// </summary>
    /// <param name="param">(M|I|D)?'variable name'</param>
    /// <param name="data"></param>
    public void SetVariable (string param, double data)
    {
      char unit;
      string variableName;
      switch (param[0]) {
        case 'M':
          unit = 'M';
          variableName = param.Substring (1);
          break;
        case 'I':
          unit = 'I';
          variableName = param.Substring (1);
          break;
        case 'D':
          unit = 'D';
          variableName = param.Substring (1);
          break;
        default:
          unit = 'D';
          variableName = param;
          break;
      }
      SetMacroVariable (variableName, data, unit);
    }

    /// <summary>
    /// Set a macro variable
    /// </summary>
    /// <param name="variableName"></param>
    /// <param name="v"></param>
    /// <param name="unit">M for metric, I for inch, D for current unit system</param>
    public void SetMacroVariable (string variableName, double v, char unit = 'D')
    {
      try {
        var macroType = MachineType.Equals ("C")
          ? "S"
          : "N";
        var data = $"C{variableName},{v.ToString (CultureInfo.InvariantCulture)}";
        SaveData ($"MCR{macroType}{unit}1", data);
      }
      catch (Exception ex) {
        log.Error ($"SetMacroVariable: {variableName}={v} unit={unit} exception", ex);
        throw;
      }
    }

    /// <summary>
    /// Set a tool offset
    /// </summary>
    /// <param name="param">toolNumber#(L|W|D|DW)</param>
    /// <param name="data"></param>
    public void SetToolOffset (string param, double data)
    {
      var t = param.Split (new char[] { '#' }, 2);
      SetToolOffset (int.Parse (t[0]), (ToolOffsetType)Enum.Parse (typeof (ToolOffsetType), t[1]), data);
    }

    /// <summary>
    /// Set a tool offset
    /// </summary>
    /// <param name="toolNumber"></param>
    /// <param name="toolOffsetType"></param>
    /// <param name="offset"></param>
    public void SetToolOffset (int toolNumber, ToolOffsetType toolOffsetType, double offset)
    {
      // Command header: CWRTTLLFn1n2k1
      // Command data: 9 bytes for ToolLengthOffset / ToolWearOffset, 8 bytes for Tool diameter wear offset
      SetData ("WRT", "TOFS", $"{toolNumber.ToString ("D2")}{(int)toolOffsetType}", offset.ToString (CultureInfo.InvariantCulture));
    }

  }
}
