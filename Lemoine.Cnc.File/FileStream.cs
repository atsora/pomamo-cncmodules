// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Lemoine.Collections;
using Lemoine.Core.Log;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Description of FileStream.
  /// </summary>
  public class FileStream : Lemoine.Cnc.BaseCncModule, Lemoine.Cnc.ICncModule, IDisposable
  {
    /// <summary>
    /// Default possible separators between the key and the value
    /// </summary>
    static readonly string DEFAULT_SEPARATORS = "=: ";
    /// <summary>
    /// Default number of lines after which the file is deleted
    /// </summary>
    static readonly int DEFAULT_DELETE_EVERY = 200;
    /// <summary>
    /// Default max size of a file to read on network. If bigger, move it locally and read it locally
    /// hint : DEFAULT_DELETE_EVERY * 80
    /// </summary>
    static readonly int DEFAULT_MAX_FILE_SIZE = 1600;


    #region Members
    string m_filePath;
    string m_separators = DEFAULT_SEPARATORS;
    bool m_first = false;
    int m_deleteEvery = DEFAULT_DELETE_EVERY;
    int m_maxFileSize = DEFAULT_MAX_FILE_SIZE;
    bool m_rename = true;

    int m_checkpoint = -1;
    int m_lineNumber = 0;
    string m_firstLine = null;
    bool m_fileError = false;
    System.IO.Stream m_stream = null;
    IDictionary<string, StandardQueue<string>> m_data = new Dictionary<string, StandardQueue<string>> ();
    // StandardQueue can be replaced by IQueue once IQueue implements IEnumerable
    IDictionary<string, string> m_current = new Dictionary<string, string> ();
    #endregion // Members

    #region Getters / Setters
    /// <summary>
    /// Path of the file to read
    /// </summary>
    public string FilePath
    {
      get { return m_filePath; }
      set { m_filePath = value; }
    }

    /// <summary>
    /// String with the possible separator characters between the key and the value
    /// </summary>
    public string Separators
    {
      get { return m_separators; }
      set { m_separators = value; }
    }

    /// <summary>
    /// When a single element is requested, if true, return the first element in the queue, else only the last one
    /// and discard the others
    /// 
    /// Default is False
    /// </summary>
    public bool First
    {
      get { return m_first; }
      set { m_first = value; }
    }

    /// <summary>
    /// Delete the file every the specified number of lines
    /// 
    /// Default is 200
    /// </summary>
    public int DeleteEvery
    {
      get { return m_deleteEvery; }
      set { m_deleteEvery = value; }
    }

    /// <summary>
    /// Rename the file before deleting it
    /// </summary>
    public bool Rename
    {
      get { return m_rename; }
      set { m_rename = value; }
    }

    /// <summary>
    /// Has the file been parsed successfully ?
    /// </summary>
    public bool FileError
    {
      get { return m_fileError; }
    }
    #endregion // Getters / Setters

    #region Constructors / Destructor / ToString methods
    /// <summary>
    /// Description of the constructor
    /// </summary>
    public FileStream ()
      : base ("Lemoine.Cnc.In.FileStream")
    {
    }

    /// <summary>
    /// <see cref="IDisposable.Dispose" />
    /// </summary>
    public void Dispose ()
    {
      if (null != m_stream) {
        CloseStream ();
        // Store m_checkpoint and m_data into files (permanent storage)
        StoreCheckpoint ();
        StoreData ();
      }

      GC.SuppressFinalize (this);
    }

    #endregion // Constructors / Destructor / ToString methods

    #region Methods
    /// <summary>
    /// Start method
    /// </summary>
    public void Start ()
    {
      Initialize ();
      Read ();
    }

    /// <summary>
    /// Make some initialization
    /// </summary>
    void Initialize ()
    {
      // Initialize m_checkpoint
      if (-1 == m_checkpoint) {
        m_checkpoint = 0;
        InitializeCheckpoint ();
        InitializeData ();
      }
    }

    /// <summary>
    /// Read the file
    /// </summary>
    void Read ()
    {
      m_fileError = false;
      string localPath = null;
      string previousFilePath = null;
      try {
        // Open the stream if it has not been opened yet
        if (null == m_stream) {
          if (log.IsDebugEnabled) {
            log.Debug ($"Read: open a stream reader for {m_filePath}");
          }
          m_stream = System.IO.File.Open (m_filePath, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite);
          if (m_maxFileSize < m_stream.Length) {
            if (log.IsDebugEnabled) {
              log.Debug ($"Read: file size greater than {m_stream.Length}. Move and read locally");
            }
            localPath = GetLocalCopyFilePath ();
            if (MoveFileToLocal (localPath)) {
              previousFilePath = m_filePath;
              m_filePath = localPath;
              if (log.IsDebugEnabled) {
                log.Debug ($"Read: open a stream reader for {localPath}");
              }
              m_stream = System.IO.File.Open (m_filePath, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite);
            }
            else {
              if (log.IsDebugEnabled) {
                log.Debug ($"Read: unable to move file locally, process anyway remotely");
              }
            }
          }
        }

        // Read the data
        using (System.IO.StreamReader streamReader = new System.IO.StreamReader (m_stream)) {
          while (false == streamReader.EndOfStream) {
            string line = streamReader.ReadLine ();
            ++m_lineNumber;
            if (m_lineNumber <= m_checkpoint) {
              if ((1 == m_lineNumber)
                  && (null != m_firstLine)
                  && !object.Equals (m_firstLine, line)) {
                log.ErrorFormat ("Read: " +
                                 "this is another file, you can't consider the checkpoint " +
                                 "=> read everything");
                m_checkpoint = 0;
              }
              else {
                if (log.IsDebugEnabled) {
                  log.DebugFormat ("Read: " +
                                   "skip line {0} because the line number {1} is less than checkpoint {2}",
                                   line, m_lineNumber, m_checkpoint);
                }
                continue;
              }
            }
            if (1 == m_lineNumber) {
              m_firstLine = line;
            }
            ParseLine (line);
            // Record a checkpoint
            m_checkpoint = m_lineNumber;
          }
        }
      }
      catch (Exception ex) {
        log.ErrorFormat ("Read: " +
                         "following exception occurred: {0}, " +
                         "=> set the error property to false",
                         ex);
        m_fileError = true;
        CloseStream ();
        if (null != previousFilePath) {
          m_filePath = previousFilePath;
        }
        if (ex.GetType ().Name != "System.IO.FileNotFoundException") {
          throw ex;
        }
      }

      // TODO: keep a lock on the file between the read time and the delete time ?
      // What is the best way to do it ? Rename the file instead of deleting it ?

      // Delete the file if enough lines have already been run
      if (m_deleteEvery < m_lineNumber) {
        log.InfoFormat ("Read: " +
                        "delete the file because already {0} lines have been read",
                        m_lineNumber);
        CloseAndDelete ();
      }
      if (null != previousFilePath) {
        m_filePath = previousFilePath;
      }
    }

    void ParseLine (string line)
    {
      string[] values = line.Split (this.m_separators.ToCharArray (),
                                     2,
                                     StringSplitOptions.RemoveEmptyEntries);
      if (values.Length < 1) {
        if (log.IsDebugEnabled) {
          log.Debug ($"Read: got an empty data in line {line}");
        }
      }
      else if (values.Length < 2) {
        log.Warn ($"Read: no key/value pair in line {line}, got only {values[0]}");
        PushData ("", values[0].Trim ());
      }
      else {
        if (log.IsDebugEnabled) {
          log.Debug ($"Read: in line {line} got key/value {values[0]}={values[1]}");
        }
        PushData (values[0].Trim (), values[1].Trim ());
      }
    }

    /// <summary>
    /// Finish: store the checkpoint
    /// </summary>
    public void Finish ()
    {
      StoreCheckpoint ();
      StoreData ();
    }

    string GetFilePath (string prefix)
    {
      string path = string.Format ("{0}-{1}",
                                   prefix, this.CncAcquisitionId);
      string directory = Lemoine.Info.PulseInfo.LocalConfigurationDirectory;
      if (log.IsDebugEnabled) {
        log.Debug ($"GetFilePath: prefix={prefix} directory={directory}");
      }
      if (!Directory.Exists (directory)) {
        Directory.CreateDirectory (directory);
      }
      return Path.Combine (directory, path);
    }

    string GetCheckpointFilePath ()
    {
      return GetFilePath ("FileStreamCheckpoint");
    }

    string GetFirstLineFilePath ()
    {
      return GetFilePath ("FileStreamFirstLine");
    }

    string GetLocalCopyFilePath ()
    {
      return GetFilePath ("FileStreamLocalCopy");
    }

    void StoreCheckpoint ()
    {

      string checkpointPath = GetCheckpointFilePath ();
      string firstLinePath = GetFirstLineFilePath ();
      if (log.IsDebugEnabled) {
        log.Debug ($"StoreCheckPoint: {checkpointPath} {m_checkpoint}");
      }
      try {
        System.IO.File.WriteAllText (checkpointPath, m_checkpoint.ToString ());
        if (null == m_firstLine) {
          System.IO.File.Delete (firstLinePath);
        }
        else {
          System.IO.File.WriteAllText (firstLinePath, m_firstLine);
        }
      }
      catch (Exception ex) {
        log.Error ($"StoreCheckpoint: saving the checkpoint {m_checkpoint} into {checkpointPath} failed with {ex}");
      }
    }

    void InitializeCheckpoint ()
    {
      string checkpointPath = GetCheckpointFilePath ();
      try {
        string fileContent = System.IO.File.ReadAllText (checkpointPath);
        m_checkpoint = int.Parse (fileContent);

        string firstLinePath = GetFirstLineFilePath ();
        try {
          m_firstLine = System.IO.File.ReadAllText (firstLinePath);
        }
        catch (Exception ex1) {
          log.Error ($"InitializeCheckpoint: reading and parsing file {firstLinePath} failed with {ex1}");
        }
      }
      catch (Exception ex) {
        log.Error ($"InitializeCheckpoint: reading and parsing file {checkpointPath} failed with {ex}");
      }
    }

    string GetDataFilePath ()
    {
      return GetFilePath ("FileStreamData");
    }

    void StoreData ()
    {
      string path = GetDataFilePath ();
      try {
        using (System.IO.StreamWriter writer = new System.IO.StreamWriter (path, false)) {
          foreach (KeyValuePair<string, StandardQueue<string>> keyQueue in m_data) {
            writer.WriteLine ("*" + keyQueue.Key);
            StandardQueue<string> queue = keyQueue.Value;
            foreach (string v in queue) {
              writer.WriteLine (v);
            }
          }
        }
      }
      catch (Exception ex) {
        log.Error ($"StoreQueue: saving the queue into {path} failed with {ex}");
      }
    }

    void InitializeData ()
    {
      string path = GetDataFilePath ();
      try {
        using (System.IO.StreamReader reader = new System.IO.StreamReader (path)) {
          StandardQueue<string> queue = null;
          while (!reader.EndOfStream) {
            string line = reader.ReadLine ();
            if (line.StartsWith ("*")) {
              string key = line.Substring (1);
              queue = new StandardQueue<string> ();
              m_data[key] = queue;
            }
            else {
              Debug.Assert (null != queue);
              if (null == queue) {
                log.Fatal ("InitializeData: no key was defined previously");
              }
              else {
                queue.Enqueue (line);
              }
            }
          }
        }
      }
      catch (Exception ex) {
        log.Error ($"InitializeData: reading and parsing file {path} failed with {ex}");
      }
    }

    void CloseAndDelete ()
    {
      try {
        CloseStream ();
        if (m_rename) {
          //
          string deletePath = m_filePath + ".del";
          // remove .del file if already exists
          if (File.Exists (deletePath)) {
            if (log.IsDebugEnabled) {
              log.Debug ($"CloseAndDelete: remove already existing file {deletePath}");
            }
            File.Delete (deletePath);
          }

          if (log.IsDebugEnabled) {
            log.Debug ($"CloseAndDelete: moving file {m_filePath} to {deletePath}");
          }
          System.IO.File.Move (m_filePath, deletePath);
          ReadOnlyLines (deletePath);
          if (log.IsDebugEnabled) {
            log.Debug ($"CloseAndDelete: deleting file {deletePath}");
          }
          System.IO.File.Delete (deletePath);
        }
        else {
          if (log.IsDebugEnabled) {
            log.Debug ($"CloseAndDelete: deleting file {m_filePath}");
          }
          System.IO.File.Delete (m_filePath);
        }
        m_checkpoint = 0;
        m_firstLine = null;
      }
      catch (Exception ex) {
        log.Error ($"CloseAndDelete: deleting file {m_filePath} failed with {ex}");
      }
    }
    bool MoveFileToLocal (string localPath)
    {
      bool result = false;
      try {
        //
        string renamePath = m_filePath + ".ren";
        // remove .ren file if already exists
        if (File.Exists (renamePath)) {
          if (log.IsDebugEnabled) {
            log.Debug ($"MoveFileToLocal: remove already existing file {renamePath}");
          }
          File.Delete (renamePath);
        }
        // remove local file if already exists
        if (File.Exists (localPath)) {
          if (log.IsDebugEnabled) {
            log.Debug ($"MoveFileToLocal: remove already existing file {localPath}");
          }
          File.Delete (localPath);
        }
        CloseStream ();
        // First rename file remotely, then move renamed file
        if (log.IsDebugEnabled) {
          log.Debug ($"MoveFileToLocal: move file {m_filePath} to {renamePath}");
        }
        System.IO.File.Move (m_filePath, renamePath);
        if (log.IsDebugEnabled) {
          log.Debug ($"MoveFileToLocal: move file {renamePath} to {localPath}");
        }
        System.IO.File.Move (renamePath, localPath);
        result = true;
      }
      catch (Exception ex) {
        log.Error ($"MoveFileToLocal: moving file {m_filePath} to {localPath} failed with {ex}");
      }
      return result;
    }

    void ReadOnlyLines (string path)
    {
      try {
        if (log.IsDebugEnabled) {
          log.Debug ("ReadOnlyLines");
        }
        using (System.IO.StreamReader streamReader = new System.IO.StreamReader (path)) {
          while (!streamReader.EndOfStream) {
            string line = streamReader.ReadLine ();
            ParseLine (line);
          }
        }
      }
      catch (Exception ex) {
        log.Error ($"ReadOnlyLines: error occurred when reading {path}, {ex}");
      }
    }

    void CloseStream ()
    {
      if (null != m_stream) {
        m_stream.Dispose ();
        m_stream = null;
        m_lineNumber = 0;
      }
    }

    void PushData (string key, string v)
    {
      // Create the queue associated to key if it does not exist
      StandardQueue<string> queue;
      if (!m_data.TryGetValue (key, out queue)) {
        queue = new Lemoine.Collections.StandardQueue<string> ();
        m_data[key] = queue;
      }
      Debug.Assert (null != queue);

      // Push the data into the queue
      queue.Enqueue (v);
    }

    /// <summary>
    /// Get the queue of the retrieved values for the specified key
    /// </summary>
    /// <param name="param">key value</param>
    /// <returns></returns>
    public IQueue<string> GetQueue (string param)
    {
      return m_data[param];
    }

    /// <summary>
    /// Dequeue the first string for a specified key and return it
    /// </summary>
    /// <param name="param">key value</param>
    /// <returns></returns>
    public string GetFirstString (string param)
    {
      IQueue<string> queue = GetQueue (param);
      if (null == queue) {
        if (log.IsDebugEnabled) {
          log.Debug ("GetFirstString: the queue was null => return the current value");
        }
        return m_current[param];
      }

      string v = queue.Dequeue ();
      m_current[param] = v;
      if (log.IsDebugEnabled) {
        log.Debug ($"GetFirstString: got {param}={v}");
      }
      return v;
    }

    /// <summary>
    /// Dequeue all the items for a specified key and return the last one
    /// </summary>
    /// <param name="param">key value</param>
    /// <returns></returns>
    public string GetLastString (string param)
    {
      IQueue<string> queue = GetQueue (param);
      if ((null == queue) || (0 == queue.Count)) {
        log.Error ("GetLastString: empty or null queue => return the current value");
        return m_current[param];
      }
      else {
        string v = null;
        while (0 < queue.Count) {
          v = queue.Dequeue ();
        }
        Debug.Assert (null != v);
        if (log.IsDebugEnabled) {
          log.Debug ($"GetLastString: got {param}={v}");
        }
        m_current[param] = v;
        return v;
      }
    }

    /// <summary>
    /// Get the value that is associated to a specified key according to the parameter First
    /// </summary>
    /// <param name="param">key value</param>
    /// <returns></returns>
    public string GetString (string param)
    {
      if (m_first) {
        return GetFirstString (param);
      }
      else {
        return GetLastString (param);
      }
    }

    /// <summary>
    /// Get the int value of a corresponding key
    /// </summary>
    /// <param name="param">key value</param>
    /// <returns></returns>
    public int GetInt (string param) => int.Parse (this.GetString (param));

    /// <summary>
    /// Get the long value of a corresponding key
    /// </summary>
    /// <param name="param">key value</param>
    /// <returns></returns>
    public long GetLong (string param) => long.Parse (this.GetString (param));

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
      return int.Parse (this.GetString (param).TrimEnd (new char[] { '%' }));
    }
    /// <summary>
    /// Get a set of values for the keys in a list string
    /// </summary>
    /// <param name="param">list string of values to get</param>
    /// <returns></returns>
    public IDictionary<string, long> GetLongSet (string param) => GetSet (param, GetLong);

    /// <summary>
    /// Get a set of values for the keys in a list string
    /// </summary>
    /// <param name="param">list string of values to get</param>
    /// <returns></returns>
    public IDictionary<string, int> GetIntSet (string param) => GetSet (param, GetInt);

    /// <summary>
    /// Get a set of values for the keys in a list string
    /// </summary>
    /// <param name="param">list string of values to get</param>
    /// <returns></returns>
    public IDictionary<string, double> GetDoubleSet (string param) => GetSet (param, GetDouble);

    /// <summary>
    /// Get a set of values for the keys in a list string
    /// </summary>
    /// <param name="param">list string of values to get</param>
    /// <returns></returns>
    IDictionary<string, T> GetSet<T> (string param, Func<string, T> get)
    {
      try {
        var keys = Lemoine.Collections.EnumerableString.ParseListString (param);
        return GetEnumerable<T> (keys, get)
          .ToDictionary (x => x.Key, x => x.Value);
      }
      catch (Exception ex) {
        log.Error ($"GetSet: param={param}", ex);
        throw;
      }
    }

    IEnumerable<KeyValuePair<string, T>> GetEnumerable<T> (IEnumerable<string> keys, Func<string, T> get)
    {
      foreach (var key in keys) {
        T v;
        try {
          v = get (key);
        }
        catch (Exception ex) {
          log.Error ($"GetEnumerable: exception for key={key}", ex);
          continue;
        }
        yield return new KeyValuePair<string, T> (key, v);
      }
    }
    #endregion // Methods
  }
}
