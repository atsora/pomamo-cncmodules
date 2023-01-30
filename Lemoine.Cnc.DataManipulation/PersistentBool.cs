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
  /// Persist a boolean value and implement Latch/Unlatch
  /// </summary>
  public sealed class PersistentBool : Lemoine.Cnc.BaseCncModule, Lemoine.Cnc.ICncModule, IDisposable
  {
    #region Members
    bool m_data = false;
    #endregion

    #region Getters / Setters
    /// <summary>
    /// Persistent stored value
    /// </summary>
    public bool Output => m_data;

    /// <summary>
    /// Latch and return the output
    /// </summary>
    public bool OutputAfterLatch
    {
      get
      {
        Latch ("", true);
        return this.Output;
      }
    }

    /// <summary>
    /// Unlatch and return the output
    /// </summary>
    public bool OutputAfterUnlatch
    {
      get
      {
        Unlatch ("", true);
        return this.Output;
      }
    }

    #endregion // Getters / Setters

    #region Constructors / Destructor / ToString methods
    /// <summary>
    /// Description of the constructor
    /// </summary>
    public PersistentBool ()
      : base ("Lemoine.Cnc.InOut.PersistentBool")
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
    }

    /// <summary>
    /// Finish method
    /// </summary>
    public void Finish ()
    {
    }

    /// <summary>
    /// Implement Output Latch (OTL)
    /// 
    /// Latch the current persistent data
    /// </summary>
    /// <param name="param"></param>
    /// <param name="data"></param>
    public void Latch (string param, object data)
    {
      var b = Convert.ToBoolean (data);
      if (b) {
        m_data = true;
      }
    }

    /// <summary>
    /// Implement Output Unlatch (OTU)
    /// 
    /// Unlatch the current persistent data
    /// </summary>
    /// <param name="param"></param>
    /// <param name="data"></param>
    public void Unlatch (string param, object data)
    {
      var b = Convert.ToBoolean (data);
      if (b) {
        m_data = false;
      }
    }
    #endregion // Methods
  }
}
