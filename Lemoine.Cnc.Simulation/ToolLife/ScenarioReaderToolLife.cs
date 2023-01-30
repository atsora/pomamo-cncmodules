// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Globalization;
using System.Text.RegularExpressions;
using Lemoine.Core.SharedData;
using Lemoine.Core.Log;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Description of ScenarioReaderToolLife.
  /// </summary>
  public class ScenarioReaderToolLife : IScenarioReader
  {
    #region Members
    readonly ToolLifeData m_currentToolLifeData = new ToolLifeData ();
    #endregion // Members

    static readonly Regex CHECK_TOOL = new Regex (@"^T[0-9]+\[[0-9]+([.][0-9]{1,3})?s\]$");
    static readonly Regex CHECK_TOOL_2 = new Regex (@"^T[0-9]+\[(up|down);[0-9]+([.][0-9]{1,3})?s;[0-9]+([.][0-9]{1,3})?s;[0-9]+([.][0-9]{1,3})?s\]$");

    ILog log = LogManager.GetLogger ("Lemoine.Cnc.In.Simulation.ScenarioReader.ToolLife");

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="l"></param>
    public ScenarioReaderToolLife (ILog l)
    {
      log = l;
    }

    #region Get methods
    /// <summary>
    /// Get the current tool life data
    /// </summary>
    /// <returns></returns>
    public ToolLifeData GetToolLifeData ()
    {
      return m_currentToolLifeData;
    }
    #endregion // Get methods

    #region Process methods
    /// <summary>
    /// <see cref="IScenarioReader"/>
    /// </summary>
    /// <param name="l"></param>
    public void UpdateLog (ILog l)
    {
      log = l;
    }

    /// <summary>
    /// Process a command
    /// </summary>
    /// <param name="command"></param>
    /// <returns>true if success</returns>
    public bool ProcessCommand (string command)
    {
      // Split according to the spaces
      var parts = command.Split (new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

      if (parts.Length > 0) {
        // Clear tools
        m_currentToolLifeData.ClearTools ();

        // Add tools
        int num = 0;
        bool ok = true;
        foreach (string part in parts) {
          ok &= FillToolData (part, num);
          num++;
        }
        return ok;
      }
      else {
        return false;
      }
    }

    bool FillToolData (string strToolData, int potNum)
    {
      if (strToolData == "-") {
        return true;
      }

      // Simple template?
      if (CHECK_TOOL.IsMatch (strToolData)) {
        return FillToolDataSimple (strToolData, potNum);
      }

      // Complex template?
      if (CHECK_TOOL_2.IsMatch (strToolData)) {
        return FillToolDataComplex (strToolData, potNum);
      }

      return true;
    }

    bool FillToolDataSimple (string strToolData, int potNum)
    {
      try {
        // Tool number and life value
        string toolNumberStr = strToolData.Substring (1, strToolData.IndexOf ('[') - 1);
        string lifeValueStr = strToolData.Substring (strToolData.IndexOf ('[') + 1,
                                                    strToolData.Length - strToolData.IndexOf ('[') - 3);

        // New tool in the data
        m_currentToolLifeData.AddTool ();
        int index = m_currentToolLifeData.ToolNumber - 1;
        m_currentToolLifeData[index].PotNumber = potNum;
        m_currentToolLifeData[index].ToolNumber = toolNumberStr;
        m_currentToolLifeData[index].ToolState = ToolState.Available;
        m_currentToolLifeData[index].AddLifeDescription ();
        m_currentToolLifeData[index][0].LifeDirection = ToolLifeDirection.Down;
        m_currentToolLifeData[index][0].LifeLimit = null;
        m_currentToolLifeData[index][0].LifeWarningOffset = null;
        m_currentToolLifeData[index][0].LifeType = ToolUnit.TimeSeconds;
        m_currentToolLifeData[index][0].LifeValue = ParseSeconds (lifeValueStr);
      }
      catch (Exception) {
        log.ErrorFormat ("FillToolDataSimple: invalid part '{0}'", strToolData);
        return false;
      }

      return true;
    }

    bool FillToolDataComplex (string strToolData, int potNum)
    {
      try {
        // Tool number and life value
        string toolNumberStr = strToolData.Substring (1, strToolData.IndexOf ('[') - 1);
        var lifeValuesStr = strToolData.Substring (strToolData.IndexOf ('[') + 1,
                                                  strToolData.Length - strToolData.IndexOf ('[') - 2).Split (';');

        // New tool in the data
        m_currentToolLifeData.AddTool ();
        int index = m_currentToolLifeData.ToolNumber - 1;
        m_currentToolLifeData[index].PotNumber = potNum;
        m_currentToolLifeData[index].ToolNumber = toolNumberStr;
        m_currentToolLifeData[index].ToolState = ToolState.Available;
        m_currentToolLifeData[index].AddLifeDescription ();
        m_currentToolLifeData[index][0].LifeDirection = lifeValuesStr[0] == "down" ?
          ToolLifeDirection.Down : ToolLifeDirection.Up;
        m_currentToolLifeData[index][0].LifeLimit = ParseSeconds (lifeValuesStr[3]);
        m_currentToolLifeData[index][0].LifeWarningOffset =
          m_currentToolLifeData[index][0].LifeDirection == ToolLifeDirection.Down ?
          ParseSeconds (lifeValuesStr[2]) - m_currentToolLifeData[index][0].LifeLimit :
          m_currentToolLifeData[index][0].LifeLimit - ParseSeconds (lifeValuesStr[2]);
        m_currentToolLifeData[index][0].LifeType = ToolUnit.TimeSeconds;
        m_currentToolLifeData[index][0].LifeValue = ParseSeconds (lifeValuesStr[1]);
      }
      catch (Exception) {
        log.ErrorFormat ("FillToolDataSimple: invalid part '{0}'", strToolData);
        return false;
      }

      return true;
    }

    double ParseSeconds (string str)
    {
      // Remove the final "s" and replace ',' into '.'
      str = str.Remove (str.Length - 1, 1).Replace (',', '.');

      // Convert into double
      var ci = CultureInfo.InvariantCulture.Clone () as CultureInfo;
      ci.NumberFormat.NumberDecimalSeparator = ".";
      return double.Parse (str.Replace (',', '.'), ci);
    }
    #endregion // Process methods
  }
}
