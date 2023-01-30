// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections.Generic;
using System.Diagnostics;

using Lemoine.Core.Log;

namespace Lemoine.Cnc
{
  /// <summary>
  /// The feedrate could not be computed because the duration between the two positions
  /// exceeds the configured maximum duration
  /// </summary>
  public class ExceededComputationTime: Exception
  {
  }
  
  /// <summary>
  /// Compute the feedrate from the position.
  /// 
  /// This includes a jitter filter (to remove the very small movements)
  /// </summary>
  public sealed class FeedrateFromPosition : Lemoine.Cnc.BaseCncModule, Lemoine.Cnc.ICncModule, IDisposable
  {
    /// <summary>
    /// Default coordinates value
    /// </summary>
    static readonly string DEFAULT_COORDINATES = "xyz";
    /// <summary>
    /// Default maximum time to compute a feedrate or determine a motion
    /// </summary>
    static readonly int DEFAULT_MAXIMUM_TIME_SECONDS = 4; // s
    /// <summary>
    /// Minimum computed feedrate from position in mm/min. Default is -1 mm/min (inactive)
    /// 
    /// Note: a gundrill machine drills with a feed that is approximately 0.3 IPM = 7.6 mm/min
    /// </summary>
    static readonly double DEFAULT_MIN_FEEDRATE_FROM_POSITION = -1; // mm/min
    /// <summary>
    /// Default jitter threshold in mm. Default is 0.1 mm
    /// </summary>
    static readonly double DEFAULT_JITTER_THRESHOLD = 0.1;
    /// <summary>
    /// Default number of positions for the jitter filter is 6
    /// </summary>
    static readonly int DEFAULT_JITTER_NUMBER_POSITIONS = 6;
    /// <summary>
    /// Default number of positions to compute a feedrate is 4
    /// </summary>
    static readonly int DEFAULT_COMPUTATION_NUMBER_POSITIONS = 4;
    /// <summary>
    /// Default attenuation factor is 1 (no attenuation)
    /// </summary>
    static readonly double DEFAULT_COMPUTATION_ATTENUATION_FACTOR = 1;
    /// <summary>
    /// Default minimum distance to trigger a motion in mm
    /// </summary>
    static readonly double DEFAULT_MIN_DISTANCE_MOTION_THRESHOLD = 0.2; // mm
    /// <summary>
    /// Default minimum angle movement from which a machine
    /// can be considered running is 0.1
    /// </summary>
    static readonly double DEFAULT_AXIS_ROTATION_THRESHOLD = 0.1;
    /// <summary>
    /// Default full rotation angle value is 360 for angles in degrees
    /// </summary>
    static readonly double DEFAULT_AXIS_ROTATION_TOUR = 360;
    
    #region Members
    string m_coordinates = DEFAULT_COORDINATES;
    bool m_inches = false;
    int m_maximumTime = DEFAULT_MAXIMUM_TIME_SECONDS; // Maximum time in s between two position to compute a feedrate
    double m_minFeedrateFromPosition = DEFAULT_MIN_FEEDRATE_FROM_POSITION; // mm/min
    double m_jitterThreshold = DEFAULT_JITTER_THRESHOLD; // mm
    int m_jitterNumberPositions = DEFAULT_JITTER_NUMBER_POSITIONS; // number of positions to take into account
    int m_computationNumberPositions = DEFAULT_COMPUTATION_NUMBER_POSITIONS; // number of positions to take into account
    double m_computationAttenuationFactor = DEFAULT_COMPUTATION_ATTENUATION_FACTOR;
    double m_minDistanceMotionThreshold = DEFAULT_MIN_DISTANCE_MOTION_THRESHOLD; // Minimum distance to trigger a motion
    double m_axisRotationThreshold = DEFAULT_AXIS_ROTATION_THRESHOLD; // Minimum angle movement from which a machine is running
    double m_axisRotationTour = DEFAULT_AXIS_ROTATION_TOUR; // Default full rotation angle value
    
