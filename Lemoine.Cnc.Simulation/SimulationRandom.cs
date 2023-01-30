// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections;
using System.Threading;
using Lemoine.Database.Persistent;
using Lemoine.GDBPersistentClasses;
using NHibernate;
using NHibernate.Criterion;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Simulator providing random values, not based on a scenario
  /// Exceptions are sometimes triggered
  /// </summary>
  public class SimulationRandom : BaseCncModule, ICncModule, IDisposable
  {
    #region Members
    Random random = new Random ();
    #endregion

    #region Constructors / Destructor / ToString methods
    /// <summary>
    /// Description of the constructor
    /// </summary>
    public SimulationRandom () : base ("Lemoine.Cnc.In.SimulationRandom")
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
    /// Get an automatic value given a range in a string parameter (low-high)
    /// You can give some default value in case the parameter is empty
    /// or not valid.
    /// </summary>
    /// <param name="param">String parameter "low-high"</param>
    /// <param name="low">Default low value</param>
    /// <param name="high">Default high value</param>
    /// <returns></returns>
    int GetAutoValue (string param, int low, int high)
    {
      int intlow = low;
      int inthigh = high;
      if (param.Length > 0) {
        try {
          string[] parameters = param.Split ('-');
          intlow = int.Parse (parameters[0]);
          inthigh = int.Parse (parameters[1]);
        }
        catch (Exception ex) {
          log.Error ($"GetAutoValue: invalid param {param}, raised exception", ex);
        }
      }
      return GetAutoValue (intlow, inthigh);
    }

    /// <summary>
    /// Get an automatic value given a range
    /// </summary>
    /// <param name="low"></param>
    /// <param name="high"></param>
    /// <returns></returns>
    int GetAutoValue (int low, int high)
    {
      return random.Next (low, high);
    }

    /// <summary>
    /// Get an auto bool value.
    /// Return true every 'frequency' times in parameter.
    /// </summary>
    /// <param name="param"></param>
    /// <param name="frequency">Default frequency</param>
    /// <returns></returns>
    bool GetAutoBool (string param, int frequency)
    {
      int high = frequency;
      if (param.Length > 0) {
        int.TryParse (param, out high);
      }
      if (random.NextDouble () * high < 1) {
        return true;
      }
      else {
        return false;
      }
    }

    /// <summary>
    /// Get a double value
    /// Default range is 0-1000
    /// </summary>
    /// <param name="param">range for the simulated double value</param>
    /// <returns></returns>
    public double GetDouble (string param)
    {
      return GetAutoValue (param, 0, 1000);
    }

    /// <summary>
    /// Get an int value
    /// Default range is 0-1000
    /// </summary>
    /// <param name="param">range for the simulated int value</param>
    /// <returns></returns>
    public int GetInt (string param)
    {
      return GetAutoValue (param, 0, 1000);
    }

    /// <summary>
    /// Get a long value
    /// Default range is 0-1000
    /// </summary>
    /// <param name="param">range for the simulated long value</param>
    /// <returns></returns>
    public long GetLong (string param)
    {
      return GetAutoValue (param, 0, 1000);
    }

    /// <summary>
    /// Get the error status.
    /// Default: every 25 times.
    /// </summary>
    /// <param name="param">Inverse of the probability to get an error</param>
    /// <returns></returns>
    public bool GetError (string param)
    {
      return GetAutoBool (param, 25); // 1/25 of errors by default
    }

    /// <summary>
    /// Get the stamp ID
    /// 
    /// Default: every 100 times
    /// </summary>
    /// <param name="param">Inverse of the probability to set effectively an new stamp, else raise an exception</param>
    /// <returns></returns>
    public int GetStampId (string param)
    {
      if (!GetAutoBool (param, 100)) { // 1/100 of probability to get false
        throw new Exception ();
      }

      using (ISession session = NHibernateHelper.OpenSession ()) {
        int count =
          session.CreateCriteria<Stamp> ()
          .SetProjection (Projections.Count ("Id"))
          .UniqueResult<int> ();
        log.DebugFormat ("GetStampId: " +
                         "there are {0} stamps",
                         count);
        if (count > 0) {
          int offset = random.Next (count);
          Stamp stamp =
            session.CreateQuery ("from Stamp stamp")
            .SetFirstResult (offset)
            .SetMaxResults (1)
            .UniqueResult<Stamp> ();
          log.DebugFormat ("GetStampId: " +
                           "got {0} at offset {1}",
                           stamp, offset);
          if (stamp != null) {
            return (int)stamp.Id;
          }
          else {
            log.ErrorFormat ("GetStampId: " +
                             "got a null operation");
            return 0;
          }
        }
        else {
          log.WarnFormat ("GetStampId: " +
                          "no operation in database");
          return 0;
        }
      }
    }

    /// <summary>
    /// Get the feedrate
    /// Default range is 0-1000
    /// </summary>
    /// <param name="param">range for the simulated feedrate</param>
    /// <returns></returns>
    public double GetFeedrate (string param)
    {
      return GetAutoValue (param, 0, 1000);
    }

    /// <summary>
    /// Get the position
    /// Default range is 0-100
    /// </summary>
    /// <param name="param">range for the simulated position</param>
    /// <returns></returns>
    public Position GetPosition (string param)
    {
      return new Position (GetAutoValue (param, 0, 100),
                           GetAutoValue (param, 0, 100),
                           GetAutoValue (param, 0, 100));
    }

    /// <summary>
    /// Get the rotation feed
    /// Default range is 0-100
    /// </summary>
    /// <param name="param">range for the simulated rotation feed</param>
    /// <returns></returns>
    public double GetRotationFeed (string param)
    {
      return GetAutoValue (param, 0, 100);
    }

    /// <summary>
    /// Get the spindle load
    /// Default range is 0-100
    /// </summary>
    /// <param name="param">range for the simulated spindle load</param>
    /// <returns></returns>
    public double GetSpindleLoad (string param)
    {
      return GetAutoValue (param, 0, 100);
    }

    /// <summary>
    /// Get the spindle speed
    /// Default range is 0-100
    /// </summary>
    /// <param name="param">range for the simulated spindle speed</param>
    /// <returns></returns>
    public double GetSpindleSpeed (string param)
    {
      return GetAutoValue (param, 0, 100);
    }

    /// <summary>
    /// Get the manual status.
    /// Default: set to manual every 25 times.
    /// </summary>
    /// <param name="param">Inverse of the probability to get the manual status</param>
    /// <returns></returns>
    public bool GetManual (string param)
    {
      return GetAutoBool (param, 25);
    }

    /// <summary>
    /// Get the completion
    /// </summary>
    /// <param name="param">range for the simulated completion</param>
    /// <returns></returns>
    public double GetCompletion (string param)
    {
      return GetAutoValue (param, 0, 100);
    }

    /// <summary>
    /// Get the feedrate override.
    /// Default range is 0-200.
    /// </summary>
    /// <param name="param"></param>
    /// <returns></returns>
    public int GetFeedrateOverride (string param)
    {
      return GetAutoValue (param, 0, 200);
    }

    /// <summary>
    /// Get the feedrate override.
    /// Default range is 0-200.
    /// </summary>
    /// <param name="param"></param>
    /// <returns></returns>
    public int GetSpindleSpeedOverride (string param)
    {
      return GetAutoValue (param, 0, 200);
    }

    /// <summary>
    /// Get the running status.
    /// Default: set to idle every 5 times
    /// </summary>
    /// <param name="param">Inverse of the probability to get the idle status</param>
    /// <returns></returns>
    public bool GetRunning (string param)
    {
      return !GetAutoBool (param, 5);
    }

    /// <summary>
    /// Get the block number
    /// Default range is 0-10
    /// </summary>
    /// <param name="param">range for the simulated block number</param>
    /// <returns></returns>
    public long GetBlockNumber (string param)
    {
      return (long)GetAutoValue (param, 0, 10);
    }

    // TODO: GetStartEnd

    /// <summary>
    /// Get a list of string events as given by param
    /// 
    /// The first character in param is the separator
    /// </summary>
    /// <param name="param"></param>
    /// <returns></returns>
    public Queue GetEvents (string param)
    {
      Queue queue = new Queue ();
      char separator = param[0];
      foreach (string item in param.Split (new Char[] { separator },
                                           StringSplitOptions.RemoveEmptyEntries)) {
        log.DebugFormat ("GetEvents: " +
                         "push event {0}",
                         item);
        queue.Enqueue (item);
      }
      return queue;
    }

    /// <summary>
    /// Loop
    /// </summary>
    /// <param name="param"></param>
    /// <returns></returns>
    public double Loop (string param)
    {
      while (true) {
        log.Debug ("Loop: " +
                   "one more loop (every 5 s)");
        Thread.Sleep (5000); // 5 s
      }
    }
    #endregion
  }
}
