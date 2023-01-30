// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
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
    #region Getters / Setters
    /// <summary>
    /// Machine Mode Id
    /// </summary>
    public int MachineModeId
    {
      set
      {
        log.DebugFormat ("MachineModeId.set: machine mode id to {0}", value);

        try {
          using (var transaction = this.Session.BeginTransaction ("Cnc.Current.MachineModeId")) {
            IMachineMode machineMode =
              ModelDAOHelper.DAOFactory.MachineModeDAO.FindById (value);
            if (null == machineMode) {
              log.ErrorFormat ("MachineModeId.set: No machine mode with Id={0}", value);
              transaction.Commit ();
              throw new ArgumentException ("Unknown machine mode Id");
            }
            else {
              SetMachineMode (machineMode);
            }
            transaction.Commit ();
          }
        }
        catch (ArgumentException) {
          throw;
        }
        catch (Exception ex) {
          log.ErrorFormat ("MachineModeId.set: " +
                           "exception {0}",
                           ex);
          CloseSession ();
          throw;
        }
      }
    }

    /// <summary>
    /// Machine Mode Translation Key Or Name
    /// </summary>
    public string MachineModeTranslationKeyOrName
    {
      set
      {
        Debug.Assert (null != value);
        log.DebugFormat ("MachineModeTranslationKeyOrName.set: " +
                         "machine mode translation key or name to {0}",
                         value);

        try {
          using (var transaction = this.Session.BeginTransaction ("Cnc.Current.MachineModeTranslationKeyOrName")) {
            IMachineMode machineMode =
              ModelDAOHelper.DAOFactory.MachineModeDAO.FindByTranslationKeyOrName (value);
            if (null == machineMode) {
              log.ErrorFormat ("MachineModeTranslationKeyOrName.set: " +
                               "No machine mode with TranslationKeyOrName={0}",
                               value);
              transaction.Commit ();
              throw new ArgumentException ("Unknown machine mode translation key or name");
            }
            else {
              SetMachineMode (machineMode);
            }
            transaction.Commit ();
          }
        }
        catch (ArgumentException) {
          throw;
        }
        catch (Exception ex) {
          log.ErrorFormat ("MachineModeTranslationKeyOrName.set: exception {0}", ex);
          CloseSession ();
          throw;
        }
      }
    }
    #endregion // Getters / Setters

    #region Methods
    void SetMachineMode (IMachineMode machineMode)
    {
      Debug.Assert (null != machineMode);
      Debug.Assert (0 != machineMode.Id);
      Debug.Assert (null != m_machine);
      Debug.Assert (null != m_session);

      ICurrentMachineMode currentMachineMode =
        ModelDAOHelper.DAOFactory.CurrentMachineModeDAO
        .Find (m_machine);
      if (null == currentMachineMode) {
        log.DebugFormat ("SetMachineMode: create a new CurrentMachineMode");
        currentMachineMode = ModelDAOHelper.ModelFactory.CreateCurrentMachineMode (m_machine);
      }
      currentMachineMode.DateTime = DateTime.UtcNow;
      currentMachineMode.MachineMode = machineMode;
      ModelDAOHelper.DAOFactory.CurrentMachineModeDAO.MakePersistent (currentMachineMode);
    }
    #endregion // Methods
  }
}