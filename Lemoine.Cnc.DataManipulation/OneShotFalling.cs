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
  /// One Shot Falling (OSF) implementation
  /// 
  /// OutputBit is true if IN is 
  /// </summary>
  public sealed class OneShotFalling : Lemoine.Cnc.BaseCncModule, Lemoine.Cnc.ICncModule, IDisposable
  {
    #region Members
    bool m_validated = false;
    bool? m_in = null;
    bool? m_storageBit = null;
    bool m_outputBit = false;
    #endregion

    #region Getters / Setters
    /// <summary>
    /// Input bit
    /// </summary>
    public bool IN
    {
      set
      {
        m_in = value;
        m_validated = false;
        ValidateInput ();
      }
    }

    /// <summary>
    /// Output bit
    /// </summary>
    public bool OB
    {
      get
      {
        ValidateInput ();
        return m_outputBit;
      }
    }

    /// <summary>
    /// Storage bit
    /// </summary>
    public bool? SB
    {
      get
      {
        ValidateInput ();
        return m_storageBit;
      }
    }
    #endregion // Getters / Setters

    #region Constructors / Destructor / ToString methods
    /// <summary>
    /// Description of the constructor
    /// </summary>
    public OneShotFalling ()
      : base ("Lemoine.Cnc.InOut.OneShotFalling")
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
      m_in = null;
      m_validated = false;
    }

    /// <summary>
    /// Finish method
    /// </summary>
    public void Finish ()
    {
      m_outputBit = false;
    }

    void ValidateInput ()
    {
      if (m_validated) {
        return; // Already done
      }

      if (m_storageBit.HasValue && m_storageBit.Value && m_in.HasValue && !m_in.Value) {
        m_outputBit = true;
      }
      else {
        m_outputBit = false;
      }
      m_storageBit = m_in;
      m_validated = true;
    }
    #endregion // Methods
  }
}
