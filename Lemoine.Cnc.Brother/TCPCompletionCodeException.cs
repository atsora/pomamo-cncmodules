// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Description of TCPCompletionCodeException.
  /// </summary>
  internal class TCPCompletionCodeException : Exception
  {
    #region Members
    readonly int m_completionCode;
    #endregion // Members

    #region Constructors
    /// <summary>
    /// Description of the constructor
    /// </summary>
    public TCPCompletionCodeException (int completionCode)
      : base ("Tcp completion error")
    {
      m_completionCode = completionCode;
    }
    #endregion // Constructors

    #region Methods
    /// <summary>
    /// Message of the alarm
    /// </summary>
    public override string Message
    {
      get {
        string message = "Completion code " + m_completionCode + ": ";
        switch (m_completionCode) {
        case 0:
          message += "Normally ended";
          break;
        case 1:
          message += "Invalid data is received";
          break;
        case 2:
          message += "Illegal slave command header";
          break;
        case 4:
          message += "Illegal slave command check sum";
          break;
        case 5:
          message += "Currently in editing or operation mode, so processing is not possible.";
          break;
        case 6:
          message += "Editing error occurred during file operation.";
          break;
        case 7:
          message += "The specified data does not exist.";
          break;
        case 8:
          message += "Slave command data name is incorrect.";
          break;
        case 9:
          message += "The specified data cannot be saved or deleted.";
          break;
        case 10:
          message += "Data protection enabled";
          break;
        case 11:
          message += "Remote operation not permitted";
          break;
        case 13:
          message += "The item of the received data is not within the allowed range or the number of items doesn’t match.";
          break;
        case 14:
          message += "Data version error";
          break;
        case 15:
          message += "During special startup";
          break;
        case 16:
          message += "Cannot read the specified data.";
          break;
        case 17:
          message += "Output of drawing data was attempted during drawing.";
          break;
        case 18:
          message += "The folder already exists when creating the folder.";
          break;
        case 19:
          message += "Designation of data size is abnormal.";
          break;
        case 20:
          message += "Binary data storage error.";
          break;
        case 30:
          message += "The value is outside the specified range.";
          break;
        case 31:
          message += "Cannot update the value.";
          break;
        case 32:
          message += "A change is required while the value is already being edited.";
          break;
        case 33:
          message += "When changing the ATC tool, the tool is not registered in the specified group.";
          break;
        case 34:
          message += "When changing the ATC tool, changing a magazine item (group / main tool / drawing color) without a tool number assigned was attempted.";
          break;
        case 35:
          message += "When changing the ATC tool, the pot adjacent to the specified pot contains a large tool.";
          break;
        case 36:
          message += "Changing the ATC tool was attempted during memory operation.";
          break;
        case 37:
          message += "Changing the ATC tool was attempted during MDI operation.";
          break;
        case 38:
          message += "Unspecified error occurred during ATC tool change.";
          break;
        case 39:
          message += "When changing the ATC tool, registering the unregistered tool in the tool list was attempted.";
          break;
        case 40:
          message += "Conflict occurred due to communication using other port.";
          break;
        case 41:
          message += "Check sum error occurred in the specified data.";
          break;
        case 42:
          message += "Parity error occurred in the specified data.";
          break;
        case 43:
          message += "The specified data is too large to be stored.";
          break;
        case 44:
          message += "The specified data cannot be stored because programs #8000 to #8999 are write-protected.";
          break;
        case 45:
          message += "Machine unit system is different.";
          break;
        case 46:
          message += "The tool that is unable to change group/main tool/tool type/drawing color in ATC tool change is set.";
          break;
        case 60:
          message += "Mode change not permitted";
          break;
        case 61:
          message += "Mode change not permitted signal is on.";
          break;
        case 62:
          message += "MDI operation mode";
          break;
        case 63:
          message += "During tool change";
          break;
        case 64:
          message += "During automatic centering";
          break;
        case 65:
          message += "During automatic workpiece measurement";
          break;
        case 66:
          message += "Automatic door operation not possible";
          break;
        case 67:
          message += "Operation not possible";
          break;
        case 68:
          message += "No program";
          break;
        case 69:
          message += "Not in memory operation mode (or edit-during-operation mode)";
          break;
        case 70:
          message += "The outer door is open.";
          break;
        case 71:
          message += "The door is open.";
          break;
        case 72:
          message += "The side door is open.";
          break;
        case 73:
          message += "Resetting";
          break;
        case 74:
          message += "Servo control is on.";
          break;
        case 75:
          message += "[FEED HOLD] switch is held down.";
          break;
        case 76:
          message += "Zero return was not conducted.";
          break;
        case 77:
          message += "Restarting / repeating the program OR sequence search in progress";
          break;
        case 78:
          message += "Pallet position error";
          break;
        case 79:
          message += "Performing tool breakage detection";
          break;
        case 80:
          message += "Program number error OR Different from pallet program";
          break;
        case 81:
          message += "Outer pallet A and B-axes operating";
          break;
        case 82:
          message += "No quick table";
          break;
        case 83:
          message += "[PALLET] key is set to [OFF].";
          break;
        case 84:
          message += "Production counter ended";
          break;
        case 85:
          message += "Executing external output command";
          break;
        case 86:
          message += "Memory operation mode";
          break;
        case 87:
          message += "External input not available";
          break;
        case 88:
          message += "In handle mode";
          break;
        case 89:
          message += "XY-axes lock signal is on.";
          break;
        case 90:
          message += "Z-axis lock signal is on.";
          break;
        case 91:
          message += "*-axis lock signal is on.";
          break;
        case 92:
          message += "Pot is not at the top end.";
          break;
        case 93:
          message += "Zero return command error";
          break;
        case 94:
          message += "Indexing not permitted signal is on.";
          break;
        case 95:
          message += "Pallet start reversed";
          break;
        case 96:
          message += "Outer pallet operating";
          break;
        case 97:
          message += "Communicating";
          break;
        case 98:
          message += "NC or conversation mode is not selected correctly.";
          break;
        case 99:
          message += "Reservation";
          break;
        default:
          message += "Unknown";
          break;
        }
        return message;
      }
    }


    #endregion // Methods
  }
}
