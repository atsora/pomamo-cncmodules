// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using Lemoine.Core.Log;
using Lemoine.WebClient;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Module which is using the /data request of the CncCoreService to get all the data of a remote acquisition
  /// </summary>
  public sealed class CncCoreData
    : Lemoine.Cnc.BaseCncModule, Lemoine.Cnc.ICncModule, IDisposable
  {
    readonly HttpClient m_httpClient;
    bool m_error = false;
    IDictionary<string, object> m_data = null;

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
    /// Api key
    /// </summary>
    public string ApiKey { get; set; } = "";
    #endregion // Getters / Setters

    #region Constructors / Destructor / ToString methods
    /// <summary>
    /// Description of the constructor
    /// </summary>
    public CncCoreData ()
      : base ("Lemoine.Cnc.In.CncCoreData")
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
      m_data = null;

      if (log.IsDebugEnabled) {
        log.Debug ($"Start: base url is {this.BaseUrl}");
      }

      try {
        var requestUrl = new RequestUrl ("data")
          .Add ("acquisition", this.AcquisitionIdentifier);
        if (!string.IsNullOrEmpty (this.ApiKey)) {
          requestUrl = requestUrl.AddHeader ("X-API-KEY", this.ApiKey);
        }
        m_data = new Query (m_httpClient, this.BaseUrl)
          .UniqueResult<IDictionary<string, object>> (requestUrl);
        return true;
      }
      catch (Exception ex) {
        log.Error ($"Start: exception", ex);
        m_error = true;
        return false;
      }
    }

    /// <summary>
    /// Finish method
    /// </summary>
    public void Finish ()
    {

    }

    /// <summary>
    /// A get method
    /// </summary>
    /// <param name="param"></param>
    /// <returns></returns>
    public object Get (string param)
    {
      if (log.IsDebugEnabled) {
        log.Debug ($"Get: param={param}");
      }
      if (null == m_data) {
        log.Error ($"Get: null data (start failed ?)");
        throw new InvalidOperationException ("null data");
      }
      return m_data[param];
    }
  }
}
