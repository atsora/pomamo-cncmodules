// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using Lemoine.Net;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Class to get the data from a file, for example the operation ID.
  /// </summary>
  public class FileProgram: Lemoine.Cnc.BaseCncModule, Lemoine.Cnc.ICncModule, IDisposable
  {
    
    static readonly string DEFAULT_SHARED_FOLDER_USER = "makino";
    static readonly string DEFAULT_SHARED_FOLDER_PASSWORD = "MAKINO-ACU";
    static readonly string DEFAULT_SHARED_FOLDER_DOMAIN = "GLOBAL";
    static readonly string DEFAULT_COMMENT_SEARCH_STRING = "(POMAMO PART=";

    #region Members
    readonly Stack m_stack = new Stack ();

    string m_currentProgramNumber = null;
    string m_currentSubProgramNumber = null;
    string m_currentProgramComment = null;
    string m_currentMainCommentLine = null;

    UNCAccess m_unc = null;
    string m_sharedProgramFolderUser = null;
    string m_sharedProgramFolderPassword = null;
    string m_sharedProgramFolderDomain = null;
    bool m_sharedProgramFolderLoginDone = false;

    Dictionary<string, string> m_subprogramList = new Dictionary<string, string> ();

    #endregion

    #region Getters / Setters    

    /// <summary>
    /// string to identify part comment in program content
    /// 
    /// </summary>
    public string CommentSearchString { get; set; }

    /// <summary>
    /// Path to shared programs folder, w/o trailing separator
    /// 
    /// For example:
    /// \\192.168.1.11\MC_Direct Mode Programs
    /// </summary>
    public string SharedProgramFolder { get; set; }

    /// <summary>
    /// User to connect to shared programs folder
    /// </summary>
    public string SharedProgramFolderUser
    {
      get {
        return (!string.IsNullOrEmpty (m_sharedProgramFolderUser) ? m_sharedProgramFolderUser : DEFAULT_SHARED_FOLDER_USER);
      }
      set {
        m_sharedProgramFolderUser = value;
      }
    }

    /// <summary>
    /// password to connect to shared programs folder
    /// </summary>
    public string SharedProgramFolderPassword
    {
      get {
        return (!string.IsNullOrEmpty (m_sharedProgramFolderPassword) ? m_sharedProgramFolderPassword : DEFAULT_SHARED_FOLDER_PASSWORD);
      }
      set {
        m_sharedProgramFolderPassword = value;
      }
    }

    /// <summary>
    /// domain to connect to shared programs folder
    /// </summary>
    public string SharedProgramFolderDomain
    {
      get {
        return (!string.IsNullOrEmpty (m_sharedProgramFolderDomain) ? m_sharedProgramFolderDomain : DEFAULT_SHARED_FOLDER_DOMAIN);
      }
      set {
        m_sharedProgramFolderDomain = value;
      }
    }

    /// <summary>
    /// Force program content FTP read even if no program number change
    /// 
    /// </summary>
    public string ForceFTPProgramContentRead { get; set; }

    #endregion

    #region Constructors / Destructor / ToString methods
    /// <summary>
    /// Description of the constructor
    /// </summary>
    public FileProgram ()
      : base("Lemoine.Cnc.In.FileProgram")
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
    /// Start method: reset the different values
    /// </summary>
    public void Start ()
    {
      Clear ();
    }

    /// <summary>
    /// Clear the stack
    /// </summary>
    public void Clear ()
    {
      m_stack.Clear ();

    }

    /// <summary>
    /// Push a data in the stack
    /// </summary>
    /// <param name="data">Data to push in the stack</param>
    public void Push (object data)
    {
      if (log.IsDebugEnabled) {
        log.Debug ($"Push: push {data} ({data.GetType()}) in the stack, count={m_stack.Count+1}");
      }
      m_stack.Push (data);
    }

    /// <summary>
    /// Get the program comment from current program in FTP network share
    /// Stack content:
    /// -program number
    /// -program name prefix
    /// -prgoram name extension
    /// -comment string to search
    /// -network path
    /// -username
    /// -password
    /// </summary>
    /// <param name="param"></param>
    public string GetProgramOperationCommentFromFTPFile (string param)
    {
      if (m_stack.Count < 7) {
        log.Error ("GetProgramOperationCommentFromFTPFile: not enough elements in the stack");
        throw new Exception ("Not enough elements in stack");
      }

      string result = null;
      string programNumber = null;
      string programName = null;
      string searchString = "";
      string sharedFolder = "";
      string userName = "";
      string password = "";
      string programFilePath = "";
      log.Debug ($"GetProgramOperationCommentFromFTPFile: param={param}");

      if (m_stack.Count == 7) {
        password = m_stack.Pop ().ToString ();
        userName = m_stack.Pop ().ToString ();
        sharedFolder = m_stack.Pop ().ToString ();
        searchString = m_stack.Pop ().ToString ();
        string programNamesuffix = m_stack.Pop ().ToString ();
        string programNamePrefix = m_stack.Pop ().ToString ();
        programNumber = m_stack.Pop ().ToString ();
        programName = programNamePrefix + programNumber + programNamesuffix;

        log.Debug ($"GetProgramOperationCommentFromFTPFile: programNumber={programNumber}, programName={programName}, searchString={searchString}");
      }
      else {
        log.Error ("GetProgramOperationCommentFromFTPFile: bad param format");
        return null;
      }

      programFilePath = Path.Combine (sharedFolder, programName);
      // if force reload is set to true, get comment from file each time
      if (!string.IsNullOrEmpty (ForceFTPProgramContentRead) && string.Equals (ForceFTPProgramContentRead, "true")) {
        result = GetCommentFromFTPFile (programFilePath, searchString, userName, password);
      }
      else {
        // read content only if programNumber change
        if (programNumber.Equals(m_currentProgramNumber) && !String.IsNullOrEmpty(m_currentProgramComment)) {
          result = m_currentProgramComment;
        }
        else {
          m_currentProgramNumber = programNumber;
          result = GetCommentFromFTPFile (programFilePath, searchString, userName, password);
          m_currentProgramComment = result;
        }
      }
      return result;
    }

    /// <summary>
    /// parse the program file and extract first line containing part comment
    /// The path to the program file folder is passed as parameter
    /// </summary>
    /// <param name="programFilePath">FTP path to ptogram file</param>
    /// <param name="searchString">sting to search in file</param>
    /// <param name="userName">sting to search in file</param>
    /// <param name="password">sting to search in file</param>
    /// <returns></returns>
    public string GetCommentFromFTPFile (string programFilePath, string searchString, string userName, string password)
    {
      log.Debug ($"GetCommentFromFTPFile: programFilePath={programFilePath}, searchString={searchString}");
      string comment = "";
      string commentSearchString = "";
      bool commentFound = false;
      string fileString = "";
      // TODO ??? test file date for change ????

      if (!string.IsNullOrEmpty (searchString)) {
        commentSearchString = searchString;
      }
      else {
        commentSearchString = DEFAULT_COMMENT_SEARCH_STRING;
      }
      log.DebugFormat ("GetCommentFromFTPFile: file: {0} searchString: {1}", programFilePath, commentSearchString);

      var request = new System.Net.WebClient ();
      request.Credentials = new NetworkCredential (userName, password);

      try {
        log.Debug ($"GetCommentFromFTPFile: DownloadData");
        byte[] newFileData = request.DownloadData (programFilePath);
        fileString = System.Text.Encoding.UTF8.GetString (newFileData);
        log.DebugFormat ("GetCommentFromFTPFile: get string, length={0}", fileString.Length);

        var parts = fileString.Split ('\n');
        log.DebugFormat ("GetCommentFromFTPFile: convert result to String list, length={0}", parts.Length);
        for (int i = 0; i < parts.Length; i++) {
          if (parts[i].IndexOf (commentSearchString, 0) != -1) {
            log.DebugFormat ("GetCommentFromFTPFile: line found: {0}", parts[i]);
            comment = parts[i];
            commentFound = true;
            break;
          }
        }
        if (!commentFound) {
          log.DebugFormat ("GetCommentFromFTPFile: no comment line found in: {0}", programFilePath);
        }
        else {
          log.DebugFormat ("GetCommentFromFTPFile: comment: {0}", comment);
        }
        request.Dispose ();
      }
      catch (WebException e) {
        log.ErrorFormat ("GetCommentFromFTPFile: failed to read file: {0}, {1}", programFilePath, e);
        return null;
      }
      return comment;
    }

       
    /// <summary>
    /// parse the program file and extract
    /// -header line with program number
    /// -first line starting with subComment set in CommentSearchString
    /// The program is in a network share provided in SharedProgramFolder properties
    /// The network credentials user/domain/password must be provided as properties
    /// </summary>
    /// <param name="programFileName"></param>
    /// <param name="subProgramName"></param>
    /// <param name="subComment"></param>
    /// <returns></returns>
    public bool GetCommentFromNetworkShareFile (string programFileName, out string subProgramName, out string subComment)
    {
      bool result = false;
      string searchString = DEFAULT_COMMENT_SEARCH_STRING;
      if (!string.IsNullOrEmpty (CommentSearchString)) {
        searchString = CommentSearchString;
      }
      bool commentFound = false;
      bool subNameFound = false;
      subProgramName = null;
      subComment = null;
      log.DebugFormat ("GetCommentFromNetworkShareFile: file: {0}", programFileName);

      try {
        var filePath = Path.Combine (SharedProgramFolder, programFileName);
        if (!m_sharedProgramFolderLoginDone) {
          // establish login for the network share
          m_unc = new UNCAccess ();
          int uncResult = m_unc.Login (SharedProgramFolder, SharedProgramFolderUser, SharedProgramFolderDomain, SharedProgramFolderPassword);
          m_sharedProgramFolderLoginDone = (0 == uncResult);
          log.DebugFormat ("GetCommentFromNetworkShareFile: UNC login to {0}, result={1}", SharedProgramFolder, uncResult);
        }

        log.DebugFormat ("GetCommentFromNetworkShareFile: filePath: {0}", filePath);
        bool firstLineRead = false;
        using (StreamReader reader = new StreamReader (new System.IO.FileStream (filePath, FileMode.Open, FileAccess.Read))) {
          while (!commentFound && !reader.EndOfStream) {
            string line = reader.ReadLine ();
            //log.DebugFormat ("GetCommentFromNetworkShareFile: line: {0}", line);
            if (!firstLineRead) {
              if (5 <= line.Length) {
                subProgramName = line.Substring (0, 5);
                log.DebugFormat ("GetCommentFromNetworkShareFile: subProgramName found: {0}", subProgramName);
                subNameFound = true;
              }
              firstLineRead = true;
            }
            else {
              if (line.IndexOf (searchString, 0) != -1) {
                subComment = line;
                log.DebugFormat ("GetCommentFromNetworkShareFile: subComment found: {0}", subComment);
                commentFound = true;
              }
            }
          }
        }

        if (!commentFound) {
          log.DebugFormat ("GetCommentFromNetworkShareFile: no comment line found in: {0}", programFileName);
        }
        result = subNameFound && commentFound;
      }
      catch (Exception e) {
        log.ErrorFormat ("GetCommentFromNetworkShareFile: failed to read file: {0}, {1}", programFileName, e);
        // reinit login to network share
        m_sharedProgramFolderLoginDone = false;
      }
      return result;
    }

    /// <summary>
    /// parse the program file and extract
    /// </summary>
    public void ReleaseNetworkShareAccess ()
    {
      log.Debug ("ReleaseNetworkShareAccess:");
      if (null != m_unc && m_sharedProgramFolderLoginDone) {
        m_unc.NetUseDelete ();
        m_sharedProgramFolderLoginDone = false;
      }
    }

    /// <summary>
    /// Get the program comment from sub programs
    /// Don't read content again if no program or subprogram change.
    /// Stack 
    /// -currentSubProgramName
    /// -programContent
    /// -SharedProgramFolder, SharedProgramFolderDomain, SharedProgramFolderUser, , SharedProgramFolderPassword
    /// </summary>
    /// <param name="param">main program full path</param>
    public string GetProgramOperationCommentFromDynamicFileName (string param)
    {
      string result = null;
      string programNumber = null;
      string subProgramNumber = null;
      //string[] lines;
      //string sharedProgramFolder = null;
      log.Debug ($"GetProgramOperationCommentFromDynamicFileName:");

      if (m_stack.Count < 7) {
        log.Error ("GetProgramOperationCommentFromDynamicFileName: not enough elements in the stack");
        throw new Exception ("Not enough elements in stack");
      }

      // retrieve parameters from stack
      SharedProgramFolderPassword = m_stack.Pop ().ToString ();
      SharedProgramFolderUser = m_stack.Pop ().ToString ();
      SharedProgramFolderDomain = m_stack.Pop ().ToString ();
      SharedProgramFolder = m_stack.Pop ().ToString ();
      subProgramNumber = m_stack.Pop ().ToString ();
      programNumber = m_stack.Pop ().ToString ();
      List<String> lines = (List<String>)(m_stack.Pop ());

      if (programNumber.Equals ("O7996")) {
        log.Debug ($"GetProgramOperationCommentFromDynamicFileName: O7996 program");
        // let's assume that if subprogam number has not changed, O7996 program has also not changed.
        if (subProgramNumber != m_currentSubProgramNumber) {

          // read subprograms
          
          log.Debug ($"GetProgramOperationCommentFromDynamicFileName: m_currentSubProgramNumber={m_currentSubProgramNumber}");
          log.Debug ($"GetProgramOperationCommentFromDynamicFileName: m_currentMainCommentLine={m_currentMainCommentLine}");
          log.Debug ($"GetProgramOperationCommentFromDynamicFileName: subProgramNumber={subProgramNumber}");

          // second line should contain the main program comment, line starting with O7996
          // O7996 (2031_76627 - 04 Seg 08 NC_OP30_1.i)
          string mainCommentLine = "";
          if (lines.Count >= 2) {
            mainCommentLine = lines[1];
            log.Debug ($"GetProgramOperationCommentFromDynamicFileName: mainCommentLine={mainCommentLine}");
          }
          else {
            log.Debug ($"GetProgramOperationCommentFromDynamicFileName: lines Count={lines.Count}");
          }
          if (mainCommentLine.ToLowerInvariant ().Contains ("o7996") || mainCommentLine.ToLowerInvariant ().Contains ("O7996")) {
            if (mainCommentLine.Equals (m_currentMainCommentLine)) {
              log.Debug ("GetProgramOperationCommentFromDynamicFileName: no main program change");
            }
            else {
              // main program change, reload subprograms
              m_currentMainCommentLine = mainCommentLine;
              if (!InitSubProgramContentListFromDynamicFileName (lines)) {
                log.Error ("GetProgramOperationCommentFromDynamicFileName: failed to initialize subprogram list");
                return m_currentProgramComment;
              }
            }

            // check in subprograms list in case of sub change
            if (subProgramNumber != m_currentSubProgramNumber) {
              string subName = 'O' + subProgramNumber.Substring (1).PadLeft (4, '0');
              log.Debug ($"GetProgramOperationCommentFromDynamicFileName: subName={subName}");
              string comment = m_subprogramList[subName];

              log.Debug ($"GetProgramOperationCommentFromDynamicFileName: comment={subName}");
              if (!string.IsNullOrEmpty (comment)) {
                log.Debug ($"GetProgramOperationCommentFromDynamicFileName: Sub={subProgramNumber}, comment={comment}");
                m_currentProgramComment = comment;
                result = comment;
              }
              m_currentSubProgramNumber = subProgramNumber;
            }
          }
        }
        else {
          log.Debug ($"GetProgramOperationCommentFromDynamicFileName: no subprogram change, comment={m_currentProgramComment}");
          result = m_currentProgramComment;
        }
      }
      else {
        // read comment directly from program
        log.Debug ($"GetProgramOperationCommentFromDynamicFileName: not O7996 program");
        if (programNumber != m_currentProgramNumber) {
          
          CommentSearchString = "PROGRAMNAME";
          foreach (string currentLine in lines) {
            if (currentLine.IndexOf (CommentSearchString, 0) != -1) {
              result = currentLine;
              log.Debug ($"GetProgramOperationCommentFromDynamicFileName: not O7996 program, comment found: {result}");
              break;
            }
          }
          m_currentProgramNumber = programNumber;
        }
        else {
          log.Debug ($"GetProgramOperationCommentFromDynamicFileName: no program change, comment={m_currentProgramComment}");
          result = m_currentProgramComment;
        }
      }
      return result;
    }

    /// <summary>
    /// Main program (O7996) contains a list of calls to subprograms, M198Pxxx
    /// </summary>
    /// <param name="fileContent">program full path</param>
    public bool InitSubProgramContentListFromDynamicFileName (IList<String> fileContent)
    {
      log.DebugFormat ("InitSubProgramContentListFromDynamicFileName:");

      bool result = false;
      if (null != fileContent) {
        // reset subprogram list
        m_subprogramList.Clear ();
;
        foreach (string currentLine in fileContent) {
          if (currentLine.StartsWith ("M198")) {
            string subProgramName = null;
            Regex progRegexp = new Regex ("^M198P(?<prog>\\d+)\\(.*", RegexOptions.Compiled);
            var match = progRegexp.Match (currentLine);
            if (match.Success) {
              if (match.Groups["prog"].Success) {
                subProgramName = 'O' + match.Groups["prog"].Value.PadLeft (4, '0');
                log.DebugFormat ("InitSubProgramContentListFromDynamicFileName: subName={0}", subProgramName);
                // get subprogram index and programname tag from Subprogram 
                string subProgramComment = null;
                string subProgramNumber = null;
                CommentSearchString = "PROGRAMNAME";
                bool subResult = GetCommentFromNetworkShareFile (subProgramName, out subProgramNumber, out subProgramComment);
                if (subResult) {
                  m_subprogramList.Add (subProgramNumber, subProgramComment);
                  result = true;
                }
              }
            }
          }
        }
        ReleaseNetworkShareAccess ();
      }
      return result;
    }



    #endregion
  }
}
