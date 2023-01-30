// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Lemoine.I18N;
using Lemoine.Core.Log;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Module to make operations between different values.
  /// 
  /// It uses the inverse polish notation to set and get the data.
  /// Remember your old HP48...
  /// </summary>
  public sealed class Operations : Lemoine.Cnc.BaseCncModule, Lemoine.Cnc.ICncModule, IDisposable
  {
    #region Members
    readonly Stack m_stack = new Stack ();
    bool error = false;
    #endregion

    #region Getters / Setters
    /// <summary>
    /// An error occurred
    /// </summary>
    public bool Error
    {
      get { return error; }
    }
    #endregion

    #region Constructors / Destructor / ToString methods
    /// <summary>
    /// Description of the constructor
    /// </summary>
    public Operations ()
      : base ("Lemoine.Cnc.InOut.Operations")
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
    /// Start method: reset the different values
    /// </summary>
    public void Start ()
    {
      Clear ();
    }

    /// <summary>
    /// Clear the stack and the error status
    /// </summary>
    public void Clear ()
    {
      m_stack.Clear ();
      error = false;
    }

    /// <summary>
    /// Push a data in the stack
    /// </summary>
    /// <param name="data">Data to push in the stack</param>
    public void Push (object data)
    {
      if (log.IsDebugEnabled) {
        log.Debug ($"Push: push {data} ({data.GetType ()}) in the stack");
      }
      m_stack.Push (data);
    }

    /// <summary>
    /// Get a string parameter to set in one of the variables
    /// </summary>
    /// <param name="param">string value to set</param>
    /// <returns></returns>
    public string GetString (string param)
    {
      log.Debug ($"GetString: param={param}");
      return param;
    }

    /// <summary>
    /// Get a double parameter to set in one of the variables
    /// </summary>
    /// <param name="param">double value to set</param>
    /// <returns></returns>
    public double GetDouble (string param)
    {
      CultureInfo usCultureInfo = new CultureInfo ("en-US");
      log.DebugFormat ("GetDouble: " +
                       "param={0}",
                       param);
      try {
        return double.Parse (param,
                             usCultureInfo);
      }
      catch (Exception ex) {
        log.Error ("GetDouble: exception", ex);
        error = true;
        throw;
      }
    }

    /// <summary>
    /// Get an int parameter to set in one of the variables
    /// </summary>
    /// <param name="param">int value to set</param>
    /// <returns></returns>
    public int GetInt (string param)
    {
      log.DebugFormat ("GetInt: " +
                       "param={0}",
                       param);
      try {
        return int.Parse (param);
      }
      catch (Exception ex) {
        log.Error ("GetInt: exception", ex);
        error = true;
        throw;
      }
    }

    /// <summary>
    /// Get an uint parameter to set in one of the variables
    /// </summary>
    /// <param name="param">uint value to set</param>
    /// <returns></returns>
    public uint GetUInt (string param)
    {
      log.DebugFormat ("GetUInt: " +
                       "param={0}",
                       param);
      try {
        return uint.Parse (param);
      }
      catch (Exception ex) {
        log.Error ($"GetUInt: exception", ex);
        error = true;
        throw;
      }
    }

    /// <summary>
    /// Get a bool parameter to set in one of the variables
    /// </summary>
    /// <param name="param">bool value to set</param>
    /// <returns></returns>
    public bool GetBool (string param)
    {
      if (log.IsDebugEnabled) {
        log.Debug ($"GetBool: param={param}");
      }
      try {
        return bool.Parse (param);
      }
      catch (Exception ex) {
        log.Error ($"GetBool: exception for param={param}", ex);
        error = true;
        throw;
      }
    }

    /// <summary>
    /// Get the object at the top of the stack
    /// </summary>
    /// <param name="param">unused</param>
    /// <returns></returns>
    public object Peek (string param)
    {
      object result = m_stack.Peek ();
      log.Debug ($"Peek: read the top element of the stack {result}");
      return result;
    }

    /// <summary>
    /// Get the size of the stack. The stack is not modified.
    /// </summary>
    /// <param name="param">unused</param>
    /// <returns></returns>
    public int GetSize (string param)
    {
      int result = m_stack.Count;
      log.DebugFormat ("GetSize: " +
                       "the size of the stack is {0}",
                       result);
      return result;
    }

    /// <summary>
    /// Is the stack empty ? The stack is not modified.
    /// </summary>
    /// <param name="param">unused</param>
    /// <returns></returns>
    public bool IsEmpty (string param)
    {
      return GetSize (param) == 0;
    }

    /// <summary>
    /// Is the stack not empty ? The stack is not modified.
    /// </summary>
    /// <param name="param">unused</param>
    /// <returns></returns>
    public bool IsNotEmpty (string param)
    {
      return !IsEmpty (param);
    }

    #region Number operations

    /// <summary>
    /// add a constant to the top element of the stack
    /// if top is different from 0
    /// </summary>
    /// <param name="incCst"></param>
    /// <returns>new value at the top of the stack</returns>
    public object IncIfNotZero (string incCst)
    {
      if (m_stack.Count < 1) {
        log.ErrorFormat ("Inc: " +
                         "not enough elements in the stack");
        error = true;
        throw new Exception ("Not enough elements in stack");
      }

      int incValue;
      if (Int32.TryParse (incCst, out incValue)) {
        try {
          object x = m_stack.Pop ();
          if (x is double) {
            double dx = (double)x;
            if (dx != 0.0) {
              double y = dx + incValue;
              m_stack.Push (y);
              return y;
            }
            else {
              m_stack.Push (x);
              return x;
            }
          }
          else if (x is long) {
            long lx = (long)x;
            if (lx != 0) {
              long y = lx + incValue;
              m_stack.Push (y);
              return y;
            }
            else {
              m_stack.Push (x);
              return x;
            }
          }
          else if (x is int) {
            int ix = (int)x;
            if (ix != 0) {
              int y = ix + incValue;
              m_stack.Push (y);
              return y;
            }
            else {
              m_stack.Push (x);
              return x;
            }
          }
          else if (x is uint) {
            uint ux = (uint)x;
            if (ux != 0) {
              uint y = ux + (uint)incValue;
              m_stack.Push (y);
              return y;
            }
            else {
              m_stack.Push (x);
              return x;
            }
          }
          else {
            log.ErrorFormat ("Inc: " +
                             "not supported types for x={0}",
                             x);
            error = true;
            throw new ArgumentException ("Unsupported types");
          }
        }
        catch (Exception ex) {
          log.ErrorFormat ("Add: " +
                           "{0} in Pop",
                           ex);
          error = true;
          throw;
        }
      }
      else {
        log.ErrorFormat ("Inc: {0} not an integer", incCst);
        error = true;
        throw new Exception ("Inc with a non integer value");
      }
    }

    /// <summary>
    /// Remove the two first elements of the stack,
    /// add them together and push the result in the stack
    /// </summary>
    /// <param name="param">unused</param>
    /// <returns>new value at the top of the stack</returns>
    public object Add (string param)
    {
      if (m_stack.Count < 2) {
        log.ErrorFormat ("Add: " +
                         "not enough elements in the stack");
        error = true;
        throw new Exception ("Not enough elements in stack");
      }
      object x, y, result;
      try {
        y = m_stack.Pop ();
        x = m_stack.Pop ();
      }
      catch (Exception ex) {
        log.ErrorFormat ("Add: " +
                         "{0} in Pop",
                         ex);
        error = true;
        throw;
      }
      try {
        if (x is double) {
          if (y is double) {
            result = (double)x + (double)y;
          }
          else if (y is long) {
            result = (double)x + (long)y;
          }
          else if (y is int) {
            result = (double)x + (int)y;
          }
          else if (y is uint) {
            result = (double)x + (uint)y;
          }
          else {
            log.ErrorFormat ("Add: " +
                             "not supported types for x={0} y={1}",
                             x, y);
            throw new ArgumentException ("Unsupported types");
          }
        }
        else if (x is long) {
          if (y is double) {
            result = (long)x + (double)y;
          }
          else if (y is long) {
            result = (long)x + (long)y;
          }
          else if (y is int) {
            result = (long)x + (int)y;
          }
          else if (y is uint) {
            result = (long)x + (uint)y;
          }
          else {
            log.ErrorFormat ("Add: " +
                             "not supported types for x={0} y={1}",
                             x, y);
            throw new ArgumentException ("Unsupported types");
          }
        }
        else if (x is int) {
          if (y is double) {
            result = (int)x + (double)y;
          }
          else if (y is long) {
            result = (int)x + (long)y;
          }
          else if (y is int) {
            result = (int)x + (int)y;
          }
          else if (y is uint) {
            result = (int)x + (uint)y;
          }
          else {
            log.ErrorFormat ("Add: " +
                             "not supported types for x={0} y={1}",
                             x, y);
            throw new ArgumentException ("Unsupported types");
          }
        }
        else if (x is uint) {
          if (y is double) {
            result = (uint)x + (double)y;
          }
          else if (y is long) {
            result = (uint)x + (long)y;
          }
          else if (y is int) {
            result = (uint)x + (int)y;
          }
          else if (y is uint) {
            result = (uint)x + (uint)y;
          }
          else {
            log.ErrorFormat ("Add: " +
                             "not supported types for x={0} y={1}",
                             x, y);
            throw new ArgumentException ("Unsupported types");
          }
        }
        else {
          log.ErrorFormat ("Add: " +
                           "not supported types for x={0} y={1}",
                           x, y);
          throw new ArgumentException ("Unsupported types");
        }
      }
      catch (Exception ex) {
        log.ErrorFormat ("Add: " +
                         "exception {0} for x={0} y={1}",
                         ex, x, y);
        error = true;
        throw;
      }
      log.DebugFormat ("Add: " +
                       "{0}+{1}={2}",
                       x, y, result);
      m_stack.Push (result);
      return result;
    }

    /// <summary>
    /// Remove the two first elements of the stack,
    /// substract the second value from the first one
    /// and push the result in the stack
    /// </summary>
    /// <param name="param">unused</param>
    /// <returns>new value at the top of the stack</returns>
    public object Substract (string param)
    {
      if (m_stack.Count < 2) {
        log.ErrorFormat ("Substract: " +
                         "not enough elements in the stack");
        error = true;
        throw new Exception ("Not enough elements in stack");
      }
      object x, y, result;
      try {
        y = m_stack.Pop ();
        x = m_stack.Pop ();
      }
      catch (Exception ex) {
        log.ErrorFormat ("Substract: " +
                         "{0} in Pop",
                         ex);
        error = true;
        throw;
      }
      try {
        if (x is double) {
          if (y is double) {
            result = (double)x - (double)y;
          }
          else if (y is long) {
            result = (double)x - (long)y;
          }
          else if (y is int) {
            result = (double)x - (int)y;
          }
          else if (y is uint) {
            result = (double)x - (uint)y;
          }
          else {
            log.ErrorFormat ("Substract: " +
                             "not supported types for x={0} y={1}",
                             x, y);
            throw new ArgumentException ("Unsupported types");
          }
        }
        else if (x is long) {
          if (y is double) {
            result = (long)x - (double)y;
          }
          else if (y is long) {
            result = (long)x - (long)y;
          }
          else if (y is int) {
            result = (long)x - (int)y;
          }
          else if (y is uint) {
            result = (long)x - (uint)y;
          }
          else {
            log.ErrorFormat ("Substract: " +
                             "not supported types for x={0} y={1}",
                             x, y);
            throw new ArgumentException ("Unsupported types");
          }
        }
        else if (x is int) {
          if (y is double) {
            result = (int)x - (double)y;
          }
          else if (y is long) {
            result = (int)x - (long)y;
          }
          else if (y is int) {
            result = (int)x - (int)y;
          }
          else if (y is uint) {
            result = (int)x - (uint)y;
          }
          else {
            log.ErrorFormat ("Substract: " +
                             "not supported types for x={0} y={1}",
                             x, y);
            throw new ArgumentException ("Unsupported types");
          }
        }
        else if (x is uint) {
          if (y is double) {
            result = (uint)x - (double)y;
          }
          else if (y is long) {
            result = (uint)x - (long)y;
          }
          else if (y is int) {
            result = (uint)x - (int)y;
          }
          else if (y is uint) {
            result = (uint)x - (uint)y;
          }
          else {
            log.ErrorFormat ("Substract: " +
                             "not supported types for x={0} y={1}",
                             x, y);
            throw new ArgumentException ("Unsupported types");
          }
        }
        else {
          log.ErrorFormat ("Substract: " +
                           "not supported types for x={0} y={1}",
                           x, y);
          throw new ArgumentException ("Unsupported types");
        }
      }
      catch (Exception ex) {
        log.ErrorFormat ("Substract: " +
                         "exception {0} for x={0} y={1}",
                         ex, x, y);
        error = true;
        throw;
      }
      log.DebugFormat ("Substract: " +
                       "{0}-{1}={2}",
                       x, y, result);
      m_stack.Push (result);
      return result;
    }

    /// <summary>
    /// Remove the two first elements of the stack,
    /// multiply them together and push the result in the stack
    /// </summary>
    /// <param name="param">unused</param>
    /// <returns>new value at the top of the stack</returns>
    public object Multiply (string param)
    {
      if (m_stack.Count < 2) {
        log.ErrorFormat ("Multiply: " +
                         "not enough elements in the stack");
        error = true;
        throw new Exception ("Not enough elements in stack");
      }
      object x, y, result;
      try {
        y = m_stack.Pop ();
        x = m_stack.Pop ();
      }
      catch (Exception ex) {
        log.ErrorFormat ("Multiply: " +
                         "{0} in Pop",
                         ex);
        error = true;
        throw;
      }
      try {
        if (x is Position) {
          Position position = (Position)x;
          Position newPosition = new Position ();
          newPosition.X = position.X * (double)y;
          newPosition.Y = position.Y * (double)y;
          newPosition.Z = position.Z * (double)y;
          newPosition.A = position.A * (double)y;
          newPosition.B = position.B * (double)y;
          newPosition.C = position.C * (double)y;
          newPosition.U = position.U * (double)y;
          newPosition.V = position.V * (double)y;
          newPosition.W = position.W * (double)y;
          newPosition.Time = position.Time;
          result = newPosition;
        }
        else if (x is double) {
          if (y is double) {
            result = (double)x * (double)y;
          }
          else if (y is long) {
            result = (double)x * (long)y;
          }
          else if (y is int) {
            result = (double)x * (int)y;
          }
          else if (y is uint) {
            result = (double)x * (uint)y;
          }
          else {
            log.ErrorFormat ("Multiply: " +
                             "not supported types for x={0} y={1}",
                             x, y);
            throw new ArgumentException ("Unsupported types");
          }
        }
        else if (x is long) {
          if (y is double) {
            result = (long)x * (double)y;
          }
          else if (y is long) {
            result = (long)x * (long)y;
          }
          else if (y is int) {
            result = (long)x * (int)y;
          }
          else if (y is uint) {
            result = (long)x * (uint)y;
          }
          else {
            log.ErrorFormat ("Multiply: " +
                             "not supported types for x={0} y={1}",
                             x, y);
            throw new ArgumentException ("Unsupported types");
          }
        }
        else if (x is int) {
          if (y is double) {
            result = (int)x * (double)y;
          }
          else if (y is long) {
            result = (int)x * (long)y;
          }
          else if (y is int) {
            result = (int)x * (int)y;
          }
          else if (y is uint) {
            result = (int)x * (uint)y;
          }
          else {
            log.ErrorFormat ("Multiply: " +
                             "not supported types for x={0} y={1}",
                             x, y);
            throw new ArgumentException ("Unsupported types");
          }
        }
        else if (x is uint) {
          if (y is double) {
            result = (uint)x * (double)y;
          }
          else if (y is long) {
            result = (uint)x * (long)y;
          }
          else if (y is int) {
            result = (uint)x * (int)y;
          }
          else if (y is uint) {
            result = (uint)x * (uint)y;
          }
          else {
            log.ErrorFormat ("Multiply: " +
                             "not supported types for x={0} y={1}",
                             x, y);
            throw new ArgumentException ("Unsupported types");
          }
        }
        else {
          log.ErrorFormat ("Multiply: " +
                           "not supported types for x={0} y={1}",
                           x, y);
          throw new ArgumentException ("Unsupported types");
        }
      }
      catch (Exception ex) {
        log.ErrorFormat ("Multiply: " +
                         "exception {0} for x={0} y={1}",
                         ex, x, y);
        error = true;
        throw;
      }
      log.DebugFormat ("Multiply: " +
                       "{0}*{1}={2}",
                       x, y, result);
      m_stack.Push (result);
      return result;
    }

    /// <summary>
    /// Remove the first element of the stack,
    /// multiply this value with the double given in parameter
    /// and push the result in the stack
    /// </summary>
    /// <param name="param">double value to multiply</param>
    /// <returns>new value at the top of the stack</returns>
    public object MultiplyDouble (string param)
    {
      if (m_stack.Count < 1) {
        log.ErrorFormat ("MultiplyDouble: " +
                         "not enough elements in the stack");
        error = true;
        throw new Exception ("Not enough elements in stack");
      }
      double y = GetDouble (param);
      m_stack.Push (y);
      return Multiply ("");
    }

    /// <summary>
    /// Remove the two first elements of the stack,
    /// divide the first value with the second one
    /// and push the result in the stack.
    /// 
    /// Note that the type Position is supported if
    /// it is the first element of the stack.
    /// </summary>
    /// <param name="param">unused</param>
    /// <returns>new value at the top of the stack</returns>
    public object Divide (string param)
    {
      if (m_stack.Count < 2) {
        log.ErrorFormat ("Divide: " +
                         "not enough elements in the stack");
        error = true;
        throw new Exception ("Not enough elements in stack");
      }
      object x, y, result;
      try {
        y = m_stack.Pop ();
        x = m_stack.Pop ();
      }
      catch (Exception ex) {
        log.ErrorFormat ("Divide: " +
                         "{0} in Pop",
                         ex);
        error = true;
        throw;
      }
      try {
        if (x is Position) {
          Position position = (Position)x;
          Position newPosition = new Position ();
          newPosition.X = position.X / (double)y;
          newPosition.Y = position.Y / (double)y;
          newPosition.Z = position.Z / (double)y;
          newPosition.A = position.A / (double)y;
          newPosition.B = position.B / (double)y;
          newPosition.C = position.C / (double)y;
          newPosition.U = position.U / (double)y;
          newPosition.V = position.V / (double)y;
          newPosition.W = position.W / (double)y;
          newPosition.Time = position.Time;
          result = newPosition;
        }
        else if (x is double) {
          if (y is double) {
            result = (double)x / (double)y;
          }
          else if (y is long) {
            result = (double)x / (long)y;
          }
          else if (y is int) {
            result = (double)x / (int)y;
          }
          else if (y is uint) {
            result = (double)x / (uint)y;
          }
          else {
            log.ErrorFormat ("Divide: " +
                             "not supported types for x={0} y={1}",
                             x, y);
            throw new ArgumentException ("Unsupported types");
          }
        }
        else if (x is long) {
          if (y is double) {
            result = (long)x / (double)y;
          }
          else if (y is long) {
            result = (long)x / (long)y;
          }
          else if (y is int) {
            result = (long)x / (int)y;
          }
          else if (y is uint) {
            result = (long)x / (uint)y;
          }
          else {
            log.ErrorFormat ("Divide: " +
                             "not supported types for x={0} y={1}",
                             x, y);
            throw new ArgumentException ("Unsupported types");
          }
        }
        else if (x is int) {
          if (y is double) {
            result = (int)x / (double)y;
          }
          else if (y is long) {
            result = (int)x / (long)y;
          }
          else if (y is int) {
            result = (int)x / (int)y;
          }
          else if (y is uint) {
            result = (int)x / (uint)y;
          }
          else {
            log.ErrorFormat ("Divide: " +
                             "not supported types for x={0} y={1}",
                             x, y);
            throw new ArgumentException ("Unsupported types");
          }
        }
        else if (x is uint) {
          if (y is double) {
            result = (uint)x / (double)y;
          }
          else if (y is long) {
            result = (uint)x / (long)y;
          }
          else if (y is int) {
            result = (uint)x / (int)y;
          }
          else if (y is uint) {
            result = (uint)x / (uint)y;
          }
          else {
            log.ErrorFormat ("Divide: " +
                             "not supported types for x={0} y={1}",
                             x, y);
            throw new ArgumentException ("Unsupported types");
          }
        }
        else {
          log.ErrorFormat ("Divide: " +
                           "not supported types for x={0} y={1}",
                           x, y);
          throw new ArgumentException ("Unsupported types");
        }
      }
      catch (Exception ex) {
        log.ErrorFormat ("Divide: " +
                         "exception {0} for x={1} y={2} typex={3} typey={4}",
                         ex, x, y, (null != x) ? x.GetType () : null, (null != y) ? y.GetType () : null);
        error = true;
        throw;
      }
      log.DebugFormat ("Divide: " +
                       "{0}/{1}={2}",
                       x, y, result);
      m_stack.Push (result);
      return result;
    }

    /// <summary>
    /// Remove the first element of the stack,
    /// divide this value with the double given in parameter
    /// and push the result in the stack
    /// </summary>
    /// <param name="param">double value</param>
    /// <returns>new value at the top of the stack</returns>
    public object DivideDouble (string param)
    {
      if (m_stack.Count < 1) {
        log.ErrorFormat ("DivideDouble: " +
                         "not enough elements in the stack");
        error = true;
        throw new Exception ("Not enough elements in stack");
      }
      double y = GetDouble (param);
      m_stack.Push (y);
      return Divide ("");
    }

    /// <summary>
    /// Remove the first element of the stack,
    /// return the absolute value
    /// </summary>
    /// <param name="param">unused</param>
    /// <returns>the absolute value of the first element of the stack</returns>
    public object AbsoluteDouble (string param)
    {
      if (m_stack.Count < 1) {
        log.ErrorFormat ("AbsoluteDouble: not enough elements in the stack");
        error = true;
        throw new Exception ("Not enough elements in stack");
      }
      return Math.Abs (Convert.ToDouble (m_stack.Pop ()));
    }

    /// <summary>
    /// Make the average all the elements of the stack
    /// </summary>
    /// <param name="param">unused</param>
    /// <returns>new value at the top of the stack</returns>
    public object Average (string param)
    {
      double sum = 0.0;
      int count = 0;
      while (true) {
        object x;
        try {
          x = m_stack.Pop ();
        }
        catch (InvalidOperationException) {
          break;
        }
        catch (Exception ex) {
          log.ErrorFormat ("Average: " +
                           "{0} in Pop",
                           ex);
          error = true;
          throw;
        }

        sum += Convert.ToDouble (x);
        ++count;
      }

      if (0 == count) {
        log.Info ("Average: " +
                  "no data in the stack to get a value");
        throw new InvalidOperationException ("No value to get an average");
      }
      else {
        double average = sum / count;
        log.DebugFormat ("Average: " +
                         "sum={0} count={1}",
                         sum, count);
        m_stack.Push (average);
        return average;
      }
    }

    /// <summary>
    /// Get the maximum value of all the elements of the stack
    /// </summary>
    /// <param name="param">unused</param>
    /// <returns>new value at the top of the stack</returns>
    public object Max (string param)
    {
      int count = 0;
      object max = null;
      while (true) {
        object x;
        try {
          x = m_stack.Pop ();
        }
        catch (InvalidOperationException) {
          break;
        }
        catch (Exception ex) {
          log.ErrorFormat ("Max: " +
                           "{0} in Pop",
                           ex);
          error = true;
          throw;
        }

        if ((null == max) || (Convert.ToDouble (max) < Convert.ToDouble (x))) {
          max = x;
        }
        ++count;
      }

      if (0 == count) {
        log.Info ("Max: " +
                  "no data in the stack to get a value");
        throw new InvalidOperationException ("No value to get an average");
      }
      else {
        log.DebugFormat ("Max: " +
                         "{0} count={1}",
                         max, count);
        m_stack.Push (max);
        return max;
      }
    }

    /// <summary>
    /// Get the minimum value of all the elements of the stack
    /// </summary>
    /// <param name="param">unused</param>
    /// <returns>new value at the top of the stack</returns>
    public object Min (string param)
    {
      int count = 0;
      object min = null;
      while (true) {
        object x;
        try {
          x = m_stack.Pop ();
        }
        catch (InvalidOperationException) {
          break;
        }
        catch (Exception ex) {
          log.ErrorFormat ("Min: " +
                           "{0} in Pop",
                           ex);
          error = true;
          throw;
        }

        if ((null == min) || (Convert.ToDouble (x) < Convert.ToDouble (min))) {
          min = x;
        }
        ++count;
      }

      if (0 == count) {
        log.Info ("Min: " +
                  "no data in the stack to get a value");
        throw new InvalidOperationException ("No value to get an average");
      }
      else {
        log.DebugFormat ("Min: " +
                         "{0} count={1}",
                         min, count);
        m_stack.Push (min);
        return min;
      }
    }
    #endregion // Number operations

    #region Object operations
    /// <summary>
    /// Remove the two first elements of the stack,
    /// check if the two elements are equals and push the result in the stack
    /// </summary>
    /// <param name="param">unused</param>
    /// <returns>new value at the top of the stack</returns>
    public object IsEqual (string param)
    {
      if (m_stack.Count < 2) {
        log.ErrorFormat ("IsEqual: " +
                         "not enough elements in the stack");
        error = true;
        throw new Exception ("Not enough elements in stack");
      }
      object x, y, result;
      try {
        y = m_stack.Pop ();
        x = m_stack.Pop ();
      }
      catch (Exception ex) {
        log.ErrorFormat ("IsEqual: " +
                         "{0} in Pop",
                         ex);
        error = true;
        throw;
      }
      try {
        result = object.Equals (x.ToString (), y.ToString ());
      }
      catch (Exception ex) {
        log.ErrorFormat ("IsEqual: " +
                         "exception {0} for x={0} y={1}",
                         ex, x, y);
        error = true;
        throw;
      }
      log.DebugFormat ("IsEqual: " +
                       "({0}=={1})={2}",
                       x, y, result);
      m_stack.Push (result);
      return result;
    }

    /// <summary>
    /// Remove the two first elements of the stack,
    /// if the first element is true then keep the second element
    /// else raise an exception
    /// </summary>
    /// <param name="param">unused</param>
    /// <returns>new value at the top of the stack</returns>
    public object If (string param)
    {
      if (m_stack.Count < 2) {
        log.ErrorFormat ("If: " +
                         "not enough elements in the stack");
        error = true;
        throw new Exception ("Not enough elements in stack");
      }
      object a, b;
      bool test = false;
      try {
        b = m_stack.Pop ();
        a = m_stack.Pop ();
      }
      catch (Exception ex) {
        log.ErrorFormat ("If: " +
                         "{0} in Pop",
                         ex);
        error = true;
        throw;
      }
      try {
        test = (bool)a;
      }
      catch (Exception ex) {
        log.ErrorFormat ("If: " +
                         "exception {0} for a={0} b={1}",
                         ex, a, b);
        error = true;
        throw;
      }
      if (test) {
        log.DebugFormat ("If: " +
                         "{0} => {1}",
                         a, b);
        m_stack.Push (b);
        return b;
      }
      else {
        // Raise an exception, not to set the value
        log.DebugFormat ("If: " +
                         "{0} is false, raise an exception", a);
        throw new Exception ("Condition is not true");
      }
    }

    /// <summary>
    /// Remove the three first elements of the stack,
    /// if the first element is true then keep the second element
    /// else keep the third element
    /// </summary>
    /// <param name="param">unused</param>
    /// <returns>new value at the top of the stack</returns>
    public object IfElse (string param)
    {
      if (m_stack.Count < 3) {
        log.ErrorFormat ("IfElse: " +
                         "not enough elements in the stack");
        error = true;
        throw new Exception ("Not enough elements in stack");
      }
      object a, b, c, result;
      try {
        c = m_stack.Pop ();
        b = m_stack.Pop ();
        a = m_stack.Pop ();
      }
      catch (Exception ex) {
        log.ErrorFormat ("IfElse: " +
                         "{0} in Pop",
                         ex);
        error = true;
        throw;
      }
      try {
        result = (bool)a ? b : c;
      }
      catch (Exception ex) {
        log.ErrorFormat ("IfElse: " +
                         "exception {0} for a={0} b={1} c={2}",
                         ex, a, b, c);
        error = true;
        throw;
      }
      log.DebugFormat ("IfElse: " +
                       "({0}?{1}:{2})={3}",
                       a, b, c, result);
      m_stack.Push (result);
      return result;
    }
    #endregion // Object operations

    #region Comparison operations
    /// <summary>
    /// Remove the two first elements of the stack,
    /// make the &gt; comparison between the two elements and push the result in the stack
    /// 
    /// The two elements must of type int, long or double
    /// </summary>
    /// <param name="param">unused</param>
    /// <returns>new value at the top of the stack</returns>
    public object Gt (string param)
    {
      if (m_stack.Count < 2) {
        log.ErrorFormat ("Gt: " +
                         "not enough elements in the stack");
        error = true;
        throw new Exception ("Not enough elements in stack");
      }
      object x, y, result;
      try {
        y = m_stack.Pop ();
        x = m_stack.Pop ();
      }
      catch (Exception ex) {
        log.ErrorFormat ("Gt: " +
                         "{0} in Pop",
                         ex);
        error = true;
        throw;
      }
      try {
        double x1 = double.Parse (x.ToString ());
        double y1 = double.Parse (y.ToString ());
        result = x1 > y1;
      }
      catch (Exception ex) {
        log.ErrorFormat ("Gt: " +
                         "exception {0} for x={0} y={1}",
                         ex, x, y);
        error = true;
        throw;
      }
      log.DebugFormat ("Gt: " +
                       "({0}>{1})={2}",
                       x, y, result);
      m_stack.Push (result);
      return result;
    }

    /// <summary>
    /// Remove the two first elements of the stack,
    /// make the &gt;= comparison between the two elements and push the result in the stack
    /// 
    /// The two elements must of type int, long or double
    /// </summary>
    /// <param name="param">unused</param>
    /// <returns>new value at the top of the stack</returns>
    public object Ge (string param)
    {
      if (m_stack.Count < 2) {
        log.ErrorFormat ("Ge: " +
                         "not enough elements in the stack");
        error = true;
        throw new Exception ("Not enough elements in stack");
      }
      object x, y, result;
      try {
        y = m_stack.Pop ();
        x = m_stack.Pop ();
      }
      catch (Exception ex) {
        log.ErrorFormat ("Ge: " +
                         "{0} in Pop",
                         ex);
        error = true;
        throw;
      }
      try {
        double x1 = double.Parse (x.ToString ());
        double y1 = double.Parse (y.ToString ());
        result = x1 >= y1;
      }
      catch (Exception ex) {
        log.ErrorFormat ("Ge: " +
                         "exception {0} for x={0} y={1}",
                         ex, x, y);
        error = true;
        throw;
      }
      log.DebugFormat ("Gt: " +
                       "({0}>={1})={2}",
                       x, y, result);
      m_stack.Push (result);
      return result;
    }

    /// <summary>
    /// Remove the two first elements of the stack,
    /// make the &lt; comparison between the two elements and push the result in the stack
    /// 
    /// The two elements must of type int, long or double
    /// </summary>
    /// <param name="param">unused</param>
    /// <returns>new value at the top of the stack</returns>
    public object Lt (string param)
    {
      if (m_stack.Count < 2) {
        log.ErrorFormat ("Lt: " +
                         "not enough elements in the stack");
        error = true;
        throw new Exception ("Not enough elements in stack");
      }
      object x, y, result;
      try {
        y = m_stack.Pop ();
        x = m_stack.Pop ();
      }
      catch (Exception ex) {
        log.ErrorFormat ("Lt: " +
                         "{0} in Pop",
                         ex);
        error = true;
        throw;
      }
      try {
        double x1 = double.Parse (x.ToString ());
        double y1 = double.Parse (y.ToString ());
        result = x1 < y1;
      }
      catch (Exception ex) {
        log.ErrorFormat ("Lt: " +
                         "exception {0} for x={0} y={1}",
                         ex, x, y);
        error = true;
        throw;
      }
      log.DebugFormat ("Lt: " +
                       "({0}<{1})={2}",
                       x, y, result);
      m_stack.Push (result);
      return result;
    }

    /// <summary>
    /// Remove the two first elements of the stack,
    /// make the &lt;= comparison between the two elements and push the result in the stack
    /// 
    /// The two elements must of type int, long or double
    /// </summary>
    /// <param name="param">unused</param>
    /// <returns>new value at the top of the stack</returns>
    public object Le (string param)
    {
      if (m_stack.Count < 2) {
        log.ErrorFormat ("Le: " +
                         "not enough elements in the stack");
        error = true;
        throw new Exception ("Not enough elements in stack");
      }
      object x, y, result;
      try {
        y = m_stack.Pop ();
        x = m_stack.Pop ();
      }
      catch (Exception ex) {
        log.ErrorFormat ("Le: " +
                         "{0} in Pop",
                         ex);
        error = true;
        throw;
      }
      try {
        double x1 = double.Parse (x.ToString ());
        double y1 = double.Parse (y.ToString ());
        result = x1 <= y1;
      }
      catch (Exception ex) {
        log.ErrorFormat ("Le: " +
                         "exception {0} for x={0} y={1}",
                         ex, x, y);
        error = true;
        throw;
      }
      log.DebugFormat ("Gt: " +
                       "({0}<={1})={2}",
                       x, y, result);
      m_stack.Push (result);
      return result;
    }
    #endregion // Comparison operations

    #region Boolean operations
    /// <summary>
    /// Return true if one element of the stack is true, else return false
    /// 
    /// If there is no element in the stack, raise an exception
    /// </summary>
    /// <param name="param">unused</param>
    /// <returns>new value at the top of the stack</returns>
    public object Any (string param)
    {
      if (0 == m_stack.Count) {
        if (log.IsDebugEnabled) {
          log.Debug ("Any: no element, return an exception");
        }
        throw new Exception ("Any: 0 elements");
      }

      bool result = false;
      while (true) {
        object x;
        try {
          x = m_stack.Pop ();
        }
        catch (InvalidOperationException) {
          break;
        }
        catch (Exception ex) {
          log.Error ("Any: exception in Pop", ex);
          error = true;
          throw;
        }

        if (!result && Convert.ToBoolean (x)) {
          result = true;
        }
      }

      log.Debug ($"Any: {result}");
      m_stack.Push (result);
      return result;
    }

    /// <summary>
    /// Return true if all the elements of the stack are true
    /// 
    /// If there is no element in the stack, raise an exception
    /// </summary>
    /// <param name="param">unused</param>
    /// <returns>new value at the top of the stack</returns>
    public object All (string param)
    {
      if (0 == m_stack.Count) {
        log.DebugFormat ("All: " +
                         "no element, return an exception");
        throw new Exception ("All: 0 elements");
      }

      bool isFalse = false;
      while (true) {
        object x;
        try {
          x = m_stack.Pop ();
        }
        catch (InvalidOperationException) {
          break;
        }
        catch (Exception ex) {
          log.Error ($"All: exception in Pop", ex);
          error = true;
          throw;
        }

        if (!isFalse && !Convert.ToBoolean (x)) {
          isFalse = true;
        }
      }

      bool result = !isFalse;
      log.DebugFormat ("All: " +
                       "{0}",
                       result);
      m_stack.Push (result);
      return result;
    }

    /// <summary>
    /// Return true if all the elements of the stack are false, else raise an exception
    /// 
    /// If there is no element in the stack, raise an exception
    /// </summary>
    /// <param name="param">unused</param>
    /// <returns>new value at the top of the stack</returns>
    public object None (string param)
    {
      if (0 == m_stack.Count) {
        log.DebugFormat ("None: " +
                         "no element, return an exception");
        throw new Exception ("None: 0 elements");
      }

      bool isTrue = false;
      while (true) {
        object x;
        try {
          x = m_stack.Pop ();
        }
        catch (InvalidOperationException) {
          break;
        }
        catch (Exception ex) {
          log.Error ("None: exception in Pop", ex);
          error = true;
          throw;
        }

        if (!isTrue && Convert.ToBoolean (x)) {
          isTrue = true;
        }
      }

      bool result = !isTrue;
      log.DebugFormat ("None: " +
                       "{0}",
                       result);
      m_stack.Push (result);
      return result;
    }

    /// <summary>
    /// Remove the two first elements of the stack,
    /// make a conjonction and push the result in the stack
    /// </summary>
    /// <param name="param">unused</param>
    /// <returns>new value at the top of the stack</returns>
    public object And (string param)
    {
      if (m_stack.Count < 2) {
        log.ErrorFormat ("And: " +
                         "not enough elements in the stack");
        error = true;
        throw new Exception ("Not enough elements in stack");
      }
      object x, y, result;
      try {
        y = m_stack.Pop ();
        x = m_stack.Pop ();
      }
      catch (Exception ex) {
        log.Error ("And: exception in Pop", ex);
        error = true;
        throw;
      }
      try {
        result = (bool)x && (bool)y;
      }
      catch (Exception ex) {
        log.Error ($"And: exception for x={x} y={y}", ex);
        error = true;
        throw;
      }
      log.DebugFormat ("And: " +
                       "{0}&&{1}={2}",
                       x, y, result);
      m_stack.Push (result);
      return result;
    }

    /// <summary>
    /// Remove the two first elements of the stack,
    /// make a disjunction and push the result in the stack
    /// </summary>
    /// <param name="param">unused</param>
    /// <returns>new value at the top of the stack</returns>
    public object Or (string param)
    {
      if (m_stack.Count < 2) {
        log.ErrorFormat ("Or: " +
                         "not enough elements in the stack");
        error = true;
        throw new Exception ("Not enough elements in stack");
      }
      object x, y, result;
      try {
        y = m_stack.Pop ();
        x = m_stack.Pop ();
      }
      catch (Exception ex) {
        log.Error ("Or: exception in Pop", ex);
        error = true;
        throw;
      }
      try {
        result = (bool)x || (bool)y;
      }
      catch (Exception ex) {
        log.Error ($"Or: exception for x={x} y={y}", ex);
        error = true;
        throw;
      }
      log.DebugFormat ("Or: " +
                       "{0}||{1}={2}",
                       x, y, result);
      m_stack.Push (result);
      return result;
    }

    /// <summary>
    /// Remove the top element of the stack,
    /// make a negation and push the result in the stack
    /// </summary>
    /// <param name="param">unused</param>
    /// <returns>new value at the top of the stack</returns>
    public object Negation (string param)
    {
      if (m_stack.Count < 1) {
        log.ErrorFormat ("Negation: " +
                         "not enough elements in the stack");
        error = true;
        throw new Exception ("Not enough elements in stack");
      }
      object x, result;
      try {
        x = m_stack.Pop ();
      }
      catch (Exception ex) {
        log.ErrorFormat ("Negation: " +
                         "{0} in Pop",
                         ex);
        error = true;
        throw;
      }
      try {
        result = !((bool)x);
      }
      catch (Exception ex) {
        log.Error ($"Negation: exception for x={x}", ex);
        error = true;
        throw;
      }
      log.DebugFormat ("Negation: " +
                       "!{0}={1}",
                       x, result);
      m_stack.Push (result);
      return result;
    }

    /// <summary>
    /// Remove the top element of the stack,
    /// check if this element can be found inparam
    /// and push the result in the stack
    /// </summary>
    /// <param name="param">enumeration of elements. The first character is the separator between the different elements.</param>
    /// <returns>true if the top element of the stack has been found in the list of elements, else false</returns>
    public object In (string param)
    {
      if (m_stack.Count < 1) {
        log.ErrorFormat ("In: " +
                         "not enough elements in the stack");
        error = true;
        throw new Exception ("Not enough elements in stack");
      }
      object x;
      bool result = false;
      try {
        x = m_stack.Pop ();
      }
      catch (Exception ex) {
        log.ErrorFormat ("In: " +
                         "{0} in Pop",
                         ex);
        error = true;
        throw;
      }
      try {
        string[] elements = param.Split (param[0]);
        for (int i = 1; i < elements.Length; ++i) {
          if ((null != x) && elements[i].Equals (x.ToString ())) {
            log.DebugFormat ("In: " +
                             "{0} found in the list of elements",
                             x);
            result = true;
            break;
          }
        }
      }
      catch (Exception ex) {
        log.ErrorFormat ("In: " +
                         "exception {0} for x={1}",
                         ex, x);
        error = true;
        throw;
      }
      log.DebugFormat ("In: " +
                       "{0} in {1}={2}",
                       x, param, result);
      m_stack.Push (result);
      return result;
    }

    /// <summary>
    /// Remove the top element of the stack,
    /// check if this element can be found inparam
    /// and push the result in the stack
    /// 
    /// If not found, an exception is raised
    /// </summary>
    /// <param name="param">enumeration of elements. The first character is the separator between the different elements.</param>
    /// <returns>true if the top element of the stack has been found in the list of elements, else an exception is raised</returns>
    public object InOnly (string param)
    {
      if (m_stack.Count < 1) {
        log.ErrorFormat ("InOnly: " +
                         "not enough elements in the stack");
        error = true;
        throw new Exception ("Not enough elements in stack");
      }
      object x;
      try {
        x = m_stack.Pop ();
      }
      catch (Exception ex) {
        log.ErrorFormat ("InOnly: " +
                         "{0} in Pop",
                         ex);
        error = true;
        throw;
      }
      try {
        string[] elements = param.Split (param[0]);
        for (int i = 1; i < elements.Length; ++i) {
          if ((null != x) && elements[i].Equals (x.ToString ())) {
            log.DebugFormat ("InOnly: " +
                             "{0} found in the list of elements",
                             x);
            m_stack.Push (true);
            return true;
          }
        }
      }
      catch (Exception ex) {
        log.ErrorFormat ("InOnly: " +
                         "exception {0} for x={1}",
                         ex, x);
        error = true;
        throw;
      }
      log.DebugFormat ("InOnly: " +
                       "{0} not in {1}",
                       x, param);
      throw new Exception ("InOnly: not found");
    }
    #endregion // Boolean operations

    #region String operations
    /// <summary>
    /// Check if a string is null or empty
    /// </summary>
    /// <param name="param"></param>
    /// <returns></returns>
    public bool IsStringNullOrEmpty (string param)
    {
      if (m_stack.Count < 1) {
        log.Error ("IsStringNullOrEmpty: not enough elements in the stack");
        error = true;
        throw new Exception ("Not enough elements in stack");
      }
      object x;
      bool result;
      try {
        x = m_stack.Pop ();
      }
      catch (Exception ex) {
        log.Error ("IsStringNullOrEmpty: exception in Pop", ex);
        error = true;
        throw;
      }
      try {
        result = string.IsNullOrEmpty (x.ToString ());
      }
      catch (Exception ex) {
        log.Error ($"IsStringNullOrEmpty: exception for x={x}", ex);
        error = true;
        throw;
      }
      if (log.IsDebugEnabled) {
        log.Debug ($"IsStringNullOrEmpty: {x} => {result}");
      }
      m_stack.Push (result);
      return result;
    }

    /// <summary>
    /// Join all the items in the stack
    /// </summary>
    /// <param name="param">separator to use. Default is ,</param>
    /// <returns></returns>
    public string Join (string param)
    {
      if (log.IsDebugEnabled) {
        log.Debug ($"Join: param={param}");
      }
      string separator = (null != param) ? param : ",";
      try {
        string[] items = new string[m_stack.Count];
        int i = m_stack.Count - 1;
        foreach (object item in m_stack) {
          items[i--] = item.ToString ();
        }
        return string.Join (separator, items);
      }
      catch (Exception ex) {
        log.Error ($"Join: exception", ex);
        error = true;
        throw;
      }
    }

    /// <summary>
    /// Check if the stack element 2 is in the list found in stack element 1
    /// </summary>
    /// <param name="param">Used separator in list</param>
    /// <returns></returns>
    public bool InList (string param)
    {
      log.DebugFormat ("InList: " +
                       "param={0}",
                       param);
      if (m_stack.Count < 2) {
        log.Error ($"InList: not enough elements in the stack");
        error = true;
        throw new Exception ("Not enough elements in stack");
      }
      string separator = param ?? ",";
      bool result = false;
      object list;
      try {
        list = m_stack.Pop ();
      }
      catch (Exception ex) {
        log.Error ($"InList: exception in Pop list", ex);
        error = true;
        throw;
      }
      object x;
      try {
        x = m_stack.Pop ();
      }
      catch (Exception ex) {
        log.Error ($"InList: exception in Pop x", ex);
        error = true;
        throw;
      }
      try {
        string[] elements = ((string)list).Split (new string[] { separator }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string element in elements) {
          if ((null != x) && element.Equals (x.ToString ())) {
            if (log.IsDebugEnabled) {
              log.Debug ($"InList: {x} found in the list of elements");
            }
            result = true;
            break;
          }
        }
      }
      catch (Exception ex) {
        log.Error ($"InList: exception for x={x}", ex);
        error = true;
        throw;
      }
      if (log.IsDebugEnabled) {
        log.Debug ($"InList: {x} in {param}={result}");
      }
      m_stack.Push (result);
      return result;
    }

    /// <summary>
    /// Remove the top element of the stack,
    /// trim this element at start with the characters in param
    /// and push the result in the stack
    /// </summary>
    /// <param name="param">characters to trim</param>
    /// <returns>trimmed string</returns>
    public object TrimStart (string param)
    {
      if (m_stack.Count < 1) {
        log.ErrorFormat ("TrimStart: " +
                         "not enough elements in the stack");
        error = true;
        throw new Exception ("Not enough elements in stack");
      }
      object x;
      string result;
      try {
        x = m_stack.Pop ();
      }
      catch (Exception ex) {
        log.ErrorFormat ("TrimStart: " +
                         "{0} in Pop",
                         ex);
        error = true;
        throw;
      }
      try {
        result = x.ToString ().TrimStart (param.ToCharArray ());
      }
      catch (Exception ex) {
        log.ErrorFormat ("TrimStart: " +
                         "exception {0} for x={1}",
                         ex, x);
        error = true;
        throw;
      }
      log.DebugFormat ("TrimStart: " +
                       "{0} in {1}={2}",
                       x, param, result);
      m_stack.Push (result);
      return result;
    }

    /// <summary>
    /// Remove the top element of the stack,
    /// trim this element at end with the characters in param
    /// and push the result in the stack
    /// </summary>
    /// <param name="param">characters to trim</param>
    /// <returns>trimmed string</returns>
    public object TrimEnd (string param)
    {
      if (m_stack.Count < 1) {
        log.ErrorFormat ("TrimEnd: " +
                         "not enough elements in the stack");
        error = true;
        throw new Exception ("Not enough elements in stack");
      }
      object x;
      string result;
      try {
        x = m_stack.Pop ();
      }
      catch (Exception ex) {
        log.ErrorFormat ("TrimEnd: " +
                         "{0} in Pop",
                         ex);
        error = true;
        throw;
      }
      try {
        result = x.ToString ().TrimEnd (param.ToCharArray ());
      }
      catch (Exception ex) {
        log.ErrorFormat ("TrimEnd: " +
                         "exception {0} for x={1}",
                         ex, x);
        error = true;
        throw;
      }
      log.DebugFormat ("TrimEnd: " +
                       "{0} in {1}={2}",
                       x, param, result);
      m_stack.Push (result);
      return result;
    }

    /// <summary>
    /// Remove the top element of the stack,
    /// trim this element starting with the firs ocurence of characters in param
    /// and push the result in the stack
    /// E.g  "P201953.MCP" with param="." gives "P201953"
    /// </summary>
    /// <param name="param">characters to trim</param>
    /// <returns>trimmed string</returns>
    public object TrimFromChar (string param)
    {
      if (m_stack.Count < 1) {
        log.ErrorFormat ("TrimFromChar: " +
                         "not enough elements in the stack");
        error = true;
        throw new Exception ("Not enough elements in stack");
      }
      object x;
      string result;
      try {
        x = m_stack.Pop ();
      }
      catch (Exception ex) {
        log.ErrorFormat ("TrimFromChar: " +
                         "{0} in Pop",
                         ex);
        error = true;
        throw;
      }
      try {
        result = x.ToString ().Split (param.ToCharArray ())[0];
      }
      catch (Exception ex) {
        log.ErrorFormat ("TrimFromChar: " +
                         "exception {0} for x={1}",
                         ex, x);
        error = true;
        throw;
      }
      log.DebugFormat ("TrimFromChar: " +
                       "{0} in {1}={2}",
                       x, param, result);
      m_stack.Push (result);
      return result;
    }

    /// <summary>
    /// Remove the top element of the stack,
    /// trim this element from start until the last ocurence of characters in param
    /// and push the result in the stack
    /// E.g  "P201953.MCP" with param="." gives ".NCP"
    /// </summary>
    /// <param name="param">characters to trim</param>
    /// <returns>trimmed string</returns>
    public object TrimStartToChar (string param)
    {
      if (m_stack.Count < 1) {
        log.ErrorFormat ("TrimStartToChar: " +
                         "not enough elements in the stack");
        error = true;
        throw new Exception ("Not enough elements in stack");
      }
      object x;
      string result;
      try {
        x = m_stack.Pop ();
      }
      catch (Exception ex) {
        log.ErrorFormat ("TrimStartToChar: " +
                         "{0} in Pop",
                         ex);
        error = true;
        throw;
      }
      try {
        // if no occurence of param, return string
        int lastPosition = x.ToString ().LastIndexOfAny (param.ToCharArray ());
        if (-1 == lastPosition) {
          result = x.ToString ();
        }
        else {
          result = x.ToString ().Substring (lastPosition + 1);
        }
      }
      catch (Exception ex) {
        log.ErrorFormat ("TrimStartToChar: " +
                         "exception {0} for x={1}",
                         ex, x);
        error = true;
        throw;
      }
      log.DebugFormat ("TrimStartToChar: " +
                       "{0} in {1}={2}",
                       x, param, result);
      m_stack.Push (result);
      return result;
    }

    /// <summary>
    /// Remove the top element of the stack,
    /// trim this element with the characters in param
    /// and push the result in the stack
    /// </summary>
    /// <param name="param">characters to trim</param>
    /// <returns>trimmed string</returns>
    public object Trim (string param)
    {
      if (m_stack.Count < 1) {
        log.ErrorFormat ("Trim: " +
                         "not enough elements in the stack");
        error = true;
        throw new Exception ("Not enough elements in stack");
      }
      object x;
      string result;
      try {
        x = m_stack.Pop ();
      }
      catch (Exception ex) {
        log.ErrorFormat ("Trim: " +
                         "{0} in Pop",
                         ex);
        error = true;
        throw;
      }
      try {
        result = x.ToString ().Trim (param.ToCharArray ());
      }
      catch (Exception ex) {
        log.ErrorFormat ("Trim: " +
                         "exception {0} for x={1}",
                         ex, x);
        error = true;
        throw;
      }
      log.DebugFormat ("Trim: " +
                       "{0} in {1}={2}",
                       x, param, result);
      m_stack.Push (result);
      return result;
    }

    /// <summary>
    /// Check if the MCode in element 2 is in the string found in stack element 1.
    /// MCode in string should not be followed directly by other digits
    /// E.G searching M60, string "M60T23" is ok. "M601" is not ok
    /// </summary>
    /// <param name="param">Used separator in list</param>
    /// <returns></returns>
    public bool ContainsExactString (string param)
    {
      log.Debug ($"ContainsExactString: param={param}");
      if (m_stack.Count < 2) {
        log.Error ($"ContainsExactString: not enough elements in the stack");
        error = true;
        throw new Exception ("Not enough elements in stack");
      }
      bool result = false;
      string mCode = null;
      string sourceString = null;

      object param1;

      try {
        param1 = m_stack.Pop ();
      }
      catch (Exception ex) {
        log.Error ($"ContainsExactString: exception in Pop list", ex);
        error = true;
        throw;
      }
      object param2;
      try {
        param2 = m_stack.Pop ();
      }
      catch (Exception ex) {
        log.Error ($"ContainsExactString: exception in Pop list", ex);
        error = true;
        throw;
      }

      mCode = param1.ToString ();
      sourceString = param2.ToString ().Trim();

      if (sourceString.EndsWith (mCode)) {
        result = true;
      }
      else {
        int mCodeIndex = 0;
        int searchIndex = 0;
        do {
          // find first occurence matching condition
          mCodeIndex = sourceString.IndexOf (mCode, searchIndex);
          log.Debug ($"ContainsExactString: index={mCodeIndex}");
          if (-1 == mCodeIndex) {
            break;
          }
          else {
            if (!Char.IsDigit (sourceString[mCodeIndex + mCode.Length])) {
              result = true;
              break;
            }
          }
          searchIndex = mCodeIndex + mCode.Length;
        }
        while (searchIndex < sourceString.Length - mCode.Length);
      }
      

      if (log.IsDebugEnabled) {
        log.Debug ($"ContainsExactString: {mCode} in {sourceString}={result}");
      }
      m_stack.Push (result);
      return result;
    }

    /// <summary>
    /// Remove the two first elements of the stack,
    /// Split first with separator in param
    /// Get the item at second element position
    /// push the result in the stack
    /// </summary>
    /// <param name="param">separator to use. Default is ,</param>
    /// <returns></returns>
    public string ElementAt (string param)
    {
      if (log.IsDebugEnabled) {
        log.Debug ($"ElementAt: param={param}");
      }
      if (m_stack.Count < 2) {
        log.ErrorFormat ("Add: " +
                         "not enough elements in the stack");
        error = true;
        throw new Exception ("Not enough elements in stack");
      }

      object x, index;
      string result;
      try {
        index = m_stack.Pop ();
        x = m_stack.Pop ();
      }
      catch (Exception ex) {
        log.ErrorFormat ("Add: " +
                         "{0} in Pop",
                         ex);
        error = true;
        throw;
      }

      try {
        string[] items = x.ToString ().Split (param.ToCharArray ());
        log.Info($"length={items.Length}");
        if (items.Length > (int)index) {
          result = items[(int)index];
        }
        else {
          error = true;
          throw new Exception ("not enough items");
        }
      }
      catch (Exception ex) {
        log.Error ($"ElementAt: exception", ex);
        error = true;
        throw;
      }
      m_stack.Push (result);
      return result;
    }


    #endregion // String operations

    #region Conversions
    /// <summary>
    /// Convert from inches to millimeters the top element of the stack
    /// </summary>
    /// <param name="param">unused</param>
    /// <returns></returns>
    public object ConvertToMillimeters (string param)
    {
      if (m_stack.Count < 1) {
        log.ErrorFormat ("ConvertToMillimeters: " +
                         "not enough elements in the stack");
        error = true;
        throw new Exception ("Not enough elements in stack");
      }
      object x;
      try {
        x = m_stack.Pop ();
      }
      catch (Exception ex) {
        log.ErrorFormat ("ConvertToMillimeters: " +
                         "{0} in Pop",
                         ex);
        error = true;
        throw;
      }
      if (x is Position) {
        Position position = (Position)x;
        Position newPosition = new Position ();
        newPosition.X = Lemoine.Conversion.Converter.ConvertToMetric (position.X, false);
        newPosition.Y = Lemoine.Conversion.Converter.ConvertToMetric (position.Y, false);
        newPosition.Z = Lemoine.Conversion.Converter.ConvertToMetric (position.Z, false);
        newPosition.A = position.A;
        newPosition.B = position.B;
        newPosition.C = position.C;
        newPosition.U = position.U;
        newPosition.V = position.V;
        newPosition.W = position.W;
        newPosition.Time = position.Time;
        m_stack.Push (newPosition);
        return newPosition;
      }
      else {
        double result = Lemoine.Conversion.Converter.ConvertToMetric ((double)x, false);
        m_stack.Push (result);
        return result;
      }
    }

    /// <summary>
    /// Convert from millimeters to inches the top element of the stack
    /// </summary>
    /// <param name="param">unused</param>
    /// <returns></returns>
    public object ConvertToInches (string param)
    {
      if (m_stack.Count < 1) {
        log.ErrorFormat ("ConvertToInches: " +
                         "not enough elements in the stack");
        error = true;
        throw new Exception ("Not enough elements in stack");
      }
      object x;
      try {
        x = m_stack.Pop ();
      }
      catch (Exception ex) {
        log.ErrorFormat ("ConvertToInches: " +
                         "{0} in Pop",
                         ex);
        error = true;
        throw;
      }
      if (x is Position) {
        Position position = (Position)x;
        Position newPosition = new Position ();
        newPosition.X = Lemoine.Conversion.Converter.ConvertToInches (position.X);
        newPosition.Y = Lemoine.Conversion.Converter.ConvertToInches (position.Y);
        newPosition.Z = Lemoine.Conversion.Converter.ConvertToInches (position.Z);
        newPosition.A = position.A;
        newPosition.B = position.B;
        newPosition.C = position.C;
        newPosition.U = position.U;
        newPosition.V = position.V;
        newPosition.W = position.W;
        newPosition.Time = position.Time;
        m_stack.Push (newPosition);
        return newPosition;
      }
      else {
        double result = Lemoine.Conversion.Converter.ConvertToInches ((double)x);
        m_stack.Push (result);
        return result;
      }
    }

    /// <summary>
    /// Get a bit specific from the top element of the stack
    /// </summary>
    /// <param name="param">bit number</param>
    /// <returns></returns>
    public object GetBit (string param)
    {
      if (m_stack.Count < 1) {
        log.ErrorFormat ("GetBit: " +
                         "not enough elements in the stack");
        error = true;
        throw new Exception ("Not enough elements in stack");
      }
      object x;
      try {
        x = m_stack.Pop ();
      }
      catch (Exception ex) {
        log.ErrorFormat ("GetBit: " +
                         "{0} in Pop",
                         ex);
        error = true;
        throw;
      }

      int bitNumber;
      try {
        bitNumber = int.Parse (param);
      }
      catch (Exception ex) {
        log.Error ($"GetBit: param {param} is not a valid number", ex);
        throw;
      }
      if (x is byte) {
        byte b = (byte)x;
        return (b & (1 << bitNumber)) != 0;
      }
      else if (x is int) {
        int b = (int)x;
        return (b & (1 << bitNumber)) != 0;
      }
      else if (x is long) {
        long b = (long)x;
        return (b & (1 << bitNumber)) != 0;
      }
      else {
        long b = Convert.ToInt64 (x);
        return (b & (1 << bitNumber)) != 0;
      }
    }
    #endregion // Conversions

    #region Utilities (Position...)
    /// <summary>
    /// Remove the three first elements of the stack,
    /// create a position from them
    /// and push the result in the stack
    /// </summary>
    /// <param name="param">unused</param>
    /// <returns>new value at the top of the stack</returns>
    public object PositionXYZ (string param)
    {
      if (m_stack.Count < 3) {
        log.ErrorFormat ("PositionXYZ: " +
                         "not enough elements in the stack");
        error = true;
        throw new Exception ("Not enough elements in stack");
      }
      object x, y, z;
      Position position;
      try {
        z = m_stack.Pop ();
        y = m_stack.Pop ();
        x = m_stack.Pop ();
      }
      catch (Exception ex) {
        log.ErrorFormat ("PositionXYZ: " +
                         "{0} in Pop",
                         ex);
        error = true;
        throw;
      }
      try {
        position = new Position ((double)x, (double)y, (double)z);
      }
      catch (Exception ex) {
        log.ErrorFormat ("PositionXYZ: " +
                         "exception {0} for x={1} y={2} z={3}",
                         ex, x, y, z);
        error = true;
        throw;
      }
      log.DebugFormat ("PositionXYZ: " +
                       "got {0}",
                       position);
      m_stack.Push (position);
      return position;
    }

    /// <summary>
    /// Remove the five first elements of the stack,
    /// create a position from them
    /// and push the result in the stack
    /// </summary>
    /// <param name="param">unused</param>
    /// <returns>new value at the top of the stack</returns>
    public object PositionXYZAB (string param)
    {
      if (m_stack.Count < 6) {
        log.Error ("PositionXYZAB: not enough elements in the stack");
        error = true;
        throw new Exception ("Not enough elements in stack");
      }
      object x, y, z, a, b;
      Position position;
      try {
        b = m_stack.Pop ();
        a = m_stack.Pop ();
        z = m_stack.Pop ();
        y = m_stack.Pop ();
        x = m_stack.Pop ();
      }
      catch (Exception ex) {
        log.Error ($"PositionXYZAB: error in Pop", ex);
        error = true;
        throw;
      }
      try {
        position = new Position ((double)x, (double)y, (double)z);
        position.A = (double)a;
        position.B = (double)b;
      }
      catch (Exception ex) {
        log.Error ($"PositionXYZAB: exception for x={x} y={y} z={z} a={a} b={b}", ex);
        error = true;
        throw;
      }
      if (log.IsDebugEnabled) {
        log.Debug ($"PositionXYZAB: got {position}");
      }
      m_stack.Push (position);
      return position;
    }

    /// <summary>
    /// Remove the six first elements of the stack,
    /// create a position from them
    /// and push the result in the stack
    /// </summary>
    /// <param name="param">unused</param>
    /// <returns>new value at the top of the stack</returns>
    public object PositionXYZABC (string param)
    {
      if (m_stack.Count < 6) {
        log.Error ("PositionXYZABC: not enough elements in the stack");
        error = true;
        throw new Exception ("Not enough elements in stack");
      }
      object x, y, z, a, b, c;
      Position position;
      try {
        c = m_stack.Pop ();
        b = m_stack.Pop ();
        a = m_stack.Pop ();
        z = m_stack.Pop ();
        y = m_stack.Pop ();
        x = m_stack.Pop ();
      }
      catch (Exception ex) {
        log.Error ("PositionXYZABC: exception in Pop", ex);
        error = true;
        throw;
      }
      try {
        position = new Position ((double)x, (double)y, (double)z);
        position.A = (double)a;
        position.B = (double)b;
        position.C = (double)c;
      }
      catch (Exception ex) {
        log.Error ($"PositionXYZABC: exception for x={x} y={y} z={z} a={a} b={b} c={c}", ex);
        error = true;
        throw;
      }
      if (log.IsDebugEnabled) {
        log.Debug ($"PositionXYZABC: got {position}");
      }
      m_stack.Push (position);
      return position;
    }

    /// <summary>
    /// Remove the nice first elements of the stack,
    /// create a position from them
    /// and push the result in the stack
    /// </summary>
    /// <param name="param">unused</param>
    /// <returns>new value at the top of the stack</returns>
    public object PositionXYZUVWABC (string param)
    {
      if (m_stack.Count < 6) {
        log.ErrorFormat ("PositionXYZUVWABC: " +
                         "not enough elements in the stack");
        error = true;
        throw new Exception ("Not enough elements in stack");
      }
      object x, y, z, u, v, w, a, b, c;
      Position position;
      try {
        c = m_stack.Pop ();
        b = m_stack.Pop ();
        a = m_stack.Pop ();
        w = m_stack.Pop ();
        v = m_stack.Pop ();
        u = m_stack.Pop ();
        z = m_stack.Pop ();
        y = m_stack.Pop ();
        x = m_stack.Pop ();
      }
      catch (Exception ex) {
        log.ErrorFormat ("PositionXYZUVWABC: " +
                         "{0} in Pop",
                         ex);
        error = true;
        throw;
      }
      try {
        position = new Position ((double)x, (double)y, (double)z);
        position.U = (double)u;
        position.V = (double)v;
        position.W = (double)w;
        position.A = (double)a;
        position.B = (double)b;
        position.C = (double)c;
      }
      catch (Exception ex) {
        log.ErrorFormat ("PositionXYZUVWABC: " +
                         "exception {0} for x={1} y={2} z={3} " +
                         "a={4} b={5} c={6}",
                         ex, x, y, z, a, b, c);
        error = true;
        throw;
      }
      log.DebugFormat ("PositionXYZUVWABC: " +
                       "got {0}",
                       position);
      m_stack.Push (position);
      return position;
    }

    /// <summary>
    /// Convert the stack content into a list
    /// </summary>
    /// <param name="param"></param>
    /// <returns></returns>
    public object ToList (string param)
    {
      IList result = new List<object> ();

      while (0 < m_stack.Count) {
        result.Insert (0, m_stack.Pop ());
      }

      return result;
    }

    /// <summary>
    /// Return a list comprising all the elements from different lists
    /// </summary>
    /// <param name="param">Unused param</param>
    /// <returns></returns>
    public object MergeList (string param)
    {
      // The first list is taken
      var listToMerge = m_stack.Pop () as IList;
      if (listToMerge == null) {
        log.ErrorFormat ("MergeList: {0} is not a list", listToMerge);
        return null;
      }

      // Merge lists
      var mergedList = (IList)Activator.CreateInstance (listToMerge.GetType ());
      string countedElements = listToMerge.Count.ToString ();
      do {
        try {
          foreach (var element in listToMerge) {
            mergedList.Add (element);
          }
        }
        catch (Exception e) {
          log.ErrorFormat ("Couldn't merge {0} and {1}: {2}", mergedList, listToMerge, e);
          countedElements += " (invalid)";
        }

        // Next list
        listToMerge = null;
        if (m_stack.Count > 0) {
          listToMerge = m_stack.Pop () as IList;
          if (listToMerge == null) {
            log.ErrorFormat ("MergeList: {0} is not a list", listToMerge);
          }
          else {
            countedElements += "+" + listToMerge.Count;
          }
        }
      } while (listToMerge != null);

      log.InfoFormat ("MergeList: returned list has {0} elements ({1})",
                     mergedList.Count, countedElements);
      return mergedList;
    }

    /// <summary>
    /// Remove the three first elements of the stack being the speed X, Y, Z
    /// compute the overall speed
    /// </summary>
    /// <param name="param">unused</param>
    /// <returns>new value at the top of the stack</returns>
    public object SpeedFromXYZ (string param)
    {
      if (m_stack.Count < 3) {
        log.ErrorFormat ("SpeedFromXYZ: not enough elements in the stack");
        error = true;
        throw new Exception ("Not enough elements in stack");
      }
      object x, y, z;
      double speed = 0;
      try {
        z = m_stack.Pop ();
        y = m_stack.Pop ();
        x = m_stack.Pop ();
      }
      catch (Exception ex) {
        log.ErrorFormat ("SpeedFromXYZ: {0} in Pop", ex);
        error = true;
        throw;
      }
      try {
        speed = Math.Sqrt ((double)x * (double)x + (double)y * (double)y + (double)z * (double)z);
      }
      catch (Exception ex) {
        log.ErrorFormat ("SpeedFromXYZ: exception {0} for x={1} y={2} z={3}", ex, x, y, z);
        error = true;
        throw;
      }
      log.DebugFormat ("SpeedFromXYZ: got {0}", speed);
      m_stack.Push (speed);
      return speed;
    }
    #endregion // Utilities
    #endregion // Methods
  }
}
