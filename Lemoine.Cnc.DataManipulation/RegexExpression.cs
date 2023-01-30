// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Lemoine.I18N;
using Lemoine.Core.Log;
using System.Text.RegularExpressions;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Module to evaluate regex expression.
  /// 
  /// It uses the inverse polish notation to set and get the data.
  /// Remember your old HP48...
  /// </summary>
  public sealed class RegexExpression : Lemoine.Cnc.BaseCncModule, Lemoine.Cnc.ICncModule, IDisposable
  {
    #region Members
    Regex m_regex = null;
    string m_line = null;

    bool error = false;
    #endregion

    #region Getters / Setters
    public string RegexString
    {
      set {
        try {
          m_regex = new Regex (value, RegexOptions.Compiled);
        }
        catch (Exception e) {
          if (log.IsDebugEnabled) {
            log.Debug ($"RegexParser: RegexString {e}");
          }
        }
      }
    }

    /// <summary>
    /// An error occurred
    /// </summary>
    public bool Error
    {
      get { return error; }
    }
    #endregion

    #region Constructors / Destructor / ToString methods
    

    /// <summary>
    /// Description of the constructor
    /// </summary>
    public RegexExpression ()
      : base ("Lemoine.Cnc.InOut.RegexParser")
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

    public void SetLine (string param)
    {
      log.Info ($"SetLine: line={param}");
      m_line = param;
    }

    public string GetGroup (string param)
    {
      log.Info ($"GetGroup: regex={m_regex.ToString()}");
      log.Info ($"GetGroup: line={m_line}");
      log.Info ($"GetGroup: param={param}");
      var match = m_regex.Match (m_line);
      if (!match.Success) {
        log.Info ($"GetGroup:param {param} not match with regex {m_regex.ToString()}");
        return null;
      }
      return match.Groups[param].Value;
    }



    #endregion // Methods

  }
}
