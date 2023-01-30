// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Description of ErrorCodeException.
  /// </summary>
//#pragma warning disable CA1032 // Implement standard exception constructors
  [Serializable]
  public class ErrorCodeException : Exception
//#pragma warning restore CA1032 // Implement standard exception constructors
  {
    /// <summary>
    /// Error code associated to the exception
    /// </summary>
    public LibPlcTag.StatusCode ErrorCode { get; private set; }

    /// <summary>
    /// Key having triggered the exception
    /// </summary>
    public string Key { get; private set; }

    /// <summary>
    /// Description of the constructor
    /// </summary>
    public ErrorCodeException (LibPlcTag.StatusCode errorCode, string key)
      : base ($"EthernetIP: key {key} has status code {errorCode}")
    {
      ErrorCode = errorCode;
      Key = key;
    }
  }
}
