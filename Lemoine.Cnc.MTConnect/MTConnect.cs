// Copyright (C) 2009-2023 Lemoine Automation Technologies
// Copyright (C) 2024 Atsora Solutions
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections;
using System.Globalization;
using System.Xml;
using System.Xml.XPath;
using System.IO;

namespace Lemoine.Cnc
{
  /// <summary>
  /// MTConnect input module
  /// </summary>
  public partial class MTConnect : BaseCncModule, ICncModule, IDisposable
  {
    static readonly string RANDOM = "RANDOM";
    static readonly string UNAVAILABLE = "UNAVAILABLE";
    static readonly string DEFAULT_MTCONNECTSTREAMS_NAMESPACE_PREFIX = "m";
    static readonly string DEFAULT_MTCONNECTSTREAMS_NAMESPACE = "urn:mtconnect.org:MTConnectStreams:1.1";
    static readonly string DEFAULT_BLOCK_PATH = "//Controller//DataItems/DataItem[@type='BLOCK']"; // Full XPath is //Controller/Components/Path/DataItems
    static readonly string DEFAULT_BLOCK_XPATH = "//m:Block";

    #region Members
    Random m_random = new Random ();
    Hashtable m_xmlns = new Hashtable ();
    string m_mtconnectStreamsPrefix = DEFAULT_MTCONNECTSTREAMS_NAMESPACE_PREFIX;
    string m_blockPath = DEFAULT_BLOCK_PATH;
    string m_blockXPath = DEFAULT_BLOCK_XPATH;

    bool m_error = true;
    XmlNamespaceManager m_streamsNs = null;
    XPathNavigator m_streamsNavigator;
    #endregion // Members

    #region Getters / Setters
    /// <summary>
    /// MTConnect URL of the machine
    /// 
    /// For example:
    /// <item>http://mtconnectagentaddress:port/mymachine/current</item>
    /// <item>http://mtconnectagentaddress:port/mymachine/current?//Controller[@id="controllerId"]|//Axes[@id="AxesId"]</item>
    /// </summary>
    public string Url { get; set; }

    /// <summary>
    /// XML namespace
    /// in the form prefix1=uri1;prefix2=uri2;
    /// 
    /// For example:
    /// <item>m=urn:mtconnect.org:MTConnectStreams:1.1</item>
    /// 
    /// If no namespace is associated to MTConnectStreamsPrefix,
    /// a default namespace is used.
    /// <see cref="MTConnectStreamsPrefix" />
    /// </summary>
    public string Xmlns
    {
      get {
        string result = "";
        foreach (DictionaryEntry i in m_xmlns) {
          result += String.Format ("{0}:{1};", i.Key, i.Value);
        }
        return result;
      }
      set {
        m_xmlns.Clear ();
        string[] xmlNamespaces = value.Split (new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string xmlNamespace in xmlNamespaces) {
          string[] xmlNamespaceKeyValue = xmlNamespace.Split ('=');
          if (xmlNamespaceKeyValue.Length != 2) {
            log.ErrorFormat ("Xmlns.set: " +
                             "invalid value {0}",
                             xmlNamespace);
            throw new ArgumentException ("Bad XML namespace");
          }
          log.DebugFormat ("Xmlns.set: " +
                           "add prefix={0} uri={1}",
                           xmlNamespaceKeyValue[0], xmlNamespaceKeyValue[1]);
          m_xmlns[xmlNamespaceKeyValue[0]] = xmlNamespaceKeyValue[1];
        }
      }
    }

    /// <summary>
    /// MTConnectStreams namespace prefix that must be used in the XPath expressions.
    /// It is used to get the Header data (instance and nextSequence values).
    /// If the XML file does not use any namespace, then use an empty string.
    /// 
    /// Default is "m".
    /// </summary>
    public string MTConnectStreamsPrefix
    {
      get { return m_mtconnectStreamsPrefix; }
      set { m_mtconnectStreamsPrefix = value; }
    }

    /// <summary>
    /// XPath used in the URL to get the BLOCK.
    /// This is XPath related to the probe page (not the current or sample pages)
    /// 
    /// Default is "//Controller//DataItems/DataItem[@type='BLOCK']".
    /// </summary>
    public string BlockPath
    {
      get { return m_blockPath; }
      set { m_blockPath = value; }
    }

    /// <summary>
    /// XPath used to get a Block note in the sample request.
    /// 
    /// Default is "//m:Block"
    /// </summary>
    public string BlockXPath
    {
      get { return m_blockXPath; }
      set { m_blockXPath = value; }
    }

    /// <summary>
    /// Error while getting the XML ?
    /// </summary>
    public bool Error
    {
      get { return m_error; }
    }
    #endregion // Getters / Setters

    #region Constructors / Destructor
    /// <summary>
    /// Description of the constructor
    /// </summary>
    public MTConnect () : base ("Lemoine.Cnc.In.MTConnect")
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
    #endregion // Constructors / Destructor

