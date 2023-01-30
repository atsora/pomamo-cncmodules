// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections;
using System.Globalization;

using Lemoine.Core.Log;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Class to get the data from a file, for example the operation ID.
  /// </summary>
  public class FileData: Lemoine.Cnc.BaseCncModule, Lemoine.Cnc.ICncModule, IDisposable
  {
    /// <summary>
    /// Default possible separators between the key and the value
    /// </summary>
    static readonly string DEFAULT_SEPARATORS = "=: ";
    /// <summary>
    /// Default string that marks a line as a comment
    /// </summary>
    static readonly string DEFAULT_COMMENT = "#";

    #region Members
    string m_fileName;
    double m_timeout = double.MaxValue;
    string m_separators = DEFAULT_SEPARATORS;
    string m_comment = DEFAULT_COMMENT;
    
    bool m_fileError = false;
    bool m_obsoleteFile = false;
    bool m_keyNotFound = false;
    
    Hashtable m_data = new Hashtable ();
    #endregion

    #region Getters / Setters    
    /// <summary>
    /// Name of the file to read
    /// </summary>
    public string FileName {
      get { return m_fileName; }
      set { m_fileName = value; }
    }
    
    /// <summary>
    /// Time in seconds after which the file is considered as not valid any more.
    /// Default is no time out (max value).
    /// </summary>
    public double Timeout {
      get { return m_timeout; }
      set { m_timeout = value; }
    }
    
    /// <summary>
    /// String with the possible separator characters between the key and the value
    /// </summary>
    public string Separators {
      get { return m_separators; }
      set { m_separators = value; }
    }
    
    /// <summary>
    /// String to mark a line as a comment
    /// </summary>
    public string Comment {
      get { return m_comment; }
      set { m_comment = value; }
    }
    
    /// <summary>
    /// Has the file been parsed successfully ?
    /// </summary>
    public bool FileError {
      get { return m_fileError; }
    }
    
    /// <summary>
    /// Set to true if the found file is too old
    /// (according to the Timeout property)
    /// </summary>
    public bool ObsoleteFile {
      get { return m_obsoleteFile; }
    }
    
    /// <summary>
    /// Has a key not been found in the file ?
    /// </summary>
    public bool KeyNotFound {
      get { return m_keyNotFound; }
    }
    #endregion

    #region Constructors / Destructor / ToString methods
    /// <summary>
    /// Description of the constructor
    /// </summary>
    public FileData ()
      : base("Lemoine.Cnc.In.FileData")
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
    /// Start method: read and parse the file
    /// </summary>
    public void Start ()
    {
      m_data.Clear ();
      m_keyNotFound = false;
      m_obsoleteFile = false;
      
      try {
        DateTime lastWriteTime = System.IO.File.GetLastWriteTime (this.FileName);
        TimeSpan age = DateTime.Now - lastWriteTime;
        if (age.TotalSeconds < 0) {
          log.ErrorFormat ("Start: " +
                           "bad date/time synchronization, " +
                           "last write time {0} is after now",
                           lastWriteTime);
        }
        else if (age.TotalSeconds > this.Timeout) {
          log.InfoFormat ("Start: " +
                          "the file is too old, " +
                          "it is {0} seconds old and the time out is {1} s, " +
                          "discard it",
                          age.TotalSeconds, this.Timeout);
          // Note: do not update the fileError property here
          m_obsoleteFile = true;
          return;
        }
        
        using (System.IO.StreamReader streamReader = System.IO.File.OpenText (this.FileName))
        {
          m_fileError = false;
          while (false == streamReader.EndOfStream) {
            string line = streamReader.ReadLine ();
            if (line.StartsWith (this.Comment)) {
              log.DebugFormat ("Start: " +
                               "{0} is a comment",
                               line);
              continue;
            }
            string [] values = line.Split (this.m_separators.ToCharArray (),
                                           2,
                                           StringSplitOptions.RemoveEmptyEntries);
            if (values.Length < 1) {
              log.InfoFormat ("Start: " +
                              "got an empty data in line {0}",
                              line);
            }
            else if (values.Length < 2) {
              log.WarnFormat ("Start: " +
                              "no key/value pair in line {0}, got only {1}",
                              line, values [0]);
              m_data [""] = values [0].Trim ();
            }
            else {
              log.DebugFormat ("Start: " +
                               "in line {0} got key/value {1}={2}",
                               line, values [0], values [1]);
              m_data [values [0].Trim ()] = values [1].Trim ();
            }
          }
        }
      }
      catch (Exception ex) {
        log.ErrorFormat ("Start: " +
                         "following exception occurred: {0}, " +
                         "=> set the error property to false",
                         ex);
        m_fileError = true;
        throw ex;
      }
    }
    
    /// <summary>
    /// Get the string value of a corresponding key
    /// </summary>
    /// <param name="param">key value</param>
    /// <returns></returns>
    public string GetString (string param)
    {
      string trimmedParam = param.Trim ();
      if (m_data.Contains (trimmedParam)) {
        log.DebugFormat ("GetString: " +
                         "get {0} for key {1}",
                         m_data [trimmedParam], param);
        return m_data [trimmedParam] as string;
      }
      else {
        log.ErrorFormat ("GetString: " +
                         "could not get key {0}",
                         param);
        m_keyNotFound = true;
        throw new Exception ("Unknown key");
      }
    }

    /// <summary>
    /// Get the int value of a corresponding key
    /// </summary>
    /// <param name="param">key value</param>
    /// <returns></returns>
    public int GetInt (string param)
    {
      return int.Parse (this.GetString (param));
    }
    
    /// <summary>
    /// Get the long value of a corresponding key
    /// </summary>
    /// <param name="param">key value</param>
    /// <returns></returns>
    public long GetLong (string param)
    {
      return long.Parse (this.GetString (param));
    }
    
    /// <summary>
    /// Get the double value of a corresponding key
    /// </summary>
    /// <param name="param">key value</param>
    /// <returns></returns>
    public double GetDouble (string param)
    {
      CultureInfo usCultureInfo = new CultureInfo ("en-US");
      return double.Parse (this.GetString (param),
                           usCultureInfo);
    }

    /// <summary>
    /// Get the boolean value of a corresponding key
    /// </summary>
    /// <param name="param">key value</param>
    /// <returns></returns>
    public bool GetBool (string param)
    {
      return bool.Parse (this.GetString (param));
    }    
    
    /// <summary>
    /// Get a percentage value of a corresponding key
    /// </summary>
    /// <param name="param">key value</param>
    /// <returns></returns>
    public int GetPercent (string param)
    {
      return int.Parse (this.GetString (param).TrimEnd (new char [] {'%'}));
    }
    #endregion
  }
}
