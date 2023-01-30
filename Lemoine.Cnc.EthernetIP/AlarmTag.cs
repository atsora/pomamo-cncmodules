// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Description of AlarmTag.
  /// </summary>
  internal class AlarmTag
  {
    /// <summary>
    /// Condition defining if the alarm is on
    /// </summary>
    public enum Condition
    {
      /// <summary>
      /// The value must be "true"
      /// </summary>
      IS_TRUE,

      /// <summary>
      /// The value must be "false
      /// </summary>
      IS_FALSE,

      /// <summary>
      /// The value must be > 0
      /// </summary>
      POSITIVE
    }

    #region Members
    readonly Tag m_tag;
    readonly Condition m_condition;
    readonly string m_message;
    #endregion // Members

    #region Getters / Setters
    /// <summary>
    /// Parameter of the alarm
    /// </summary>
    public string Parameter { get; private set; }
    #endregion // Getters / Setters

    #region Constructors
    /// <summary>
    /// Description of the constructor
    /// </summary>
    /// <param name="tag"></param>
    /// <param name="condition"></param>
    /// <param name="parameter"></param>
    /// <param name="message"></param>
    public AlarmTag (Tag tag, Condition condition, string parameter, string message)
    {
      m_tag = tag;
      m_condition = condition;
      Parameter = parameter;
      m_message = message;
    }
    #endregion // Constructors

    #region Methods
    /// <summary>
    /// Get a cnc alarm if it's been detected as on
    /// </summary>
    /// <returns></returns>
    public CncAlarm GetAlarm ()
    {
      // Read the value
      object value = m_tag.GetValue (0);

      bool enabled = false;
      switch (m_condition) {
      case Condition.IS_TRUE:
        enabled = object.Equals (value, true);
        break;
      case Condition.IS_FALSE:
        enabled = object.Equals (value, false);
        break;
      case Condition.POSITIVE:
        enabled = Convert.ToDecimal (value) > 0;
        break;
      }

      if (!enabled) {
        return null;
      }

      // Create an alarm
      var alarm = new CncAlarm ("EthernetIP", "PLC", Parameter);
      alarm.Message = m_message;
      return alarm;
    }
    #endregion // Methods
  }
}