    #region Methods
    /// <summary>
    /// Start method
    /// </summary>
    public bool Start ()
    {
      // 1. Prepare streams
      m_error = true;
      try {
        string url = Url.Contains (RANDOM) ?
          Url.Replace (RANDOM, m_random.Next ().ToString ()) :
          Url;

        // Streams navigator
        var document = new XPathDocument (url);
        m_streamsNavigator = document.CreateNavigator ();

        // Namespace
        m_streamsNs = CreateNamespace (m_streamsNavigator, m_mtconnectStreamsPrefix, DEFAULT_MTCONNECTSTREAMS_NAMESPACE);
      }
      catch (System.Net.WebException ex) {
        log.Warn ($"Start: unable to connect to {this.Url}", ex);
        return false;
      }
      catch (Exception ex) {
        log.Error ($"Start: exception raised when trying to load URL={this.Url}", ex);
        throw;
      }

      // No error
      m_error = false;

      // 2. Prepare assets (for tool life management)
      try {
        StartAssets ();
      }
      catch (Exception ex) {
        log.Error ("Start: failed starting assets", ex);
      }

      // 3. Reset blocks
      m_hasBlocks = false;
      return true;
    }

    XmlNamespaceManager CreateNamespace (XPathNavigator navigator, string prefix, string defaultNs)
    {
      XmlNamespaceManager nsManager = null;
      if (m_xmlns.Count == 0) {
        if (0 == prefix.Length) {
          // No namespace
          nsManager = null;
          log.InfoFormat ("Start: no namespace for MTConnect");
        }
        else {
          // Default namespace
          nsManager = new XmlNamespaceManager (navigator.NameTable);
          nsManager.AddNamespace (prefix, defaultNs);
          log.InfoFormat ("Start: use default namespace {0} for MTConnect prefix {1}",
                          defaultNs, prefix);
        }
      }
      else {
        // From the Xmlns variable
        nsManager = new XmlNamespaceManager (navigator.NameTable);
        foreach (DictionaryEntry i in m_xmlns) {
          nsManager.AddNamespace (i.Key.ToString (), i.Value.ToString ());
        }
      }

      return nsManager;
    }

    /// <summary>
    /// Get a valid value from a XPathNavigator, else raise an exception
    /// </summary>
    /// <param name="node"></param>
    /// <param name="xpath"></param>
    /// <param name="mayBeUnavailable"></param>
    /// <returns></returns>
    string GetValidValue (XPathNavigator node, string xpath = null, bool mayBeUnavailable = false)
    {
      if (null == node) {
        log.Warn ("GetValidValue: no node was found");
        throw new Exception ("No node");
      }
      if (log.IsDebugEnabled) {
        log.Debug ($"GetValidValue: got {node.Value} for {xpath ?? node.ToString ()}");
      }
      if (node.Value.Equals (UNAVAILABLE)) {
        if (mayBeUnavailable) {
          log.Warn ($"GetValidValue: UNAVAILABLE returned for {xpath ?? node.ToString ()}");
        }
        else {
          log.Error ($"GetValidValue: UNAVAILABLE returned for {xpath ?? node.ToString ()}");
        }
        throw new Exception ("Unavailable");
      }

      return node.Value;
    }

    /// <summary>
    /// Get a string value
    /// </summary>
    /// <param name="xpath">XPath</param>
    /// <returns></returns>
    public string GetString (string xpath)
    {
      if (m_error) {
        log.Error ($"GetString: Error while loading the XML");
        throw new Exception ("Error while loading XML");
      }

      XPathNavigator node = (m_streamsNs == null) ?
        m_streamsNavigator.SelectSingleNode (xpath) :
        m_streamsNavigator.SelectSingleNode (xpath, m_streamsNs);

      return GetValidValue (node, xpath);
    }

    /// <summary>
    /// Get a string value that may be unavailable
    /// </summary>
    /// <param name="xpath">XPath</param>
    /// <returns></returns>
    public string GetPossiblyUnavailableString (string xpath)
    {
      if (m_error) {
        log.Error ($"GetPossiblyUnavailableString: Error while loading the XML");
        throw new Exception ("Error while loading XML");
      }

      XPathNavigator node = (m_streamsNs == null) ?
        m_streamsNavigator.SelectSingleNode (xpath) :
        m_streamsNavigator.SelectSingleNode (xpath, m_streamsNs);

      return GetValidValue (node, xpath, true);
    }

    /// <summary>
    /// Get an int value
    /// </summary>
    /// <param name="param">XPath</param>
    /// <returns></returns>
    public int GetInt (string param)
    {
      return int.Parse (this.GetString (param));
    }

    /// <summary>
    /// Get an int value that may be unavailable
    /// </summary>
    /// <param name="param">XPath</param>
    /// <returns></returns>
    public int GetPossiblyUnavailableInt (string param)
    {
      return int.Parse (this.GetPossiblyUnavailableString (param));
    }

