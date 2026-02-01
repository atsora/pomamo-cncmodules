// Copyright (C) 2009-2023 Lemoine Automation Technologies
// Copyright (C) 2026 Atsora Solutions
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections;
using System.Collections.Generic;
using System.Xml;
using System.Xml.XPath;

namespace Lemoine.Cnc
{
  /// <summary>
  /// MTConnect input module
  /// </summary>
  public partial class MTConnect
  {
    List<string> m_blocks = new List <string> ();
    bool m_hasBlocks = false;
    Hashtable m_blockValues = new Hashtable ();
    string m_blockInstance = ""; // empty is for unknown
    long m_blockNextSequence = 0; // 0 is for unknown
    string m_programName = null;
    
    /// <summary>
    /// Get an integer value in the blocks
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public int GetBlockValueInt (string key)
    {
      string latestBlock = GetBlock (key);
      if (null != latestBlock) {
        string[] parts = latestBlock.Split ('=');
        if (2 == parts.Length) {
          int v = int.Parse (parts [2]);
          log.DebugFormat ("GetBlockValueInt: " +
                           "about to add a new value {0} from line {1} " +
                           "for key {2}",
                           v, latestBlock,
                           key);
          this.m_blockValues [key] = v;
        }
      }
      
      int result = (int) this.m_blockValues [key];
      log.DebugFormat ("GetBlockValueInt: " +
                       "return {0} for key {1}",
                       result, key);
      return result;
    }
    
    /// <summary>
    /// Get the latest block that contains the given key.
    /// If no such block has been found, null is returned.
    /// 
    /// The syntax of a key is: [Key]= Value
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    string GetBlock (string key)
    {
      GetBlocks ();
      
      string result = null;
      foreach (string block in this.m_blocks) {
        if (block.Contains (String.Format ("[{0}]", key))) {
          result = block;
        }
      }
      
      return result;
    }
    
