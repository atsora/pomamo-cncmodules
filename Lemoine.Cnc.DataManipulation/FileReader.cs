// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.IO;
using System.Reflection;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Class that reads the content of a file
  /// </summary>
  public sealed class FileReader : Lemoine.Cnc.BaseCncModule, Lemoine.Cnc.ICncModule, IDisposable
  {
    #region Members
    string m_content = null;
    string m_path = null;
    #endregion // Members

    #region Getters / Setters
    /// <summary>
    /// Path of the file
    /// </summary>
    public string FilePath
    {
      get { return m_path; }
      set
      {
        if (String.IsNullOrEmpty (value)) {
          log.ErrorFormat ("DataManipulation.FileReader: path is empty");
          Error = true;
        }
        else {
          // Reset the error state
          Error = false;

          // Try to find the full path of the file
          m_path = value;
          if (File.Exists (m_path)) {
            log.Info ($"DataManipulation.FileReader: use path {m_path}");
          }
          else {
            m_path = "";
            log.Error ($"DataManipulation.FileReader: couldn't find path {value}");
            Error = true;
          }
        }
      }
    }

    /// <summary>
    /// Get the content of the file
    /// </summary>
    public string Content
    {
      get
      {
        if (Error) {
          log.ErrorFormat ("DataManipulation.FileReader: cannot read {0} because of an error state", FilePath);
          return "";
        }

        if (m_content == null) {
          ReadContent ();
        }

        return m_content;
      }
    }

    /// <summary>
    /// Return true if the file cannot be read
    /// </summary>
    public bool Error { get; private set; }
    #endregion // Getters / Setters

    #region Constructors / Destructor
    /// <summary>
    /// Description of the constructor
    /// </summary>
    public FileReader () : base ("Lemoine.Cnc.InOut.FileReader")
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
    void ReadContent ()
    {
      m_content = "";
      try {
        log.InfoFormat ("FileReader: reading content of path {0}", FilePath);
        m_content = File.ReadAllText (FilePath);
      }
      catch (Exception e) {
        Error = true;
        log.Error ($"DataManipulation.FileReader: error while reading {FilePath}", e);
      }
    }
    #endregion // Methods
  }
}
