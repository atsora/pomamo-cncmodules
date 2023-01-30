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
  /// Numatix CNC
  /// </summary>
  public class Numatix: Lemoine.Cnc.BaseCncModule, Lemoine.Cnc.ICncModule, IDisposable
  {
    /// <summary>
    /// Default possible separators between the key and the value
    /// </summary>
    static readonly string DEFAULT_SEPARATORS = ":";
    /// <summary>
    /// Default string that marks a line as a comment
    /// </summary>
    static readonly string DEFAULT_COMMENT = "#";
    /// <summary>
    /// Default actual feedrate key to get the running status
    /// </summary>
    static readonly string DEFAULT_FEEDRATE_KEY = "FEED ACTUAL";
    /// <summary>
    /// Default USER INFO key to get additional data like the operation ID
    /// </summary>
    static readonly string DEFAULT_USER_INFO_KEY = "USER INFO";
    /// <summary>
    /// Default USER INFO main separators between the key/value pairs
    /// </summary>
    static readonly string DEFAULT_USER_INFO_MAIN_SEPARATORS = ";";
    /// <summary>
    /// Default separators used between the key and the value in the USER INFO field
    /// </summary>
    static readonly string DEFAULT_USER_INFO_KEY_VALUE_SEPARATORS = ":";
    
    #region Members
    string fileName;
    double timeout = 10; // 10 s by default
    string separators = DEFAULT_SEPARATORS;
    string comment = DEFAULT_COMMENT;
    string feedrateKey = DEFAULT_FEEDRATE_KEY;
    string userInfoKey = DEFAULT_USER_INFO_KEY;
    string userInfoMainSeparators = DEFAULT_USER_INFO_MAIN_SEPARATORS;
    string userInfoKeyValueSeparators = DEFAULT_USER_INFO_KEY_VALUE_SEPARATORS;
    
    bool fileError = false;
    bool keyNotFound = false;
    string header = "";
    DateTime updateTime = DateTime.UtcNow;
    
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
    /// Default is 10s.
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
    /// Feedrate key to get the running status
    /// 
    /// Default is "FEED ACTUAL"
    /// </summary>
    public string FeedrateKey {
      get { return feedrateKey; }
      set { feedrateKey = value.Trim (); }
    }
    
    /// <summary>
    /// USER INFO key to get additionnal information like the operation ID
    /// </summary>
    public string UserInfoKey {
      get { return userInfoKey; }
      set { userInfoKey = value.Trim (); }
    }
    
    /// <summary>
    /// Main separators used in the USER INFO field between the key/value pairs
    /// </summary>
    public string UserInfoMainSeparators {
      get { return userInfoMainSeparators; }
      set { userInfoMainSeparators = value; }
    }
    
    /// <summary>
    /// Separators used between the key and the value in the USER INFO field
    /// </summary>
    public string UserInfoKeyValueSeparators {
      get { return userInfoKeyValueSeparators; }
      set { userInfoKeyValueSeparators = value; }
    }
    
    /// <summary>
    /// Has the file been parsed successfully ?
    /// </summary>
    public bool FileError {
      get { return fileError; }
    }
    
    /// <summary>
    /// Has a key not been found in the file ?
    /// </summary>
    public bool KeyNotFound {
      get { return keyNotFound; }
    }
    
    /// <summary>
    /// Is the machine running ?
    /// 
    /// Yes if there is a feedrate in the file
    /// or if the file has not been updated for a long time
    /// </summary>
    public bool Running {
      get
      {
        if (true == FileError) {
          log.ErrorFormat ("Running.get: " +
                           "FileError");
          throw new Exception ("FileError");
        }
        
        string feedString = GetString (DEFAULT_FEEDRATE_KEY);
        
        if ( (feedString.Length > 0)
            && (!feedString.Equals ("0.00"))) {
          // Real feedrate => Running
          log.DebugFormat ("Running.get: " +
                           "got real feedrate {0} => Running",
                           feedString);
          return true;
        }
        else { // 2 cases: the file has been updated recently or not
          TimeSpan age = DateTime.UtcNow - updateTime;
          if (age.TotalSeconds < 0) {
            log.FatalFormat ("Running.get: " +
                             "unexpected time results, " +
                             "updateTime {0} UTC is greater than now {1} UTC",
                             updateTime, DateTime.UtcNow);
            throw new Exception ("BadTimeResults");
          }
          else if (age.TotalSeconds < this.Timeout) {
            // The file has been updated recently
            // => real idle status
            log.DebugFormat ("Running.get: " +
                             "recent file with idle status " +
                             "=> real idle status " +
                             "age is {0}",
                             age);
            return false;
          }
          else {
            // The file has not been updated for a long time
            // => we suppose the machine is running
            log.DebugFormat ("Running.get: " +
                             "old file with idle status " +
                             "=> suppose the machine is running",
                             age);
            return true;
          }
        }
      }
    }
    #endregion

    #region Constructors / Destructor / ToString methods
    /// <summary>
    /// Description of the constructor
    /// </summary>
    public Numatix ()
      : base("Lemoine.Cnc.In.Numatix")
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
      
      try {
        using (System.IO.StreamReader streamReader = System.IO.File.OpenText (this.FileName))
        {
          fileError = false;
          
          // Header
          string line = streamReader.ReadLine ();
          if (!line.Equals (header)) {
            // Update updateTime only if the header changed
            log.DebugFormat ("Start: " +
                             "header was updated from {0} to {1}",
                             header, line);
            header = line;
            updateTime = DateTime.UtcNow;
          }
          
          while (false == streamReader.EndOfStream) {
            line = streamReader.ReadLine ();
            if (line.StartsWith (this.Comment, StringComparison.InvariantCultureIgnoreCase)) {
              log.DebugFormat ("Start: " +
                               "{0} is a comment",
                               line);
              continue;
            }
            string [] values = line.Split (this.Separators.ToCharArray (),
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
              data [""] = values [0].Trim ();
            }
            else {
              log.DebugFormat ("Start: " +
                               "in line {0} got key/value {1}={2}",
                               line, values [0], values [1]);
              string k = values [0].Trim ();
              string v = values [1].Trim ();
              if (k.Equals (UserInfoKey)) { // User info field
                string [] userInfoValues = v.Split (this.UserInfoMainSeparators.ToCharArray (),
                                                    StringSplitOptions.RemoveEmptyEntries);
                foreach (string userInfoValue in userInfoValues) {
                  string [] keyvalues = userInfoValue.Split (this.UserInfoKeyValueSeparators.ToCharArray (),
                                                             2,
                                                             StringSplitOptions.RemoveEmptyEntries);
                  if (keyvalues.Length < 1) {
                    log.InfoFormat ("Start: " +
                                    "got an empty USER INFO in line {0}",
                                    line);
                  }
                  else if (keyvalues.Length < 2) {
                    log.InfoFormat ("Start: " +
                                    "not key/value USER INFO pair in line {0}, " +
                                    "got only {1}, " +
                                    "=> add {2}={3}",
                                    line, userInfoValue,
                                    k, userInfoValue);
                    data [k] = userInfoValue;
                  }
                  else {
                    log.DebugFormat ("Start: " +
                                     "ine line {0} got USER INFO key/value {1}={2}",
                                     line, keyvalues [0], keyvalues [1]);
                    data [keyvalues [0]] = keyvalues [1];
                  }
                }
              }
              else {
                data [k] = v;
              }
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
      string trimmedParam = param.Trim ();
      if (data.Contains (trimmedParam)) {
        log.DebugFormat ("GetString: " +
                         "get {0} for key {1}",
                         data [trimmedParam], param);
        return data [trimmedParam] as string;
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
      string s = this.GetString(param);
      if (s.Equals("T")) {
        return true;
      }
      else if (s.Equals("F")) {
        return false;
      }
      else {
        return bool.Parse (s);
      }
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
