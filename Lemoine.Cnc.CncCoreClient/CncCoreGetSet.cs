// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml;
using Lemoine.Core.Log;
using Lemoine.WebClient;
using Lemoine.Cnc.Asp;
using System.Net.Http;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Module to request the cnc core service with the get, set, getproperty or setproperty instructions
  /// </summary>
  public sealed class CncCoreGetSet
    : Lemoine.Cnc.BaseCncModule, Lemoine.Cnc.ICncModule, IDisposable
  {
    readonly HttpClient m_httpClient;
    bool m_error = false;
    DateTime m_lastRequest = DateTime.UtcNow;

    #region Getters / Setters
    /// <summary>
    /// An error occurred
    /// </summary>
    public bool Error => m_error;

    /// <summary>
    /// Base Cnc core service url
    /// </summary>
    public string BaseUrl { get; set; }

    /// <summary>
    /// Acquisition identifier
    /// </summary>
    public string AcquisitionIdentifier { get; set; } = "";

    /// <summary>
    /// Remote module reference
    /// </summary>
    public string ModuleRef { get; set; }

    /// <summary>
    /// Api key
    /// </summary>
    public string ApiKey { get; set; } = "";

    /// <summary>
    /// Minimum interval in ms between two requests
    /// </summary>
    public int IntervalMs { get; set; } = 0;
    #endregion // Getters / Setters

    #region Constructors / Destructor / ToString methods
    /// <summary>
    /// Constructor
    /// </summary>
    public CncCoreGetSet ()
      : base ("Lemoine.Cnc.In.CncCoreGetSet")
    {
      // To accept not valid SSL certificates
      var handler = new HttpClientHandler () {
        ServerCertificateCustomValidationCallback = (httpRequestMessage, cert, cetChain, policyError) => true,
      };
      m_httpClient = new HttpClient (handler);
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

    /// <summary>
    /// Start method: reset the different values
    /// </summary>
    public bool Start ()
    {
      m_error = false;

      return true;
    }

    /// <summary>
    /// Finish method
    /// </summary>
    public void Finish ()
    {

    }

    void WaitExecutionTime ()
    {
      if (0 < this.IntervalMs) {
        var nextExecutionDateTime = m_lastRequest.AddMilliseconds (this.IntervalMs);
        var now = DateTime.UtcNow;
        if (now < nextExecutionDateTime) {
          var waitDuration = nextExecutionDateTime.Subtract (now);
          if (log.IsDebugEnabled) {
            log.Debug ($"WaitExecutionTime: wait {waitDuration}");
          }
          System.Threading.Thread.Sleep (waitDuration);
        }
        m_lastRequest = DateTime.UtcNow;
      }
    }

    /// <summary>
    /// Use the /get?moduleref=...&amp;method=...&amp;param=... request of the cnc core service
    /// </summary>
    /// <param name="param">:method:param where the first character is the separator to use</param>
    /// <returns></returns>
    public object Get (string param)
    {
      if (log.IsDebugEnabled) {
        log.Debug ($"Get: param={param}");
      }

      WaitExecutionTime ();

      var methodParameter = Lemoine.Collections.EnumerableString.ParseListString (param);
      if (2 != methodParameter.Length) {
        log.Error ($"Get: the syntax of param {param} is not correct, it is not a ListString");
        throw new ArgumentException ("Not a ListString with two values", "param");
      }
      var method = methodParameter[0];
      var requestParam = methodParameter[1];

      try {
        var requestUrl = new RequestUrl ("get")
          .Add ("acquisition", this.AcquisitionIdentifier)
          .Add ("moduleref", this.ModuleRef)
          .Add ("method", method)
          .Add ("param", requestParam);
        if (!string.IsNullOrEmpty (this.ApiKey)) {
          requestUrl = requestUrl.AddHeader ("X-API-KEY", this.ApiKey);
        }
        var singleResponse = new Query (m_httpClient, this.BaseUrl)
          .UniqueResult<SingleResponse> (requestUrl);
        if (singleResponse.Success) {
          var data = singleResponse.Result;
          if (log.IsDebugEnabled) {
            log.Debug ($"Get: response for {requestUrl} is {data}");
          }
          return data;
        }
        else {
          log.Error ($"Get: error {singleResponse.Error} for {requestUrl}");
          m_error = true;
          throw new Exception ($"CncCoreGetSet.Get: {singleResponse.Error}");
        }
      }
      catch (Exception ex) {
        log.Error ($"Get: exception", ex);
        m_error = true;
        throw;
      }
    }

    /// <summary>
    /// Use the /get?moduleref=...&amp;property=... request of the cnc core service
    /// </summary>
    /// <param name="param">property</param>
    /// <returns></returns>
    public object GetProperty (string param)
    {
      if (log.IsDebugEnabled) {
        log.Debug ($"GetProperty: param={param}");
      }

      WaitExecutionTime ();

      try {
        var requestUrl = new RequestUrl ("get")
          .Add ("acquisition", this.AcquisitionIdentifier)
          .Add ("moduleref", this.ModuleRef)
          .Add ("property", param);
        if (!string.IsNullOrEmpty (this.ApiKey)) {
          requestUrl = requestUrl.AddHeader ("X-API-KEY", this.ApiKey);
        }
        var singleResponse = new Query (m_httpClient, this.BaseUrl)
          .UniqueResult<SingleResponse> (requestUrl);
        if (singleResponse.Success) {
          var data = singleResponse.Result;
          if (log.IsDebugEnabled) {
            log.Debug ($"GetProperty: response for {requestUrl} is {data}");
          }
          return data;
        }
        else {
          log.Error ($"GetProperty: error {singleResponse.Error} for {requestUrl}");
          m_error = true;
          throw new Exception ($"CncCoreGetSet.Get: {singleResponse.Error}");
        }
      }
      catch (Exception ex) {
        log.Error ($"GetProperty: exception", ex);
        m_error = true;
        throw;
      }
    }

    /// <summary>
    /// Use the /set?moduleref=...&amp;method=...&amp;param=...&amp;v=...
    /// (or &amp;long=... or &amp;int=... or &amp;double=... or &amp;boolean=...)
    /// request of the cnc core service
    /// </summary>
    /// <param name="param">:method:param where the first character is the separator to use</param>
    /// <param name="v"></param>
    /// <returns></returns>
    public void Set (string param, object v)
    {
      if (log.IsDebugEnabled) {
        log.Debug ($"Set: param={param} v={v}");
      }

      WaitExecutionTime ();

      var methodParameter = Lemoine.Collections.EnumerableString.ParseListString (param);
      if (2 != methodParameter.Length) {
        log.Error ($"Set: the syntax of param {param} is not correct, it is not a ListString");
        throw new ArgumentException ("Not a ListString with two values", "param");
      }
      var method = methodParameter[0];
      var requestParam = methodParameter[1];

      try {
        var requestUrl = new RequestUrl ("set")
          .Add ("acquisition", this.AcquisitionIdentifier)
          .Add ("moduleref", this.ModuleRef)
          .Add ("method", method)
          .Add ("param", requestParam);
        if (v is long) {
          requestUrl = requestUrl
            .Add ("long", v.ToString ());
        }
        else if (v is int) {
          requestUrl = requestUrl
            .Add ("int", v.ToString ());
        }
        else if (v is double) {
          requestUrl = requestUrl
            .Add ("double", v.ToString ());
        }
        else if (v is bool) {
          requestUrl = requestUrl
            .Add ("boolean", v.ToString ());
        }
        else if (v is string) {
          requestUrl = requestUrl
            .Add ("string", v.ToString ());
        }
        else {
          requestUrl = requestUrl
            .Add ("v", v.ToString ());
        }
        if (!string.IsNullOrEmpty (this.ApiKey)) {
          requestUrl = requestUrl.AddHeader ("X-API-KEY", this.ApiKey);
        }
        var singleResponse = new Query (m_httpClient, this.BaseUrl)
          .UniqueResult<SingleResponse> (requestUrl);
        if (singleResponse.Success) {
          if (log.IsDebugEnabled) {
            log.Debug ($"Set: success for {requestUrl}");
          }
          return;
        }
        else {
          log.Error ($"Set: error {singleResponse.Error} for {requestUrl}");
          m_error = true;
          throw new Exception ($"CncCoreGetSet.Set: {singleResponse.Error}");
        }
      }
      catch (Exception ex) {
        log.Error ($"Set: exception", ex);
        m_error = true;
        throw;
      }
    }

    /// <summary>
    /// Use the /set?moduleref=...&amp;property=...&amp;v=...
    /// (or &amp;long=... or &amp;int=... or &amp;double=... or &amp;boolean=...)
    /// request of the cnc core service
    /// </summary>
    /// <param name="param">property</param>
    /// <param name="v"></param>
    /// <returns></returns>
    public void SetProperty (string param, object v)
    {
      if (log.IsDebugEnabled) {
        log.Debug ($"SetProperty: param={param} v={v}");
      }

      WaitExecutionTime ();

      try {
        var requestUrl = new RequestUrl ("set")
          .Add ("acquisition", this.AcquisitionIdentifier)
          .Add ("moduleref", this.ModuleRef)
          .Add ("property", param);
        if (v is long) {
          requestUrl = requestUrl
            .Add ("long", v.ToString ());
        }
        else if (v is int) {
          requestUrl = requestUrl
            .Add ("int", v.ToString ());
        }
        else if (v is double) {
          requestUrl = requestUrl
            .Add ("double", v.ToString ());
        }
        else if (v is bool) {
          requestUrl = requestUrl
            .Add ("boolean", v.ToString ());
        }
        else if (v is string) {
          requestUrl = requestUrl
            .Add ("string", v.ToString ());
        }
        else {
          requestUrl = requestUrl
            .Add ("v", v.ToString ());
        }
        if (!string.IsNullOrEmpty (this.ApiKey)) {
          requestUrl = requestUrl.AddHeader ("X-API-KEY", this.ApiKey);
        }
        var singleResponse = new Query (m_httpClient, this.BaseUrl)
          .UniqueResult<SingleResponse> (requestUrl);
        if (singleResponse.Success) {
          if (log.IsDebugEnabled) {
            log.Debug ($"SetProperty: success for {requestUrl}");
          }
          return;
        }
        else {
          log.Error ($"SetProperty: error {singleResponse.Error} for {requestUrl}");
          m_error = true;
          throw new Exception ($"CncCoreGetSet.SetProperty: {singleResponse.Error}");
        }
      }
      catch (Exception ex) {
        log.Error ($"SetProperty: exception", ex);
        m_error = true;
        throw;
      }
    }

  }
}
