// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections.Generic;
using System.Text;
using Lemoine.Core.Log;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Description of FapiReader.
  /// </summary>
  public class FapiReader
  {
    #region Members / Getters / Setters
    readonly ValueStorage m_valueStorage = new ValueStorage();
    readonly KeyCheck m_keyCheck = new KeyCheck();
    readonly IDictionary<string, IList<string>> m_keysToRead = new Dictionary<string, IList<string>>();
    #endregion // Members / Getters / Setters
    
    static readonly ILog log = LogManager.GetLogger("Lemoine.Cnc.In.Fapi");
    
    #region Constructor
    /// <summary>
    /// Constructor
    /// </summary>
    public FapiReader()
    {
      ConnectionId = -1;
      ConnectionRequired = false;
    }
    #endregion // Constructor
    
    #region Getters / Setters
    /// <summary>
    /// Connexion id used to get data
    /// </summary>
    public int ConnectionId {
      get { return m_connectionId; }
      set {
        m_keyCheck.Initialize(value, ListCommandsInLog);
        m_connectionId = value;
      }
    }
    int m_connectionId;
    
    /// <summary>
    /// True if the connection must be restored
    /// </summary>
    public bool ConnectionRequired { get; set; }
    
    /// <summary>
    /// If true, list all commands in logs (as fatal)
    /// => only for a debug reason or for completing the ILMA API documentation
    /// </summary>
    public bool ListCommandsInLog { get; set; }
    #endregion // Getters / Setters
    
    #region Methods
    /// <summary>
    /// Start a new reading
    /// </summary>
    public void Start()
    {
      // Non-cached values cannot be read
      m_valueStorage.SetReady(false, false);
      
      // Compute all values
      if (ConnectionId == -1) {
        ConnectionRequired = true;
        throw new Exception("ConnectionId not initialized");
      }
      
      // Compute all keys if necessary
      if (m_valueStorage.KeysChanged) {
        log.Info("FapiReader.Start - compute all keys to read");
        
        // Format the keys so that we have param -> params
        var keysToRead = m_valueStorage.GetKeysToRead();
        m_keysToRead.Clear();
        foreach (var key in keysToRead) {
          // Store the key
          var split = key.Split('/');
          if (!m_keysToRead.ContainsKey(split[0]))
            m_keysToRead[split[0]] = new List<string>();
          string subParam = (split.Length == 2 ? split[1] : "");
          if (!m_keysToRead[split[0]].Contains(subParam)) {
            if (string.IsNullOrEmpty(subParam)) {
              m_keysToRead[split[0]].Insert(0, "");
            } else {
              m_keysToRead[split[0]].Add(subParam);
            }
          }
        }
      }
      
      // Read each group
      foreach (var key in m_keysToRead.Keys)
        ReadValues(key, m_keysToRead[key]);
      
      // Now both values and cached values are allowed to be read
      m_valueStorage.SetReady(true, false);
      m_valueStorage.SetReady(true, true);
    }
    
    /// <summary>
    /// Store keys
    /// (useful if several keys need to be read)
    /// </summary>
    /// <param name="keys"></param>
    /// <param name="isCache">true if the key will be used in cache</param>
    /// <param name="throwException">throw an exception at the end if a key is added</param>
    public void StoreKeys(string [] keys, bool isCache, bool throwException)
    {
      bool newKeysAdded = false;
      foreach (var key in keys) {
        if (!m_valueStorage.ContainKey(key)) {
          log.InfoFormat("FapiReader.StoreKeys - storing key '{0}'", key);
          newKeysAdded = true;
          
          // Check the validity of the key and store it if ok
          if (m_keyCheck.CheckKey(key))
            m_valueStorage.StoreKey(key, isCache);
          else
            throw new Exception("Invalid key: " + key);
        }
      }
      
      if (newKeysAdded && throwException)
        throw new Exception("Storing key(s)");
    }
    
    /// <summary>
    /// Get a value or throw an error if it is not available yet
    /// </summary>
    /// <param name="key">key used to specify a variable to read</param>
    /// <param name="cached">if true, this value will be read only once</param>
    /// <returns></returns>
    public string GetValue(string key, bool cached)
    {
      if (ConnectionRequired)
        throw new Exception("A connection is required");
      
      // Store a new key and throw an exception if necessary
      StoreKeys(new []{ key }, cached, true);
      
      // Return the value if the key is found or throw an exception if not available
      return m_valueStorage.ReadValue(key);
    }
    
    void ReadValues(string param, IList<string> subParams)
    {
      // Read a parameter with one or several sub parameters
      if (subParams.Count == 0)
        subParams.Add("");
      FapiError error = (subParams.Count == 1) ? ReadParameter(param, subParams[0]) :
        ReadParameters(param, subParams);
      
      // Log and clear the values related of the param in case of an error
      if (error != FapiError.FCL_RET_EXECUTED) {
        switch (error) {
          case FapiError.FCL_RET_ABORTED:
            log.ErrorFormat("FapiReader.ReadValues - cannot read {0} with {1} argument(s) " +
                            "because of a wrong subparameter name or a communication problem",
                            param, subParams.Count);
            break;
          case FapiError.FCL_RET_TIMEOUT:
            log.ErrorFormat("FapiReader.ReadValues - cannot read {0} with {1} argument(s) " +
                            "because of an operation timeout",
                            param, subParams.Count);
            break;
          case FapiError.FCL_RET_FAILED:
            log.ErrorFormat("FapiReader.ReadValues - cannot read {0} with {1} argument(s) " +
                            "because of connections problem -> a new connection is required",
                            param, subParams.Count);
            ConnectionRequired = true;
            break;
          default:
            log.ErrorFormat("FapiReader.ReadValues - cannot read {0} with {1} argument(s) " +
                            "because of the unexpected error '{2}'",
                            param, subParams.Count, error);
            break;
        }
        
        foreach (var subParam in subParams)
          m_valueStorage.StoreValue(GetKey(param, subParam), null);
      }
    }
    
    FapiError ReadParameter(string param, string subParam)
    {
      FapiError error = FapiError.FCL_RET_FAILED;
      string key = GetKey(param, subParam);
      log.InfoFormat("FapiReader.ReadParameter - read '{0}'", key);
      
      try {
        // Prepare the result
        var result = new StringBuilder(FapiCorbaLib.VALUE_LENGTH);
        
        // Query the machine
        error = FapiCorbaLib.FapiReadParameter(ConnectionId, param, subParam, result, FapiCorbaLib.VALUE_LENGTH);
        
        // Store the result if everything went right
        if (error == FapiError.FCL_RET_EXECUTED)
          m_valueStorage.StoreValue(key, result.ToString());
      } catch (Exception ex) {
        log.ErrorFormat("FapiReader.ReadParameter - couldn't use 'FapiCorbaLib.FapiReadParameter({1})': {0}", ex, key);
      }
      
      return error;
    }
    
    FapiError ReadParameters(string param, IList<string> subParams)
    {
      FapiError error = FapiError.FCL_RET_FAILED;
      var subParamsForLog = subParams[0];
      for (int i = 1; i < subParams.Count; i++)
        subParamsForLog += "|" + subParams[i];
      log.InfoFormat("FapiReader.ReadParameters - read '{0}/{1}'", param, subParamsForLog);
      
      try {
        // Format the subParams
        var formattedSubParams = new FapiCorbaLib.StringName[subParams.Count];
        for (int i = 0; i < subParams.Count; i++)
          formattedSubParams[i].msg = subParams[i];
        
        // Prepare the result
        var result = new FapiCorbaLib.StringValue[subParams.Count];
        
        // Query the machine
        error = FapiCorbaLib.FapiReadParameters(ConnectionId, param,
                                                formattedSubParams, formattedSubParams.Length,
                                                result, result.Length);
        
        // Store the result if everything went right
        if (error == FapiError.FCL_RET_EXECUTED)
          for (int i = 0; i < result.Length; i++)
            m_valueStorage.StoreValue(GetKey(param, subParams[i]), result[i].msg);
      } catch (Exception ex) {
        log.ErrorFormat("FapiReader.ReadParameters - couldn't use 'FapiCorbaLib.FapiReadParameters': {0}", ex);
      }
      
      return error;
    }
    
    string GetKey(string param, string subParam)
    {
      return string.IsNullOrEmpty(subParam) ? param : param + "/" + subParam;
    }
    #endregion // Methods
  }
}
