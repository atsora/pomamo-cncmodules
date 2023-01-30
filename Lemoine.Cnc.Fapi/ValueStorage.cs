// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections.Generic;
using Lemoine.Core.Log;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Description of ValueStorage.
  /// </summary>
  public class ValueStorage
  {
    #region Members
    bool m_cacheReady = false;
    readonly IDictionary<string, string> m_cache = new Dictionary<string, string>();
    bool m_valuesReady = false;
    readonly IDictionary<string, string> m_values = new Dictionary<string, string>();
    #endregion // Members

    static readonly ILog log = LogManager.GetLogger("Lemoine.Cnc.In.Fapi");
    
    #region Getters / Setters
    /// <summary>
    /// True if a new list of keys needs to be computed
    /// Will be set to false after having called "GetKeysToRead"
    /// </summary>
    public bool KeysChanged { get; private set; }
    #endregion // Getters / Setters

    #region Methods
    /// <summary>
    /// Specify if values can be read
    /// </summary>
    /// <param name="isReady">true if values can be read</param>
    /// <param name="isCache">true for the cache, false for the other values</param>
    public void SetReady(bool isReady, bool isCache)
    {
      if (isCache)
        m_cacheReady = isReady;
      else
        m_valuesReady = isReady;
    }
    
    /// <summary>
    /// Store a new key
    /// </summary>
    /// <param name="key"></param>
    /// <param name="isCache">true if the key will be used in cache</param>
    public void StoreKey(string key, bool isCache)
    {
      if (string.IsNullOrEmpty(key))
        throw new Exception("ValueStorage.StoreKey - cannot store a null key");
      if (isCache) {
        // Don't store it if the non-cached list already contains the key
        if (m_values.ContainsKey(key)) {
          log.WarnFormat("ValueStorage.StoreKey - key '{0}' already in the non-cached list", key);
        } else if (!m_cache.ContainsKey(key)) {
          m_cache[key] = null;
          m_cacheReady = false;
          KeysChanged = true;
        }
      } else {
        // If the key is already in the cached list => remove it
        if (m_cache.ContainsKey(key)) {
          m_cache.Remove(key);
          log.WarnFormat("ValueStorage.StoreKey - removed the key '{0}' from the cached list before adding it in the non-cached list", key);
        }
        
        if (!m_values.ContainsKey(key)) {
          m_values[key] = null;
          m_valuesReady = false;
          KeysChanged = true;
        }
      }
    }
    
    /// <summary>
    /// Store a value related to a key
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    public void StoreValue(string key, string value)
    {
      // Store the value (change the cache only if it's not already specified)
      bool found = false;
      if (m_cache.ContainsKey(key)) {
        m_cache[key] = value;
        log.InfoFormat("ValueStorage.StoreValue - stored the cached value {0} for the key {1}", value, key);
        found = true;
      }
      if (m_values.ContainsKey(key)) {
        m_values[key] = value;
        log.InfoFormat("ValueStorage.StoreValue - stored the value {0} for the key {1}", value, key);
        found = true;
      }
      
      if (!found)
        log.ErrorFormat("ValueStorage.StoreValue - cannot store a value for the key '{0}': not prepared in any list", key);
    }
    
    /// <summary>
    /// Try to find a key
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public bool ContainKey(string key)
    {
      return m_cache.ContainsKey(key) || m_values.ContainsKey(key);
    }
    
    /// <summary>
    /// Return a value or throw an error if it is not available yet
    /// </summary>
    /// <param name="key">key used to specify a variable to read</param>
    /// <returns></returns>
    public string ReadValue(string key)
    {
      // First try to find the key in non-cached values (priority)
      if (m_values.ContainsKey(key) && m_valuesReady)
        return m_values[key];
      
      // Then in the cached values
      if (m_cache.ContainsKey(key) && m_cacheReady)
        return m_cache[key];
      
      throw new Exception("ValueStorage.GetValue - key '" + key + "' not ready");
    }
    
    /// <summary>
    /// Get all keys to read so that the values will be ready
    /// </summary>
    /// <returns></returns>
    public IList<string> GetKeysToRead()
    {
      // Reset "KeysChanged"
      KeysChanged = false;
      
      // Concat all keys
      IList<string> keys = new List<string>();
      if (!m_cacheReady) // Those keys are read only if the cache is not ready
        foreach (var key in m_cache.Keys)
          keys.Add(key);
      foreach (var key in m_values.Keys)
        if (!keys.Contains(key))
          keys.Add(key);
      
      return keys;
    }
    #endregion // Methods
  }
}
