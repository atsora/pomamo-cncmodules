// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections.Generic;
using Lemoine.Core.Log;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Description of KeyCheck.
  /// </summary>
  public class KeyCheck
  {
    #region Members
    int m_connectionId = -1;
    readonly IDictionary<string, IList<string>> m_possibleKeys = new Dictionary<string, IList<string>>();
    readonly IDictionary<string, int> m_subParamNumber = new Dictionary<string, int>();
    bool m_checkPossible = false;
    #endregion // Members

    static readonly ILog log = LogManager.GetLogger("Lemoine.Cnc.In.Fapi");

    #region Methods
    /// <summary>
    /// Reset the possible keys and browse the params
    /// </summary>
    public void Initialize(int connectionId, bool listCommandsInLog)
    {
      m_connectionId = connectionId;
      m_checkPossible = false;
      m_possibleKeys.Clear();
      m_subParamNumber.Clear();
      
      if (m_connectionId != -1) {
        try {
          // List of parameters
          BrowseParams(listCommandsInLog);
          
          // This is now possible to check keys
          m_checkPossible = true;
        } catch (Exception ex) {
          log.ErrorFormat("KeyCheck.Initialize - couldn't browse params: {0}", ex);
        }
      }
    }
    
    /// <summary>
    /// True if the key is valid and allowed by the cnc
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public bool CheckKey(string key)
    {
      bool valid = false;
      var split = key.Split('/');
      if (split.Length > 2)
      {
        // Reject keys with too many parts
        log.ErrorFormat("KeyCheck.CheckKey: invalid key {0} -> too many parts", key);
      }
      else if (string.IsNullOrEmpty(split[0]) || (split.Length == 2 && string.IsNullOrEmpty(split[1])))
      {
        // Reject keys with a null or empty part
        log.ErrorFormat("KeyCheck.CheckKey: invalid key {0} -> empty element", key);
      }
      else if (!CheckKey(split[0], split.Length == 2 ? split[1] : ""))
      {
        // Reject keys that are not allowed by the CNC
        log.ErrorFormat("KeyCheck.CheckKey: invalid key {0} -> not allowed by CNC", key);
      }
      else
        valid = true;
      
      return valid;
    }
    
    bool CheckKey(string param, string subParam)
    {
      bool valid = false;
      if (m_checkPossible) {
        // Check if the param is in the list
        valid = m_possibleKeys.ContainsKey(param);
        
        // Possibly check if the subParam is in a sublist
        if (valid && !string.IsNullOrEmpty(subParam)) {
          if (m_possibleKeys[param] == null) {
            try {
              BrowseSubParams(param, false);
            } catch (Exception ex) {
              log.ErrorFormat("KeyCheck.CheckKey - couldn't browse the subparams of '{0}': {1}", param, ex);
            }
          }
          valid = m_possibleKeys[param] != null && m_possibleKeys[param].Contains(subParam);
        }
      } else
        valid = true;
      
      return valid;
    }
    
    void BrowseParams(bool listCommandsInLog)
    {
      // Number of parameters
      int paramNum = FapiCorbaLib.GetParameterNum(m_connectionId);
      if (paramNum == -1)
        throw new Exception("FapiCorbaLib.GetParameterNum return -1");
      
      // Prepare the results
      var names = new FapiCorbaLib.StringName[paramNum];
      var descriptions = new FapiCorbaLib.StringDescription[paramNum];
      var numbers = new int[paramNum];
      
      // Query
      if (listCommandsInLog)
        log.FatalFormat("FAPI COMMAND LIST - {0} results", paramNum);
      else
        log.InfoFormat("KeyCheck.BrowseParams - browsing {0} possible params", paramNum);
      var error = FapiCorbaLib.GetParList(m_connectionId, names, descriptions, numbers, paramNum);
      
      // Store the results
      bool hasValidParam = false;
      for (int i = 0; i < paramNum; i++) {
        if (!string.IsNullOrEmpty(names[i].msg)) {
          // Store the param
          hasValidParam = true;
          m_possibleKeys[names[i].msg] = null;
          m_subParamNumber[names[i].msg] = numbers[i];
          
          // Show a log, possibly list everything
          if (listCommandsInLog) {
            log.FatalFormat("{0} {1}", names[i].msg, descriptions[i].msg);
            try {
              BrowseSubParams(names[i].msg, listCommandsInLog);
            } catch (Exception ex) {
              log.FatalFormat("     /!\\ {0} /!\\", ex.Message);
            }
          } else {
            log.DebugFormat("KeyCheck.BrowseParam - possible param '{0}' has {1} subparam(s)", names[i].msg, numbers[i]);
          }
        }
      }
      
      if (!hasValidParam)
        throw new Exception("FapiCorbaLib.GetParList returned no valid results");
    }
    
    void BrowseSubParams(string param, bool listCommandsInLog)
    {
      // Return if "param" is unknown or if "BrowseParam" has already been performed on "param"
      if (!m_possibleKeys.ContainsKey(param) || m_possibleKeys[param] != null)
        return;
      
      // Initialize the possible values
      m_possibleKeys[param] = new List<string>();
      
      // Number of subparameters
      int subParamNum = m_subParamNumber[param];
      if (subParamNum == 0)
        return; // No need to go further
      
      if (!listCommandsInLog)
        log.InfoFormat("KeyCheck.BrowseSubParams - browsing {0} subparams of '{1}'", subParamNum, param);
      
      // Prepare the results
      var names = new FapiCorbaLib.StringName[subParamNum];
      var types = new FapiCorbaLib.StringName[subParamNum];
      var values = new FapiCorbaLib.StringValue[subParamNum];
      var descriptions = new FapiCorbaLib.StringDescription[subParamNum];
      var sizes = new int[subParamNum];
      
      // Query
      FapiCorbaLib.GetSubParameterInfo(m_connectionId, param, names, types, values, descriptions, sizes, subParamNum);
      
      // Store the results
      bool hasValidSubParam = false;
      for (int i = 0; i < subParamNum; i++) {
        if (!String.IsNullOrEmpty(names[i].msg)) {
          // Store the subparam
          m_possibleKeys[param].Add(names[i].msg);
          hasValidSubParam = true;
          
          // Show a log
          if (listCommandsInLog) {
            log.FatalFormat("  => {0} - type {1}, size {2}", names[i].msg, types[i].msg, sizes[i]);
            if (!string.IsNullOrEmpty(values[i].msg))
              log.FatalFormat("     values: {0}", values[i].msg);
            if (!string.IsNullOrEmpty(descriptions[i].msg))
              log.FatalFormat("     description: {0}", descriptions[i].msg);
          } else {
            log.DebugFormat("KeyCheck.BrowseSubParams - param '{0}' has a possible subparam '{1}'", param, names[i].msg);
          }
        }
      }
      
      if (!hasValidSubParam)
        throw new Exception("FapiCorbaLib.GetSubParameterInfo returned no valid results");
    }
    #endregion // Methods
  }
}