    bool m_feedrateFollowingNewPosition = false;
    List<Position> m_positions = new List<Position> ();
    List<double> m_feedrates = new List<double> ();
    #endregion

    #region Getters / Setters
    /// <summary>
    /// Coordinates to consider. One of the following:
    /// <item>xyz / x,y,z (default, when absolute coordinates are used)</item>
    /// <item>xyzw / x,y,zw (when relative coordinates are used, for example in drilling, z and w are parallel)</item>
    /// <item>xyzuvw / xu,yv,zw</item>
    /// <item>xyzu / x,y,zu (when relative coordinates are used, for example in drilling, z and u are parallel)</item>
    /// <item>xyzv / x,y,zv (when relative coordinates are used, for example in drilling, z and v are parallel)</item>
    /// <item>x,y,zuw (when relative coordinates are used, for example in drilling, z, u and w are parallel)</item>
    /// <item>xyzabcuvw is not implemented yet</item>
    /// 
    /// not null or empty
    /// </summary>
    public string Coordinates {
      get { return m_coordinates; }
      set
      {
        if (string.IsNullOrEmpty (value)) {
          m_coordinates = DEFAULT_COORDINATES;
        }
        else {
          m_coordinates = value;
        }
      }
    }
    
    /// <summary>
    /// Inches unit, else mm
    /// </summary>
    public bool Inches {
      get { return m_inches; }
      set { m_inches = value; }
    }
    
    /// <summary>
    /// Maximum time in seconds allowed between two positions
    /// to be able to compute a feedrate or determine a motion
    /// </summary>
    public int MaximumTime {
      get { return m_maximumTime; }
      set { m_maximumTime = value; }
    }
    
    /// <summary>
    /// Minimum position amplitude to get a feedrate in the jitter filter
    /// </summary>
    public double JitterThreshold {
      get { return m_jitterThreshold; }
      set { m_jitterThreshold = value; }
    }
    
    /// <summary>
    /// Number of positions to take into account in the jitter filter
    /// </summary>
    public int JitterNumberPositions {
      get { return m_jitterNumberPositions; }
      set { m_jitterNumberPositions = value; }
    }
    
    /// <summary>
    /// Number of positions to take into account to compute the feedrate
    /// </summary>
    public int ComputationNumberPositions {
      get { return m_computationNumberPositions; }
      set { m_computationNumberPositions = value; }
    }
    
    /// <summary>
    /// Attenuation factor of the old positions to compute the feedrate
    /// 
    /// 1 for no attenuation
    /// </summary>
    public double ComputationAttenuationFactor {
      get { return m_computationAttenuationFactor; }
      set { m_computationAttenuationFactor = value; }
    }
    
    /// <summary>
    /// Minimum distance to trigger a motion in mm
    /// </summary>
    public double MinDistanceMotionThreshold {
      get { return m_minDistanceMotionThreshold; }
      set { m_minDistanceMotionThreshold = value; }
    }
    
    /// <summary>
    /// Minimum angle axis movement from which a machine
    /// is considered running
    /// </summary>
    public double AxisRotationThreshold {
      get { return m_axisRotationThreshold; }
      set { m_axisRotationThreshold = value; }
    }
    
    /// <summary>
    /// Full rotation angle value
    /// 
    /// 360 if the angles are in degrees
    /// </summary>
    public double AxisRotationTour {
      get { return m_axisRotationTour; }
      set { m_axisRotationTour = value; }
    }
    
