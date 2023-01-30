// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Build a set of CNC variables from individual CNC variables
  /// </summary>
  public sealed class CncVariableSetBuilder : Lemoine.Cnc.BaseCncModule, Lemoine.Cnc.ICncModule, IDisposable
  {
    #region Members
    #endregion // Members

    #region Getters / Setters    
    /// <summary>
    /// Result
    /// </summary>
    public IDictionary<string, object> CncVariableSet
    {
      get; set;
    }
    #endregion // Getters / Setters

    #region Constructors / Destructor / ToString methods
    /// <summary>
    /// Description of the constructor
    /// </summary>
    public CncVariableSetBuilder ()
      : base("Lemoine.Cnc.InOut.CncVariableSetBuilder")
    {
      this.CncVariableSet = new Dictionary<string, object> ();
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
    /// Add a cnc variable
    /// </summary>
    /// <param name="param">Cnc variable key</param>
    /// <param name="v">Cnc variable value</param>
    public void Add (string param, object v)
    {
      this.CncVariableSet[param] = v;
    }
    #endregion // Methods
  }
}
