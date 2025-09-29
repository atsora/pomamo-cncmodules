// Copyright (c) 2023-2025 Atsora Solutions
//
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Converter
  /// </summary>
  internal class OpcUaConverter
  {
    /// <summary>
    /// Constructor
    /// </summary>
    public OpcUaConverter ()
    {
    }

    public T ConvertAuto<T> (object datavalue)
    {
      return (T)ConvertAuto (datavalue, typeof (T));
    }

    public virtual object ConvertAuto (object datavalue, Type type)
    {
      if (datavalue.GetType ().Equals (type)) {
        return datavalue;
      }

      if (type == typeof (object)) {
        return datavalue;
      }

      if (type.IsInterface && type.IsInstanceOfType (datavalue)) {
        return datavalue;
      }

      if (datavalue.GetType ().Equals (typeof (Nullable<int>))) {
        return ConvertAuto (((Nullable<int>)datavalue).Value, type);
      }
      else if (datavalue.GetType ().Equals (typeof (Nullable<long>))) {
        return ConvertAuto (((Nullable<long>)datavalue).Value, type);
      }
      else if (datavalue.GetType ().Equals (typeof (Nullable<double>))) {
        return ConvertAuto (((Nullable<double>)datavalue).Value, type);
      }
      else if (datavalue.GetType ().Equals (typeof (Nullable<bool>))) {
        return ConvertAuto (((Nullable<bool>)datavalue).Value, type);
      }
      else if (datavalue.GetType ().Equals (typeof (Nullable<uint>))) {
        return ConvertAuto (((Nullable<uint>)datavalue).Value, type);
      }
      else if (type == typeof (Nullable<bool>)) {
        return (Nullable<bool>)Convert.ChangeType (datavalue, typeof (bool));
      }
      else if (type == typeof (Nullable<int>)) {
        return (Nullable<int>)Convert.ChangeType (datavalue, typeof (int));
      }
      else if (type == typeof (Nullable<double>)) {
        return (Nullable<double>)Convert.ChangeType (datavalue, typeof (double));
      }
      else if (type == typeof (Nullable<long>)) {
        return (Nullable<long>)Convert.ChangeType (datavalue, typeof (long));
      }
      else if (type == typeof (Nullable<uint>)) {
        return (Nullable<uint>)Convert.ChangeType (datavalue, typeof (uint));
      }

      if (datavalue.GetType ().Equals (typeof (string))) {
        // Try the parse methods
        // Note: string to int and long is managed by Convert.ChangeType
        if (type == typeof (double)) {
          try {
            return double.Parse (datavalue.ToString (), System.Globalization.CultureInfo.InvariantCulture);
          }
          catch (Exception) { }
        }
      }

      // Try with a constructor
      foreach (var constructor in type.GetConstructors ()) {
        var parameters = constructor.GetParameters ();
        if (1 == parameters.Length) {
          var parameterType = parameters[0].ParameterType;
          if (parameterType.Equals (type)) {
            continue;
          }
          object parameter;
          try {
            parameter = ConvertAuto (datavalue, parameterType);
          }
          catch (Exception) {
            continue;
          }
          try {
            return constructor.Invoke (new object[] { parameter });
          }
          catch (Exception) { }
        }
      }

      return Convert.ChangeType (datavalue, type);
    }
  }
}
