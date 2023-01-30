// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Xml;
using Lemoine.Core.Log;
using Lemoine.WebClient;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Module to post a request in XML format to the Cnc Core Service
  /// </summary>
  public sealed class CncCoreXmlPost
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
    /// Remote module reference
    /// </summary>
    public string ModuleRef { get; set; }

    /// <summary>
    /// Api key
    /// </summary>
    public string ApiKey { get; set; } = "";
    #endregion // Getters / Setters

    #region Constructors / Destructor / ToString methods
    /// <summary>
    /// Constructor
    /// </summary>
    public CncCoreXmlPost ()
      : base ("Lemoine.Cnc.In.CncCoreXmlPost")
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
    public bool Start (XmlElement moduleElement, IDictionary<string, object> cncData)
    {
      m_error = false;
      m_data = null;

      if (log.IsDebugEnabled) {
        log.Debug ($"Start: base url is {this.BaseUrl}");
      }

      var xml = $@"<root>
  <moduleref ref=""{this.ModuleRef}"">
{moduleElement.InnerXml}
  </moduleref>
</root>";

      try {
        var requestUrl = new RequestUrl ("xml")
          .Add ("acquisition", this.AcquisitionIdentifier);
        if (!string.IsNullOrEmpty (this.ApiKey)) {
          requestUrl = requestUrl.AddHeader ("X-API-KEY", this.ApiKey);
        }
        m_data = new Query (m_httpClient, this.BaseUrl)
          .UniqueResult<IDictionary<string, object>> (requestUrl, xml, "text/xml");
        foreach (var data in m_data) {
          if (log.IsDebugEnabled) {
            log.Debug ($"Start: set {data.Key} = {data.Value}");
          }
          cncData[data.Key] = data.Value;
        }
        return true;
      }
      catch (Exception ex) {
        log.Error ($"Start: exception", ex);
        if (moduleElement.HasAttribute ("starterror")) {
          string starterror = moduleElement.GetAttribute ("starterror");
          if (log.IsDebugEnabled) {
            log.Debug ($"Start: set true to starterror property {starterror} because of an exception");
          }
          cncData[starterror] = true;
        }
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
  }
}
