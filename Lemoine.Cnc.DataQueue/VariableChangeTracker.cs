// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using Lemoine.Core.Log;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Lemoine.Cnc.DataQueue
{
  /// <summary>
  /// Track a single variable
  /// </summary>
  internal class VariableChangeTracker
  {
    static readonly string DEFAULT_PREFIX = "Variable";

    string m_filePrefix;
    IDictionary<string, object> m_variables = new Dictionary<string, object> ();

    ILog log = LogManager.GetLogger (typeof (VariableChangeTracker).FullName);

    /// <summary>
    /// Machine Id
    /// </summary>
    public int MachineId { get; set; }

    /// <summary>
    /// Machine Module Id
    /// </summary>
    public int MachineModuleId { get; set; }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="filePrefix"></param>
    public VariableChangeTracker (string filePrefix)
    {
      m_filePrefix = filePrefix;
    }

    /// <summary>
    /// Constructor (with a default prefix)
    /// </summary>
    public VariableChangeTracker ()
      : this (DEFAULT_PREFIX)
    {
    }

    /// <summary>
    /// Check if this is a new variable
    /// </summary>
    /// <param name="variableName"></param>
    /// <param name="variableValue">not null</param>
    /// <returns></returns>
    public bool IsNewVariableValue (string variableName, object variableValue)
    {
      Debug.Assert (null != variableValue);

      object currentVariableValue;
      string currentVariablePrint;
      if (TryGetCurrentVariableValueInMemory (variableName, out currentVariableValue)) {
        log.DebugFormat ("IsNewVariableValue: " +
                         "current={0} new={1}",
                         currentVariableValue, variableValue);
        return !object.Equals (currentVariableValue, variableValue);
      }
      else if (TryGetVariablePrintFromFile (variableName, out currentVariablePrint)) {
        log.DebugFormat ("IsNewVariableValue: " +
                         "currentPrint={0} new={1}",
                         currentVariablePrint, variableValue);
        return !string.Equals (currentVariablePrint, variableValue.ToString (), StringComparison.InvariantCulture);
      }
      else {
        log.WarnFormat ("IsNewVariableValue: " +
                        "TryGetCurrentVariableValueInMemory and TryGetVariablePrintFromFile failed " +
                        "=> return true");
        return true;
      }
    }

    /// <summary>
    /// Check if the integer part of a variable changed
    /// </summary>
    /// <param name="variableName"></param>
    /// <param name="variableValue">not null</param>
    /// <returns></returns>
    public bool IsNewVariableIntValue (string variableName, object variableValue)
    {
      Debug.Assert (null != variableValue);

      string currentVariablePrint;
      if (TryGetCurrentVariableValueInMemory (variableName, out object currentVariableValue)) {
        if (log.IsDebugEnabled) {
          log.Debug ($"IsNewVariableIntValue: current={currentVariableValue} new={variableValue}");
        }
        if (currentVariableValue is null) {
          return !(variableValue is null);
        }
        else { // not null
          if (variableValue is null) {
            return true;
          }
          try {
            var currentVariableIntValue = Math.Floor ((double)currentVariableValue);
            var variableIntValue = Math.Floor((double)variableValue);
            return currentVariableIntValue != variableIntValue;
          }
          catch (Exception ex) {
            log.Error ($"IsNewVariableIntValue: one of the variables {variableValue}, {currentVariableValue} can't be cast to an int => return true", ex);
            return true;
          }
        }
      }
      else if (TryGetVariablePrintFromFile (variableName, out currentVariablePrint)) {
        if (log.IsDebugEnabled) {
          log.Debug ($"IsNewVariableValue: currentPrint={currentVariablePrint} new={variableValue}");
        }
        if (currentVariableValue is null) {
          return !(variableValue is null);
        }
        else { // not null
          if (variableValue is null) {
            return true;
          }
          try {
            var currentVariableDoubleValue = double.Parse (currentVariablePrint, System.Globalization.CultureInfo.InvariantCulture);
            var currentVariableIntValue = Math.Floor (currentVariableDoubleValue);
            var variableIntValue = Math.Floor ((double)variableValue);
            return currentVariableIntValue != variableIntValue;
          }
          catch (Exception ex) {
            log.Error ($"IsNewVariableIntValue: one of the variables {variableValue}, {currentVariablePrint} can't be cast to an int => return true", ex);
            return true;
          }
        }
      }
      else {
        log.Warn ("IsNewVariableIntValue: TryGetCurrentVariableValueInMemory and TryGetVariablePrintFromFile failed => return true");
        return true;
      }
    }

    /// <summary>
    /// Check if the variable value changed
    /// </summary>
    /// <param name="variableName"></param>
    /// <param name="variableValue">not null</param>
    /// <returns></returns>
    public bool IsVariableValueChange (string variableName, object variableValue)
    {
      Debug.Assert (null != variableValue);

      object currentVariableValue;
      string currentVariablePrint;
      if (TryGetCurrentVariableValueInMemory (variableName, out currentVariableValue)) {
        if (log.IsDebugEnabled) {
          log.Debug ($"IsVariableValueChange: current={currentVariableValue} new={variableValue}");
        }
        return !object.Equals (currentVariableValue, variableValue);
      }
      else if (TryGetVariablePrintFromFile (variableName, out currentVariablePrint)) {
        if (log.IsDebugEnabled) {
          log.Debug ($"IsNewVariableValue: currentPrint={currentVariablePrint} new={variableValue}");
        }
        return !string.Equals (currentVariablePrint, variableValue.ToString (), StringComparison.InvariantCulture);
      }
      else {
        log.Warn ("IsVariableValueChange: GetCurrentVariableValue failed => return false");
        return false;
      }
    }

    bool TryGetCurrentVariableValueInMemory (string variableName, out object variableValue)
    {
      if (m_variables.TryGetValue (variableName, out variableValue)) {
        log.DebugFormat ("GetCurrentVariableValue: " +
                         "got stamp {0} for param {1}",
                         variableValue, variableName);
        return true;
      }
      return false;
    }

    bool TryGetVariablePrintFromFile (string variableName, out string variablePrint)
    {
      try {
        variablePrint = GetVariablePrintFromFile (variableName);
        log.DebugFormat ("GetCurrentVariableValue: " +
                         "got variable print {0} for variable name {1} from file",
                         variablePrint, variableName);
        return true;
      }
      catch (Exception ex) {
        variablePrint = "";
        log.ErrorFormat ("GetCurrentVariableValue: " +
                         "GetVariableFromFile failed with {0}",
                         ex);
        return false;
      }
    }

    /// <summary>
    /// Store a new variable
    /// </summary>
    /// <param name="variableName"></param>
    /// <param name="variableValue"></param>
    public void StoreNewVariable (string variableName, object variableValue)
    {
      m_variables[variableName] = variableValue;
      StoreVariablePrintIntoFile (variableName, variableValue.ToString ());
    }

    string GetVariableFilePath (string variableName)
    {
      string fileName = string.Format ("{0}-{1}-{2}-{3}",
                                       m_filePrefix, this.MachineId, this.MachineModuleId, variableName);
      string directory = Lemoine.Info.PulseInfo.LocalConfigurationDirectory;
      if (!Directory.Exists (directory)) {
        Directory.CreateDirectory (directory);
      }
      return Path.Combine (directory, fileName);
    }

    /// <summary>
    /// In case the file is not found or the file content is not valid, an exception is raised
    /// </summary>
    /// <param name="variableName"></param>
    /// <returns></returns>
    string GetVariablePrintFromFile (string variableName)
    {
      return File.ReadAllText (GetVariableFilePath (variableName));
    }

    void StoreVariablePrintIntoFile (string variableName, string variablePrint)
    {
      try {
        File.WriteAllText (GetVariableFilePath (variableName), variablePrint);
      }
      catch (Exception ex) {
        log.ErrorFormat ("StoreVariableIntoFile: " +
                         "the variable value {0} for name {1} can't be stored " +
                         "exception {2}",
                         variablePrint, variableName,
                         ex);
      }
    }

  }
}
