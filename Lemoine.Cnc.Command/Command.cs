// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Module to run external command to get or set some values
  /// </summary>
  public sealed class Command: Lemoine.Cnc.BaseCncModule, Lemoine.Cnc.ICncModule, IDisposable
  {
    #region Members
    ProcessStartInfo startInfo = new ProcessStartInfo ();
    #endregion

    #region Getters / Setters
    /// <summary>
    /// Environment variables to set
    /// 
    /// This contains a list of key=value pairs.
    /// 
    /// The first character is the separator used to separate
    /// the different environment variables.
    /// 
    /// For example: " OPC_SERVER=OPC.SINUMERIK.Machineswitch OPC_GATE_HOST=siemens321"
    /// </summary>
    public string EnvironmentVariables {
      set
      {
        if (value.Length == 0) {
          log.DebugFormat ("EnvironmentVariables.set: " +
                           "empty parameter, do nothing");
          return;
        }
        else if (value.Length <= 4) {
          log.WarnFormat ("EnvironmentVariables.set: " +
                          "invalid parameter {0} " +
                          "=> do nothing",
                          value);
          return;
        }
        else {
          string[] variables = value.Split (new char [] {value [0]},
                                            StringSplitOptions.RemoveEmptyEntries);
          foreach (string variable in variables) {
            string[] keyvalue = variable.Split (new char [] {'='}, 2,
                                                StringSplitOptions.None);
            if (keyvalue.Length != 2) {
              log.WarnFormat ("EnvironmentVariables.set: " +
                              "invalid key=value {0}",
                              variable);
            }
            else {
              log.DebugFormat ("EnvironmentVariables.set: " +
                               "set {1} to {0}",
                               keyvalue [0], keyvalue [1]);
              try {
                Environment.SetEnvironmentVariable (keyvalue [0], keyvalue [1]);
              }
              catch (Exception ex) {
                log.ErrorFormat ("EnvironmentVariables.set: " +
                                 "SetEnvironmentVariable failed with {0}",
                                 ex);
              }
            }
          }
        }
      }
    }
    
    /// <summary>
    /// Current directory
    /// </summary>
    public string CurrentDirectory {
      get { return Environment.CurrentDirectory; }
      set { Environment.CurrentDirectory = value; }
    }
    
    /// <summary>
    /// Working directory
    /// </summary>
    public string WorkingDirectory {
      get { return startInfo.WorkingDirectory; }
      set { startInfo.WorkingDirectory = value; }
    }
    #endregion

    #region Constructors / Destructor / ToString methods
    /// <summary>
    /// Description of the constructor
    /// </summary>
    public Command ()
      : base("Lemoine.Cnc.InOut.Command")
    {
      startInfo.CreateNoWindow = true;
      startInfo.RedirectStandardError = true;
      startInfo.RedirectStandardOutput = true;
      startInfo.UseShellExecute = false;
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
    /// Get a string from a command line
    /// </summary>
    /// <param name="param">command line</param>
    /// <returns></returns>
    public string GetString (string param)
    {
      string[] programArguments = param.Split (new char [] {' '}, 2);
      if (programArguments.Length < 1) {
        log.ErrorFormat ("GetString: " +
                         "param {0} is not valid");
        throw new ArgumentException ("Invalid param");
      }
      log.DebugFormat ("GetString: " +
                       "set startInfo.FileName to {0}",
                       programArguments [0]);
      startInfo.FileName = programArguments [0];
      if (programArguments.Length > 1) {
        log.DebugFormat ("GetString: " +
                         "set arguments {0}",
                         programArguments [1]);
        startInfo.Arguments = programArguments [1];
      }
      else {
        startInfo.Arguments = "";
      }
      
      string standardError;
      string standardOutput;
      using (Process process = Process.Start (startInfo)) {
        using (StreamReader reader = process.StandardError) {
          standardError = reader.ReadToEnd ();
        }
        using (StreamReader reader = process.StandardOutput) {
          standardOutput = reader.ReadToEnd ();
        }
        process.WaitForExit ();
        if (0 != process.ExitCode) {
          log.ErrorFormat ("GetString: " +
                           "{0} {1} failed with error {2}",
                           startInfo.FileName, startInfo.Arguments,
                           standardError);
          throw new Exception ("Process failed");
        }
      }
      
      log.DebugFormat ("GetString: " +
                       "{0} returned {1}",
                       param, standardOutput);
      return standardOutput;
    }
    
    /// <summary>
    /// Get an int value from a command line
    /// </summary>
    /// <param name="param">command line</param>
    /// <returns></returns>
    public int GetInt (string param)
    {
      return int.Parse (this.GetString (param));
    }
    
    /// <summary>
    /// Get a long value from a command line
    /// </summary>
    /// <param name="param">command line</param>
    /// <returns></returns>
    public long GetLong (string param)
    {
      return long.Parse (this.GetString (param));
    }
    
    /// <summary>
    /// Get a double value from a command line
    /// </summary>
    /// <param name="param">command line</param>
    /// <returns></returns>
    public double GetDouble (string param)
    {
      CultureInfo usCultureInfo = new CultureInfo ("en-US");
      return double.Parse (this.GetString (param),
                           usCultureInfo);
    }
    #endregion
  }
}
