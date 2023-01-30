// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

using Lemoine.Model;
using Lemoine.ModelDAO;
using Lemoine.Core.Log;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Try to get some data from the ISO file itself
  /// </summary>
  public sealed class IsoFileContent: Lemoine.Cnc.BaseCncModule, Lemoine.Cnc.ICncModule, IDisposable
  {
    #region Members
    string m_programName = "";
    int m_isoFileId = 0;
    IIsoFile m_isoFile = null;
    int m_stampIdVariable = 0;
    IStamp m_stamp = null;
    int m_blockNumber = 0;
    int m_nextBlockNumber = int.MaxValue;
    int m_sequenceIdVariable = 0;
    #endregion

    #region Getters / Setters
    /// <summary>
    /// Program name
    /// 
    /// It can be determined from:
    /// <item>the ISO file ID</item>
    /// </summary>
    public string ProgramName {
      get {
        if (0 != m_programName.Length) { // In cache or manually set
          log.DebugFormat ("ProgramName.get: " +
                           "got program name {0} in cache",
                           m_programName);
          return m_programName;
        }
        if (0 == m_stampIdVariable) {
          log.ErrorFormat ("ProgramName.get: " +
                           "there is not enough known parameters " +
                           "to get the program name");
          throw new Exception ("Not enough known parameters");
        }
        // Try to get the program name from the file ID
        if (null == this.IsoFile) {
          log.ErrorFormat ("ProgramName.get: " +
                           "the Iso File is unknown, " +
                           "could not get the program name");
          throw new Exception ("Unknown Iso File");
        }
        else {
          m_programName = Path.GetFileName (this.IsoFile.Name);
          log.DebugFormat ("ProgramName.get: " +
                           "got program name {0} from fileId {1}",
                           m_programName, m_isoFileId);
          return m_programName;
        }
      }
      set { m_programName = value; }
    }
    
    /// <summary>
    /// Iso File ID
    /// 
    /// It can be determined from:
    /// <item>the stamp ID</item>
    /// <item>the program name</item>
    /// </summary>
    int IsoFileId {
      get {
        if (0 != m_isoFileId) { // In cache
          log.DebugFormat ("IsoFileId.get: " +
                           "got ISO file Id {0} in cache",
                           m_isoFileId);
          return m_isoFileId;
        }
        if ((0 == m_stampIdVariable) && (0 == m_programName.Length)) {
          log.ErrorFormat ("IsoFileId.get: " +
                           "there is not enough known parameters " +
                           "to get the ISO file ID");
          throw new Exception ("Not enough known parameters");
        }
        // Try to get the file id from the stamp Id
        if ((0 != m_stampIdVariable) && (null != Stamp)) {
          using (IDAOSession session = ModelDAOHelper.DAOFactory.OpenSession ()) {
            m_isoFileId = Stamp.IsoFile.Id;
          }
          log.DebugFormat ("IsoFileId.get: " +
                           "got ISO file Id {0} from stamp id {1}",
                           m_isoFileId, m_stampIdVariable);
          return m_isoFileId;
        }
        // Try to get it from the program name
        if (0 != m_programName.Length) {
          using (IDAOSession session = ModelDAOHelper.DAOFactory.OpenSession ()) {
            IList<IIsoFile> isoFiles = ModelDAOHelper.DAOFactory.IsoFileDAO
              .GetIsoFile (ProgramName);
            if (0 == isoFiles.Count) {
              log.ErrorFormat ("IsoFileId.get: " +
                               "no Iso file in database with name like {0}",
                               ProgramName);
              throw new Exception ("No ISO file match the program name (1)");
            }
            else if (1 == isoFiles.Count) {
              if (ProgramName.Equals (isoFiles [0].Name)
                  || ProgramName.Equals (Path.GetFileName (isoFiles [0].Name))
                  || ProgramName.Equals (Path.GetFileNameWithoutExtension (isoFiles [0].Name))) {
                m_isoFile = isoFiles [0];
                m_isoFileId = m_isoFile.Id;
                log.DebugFormat ("IsoFileId.get: " +
                                 "program name {0} match the ISO file {1} " +
                                 "=> got ISO file Id {2}",
                                 ProgramName, m_isoFile.Name,
                                 m_isoFileId);
                return m_isoFileId;
              }
              else {
                log.ErrorFormat ("IsoFileId.get: " +
                                 "no ISO file matches the program name {0}");
                throw new Exception ("No ISO file matches the program name (2)");
              }
            }
            else { // Several iso files match, take the latest one
              m_isoFileId = 0;
              foreach (IIsoFile tmpIsoFile in isoFiles) {
                if (ProgramName.Equals (tmpIsoFile.Name)
                    || ProgramName.Equals (Path.GetFileName (tmpIsoFile.Name))
                    || ProgramName.Equals (Path.GetFileNameWithoutExtension (tmpIsoFile.Name))) {
                  m_isoFile = tmpIsoFile;
                  m_isoFileId = m_isoFile.Id;
                  log.DebugFormat ("IsoFileId.get: " +
                                   "program name {0} match the ISO file {1} " +
                                   "=> got ISO file Id {2}",
                                   ProgramName, m_isoFile.Name,
                                   m_isoFileId);
                  log.WarnFormat ("IsoFileId.get: " +
                                  "several ISO files (potentially {0}) could match program {1}" +
                                  "=> take the latest one that matches",
                                  isoFiles.Count, ProgramName);
                  // If there is no operation for this ISO file, try another one
                  IList<IStamp> matchingStamps = ModelDAOHelper.DAOFactory.StampDAO
                    .FindAllWithIsoFile (m_isoFile);
                  if (0 == matchingStamps.Count) {
                    log.WarnFormat ("IsoFileId.get: " +
                                    "One matching ISO file found with ID {0} " +
                                    "but there is no corresponding stamp " +
                                    "=> try another one",
                                    m_isoFileId);
                  }
                  else {
                    return m_isoFileId;
                  }
                }
              }
              if (0 != m_isoFileId) {
                return m_isoFileId;
              }
              else {
                log.ErrorFormat ("IsoFileId.get: " +
                                 "no ISO file match the program name {0}");
                throw new Exception ("No ISO file match the program name (2)");
              }
            }
          }
        }
        log.ErrorFormat ("IsoFileId.get: " +
                         "Stamp and program name unknown " +
                         "=> could not determine the file Id");
        throw new Exception ("Unknown stamp and program name");
      }
    }
    
    /// <summary>
    /// Sequence ID variable (needed by LPstFileMap)
    /// 
    /// It is determined from the stamp ID
    /// </summary>
    public int SequenceIdVariable {
      get {
        if (0 != m_sequenceIdVariable) { // In cache
          log.DebugFormat ("SequenceIdVariable.get: " +
                           "got Sequence Id {0} in cache",
                           m_sequenceIdVariable);
          return m_sequenceIdVariable;
        }
        if (0 == m_stampIdVariable) {
          log.ErrorFormat ("SequenceIdVariable.get: " +
                           "Stamp ID is 0 => return 0");
          return 0;
        }
        // Try to get the sequence id from the stamp Id
        if ((0 != m_stampIdVariable) && (null != Stamp)) {
          using (IDAOSession session = ModelDAOHelper.DAOFactory.OpenSession ()) {
            if (null != Stamp.Sequence) {
              m_sequenceIdVariable = ((Lemoine.Collections.IDataWithId)Stamp.Sequence).Id;
            }
          }
          log.DebugFormat ("SequenceIdVariable.get: " +
                           "got Sequence Id {0} from stamp id {1}",
                           m_sequenceIdVariable, m_stampIdVariable);
          return m_sequenceIdVariable;
        }
        log.ErrorFormat ("SequenceIdVariable.get: " +
                         "Stamp unknown " +
                         "=> could not determine the sequence Id");
        throw new Exception ("Unknown stamp");
      }
    }
    
    /// <summary>
    /// Stamp ID
    /// 
    /// It can be determined from:
    /// <item>the ISO file ID and the block number</item>
    /// </summary>
    public int StampIdVariable {
      get {
        if (0 != m_stampIdVariable) { // In cache
          log.DebugFormat ("StampId.get: " +
                           "got Stamp Id {0} in cache",
                           m_stampIdVariable);
          return m_stampIdVariable;
        }
        if (0 == m_programName.Length) {
          // This is to avoid any loop between different properties
          log.ErrorFormat ("StampId.get: " +
                           "there is not enough known parameters " +
                           "to get the Stamp ID");
          throw new Exception ("Not enough known parameters");
        }
        // Try to get the stamp Id from IsoFileId and BlockNumber
        if (IsoFileId == 0) {
          log.ErrorFormat ("StampId.get: " +
                           "the ISO file Id is unknown, " +
                           "=> could not determine the stamp Id " +
                           "from the ISO file id");
          throw new Exception ("Unknown ISO file");
        }
        else { // IsoFileId != 0
          using (IDAOSession session = ModelDAOHelper.DAOFactory.OpenSession ()) {
            IList<IStamp> stamps = ModelDAOHelper.DAOFactory.StampDAO
              .GetAllWithAscendingPosition (IsoFileId);
            if (0 == stamps.Count) {
              log.ErrorFormat ("StampId.get: " +
                               "no operation match file Id {0}",
                               IsoFileId);
              throw new Exception ("No matching stamp for ISO file");
            }
            IStamp matchedStamp = stamps [0];
            m_nextBlockNumber = int.MaxValue;
            foreach (IStamp stamp in stamps) {
              Debug.Assert (stamp.Position.HasValue);
              if (BlockNumber < stamp.Position) { // Next stamp
                m_nextBlockNumber = stamp.Position.Value;
                break;
              }
              matchedStamp = stamp;
            }
            m_stamp = matchedStamp;
            m_stampIdVariable = ((Lemoine.Collections.IDataWithId)m_stamp).Id;
            log.DebugFormat ("StampId.get: " +
                             "got stamp Id {0} and next block number {1} " +
                             "from ISO file Id {2} and block number {3}",
                             m_stampIdVariable, m_nextBlockNumber,
                             IsoFileId, BlockNumber);
            return m_stampIdVariable;
          }
        }
      }
      set {
        if (m_stampIdVariable != value) {
          m_stampIdVariable = value;
          // And reset the associated values
          m_isoFileId = 0;
          m_isoFile = null;
          m_stamp = null;
          m_programName = "";
          m_sequenceIdVariable = 0;
        }
      }
    }
    
    /// <summary>
    /// Block number
    /// </summary>
    public int BlockNumber {
      get { return m_blockNumber; }
      set
      {
        m_blockNumber = value;
        if (m_nextBlockNumber <= value) {
          log.DebugFormat ("BlockNumber.set: " +
                           "block number {0} is greater than " +
                           "the stored next number {1} " +
                           "=> reset operationId and operation",
                           m_blockNumber,
                           m_nextBlockNumber);
          m_stampIdVariable = 0;
          m_stamp = null;
        }
      }
    }
    
    /// <summary>
    /// Completion
    /// </summary>
    public double Completion {
      get
      {
        if (null == IsoFile) {
          log.ErrorFormat ("Completion.get: " +
                           "IsoFile is unknown " +
                           "=> could not determine the completion");
          throw new Exception ("Unknown ISO file");
        }
        if (!IsoFile.Size.HasValue) {
          log.ErrorFormat ("Completion.get: " +
                           "IsoFile has not a known size " +
                           "=> could not determine the completion");
          throw new Exception ("ISO file with an unknown size");
        }
        if (IsoFile.Size.Value <= 0) {
          log.ErrorFormat ("Completion.get: " +
                           "IsoFile has a negative or null size " +
                           "=> could not determine the completion");
          throw new Exception ("ISO file with a negative or null size");
        }
        double completion  = ((double) BlockNumber) * 100 / IsoFile.Size.Value;
        log.DebugFormat ("Completion.get: " +
                         "got completion  {0} from block number {1} and ISO file {2}",
                         completion, BlockNumber, IsoFile.Id);
        return completion;
      }
    }
    
    /// <summary>
    /// ISO file persistent class
    /// 
    /// It is determined from IsoFileId
    /// </summary>
    IIsoFile IsoFile {
      get
      {
        if (null != m_isoFile) {
          log.DebugFormat ("IsoFile.get: " +
                           "take iso file in cache");
          return m_isoFile;
        }
        if (0 == IsoFileId) {
          log.ErrorFormat ("IsoFile.get: " +
                           "the iso file id is unknown");
          throw new Exception ("ISO file id unknown");
        }
        using (IDAOSession session = ModelDAOHelper.DAOFactory.OpenSession ()) {
          m_isoFile = ModelDAOHelper.DAOFactory.IsoFileDAO
            .FindById (IsoFileId);
        }
        if (null == m_isoFile) {
          log.ErrorFormat ("IsoFile.get: " +
                           "there is no Iso File with ID {0}",
                           IsoFileId);
          throw new Exception ("Iso file does not exist");
        }
        else {
          log.DebugFormat ("IsoFile.get: " +
                           "got Iso file for ID {0}",
                           IsoFileId);
          return m_isoFile;
        }
      }
    }
    
    /// <summary>
    /// Stamp persistent class
    /// 
    /// It is determined from StampId
    /// </summary>
    IStamp Stamp {
      get
      {
        if (null != m_stamp) {
          log.DebugFormat ("Stamp.get: " +
                           "take stamp in cache");
          return m_stamp;
        }
        if (0 == StampIdVariable) {
          log.ErrorFormat ("Stamp.get: " +
                           "Stamp ID unknown");
          throw new Exception ("Stamp ID unknown");
        }
        using (IDAOSession session = ModelDAOHelper.DAOFactory.OpenSession ()) {
          m_stamp = ModelDAOHelper.DAOFactory.StampDAO
            .FindById (StampIdVariable);
        }
        if (null == m_stamp) {
          log.ErrorFormat ("Stamp.get: " +
                           "there is no stamp with Id {0}",
                           StampIdVariable);
          throw new Exception ("Stamp does not exist");
        }
        else {
          log.DebugFormat ("Stamp.get: " +
                           "got stamp for StampId {0}",
                           StampIdVariable);
          return m_stamp;
        }
      }
    }
    
    #endregion

    #region Constructors / Destructor / ToString methods
    /// <summary>
    /// Description of the constructor
    /// </summary>
    public IsoFileContent ()
      : base("Lemoine.Cnc.InOut.IsoFileContent")
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
  }
}
