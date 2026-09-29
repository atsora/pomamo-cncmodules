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

    /// <summary>
    /// An error occurred
    /// </summary>
    public bool Error => m_error;

    /// <summary>
    /// Base Cnc core service url
    /// </summary>
    public string BaseUrl { get; set; }

    /// <summary>
    /// Port of the service on the local machine
    ///
    /// It is a shortcut for BaseUrl, when the service runs on the same machine as the acquisition,
    /// which is for example the case of Lem_OpcUaClientService. BaseUrl comes first when both are set.
    /// </summary>
    public int Port { get; set; } = 0;

    /// <summary>
    /// Base url of the service to request
    /// </summary>
    string ServiceUrl => string.IsNullOrEmpty (this.BaseUrl) && (0 < this.Port)
      ? $"http://localhost:{this.Port}"
      : this.BaseUrl;

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
        log.Debug ($"Start: base url is {this.ServiceUrl}");
      }

      var xml = BuildXml (moduleElement);

      try {
        var requestUrl = new RequestUrl ("xml")
          .Add ("acquisition", this.AcquisitionIdentifier);
        if (!string.IsNullOrEmpty (this.ApiKey)) {
          requestUrl = requestUrl.AddHeader ("X-API-KEY", this.ApiKey);
        }
        m_data = new Query (m_httpClient, this.ServiceUrl)
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

    /// <summary>
    /// Get methods for the data the remote module did not return.
    ///
    /// Start () stores in the acquisition data every value the service returned, and the
    /// acquisition engine only runs a get instruction when its data is still unknown: these
    /// methods are therefore reached only when the service did not return the value, because the
    /// request failed or because the remote module could not read it.
    ///
    /// They exist so that such a value is reported for what it is. Without them the engine finds
    /// no method on this module and logs a Fatal about the configuration, which points at the
    /// wrong problem.
    /// </summary>
    /// <param name="param">parameter of the get instruction</param>
    /// <param name="method">name of the calling method</param>
    /// <returns>never, it always raises an exception</returns>
    Exception MissingValue (string param, [System.Runtime.CompilerServices.CallerMemberName] string method = "")
    {
      if (m_error || (null == m_data)) {
        log.Error ($"{method}: no value for {param} because the request to {this.ServiceUrl} failed");
        return new Exception ($"CncCoreXmlPost.{method}: the request to {this.ServiceUrl} failed");
      }

      log.Error ($"{method}: {this.ServiceUrl} did not return any value for {param}, the remote module could not read it");
      return new Exception ($"CncCoreXmlPost.{method}: no value for {param}");
    }

    /// <summary>
    /// <see cref="MissingValue" />
    /// </summary>
    /// <param name="param"></param>
    public object Get (string param) => throw MissingValue (param);

    /// <summary>
    /// <see cref="MissingValue" />
    /// </summary>
    /// <param name="param"></param>
    public object GetData (string param) => throw MissingValue (param);

    /// <summary>
    /// <see cref="MissingValue" />
    /// </summary>
    /// <param name="param"></param>
    public string GetString (string param) => throw MissingValue (param);

    /// <summary>
    /// <see cref="MissingValue" />
    /// </summary>
    /// <param name="param"></param>
    public bool GetBool (string param) => throw MissingValue (param);

    /// <summary>
    /// <see cref="MissingValue" />
    /// </summary>
    /// <param name="param"></param>
    public int GetInt (string param) => throw MissingValue (param);

    /// <summary>
    /// <see cref="MissingValue" />
    /// </summary>
    /// <param name="param"></param>
    public long GetLong (string param) => throw MissingValue (param);

    /// <summary>
    /// <see cref="MissingValue" />
    /// </summary>
    /// <param name="param"></param>
    public double GetDouble (string param) => throw MissingValue (param);

    /// <summary>
    /// Attributes of the module element that are not forwarded to the remote module:
    /// they are either processed by the acquisition engine, or they are properties of this module
    /// </summary>
    static readonly HashSet<string> NOT_FORWARDED_ATTRIBUTES =
      new HashSet<string> (StringComparer.InvariantCultureIgnoreCase) {
        "type", "ref", "starterror",
        "if", "ifnot", "ifnotorunknown", "ifandnotunknown", "ifnotempty", "ifempty",
        "ifdefined", "ifnotdefined", "iforunknown",
        "BaseUrl", "Port", "ApiKey", "AcquisitionIdentifier", "ModuleRef"
      };

    /// <summary>
    /// Build the XML to post
    ///
    /// The instructions of the module element are forwarded, and so are its own attributes, so that
    /// the remote module can be configured by this acquisition. An attribute that is not a property
    /// of this module only makes the acquisition engine log a warning when the module is loaded.
    /// </summary>
    /// <param name="moduleElement">not null</param>
    string BuildXml (XmlElement moduleElement)
    {
      var document = new XmlDocument ();
      var root = document.CreateElement ("root");
      document.AppendChild (root);

      var moduleRefElement = document.CreateElement ("moduleref");
      moduleRefElement.SetAttribute ("ref", this.ModuleRef ?? "");
      foreach (XmlAttribute attribute in moduleElement.Attributes) {
        if (NOT_FORWARDED_ATTRIBUTES.Contains (attribute.Name)
          || attribute.Name.StartsWith ("xmlns", StringComparison.InvariantCultureIgnoreCase)) {
          continue;
        }
        if (log.IsDebugEnabled) {
          log.Debug ($"BuildXml: forward the attribute {attribute.Name} to the remote module");
        }
        moduleRefElement.SetAttribute (attribute.Name, attribute.Value);
      }
      moduleRefElement.InnerXml = moduleElement.InnerXml;
      root.AppendChild (moduleRefElement);

      return document.OuterXml;
    }
  }
}
