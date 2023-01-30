// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;

using Lemoine.Core.Log;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Fill a position from:
  /// <item>the axis name</item>
  /// <item>the axis value</item>
  /// </summary>
  public sealed class PositionFromAxisValues : Lemoine.Cnc.BaseCncModule, Lemoine.Cnc.ICncModule, IDisposable
  {
    #region Members
    Position m_position;
    bool m_axisPositionSet = false;
    string m_axisName = "";
    #endregion

    #region Getters / Setters
    /// <summary>
    /// Axis name
    /// </summary>
    public string AxisName {
      get { return m_axisName; }
      set { m_axisName = value; }
    }
    
    /// <summary>
    /// Axis position
    /// </summary>
    public double AxisPosition {
      get
      {
        if (m_axisName.Equals ("X", StringComparison.InvariantCultureIgnoreCase)) {
          return m_position.X;
        }
        else if (m_axisName.Equals ("Y", StringComparison.InvariantCultureIgnoreCase)) {
          return m_position.Y;
        }
        else if (m_axisName.Equals ("Z", StringComparison.InvariantCultureIgnoreCase)) {
          return m_position.Z;
        }
        else if (m_axisName.Equals ("U", StringComparison.InvariantCultureIgnoreCase)) {
          return m_position.U;
        }
        else if (m_axisName.Equals ("V", StringComparison.InvariantCultureIgnoreCase)) {
          return m_position.V;
        }
        else if (m_axisName.Equals ("W", StringComparison.InvariantCultureIgnoreCase)) {
          return m_position.W;
        }
        else if (m_axisName.Equals ("A", StringComparison.InvariantCultureIgnoreCase)) {
          return m_position.A;
        }
        else if (m_axisName.Equals ("B", StringComparison.InvariantCultureIgnoreCase)) {
          return m_position.B;
        }
        else if (m_axisName.Equals ("C", StringComparison.InvariantCultureIgnoreCase)) {
          return m_position.C;
        }
        else {
          log.ErrorFormat ("AxisPosition.get: " +
                           "invalid axis name {0}",
                           m_axisName);
          throw new Exception ("Invalid axis name");
        }
      }
      set
      {
        m_axisPositionSet = false;
        if (m_axisName.Equals ("X", StringComparison.InvariantCultureIgnoreCase)) {
          log.DebugFormat ("AxisPosition.set: " +
                           "set X position {0}",
                           value);
          m_position.X = value;
        }
        else if (m_axisName.Equals ("Y", StringComparison.InvariantCultureIgnoreCase)) {
          log.DebugFormat ("AxisPosition.set: " +
                           "set Y position {0}",
                           value);
          m_position.Y = value;
        }
        else if (m_axisName.Equals ("Z", StringComparison.InvariantCultureIgnoreCase)) {
          log.DebugFormat ("AxisPosition.set: " +
                           "set Z position {0}",
                           value);
          m_position.Z = value;
        }
        else if (m_axisName.Equals ("U", StringComparison.InvariantCultureIgnoreCase)) {
          log.DebugFormat ("AxisPosition.set: " +
                           "set U position {0}",
                           value);
          m_position.U = value;
        }
        else if (m_axisName.Equals ("V", StringComparison.InvariantCultureIgnoreCase)) {
          log.DebugFormat ("AxisPosition.set: " +
                           "set V position {0}",
                           value);
          m_position.V = value;
        }
        else if (m_axisName.Equals ("W", StringComparison.InvariantCultureIgnoreCase)) {
          log.DebugFormat ("AxisPosition.set: " +
                           "set W position {0}",
                           value);
          m_position.W = value;
        }
        else if (m_axisName.Equals ("A", StringComparison.InvariantCultureIgnoreCase)) {
          log.DebugFormat ("AxisPosition.set: " +
                           "set A position {0}",
                           value);
          m_position.A = value;
        }
        else if (m_axisName.Equals ("B", StringComparison.InvariantCultureIgnoreCase)) {
          log.DebugFormat ("AxisPosition.set: " +
                           "set B position {0}",
                           value);
          m_position.B = value;
        }
        else if (m_axisName.Equals ("C", StringComparison.InvariantCultureIgnoreCase)) {
          log.DebugFormat ("AxisPosition.set: " +
                           "set C position {0}",
                           value);
          m_position.C = value;
        }
        else {
          log.ErrorFormat ("AxisPosition.set: " +
                           "invalid axis name {0}",
                           m_axisName);
          return; // leave m_axisPositionSet set to false
        }
        m_axisPositionSet = true;
      }
    }

    /// <summary>
    /// Position
    /// </summary>
    public Position Position {
      get {
        if (!m_axisPositionSet) {
          log.ErrorFormat ("Position.set: " +
                           "no axis position was set");
          throw new Exception ("No axis position set");
        }
        return m_position;
      }
      set
      {
        m_position = value;
        m_axisPositionSet = true;
      }
    }
    
    /// <summary>
    /// Distance from the origin (considering only X, Y and Z)
    /// </summary>
    public double Distance {
      get {
        return Convert.ToDouble (this.Position);
      }
    }
    
    /// <summary>
    /// Get the projection of a position to the given axis
    /// </summary>
    /// <param name="parameters">List of axis. The first character is the separator</param>
    /// <returns></returns>
    public Position GetProjection (string parameters)
    {
      Position projection = new Position ();
      projection.Time = m_position.Time;
      string[] axisNames = parameters.Split (parameters [0]);
      for (int i = 1; i < axisNames.Length; ++i) {
        string axisName = axisNames [i];
        if (axisName.Equals ("X", StringComparison.InvariantCultureIgnoreCase)) {
          projection.X = m_position.X;
        }
        else if (axisName.Equals ("Y", StringComparison.InvariantCultureIgnoreCase)) {
          projection.Y = m_position.Y;
        }
        else if (axisName.Equals ("Z", StringComparison.InvariantCultureIgnoreCase)) {
          projection.Z = m_position.Z;
        }
        else if (axisName.Equals ("U", StringComparison.InvariantCultureIgnoreCase)) {
          projection.U = m_position.U;
        }
        else if (axisName.Equals ("V", StringComparison.InvariantCultureIgnoreCase)) {
          projection.V = m_position.V;
        }
        else if (axisName.Equals ("W", StringComparison.InvariantCultureIgnoreCase)) {
          projection.W = m_position.W;
        }
        else if (axisName.Equals ("A", StringComparison.InvariantCultureIgnoreCase)) {
          projection.A = m_position.A;
        }
        else if (axisName.Equals ("B", StringComparison.InvariantCultureIgnoreCase)) {
          projection.B = m_position.B;
        }
        else if (axisName.Equals ("C", StringComparison.InvariantCultureIgnoreCase)) {
          projection.C = m_position.C;
        }
      }
      return projection;
    }
    #endregion

    #region Constructors / Destructor / ToString methods
    /// <summary>
    /// Description of the constructor
    /// </summary>
    public PositionFromAxisValues ()
      : base("Lemoine.Cnc.InOut.PositionFromAxisValues")
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

    /// <summary>
    /// Start method
    /// </summary>
    /// <returns></returns>
    public bool Start ()
    {
      m_position.Time = DateTime.UtcNow;
      return true;
    }
  }
}