    /// <summary>
    /// Set a new position
    /// </summary>
    public Position Position
    {
      set
      {
        m_positions.Insert (0, value); // Insert at the beggining of the list
        while ( (m_positions.Count > m_jitterNumberPositions)
               && (m_positions.Count > m_computationNumberPositions)) {
          m_positions.RemoveAt (m_positions.Count - 1);
        }
        // Compute the feedrates
        if (m_positions.Count >= 2) {
          double feedrate;
          try {
            feedrate = ComputeFeedrate (m_positions [0], m_positions [1]);
          }
          catch (ExceededComputationTime) {
            log.InfoFormat ("Position.set: " +
                            "computation duration time reached the maximum time");
            m_feedrates.Clear ();
            m_positions.RemoveRange (1, m_positions.Count-1);
            return;
          }
          Debug.Assert (0.0 <= feedrate);
          if (feedrate < 0.0) {
            log.ErrorFormat ("Position.set: " +
                             "computed feedrate {0} is negative " +
                             "=> turn it to a positive value",
                             feedrate);
            feedrate = Math.Abs (feedrate);
          }
          log.DebugFormat ("Position.set: " +
                           "new position, new feedrate {0} " +
                           "from positions {1} and {2}",
                           feedrate,
                           m_positions [0], m_positions [1]);
          m_feedrates.Insert (0, feedrate);
          m_feedrateFollowingNewPosition = true;
        }
        while (m_feedrates.Count > m_computationNumberPositions - 1) {
          m_feedrates.RemoveAt (m_feedrates.Count - 1);
        }
      }
    }
    
    /// <summary>
    /// Feedrate from the position
    /// </summary>
    public double Feedrate
    {
      get
      {
        if (false == m_feedrateFollowingNewPosition) {
          log.ErrorFormat ("Feedrate.get: " +
                           "no new position " +
                           "=> do not compute feed");
          throw new Exception ("No new position");
        }
        if (m_positions.Count <= 1) {
          log.ErrorFormat ("Feedrate.get: " +
                           "no enough positions ({0}) have been given",
                           m_positions.Count);
          throw new Exception ("Not enough positions to get a feedrate");
        }
        
        // 1. Jitter filter
        double jitterThreshold = m_inches
          ? Conversion.Converter.ConvertToInches (m_jitterThreshold)
          : m_jitterThreshold;
        Position m = m_positions [0]; // Minimum values
        Position M = m_positions [0]; // Maximum values
        for (int i = 1;
             (i < m_jitterNumberPositions) && (i < m_positions.Count);
             ++i) {
          if (m_positions [i].X < m.X) {
            m.X = m_positions [i].X;
          }
          if (m_positions [i].X > M.X) {
            M.X = m_positions [i].X;
          }
          if (m_positions [i].Y < m.Y) {
            m.Y = m_positions [i].Y;
          }
          if (m_positions [i].Y > M.Y) {
            M.Y = m_positions [i].Y;
          }
          if (m_positions [i].Z < m.Z) {
            m.Z = m_positions [i].Z;
          }
          if (m_positions [i].Z > M.Z) {
            M.Z = m_positions [i].Z;
          }
        }
        if ( (Math.Abs (M.X - m.X) < jitterThreshold)
            && (Math.Abs (M.Y - m.Y) < jitterThreshold)
            && (Math.Abs (M.Z - m.Z) < jitterThreshold)) {
          log.InfoFormat ("Feedrate.get: " +
                          "jitter detected => return 0.0 ! " +
                          "interval is {0} {1} " +
                          "and threshold is {2}",
                          m, M,
                          jitterThreshold);
          return 0.0;
        }
        
        // 2. Feedrate computation
        double feedrate = m_feedrates [0];
        log.DebugFormat ("Feedrate.get: " +
                         "first computed feedrate is {0} " +
                         "from positions {1} and {2}",
                         feedrate,
                         m_positions [0], m_positions [1]);
        if (double.IsInfinity (feedrate)) {
          log.ErrorFormat ("Feedrate.get: " +
                           "duration was 0");
          throw new Exception ("Duration is 0, impossible to get the feedrate");
        }
        double totalWeight = 1.0;
        double newWeight = 1.0; // weight of the new value
        for (int i = 1;
             (i < m_computationNumberPositions - 1) && (i < m_feedrates.Count);
             ++i) {
          double f = m_feedrates [i];
          Debug.Assert (0.0 <= f);
          if (double.IsInfinity (f)) {
            log.InfoFormat ("Feedrate.get: " +
                            "infinitive duration, skip it");
          }
          else if (f < 0) {
            log.InfoFormat ("Feedrate.get: " +
                            "got a negative feedrate because it has been discarded, " +
                            "skip it");
          }
          else {
            log.DebugFormat ("Feedrate.get: " +
                             "{0} computed feedrate is {1} " +
                             "from positions {2} and {3}",
                             i, f,
                             m_positions [0], m_positions [1]);
            newWeight /= m_computationAttenuationFactor;
            double futureTotalWeight = totalWeight + newWeight;
            feedrate = (feedrate/futureTotalWeight*totalWeight) + (f/futureTotalWeight*newWeight);
            totalWeight = futureTotalWeight;
            Debug.Assert (0.0 <= feedrate);
            if (feedrate < 0.0) {
              log.ErrorFormat ("Feedrate.get: " +
                               "feedrate {0} is negative => adjust it",
                               feedrate);
              feedrate = Math.Abs (feedrate);
            }
          }
        }
        Debug.Assert (0.0 <= feedrate);
        if (feedrate < 0.0) {
          log.ErrorFormat ("Feedrate.get: " +
                           "feedrate {0} is negative => adjust it",
                           feedrate);
          feedrate = Math.Abs (feedrate);
        }
        double minFeedrate = m_inches
          ? Lemoine.Conversion.Converter.ConvertToInches (m_minFeedrateFromPosition)
          : m_minFeedrateFromPosition;
        if (feedrate <= minFeedrate) {
          log.InfoFormat ("Feedrate.get: " +
                          "feedrate {0} is less than minimum feedrate {1}, " +
                          "discard it",
                          feedrate, minFeedrate);
          throw new Exception ("Computed feedrate too low");
        }
        log.DebugFormat ("Feedrate.get: " +
                         "final average feedrate is {0}",
                         feedrate);
        return feedrate;
      }
    }
    
