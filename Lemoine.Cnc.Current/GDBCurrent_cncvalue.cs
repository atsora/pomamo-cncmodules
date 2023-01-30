// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Lemoine.Model;
using Lemoine.ModelDAO;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Description of GDBCurrent
  /// </summary>
  public partial class GDBCurrent
  {
    #region Members
    ISet<string> m_stoppedCncValues = new HashSet<string> ();
    #endregion // Members

    #region Methods
    /// <summary>
    /// Set a Cnc value
    /// </summary>
    /// <param name="param">Field key</param>
    /// <param name="v">Field value</param>
    public void SetCncValue (string param, object v)
    {
      // - Remove the data from the set of already stopped Cnc values
      m_stoppedCncValues.Remove (param);

      // - Set the Cnc values
      Debug.Assert (null != m_machineModule);
      if (null == m_machineModule) {
        log.ErrorFormat ("SetCncValue: " +
                         "unknown machine module");
        throw new Exception ("Unknown machine module");
      }

      try {
        using (var transaction = this.Session.BeginTransaction ("Cnc.Current.SetCncValue")) {
          IField field = ModelDAOHelper.DAOFactory.FieldDAO.FindByCode (param);
          if (null == field) {
            log.ErrorFormat ("SetCncValue: " +
                             "unknown field code {0}",
                             param);
            transaction.Commit ();
            throw new ArgumentException ("param does not correspond to any field code");
          }

          ICurrentCncValue currentCncValue =
            ModelDAOHelper.DAOFactory.CurrentCncValueDAO
            .Find (m_machineModule, field);
          if (null == currentCncValue) {
            log.DebugFormat ("SetCncValue: " +
                             "create a new CurrentCncValue");
            currentCncValue = ModelDAOHelper.ModelFactory.CreateCurrentCncValue (m_machineModule, field);
          }
          currentCncValue.DateTime = DateTime.UtcNow;
          currentCncValue.Value = v;
          ModelDAOHelper.DAOFactory.CurrentCncValueDAO.MakePersistent (currentCncValue);
          transaction.Commit ();
        }
      }
      catch (ArgumentException) {
        throw;
      }
      catch (Exception ex) {
        log.ErrorFormat ("SetCncValue: " +
                         "exception {0}",
                         ex);
        CloseSession ();
        throw;
      }
    }

    /// <summary>
    /// Stop the given Cnc values if the condition is true
    /// </summary>
    /// <param name="param">Cnc value list separated by a comma (,)</param>
    /// <param name="v">Condition</param>
    public void StopCncValue (string param, object v)
    {
      if ((bool)v) {
        Debug.Assert (null != m_machineModule);
        if (null == m_machineModule) {
          log.ErrorFormat ("StopCncValue: " +
                           "unknown machine module");
          throw new Exception ("Unknown machine module");
        }

        foreach (string fieldKey in param.Split (new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries)) {
          if (m_stoppedCncValues.Contains (fieldKey)) {
            // The field has already been stopped
            continue;
          }

          try {
            using (var transaction = this.Session.BeginTransaction ("Cnc.Current.StopCncValue")) {
              IField field =
                ModelDAOHelper.DAOFactory.FieldDAO
                .FindByCode (fieldKey);
              if (null == field) {
                log.WarnFormat ("StopCncValue: " +
                                "unknown field code {0}",
                                fieldKey);
                transaction.Commit ();
                continue;
              }

              ICurrentCncValue currentCncValue =
                ModelDAOHelper.DAOFactory.CurrentCncValueDAO
                .Find (m_machineModule, field);
              if (null != currentCncValue) {
                log.DebugFormat ("StopCncValue: " +
                                 "make transient cnc value of field code {0}",
                                 fieldKey);
                ModelDAOHelper.DAOFactory.CurrentCncValueDAO.MakeTransient (currentCncValue);
              }
              transaction.Commit ();
            }
          }
          catch (Exception ex) {
            log.ErrorFormat ("StopCncValue: " +
                             "exception {0}",
                             ex);
            CloseSession ();
            throw;
          }

          // The data was successfully discontinued:
          // add it to the set of already stopped cnc values
          m_stoppedCncValues.Add (fieldKey);
        }
      }
    }

    /// <summary>
    /// Stop the given Cnc values if the condition is false
    /// </summary>
    /// <param name="param">Cnc value list separated by a comma (,)</param>
    /// <param name="v">Condition</param>
    public void StopCncValueIfNot (string param, object v)
    {
      StopCncValue (param, !((bool)v));
    }
    #endregion // Methods
  }
}
