// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;

namespace Lemoine.Cnc
{
  /// <summary>
  /// EthernetIP input module, set methods
  /// </summary>
  public sealed partial class EthernetIP
  {
    /// <summary>
    /// Set a bool
    /// </summary>
    /// <param name="param">Format {name}|{elementCount}|{elementNumber} or just {name}</param>
    /// <param name="v">Value to set</param>
    public void SetBool (string param, object v)
    {
      throw new NotImplementedException ();
    }

    /// <summary>
    /// Set a UInt8
    /// </summary>
    /// <param name="param">Format {name}|{elementCount}|{elementNumber} or just {name}</param>
    /// <param name="v">Value to set</param>
    public void SetUInt8 (string param, object v)
    {
      SetValue<byte> (param, 1, v);
    }

    /// <summary>
    /// Set a Int8
    /// </summary>
    /// <param name="param">Format {name}|{elementCount}|{elementNumber} or just {name}</param>
    /// <param name="v">Value to set</param>
    public void SetInt8 (string param, object v)
    {
      SetValue<sbyte> (param, 1, v);
    }

    /// <summary>
    /// Set a UInt16
    /// </summary>
    /// <param name="param">Format {name}|{elementCount}|{elementNumber} or just {name}</param>
    /// <param name="v">Value to set</param>
    public void SetUInt16 (string param, object v)
    {
      SetValue<UInt16> (param, 2, v);
    }

    /// <summary>
    /// Set a Int16
    /// </summary>
    /// <param name="param">Format {name}|{elementCount}|{elementNumber} or just {name}</param>
    /// <param name="v">Value to set</param>
    public void SetInt16 (string param, object v)
    {
      SetValue<Int16> (param, 2, v);
    }

    /// <summary>
    /// Set a UInt32
    /// </summary>
    /// <param name="param">Format {name}|{elementCount}|{elementNumber} or just {name}</param>
    /// <param name="v">Value to set</param>
    public void SetUInt32 (string param, object v)
    {
      SetValue<UInt32> (param, 4, v);
    }

    /// <summary>
    /// Set a Int32
    /// </summary>
    /// <param name="param">Format {name}|{elementCount}|{elementNumber} or just {name}</param>
    /// <param name="v">Value to set</param>
    public void SetInt32 (string param, object v)
    {
      SetValue<Int32> (param, 4, v);
    }

    /// <summary>
    /// Set a float
    /// </summary>
    /// <param name="param">Format {name}|{elementCount}|{elementNumber} or just {name}</param>
    /// <param name="v">Value to set</param>
    public void SetFloat (string param, object v)
    {
      SetValue<float> (param, 4, v);
    }

    void SetValue<T> (string param, int elementSize, object v)
    {
      if (m_acquisitionError) {
        log.InfoFormat ("SetValue: previous acquisition error, skip {0}", param);
        throw new Exception ("EthernetIP - don't write this turn");
      }

      var tagName = param;
      var elementCount = 1;
      var elementNumber = 0;

      // If an element within an array is to be read
      var split = param.Split ('|');
      if (split.Length == 3) {
        tagName = split[0];
        elementCount = int.Parse (split[1]);
        elementNumber = int.Parse (split[2]);
      }
      else {
        tagName = split[0];
      }

      var tag = GetTag<T> (tagName, elementCount, elementSize);
      try {
        tag.SetValue (elementNumber, elementSize, v);
      }
      catch (Exception ex) {
        ProcessException (ex);
        throw;
      }
    }
  }
}
