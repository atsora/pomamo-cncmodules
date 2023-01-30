// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Lemoine.Core.Log;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Dictionary string-string based on a file
  /// Associate a message to an alarm identifier
  /// The file must comprise lines in the form {CODE}\t{MESSAGE}
  /// </summary>
  public class FileDictionary
  {
    #region Members
    readonly IDictionary<string, string> m_messages = new Dictionary<string, string> ();
    readonly IDictionary<string, IDictionary<string, string>> m_attributes =
      new Dictionary<string, IDictionary<string, string>> ();
    readonly IDictionary<string, string> m_types = new Dictionary<string, string> ();
    #endregion // Members

    static readonly ILog log = LogManager.GetLogger (typeof (FileDictionary).FullName);

    #region Getters / Setters
    /// <summary>
    /// True if an error occured during the reading of the file
    /// </summary>
    public bool Error { get; private set; }
    #endregion // Getters / Setters

    #region Methods
    /// <summary>
    /// Parse a file
    /// </summary>
    /// <param name="filePath">Path of the file to parse</param>
    /// <param name="embedded">true if the file to parse is embedded in the dll</param>
    public void ParseFile (string filePath, bool embedded)
    {
      // Read the content of the file
      string content = "";
      if (embedded) {
        // In a file of this assembly
        using (Stream stream = Assembly.GetExecutingAssembly ()
               .GetManifestResourceStream ("Lemoine.Cnc.AlarmProcessing." + filePath)) {
          using (var reader = new StreamReader (stream)) {
            content = reader.ReadToEnd ();
          }
        }
      }
      else {
        // Somewhere in the computer
        using (var file = new StreamReader (filePath)) {
          content = file.ReadToEnd ();
        }
      }

      ParseContent (content);
    }

    /// <summary>
    /// Parse a text to create alarm translations
    /// </summary>
    /// <param name="content"></param>
    public void ParseContent (string content)
    {
      // Fill the dictionary
      var lines = content.Split (new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
      foreach (string line in lines) {
        // Parse the line
        var elements = line.Split (new[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);

        // Tests
        if (elements.Length < 2) {
          log.WarnFormat ("AlarmProcessing.FileDictionary: bad entry {0}", line);
          continue;
        }
        string alarmCode = ProcessCode (elements[0]);
        string alarmMessage = elements[1];
        if (m_messages.ContainsKey (alarmCode)) {
          log.InfoFormat ("AlarmProcessing.FileDictionary: key '{0}' will change from {1} to {2}",
                         alarmCode, m_messages[alarmCode], elements[1]);
        }

        // Add the definition
        m_messages[alarmCode] = alarmMessage;

        // Attributes or type?
        for (int i = 2; i < elements.Length; i++) {
          var elts = elements[i].Split ('=');
          if (elts.Length >= 2) {
            for (int j = 2; j < elts.Length; j++) {
              elts[1] += "=" + elts[j];
            }

            if (elts[0].Equals ("type", StringComparison.InvariantCultureIgnoreCase)) {
              m_types[alarmCode] = elts[1];
            }
            else {
              if (!m_attributes.ContainsKey (alarmCode)) {
                m_attributes[alarmCode] = new Dictionary<string, string> ();
              }

              m_attributes[alarmCode][elts[0]] = elts[1];
            }
          }
          else {
            log.WarnFormat ("AlarmProcessing.FileDictionary: cannot process attribute {0}", elements[i]);
          }
        }
      }

      log.DebugFormat ("AlarmProcessing.FileDictionary: successfully parsed {0} definitions",
                      m_messages.Count);
    }

    /// <summary>
    /// Clear all translations
    /// </summary>
    public void Clear ()
    {
      m_messages.Clear ();
      m_attributes.Clear ();
      m_types.Clear ();
    }

    /// <summary>
    /// Get the translation found in the dictionary
    /// </summary>
    /// <param name="code"></param>
    /// <returns></returns>
    public string GetTranslation (string code)
    {
      code = ProcessCode (code);
      return m_messages.ContainsKey (code) ? m_messages[code] : null;
    }

    /// <summary>
    /// Get the attributes found in the file
    /// </summary>
    /// <param name="code"></param>
    /// <returns></returns>
    public IDictionary<string, string> GetAttributes (string code)
    {
      code = ProcessCode (code);
      return m_attributes.ContainsKey (code) ? m_attributes[code] : null;
    }

    /// <summary>
    /// Get the alarm type found in the file
    /// </summary>
    /// <param name="code"></param>
    /// <returns></returns>
    public string GetType (string code)
    {
      code = ProcessCode (code);
      return m_types.ContainsKey (code) ? m_types[code] : null;
    }

    /// <summary>
    /// Adapt the code to browse the translations
    /// </summary>
    /// <param name="code"></param>
    /// <returns></returns>
    string ProcessCode (string code)
    {
      // Everything in lower case
      // Remove the leading '0', ' '
      return code.ToLower ().TrimStart (new[] { '0', ' ' });
    }
    #endregion // Methods
  }
}