    /// <summary>
    /// Is the idle axis ?
    /// 
    /// true is returned if axis was considered idle
    /// else an exception is thrown
    /// 
    /// This property is determined from the set positions
    /// </summary>
    public bool IdleAxis {
      get
      {
        // 1. Check there are at least two positions
        if (false == m_feedrateFollowingNewPosition) {
          log.ErrorFormat ("IdleAxis.get: " +
                           "no new position " +
                           "=> do not compute anything");
          throw new Exception ("No new position");
        }
        if (m_positions.Count <= 1) {
          log.ErrorFormat ("IdleAxis.get: " +
                           "no enough positions ({0}) have been given",
                           m_positions.Count);
          throw new Exception ("Not enough positions to get an axis movement");
        }
        Position p0 = m_positions [0];
        Position p1 = m_positions [1];
        
        // 2. Check the latest time difference is valid
        TimeSpan duration;
        if (p0.Time > p1.Time) {
          duration = p0.Time - p1.Time;
        }
        else {
          duration = p1.Time - p0.Time;
        }
        if (duration.TotalSeconds > MaximumTime) {
          log.ErrorFormat ("IdleAxis.get: " +
                           "duration {0} exceeds MaximumTime {1} " +
                           "=> raise an exception",
                           duration, MaximumTime);
          throw new Exception ("Maximum time was reached in GetAxisMovement");
        }
        if (duration.TotalMinutes <= 0.0) {
          log.ErrorFormat ("IdleAxis.get: " +
                           "duration is 0");
          throw new Exception ("No time difference in GetAxisMovement");
        }
        
        // 3. Check if there was a big enough axis rotation
        double threshold = m_inches
          ? Conversion.Converter.ConvertToInches (MinDistanceMotionThreshold)
          : MinDistanceMotionThreshold;
        if ( (threshold < Math.Abs (p0.X - p1.X))
            || (threshold < Math.Abs (p0.Y - p1.Y))
            || (threshold < Math.Abs (p0.Z - p1.Z))
            || (threshold < Math.Abs (p0.U - p1.U))
            || (threshold < Math.Abs (p0.V - p1.V))
            || (threshold < (Math.Abs (p0.W - p1.W)))) {
          log.DebugFormat ("IdleAxis.get: " +
                           "an axis linear movement was detected between {0} and {1}",
                           p1, p0);
          throw new Exception ("Axis linear movement detected");
        }
        if ( ( (Math.Abs (p0.A - p1.A) > AxisRotationThreshold)
              && (Math.Abs(p0.A - p1.A) < (AxisRotationTour - AxisRotationThreshold)))
            || ( (Math.Abs (p0.B - p1.B) > AxisRotationThreshold)
                && (Math.Abs(p0.B - p1.B) < (AxisRotationTour - AxisRotationThreshold)))
            || ( (Math.Abs (p0.C - p1.C) > AxisRotationThreshold)
                && (Math.Abs(p0.C - p1.C) < (AxisRotationTour - AxisRotationThreshold)))) {
          log.DebugFormat ("IdleAxis.get: " +
                           "an axis rotation was detected between {0} and {1}",
                           p1, p0);
          throw new Exception ("Axis rotation detected");
        }

        log.DebugFormat ("IdleAxis.get: " +
                         "no axis movement was detected from {0} to {1}",
                         p1, p0);
        return true;
      }
    }
    #endregion Getters / Setters

