// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Lemoine.Cnc
{
  /// <summary>
  /// EthernetIP input module, get methods
  /// </summary>
  public sealed partial class EthernetIP
  {
    #region Members
    AlarmReader m_alarmReader = null;
    #endregion Members

    /// <summary>
    /// Get a string (old method, not working any more with the latest plctag library at Paragon Metals)
    /// </summary>
    /// <param name="param">Format {name}</param>
    /// <returns></returns>
    public string GetStringV1 (string param)
    {
      return GetStringV1 (param, false);
    }

    /// <summary>
    /// Get a string once (old method, not working any more with the latest plctag library at Paragon Metals)
    /// </summary>
    /// <param name="param"></param>
    /// <returns></returns>
    public string GetStringV1Once (string param)
    {
      return GetStringV1 (param, true);
    }

    /// <summary>
    /// Get a string (old method, not working any more with the latest plctag library at Paragon Metals)
    /// </summary>
    /// <param name="tagName"></param>
    /// <param name="once">get the data only once</param>
    /// <returns></returns>
    string GetStringV1 (string tagName, bool once)
    {
      return GetStringV1 (tagName, once, 2);
    }

    /// <summary>
    /// Get a string (old method, not working any more with the latest plctag library at Paragon Metals)
    /// </summary>
    /// <param name="tagName"></param>
    /// <param name="once">get the data only once</param>
    /// <param name="attempt">remaining attempts</param>
    /// <returns></returns>
    string GetStringV1 (string tagName, bool once, int attempt)
    {
      if (0 == attempt) {
        log.Error ($"GetString: latest attempt reached for tag {tagName} ! throw an exception");
        throw new Exception ("Latest attempt reached in GetString");
      }

      // Length of the data
      var length = GetValue<int> (tagName + ".LEN", once);
      if (log.IsDebugEnabled) {
        log.Debug ($"GetString: tagName={tagName} once={once} length={length}");
      }

      if (0 == length) {
        if (log.IsDebugEnabled) {
          log.Debug ($"GetString: return an empty string for tag {tagName}");
        }
        return "";
      }
      else { // 0 != length
        string result = "";
        var dataTagName = tagName + ".DATA";
        if (log.IsDebugEnabled) {
          log.Debug ($"GetString: about to get data tag {dataTagName}");
        }
        Tag dataTag;
        try {
          dataTag = GetTag<byte> (dataTagName, length + 1);
        }
        catch (Exception ex) {
          log.Error ("GetString: GetTag returned an exception", ex);
          ProcessException (ex);
          throw;
        }
        dataTag.ReadOnce = once;
        byte b;
        for (int i = 0; i < length; i++) {
          if (log.IsDebugEnabled) {
            log.Debug ($"GetString: try to read character {i}");
          }
          try {
            b = (byte)dataTag.GetValue (i);
          }
          catch (Exception ex) {
            log.Error ("GetString: GetValue returned an exception", ex);
            ProcessException (ex);
            throw;
          }
          if (0 == b) {
            log.Warn ($"GetString: got a null character, do DATA and LEN match ? Stop here, return {result} for tag {tagName}");
            return result;
          }
          try {
            var c = Convert.ToChar (b);
            if (log.IsDebugEnabled) {
              log.Debug ($"GetString: character {i} is {c}");
            }
            result += c;
          }
          catch (Exception ex) {
            log.Error ("GetString: GetValue returned an exception", ex);
            ProcessException (ex);
            throw;
          }
        }
        b = (byte)dataTag.GetValue (length);
        if (0 != b) {
          log.Warn ($"GetString: end of line character {b} is wrong. Got {result}, length={length}. Try again, attempt={attempt - 1}");
          return GetStringV1 (tagName, once, attempt - 1);
        }

        if (log.IsDebugEnabled) {
          log.Debug ($"GetString: read {result} for tag {tagName}");
        }
        return result;
      }
    }

    /// <summary>
    /// Get a string array once (old method, not working any more with the latest plctag library at Paragon Metals)
    /// </summary>
    /// <param name="param">tagName:length or tagName:start:length</param>
    /// <returns></returns>
    public string[] GetStringV1ArrayOnce (string param)
    {
      string[] cacheValue;
      if (TryInCache<string[]> (param, out cacheValue)) {
        log.Info ($"GetStringArrayOnce: get value for {param} in cache");
        return cacheValue;
      }

      if (m_acquisitionError) {
        log.Debug ($"GetStringArrayOnce: acquisition error, skip {param}");
        throw new Exception ("EthernetIP: acquisition error in a previous request, skip this data");
      }

      string tagName;
      int start = 0;
      int length;
      var parameters = param.Split (new char[] { ':' });
      switch (parameters.Length) {
      case 3:
        start = int.Parse (parameters[1]);
        goto case 2;
      case 2:
        tagName = parameters[0];
        length = int.Parse (parameters[parameters.Length - 1]);
        break;
      default:
        log.Error ($"GetStringArrayOnce: invalid parameter {param}");
        throw new ArgumentException ("Invalid parameter", "param");
      }

      if (log.IsDebugEnabled) {
        log.Debug ($"GetStringArrayOnce: tag={tagName} start={start} length={length}");
      }
      var result = new string[length];
      for (int i = 0; i < length; ++i) {
        var tagIndex = i + start;
        var subTag = $"{tagName}[{tagIndex}]";
        if (log.IsDebugEnabled) {
          log.Debug ($"GetStringArrayOnce: try to read subTag {subTag}");
        }
        string s;
        try {
          s = GetStringV1Once (subTag);
        }
        catch (Exception ex) {
          log.Error ($"GetStringArrayOnce: could not read subTag {subTag}", ex);
          s = null;
        }
        result[i] = s;
      }

      if (!result.Any (s => null == s)) {
        if (log.IsDebugEnabled) {
          log.Debug ($"GetStringArrayOnce: push result in cache tag={tagName}");
        }
        PushInCache (result, param);
      }
      else {
        log.Debug ("GetStringArrayOnce: once of the string is null, do not push it in cache");
      }
      return result;
    }

    /// <summary>
    /// Get a bit
    /// </summary>
    /// <param name="param">Format {name}|{elementCount}|{elementNumber} or just {name}</param>
    /// <returns></returns>
    public bool GetBit (string param)
    {
      return GetValue<bool> (param, 1, (tag, elementNumber) => tag.GetBit (elementNumber));
    }

    /// <summary>
    /// Get a bool
    /// </summary>
    /// <param name="param">Format {name}|{elementCount}|{elementNumber} or just {name}</param>
    /// <returns></returns>
    public bool GetBool (string param)
    {
      return GetValue<bool> (param);
    }

    /// <summary>
    /// Get a UInt8
    /// </summary>
    /// <param name="param">Format {name}|{elementCount}|{elementNumber} or just {name}</param>
    /// <returns></returns>
    public byte GetUInt8 (string param)
    {
      return GetValue<byte> (param);
    }

    /// <summary>
    /// Get a Int8
    /// </summary>
    /// <param name="param">Format {name}|{elementCount}|{elementNumber} or just {name}</param>
    /// <returns></returns>
    public sbyte GetInt8 (string param)
    {
      return GetValue<sbyte> (param);
    }

    /// <summary>
    /// Get a UInt16
    /// </summary>
    /// <param name="param">Format {name}|{elementCount}|{elementNumber} or just {name}</param>
    /// <returns></returns>
    public UInt16 GetUInt16 (string param)
    {
      return GetValue<ushort> (param);
    }

    /// <summary>
    /// Get a Int16
    /// </summary>
    /// <param name="param">Format {name}|{elementCount}|{elementNumber} or just {name}</param>
    /// <returns></returns>
    public Int16 GetInt16 (string param)
    {
      return GetValue<short> (param);
    }

    /// <summary>
    /// Get a UInt32
    /// </summary>
    /// <param name="param">Format {name}|{elementCount}|{elementNumber} or just {name}</param>
    /// <returns></returns>
    public UInt32 GetUInt32 (string param)
    {
      return GetValue<uint> (param);
    }

    /// <summary>
    /// Get a Int32
    /// </summary>
    /// <param name="param">Format {name}|{elementCount}|{elementNumber} or just {name}</param>
    /// <returns></returns>
    public Int32 GetInt32 (string param)
    {
      return GetValue<int> (param);
    }

    /// <summary>
    /// Get a float
    /// </summary>
    /// <param name="param">Format {name}|{elementCount}|{elementNumber} or just {name}</param>
    /// <returns></returns>
    public float GetFloat (string param)
    {
      return GetValue<float> (param);
    }

    /// <summary>
    /// Get a string using directly libplctag
    /// </summary>
    /// <param name="param">Format {name}|{elementCount}|{elementNumber} or just {name}</param>
    /// <returns></returns>
    public string GetString (string param)
    {
      return GetValue<string> (param);
    }

    /// <summary>
    /// Get a string using directly libplctag once
    /// </summary>
    /// <param name="param">Format {name}|{elementCount}|{elementNumber} or just {name}</param>
    /// <returns></returns>
    public string GetStringOnce (string param)
    {
      return GetValue<string> (param, true);
    }

    /// <summary>
    /// Get a string array once using directly libplctag
    /// </summary>
    /// <param name="param">tagName:length or tagName:start:length</param>
    /// <returns></returns>
    public string[] GetStringArrayOnce (string param)
    {
      string[] cacheValue;
      if (TryInCache<string[]> (param, out cacheValue)) {
        log.Info ($"GetStringArrayOnce: get value for {param} in cache");
        return cacheValue;
      }

      if (m_acquisitionError) {
        log.Debug ($"GetStringArrayOnce: acquisition error, skip {param}");
        throw new Exception ("EthernetIP: acquisition error in a previous request, skip this data");
      }

      string tagName;
      int start = 0;
      int length;
      var parameters = param.Split (new char[] { ':' });
      switch (parameters.Length) {
      case 3:
        start = int.Parse (parameters[1]);
        goto case 2;
      case 2:
        tagName = parameters[0];
        length = int.Parse (parameters[parameters.Length - 1]);
        break;
      default:
        log.Error ($"GetStringArrayOnce: invalid parameter {param}");
        throw new ArgumentException ("Invalid parameter", "param");
      }

      if (log.IsDebugEnabled) {
        log.Debug ($"GetStringArrayOnce: tag={tagName} start={start} length={length}");
      }
      var result = new string[length];
      for (int i = 0; i < length; ++i) {
        var tagIndex = i + start;
        var subTag = $"{tagName}[{tagIndex}]";
        if (log.IsDebugEnabled) {
          log.Debug ($"GetStringArrayOnce: try to read subTag {subTag}");
        }
        string s;
        try {
          s = GetStringOnce (subTag);
        }
        catch (Exception ex) {
          log.Error ($"GetStringArrayOnce: could not read subTag {subTag}", ex);
          s = null;
        }
        result[i] = s;
      }

      if (!result.Any (s => null == s)) {
        if (log.IsDebugEnabled) {
          log.Debug ($"GetStringArrayOnce: push result in cache tag={tagName}");
        }
        PushInCache (result, param);
      }
      else {
        log.Debug ("GetStringArrayOnce: once of the string is null, do not push it in cache");
      }
      return result;
    }

    int GetElementSize<T> ()
    {
      return GetElementSize (typeof (T));
    }

    int GetElementSize (Type t)
    {
      // Note: Marshal.SizeOf(Boolean) returns 4 instead of 1
      // This is why this method can't be used
      switch (Type.GetTypeCode (t)) {
      case TypeCode.Boolean:
      case TypeCode.Byte:
      case TypeCode.SByte:
        return 1;
      case TypeCode.UInt16:
      case TypeCode.Int16:
        return 2;
      case TypeCode.UInt32:
      case TypeCode.Int32:
      case TypeCode.Single:
        return 4;
      case TypeCode.String:
        return 1;
      default:
        var elementSize = System.Runtime.InteropServices.Marshal.SizeOf (t);
        log.Warn ($"GetElementSize: type {t} is not listed, use {elementSize}");
        return elementSize;
      }
    }

    Tag GetTag<T> (string tagName, int elementCount)
    {
      int elementSize = GetElementSize<T> ();
      if (log.IsDebugEnabled) {
        log.Debug ($"GetTag: element size is {elementSize} for type {typeof (T)} tag {tagName}");
      }
      return GetTag<T> (tagName, elementCount, elementSize);
    }

    Tag GetTag<T> (string tagName, int elementCount, int elementSize)
    {
      Debug.Assert (!string.IsNullOrEmpty (tagName));

      if (m_acquisitionError) {
        log.Info ($"GetTag: previous acquisition error, skip {tagName}");
        throw new Exception ("EthernetIP: acquisition error in a previous request, skip this data");
      }

      try {
        return m_tagManager.GetTag (tagName, elementCount, elementSize, typeof (T));
      }
      catch (Exception ex) {
        log.Error ($"GetTag: GetTag returned an exception", ex);
        ProcessException (ex);
        throw;
      }
    }

    T GetValue<T> (string param)
    {
      int elementSize = GetElementSize<T> ();
      if (log.IsDebugEnabled) {
        log.DebugFormat ("GetValue: got elementSize={0} for type {1} tag {2}",
          elementSize, typeof (T), param);
      }
      return GetValue<T> (param, elementSize, null);
    }

    T GetValue<T> (string param, bool once)
    {
      int elementSize = GetElementSize<T> ();
      if (log.IsDebugEnabled) {
        log.DebugFormat ("GetValue: got elementSize={0} for type {1} tag {2}",
          elementSize, typeof (T), param);
      }
      return GetValue<T> (param, elementSize, once);
    }

    T GetValue<T> (string param, int elementSize, bool? once = null)
    {
      return GetValue<T> (param, elementSize, (tag, elementNumber) => tag.GetValue (elementNumber), once);
    }

    T GetValue<T> (string param, int elementSize, Func<Tag, int, object> getValue, bool? once = null)
    {
      Debug.Assert (!string.IsNullOrEmpty (param));

      T cacheValue;
      if (TryInCache<T> (param, out cacheValue)) {
        log.InfoFormat ("GetValue: get value for {0} in cache", param);
        return cacheValue;
      }

      if (m_acquisitionError) {
        log.Debug ($"GetValue: acquisition error, skip {param}");
        throw new Exception ("EthernetIP: acquisition error in a previous request, skip this data");
      }

      var tagName = param;
      var elementCount = 1;
      var elementNumber = 0;

      // If an element within an array is to be read
      var split = param.Split ('|');
      if (split.Length == 3) {
        tagName = split[0];
        elementCount = int.Parse (split[1]);
        elementNumber = int.Parse (split[2]);
      }

      var tag = GetTag<T> (tagName, elementCount, elementSize);
      if (once.HasValue) {
        tag.ReadOnce = once.Value;
      }

      try {
        object v = getValue (tag, elementNumber);
        if (log.IsDebugEnabled) {
          log.DebugFormat ("GetValue: read {0} for param {1}", v, param);
        }
        ++m_successfulReadAttempts;
        return (T)v;
      }
      catch (Exception ex) {
        log.Error ($"GetValue: GetValue returned an exception param={param}", ex);
        ProcessException (ex);
        throw;
      }
    }

    /// <summary>
    /// Get a list of alarms, the way to read them being specified in a CSV file
    /// * each line representing an alarm
    /// * description of the rows:
    ///   * first is the parameter to read
    ///   * second is the kind of data (BOOL, INT32)
    ///   * third is the condition to verify so that the alarm is triggered (IS_FALSE, IS_TRUE, POSITIVE)
    ///   * fourth is the alarm message
    ///   * fifth (not mandatory) is the severity
    /// Example of lines:
    /// * LASER_MACHINE_LIFT_ADVANCE_FAULT,BOOL,IS_TRUE,laser machine lift advance fault
    /// * CELL_FAULTS,INT32,POSITIVE,cell fault
    /// </summary>
    /// <param name="param">path of the CSV file</param>
    /// <returns></returns>
    public IList<CncAlarm> GetAlarms (string param)
    {
      if (m_alarmReader == null) {
        m_alarmReader = new AlarmReader (m_tagManager, log, param);
      }

      IList<CncAlarm> alarms = null;
      try {
        alarms = m_alarmReader.GetAlarms ();
      }
      catch (Exception ex) {
        ProcessException (ex);
        throw;
      }
      return alarms;
    }
  }
}
