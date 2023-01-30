// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections.Generic;

namespace Lemoine.Cnc
{
  /// <summary>
  /// The module create tool life data based on a series of variables
  ///  * some of the variables relates to Tool1 (current tool being used on the machine)
  ///  * other variables relates to Tool2, the previous tool being used
  ///  * KeepRemovedTools must be set to "True" in the xml file otherwise all old tools will be removed
  /// </summary>
  public sealed class ToolDataFactoryDual : Lemoine.Cnc.BaseCncModule, Lemoine.Cnc.ICncModule, IDisposable
  {
    #region Members
    ToolLifeData m_toolLifeData = null;
    IDictionary<int, double> m_storedLimits = new Dictionary<int, double>();
    IDictionary<int, double> m_storedWarnings = new Dictionary<int, double>();
    #endregion // Members
    
    #region Getters / Setters
    /// <summary>
    /// Number of first tool
    /// </summary>
    public int T1 { get; set; }
    
    /// <summary>
    /// Number of second tool
    /// </summary>
    public int T2 { get; set; }
    
    /// <summary>
    /// Current value for first tool (seconds)
    /// </summary>
    public double Current1 { get; set; }
    
    /// <summary>
    /// Current value for second tool (seconds)
    /// </summary>
    public double Current2 { get; set; }
    
    /// <summary>
    /// Warning for first tool (seconds)
    /// </summary>
    public double Warning1 {
      get {
        return m_warning1Defined ? m_warning1 :
          (m_storedWarnings.ContainsKey(T1) ? m_storedWarnings[T1] : 0);
      }
      set {
        log.InfoFormat("ToolDataFactoryDual.Warning1: set {0}", value);
        m_warning1 = value;
        m_warning1Defined = true;
      }
    }
    bool m_warning1Defined = false;
    double m_warning1 = 0;
    
    /// <summary>
    /// Warning for second tool (seconds)
    /// </summary>
    public double Warning2 {
      get {
        return m_warning2Defined ? m_warning2 :
          (m_storedWarnings.ContainsKey(T2) ? m_storedWarnings[T2] : 0);
      }
      set {
        log.InfoFormat("ToolDataFactoryDual.Warning2: set {0}", value);
        m_warning2 = value;
        m_warning2Defined = true;
      }
    }
    bool m_warning2Defined = false;
    double m_warning2 = 0;
    
    /// <summary>
    /// Limit for first tool (seconds)
    /// </summary>
    public double Limit1 {
      get {
        return m_limit1Defined ? m_limit1 :
          (m_storedLimits.ContainsKey(T1) ? m_storedLimits[T1] : 0);
      }
      set {
        log.InfoFormat("ToolDataFactoryDual.Limit1: set {0}", value);
        m_limit1 = value;
        m_limit1Defined = true;
      }
    }
    bool m_limit1Defined = false;
    double m_limit1 = 0;
    
    /// <summary>
    /// Limit for second tool (seconds)
    /// </summary>
    public double Limit2 {
      get {
        return m_limit2Defined ? m_limit2 :
          (m_storedLimits.ContainsKey(T2) ? m_storedLimits[T2] : 0);
      }
      set {
        log.InfoFormat("ToolDataFactoryDual.Limit2: set {0}", value);
        m_limit2 = value;
        m_limit2Defined = true;
      }
    }
    bool m_limit2Defined = false;
    double m_limit2 = 0;
    
    /// <summary>
    /// Unit for first tool, among:
    /// Unknown, TimeSeconds, Parts, NumberOfTimes, Wear, DistanceMillimeters, DistanceInch
    /// Default: TimeSeconds
    /// TimeMinutes is possible here, a conversion will be made
    /// </summary>
    public string Unit1 { get; set; }
    
    /// <summary>
    /// Unit for second tool, among:
    /// Unknown, TimeSeconds, Parts, NumberOfTimes, Wear, DistanceMillimeters, DistanceInch
    /// Default: TimeSeconds
    /// TimeMinutes is possible here, a conversion will be made
    /// </summary>
    public string Unit2 { get; set; }
    
