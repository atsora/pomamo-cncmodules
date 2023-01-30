// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

#if NETSTANDARD || NET48 || NETCOREAPP

using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Description of Lambda.
  /// </summary>
  public sealed class Lambda : Lemoine.Cnc.BaseCncModule, Lemoine.Cnc.ICncModule, IDisposable
  {
    #region Members
    readonly Stack m_stack = new Stack ();
    bool m_error = false;
    readonly ScriptOptions m_scriptOptions;
    readonly IDictionary<string, object> m_lambdas = new Dictionary<string, object> ();
    readonly Regex m_lambdaRegex =
      new Regex (@"(?'func'new +Func<(\w+,\s*)*\w+>\s*\()?\s*(?'pbracket'\()?(?'params'(\(\)|\w+|\((\w+\s*,\s*)*\w+\)))\s*(?'-pbracket'\))?\s*=>\s*(?'evaluation'.*)\s*(?'-func'\))?",
                 RegexOptions.Compiled | RegexOptions.CultureInvariant);
    readonly Regex m_genericStructureRegex =
      new Regex (@"(?'maintype'.*)(?'open'`\d\[)(?'types'(\[(?'type1'[^][,]*)(,[^][]*)?\]*,)*\[(?'type2'[^][,]*)(,[^][]*)?\])(?'close'\])",
                 RegexOptions.Compiled | RegexOptions.CultureInvariant);
    readonly Regex m_lambdaWithTypesRegex =
      new Regex (@"<(?'types'[^>]*)>(?'lambda'.*)",
                 RegexOptions.Compiled | RegexOptions.CultureInvariant);
    readonly Regex m_funcRegex =
      new Regex (@"^\s*new Func<((?'types'.+),)?\s*object>\s*\((?'arguments'.*)\)\s*$",
        RegexOptions.Compiled);
    #endregion // Members

    #region Getters / Setters
    /// <summary>
    /// An error occurred
    /// </summary>
    public bool Error
    {
      get { return m_error; }
    }
    #endregion // Getters / Setters

    #region Constructors / Destructor / ToString methods
    /// <summary>
    /// Description of the constructor
    /// </summary>
    public Lambda ()
      : base ("Lemoine.Cnc.InOut.Lambda")
    {
      m_scriptOptions = ScriptOptions.Default
        .AddReferences (new System.Reflection.Assembly[] {
          typeof (Lambda).Assembly,
          typeof (Convert).Assembly,
          typeof (System.Nullable).Assembly
        })
        .AddImports (new string[] {
          "System",
          "System.Linq",
          "System.Collections",
          "System.Collections.Generic"
        });
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
      m_error = false;
    }

    /// <summary>
    /// Push a data in the stack
    /// </summary>
    /// <param name="data">Data to push in the stack</param>
    public void Push (object data)
    {
      log.DebugFormat ("Push: " +
                       "push {0} ({1}) in the stack",
                       data, data.GetType ());
      m_stack.Push (data);
    }

    /// <summary>
    /// Remove all the necessary parameters of the lambda expression from the stack,
    /// run the lambda expression
    /// and push the result in the stack
    /// 
    /// Default method where the argumenets of the lambda expression are of type object
    /// </summary>
    /// <param name="lambdaExpression">lambda expresion</param>
    /// <returns>new value at the top of the stack</returns>
    public object Run (string lambdaExpression)
    {
      try {
        return RunGeneric<object, object, object, object> (lambdaExpression);
      }
      catch (Exception ex) {
        log.Error ($"Run: exception in {lambdaExpression}", ex);
        throw;
      }
    }

    /// <summary>
    /// Remove all the necessary parameters of the lambda expression from the stack,
    /// run the lambda expression
    /// and push the result in the stack
    /// 
    /// Note this does not work with multiple generic types for the moment (only a single one)
    /// </summary>
    /// <param name="lambdaWithType">lambda expresion which is preceded by &lt;type1,type2&gt;</param>
    /// <returns>new value at the top of the stack</returns>
    public object RunWithType (string lambdaWithType)
    {
      try {
        var match = m_lambdaWithTypesRegex.Match (lambdaWithType);
        Debug.Assert (match.Groups["types"].Success);
        Debug.Assert (match.Groups["lambda"].Success);
        var typesString = match.Groups["types"].Value;
        var lambdaExpression = match.Groups["lambda"].Value;
        typesString = typesString
          .Replace ("int", "System.Int32")
          .Replace ("double", "System.Double")
          .Replace ("long", "System.Int64")
          .Replace ("string", "System.String");

        Type[] types;
        if (typesString.Contains ("`")) { // Generic type, consider only one type        
          types = new Type[] { Type.GetType (typesString) };
        }
        else {
          types = typesString
            .Split (new char[] { ',' })
            .Select (s => Type.GetType (s))
            .ToArray ();
        }

        var method = this.GetType ().GetMethod ("Run" + types.Length,
                                                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Debug.Assert (null != method);
        var generic = method.MakeGenericMethod (types);
        Debug.Assert (null != generic);

        return generic.Invoke (this, new Object[] { lambdaExpression });
      }
      catch (Exception ex) {
        log.Error ($"RunWithType: exception", ex);
        throw;
      }
    }

    object Run1<T> (string lambdaExpression)
    {
      return RunGeneric<T> (lambdaExpression);
    }

    object Run2<T, U> (string lambdaExpression)
    {
      return RunGeneric<T, U> (lambdaExpression);
    }

    object Run3<T, U, V> (string lambdaExpression)
    {
      return RunGeneric<T, U, V> (lambdaExpression);
    }

    object Run4<T, U, V, W> (string lambdaExpression)
    {
      return RunGeneric<T, U, V, W> (lambdaExpression);
    }

    /// <summary>
    /// Remove all the necessary parameters of the lambda expression from the stack,
    /// run the lambda expression
    /// and push the result in the stack
    /// 
    /// T is the type of all the arguments of the lamba expression
    /// </summary>
    /// <param name="lambdaExpression">lambda expresion</param>
    /// <returns>new value at the top of the stack</returns>
    public object RunGeneric<T> (string lambdaExpression)
    {
      return RunGeneric<T, T, T, T> (lambdaExpression);
    }

    /// <summary>
    /// Remove all the necessary parameters of the lambda expression from the stack,
    /// run the lambda expression
    /// and push the result in the stack
    /// 
    /// T is the type of the first argument of the lamba expression
    /// U is the type of the second argument of the lamba expression
    /// </summary>
    /// <param name="lambdaExpression">lambda expresion</param>
    /// <returns>new value at the top of the stack</returns>
    public object RunGeneric<T, U> (string lambdaExpression)
    {
      return RunGeneric<T, U, object, object> (lambdaExpression);
    }

    /// <summary>
    /// Remove all the necessary parameters of the lambda expression from the stack,
    /// run the lambda expression
    /// and push the result in the stack
    /// 
    /// T is the type of the first argument of the lamba expression
    /// U is the type of the second argument of the lamba expression
    /// V is the type of the third argument of the lambda expression
    /// </summary>
    /// <param name="lambdaExpression">lambda expresion</param>
    /// <returns>new value at the top of the stack</returns>
    public object RunGeneric<T, U, V> (string lambdaExpression)
    {
      return RunGeneric<T, U, V, object> (lambdaExpression);
    }

    /// <summary>
    /// Remove all the necessary parameters of the lambda expression from the stack,
    /// run the lambda expression
    /// and push the result in the stack
    /// 
    /// T is the type of the first argument of the lamba expression
    /// U is the type of the second argument of the lamba expression
    /// V is the type of the third argument of the lambda expression
    /// W is the type of the fourth argument of the lambda expression
    /// </summary>
    /// <param name="lambdaExpression">lambda expresion</param>
    /// <returns>new value at the top of the stack</returns>
    public object RunGeneric<T, U, V, W> (string lambdaExpression)
    {
      int nbParameters;

      try {
        var match = m_lambdaRegex.Match (lambdaExpression);
        if (!match.Success) {
          log.Error ($"RungGeneric: {lambdaExpression} does not match regex {m_lambdaRegex}");
          m_error = true;
          throw new ArgumentException ("Wrong lambda expression");
        }
        string commaSeparatedParameters = match.Groups["params"].Value;
        string[] parameters = commaSeparatedParameters.Split (new char[] { ',' });
        nbParameters = parameters.Length;
        if ((1 == nbParameters) && (parameters[0].Trim ().Equals ("()"))) {
          nbParameters = 0;
        }
      }
      catch (Exception ex) {
        log.Error ($"RungGeneric: wrong lambda expression {lambdaExpression}", ex);
        m_error = true;
        throw;
      }

      if (m_stack.Count < nbParameters) {
        log.Error ("RungGeneric: not enough elements in the stack");
        m_error = true;
        throw new Exception ("Not enough elements in stack");
      }

      object lambda;
      try {
        if (!m_lambdas.TryGetValue (lambdaExpression, out lambda)) {
          lambda = GetLambdaFunc<T, U, V, W> (lambdaExpression, nbParameters);
          Debug.Assert (null != lambda);
          m_lambdas[lambdaExpression] = lambda;
        }
      }
      catch (Exception ex) {
        log.Error ($"RungGeneric: lambda {lambdaExpression} is not valid", ex);
        m_error = true;
        throw;
      }

      IList<object> arguments = new List<object> ();
      try {
        for (int i = 0; i < nbParameters; ++i) {
          arguments.Insert (0, m_stack.Pop ());
        }
      }
      catch (Exception ex) {
        log.Error ($"RungGeneric: exception in Pop", ex);
        m_error = true;
        throw;
      }

      Debug.Assert (nbParameters == arguments.Count);
      object result;
      try {
        if (0 == nbParameters) {
          result = ((Func<object>)lambda).Invoke ();
        }
        else if (1 == nbParameters) {
          result = ((Func<T, object>)lambda).Invoke ((T)arguments[0]);
        }
        else if (2 == nbParameters) {
          result = ((Func<T, U, object>)lambda).Invoke ((T)arguments[0], (U)arguments[1]);
        }
        else if (3 == nbParameters) {
          result = ((Func<T, U, V, object>)lambda).Invoke ((T)arguments[0], (U)arguments[1], (V)arguments[2]);
        }
        else if (4 == nbParameters) {
          result = ((Func<T, U, V, W, object>)lambda).Invoke ((T)arguments[0], (U)arguments[1], (V)arguments[2], (W)arguments[3]);
        }
        else {
          log.Error ($"RunGeneric: the number of arguments {nbParameters} is not supported for the moment");
          m_error = true;
          throw new NotSupportedException ("Number of arguments in lambda expression");
        }
      }
      catch (Exception ex) {
        log.Error ("RungGeneric: exception", ex);
        m_error = true;
        throw;
      }

      if (log.IsDebugEnabled) {
        log.Debug ($"RungGeneric: {lambdaExpression}={result}");
      }
      m_stack.Push (result);
      return result;
    }

    object GetLambdaFunc<T, U, V, W> (string lambdaExpression, int nbParameters)
    {
      Debug.Assert (0 <= nbParameters);

      string s;
      var funcRegexMatch = m_funcRegex.Match (lambdaExpression);
      if (funcRegexMatch.Success) {
        s = "new Func";
        if (funcRegexMatch.Groups["types"].Success) {
          s += "<";
          s += funcRegexMatch.Groups["types"].Value;
          s += ", object>";
        }
        s += "(";
        Debug.Assert (funcRegexMatch.Groups["arguments"].Success);
        s += funcRegexMatch.Groups["arguments"].Value;
        s += ")";
        return Task.Run (() => CSharpScript.EvaluateAsync (s, m_scriptOptions)).Result;
      }
      else {
        switch (nbParameters) {
        case 0:
          return Task.Run (() => CSharpScript.EvaluateAsync<Func<object>> (lambdaExpression, m_scriptOptions)).Result;
        case 1:
          return Task.Run (() => CSharpScript.EvaluateAsync<Func<T, object>> (lambdaExpression, m_scriptOptions)).Result;
        case 2:
          return Task.Run (() => CSharpScript.EvaluateAsync<Func<T, U, object>> (lambdaExpression, m_scriptOptions)).Result;
        case 3:
          return Task.Run (() => CSharpScript.EvaluateAsync<Func<T, U, V, object>> (lambdaExpression, m_scriptOptions)).Result;
        case 4:
          return Task.Run (() => CSharpScript.EvaluateAsync<Func<T, U, V, W, object>> (lambdaExpression, m_scriptOptions)).Result;
        default:
          log.Error ($"GetLambdaFunc: nbParameters={nbParameters} not supported");
          throw new NotImplementedException ("Number of parameters in lambda not supported");
        }
      }
    }

    string RemoveGenericStructures (string s)
    {
      // Note that here the regex only supports one level of generic types,
      // which is probably sufficient for the moment
      var match = m_genericStructureRegex.Match (s);
      if (!match.Success) {
        log.DebugFormat ("RemoveGenericStructures: " +
                         "{0} did not match regex {1} " +
                         "=> return the original string",
                         s, m_genericStructureRegex);
        return s;
      }
      else {
        Debug.Assert (match.Groups["maintype"].Success);
        string result = match.Groups["maintype"].Value + "<";
        foreach (var capture1 in match.Groups["type1"].Captures) {
          result += capture1 + ",";
        }
        var type2 = match.Groups["type2"];
        if (type2.Success) {
          result += type2.Value;
        }
        result += ">";
        return result;
      }
    }
    #endregion // Methods
  }
}

#endif // NETSTANDARD || NET48 || NETCOREAPP
