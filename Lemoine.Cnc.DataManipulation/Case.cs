// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;

using Lemoine.Core.Log;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Description of Case.
  /// </summary>
  public sealed class Case: Lemoine.Cnc.BaseCncModule, Lemoine.Cnc.ICncModule, IDisposable
  {
    #region Members
    string m_result = null;
    #endregion // Members

    #region Getters / Setters    
    /// <summary>
    /// Result
    /// </summary>
    public string Result {
      get {
        if (null != m_result) {
          return m_result;
        }
        else { // null == m_result
          log.ErrorFormat ("Result.get: " +
                           "null value, no match");
          throw new Exception ("No match");
        }
      }
      set { m_result = value; }
    }
    #endregion // Getters / Setters

    #region Constructors / Destructor / ToString methods
    /// <summary>
    /// Description of the constructor
    /// </summary>
    public Case ()
      : base("Lemoine.Cnc.InOut.Case")
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
    /// Add a case condition
    /// </summary>
    /// <param name="param">Value to use for the result in case v is true</param>
    /// <param name="v"></param>
    public void AddCase (string param, bool v)
    {
      if (v) {
        log.DebugFormat ("AddCase: " +
                         "{0} is true => result is {1}",
                         v, param);
        m_result = param;
      }
    }
    
    /// <summary>
    /// Add a case condition
    /// </summary>
    /// <param name="param">Value to use for the result in case v is false</param>
    /// <param name="v"></param>
    public void AddCaseNot (string param, bool v)
    {
      if (!v) {
        log.DebugFormat ("AddCase: " +
                         "{0} is false => result is {1}",
                         v, param);
        m_result = param;
      }
    }
    #endregion // Methods
  }
}