    void GetBlocks ()
    {
      // 0. Check if it has not been done before
      if (true == m_hasBlocks) {
        log.Debug ("GetBlocks: " +
                   "blocks are already known");
        return;
      }
      
      // 1. Blocks are not known yet, clean blocks
      m_blocks.Clear ();
      
      // 2. Set the XPaths
      string instanceXPath = "//Header/@instanceId";
      if (m_mtconnectStreamsPrefix.Length > 0) {
        instanceXPath = String.Format ("//{0}:Header/@instanceId",
                                       m_mtconnectStreamsPrefix);
      }
      string nextSequenceXPath = "//Header/@nextSequence";
      if (m_mtconnectStreamsPrefix.Length > 0) {
        nextSequenceXPath = String.Format ("//{0}:Header/@nextSequence",
                                           m_mtconnectStreamsPrefix);
      }
      
      // 3. Initialize blockInstance and blockNextSequence if needed
      long nextSequence = 0;
      if (0 == m_blockNextSequence) {
        try {
          nextSequence = GetLong (nextSequenceXPath);
        }
        catch (Exception ex) {
          log.WarnFormat ("Start: " +
                          "could not read nextSequence value xpath={0}, {1}, " +
                          "=> use the default 0 instead",
                          nextSequenceXPath, ex);
        }
      }
      try {
        string instance = GetString (instanceXPath);
        log.DebugFormat ("Start: " +
                         "got instance {0}",
                         instance);
        if (!instance.Equals (this.m_blockInstance)) {
          log.InfoFormat ("Start: " +
                          "instance was updated from {0} to {1}",
                          this.m_blockInstance, instance);
          this.m_blockNextSequence = nextSequence;
          this.m_blockInstance = instance;
          this.m_blockValues.Clear ();
        }
      }
      catch (Exception ex) {
        log.WarnFormat ("Start: " +
                        "could not read the instanceId xpath={0}, {1}",
                        instanceXPath, ex);
      }
      
      // 4. Read the blocks
      try {
        // 4.a) Make the URL
        string baseUrl = Url.Substring (0, Url.IndexOf ("current"));
        string blockUrl = String.Format ("{0}sample?path={1}&from={2}",
                                         baseUrl,
                                         this.m_blockPath,
                                         this.m_blockNextSequence);
        // 4.b) Parse the result
        var blockDocument = new XmlDocument ();
        blockDocument.Load (blockUrl);
        XmlNamespaceManager blockXmlnsManager = null;
        XPathNavigator blockPathNavigator = blockDocument.CreateNavigator ();
        if (m_xmlns.Count == 0) {
          if (0 == m_mtconnectStreamsPrefix.Length) {
            blockXmlnsManager = null;
            log.InfoFormat ("GetBlocks: " +
                            "no namespace for MTConect Streams");
          }
          else {
            blockXmlnsManager = new XmlNamespaceManager (blockPathNavigator.NameTable);
            blockXmlnsManager.AddNamespace (m_mtconnectStreamsPrefix,
                                            DEFAULT_MTCONNECTSTREAMS_NAMESPACE);
            log.InfoFormat ("GetBlocks: " +
                            "use default namespace {0} for MTConnect Streams Prefix {1}",
                            DEFAULT_MTCONNECTSTREAMS_NAMESPACE,
                            m_mtconnectStreamsPrefix);
          }
        }
        else {
          blockXmlnsManager = new XmlNamespaceManager (blockPathNavigator.NameTable);
          foreach (DictionaryEntry i in m_xmlns) {
            blockXmlnsManager.AddNamespace (i.Key.ToString (),
                                            i.Value.ToString ());
          }
        }
        // 4.c) Check the Instance
        XPathNavigator node;
        if (null != blockXmlnsManager) {
          node = blockPathNavigator.SelectSingleNode (instanceXPath,
                                                      blockXmlnsManager);
        }
        else {
          node = blockPathNavigator.SelectSingleNode (instanceXPath);
        }
        string instance = GetValidValue (node, instanceXPath);
        if (0 == this.m_blockInstance.Length) {
          log.DebugFormat ("GetBlocks: " +
                           "set instance={0} sequence={1} to replace an empty instance",
                           instance, 0);
          this.m_blockInstance = instance;
          this.m_blockNextSequence = 0;
          this.m_blockValues.Clear ();
        }
        else if (!this.m_blockInstance.Equals (instance)) {
          log.InfoFormat ("GetBlocks: " +
                          "instance was updated from {0} to {1}",
                          this.m_blockInstance, instance);
          this.m_blockInstance = instance;
          this.m_blockNextSequence = 0;
          this.m_blockValues.Clear ();
          throw new Exception ("Updated instance"); // Try again the next time
        }
        // 4.d) Get the blocks
        XPathNodeIterator it;
        if (null != blockXmlnsManager) {
          it = blockPathNavigator.Select (this.m_blockXPath, blockXmlnsManager);
        }
        else {
          it = blockPathNavigator.Select (this.m_blockXPath);
        }
        foreach (XPathNavigator blockNode in it) {
          if ( (null != blockNode) && (!blockNode.Value.Equals (UNAVAILABLE))) {
            this.m_blocks.Add (blockNode.Value);
          }
        }
        // 4.e) Update this.blockNextSequence
        if (null != blockXmlnsManager) {
          node = blockPathNavigator.SelectSingleNode (nextSequenceXPath,
                                                      blockXmlnsManager);
        }
        else {
          node = blockPathNavigator.SelectSingleNode (nextSequenceXPath);
        }
        string sequence = GetValidValue (node, nextSequenceXPath);
        this.m_blockNextSequence = long.Parse (sequence);
        // 4.f) Job done, set hasBlocks to true
        m_hasBlocks = true;
      }
      catch (Exception ex) {
        log.ErrorFormat ("GetBlocks: {0}", ex);
        throw;
      }
      
      return;
    }
  }
}
