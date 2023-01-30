// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections.Generic;
using System.Linq;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Module to parse a string as a representation of a map
  /// It is then possible to extract specific data
  /// </summary>
  public sealed class StringMapParser : Lemoine.Cnc.BaseCncModule, Lemoine.Cnc.ICncModule, IDisposable
  {
    #region Members
    string m_itemSeparators = " ";
    string m_keyValueSeparators = ":=";
    readonly IDictionary<string, string> m_dictionary = new Dictionary<string, string> ();
    bool m_error = false;
    #endregion // Members

    #region Getters / Setters
    /// <summary>
    /// Item separators
    /// 
    /// By default: space
    /// </summary>
    public string ItemSeparators
    {
      get { return m_itemSeparators; }
      set
      {
        m_itemSeparators = value
          .Replace ("\\n", "\n")
          .Replace ("\\t", "\t")
          .Replace ("\\r", "\r");
      }
    }

    /// <summary>
    /// KeyValue separators
    /// 
    /// By default: ':' and '='
    /// </summary>
    public string KeyValueSeparators
    {
      get { return m_keyValueSeparators; }
      set
      {
        m_keyValueSeparators = value
          .Replace ("\\n", "\n")
          .Replace ("\\t", "\t")
          .Replace ("\\r", "\r");
      }
    }

    /// <summary>
    /// Was there a parsing error ?
    /// </summary>
    public bool Error
    {
      get { return m_error; }
    }
    #endregion // Getters / Setters

    #region Constructors / Destructor / ToString methods
    /// <summary>
    /// Description of the constructor
    /// </summary>
    public StringMapParser ()
      : base ("Lemoine.Cnc.InOut.StringMapParser")
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
    #endregion // Constructors / Destructor / ToString methods

    #region Methods
    /// <summary>
    /// Set the string map
    /// </summary>
    /// <param name="stringMap"></param>
    public void SetStringMap (object stringMap)
    {
      m_error = false;

      if (stringMap == null) {
        stringMap = "";
      }

      try {
        string stringMapString = stringMap.ToString ();
        string[] keyValues = stringMapString.Split (m_itemSeparators.ToCharArray (), StringSplitOptions.RemoveEmptyEntries);
        foreach (string keyValue in keyValues) {
          string[] keyValueArray = keyValue.Replace ("\r\n", "").Trim ().Split (m_keyValueSeparators.ToCharArray (), 2);
          if (2 == keyValueArray.Length) {
            m_dictionary[keyValueArray[0]] = keyValueArray[1];
          } else {
            log.ErrorFormat ("SetStringMap: " +
            "no key/value pair detected in {0} " +
            "=> skip it",
              keyValue);
          }
        }

        log.InfoFormat ("SetStringMap: {0} key/value pair(s) detected", m_dictionary.Count);
      } catch (Exception ex) {
        log.ErrorFormat ("SetStringMap: " +
        "error {0}",
          ex);
        m_error = true;
        throw;
      }
    }

    /// <summary>
    /// Get one string item of the map
    /// </summary>
    /// <param name="param">key</param>
    /// <returns></returns>
    public string GetString (string param)
    {
      return m_dictionary[param];
    }

    /// <summary>
    /// Get one int item of the map
    /// </summary>
    /// <param name="param"></param>
    /// <returns></returns>
    public int GetInt (string param)
    {
      return int.Parse (GetString (param));
    }

    /// <summary>
    /// Get one double item of the map
    /// </summary>
    /// <param name="param"></param>
    /// <returns></returns>
    public double GetDouble (string param)
    {
      return double.Parse (GetString (param), System.Globalization.CultureInfo.InvariantCulture.NumberFormat);
    }

    /// <summary>
    /// Extract the Cnc Variables and build a CncVariableSet
    /// </summary>
    /// <param name="param">if null or empty, extract all the variables, else specify here ListString to filter them</param>
    /// <returns></returns>
    public IDictionary<string, string> GetCncVariableSet (string param)
    {
      if (string.IsNullOrEmpty (param)) {
        return m_dictionary;
      } else { // Filter the variables
        var keys = Lemoine.Collections.EnumerableString.ParseListString (param);
        return m_dictionary
          .Where (i => keys.Contains (i.Key))
          .ToDictionary (i => i.Key, i => i.Value);
      }
    }

    /// <summary>
    /// Extract the Cnc Variables and build a CncVariableSet after using the convert function
    /// </summary>
    /// <param name="param">if null or empty, extract all the variables, else specify here ListString to filter them</param>
    /// <param name="convert"></param>
    /// <returns></returns>
    IDictionary<string, T> GetCncVariableSetConvert<T> (string param, Func<string, T> convert)
    {
      var filtered = GetCncVariableSet (param);
      try {
        return filtered.ToDictionary (i => (string)(i.Key), i => (T)(convert (i.Value)));
      } catch (Exception ex) {
        log.ErrorFormat ("GetCncVariableSetConvert: conversion error {0}", ex);
        throw;
      }
    }

    /// <summary>
    /// Extract the Cnc Variables and build a CncVariableSet of type Int32
    /// </summary>
    /// <param name="param">if null or empty, extract all the variables, else specify here ListString to filter them</param>
    /// <returns></returns>
    public IDictionary<string, int> GetCncVariableSetInt (string param)
    {
      return GetCncVariableSetConvert<int> (param, int.Parse);
    }

    /// <summary>
    /// Extract the Cnc Variables and build a CncVariableSet of type double
    /// </summary>
    /// <param name="param">if null or empty, extract all the variables, else specify here ListString to filter them</param>
    /// <returns></returns>
    public IDictionary<string, double> GetCncVariableSetDouble (string param)
    {
      return GetCncVariableSetConvert<double> (param, i => double.Parse (i, System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Get a range of variables from X to Y included having a gap of Z between each variable,
    /// param being in the format X-Y-Z
    /// For example: 1-9-2 will read 1, 3, 5, 7, 9
    /// Accidents may occur: for this add !U>V at the end, U being replaced by V
    /// For example: 1-31-2!19>20 will read 1, 3, ..., 17, 20, 21, 23, ..., 31
    ///              1-7-2!3>4!5>6 will read 1, 4, 6, 7
    /// If empty, all variables are retrieved
    /// </summary>
    /// <param name="param"></param>
    /// <returns>dictionary of doubles</returns>
    public IDictionary<int, double> GetVariables (string param)
    {
      IDictionary<int, double> ret = new Dictionary<int, double> ();

      if (string.IsNullOrEmpty (param)) {
        // Get values
        foreach (var element in m_dictionary) {
          try {
            ret[int.Parse (element.Key)] = GetDouble (element.Key);
          } catch (Exception e) {
            log.ErrorFormat ("DataManipulation.StringMapParser.GetVariables - cannot read variable {0}-{1}: {2}",
              element.Key, element.Value, e);
          }
        }
      } else {
        // Extract special rules
        IDictionary<int, int> mapAccidents = new Dictionary<int, int> ();
        try {
          var accidents = param.Split ('!');
          for (int i = 1; i < accidents.Length; i++) {
            var accident = accidents[i].Split ('>');
            if (accident.Length != 2) {
              throw new Exception ("Special rule '" +
              accidents[i] + "' not valid");
            }

            mapAccidents[int.Parse (accident[0])] = int.Parse (accident[1]);
          }
          param = accidents[0];
        } catch (Exception e) {
          log.ErrorFormat ("StringMapParser::GetVariables - error while loading special rules: {0}", e);
        }

        // Process the range
        var splitParam = param.Split ('-');

        if (splitParam.Length != 3) {
          string txt = string.Format ("GetVariables: parameter {0} is not in the format X-Y", param);
          log.ErrorFormat (txt);
          throw new ArgumentException (txt);
        }

        int valInf, valSup, gap;
        try {
          valInf = int.Parse (splitParam[0]);
        } catch (Exception) {
          string txt = string.Format ("GetVariables: couldn't parse X as an int in parameter {0}", param);
          log.ErrorFormat (txt);
          throw new ArgumentException (txt);
        }

        try {
          valSup = int.Parse (splitParam[1]);
        } catch (Exception) {
          string txt = string.Format ("GetVariables: couldn't parse Y as an int in parameter {0}", param);
          log.ErrorFormat (txt);
          throw new ArgumentException (txt);
        }

        try {
          gap = int.Parse (splitParam[2]);
        } catch (Exception) {
          string txt = string.Format ("GetVariables: couldn't parse Z as an int in parameter {0}", param);
          log.ErrorFormat (txt);
          throw new ArgumentException (txt);
        }

        // Get values
        for (int i = valInf; i <= valSup; i += gap) {
          int key = mapAccidents.ContainsKey (i) ? mapAccidents[i] : i;
          try {
            ret[key] = GetDouble (key.ToString ());
          } catch (Exception e) {
            log.ErrorFormat ("DataManipulation.StringMapParser.GetVariables - cannot read variable {0}: {1}", key, e);
          }
        }
      }

      log.InfoFormat ("StringMapParser:GetVariables - extracted {0} values with argument {1}",
        ret.Count, param);

      return ret;
    }
    #endregion // Methods
  }
}
