// Copyright (C) 2009-2023 Lemoine Automation Technologies
// Copyright (C) 2025 Atsora Solutions
//
// SPDX-License-Identifier: GPL-2.0-or-later

using Lemoine.Core.Log;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Röders acquisition: override FileXml to consider the /erp/data/time node
  /// </summary>
  public sealed class Roeders: FileXml
  {
    TimeSpan m_timeLag;
    bool m_timeLagInitialized = false;
    TimeSpan m_timeLagShift;

    /// <summary>
    /// Last write age
    /// </summary>
    public override TimeSpan LastWriteAge => m_timeLagShift;

    /// <summary>
    /// Description of the constructor
    /// </summary>
    public Roeders ()
      : base ("Lemoine.Cnc.In.Roeders")
    {
    }

    /// <summary>
    /// Start method
    /// </summary>
    /// <returns></returns>
    public override bool Start ()
    {
      if (!base.Start ()) {
        log.Error ("Start: FileXml.Start returned false");
        return false;
      }

      try {
        DateTime now = DateTime.Now;
        string roedersTimeString = GetString ("/erp/data/time");
        DateTime roedersTime = DateTime.Parse (roedersTimeString);
        TimeSpan newLag = now.Subtract (roedersTime);
        if (!m_timeLagInitialized) {
          m_timeLag = newLag;
          m_timeLagInitialized = true;
        }
        else {
          if (newLag < m_timeLag) { // Better time lag: store it
            m_timeLag = newLag;
            m_timeLagShift = TimeSpan.FromSeconds (0);
          }
          else {
            m_timeLagShift = newLag.Subtract (m_timeLag);
          }
        }
      }
      catch (Exception ex) {
        log.Error ($"Start: error while processing the time lag but dismiss it", ex);
      }
      
      return true;
    }

    /// <summary>
    /// Get a set of cnc variables.
    /// </summary>
    /// <param name="param">ListString (first character is the separator)</param>
    /// <returns></returns>
    public IDictionary<string, object> GetCncVariableSet (string param)
    {
      var cncVariables = Lemoine.Collections.EnumerableString.ParseListString (param)
        .Distinct ();
      var result = new Dictionary<string, object> ();
      foreach (var cncVariable in cncVariables) {
        try {
          result[cncVariable] = GetString ($"/erp/data/user/{cncVariable}");
        }
        catch (Exception ex) {
          log.Error ($"GetCncVariableSet: exception for variable {cncVariable}", ex);
        }
      }
      return result;
    }

    /// <summary>
    /// Get a set of cnc variables in double format
    /// </summary>
    /// <param name="param">ListString (first character is the separator)</param>
    /// <returns></returns>
    public IDictionary<string, object> GetCncVariableDoubleSet (string param)
    {
      var cncVariables = Lemoine.Collections.EnumerableString.ParseListString (param)
        .Distinct ();
      var result = new Dictionary<string, object> ();
      foreach (var cncVariable in cncVariables) {
        try {
          result[cncVariable] = GetDouble ($"/erp/data/user/{cncVariable}");
        }
        catch (Exception ex) {
          log.Error ($"GetCncVariableDoubleSet: exception for variable {cncVariable}", ex);
        }
      }
      return result;
    }
  }
}
