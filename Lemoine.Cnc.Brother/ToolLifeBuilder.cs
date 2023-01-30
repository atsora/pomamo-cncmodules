// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Lemoine.Core.Log;

namespace Lemoine.Cnc.Module.Brother
{
  /// <summary>
  /// ToolLifeBuilder
  /// </summary>
  internal class ToolLifeBuilder
  {
    readonly ILog log = LogManager.GetLogger (typeof (ToolLifeBuilder).FullName);

    #region Getters / Setters
    #endregion // Getters / Setters

    #region Constructors
    /// <summary>
    /// Constructor
    /// </summary>
    public ToolLifeBuilder ()
    {
    }
    #endregion // Constructors

    public void AddToolLife (ref ToolLifeData tld, string toolId, IList<string> parts)
    {
      // Tool number
      int toolNumber;
      try {
        if (toolId[0] != 'T') {
          return;
        }

        toolNumber = int.Parse (toolId.Substring (1, 2));
      }
      catch (Exception ex) {
        log.Error ($"AddToolLife: Couldn't parse tool number {toolId}", ex);
        return;
      }

      // Create a tool
      int index = tld.AddTool ();
      tld[index].MagazineNumber = 0;
      tld[index].PotNumber = toolNumber;
      tld[index].ToolNumber = toolNumber.ToString ();
      tld[index].ToolId = toolId;

      // Tool life
      switch (parts[4]) {
      case "0":
      case "1":
      case "":
        // No count => do nothing
        break;
      case "2":
        // Unit: minute
        AddToolLifeDescription (ref tld, index, parts[5], parts[6], parts[7], Lemoine.Core.SharedData.ToolUnit.TimeSeconds, 60);
        break;
      case "3":
        // Unit: number of times
        AddToolLifeDescription (ref tld, index, parts[5], parts[6], parts[7], Lemoine.Core.SharedData.ToolUnit.NumberOfTimes, 1);
        break;
      default:
        // Unknown value
        log.ErrorFormat ("Brother.AddToolLife - Unknown tool life unit: {0}", parts[4]);
        break;
      }

      // Extra properties
      tld[index].Properties["LengthCompensation"] = parts[0];
      tld[index].Properties["CutterCompensation"] = parts[2];
      var toolName = parts[8].Trim (new[] { ' ', '\'' });
      if (!string.IsNullOrEmpty (toolName)) {
        tld[index].Properties["ToolName"] = toolName;
      }
      //tld[index].Properties["tool length wear"] = parts[1];
      //tld[index].Properties["cutter compensation wear offset"] = parts[3];
    }

    void AddToolLifeDescription (ref ToolLifeData tld, int index, string max, string warning, string current,
                                Lemoine.Core.SharedData.ToolUnit toolUnit, double multiplier)
    {
      // Current
      double currentValue = -1;
      try {
        if (!string.IsNullOrEmpty (current)) {
          currentValue = multiplier * double.Parse (current);
        }
      }
      catch (Exception ex) {
        log.ErrorFormat ($"AddToolLifeDescription: Cannot parse current {current} as double", ex);
        return; // No tool life description in that case
      }

      // Warning
      double warningValue = -1;
      try {
        if (!string.IsNullOrEmpty (warning)) {
          warningValue = multiplier * double.Parse (warning);
        }
      }
      catch (Exception ex) {
        log.Error ($"AddToolLifeDescription: Cannot parse warning {warning}' as double", ex);
      }

      // Max value
      double maxValue = -1;
      try {
        if (!string.IsNullOrEmpty (max)) {
          maxValue = multiplier * double.Parse (max);
        }
      }
      catch (Exception ex) {
        log.Error ($"AddToolLifeDescription: Cannot parse max {max} as double", ex);
      }

      int index2 = tld[index].AddLifeDescription ();
      tld[index][index2].LifeType = toolUnit;
      tld[index][index2].LifeDirection = Lemoine.Core.SharedData.ToolLifeDirection.Down;
      tld[index][index2].LifeValue = currentValue;
      if (warningValue > 0) {
        tld[index][index2].LifeWarningOffset = warningValue;
      }

      if (maxValue > 0) {
        tld[index][index2].LifeLimit = maxValue;
      }
    }
  }
}
