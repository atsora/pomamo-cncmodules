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
  /// Module to ...
  /// </summary>
  public sealed class AutoConvert
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

    /// <summary>
    /// Data
    /// </summary>
    public object Data { get; set; }
    #endregion // Getters / Setters

    #region Constructors / Destructor / ToString methods
    /// <summary>
    /// Description of the constructor
    /// </summary>
    public AutoConvert ()
      : base ("Lemoine.Cnc.InOut.AutoConvert")
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
    /// Convert to a <see cref="JPosition"/>
    /// </summary>
    /// <returns></returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage ("Style", "IDE0060:Remove unused parameter", Justification = "Part of the Cnc Engine process")]
    public object ConvertToJPosition (string param)
    {
      if (log.IsDebugEnabled) {
        log.Debug ($"ConvertToPosition");
      }

      try {
        m_data = m_autoConverter.ConvertAuto<JPosition> (m_data);
        return m_data;
      }
      catch (Exception ex) {
        log.Error ($"ConvertToJPosition: exception", ex);
        m_error = true;
        throw;
      }
    }

    /// <summary>
    /// A set method
    /// </summary>
    /// <param name="param"></param>
    /// <param name="data"></param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage ("Style", "IDE0060:Remove unused parameter", Justification = "Part of the Cnc Engine process")]
    public void SetData (string param, object data)
    {
      m_data = data;
    }
  }
}
