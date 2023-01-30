// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

using Lemoine.Core.Log;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Haas M-Net CNC Input module
  /// </summary>
  public class MNet: Lemoine.Cnc.BaseCncModule, Lemoine.Cnc.ICncModule, IDisposable
  {
    #region Members
    string url;
    string query = "m-net?mds,1,";
    
    List<string> macroVariables = new List<string> ();
    List<string> systemVariables = new List<string> ();
    Hashtable macroVariableValues = new Hashtable ();
    Hashtable systemVariableValues = new Hashtable ();
    bool error = false;
    bool parseError = false;
    bool undefined = false;
    
    bool dynamicQueryRequest = true;

    Regex variableRegex =
      new Regex ("Data item 000000008 \\(length = \\d*\\) = \"(\\d+)\"<br>");
    Regex valueRegex =
      new Regex ("Data item 16000000(\\d) \\(length = \\d*\\) = \"([^\"\"]+)\"<br>");
    Regex macroValueRegex =
      new Regex ("^MACRO, (\\d+), *([-A-Z0-9.]+)$");
    Regex modeRegex =
      new Regex ("MODE, \\((\\w+)\\)");
    #endregion

    #region Getters / Setters
    /// <summary>
    /// URL to the Haas CNC
    /// </summary>
    /// <example>http://haasmachine</example>
    public string Url {
      get { return url; }
      set { url = value; }
    }
    
    /// <summary>
    /// Query command that is appended to the URL, before any specific command
    /// 
    /// Default is: "m-net?mds,1,,"
    /// </summary>
    /// <example>m-net?mds,1,,</example>
    public string Query {
      get { return query; }
      set { query = value; }
    }
    
    /// <summary>
    /// Build dynamically the M-Net query request from:
    /// <item>the Url property</item>
    /// <item>the Query property</item>
    /// <item>the asked variables</item>
    /// 
    /// Default is 'true'.
    /// 
    /// If this value is 'false',
    /// only the Url property is taken into account.
    /// This can be very useful in case of tests.
    /// </summary>
    public bool DynamicQueryRequest {
      get { return dynamicQueryRequest; }
      set { dynamicQueryRequest = value; }
    }
    
    /// <summary>
    /// Host name or IP address
    /// </summary>
    public string Host {
      get
      {
        Uri uri = new Uri (this.Url);
        return uri.Host;
      }
    }
    #endregion
    
    #region Constructors / Destructor / ToString methods
    /// <summary>
    /// Description of the constructor
    /// </summary>
    public MNet ()
      : base("Lemoine.Cnc.In.MNet")
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
    /// <returns></returns>
    public bool Start ()
    {
      // 1. Clear the previously stored values
      macroVariableValues.Clear ();
      systemVariableValues.Clear ();
      error = false;
      parseError = false;
      
      // 2. Make the M-Net request
      string request = this.Url;
      if (dynamicQueryRequest) {
        request += "/";
        request += this.Query;
        foreach (string macroVariable in macroVariables) {
          request += ",8=";
          request += macroVariable;
          request += ",160000001";
        }
        foreach (string systemVariable in systemVariables) {
          request += ",8=";
          request += systemVariable;
          request += ",160000000";
        }
      }
      log.DebugFormat ("Start: " +
                       "request is {0}",
                       request);
      
      // 3. Call the built request
      var webClient = new System.Net.WebClient ();
      string response;
      try {
        response = webClient.DownloadString (request);
      }
      catch (Exception ex) {
        log.ErrorFormat ("Start: " +
                         "could not get request={0}, " +
                         "{1} raised",
                         request,
                         ex);
        error = true;
        return false;
      }
      
      // 4. Parse the response
      CultureInfo usCultureInfo = new CultureInfo ("en-US");
      foreach (Match match in variableRegex.Matches (response)) {
        string variable = match.Groups [1].Value;
        log.DebugFormat ("Start: " +
                         "process variable {0}",
                         variable);
        Match valueMatch = valueRegex.Match (response, match.Index + match.Length + 2);
        if (!valueMatch.Success) {
          log.ErrorFormat ("Start: " +
                           "there is no item value for variable {0}",
                           variable);
          parseError = true;
        }
        else {
          string variableType = valueMatch.Groups [1].Value;
          string variableValue = valueMatch.Groups [2].Value;
          if (variableType.Equals ("0")) { // System variable
            log.DebugFormat ("Start: " +
                             "got system variable {0}={1}",
                             variable, variableValue);
            systemVariableValues [variable] = variableValue;
          }
          else if (variableType.Equals ("1")) { // Macro variable
            log.DebugFormat ("Start: " +
                             "got macro variable {0}={1}",
                             variable, variableValue);
            Match macroValueMatch = macroValueRegex.Match (variableValue);
            if (!macroValueMatch.Success) {
              log.ErrorFormat ("Start: " +
                               "macro value syntax {0} is not correct",
                               variableValue);
              parseError = true;
            }
            else {
              if (!macroValueMatch.Groups [1].Value.Equals (variable)) {
                log.ErrorFormat ("Start: " +
                                 "macro variable name changed in value " +
                                 "from {0} to {1}",
                                 macroValueMatch.Groups [1].Value,
                                 variable);
                parseError = true;
              }
              else {
                string macroStringValue = macroValueMatch.Groups [2].Value;
                if (macroStringValue.Equals ("UNDEFINED")) {
                  log.ErrorFormat ("Start: " +
                                   "macro variable {0} is not defined",
                                   variable);
                  undefined = true;
                }
                else {
                  double macroDoubleValue = 0.0;
                  try {
                    macroDoubleValue = double.Parse (macroStringValue,
                                                     usCultureInfo);
                    macroVariableValues [variable] = macroDoubleValue;
                  }
                  catch (Exception ex) {
                    log.ErrorFormat ("Start: " +
                                     "macro value {0} is not a double, " +
                                     "{1} raised",
                                     macroStringValue,
                                     ex);
                    parseError = true;
                  }
                }
              }
            }
          }
          else { // Unsupported type of variable
            log.ErrorFormat ("Start: " +
                             "unsupported type of variable {0}",
                             variableType);
            parseError = true;
          }
        }
      }
      
      return true;
    }
    
    /// <summary>
    /// Get the value of a given macro variable
    /// </summary>
    /// <param name="param">Macro variable</param>
    /// <returns></returns>
    public double GetMacroVariable (string param)
    {
      if (false == macroVariables.Contains (param)) {
        log.InfoFormat ("GetMacroVariable: " +
                        "add macro variable {0} in the list of macro variables",
                        param);
        macroVariables.Add (param);
        throw new Exception ("Initialization step");
      }
      
      if (false == macroVariableValues.ContainsKey (param)) {
        log.ErrorFormat ("GetMacroVariable: " +
                         "macro variable {0} has not been retrieved",
                         param);
        throw new Exception ("Macro variable unknown");
      }
      
      log.DebugFormat ("GetMacroVariable: " +
                       "{0}={1}",
                       param,
                       macroVariableValues [param]);
      return (double) macroVariableValues [param];
    }
    
    /// <summary>
    /// Get the value of a given system variable
    /// </summary>
    /// <param name="param">System variable</param>
    /// <returns></returns>
    public string GetSystemVariable (string param)
    {
      if (false == systemVariables.Contains (param)) {
        log.InfoFormat ("GetSystemVariable: " +
                        "add system variable {0} in the list of system variables",
                        param);
        systemVariables.Add (param);
        throw new Exception ("Initialization step");
      }
      
      if (false == systemVariableValues.ContainsKey (param)) {
        log.ErrorFormat ("GetSystemVariable: " +
                         "system variable {0} has not been retrieved",
                         param);
        throw new Exception ("System variable unknown");
      }
      
      log.DebugFormat ("GetSystemVariable: " +
                       "{0}={1}",
                       param,
                       systemVariableValues [param]);
      return (string) systemVariableValues [param];
    }
    #endregion
    
    #region CNC properties
    /// <summary>
    /// Program Name
    /// (from system variable 500)
    /// </summary>
    public string ProgramName {
      get
      {
        string systemVariable3in1 = GetSystemVariable ("500");
        string[] systemValues = systemVariable3in1.Split (',');
        string programName = systemValues [1];
        if (false == programName.StartsWith ("O")) {
          log.InfoFormat ("ProgramName: " +
                          "2nd value of system variable 500 {0}={1} " +
                          "is not a program name, " +
                          "manual mode ?",
                          systemVariable3in1, programName);
          throw new Exception ("No program name");
        }
        log.DebugFormat ("ProgramName: " +
                         "got {0}",
                         programName);
        return programName;
      }
    }

    /// <summary>
    /// Running status
    /// (from system variable 500)
    /// </summary>
    public bool Running {
      get
      {
        string systemVariable3in1 = GetSystemVariable ("500");
        string[] systemValues = systemVariable3in1.Split (',');
        string status = systemValues [2];
        if (status.Equals ("RUNNING")) {
          log.DebugFormat ("Running: " +
                           "status={0} => running",
                           status);
          return true;
        }
        else if (status.Equals ("IDLE")) {
          log.DebugFormat ("Running: " +
                           "status={0} => idle",
                           status);
          return false;
        }
        else {
          log.ErrorFormat ("Running: " +
                           "status {0} is unknown in {1}",
                           status, systemVariable3in1);
          parseError = true;
          throw new Exception ("Invalid running status");
        }
      }
    }
    
    /// <summary>
    /// Mode
    /// (from system variable 104)
    /// </summary>
    public string Mode {
      get
      {
        string modeSystemVariable = GetSystemVariable ("104");
        Match modeMatch = modeRegex.Match (modeSystemVariable);
        if (!modeMatch.Success) {
          log.ErrorFormat ("Mode: " +
                           "mode syntax {0} is not correct",
                           modeSystemVariable);
          parseError = true;
          throw new Exception ("Bad syntax for mode");
        }
        else {
          string mode = modeMatch.Groups [1].Value;
          log.DebugFormat ("Mode: " +
                           "mode is {0}",
                           mode);
          return mode;
        }
      }
    }
    
    /// <summary>
    /// The M-Net request was not valid, the machine may be stopped
    /// </summary>
    public bool Error {
      get { return error; }
    }
    
    /// <summary>
    /// There was a parsing error in the response of the M-Net request
    /// </summary>
    public bool ParseError {
      get { return parseError; }
    }
    
    /// <summary>
    /// A called macro value is not defined
    /// </summary>
    public bool Undefined {
      get { return undefined; }
    }
    #endregion
  }
}
