// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using Lemoine.Core.Log;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Interface shared by all scenario readers
  /// </summary>
  public interface IScenarioReader
  {
    /// <summary>
    /// Process a command
    /// </summary>
    /// <param name="command"></param>
    /// <returns>true if success</returns>
    bool ProcessCommand (string command);

    /// <summary>
    /// Update the log
    /// </summary>
    /// <param name="log"></param>
    void UpdateLog (ILog log);
  }

  /// <summary>
  /// Simulation based on a scenario, providing values for:
  /// - cnc values
  /// - cnc alarms
  /// - tool life
  /// </summary>
  public partial class SimulationScenario : BaseCncModule, ICncModule
  {
    #region Members
    bool m_alreadyStarted = false;
    volatile bool m_stop = false;
    volatile IDictionary<char, IScenarioReader> m_readers = new Dictionary<char, IScenarioReader> ();
    readonly AutoResetEvent m_autoEvent = new AutoResetEvent (false);
    bool m_waitForEnd = false;
    #endregion // Members

    static readonly Regex CHECK_TIME = new Regex (@"^(20|21|22|23|[01][0-9]|[0-9]):[0-5][0-9]:[0-5][0-9]$");

    #region Getters / Setters
    /// <summary>
    /// Path of the scenario to read
    /// 
    /// If relative, consider it is relative to CommonConfigDirectory
    /// </summary>
    public string ScenarioPath { get; set; }

    /// <summary>
    /// If true, the scenario is repeated again and again
    /// (default is true)
    /// </summary>
    public bool AutoLoop { get; set; }

    /// <summary>
    /// True if a scenario contains an error
    /// </summary>
    public bool Error { get; private set; }

    /// <summary>
    /// True if the simulation ended
    /// </summary>
    public bool End { get { return m_stop; } }

    /// <summary>
    /// Cnc Acquisition ID
    /// </summary>
    public override int CncAcquisitionId
    {
      get { return base.CncAcquisitionId; }
      set {
        base.CncAcquisitionId = value;
        lock (m_readers) {
          foreach (var reader in m_readers.Values) {
            reader.UpdateLog (log);
          }
        }
      }
    }
    #endregion // Getters / Setters

    #region Constructors
    /// <summary>
    /// Description of the constructor
    /// </summary>
    public SimulationScenario () : base ("Lemoine.Cnc.In.SimulationScenario")
    {
      AutoLoop = true;
      Error = false;
      lock (m_readers) {
        m_readers['T'] = new ScenarioReaderToolLife (log);
        m_readers['V'] = new ScenarioReaderCncValue (log);
        m_readers['A'] = new ScenarioReaderCncAlarm (log);
      }
    }

    /// <summary>
    /// <see cref="IDisposable.Dispose" />
    /// </summary>
    public void Dispose ()
    {
      m_stop = true;
      m_autoEvent.Set ();
      GC.SuppressFinalize (this);
    }
    #endregion // Constructors

    #region Scenario methods
    /// <summary>
    /// Start method (data begins to be read)
    /// </summary>
    public void Start ()
    {
      if (string.IsNullOrEmpty (ScenarioPath)) {
        m_stop = true;
      }
      else if (!m_alreadyStarted) {
        m_alreadyStarted = true;
        var thread = new Thread (ReadingProgram);
        thread.Start ();
      }
    }

    /// <summary>
    /// Finish method (end of the reading of data)
    /// </summary>
    public void Finish ()
    {
      if (m_waitForEnd) {
        m_waitForEnd = false;
        m_autoEvent.Set ();
      }
    }

    void ReadingProgram ()
    {
      try {
        // Loop
        bool firstTime = true;
        while (!m_stop && (AutoLoop || firstTime)) {
          firstTime = false;
          string line;
          var path = this.ScenarioPath;
          if (!Path.IsPathRooted (path)) {
            path = Path.Combine (Lemoine.Info.PulseInfo.CommonConfigurationDirectory, path);
            if (log.IsDebugEnabled) {
              log.Debug ($"ReadingProgram: consider file {this.ScenarioPath} relative to CommonConfigurationDirectory");
            }
          }
          if (!File.Exists (path)) {
            log.Error ($"ReadingProgram: file {path} does not exist");
          }
          using (TextReader reader = new StreamReader (ScenarioPath)) {
            while (!m_stop && (line = reader.ReadLine ()) != null) {
              // Remove comment
              string[] parts = line.Split ('#');
              line = (parts.Length > 0) ? parts[0] : "";

              // Process the line if it is not empty
              if (!string.IsNullOrEmpty (line)) {
                ProcessLine (line);
              }
            }
          }
        }
      }
      catch (Exception ex) {
        log.Error ($"ReadingProgram: exception for scenarionPath={this.ScenarioPath}", ex);
        Error = true;
      }

      m_stop = true;
    }

    /// <summary>
    /// Process a line from the scenario
    /// public method so that it can be used by unit tests
    /// </summary>
    /// <param name="line"></param>
    public void ProcessLine (string line)
    {
      if (CHECK_TIME.IsMatch (line)) {
        Pause (line);
      }
      else if (line.ToLower () == "wait") {
        Pause ();
      }
      else {
        if (line.Length > 2 && line[1] == '>') {
          if (log.IsDebugEnabled) {
            log.Debug ($"ProcessLine: line={line}");
          }

          // Retrieve the command type and the command
          var commandType = line.ToUpper ()[0];
          var command = line.Remove (0, 2).Trim (' ');

          bool ok = false;
          if (!string.IsNullOrEmpty (command)) {
            lock (m_readers) {
              if (m_readers.ContainsKey (commandType)) {
                ok = m_readers[commandType].ProcessCommand (command);
              }
            }
          }

          if (!ok) {
            Error = true;
            log.Error ($"ProcessLine: unknown command '{command}' => skip it");
          }
        }
        else {
          Error = true;
          log.Error ($"ProcessLine: no command type in '{line}' => skip it");
        }
      }
    }

    void Pause (string strTime)
    {
      try {
        // Time to wait
        TimeSpan waitTime = TimeSpan.Parse (strTime);
        log.Debug ($"Pause: Wait {strTime}");
        Timer timer = null;
        timer = new Timer (
          (obj) => {
            m_autoEvent.Set ();
            timer.Dispose ();
          }, null, waitTime, TimeSpan.FromSeconds (20));
        m_autoEvent.WaitOne (); // Wait until the timer release the autoEvent
      }
      catch (Exception ex) {
        Error = true;
        log.Error ($"Pause: invalid line '{strTime}' => skip it", ex);
      }
    }

    void Pause ()
    {
      // Wait until the next reading is finished before continuing
      log.Debug ("Pause: wait for the next reading");
      m_waitForEnd = true;
      m_autoEvent.WaitOne ();
    }
    #endregion // Scenario methods
  }
}