    /// <summary>
    /// Get a long value
    /// </summary>
    /// <param name="param">XPath</param>
    /// <returns></returns>
    public long GetLong (string param)
    {
      return long.Parse (this.GetString (param));
    }

    /// <summary>
    /// Get a long value that may be unavailable
    /// </summary>
    /// <param name="param">XPath</param>
    /// <returns></returns>
    public long GetPossiblyUnavailableLong (string param)
    {
      return long.Parse (this.GetPossiblyUnavailableString (param));
    }

    /// <summary>
    /// Get a double value
    /// </summary>
    /// <param name="param">XPath</param>
    /// <returns></returns>
    public double GetDouble (string param)
    {
      var usCultureInfo = new CultureInfo ("en-US"); // Point is the decimal separator
      return double.Parse (this.GetString (param),
                           usCultureInfo);
    }

    /// <summary>
    /// Get a double value that may be unavailable
    /// </summary>
    /// <param name="param">XPath</param>
    /// <returns></returns>
    public double GetPossiblyUnavailableDouble (string param)
    {
      var usCultureInfo = new CultureInfo ("en-US"); // Point is the decimal separator
      return double.Parse (this.GetPossiblyUnavailableString (param),
                           usCultureInfo);
    }

    /// <summary>
    /// Get a position from string with three values, comma or space separated
    /// 
    /// For example: 1.0 0.45 2.3 or 1.0,0.45,2.3
    /// </summary>
    /// <param name="param">XPath</param>
    /// <returns></returns>
    public Position GetPosition (string param)
    {
      string[] values = this.GetString (param).Split (new char[] { ' ', ',' });
      if (values.Length != 3) {
        log.Error ($"GetPosition: number of values is {values.Length} and not 3");
      }
      var usCultureInfo = new CultureInfo ("en-US"); // Point is the decimal separator
      var x = double.Parse (values[0], usCultureInfo);
      var y = double.Parse (values[1], usCultureInfo);
      var z = double.Parse (values[2], usCultureInfo);
      var position = new Position (x, y, z);
      return position;
    }

    /// <summary>
    /// Get a position from string with three values, space separated
    /// 
    /// For example: 1.0 0.45 2.3
    /// </summary>
    /// <param name="param">XPath</param>
    /// <returns></returns>
    public Position GetPositionSpaceSep (string param)
    {
      string[] values = this.GetString (param).Split (' ');
      if (values.Length != 3) {
        log.Error ($"GetPosition: number of values is {values.Length} and not 3");
      }
      var usCultureInfo = new CultureInfo ("en-US"); // Point is the decimal separator
      var x = double.Parse (values[0], usCultureInfo);
      var y = double.Parse (values[1], usCultureInfo);
      var z = double.Parse (values[2], usCultureInfo);
      var position = new Position (x, y, z);
      return position;
    }

    /// <summary>
    /// Get a position from string with three values separated by a comma
    /// 
    /// For example: 1.0,0.45,2.3
    /// </summary>
    /// <param name="param">XPath</param>
    /// <returns></returns>
    public Position GetPositionCommaSep (string param)
    {
      string[] values = this.GetString (param).Split (',');
      if (values.Length != 3) {
        log.Error ($"GetPositionCommaSep: number of values is {values.Length} and not 3");
      }
      var usCultureInfo = new CultureInfo ("en-US"); // Point is the decimal separator
      var x = double.Parse (values[0], usCultureInfo);
      var y = double.Parse (values[1], usCultureInfo);
      var z = double.Parse (values[2], usCultureInfo);
      var position = new Position (x, y, z);
      return position;
    }

    /// <summary>
    /// Convert an ON/OFF string into a boolean value
    /// </summary>
    /// <param name="param"></param>
    /// <returns></returns>
    public bool GetOnOff (string param)
    {
      string v = this.GetString (param);
      if (object.Equals ("ON", v)) {
        return true;
      }
      else if (object.Equals ("OFF", v)) {
        return false;
      }
      else {
        log.Error ($"GetOnOff: {v} is not a valid ON/OFF value");
        throw new Exception ("Invalid ON/OFF value");
      }
    }

    /// <summary>
    /// Get the program name.
    /// This is the same as GetString, except the block internal values
    /// are cleaned in case the program name changes
    /// </summary>
    /// <param name="param"></param>
    /// <returns></returns>
    public string GetProgramName (string param)
    {
      string program = GetString (param);
      if (null == this.m_programName) {
        this.m_programName = program;
      }
      else if (!program.Equals (this.m_programName)) {
        if (log.IsDebugEnabled) {
          log.DebugFormat ("GetProgramName: " +
                           "program name was updated from {0} to {1} " +
                           "=> clean the block internal values",
                           this.m_programName, program);
        }
        this.m_blockValues.Clear ();
        this.m_programName = program;
      }

      return program;
    }
    #endregion // Methods
  }
}
