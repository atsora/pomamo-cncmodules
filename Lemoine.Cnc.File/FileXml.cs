// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Globalization;
using System.Xml.XPath;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Input module to parse the acquisition data on XML files
  /// </summary>
  public class FileXml : Lemoine.Cnc.BaseCncModule, Lemoine.Cnc.ICncModule, IDisposable
  {
    #region Members
    string m_xmlPath;
    TimeSpan m_disconnectTime = TimeSpan.FromSeconds (10);
    
    XPathDocument  m_document = null;
    TimeSpan m_lastWriteAge;
    bool m_error = true;
    XPathNavigator m_pathNavigator;
    #endregion

    #region Getters / Setters
    /// <summary>
    /// Path of the XML file
    /// </summary>
    public string XmlPath {
      get { return m_xmlPath; }
      set { m_xmlPath = value; }
    }
    
    /// <summary>
    /// Disconnect Time (in s)
    /// </summary>
    public int DisconnectTime {
      get { return (int) m_disconnectTime.TotalSeconds; }
      set { m_disconnectTime = TimeSpan.FromSeconds (value); }
    }
    
    /// <summary>
    /// Error (including if the file is older than DisconnectTime)
    /// </summary>
    public virtual bool Error {
      get { return m_error || (TimeSpan.FromSeconds (this.DisconnectTime) < this.LastWriteAge); }
    }
    
    /// <summary>
    /// Age of the last write time of the file
    /// </summary>
    public virtual TimeSpan LastWriteAge {
      get { return m_lastWriteAge; }
    }
    
    /// <summary>
    /// Age of the last write time of the file in seconds
    /// </summary>
    public double LastWriteAgeSeconds {
      get { return this.LastWriteAge.TotalSeconds; }
    }
    #endregion

    #region Constructors
    /// <summary>
    /// Description of the constructor
    /// </summary>
    public FileXml ()
      : base("Lemoine.Cnc.In.FileXml")
    {
    }

    /// <summary>
    /// Description of the constructor
    /// </summary>
    protected FileXml (string name)
      : base (name)
    {
    }
    #endregion

    #region Methods
    /// <summary>
    /// <see cref="IDisposable.Dispose" />
    /// </summary>
    public void Dispose ()
    {
      // Do nothing special here
      GC.SuppressFinalize (this);
    }
    
    /// <summary>
    /// Start method
    /// </summary>
    /// <returns></returns>
    public virtual bool Start ()
    {
      if ( "" == m_xmlPath ) {
        log.WarnFormat(
          "Start: " +
          "no XML path {0}",
          m_xmlPath);
        m_error = true;
        return false;
      }
      
      try {       
        DateTime lastWriteTime = System.IO.File.GetLastWriteTime (this.m_xmlPath);
        m_lastWriteAge = DateTime.Now - lastWriteTime;
        if (m_lastWriteAge.TotalSeconds < 0) {
          log.ErrorFormat ("Start: " +
                           "bad date/time synchronization, " +
                           "last write time {0} is after now",
                           lastWriteTime);
          m_lastWriteAge = TimeSpan.FromSeconds (0);
        }
        
        m_document = new XPathDocument (this.m_xmlPath);
        m_pathNavigator = m_document.CreateNavigator ();
        
        m_error = false;
      }
      catch (Exception ex) {
        log.ErrorFormat ("Start: " +
                         "following exception occurred: {0}, " +
                         "=> disconnect",
                         ex);
        m_error = true;
        return false;
      }
      
      return true;
    }
    
    /// <summary>
    /// Finish method
    /// </summary>
    /// <returns></returns>
    public bool Finish ()
    {
      return true;
    }
    
    /// <summary>
    /// Get a valid value from a XPathNavigator, else raise an exception
    /// </summary>
    /// <param name="node"></param>
    /// <returns></returns>
    string GetValidValue (XPathNavigator node)
    {
      if (null == node) {
        log.Error ("GetValidValue: " +
                   "no node was found");
        throw new Exception ("No node");
      }
      log.DebugFormat ("GetValidValue: " +
                       "got {0}",
                       node.Value);
      
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
        log.ErrorFormat ("GetString: " +
                         "Error while loading the XML");
        throw new Exception ("Error while loading XML");
      }
      
      XPathNavigator node;
      node = m_pathNavigator.SelectSingleNode (xpath);
      
      return GetValidValue (node);
    }
    
    /// <summary>
    /// Get a double value
    /// </summary>
    /// <param name="param">Key</param>
    /// <returns></returns>
    public double GetDouble (string param)
    {
      CultureInfo usCultureInfo = new CultureInfo ("en-US");
      return double.Parse (this.GetString (param), usCultureInfo);
    }
    
    #endregion
  }
}
