// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

#if NETSTANDARD || NET48 || NETCOREAPP

using System;

using System.Collections.Generic;
using Lemoine.Core.Log;
using Lemoine.Model;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Return a Machine Mode given the following properties:
  /// <item>Error</item>
  /// <item>Running</item>
  /// <item>Manual</item>
  /// </summary>
  public sealed class MachineMode : Lemoine.Cnc.BaseCncModule, Lemoine.Cnc.ICncModule, IDisposable
  {
    static readonly double DEFAULT_FEEDRATE_OVERRIDE_THRESHOLD = 1.0;
    static readonly double DEFAULT_RAPID_TRAVERSE_OVERRIDE_THRESHOLD = 1.0;
    
    #region Members
    double m_feedrateOverrideThreshold = DEFAULT_FEEDRATE_OVERRIDE_THRESHOLD;
    double m_rapidTraverseOverrideThreshold = DEFAULT_RAPID_TRAVERSE_OVERRIDE_THRESHOLD;

    bool? m_motion = null;
    bool m_emergency = false;
    bool m_inactive = false;
    bool m_active = false;
    bool m_auto = false;
    bool m_unavailable = false;
    bool m_alarmSignal = false;
    bool m_error = false;
    bool? m_running = null;
    bool? m_manual = null;
    bool m_jog = false;
    bool m_handle = false;
    bool m_reference = false;
    bool m_mdi = false;
    bool m_singleBlock = false;
    bool m_dryRun = false;
    bool m_machineLock = false;
    bool m_hold = false;
    bool m_on = false;
    bool m_off = false;
    bool m_programExecution = false;
    bool m_plcBlock = false;
    bool m_noRunningProgram = false;
    bool m_ready = false;
    bool m_stopped = false;
    bool m_finished = false;
    bool m_reset = false;
    bool m_notReady = false;
    bool m_interrupted = false;
    bool m_errorCleared = false;
    bool m_toolChange = false;
    bool m_laserCheck = false;
    bool m_palletChange = false;
    bool m_probingCycle = false;
    bool m_autoHomePositioning = false;
    bool m_mStop = false;
    bool m_m0 = false;
    bool m_m1 = false;
    bool m_m60 = false;
    bool m_mWait = false;
    int? m_mCode = null;
    System.Collections.IList m_activeMCodes = new List<int> ();
    double? m_feedrateOverride = null;
    double? m_rapidTraverseOverride = null;
    bool m_acquisitionError = false;
    bool m_remoteAcquisitionError = false;
    bool m_pingOk = false;
    bool m_addressNotValid = false;
    bool m_idleAxis = false;
    Model.StackLight? m_stackLight;
    bool m_autoUnknownIsActive = false;
    bool m_unknownIsInactive = false;
    bool m_manualUnknownIsInactive = false;
    bool m_probablyOffIfAcquisitionError = false;
    bool m_jogIfActiveOnly = false;
    bool m_redStackLightIsEmergency = false;
    
    int m_machineModeId = 0;
    bool m_activeProgram = false;
    bool m_activeProgrammedFeedrate = false;
    #endregion // Members

    #region Getters / Setters
    /// <summary>
    /// Feedrate override threshold
    /// 
    /// Default is 1.0
    /// </summary>
    public double FeedrateOverrideThreshold {
      get { return m_feedrateOverrideThreshold; }
      set { m_feedrateOverrideThreshold = value; }
    }
    
    /// <summary>
    /// Rapid traverse override treshold
    /// 
    /// Default is 1.0
    /// </summary>
    public double RapidTraverseOverrideTreshold {
      get { return m_rapidTraverseOverrideThreshold; }
      set { m_rapidTraverseOverrideThreshold = value; }
    }
    
    
    /// <summary>
    /// Motion property
    /// 
    /// Default is null
    /// </summary>
    public bool? Motion {
      get { return m_motion; }
      set { m_motion = value; }
    }
    
    /// <summary>
    /// Emergency property
    /// 
    /// Default is false (not defined / unknown)
    /// </summary>
    public bool Emergency {
      get { return m_emergency; }
      set { m_emergency = value; }
    }
    
    /// <summary>
    /// Inactive property
    /// 
    /// Default is false (not defined / unknown)
    /// </summary>
    public bool Inactive {
      get { return m_inactive; }
      set { m_inactive = value; }
    }
    
    /// <summary>
    /// Active property
    /// 
    /// Default is false (not defined / unknown)
    /// </summary>
    public bool Active {
      get { return m_active; }
      set { m_active = value; }
    }
    
    /// <summary>
    /// Auto property
    /// 
    /// Default is false (not defined / unknown)
    /// </summary>
    public bool Auto {
      get { return m_auto; }
      set { m_auto = value; }
    }
    
    /// <summary>
    /// ManualAny property
    /// 
    /// Default is false (not defined / unknown)
    /// </summary>
    public bool ManualAny {
      get { return m_manual.HasValue && m_manual.Value; }
      set
      {
        if (value) {
          m_manual = true;
        }
      }
    }
    
    /// <summary>
    /// Unavailable property (no connection can be made with the control)
    /// 
    /// Default is false (not defined / unknown)
    /// </summary>
    public bool Unavailable {
      get { return m_unavailable; }
      set { m_unavailable = value; }
    }

    /// <summary>
    /// Alarm signal (machine must stop because of an alarm in the CNC)
    /// 
    /// Default is false (not defined / unknown)
    /// </summary>
    public bool AlarmSignal {
      get { return m_alarmSignal; }
      set { m_alarmSignal = value; }
    }
    
    /// <summary>
    /// Error property (error in the CNC)
    /// 
    /// Default is false (not defined / unknown)
    /// </summary>
    public bool Error {
      get { return m_error; }
      set { m_error = value; }
    }
    
    /// <summary>
    /// Running property
    /// 
    /// Default is Exception
    /// </summary>
    public bool Running {
      get { return m_running.Value; }
      set { m_running = value; }
    }
    
    /// <summary>
    /// Auto/Manual property
    /// 
    /// true: Manual
    /// false: Auto
    /// 
    /// Default is null (unknown)
    /// </summary>
    public bool? Manual {
      get { return m_manual; }
      set { m_manual = value; }
    }
    
    /// <summary>
    /// Jog property
    /// 
    /// Default is false (not defined / unknown)
    /// </summary>
    public bool Jog {
      get { return m_jog; }
      set { m_jog = value; }
    }

    /// <summary>
    /// Handle/Handwheel property
    /// 
    /// Default is false (not defined / unknown)
    /// </summary>
    public bool Handle {
      get { return m_handle; }
      set { m_handle = value; }
    }
    
    /// <summary>
    /// Manual return to reference
    /// </summary>
    public bool Reference {
      get { return m_reference; }
      set { m_reference = value; }
    }

    /// <summary>
    /// MDI property
    /// 
    /// Default is false (not defined / unknown)
    /// </summary>
    public bool MDI {
      get { return m_mdi; }
      set { m_mdi = value; }
    }

    /// <summary>
    /// SingleBlock property
    /// 
    /// Default is false (not defined / unknown)
    /// </summary>
    public bool SingleBlock {
      get { return m_singleBlock; }
      set { m_singleBlock = value; }
    }

    /// <summary>
    /// Dry run
    /// 
    /// Default is false (not defined / unknown)
    /// </summary>
    public bool DryRun {
      get { return m_dryRun; }
      set { m_dryRun = value; }
    }
    
    /// <summary>
    /// Machine lock
    /// 
    /// Default is false (not defined / unknown)
    /// </summary>
    public bool MachineLock {
      get { return m_machineLock; }
      set { m_machineLock = value; }
    }
    
    /// <summary>
    /// Feed hold property (in Auto mode only)
    /// 
    /// Default is false (not defined / unknown)
    /// </summary>
    public bool Hold {
      get { return m_hold; }
      set { m_hold = value; }
    }

    
    /// <summary>
    /// On property
    /// 
    /// The machine is On
    /// 
    /// Default is false (unknown)
    /// </summary>
    public bool On {
      get { return m_on; }
      set { m_on = value; }
    }
    
    /// <summary>
    /// Off property
    /// 
    /// Default is false (not defined / unknown)
    /// </summary>
    public bool Off {
      get { return m_off; }
      set { m_off = value; }
    }
    
    /// <summary>
    /// Program execution
    /// 
    /// Default is false (not defined / unknown)
    /// </summary>
    public bool ProgramExecution {
      get { return m_programExecution; }
      set { m_programExecution = value; }
    }
    
    /// <summary>
    /// PLC block execution
    /// 
    /// Default is false (not defined / unknown)
    /// </summary>
    public bool PlcBlock {
      get { return m_plcBlock; }
      set { m_plcBlock = value; }
    }
    
    /// <summary>
    /// Auto mode and no program is current running property
    /// 
    /// Default is false
    /// </summary>
    public bool NoRunningProgram {
      get { return m_noRunningProgram; }
      set { m_noRunningProgram = value; }
    }

    /// <summary>
    /// Ready property
    /// 
    /// The controller is ready to execute. It is currently idle.
    /// 
    /// Default is false
    /// </summary>
    public bool Ready {
      get { return m_ready; }
      set { m_ready = value; }
    }
    
    /// <summary>
    /// Stopped property
    /// 
    /// The program was stopped. The machine is idle
    /// 
    /// Default is false
    /// </summary>
    public bool Stopped {
      get { return m_stopped; }
      set { m_stopped = value; }
    }
    
    /// <summary>
    /// Finished property
    /// 
    /// The program has just been completed
    /// 
    /// Default is false
    /// </summary>
    public bool Finished {
      get { return m_finished; }
      set { m_finished = value; }
    }
    
    /// <summary>
    /// Reset property
    /// 
    /// The program execution was reset or canceled
    /// 
    /// Default is false
    /// </summary>
    public bool Reset {
      get { return m_reset; }
      set { m_reset = value; }
    }
    
    /// <summary>
    /// Not ready property
    /// 
    /// The program execution is not ready
    /// 
    /// Default is false
    /// </summary>
    public bool NotReady {
      get { return m_notReady; }
      set { m_notReady = value; }
    }

    /// <summary>
    /// Interrupted property
    /// 
    /// The operator or the program has paused execution of the controller and the program is waiting to be continued
    /// 
    /// Default is false
    /// </summary>
    public bool Interrupted {
      get { return m_interrupted; }
      set { m_interrupted = value; }
    }
    
    /// <summary>
    /// The error during a program execution was cleared
    /// 
    /// Default is false
    /// </summary>
    public bool ErrorCleared {
      get { return m_errorCleared; }
      set { m_errorCleared = value; }
    }
    
    /// <summary>
    /// Tool change property
    /// 
    /// Default is false
    /// </summary>
    public bool ToolChange {
      get { return m_toolChange; }
      set { m_toolChange = value; }
    }
    
    /// <summary>
    /// Laser check property
    /// 
    /// Default is false
    /// </summary>
    public bool LaserCheck {
      get { return m_laserCheck; }
      set { m_laserCheck = value; }
    }

    /// <summary>
    /// Pallet change property
    /// 
    /// Default is false
    /// </summary>
    public bool PalletChange {
      get { return m_palletChange; }
      set { m_palletChange = value; }
    }
    
    /// <summary>
    /// Probing cycle property
    /// 
    /// Default is false
    /// </summary>
    public bool ProbingCycle {
      get { return m_probingCycle; }
      set { m_probingCycle = value; }
    }
    
    /// <summary>
    /// Home positioning property
    /// 
    /// Default is false
    /// </summary>
    public bool AutoHomePositioning {
      get { return m_autoHomePositioning; }
      set { m_autoHomePositioning = value; }
    }

    /// <summary>
    /// There is a programmed stop
    /// 
    /// Default is false
    /// </summary>
    public bool MStop {
      get { return m_mStop; }
      set { m_mStop = value; }
    }
    
    /// <summary>
    /// M0 (Stop) property
    /// 
    /// Default is false
    /// </summary>
    public bool M0 {
      get { return m_m0; }
      set { m_m0 = value; }
    }
    
    /// <summary>
    /// M1 (Optional stop)  property
    /// 
    /// Default is false
    /// </summary>
    public bool M1 {
      get { return m_m1; }
      set { m_m1 = value; }
    }
    
    /// <summary>
    /// M60 (Pallet shuttle and stop)  property
    /// 
    /// Default is false
    /// </summary>
    public bool M60 {
      get { return m_m60; }
      set { m_m60 = value; }
    }

    /// <summary>
    /// Programmed operator input wait  property
    /// 
    /// Default is false
    /// </summary>
    public bool MWait {
      get { return m_mWait; }
      set { m_mWait = value; }
    }
    
    /// <summary>
    /// M Code property
    /// 
    /// Default is null
    /// </summary>
    public int? MCode {
      get { return m_mCode; }
      set { m_mCode = value; }
    }

    /// <summary>
    /// M Code property, excluding the 0 values
    /// 
    /// Default is null
    /// </summary>
    public int? MCodeExcept0
    {
      get {
        return m_mCode;
      }
      set {
        if (value.HasValue && (0 != value.Value)) {
          m_mCode = value;
        }
      }
    }

    /// <summary>
    /// Active M Codes
    /// 
    /// Default is an empty list
    /// </summary>
    public System.Collections.IList ActiveMCodes {
      get { return m_activeMCodes; }
      set { m_activeMCodes = value; }
    }
    
    /// <summary>
    /// Feedrate override
    /// 
    /// Default is not defined
    /// </summary>
    public double? FeedrateOverride {
      get { return m_feedrateOverride; }
      set { m_feedrateOverride = value; }
    }
    
    /// <summary>
    /// Rapid traverse override
    /// 
    /// Default is not defined
    /// </summary>
    public double? RapidTraverseOverride {
      get { return m_rapidTraverseOverride; }
      set { m_rapidTraverseOverride = value; }
    }
    
    /// <summary>
    /// Acquisition error
    /// 
    /// Default is false
    /// </summary>
    public bool AcquisitionError {
      get { return m_acquisitionError; }
      set { m_acquisitionError = value; }
    }
    
    /// <summary>
    /// Remote acquisition error
    /// 
    /// Default is false
    /// </summary>
    public bool RemoteAcquisitionError {
      get { return m_remoteAcquisitionError; }
      set { m_remoteAcquisitionError = value; }
    }
    
    /// <summary>
    /// Ping ok
    /// 
    /// Default is false (ping not ok or unknown)
    /// </summary>
    public bool PingOk {
      get { return m_pingOk; }
      set { m_pingOk = value; }
    }
    
    /// <summary>
    /// Address not valid
    /// 
    /// Default is false (valid or unknown)
    /// </summary>
    public bool AddressNotValid {
      get { return m_addressNotValid; }
      set { m_addressNotValid = value; }
    }
    
    /// <summary>
    /// Axis are idle (no motion)
    /// 
    /// Default is false (unknown)
    /// </summary>
    public bool IdleAxis {
      get { return m_idleAxis; }
      set { m_idleAxis = value; }
    }
    
    /// <summary>
    /// Stack light
    /// </summary>
    public int StackLight {
      get { return (int)m_stackLight.Value;  }
      set { m_stackLight = (Model.StackLight)value; }
    }

    /// <summary>
    /// Auto + unknown = Active (default: unknown)
    /// </summary>
    public bool AutoUnknownIsActive {
      get { return m_autoUnknownIsActive; }
      set { m_autoUnknownIsActive = value; }
    }
    
    /// <summary>
    /// When the status + auto/manual is still unknown, set it to inactive
    /// </summary>
    public bool UnknownIsInactive {
      get { return m_unknownIsInactive; }
      set { m_unknownIsInactive = value; }
    }

    /// <summary>
    /// Manual + unknown = Inactive (default: unknown)
    /// </summary>
    public bool ManualUnknownIsInactive
    {
      get { return m_manualUnknownIsInactive; }
      set { m_manualUnknownIsInactive = value; }
    }

    /// <summary>
    /// Option to consider the machine is probably off in case of acquisition error
    /// </summary>
    public bool ProbablyOffIfAcquisitionError {
      get { return m_probablyOffIfAcquisitionError; }
      set { m_probablyOffIfAcquisitionError = value; }
    }
    
    /// <summary>
    /// Option to consider the jog flag is valid only if the machine is active
    /// </summary>
    public bool JogIfActiveOnly {
      get { return m_jogIfActiveOnly; }
      set { m_jogIfActiveOnly = value; }
    }
    
    /// <summary>
    /// Option to consider the red stack light corresponds to an emergency stop
    /// </summary>
    public bool RedStackLightIsEmergency {
      get { return m_redStackLightIsEmergency; }
      set { m_redStackLightIsEmergency = value; }
    }

    /// <summary>
    /// Machine Mode Id
    /// </summary>
    public int MachineModeId {
      get
      {
        Compute ();
        return m_machineModeId;
      }
    }

    /// <summary>
    /// Is the program active ? From all the other parameters
    /// </summary>
    public bool ActiveProgram {
      get
      {
        Compute ();
        return m_activeProgram;
      }
    }

    /// <summary>
    /// Is the programmed feedrate active ? From all the other parameters
    /// </summary>
    public bool ActiveProgrammedFeedrate {
      get
      {
        Compute ();
        return m_activeProgrammedFeedrate;
      }
    }
    #endregion // Getters / Setters

    #region Constructors / Destructor / ToString methods
    /// <summary>
    /// Description of the constructor
    /// </summary>
    public MachineMode ()
      : base ("Lemoine.Cnc.InOut.MachineMode")
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
    #endregion // Constructors / Destructor / ToString methods

    #region Methods
    /// <summary>
    /// Start method
    /// </summary>
    /// <returns></returns>
    public bool Start ()
    {
      m_motion = null;
      m_emergency = false;
      m_inactive = false;
      m_active = false;
      m_auto = false;
      m_unavailable = false;
      m_alarmSignal = false;
      m_error = false;
      m_running = null;
      m_manual = null;
      m_jog = false;
      m_handle = false;
      m_reference = false;
      m_mdi = false;
      m_singleBlock = false;
      m_dryRun = false;
      m_machineLock = false;
      m_hold = false;
      m_off = false;
      m_programExecution = false;
      m_plcBlock = false;
      m_noRunningProgram = false;
      m_ready = false;
      m_stopped = false;
      m_finished = false;
      m_reset = false;
      m_notReady = false;
      m_interrupted = false;
      m_errorCleared = false;
      m_toolChange = false;
      m_laserCheck = false;
      m_palletChange = false;
      m_probingCycle = false;
      m_autoHomePositioning = false;
      m_mStop = false;
      m_m0 = false;
      m_m1 = false;
      m_m60 = false;
      m_mWait = false;
      m_mCode = null;
      m_activeMCodes = new List<int> ();
      m_feedrateOverride = null;
      m_rapidTraverseOverride = null;
      m_acquisitionError = false;
      m_remoteAcquisitionError = false;
      m_pingOk = false;
      m_addressNotValid = false;
      
      m_machineModeId = 0;
      m_activeProgram = false;
      m_activeProgrammedFeedrate = false;
      
      return true;
    }
    
    bool IsAuto ()
    {
      return Auto || (Manual.HasValue && !Manual.Value);
    }
    
    bool IsManual ()
    {
      return !Auto
        && ((Manual.HasValue && Manual.Value)
            || IsJog () || Handle || MDI);
    }
    
    bool IsJog ()
    {
      if (JogIfActiveOnly) {
        return Jog && IsActive ();
      }
      else {
        return Jog;
      }
    }
    
    bool IsRunning () {
      return m_running.HasValue && m_running.Value;
    }
    
    bool IsNotRunning () {
      return m_running.HasValue && !m_running.Value;
    }
    
    bool IsInMotion () {
      return Motion.HasValue && Motion.Value;
    }

    bool IsInactiveFromStackLight ()
    {
      return m_stackLight.HasValue
        && (m_stackLight.Value.IsOnOrFlashingIfAcquired (StackLightColor.Red)
            || (m_stackLight.Value.HasFlag (Model.StackLightColor.Green, StackLightStatus.Off)
                && m_stackLight.Value.IsOnOrFlashingIfAcquired (StackLightColor.Yellow)));
    }

    bool IsInactive ()
    {
      return (Inactive || IsNotRunning () || IsInactiveFromStackLight ())
        && !IsRunning ()
        && !IsInMotion ()
        && !Active;
    }
    
    bool IsActive ()
    {
      return (Active || IsInMotion () || IsRunning ())
        && !IsNotRunning ()
        && !Inactive;
    }
    
    /// <summary>
    /// No motion, probably inactive (but not sure)
    /// </summary>
    /// <returns></returns>
    bool IsNoMotion ()
    {
      return (!Motion.HasValue && IdleAxis) || (Motion.HasValue && !Motion.Value);
    }
    
    /// <summary>
    /// This is not sure there is no motion
    /// </summary>
    /// <returns></returns>
    bool IsMotionUnknown ()
    {
      return !Motion.HasValue && !IdleAxis;
    }
    
    /// <summary>
    /// There is either no motion or the motion status is unknown
    /// </summary>
    /// <returns></returns>
    bool IsNoMotionOrUnknown ()
    {
      return !Motion.HasValue || !Motion.Value;
    }
    
    void Compute ()
    {
      if (m_machineModeId <= 0) {
        m_machineModeId = ComputeMachineMode ();
      }
    }
    
    int ComputeMachineMode ()
    {
      if (!IsInMotion ()) { // Discard the next values if a motion was detected
        if (AddressNotValid) {
          return (int)Lemoine.Model.MachineModeId.AcquisitionError;
        }
        if (AcquisitionError) {
          if (ProbablyOffIfAcquisitionError && !PingOk) {
            return (int)Lemoine.Model.MachineModeId.ProbablyOff;
          }
          else {
            return (int)Lemoine.Model.MachineModeId.AcquisitionError;
          }
        }
        if (RemoteAcquisitionError) {
          return (int)Lemoine.Model.MachineModeId.AcquisitionError;
        }
        if (Unavailable) {
          if (ProbablyOffIfAcquisitionError && !PingOk) {
            return (int)Lemoine.Model.MachineModeId.ProbablyOff;
          }
          else {
            return (int)Lemoine.Model.MachineModeId.Unavailable;
          }
        }
        if (Off) {
          return (int)Lemoine.Model.MachineModeId.Off;
        }
        if (Emergency || (m_redStackLightIsEmergency && m_stackLight.HasValue && m_stackLight.Value.IsOnOrFlashingIfAcquired (StackLightColor.Red))) {
          if (IsAuto ()) {
            return (int)Lemoine.Model.MachineModeId.AutoEmergency;
          }
          else {
            return (int)Lemoine.Model.MachineModeId.Emergency;
          }
        }
        if (Error) {
          if (IsAuto ()) {
            return (int)Lemoine.Model.MachineModeId.AutoError;
          }
          else {
            return (int)Lemoine.Model.MachineModeId.Error;
          }
        }
        if (ErrorCleared) {
          return (int)Lemoine.Model.MachineModeId.AutoErrorCleared;
        }
      }
      if (!IsManual ()) {
        if (!IsInMotion ()) { // Discard the next values if a motion was detected
          if (Ready) {
            return (int)Lemoine.Model.MachineModeId.Ready;
          }
          if (Stopped) {
            return (int)Lemoine.Model.MachineModeId.Stopped;
          }
          if (Finished) {
            return (int)Lemoine.Model.MachineModeId.Finished;
          }
          if (Reset) {
            return (int)Lemoine.Model.MachineModeId.Reset;
          }
          if (NotReady) {
            return (int)Lemoine.Model.MachineModeId.NotReady;
          }
          if (NoRunningProgram) {
            return (int)Lemoine.Model.MachineModeId.AutoNoRunningProgram;
          }
          if (Hold) {
            return (int)Lemoine.Model.MachineModeId.Hold;
          }
          if (M0) {
            return (int)Lemoine.Model.MachineModeId.M0;
          }
          if (M1) {
            return (int)Lemoine.Model.MachineModeId.M1;
          }
          if (M60) {
            return (int)Lemoine.Model.MachineModeId.M60;
          }
          if (MWait) {
            return (int)Lemoine.Model.MachineModeId.MWait;
          }
        }
        // TODO: parametrize the m codes
        if (MCode.HasValue) {
          switch (MCode.Value) {
            case 0:
              if (!IsInMotion ()) {
                return (int)Lemoine.Model.MachineModeId.M0;
              }
              else {
                break;
              }
            case 1:
              if (!IsInMotion ()) {
                return (int)Lemoine.Model.MachineModeId.M1;
              }
              else {
                break;
              }
            case 60:
              if (!IsInMotion ()) {
                return (int)Lemoine.Model.MachineModeId.M60;
              }
              else {
                break;
              }
            case 6:
              if (IsAuto ()) {
                return (int)Lemoine.Model.MachineModeId.AutoToolChange;
              }
              else {
                break;
              }
            default:
              break;
          }
        }
        foreach (var activeMCode in ActiveMCodes) {
          switch ((int)activeMCode) {
            case 0:
              if (!IsInMotion ()) {
                return (int)Lemoine.Model.MachineModeId.M0;
              }
              else {
                break;
              }
            case 1:
              if (!IsInMotion ()) {
                return (int)Lemoine.Model.MachineModeId.M1;
              }
              else {
                break;
              }
            case 60:
              if (!IsInMotion ()) {
                return (int)Lemoine.Model.MachineModeId.M60;
              }
              else {
                break;
              }
            case 6:
              if (IsAuto ()) {
                return (int)Lemoine.Model.MachineModeId.AutoToolChange;
              }
              else {
                break;
              }
            default:
              break;
          }
        }
        if (MStop && !IsInMotion ()) {
          return (int)Lemoine.Model.MachineModeId.MStop;
        }
        if (Interrupted && !IsInMotion ()) {
          return (int)Lemoine.Model.MachineModeId.Interrupted;
        }
        if (DryRun) {
          return (int)Lemoine.Model.MachineModeId.DryRun;
        }
        if (MachineLock) {
          return (int)Lemoine.Model.MachineModeId.MachineLock;
        }
      }
      if (ToolChange) {
        if (IsAuto ()) {
          return (int)Lemoine.Model.MachineModeId.AutoToolChange;
        }
      }
      
      if (Reference) {
        if (IsInactive ()) {
          return (int)Lemoine.Model.MachineModeId.ManualInactive;
        }
        else {
          return (int)Lemoine.Model.MachineModeId.Reference;
        }
      }
      
      if (ProbingCycle) {
        if (IsAuto ()) {
          m_activeProgram = true;
          m_activeProgrammedFeedrate = true;
          return (int)Lemoine.Model.MachineModeId.AutoProbingCycle;
        }
        return (int)Lemoine.Model.MachineModeId.ProbingCycle;
      }
      if (LaserCheck) {
        if (IsAuto ()) {
          m_activeProgram = true;
          return (int)Lemoine.Model.MachineModeId.AutoLaserCheck;
        }
      }
      if (PalletChange) {
        if (IsAuto ()) {
          m_activeProgram = true;
          return (int)Lemoine.Model.MachineModeId.AutoPalletChange;
        }
      }
      if (AutoHomePositioning) {
        m_activeProgram = true;
        return (int)Lemoine.Model.MachineModeId.AutoHomePositioning;
      }
      if (IsActive ()) {
        if (SingleBlock) {
          return (int)Lemoine.Model.MachineModeId.SingleBlockActive;
        }
        if (MDI) {
          return (int)Lemoine.Model.MachineModeId.MdiActive;
        }
        if (IsJog ()) {
          return (int)Lemoine.Model.MachineModeId.JogActive;
        }
        if (Handle) {
          return (int)Lemoine.Model.MachineModeId.HandleActive;
        }
        if (IsManual ()) {
          return (int)Lemoine.Model.MachineModeId.ManualActive;
        }
        if (IsAuto ()) {
          m_activeProgram = true;
          if (Motion.HasValue) {
            if (Motion.Value) {
              m_activeProgrammedFeedrate = true;
              return (int)Lemoine.Model.MachineModeId.AutoMachining;
            }
            else {
              return (int)Lemoine.Model.MachineModeId.AutoOtherOperation;
            }
          }
          else if (IdleAxis) {
            return (int)Lemoine.Model.MachineModeId.AutoOtherOperation;
          }
          return (int)Lemoine.Model.MachineModeId.AutoActive;
        }
        return (int)Lemoine.Model.MachineModeId.Active;
      } // IsActive ()
      if (IsAuto () && !IsInMotion ()) { // AutoNullOverride
        if (FeedrateOverride.HasValue || RapidTraverseOverride.HasValue) {
          if ( (FeedrateOverride.HasValue && (FeedrateOverride.Value < FeedrateOverrideThreshold))
              || (RapidTraverseOverride.HasValue && (RapidTraverseOverride.Value < RapidTraverseOverrideTreshold))) {
            m_activeProgrammedFeedrate = true;
            return (int)Lemoine.Model.MachineModeId.AutoNullOverride;
          }
        }
      } // IsAuto ()
      if (IsInactive ()) {
        if (AlarmSignal) {
          if (IsAuto ()) {
            return (int)Lemoine.Model.MachineModeId.AutoAlarmStop;
          }
          else {
            return (int)Lemoine.Model.MachineModeId.AlarmStop;
          }
        }
        if (SingleBlock) {
          return (int)Lemoine.Model.MachineModeId.SingleBlockInactive;
        }
        if (MDI) {
          return (int)Lemoine.Model.MachineModeId.MdiInactive;
        }
        if (IsJog ()) {
          return (int)Lemoine.Model.MachineModeId.JogInactive;
        }
        if (Handle) {
          return (int)Lemoine.Model.MachineModeId.HandleInactive;
        }
        if (IsManual ()) {
          return (int)Lemoine.Model.MachineModeId.ManualInactive;
        }
        if (On) {
          return (int)Lemoine.Model.MachineModeId.InactiveOn;
        }
        if (IsAuto ()) {
          // AutoNullOverride is processed above
          return (int)Lemoine.Model.MachineModeId.AutoInactive;
        }
        return (int)Lemoine.Model.MachineModeId.Inactive;
      } // IsInactive
      else if (IsNoMotion ()) { // No motion and probably inactive (but not sure)
        if (AlarmSignal) {
          if (IsAuto ()) {
            return (int)Lemoine.Model.MachineModeId.AutoAlarmStop;
          }
          else {
            return (int)Lemoine.Model.MachineModeId.AlarmStop;
          }
        }
        if (SingleBlock) {
          return (int)Lemoine.Model.MachineModeId.SingleBlockNoMotion;
        }
        if (MDI) {
          return (int)Lemoine.Model.MachineModeId.MdiNoMotion;
        }
        if (IsJog ()) {
          return (int)Lemoine.Model.MachineModeId.JogNoMotion;
        }
        if (Handle) {
          return (int)Lemoine.Model.MachineModeId.HandleNoMotion;
        }
        if (IsManual ()) {
          return (int)Lemoine.Model.MachineModeId.ManualNoMotion;
        }
        if (IsAuto ()) {
          // AutoNullOverride is processed above
          if (AutoUnknownIsActive) {
            return (int)Lemoine.Model.MachineModeId.AutoOtherOperation;
          }
          return (int)Lemoine.Model.MachineModeId.AutoNoMotion;
        }
        return (int)Lemoine.Model.MachineModeId.NoMotion;
      } // Probably inactive
      // Unknown
      if (ProgramExecution && !IsJog () && !Handle) {
        m_activeProgram = true;
        m_activeProgrammedFeedrate = true;
        if (FeedrateOverride.HasValue || RapidTraverseOverride.HasValue) {
          // AutoNullOverride is processed above
          if (MDI) {
            m_activeProgram = false;
            return (int)Lemoine.Model.MachineModeId.MdiActive;
          }
          else if (SingleBlock) {
            return (int)Lemoine.Model.MachineModeId.SingleBlockActive;
          }
          else { // Not MDI or SingleBlock
            if (IsNoMotionOrUnknown ()) {
              return (int)Lemoine.Model.MachineModeId.AutoOtherOperation;
            }
            else {
              return (int)Lemoine.Model.MachineModeId.AutoActive;
            }
          }
        }
        else { // No override
          if (UnknownIsInactive) {
            if (MDI) {
              return (int)Lemoine.Model.MachineModeId.MdiInactive;
            }
            else if (SingleBlock) {
              return (int)Lemoine.Model.MachineModeId.SingleBlockInactive;
            }
            else {
              return (int)Lemoine.Model.MachineModeId.AutoInactive;
            }
          }
          else { // !UnknownIsInactive
            if (MDI) {
              return (int)Lemoine.Model.MachineModeId.Mdi;
            }
            else if (SingleBlock) {
              return (int)Lemoine.Model.MachineModeId.SingleBlock;
            }
            else {
              return (int)Lemoine.Model.MachineModeId.AutoUnknown;
            }
          }
        }
      }
      if (AlarmSignal && !IsInMotion ()) {
        if (IsAuto ()) {
          return (int)Lemoine.Model.MachineModeId.AutoAlarmStop;
        }
        else {
          return (int)Lemoine.Model.MachineModeId.AlarmStop;
        }
      }
      if (IsManual () || SingleBlock || MDI || IsJog () || Handle) {
        if (this.ManualUnknownIsInactive) {
          if (SingleBlock) {
            return (int)Lemoine.Model.MachineModeId.SingleBlockInactive;
          }
          else if (MDI) {
            return (int)Lemoine.Model.MachineModeId.MdiInactive;
          }
          else if (IsJog ()) {
            return (int)Lemoine.Model.MachineModeId.JogInactive;
          }
          else if (Handle) {
            return (int)Lemoine.Model.MachineModeId.HandleInactive;
          }
          else { // Other manual
            return (int)Lemoine.Model.MachineModeId.ManualInactive;
          }
        }
        else { // !ManualUnknownIsInactive
          if (SingleBlock) {
            return (int)Lemoine.Model.MachineModeId.SingleBlock;
          }
          else if (MDI) {
            return (int)Lemoine.Model.MachineModeId.Mdi;
          }
          else if (IsJog ()) {
            return (int)Lemoine.Model.MachineModeId.Jog;
          }
          else if (Handle) {
            return (int)Lemoine.Model.MachineModeId.Handle;
          }
          else { // Other manual
            return (int)Lemoine.Model.MachineModeId.ManualUnknown;
          }
        }
      }
      if (IsAuto ()) {
        // AutoNullOverride is processed above
        if (AutoUnknownIsActive) {
          m_activeProgram = true;
          if (Motion.HasValue) {
            if (Motion.Value) {
              m_activeProgrammedFeedrate = true;
              return (int)Lemoine.Model.MachineModeId.AutoMachining;
            }
            else {
              return (int)Lemoine.Model.MachineModeId.AutoOtherOperation;
            }
          }
          else if (IdleAxis) {
            return (int)Lemoine.Model.MachineModeId.AutoOtherOperation;
          }
          return (int)Lemoine.Model.MachineModeId.AutoActive;
        }
        if (UnknownIsInactive) {
          return (int)Lemoine.Model.MachineModeId.AutoInactive;
        }
        return (int)Lemoine.Model.MachineModeId.AutoUnknown;
      }
      if (PlcBlock) {
        return (int)Lemoine.Model.MachineModeId.Active;
      }
      if (UnknownIsInactive) {
        return (int)Lemoine.Model.MachineModeId.Inactive;
      }
      return (int)Lemoine.Model.MachineModeId.MissingInfo;
    }
    #endregion // Methods
  }
}

#endif // NETSTANDARD || NET48 || NETCOREAPP
