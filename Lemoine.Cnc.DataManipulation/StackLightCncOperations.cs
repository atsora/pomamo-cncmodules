// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

#if NETSTANDARD || NET48 || NETCOREAPP

using System;

using Lemoine.Core.Log;
using Lemoine.Model;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Stack light operations
  /// </summary>
  public sealed class StackLightCncOperations : Lemoine.Cnc.BaseCncModule, Lemoine.Cnc.ICncModule, IDisposable
  {
    #region Members
    Model.StackLight? m_stackLight;
    #endregion // Members

    #region Getters / Setters
    /// <summary>
    /// Initialized stack light (there was an access to it => initialize it)
    /// </summary>
    StackLight InitializedStackLight
    {
      get
      {
        if (!m_stackLight.HasValue) {
          m_stackLight = Model.StackLight.None;
        }
        return m_stackLight.Value;
      }
    }

    /// <summary>
    /// Stack light red
    /// </summary>
    public bool StackLightRed
    {
      get { return m_stackLight.Value.IsOnOrFlashing (StackLightColor.Red); }
      set { m_stackLight = InitializedStackLight.Set (Model.StackLightColor.Red, value); }
    }
    
    /// <summary>
    /// Stack light red, flashing
    /// </summary>
    public bool StackLightRedFlashing
    {
      get { return m_stackLight.Value.HasFlag (Model.StackLight.RedFlashing); }
      set {
        if (value) {
          m_stackLight = InitializedStackLight.Set (Model.StackLightColor.Red, Model.StackLightStatus.Flashing);
        }
        else if (StackLightRed) {
          m_stackLight = InitializedStackLight.Set (Model.StackLightColor.Red, Model.StackLightStatus.On);
        }
      }
    }

    /// <summary>
    /// Stack light yellow
    /// </summary>
    public bool StackLightYellow
    {
      get { return m_stackLight.Value.IsOnOrFlashing (StackLightColor.Yellow); }
      set { m_stackLight = InitializedStackLight.Set (Model.StackLightColor.Yellow, value); }
    }
    
    /// <summary>
    /// Stack light yellow, flashing
    /// </summary>
    public bool StackLightYellowFlashing
    {
      get { return m_stackLight.Value.HasFlag (Model.StackLight.YellowFlashing); }
      set {
        if (value) {
          m_stackLight = InitializedStackLight.Set (Model.StackLightColor.Yellow, Model.StackLightStatus.Flashing);
        }
        else if (StackLightYellow) {
          m_stackLight = InitializedStackLight.Set (Model.StackLightColor.Yellow, Model.StackLightStatus.On);
        }
      }
    }

    /// <summary>
    /// Stack light green
    /// </summary>
    public bool StackLightGreen
    {
      get { return m_stackLight.Value.IsOnOrFlashing (StackLightColor.Green); }
      set { m_stackLight = InitializedStackLight.Set (Model.StackLightColor.Green, value); }
    }
    
    /// <summary>
    /// Stack light green, flashing
    /// </summary>
    public bool StackLightGreenFlashing
    {
      get { return m_stackLight.Value.HasFlag (Model.StackLight.GreenFlashing); }
      set {
        if (value) {
          m_stackLight = InitializedStackLight.Set (Model.StackLightColor.Green, Model.StackLightStatus.Flashing);
        }
        else if (StackLightGreen) {
          m_stackLight = InitializedStackLight.Set (Model.StackLightColor.Green, Model.StackLightStatus.On);
        }
      }
    }

    /// <summary>
    /// Stack light blue
    /// </summary>
    public bool StackLightBlue
    {
      get { return m_stackLight.Value.IsOnOrFlashing (StackLightColor.Blue); }
      set { m_stackLight = InitializedStackLight.Set (Model.StackLightColor.Blue, value); }
    }
    
    /// <summary>
    /// Stack light blue, flashing
    /// </summary>
    public bool StackLightBlueFlashing
    {
      get { return m_stackLight.Value.HasFlag (Model.StackLight.BlueFlashing); }
      set {
        if (value) {
          m_stackLight = InitializedStackLight.Set (Model.StackLightColor.Blue, Model.StackLightStatus.Flashing);
        }
        else if (StackLightBlue) {
          m_stackLight = InitializedStackLight.Set (Model.StackLightColor.Blue, Model.StackLightStatus.On);
        }
      }
    }

    /// <summary>
    /// Stack light white
    /// </summary>
    public bool StackLightWhite
    {
      get { return m_stackLight.Value.IsOnOrFlashing (StackLightColor.White); }
      set { m_stackLight = InitializedStackLight.Set (Model.StackLightColor.White, value); }
    }
    
    /// <summary>
    /// Stack light white, flashing
    /// </summary>
    public bool StackLightWhiteFlashing
    {
      get { return m_stackLight.Value.HasFlag (Model.StackLight.WhiteFlashing); }
      set {
        if (value) {
          m_stackLight = InitializedStackLight.Set (Model.StackLightColor.White, Model.StackLightStatus.Flashing);
        }
        else if (StackLightWhite) {
          m_stackLight = InitializedStackLight.Set (Model.StackLightColor.White, Model.StackLightStatus.On);
        }
      }
    }

    /// <summary>
    /// Stack light
    /// 
    /// Return an exception if it was not initialized
    /// </summary>
    public int StackLight
    {
      get
      {
        return (int)m_stackLight.Value;
      }
      set { m_stackLight = (Model.StackLight)value; }
    }
    #endregion // Getters / Setters

    #region Constructors / Destructor / ToString methods
    /// <summary>
    /// Description of the constructor
    /// </summary>
    public StackLightCncOperations ()
      : base ("Lemoine.Cnc.InOut.StackLightCncOperations")
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
      m_stackLight = null;
      return true;
    }
    #endregion // Methods
  }
}

#endif // NETSTANDARD || NET48 || NETCOREAPP
