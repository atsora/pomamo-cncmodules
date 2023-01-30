// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections.Generic;
using Lemoine.Core.Log;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Description of FileParser.
  /// </summary>
  internal sealed class FileParser
  {
    #region Members
    readonly ILog m_log = null;
    readonly string m_fileName = "";
    readonly IDictionary<string, IList<string>> m_sortedContent = new Dictionary<string, IList<string>> ();
    #endregion // Members

    #region Constructors
    /// <summary>
    /// Description of the constructor
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="fileName"></param>
    /// <param name="fileContent"></param>
    public FileParser (ILog logger, string fileName, string fileContent)
    {
      // Keep the logger and the fileName somewhere
      m_log = logger;
      m_fileName = fileName;

      // Parse the content of the file
      var lines = fileContent.Split (new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
      foreach (var line in lines) {
        var parts = line.Split (',');
        if (parts.Length < 2) {
          m_log.Warn ($"FileParser: Couldn't use line {line} of file {m_fileName}");
        }
        else {
          var symbol = parts[0];
          if (m_sortedContent.ContainsKey (symbol)) {
            m_log.Error ($"FileParser: Symbol {symbol} is specified several times in file {m_fileName}");
          }
          else {
            // Store data
            m_sortedContent[symbol] = new List<string> ();
            for (int i = 1; i < parts.Length; i++) {
              m_sortedContent[symbol].Add (parts[i].Trim (' '));
            }
          }
        }
      }
    }
    #endregion // Constructors

    #region Methods
    /// <summary>
    /// Get a string
    /// </summary>
    /// <param name="symbol"></param>
    /// <param name="position"></param>
    /// <returns></returns>
    public string GetString (string symbol, int position)
    {
      if (!m_sortedContent.ContainsKey (symbol)) {
        m_log.Error ($"FileParser.GetString: {symbol} not found in {m_fileName}");
        throw new Exception ("Symbol not found");
      }

      if (position >= m_sortedContent[symbol].Count) {
        m_log.Error ($"FileParser.GetString: invalid position {position}, only {m_sortedContent[symbol].Count} elements");
        throw new Exception ("Invalid position");
      }

      return m_sortedContent[symbol][position];
    }

    /// <summary>
    /// Get the element list associated to a symbol
    /// </summary>
    /// <param name="symbol"></param>
    /// <returns></returns>
    public IList<string> GetStringList (string symbol)
    {
      if (!m_sortedContent.ContainsKey (symbol)) {
        m_log.Error ($"FileParser.GetStringList: {symbol} not found in {m_fileName}");
        throw new Exception ("Symbol not found");
      }

      return m_sortedContent[symbol];
    }

    /// <summary>
    /// Get all the data
    /// </summary>
    /// <returns></returns>
    public IDictionary<string, IList<string>> GetFullContent ()
    {
      return m_sortedContent;
    }
    #endregion // Methods
  }
}
