// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections.Generic;
using System.Linq;
using Lemoine.Core.Log;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Description of CommandStorage.
  /// </summary>
  public class CommandStorage
  {
    #region Members
    readonly IDictionary<int, IDictionary<string, string>> m_storage = new Dictionary<int, IDictionary<string, string>> ();
    readonly ILog m_log;
    #endregion // Members

    /// <summary>
    /// Default constructor
    /// </summary>
    /// <param name="logger"></param>
    public CommandStorage (ILog logger)
    {
      m_log = logger;
    }

    #region Methods
    /// <summary>
    /// Store a command
    /// </summary>
    /// <param name="commandNumber"></param>
    /// <param name="commandString"></param>
    /// <returns>True if this is a new command</returns>
    public bool StoreCommand (int commandNumber, string commandString)
    {
      if (!m_storage.ContainsKey (commandNumber)) {
        m_storage[commandNumber] = new Dictionary<string, string> ();
      }

      if (!m_storage[commandNumber].ContainsKey (commandString)) {
        if (m_log.IsInfoEnabled) {
          m_log.Info ($"DElectronEPS.CommandStorage: Storing '{commandString}' for command '{commandNumber}'");
        }
        m_storage[commandNumber][commandString] = "";
        return true;
      }

      return false;
    }

    /// <summary>
    /// Clear all values, keep the list of commands
    /// </summary>
    public void ClearValues ()
    {
      var commandNumbers = new List<int> (m_storage.Keys); // New list because the collection will be modified
      foreach (int commandNumber in commandNumbers) {
        var commandStrings = new List<string> (m_storage[commandNumber].Keys); // Same reason
        foreach (string commandString in commandStrings) {
          m_storage[commandNumber][commandString] = "";
        }
      }
    }

    /// <summary>
    /// Store values
    /// </summary>
    /// <param name="commandNumber"></param>
    /// <param name="values"></param>
    public void StoreValues (int commandNumber, IEnumerable<string> values)
    {
      // TODO: rework...
      var valueList = values.ToList ();
      var commandStrings = new List<string> (m_storage[commandNumber].Keys);
      if (1 == commandStrings.Count) {
        m_storage[commandNumber][commandStrings[0]] = String.Join ("|", valueList);
      }
      else { // More than one command string
        int gap = 1;
        if (commandStrings.Count != valueList.Count) {
          // Values can go with extra '0'
          if (valueList.Count < commandStrings.Count || valueList.Count % commandStrings.Count != 0) {
            m_log.Error ($"DElectronEPS.CommandStorage: wrong count of values for command {commandNumber}");
            throw new Exception ("DElectronEPS: wrong count of values for command " + commandNumber);
          }

          gap = valueList.Count / commandStrings.Count;
        }

        for (int i = 0; i < commandStrings.Count; i++) {
          var subList = valueList.GetRange (i, gap);
          m_storage[commandNumber][commandStrings[i]] = String.Join ("|", subList);
        }
      }
    }

    /// <summary>
    /// Get a value
    /// </summary>
    /// <param name="commandNumber"></param>
    /// <param name="commandString"></param>
    /// <param name="itemNumber">item number, starting from 0</param>
    /// <returns></returns>
    public string GetValue (int commandNumber, string commandString, int itemNumber = 0)
    {
      if (!m_storage.ContainsKey (commandNumber) || !m_storage[commandNumber].ContainsKey (commandString)) {
        m_log.Error ($"DElectronEPS.CommandStorage: {commandNumber}:{commandString} not available");
        throw new Exception ("DElectronEPS: Value not available");
      }

      var v = m_storage[commandNumber][commandString];
      return v.Split ('|')[itemNumber];
    }

    /// <summary>
    /// Get all command numbers
    /// </summary>
    /// <returns></returns>
    public ICollection<int> GetCommands ()
    {
      return m_storage.Keys;
    }

    /// <summary>
    /// Get all command string for a command number
    /// </summary>
    /// <param name="commandNumber"></param>
    /// <returns></returns>
    public ICollection<string> GetCommands (int commandNumber)
    {
      return m_storage[commandNumber].Keys;
    }

    /// <summary>
    /// Remove a command if DElectron doesn't support it
    /// </summary>
    /// <param name="commandNumber"></param>
    public void RemoveCommand (int commandNumber)
    {
      m_storage.Remove (commandNumber);
    }
    #endregion // Methods
  }
}
