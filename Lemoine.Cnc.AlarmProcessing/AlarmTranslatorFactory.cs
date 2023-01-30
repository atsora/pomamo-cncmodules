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
  /// Basic alarm translator
  /// 
  /// <see cref="IAlarmTranslator"/>
  /// </summary>
  public class BasicAlarmTranslator : IAlarmTranslator
  {
    IDictionary<string, string> m_numberMessage = new Dictionary<string, string> ();

    /// <summary>
    /// <see cref="IAlarmTranslator"/>
    /// </summary>
    /// <param name="parameters"></param>
    /// <returns></returns>
    public bool Initialize (IDictionary<string, string> parameters)
    {
      return true;
    }

    internal void AddMessage (string number, string message)
    {
      m_numberMessage[number] = message;
    }

    /// <summary>
    /// <see cref="IAlarmTranslator"/>
    /// </summary>
    /// <param name="alarm"></param>
    public void ProcessAlarm (CncAlarm alarm)
    {
      var number = alarm.Number;
      if (m_numberMessage.TryGetValue (number, out string message)) {
        alarm.Message = message;
      }
    }
  }

  /// <summary>
  /// Module to build an alarm translator
  /// </summary>
  public sealed class AlarmTranslatorFactory : Lemoine.Cnc.BaseCncModule, Lemoine.Cnc.ICncModule, IDisposable
  {
    #region Members
    bool m_error = false;
    BasicAlarmTranslator m_alarmTranslator = new BasicAlarmTranslator ();
    #endregion

    #region Getters / Setters
    /// <summary>
    /// An error occurred
    /// </summary>
    public bool Error => m_error;
    #endregion

    #region Constructors / Destructor / ToString methods
    /// <summary>
    /// Description of the constructor
    /// </summary>
    public AlarmTranslatorFactory ()
      : base ("Lemoine.Cnc.InOut.AlarmTranslatorFactory")
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

    /// <summary>
    /// Start method: reset the different values
    /// </summary>
    public void Start ()
    {
      m_error = false;
    }

    /// <summary>
    /// Finish method
    /// </summary>
    public void Finish ()
    {

    }

    /// <summary>
    /// Use a DictionaryString from <see cref="Lemoine.Collections.EnumerableString"/>
    /// to associate a number to a message
    /// 
    /// <item>the first character is the separator between a key and a value</item>
    /// <item>the second character is the separator character between the elements</item>
    /// </summary>
    /// <param name="param"></param>
    /// <returns><see cref="IAlarmTranslator"/></returns>
    public IAlarmTranslator AddNumberMessages (string param)
    {
      try {
        var dictionary = Lemoine.Collections.EnumerableString
          .ParseDictionaryString (param);
        foreach (var kv in dictionary) {
          m_alarmTranslator.AddMessage (kv.Key, kv.Value);
        }
        return m_alarmTranslator;
      }
      catch (Exception ex) {
        log.Error ("AddNumberMessage: exception", ex);
        throw;
      }
    }
  }
}
