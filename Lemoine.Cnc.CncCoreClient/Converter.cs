// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Lemoine.Conversion;
using Lemoine.Core.Log;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Module to convert values
  /// </summary>
  public sealed class Converter
    : Lemoine.Cnc.BaseCncModule, Lemoine.Cnc.ICncModule, IDisposable
  {
    readonly IAutoConverter m_autoConverter = new DefaultAutoConverter ();
    bool m_error = false;
    object m_data = null;

    #region Getters / Setters
    /// <summary>
    /// An error occurred
    /// </summary>
    public bool Error => m_error;
    #endregion // Getters / Setters

    #region Constructors / Destructor / ToString methods
    /// <summary>
    /// Description of the constructor
    /// </summary>
    public Converter ()
      : base ("Lemoine.Cnc.InOut.Converter")
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
    #endregion // Constructors / Destructors / ToString methods

    /// <summary>
    /// Start method
    /// </summary>
    /// <returns>success</returns>
    public bool Start ()
    {
      m_error = false;
      return true;
    }

    /// <summary>
    /// Finish method
    /// </summary>
    public void Finish ()
    {

    }

    /// <summary>
    /// A get method
    /// </summary>
    public object GetData (string param)
    {
      if (log.IsDebugEnabled) {
        log.Debug ($"GetData");
      }
      if (null == m_data) {
        log.Error ($"GetData: null");
        throw new Exception ("No data was pushed");
      }
      return m_data;
    }

    /// <summary>
    /// Get a position
    /// </summary>
    /// <param name="param"></param>
    /// <returns></returns>
    public object GetPosition (string param)
    {
      try {
        return m_autoConverter.ConvertAuto<Position> (m_data);
      }
      catch (Exception ex) {
        log.Error ($"GetPosition: exception", ex);
        throw;
      }
    }

    /// <summary>
    /// Get a Cnc alarm, after converting it
    /// </summary>
    /// <param name="param"></param>
    /// <returns></returns>
    public object GetCncAlarm (string param)
    {
      try {
        return m_autoConverter.ConvertAuto<CncAlarm> (m_data);
      }
      catch (Exception ex) {
        log.Error ($"GetCncAlarm: exception", ex);
        throw;
      }
    }

    /// <summary>
    /// A set method
    /// </summary>
    public void Push (string param, object data)
    {
      if (data is null) {
        log.Error ($"Push: data is null");
      }
      m_data = data;
    }

    /// <summary>
    /// Push a JPosition
    /// </summary>
    /// <param name="param"></param>
    /// <param name="data"></param>
    public void PushJPosition (string param, object data)
    {
      try {
        Push (param, m_autoConverter.ConvertAuto<JPosition> (data));
      }
      catch (Exception ex) {
        log.Error ($"PushJPosition: exception", ex);
        throw;
      }
    }

    /// <summary>
    /// Push an Okuma alarm
    /// </summary>
    public void PushOkumaAlarm (string param, object data)
    {
      try {
        Push (param, m_autoConverter.ConvertAuto<CncCoreClient.Okuma.CCurrentAlarm> (data));
      }
      catch (Exception ex) {
        log.Error ($"PushOkumaAlarm: exception", ex);
        throw;
      }
    }
  }
}
