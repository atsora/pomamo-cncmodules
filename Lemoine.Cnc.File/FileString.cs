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
  public class FileString: Lemoine.Cnc.BaseCncModule, Lemoine.Cnc.ICncModule, IDisposable
  {
    /// <summary>
    /// Default possible separators between the key and the value
    /// </summary>
    static readonly string DEFAULT_SEPARATORS = " ";
    /// <summary>
    /// Default string that marks a line as a comment
    /// </summary>
    static readonly string DEFAULT_COMMENT = "#";

    #region Members
    string fileName;
    double timeout = double.MaxValue;
    string separators = DEFAULT_SEPARATORS;
    string comment = DEFAULT_COMMENT;
    
    bool fileError = false;
    bool obsoleteFile = false;
    bool keyNotFound = false;
    
    Hashtable data = new Hashtable ();
    #endregion

    #region Getters / Setters
    /// <summary>
    /// Name of the file to read
    /// </summary>
    public string FileName {
      get { return fileName; }
      set { fileName = value; }
    }
    
    /// <summary>
    /// Time in seconds after which the file is considered as not valid any more.
    /// Default is no time out (max value).
    /// </summary>
    public double Timeout {
      get { return timeout; }
      set { timeout = value; }
    }
    
    /// <summary>
    /// String with the possible separator characters between the key and the value
    /// </summary>
    public string Separators {
      get { return separators; }
      set { separators = value; }
    }
    
    /// <summary>
    /// String to mark a line as a comment
    /// </summary>
    public string Comment {
      get { return comment; }
      set { comment = value; }
    }
    
    /// <summary>
    /// Has the file been parsed successfully ?
    /// </summary>
    public bool FileError {
      get { return fileError; }
    }
    
    /// <summary>
    /// Set to true if the found file is too old
    /// (according to the Timeout property)
    /// </summary>
    public bool ObsoleteFile {
      get { return obsoleteFile; }
    }
    
    /// <summary>
    /// Has a key not been found in the file ?
    /// </summary>
    public bool KeyNotFound {
      get { return keyNotFound; }
    }
    #endregion

    #region Constructors / Destructor / ToString methods
    /// <summary>
    /// Description of the constructor
    /// </summary>
    public FileString ()
      : base("Lemoine.Cnc.In.FileString")
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
      data.Clear ();
      keyNotFound = false;
      obsoleteFile = false;
      
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
          obsoleteFile = true;
          return;
        }
        
        using (System.IO.StreamReader streamReader = System.IO.File.OpenText (this.FileName))
        {
          fileError = false;
          while (false == streamReader.EndOfStream) {
            string line = streamReader.ReadLine ();
            if (line.StartsWith (this.Comment)) {
              log.DebugFormat ("Start: " +
                               "{0} is a comment",
                               line);
              continue;
            }
            string [] values = line.Split (this.separators.ToCharArray (),
                                           StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < values.Length-1; ++i) {
              log.DebugFormat ("Start: " +
                               "got {0}={1}",
                               values [i], values [i+1]);
              data [values [i]] = values [i+1];
            }
          }
        }
      }
      catch (Exception ex) {
        log.ErrorFormat ("Start: " +
                         "following exception occurred: {0}, " +
                         "=> set the error property to false",
                         ex);
        fileError = true;
        throw;
      }
    }
    
    /// <summary>
    /// Get the string value of a corresponding key
    /// </summary>
    /// <param name="param">key value</param>
    /// <returns></returns>
    public string GetString (string param)
    {
      if (data.Contains (param)) {
        log.DebugFormat ("GetString: " +
                         "get {0} for key {1}",
                         data [param], param);
        return data [param] as string;
      }
      else {
        log.ErrorFormat ("GetString: " +
                         "could not get key {0}",
                         param);
        keyNotFound = true;
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
    #endregion
  }
}