    /// <summary>
    /// Direction of first tool, among: Down, Up
    /// Default: Up
    /// </summary>
    public string Direction1 { get; set; }
    
    /// <summary>
    /// Direction of second tool, among: Down, Up
    /// Default: Up
    /// </summary>
    public string Direction2 { get; set; }
    
    /// <summary>
    /// True if the values can be read
    /// Can be false in the case where the values were in an indetermined state
    /// By default this is true
    /// </summary>
    public bool CanRead { get; set; }

    /// <summary>
    /// Token used to assure the integrity of the data we are going to read
    /// An odd token means that the data is being updated => no further reading
    /// An even number means a version is available
    /// Default is 0
    /// </summary>
    public int IntegrityTokenBefore { get; set; }

    /// <summary>
    /// Token used to assure the integrity of the data we have just read
    /// A number different from IntegrityTokenBefore means that an update began
    /// or that a new version is available => the tool life data is canceled
    /// Default is 0
    /// </summary>
    public int IntegrityTokenAfter { get; set; }

    /// <summary>
    /// Default warning used if no warning is read
    /// </summary>
    public double DefaultWarningOffset { get; set; }
    
    /// <summary>
    /// Get the computed tool life data
    /// </summary>
    public ToolLifeData ToolLifeData {
      get {
        if (CanRead && (IntegrityTokenBefore % 2 == 0) && IntegrityTokenBefore == IntegrityTokenAfter) {
          ComputeToolLifeData ();
        }
        else {
          log.Info("ToolDataFactoryDual: data integrity is not assured -> the previous tool life data is kept");
        }

        return m_toolLifeData;
      }
    }
    #endregion // Getters / Setters
    
    #region Constructor / Destructor
    /// <summary>
    /// Constructor
    /// </summary>
    public ToolDataFactoryDual() : base("Lemoine.Cnc.InOut.ToolDataFactoryDual")
    {
      // Initialization of properties
      T1 = 0;
      T2 = 0;
      Unit1 = "TimeSeconds";
      Unit2 = "TimeSeconds";
      Direction1 = "Up";
      Direction2 = "Up";
      CanRead = true;
      IntegrityTokenBefore = 0;
      IntegrityTokenAfter = 0;
      DefaultWarningOffset = 0;
    }
    
    /// <summary>
    /// <see cref="IDisposable.Dispose" />
    /// </summary>
    public void Dispose ()
    {
      // Do nothing special here
      GC.SuppressFinalize(this);
    }
    #endregion Constructor / Destructor

    #region Methods
    /// <summary>
    /// Start method
    /// </summary>
    public void Start()
    {
      // Reset the warnings and limits
      m_warning1Defined = false;
      m_warning2Defined = false;
      m_limit1Defined = false;
      m_limit2Defined = false;
      T1 = 0;
      T2 = 0;
    }
    
    void ComputeToolLifeData()
    {
      // Compute a new ToolLifeData
      m_toolLifeData = new ToolLifeData();
      
      // T1?
      if (T1 != 0) {
        // Store warnings and limits if specified
        if (m_warning1Defined) {
          m_storedWarnings[T1] = m_warning1;
        }

        if (m_limit1Defined) {
          m_storedLimits[T1] = m_limit1;
        }

        AddTool (T1, Current1, Warning1, Limit1, Unit1, Direction1);
      }

      // T2?
      if (T2 != 0) {
        // Store warnings and limits if specified
        if (m_warning2Defined) {
          m_storedWarnings[T2] = m_warning2;
        }

        if (m_limit2Defined) {
          m_storedLimits[T2] = m_limit2;
        }

        AddTool (T2, Current2, Warning2, Limit2, Unit2, Direction2);
      }
      
      log.InfoFormat("ToolDataFactory - Computed {0} tool life data", m_toolLifeData.ToolNumber);
    }
    