    /// <summary>
    /// Were there a big enough axis movement
    /// to consider the machine is in motion ?
    /// </summary>
    /// <param name="unused"></param>
    /// <returns>true if a big enough motion was detected, else an exception is raised</returns>
    public bool GetAxisMovement (String unused)
    {
      // 1. Check there are at least two positions
      if (false == m_feedrateFollowingNewPosition) {
        log.ErrorFormat ("GetAxisMovement: " +
                         "no new position " +
                         "=> do not compute anything");
        throw new Exception ("No new position");
      }
      if (m_positions.Count <= 1) {
        log.ErrorFormat ("GetAxisMovement: " +
                         "no enough positions ({0}) have been given",
                         m_positions.Count);
        throw new Exception ("Not enough positions to get an axis movement");
      }
      Position p0 = m_positions [0];
      Position p1 = m_positions [1];
      
      // 2. Check the latest time difference is valid
      TimeSpan duration;
      if (p0.Time > p1.Time) {
        duration = p0.Time - p1.Time;
      }
      else {
        duration = p1.Time - p0.Time;
      }
      if (duration.TotalSeconds > MaximumTime) {
        log.ErrorFormat ("GetAxisMovement: " +
                         "duration {0} exceeds MaximumTime {1} " +
                         "=> raise an exception",
                         duration, MaximumTime);
        throw new Exception ("Maximum time was reached in GetAxisMovement");
      }
      if (duration.TotalMinutes <= 0.0) {
        log.ErrorFormat ("GetAxisMovement: " +
                         "duration is 0");
        throw new Exception ("No time difference in GetAxisMovement");
      }
      
      // 3. Check if there was a big enough axis rotation
      double threshold = m_inches
        ? Conversion.Converter.ConvertToInches (MinDistanceMotionThreshold)
        : MinDistanceMotionThreshold;
      if ( (Math.Abs (p0.X - p1.X) <= threshold)
          && (Math.Abs (p0.Y - p1.Y) <= threshold)
          && (Math.Abs (p0.Z - p1.Z) <= threshold)
          && (Math.Abs (p0.U - p1.U) <= threshold)
          && (Math.Abs (p0.V - p1.V) <= threshold)
          && (Math.Abs (p0.W - p1.W) <= threshold)) {
        log.DebugFormat ("GetAxisMovement: " +
                         "the axis movement between {0} and {1} was too weak",
                         p1, p0);
        throw new Exception ("Too weak axis movement");
      }

      log.DebugFormat ("GetAxisMovement: " +
                       "an axis motion was detected from {0} to {1}",
                       p1, p0);
      return true;
    }
    
