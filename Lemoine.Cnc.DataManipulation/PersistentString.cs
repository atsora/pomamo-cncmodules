// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Lemoine.Core.Log;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Persist an int value
  /// </summary>
  public sealed class PersistentString : Lemoine.Cnc.BaseCncModule, Lemoine.Cnc.ICncModule, IDisposable
  {
    #region Members
    bool m_data = false;
    readonly Stack m_stack = new Stack ();
    IDictionary<string, string> m_persistentVariables = new Dictionary<string, string> ();
    #endregion

    #region Getters / Setters
    /// <summary>
    /// Persistent stored value
    /// </summary>
    public bool Output => m_data;


    #endregion // Getters / Setters

    #region Constructors / Destructor / ToString methods
    /// <summary>
    /// Description of the constructor
    /// </summary>
    public PersistentString ()
      : base ("Lemoine.Cnc.InOut.PersistentString")
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
    /// Start method: reset the different values
    /// </summary>
    public void Start ()
    {
    }

    /// <summary>
    /// Clear the list
    /// </summary>
    public void Clear ()
    {
      m_persistentVariables.Clear ();
    }

    /// <summary>
    /// Finish method
    /// </summary>
    public void Finish ()
    {
    }

    /// <summary>
    /// Push a data in the stack
    /// </summary>
    /// <param name="data">Data to push in the stack</param>
    public void Push (object data)
    {
      if (log.IsDebugEnabled) {
        log.Debug ($"Push: push {data} ({data.GetType ()}) in the stack");
      }
      m_stack.Push (data);
    }


    public string Persist (string param)
    {

      if (m_stack.Count < 2) {
        log.Error ("Persist not enough elements in the stack");
        throw new Exception ("Not enough elements in stack");
      }
      string key, value;
      try {
        if (log.IsDebugEnabled) {
          log.Debug ($"Push: start: m_persistentVariables.Count={m_persistentVariables.Count}");
        }
        value = m_stack.Pop ().ToString();
        key = m_stack.Pop ().ToString();

        if (m_persistentVariables.ContainsKey (key)) {
          if (log.IsDebugEnabled) {
            log.Debug ($"Push: Push update param={key} value={value}");
          }
          // update value
          m_persistentVariables[key] = Convert.ToString (value);
        }
        else {
          // create new entry in list
          if (log.IsDebugEnabled) {
            log.Debug ($"Push: Push add param={key} value={value}");
          }
          m_persistentVariables.Add (key, Convert.ToString (value));
        }
        return value;
      }
      catch (Exception ex) {
        log.Error ($"Persist: {ex} in Pop");
        throw;
      }

    }

    /// <summary>
    /// Get a string parameter from persistent list
    /// </summary>
    /// <param name="key">key of string value to get</param>
    /// <returns></returns>
      public string GetString (string key)
    {
      if (log.IsDebugEnabled) {
        log.Debug ($"GetString: param={key}");
      }

      if (m_persistentVariables.ContainsKey (key)) {
        string value = null;
        if (m_persistentVariables.TryGetValue (key, out value)) {
          if (log.IsDebugEnabled) {
            log.Debug ($"GetString: param={key} value={value}");
          }
          return value;
        }
        else {
          log.Error ($"GetString: fail to get value for param={key}");
          return "";
        }
      }
      else {
        log.Error ($"GetString: fail to get param={key}");
        return "";
      }
    }
    #endregion // Methods
  }
}