    void AddTool(int number, double current, double warning, double limit, string unit, string direction)
    {
      log.InfoFormat("ToolDataFactoryDual.AddTool: T{0}, current={1}, warning={2}, limit={3}, unit={4}, direction={5}",
                     number, current, warning, limit, unit, direction);

      // Conversion of values if "TimeMinutes"
      if (unit.Equals("TimeMinutes", StringComparison.CurrentCultureIgnoreCase)) {
        current *= 60;
        warning *= 60;
        limit *= 60;
      }

      // New tool with a life description
      int index = m_toolLifeData.AddTool ();
      m_toolLifeData[index].ToolNumber = number.ToString ();
      m_toolLifeData[index].ToolId = number.ToString();
      m_toolLifeData[index].AddLifeDescription();
      m_toolLifeData[index][0].LifeDirection = ParseDirection(direction);
      m_toolLifeData[index][0].LifeType = ParseUnit(unit);
      m_toolLifeData[index].ToolState = Lemoine.Core.SharedData.ToolState.Available;
      
      // Values and limits
      m_toolLifeData[index][0].LifeValue = current;
      if (limit > 0) {
        m_toolLifeData[index][0].LifeLimit = limit;
        
        if (m_toolLifeData[index][0].LifeDirection == Lemoine.Core.SharedData.ToolLifeDirection.Up) {
          // Current tool life is increasing
          if (warning > 0 && warning < limit) {
            m_toolLifeData[index][0].LifeWarningOffset = limit - warning; // Conversion to an offset
          }
          else if (DefaultWarningOffset > 0 && DefaultWarningOffset < limit) {
            m_toolLifeData[index][0].LifeWarningOffset = DefaultWarningOffset;
          }

          if (current >= limit) {
            m_toolLifeData[index].ToolState = Lemoine.Core.SharedData.ToolState.Expired;
          }
        } else {
          // Current tool life is decreasing
          if (warning > 0 && warning < limit) {
            m_toolLifeData[index][0].LifeWarningOffset = warning;
          }
          else if (DefaultWarningOffset > 0 && DefaultWarningOffset < limit) {
            m_toolLifeData[index][0].LifeWarningOffset = DefaultWarningOffset;
          }

          if (current <= 0) {
            m_toolLifeData[index].ToolState = Lemoine.Core.SharedData.ToolState.Expired;
          }
        }
      }
    }
    
    Lemoine.Core.SharedData.ToolLifeDirection ParseDirection(string direction)
    {
      // Default: Up
      var ret = Lemoine.Core.SharedData.ToolLifeDirection.Up;
      
      // If direction is set
      if (!string.IsNullOrEmpty(direction)) {
        try {
          ret = (Lemoine.Core.SharedData.ToolLifeDirection)
            Enum.Parse(typeof(Lemoine.Core.SharedData.ToolLifeDirection), direction, true);
        } catch (Exception e) {
          log.WarnFormat("ToolDataFactoryDual - couldn't parse direction {0}: {1}", direction, e.Message);
        }
      }
      return ret;
    }
    
    Lemoine.Core.SharedData.ToolUnit ParseUnit(string unit)
    {
      // Default: TimeSeconds
      var ret = Lemoine.Core.SharedData.ToolUnit.TimeSeconds;
      
      // If unit is set and different from "TimeMinutes"
      if (!string.IsNullOrEmpty(unit) &&
          !unit.Equals("TimeMinutes", StringComparison.CurrentCultureIgnoreCase)) {
        try {
          ret = (Lemoine.Core.SharedData.ToolUnit)
            Enum.Parse(typeof(Lemoine.Core.SharedData.ToolUnit), unit, true);
        } catch (Exception e) {
          log.WarnFormat("ToolDataFactoryDual - couldn't parse unit {0}: {1}", unit, e.Message);
        }
      }
      return ret;
    }
    #endregion // Methods
  }
}