    /// <summary>
    /// Were there a rotation axis movement
    /// that was big enough to consider the machine running ?
    /// </summary>
    /// <param name="unused"></param>
    /// <returns>true if a big enough rotation movement was detected, else an exception is raised</returns>
    public bool GetRotationAxisMovement (String unused)
    {
      // 1. Check there are at least two positions
      if (false == m_feedrateFollowingNewPosition) {
        log.ErrorFormat ("GetRotationAxisMovement: " +
                         "no new position " +
                         "=> do not compute anything");
        throw new Exception ("No new position");
      }
      if (m_positions.Count <= 1) {
        log.ErrorFormat ("GetRotationAxisMovement: " +
                         "no enough positions ({0}) have been given",
                         m_positions.Count);
        throw new Exception ("Not enough positions to get a rotation axis movement");
      }
      Position p0 = m_positions [0];
      Position p1 = m_positions [1];
      
      // 2. Check the latest time difference is valid
      TimeSpan duration;
      if (p0.Time > p1.Time) {
        duration = p0.Time - p1.Time;
      }
      else {
        duration = p1.Time - p0.Time;
      }
      if (duration.TotalSeconds > MaximumTime) {
        log.ErrorFormat ("GetRotationAxisMovement: " +
                         "duration {0} exceeds MaximumTime {1} " +
                         "=> raise an exception",
                         duration, MaximumTime);
        throw new Exception ("Maximum time was reached in GetRotationAxisMovement");
      }
      if (duration.TotalMinutes <= 0.0) {
        log.ErrorFormat ("GetRotationAxisMovement: " +
                         "duration is 0");
        throw new Exception ("No time difference in GetRotationAxisMovement");
      }
      
      // 3. Check if there was a big enough axis rotation
      if ( ( (Math.Abs (p0.A - p1.A) <= AxisRotationThreshold)
            || (Math.Abs(p0.A - p1.A) >= (AxisRotationTour - AxisRotationThreshold)))
          && ( (Math.Abs (p0.B - p1.B) <= AxisRotationThreshold)
              || (Math.Abs(p0.B - p1.B) >= (AxisRotationTour - AxisRotationThreshold)))
          && ( (Math.Abs (p0.C - p1.C) <= AxisRotationThreshold)
              || (Math.Abs(p0.C - p1.C) >= (AxisRotationTour - AxisRotationThreshold)))) {
        log.DebugFormat ("GetRotationAxisMovement: " +
                         "the axis rotation between {0} and {1} was too weak",
                         p1, p0);
        throw new Exception ("Too weak rotation axis movement");
      }

      log.DebugFormat ("GetRotationAxisMovement: " +
                       "an axis rotation was detected from {0} to {1}",
                       p1, p0);
      return true;
    }
    
    #region Constructors / Destructor / ToString methods
    /// <summary>
    /// Default constructor
    /// </summary>
    public FeedrateFromPosition ()
      : base ("Lemoine.Cnc.InOut.FeedrateFromPosition")
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
    /// Start method to reset the fact there is no new position for the moment
    /// </summary>
    public void Start ()
    {
      m_feedrateFollowingNewPosition = false;
    }
    
    /// <summary>
    /// Compute the feedrate from two positions.
    /// </summary>
    /// <param name="p1">position 1</param>
    /// <param name="p2">position 2</param>
    /// <returns>Computed feedrate</returns>
    /// <exception cref="ExceededComputationTime">The difference of time between the two positions
    /// exceeds MaximumTime</exception>
    double ComputeFeedrate (Position p1, Position p2)
    {
      double distance = GetDistance (p1, p2);
      TimeSpan duration;
      if (p1.Time > p2.Time) {
        duration = p1.Time - p2.Time;
      }
      else {
        duration = p2.Time - p1.Time;
      }
      if (duration.TotalSeconds > MaximumTime) {
        log.WarnFormat ("ComputeFeedrate: " +
                        "duration {0} exceeds MaximumTime {1} " +
                        "=> return -1",
                        duration, MaximumTime);
        throw new ExceededComputationTime ();
      }
      if (duration.TotalMinutes <= 0.0) {
        log.ErrorFormat ("ComputeFeedrate: " +
                         "duration is 0, return infinity");
        return double.PositiveInfinity;
      }
      double feedrate = distance / duration.TotalMinutes;
      log.DebugFormat ("ComputeFeedrate: " +
                       "got feedrate {0} " +
                       "from distance {1} and duration {2}",
                       feedrate,
                       distance, duration);
      return feedrate;
    }
    
