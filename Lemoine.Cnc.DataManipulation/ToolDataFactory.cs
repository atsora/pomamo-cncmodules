// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections.Generic;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Description of ToolDataFactory.
  /// </summary>
  public sealed class ToolDataFactory : Lemoine.Cnc.BaseCncModule, Lemoine.Cnc.ICncModule, IDisposable
  {
    #region Members
    IDictionary<int, double> m_currentData = null;
    IDictionary<int, double> m_limitData = null;
    IDictionary<int, double> m_warningData = null;
    IDictionary<int, double> m_lengthCompensationData = null;
    IDictionary<int, double> m_diameterCompensationData = null;
    ToolLifeData m_toolLifeData = null;
    double m_multiplier = 1.0;
    string m_toolUnitString = "NumberOfTimes";
    Lemoine.Core.SharedData.ToolUnit m_toolUnit = Core.SharedData.ToolUnit.NumberOfTimes;
    string m_directionString = "Up";
    Lemoine.Core.SharedData.ToolLifeDirection m_direction = Core.SharedData.ToolLifeDirection.Up;
    #endregion // Members

    #region Getters / Setters
    /// <summary>
    /// Get the computed tool life data
    /// </summary>
    public ToolLifeData ToolLifeData {
      get {
        if (m_toolLifeData == null) {
          ComputeToolLifeData ();
        }

        return m_toolLifeData;
      }
    }
    
    /// <summary>
    /// Get / set the current tool life values
    /// </summary>
    public IDictionary<int, double> CurrentToolLife {
      get { return m_currentData; }
      set {
        log.InfoFormat("ToolDataFactory: received {0} current data", value.Count);
        m_currentData = value;
        m_toolLifeData = null;
      }
    }

    /// <summary>
    /// Get / set the warning for tool life data
    /// </summary>
    public IDictionary<int, double> ToolLifeWarning
    {
      get { return m_warningData; }
      set
      {
        log.InfoFormat ("ToolDataFactory: received {0} warnings", value.Count);
        m_warningData = value;
        m_toolLifeData = null;
      }
    }

    /// <summary>
    /// Get / set the limit for tool life data
    /// </summary>
    public IDictionary<int, double> ToolLifeLimit
    {
      get { return m_limitData; }
      set
      {
        log.InfoFormat ("ToolDataFactory: received {0} limits", value.Count);
        m_limitData = value;
        m_toolLifeData = null;
      }
    }

    /// <summary>
    /// Get / set the tool length compensation
    /// </summary>
    public IDictionary<int, double> ToolLengthCompensation
    {
      get { return m_lengthCompensationData; }
      set
      {
        log.InfoFormat ("ToolDataFactory: received {0} length compensations", value.Count);
        m_lengthCompensationData = value;
        m_toolLifeData = null;
      }
    }

    /// <summary>
    /// Get / set the tool diameter compensation
    /// </summary>
    public IDictionary<int, double> ToolDiameterCompensation
    {
      get { return m_diameterCompensationData; }
      set
      {
        log.InfoFormat ("ToolDataFactory: received {0} diameter compensations", value.Count);
        m_diameterCompensationData = value;
        m_toolLifeData = null;
      }
    }

    /// <summary>
    /// Warning offset
    /// </summary>
    public double WarningOffset {
      get; set;
    }
    
    /// <summary>
    /// Tool gap: indicates how is the series of tool
    /// Gap 1 (by default): T1, T2, T3, ..., T(n)
    /// Gap 2: T1, T3, T5, ..., T(2n+1)
    /// Note: used only if IndexIsToolNumber is false
    /// </summary>
    public int ToolGap {
      get; set;
    }
    
    /// <summary>
    /// If true, the keys contained in CurrentToolLife and ToolLifeLimit
    /// corresponds to the tool number
    /// Otherwise, the keys are only indexes and the tool number is determined
    /// in an incremental way based on the order, taking into account the ToolGap
    /// </summary>
    public bool IndexIsToolNumber { get; set; }

    /// <summary>
    /// Unit for the tool lifes, among:
    /// Unknown, TimeSeconds, Parts, NumberOfTimes, Wear, DistanceMillimeters, DistanceInch, NumberOfCycles
    /// Default: NumberOfTimes
    /// TimeMinutes is possible here, a conversion will be made
    /// </summary>
    public string Unit {
      get { return m_toolUnitString; }
      set
      {
        m_toolUnitString = value;
        ParseUnit (m_toolUnitString);
      }
    }

    /// <summary>
    /// Direction of the tool life, among: Down, Up
    /// Default: Up
    /// </summary>
    public string Direction {
      get { return m_directionString; }
      set
      {
        m_directionString = value;
        ParseDirection (m_directionString);
      }
    }
    #endregion // Getters / Setters

    #region Constructor / Destructor
    /// <summary>
    /// Constructor
    /// </summary>
    public ToolDataFactory() : base("Lemoine.Cnc.InOut.ToolDataFactory")
    {
      // Default values
      ToolGap = 1;
      WarningOffset = 0;
      IndexIsToolNumber = false; // By default, tool number determined by the order of the indexes
      Unit = "NumberOfTimes";
      Direction = "Up";
    }
    
    /// <summary>
    /// <see cref="IDisposable.Dispose" />
    /// </summary>
    public void Dispose ()
    {
      // Do nothing special here
      GC.SuppressFinalize (this);
    }
    #endregion Constructor / Destructor

    #region Methods
    void ComputeToolLifeData()
    {
      if (m_currentData == null) {
        throw new Exception("DataManipulation.ToolDataFactory.ComputeToolLifeData - no current data");
      }

      // Compute the list of keys
      var currentKeys = new List<int> ();
      foreach (var key in m_currentData.Keys) {
        currentKeys.Add (key);
      }

      var limitKeys = new List<int>();
      if (m_limitData != null) {
        foreach (var key in m_limitData.Keys) {
          limitKeys.Add (key);
        }
      }

      var warningKeys = new List<int> ();
      if (m_warningData != null) {
        foreach (var key in m_warningData.Keys) {
          warningKeys.Add (key);
        }
      }

      var lengthCompensationKeys = new List<int> ();
      if (m_lengthCompensationData != null) {
        foreach (var key in m_lengthCompensationData.Keys) {
          lengthCompensationKeys.Add (key);
        }
      }

      var diameterCompensationKeys = new List<int> ();
      if (m_diameterCompensationData != null) {
        foreach (var key in m_diameterCompensationData.Keys) {
          diameterCompensationKeys.Add (key);
        }
      }

      // Create tool life data
      m_toolLifeData = new ToolLifeData ();
      for (int i = 0; i < currentKeys.Count; i++) {

        // Index
        int index = IndexIsToolNumber ? currentKeys[i] : (i * ToolGap + 1);

        // Current value
        double currentValue = m_currentData[currentKeys[i]];

        // Warning
        double warningValue = 0;
        if (IndexIsToolNumber) {
          if (m_warningData != null && m_warningData.ContainsKey (index)) {
            warningValue = m_warningData[index];
          }
        } else {
          if (m_warningData != null && m_warningData.Count > i) {
            warningValue = m_warningData[warningKeys[i]];
          }
        }

        // Limit
        double limitValue = 0;
        if (IndexIsToolNumber) {
          if (m_limitData != null && m_limitData.ContainsKey (index)) {
            limitValue = m_limitData[index];
          }
        } else {
          if (m_limitData != null && m_limitData.Count > i) {
            limitValue = m_limitData[limitKeys[i]];
          }
        }

        // Length compensation
        double? lengthComp = null;
        if (IndexIsToolNumber) {
          if (m_lengthCompensationData != null && m_lengthCompensationData.ContainsKey (index)) {
            lengthComp = m_lengthCompensationData[index];
          }
        } else {
          if (m_lengthCompensationData != null && m_lengthCompensationData.Count > i) {
            lengthComp = m_lengthCompensationData[lengthCompensationKeys[i]];
          }
        }

        // Diameter compensation
        double? diameterComp = null;
        if (IndexIsToolNumber) {
          if (m_diameterCompensationData != null && m_diameterCompensationData.ContainsKey (index)) {
            diameterComp = m_diameterCompensationData[index];
          }
        } else {
          if (m_diameterCompensationData != null && m_diameterCompensationData.Count > i) {
            diameterComp = m_diameterCompensationData[diameterCompensationKeys[i]];
          }
        }

        // Add a new tool
        AddTool (index, currentValue, warningValue, limitValue, lengthComp, diameterComp);
      }
      
      log.InfoFormat("ToolDataFactory - Computed {0} tool life data", m_toolLifeData.ToolNumber);
    }

    void AddTool (int toolNumber, double current, double warning, double limit, double? lengthCompensation, double? diameterCompensation)
    {
      // Conversion of values
      current *= m_multiplier;
      warning *= m_multiplier;
      limit *= m_multiplier;

      // New tool with a life description
      int index = m_toolLifeData.AddTool ();
      m_toolLifeData[index].ToolNumber = toolNumber.ToString ();
      m_toolLifeData[index].ToolId = toolNumber.ToString ();
      m_toolLifeData[index].AddLifeDescription ();
      m_toolLifeData[index][0].LifeDirection = m_direction;
      m_toolLifeData[index][0].LifeType = m_toolUnit;
      m_toolLifeData[index].ToolState = Lemoine.Core.SharedData.ToolState.Available;

      // Values and limits
      m_toolLifeData[index][0].LifeValue = current;
      if (limit > 0) {
        m_toolLifeData[index][0].LifeLimit = limit;

        if (m_toolLifeData[index][0].LifeDirection == Lemoine.Core.SharedData.ToolLifeDirection.Up) {
          // Current tool life increase
          if (warning > 0 && warning < limit) {
            m_toolLifeData[index][0].LifeWarningOffset = limit - warning; // Conversion to an offset
          }
          else if (WarningOffset > 0 && WarningOffset < limit) {
            m_toolLifeData[index][0].LifeWarningOffset = WarningOffset;
          }

          if (current >= limit) {
            m_toolLifeData[index].ToolState = Lemoine.Core.SharedData.ToolState.Expired;
          }
        } else {
          // Current tool life decrease
          if (warning > 0 && warning < limit) {
            m_toolLifeData[index][0].LifeWarningOffset = warning;
          }
          else if (WarningOffset > 0 && WarningOffset < limit) {
            m_toolLifeData[index][0].LifeWarningOffset = WarningOffset;
          }

          if (current <= 0) {
            m_toolLifeData[index].ToolState = Lemoine.Core.SharedData.ToolState.Expired;
          }
        }
      } else {
        m_toolLifeData[index][0].LifeLimit = null;
      }

      // Expired tool?
      if (m_toolLifeData[index][0].LifeLimit.HasValue &&
          m_toolLifeData[index][0].LifeLimit.Value <= m_toolLifeData[index][0].LifeValue) {
        m_toolLifeData[index].ToolState = Lemoine.Core.SharedData.ToolState.Expired;
      }

      // Compensations?
      if (lengthCompensation != null) {
        m_toolLifeData[index].SetProperty ("LengthCompensation", lengthCompensation.Value);
      }

      if (diameterCompensation != null) {
        m_toolLifeData[index].SetProperty ("CutterCompensation", diameterCompensation.Value);
      }
    }

    void ParseDirection (string direction)
    {
      // Default: Up
      m_direction = Lemoine.Core.SharedData.ToolLifeDirection.Up;

      // If direction is set
      if (!string.IsNullOrEmpty (direction)) {
        try {
          m_direction = (Lemoine.Core.SharedData.ToolLifeDirection) Enum.Parse (typeof (Lemoine.Core.SharedData.ToolLifeDirection), direction, true);
        } catch (Exception e) {
          log.WarnFormat ("ToolDataFactory - couldn't parse direction {0}: {1}", direction, e);
        }
      }
    }

    void ParseUnit (string unit)
    {
      // Default value
      m_multiplier = 1.0;
      m_toolUnit = Core.SharedData.ToolUnit.NumberOfTimes;

      if (Unit.Equals ("TimeMinutes", StringComparison.CurrentCultureIgnoreCase)) {
        // Special case: TimeMinutes requires a conversion to TimeSeconds
        m_multiplier = 60.0;
        m_toolUnit = Core.SharedData.ToolUnit.TimeSeconds;
      } else if (!string.IsNullOrEmpty (unit)) {
        try {
          m_toolUnit = (Lemoine.Core.SharedData.ToolUnit) Enum.Parse (typeof (Lemoine.Core.SharedData.ToolUnit), unit, true);
        } catch (Exception e) {
          log.WarnFormat ("ToolDataFactory - couldn't parse unit {0}: {1}", unit, e);
        }
      }
    }
    #endregion // Methods
  }
}