    /// <summary>
    /// Get the distance between two positions
    /// </summary>
    /// <param name="p1"></param>
    /// <param name="p2"></param>
    /// <returns></returns>
    double GetDistance (Position p1, Position p2)
    {
      Debug.Assert (null != this.Coordinates);
      
      double x1, x2, y1, y2, z1, z2;
      if (this.Coordinates.Equals ("xyz", StringComparison.InvariantCultureIgnoreCase)
        || this.Coordinates.Equals ("x,y,z", StringComparison.InvariantCultureIgnoreCase)) {
        x1 = p1.X;
        x2 = p2.X;
        y1 = p1.Y;
        y2 = p2.Y;
        z1 = p1.Z;
        z2 = p2.Z;
      }
      else if (this.Coordinates.Equals ("xyzw", StringComparison.InvariantCultureIgnoreCase)
        || this.Coordinates.Equals ("x,y,zw", StringComparison.InvariantCultureIgnoreCase)) {
        x1 = p1.X;
        x2 = p2.X;
        y1 = p1.Y;
        y2 = p2.Y;
        z1 = p1.Z + p1.W;
        z2 = p2.Z + p2.W;
      }
      else if (this.Coordinates.Equals ("xyzuvw", StringComparison.InvariantCultureIgnoreCase)
        || this.Coordinates.Equals ("xu,yv,zw", StringComparison.InvariantCultureIgnoreCase)) {
        x1 = p1.X + p1.U;
        x2 = p2.X + p2.U;
        y1 = p1.Y + p1.V;
        y2 = p2.Y + p2.V;
        z1 = p1.Z + p1.W;
        z2 = p2.Z + p2.W;
      }
      else if (this.Coordinates.Equals ("xyzu", StringComparison.InvariantCultureIgnoreCase)
        || this.Coordinates.Equals ("x,y,zu", StringComparison.InvariantCultureIgnoreCase)) {
        x1 = p1.X;
        x2 = p2.X;
        y1 = p1.Y;
        y2 = p2.Y;
        z1 = p1.Z + p1.U;
        z2 = p2.Z + p2.U;
      }
      else if (this.Coordinates.Equals ("xyzv", StringComparison.InvariantCultureIgnoreCase)
        || this.Coordinates.Equals ("x,y,zv", StringComparison.InvariantCultureIgnoreCase)) {
        x1 = p1.X;
        x2 = p2.X;
        y1 = p1.Y;
        y2 = p2.Y;
        z1 = p1.Z + p1.V;
        z2 = p2.Z + p2.V;
      }
      else if (this.Coordinates.Equals ("x,y,zuw)", StringComparison.InvariantCultureIgnoreCase)) {
        x1 = p1.X;
        x2 = p2.X;
        y1 = p1.Y;
        y2 = p2.Y;
        z1 = p1.Z + p1.U + p1.W;
        z2 = p2.Z + p2.U + p2.W;
      }
      else if (this.Coordinates.Equals ("xyzabcuvw", StringComparison.InvariantCultureIgnoreCase)) {
        log.ErrorFormat ("ComputeFeedrate: " +
                         "coordinates {0} is not implemented",
                         this.Coordinates);
        throw new NotImplementedException ("Coordinates xyzabcuvw");
      }
      else {
        log.ErrorFormat ("ComputeFeedrate: " +
                         "coordinates {0} is not implemented",
                         this.Coordinates);
        throw new NotImplementedException ("Coordinates");
      }
      return Math.Sqrt (Math.Pow (x2 - x1, 2) +
                        Math.Pow (y2 - y1, 2) +
                        Math.Pow (z2 - z1, 2));
    }
    #endregion
  }
}
