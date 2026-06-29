// Copyright (C) 2009-2023 Lemoine Automation Technologies
// Copyright (C) 2026 Atsora Solutions
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;

using Lemoine.Core.Log;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Class to get a Perle IO input
  /// </summary>
  public class PerleIO : AbstractSerial, Lemoine.Cnc.ICncModule
  {
    bool m_error = false;

    /// <summary>
    /// Error ?
    /// </summary>
    public bool Error => m_error;
 
    /// <summary>
    /// Description of the constructor
    /// </summary>
    public PerleIO ()
      : base ("Lemoine.Cnc.In.PerleIO")
    {
    }

    // Note: the Dispose method is implemented in
    //       the base class AbstractSerial    

    /// <summary>
    /// Get an I/O status given an I/O number
    /// 
    /// Use a number:
    /// <item>between 1 and 4 for D4 or D2R2</item>
    /// <item>between 5 and 6 for A4D2</item>
    /// </summary>
    /// <param name="param">I/O number</param>
    /// <returns></returns>
    public bool GetIO (string param)
    {
      byte ioNumber;
      try {
        ioNumber = byte.Parse (param);
      }
      catch (Exception ex) {
        log.Error ($"GetIO: invalid param {param} for IO number", ex);
        m_error = true;
        throw ex;
      }
      return GetIO (ioNumber);
    }

    /// <summary>
    /// Get an I/O status given an I/O number
    /// 
    /// Use a number:
    /// <item>between 1 and 4 for D4 or D2R2</item>
    /// <item>between 5 and 6 for A4D2</item>
    /// </summary>
    /// <param name="ioNumber">I/O number</param>
    /// <returns></returns>
    public bool GetIO (byte ioNumber)
    {
      if (SerialPort.IsOpen == false) {
        log.Warn ($"GetIO ({ioNumber}): IO Com port is not opened, try to open it");
        try {
          SerialPort.Open ();
        }
        catch (Exception ex) {
          log.Error ($"GetIO ({ioNumber}): Open IO COM Exception {ex.Message}", ex);
          m_error = true;
          throw ex;
        }
        if (SerialPort.IsOpen == false) {
          log.Fatal ($"GetIO ({ioNumber}): IO COM is not opened even after successfully opening the port");
          m_error = true;
          throw new Exception ("IO COM is not opened");
        }
      }
      // For D4/D2R2:
      // IONum = 1 is at address 6145 = 0x1801
      // IONum = 2 is at address 6146 = 0x1802
      // IONum = 3 is at address 6147 = 0x1803
      // IONum = 4 is at address 6148 = 0x1804
      // For A4D2:
      // IONum = 5 is at address 6149 = 0x1805
      // IONum = 6 is at address 6150 = 0x1806

      if (log.IsDebugEnabled) {
        log.Debug ($"GetIO /B IO Number={ioNumber}");
      }

      byte[] command = new byte[5];
      // byte 0 : function type
      command[0] = 0x01; // coils (boolean register)
      // bytes 1 & 2 starting register adress (0x18IONum = 0x1801 -> 0x1806)
      command[1] = 0x18;
      command[2] = ioNumber;
      // bytes 3 & 4 number of register to read = 1 (0x0001)
      command[3] = 0x00;
      command[4] = 0x01;
      try {
        SerialPort.Write (command, 0, 5);
        if (log.IsDebugEnabled) {
          log.Debug ($"GetIO IONum={ioNumber}: {command} has been written in IO Com");
        }
      }
      catch (Exception ex) {
        log.Error ($"GetIO IONum={ioNumber}: Write exception {ex.Message}", ex);
        SerialPort.Close ();
        m_error = true;
        throw ex;
      }

      const int MAX_BYTES_RES = 4;
      byte[] response = new byte[MAX_BYTES_RES];
      int totalread = 0;

      // Normally we should receive 3 bytes as a reply.
      do {
        try {
          int nb = SerialPort.Read (command, 0, MAX_BYTES_RES);
          if (log.IsDebugEnabled) {
            log.Debug ($"GetIO IONum={ioNumber}: read {nb} bytes");
          }
          for (int i = 0; i < nb; i++) {
            if (log.IsDebugEnabled) {
              log.Debug ($"GetIO IONum={ioNumber}: response[{totalread}] = {command[i]:X} ({command[i]})");
            }
            response[totalread] = command[i]; totalread++;
            if (totalread >= MAX_BYTES_RES) {
              break;
            }
          }
          if (totalread >= MAX_BYTES_RES) {
            break;
          }
        }
        catch (Exception ex) {
          log.Error ($"GetIO IONum={ioNumber}: Read exception {ex.Message}", ex);
          SerialPort.Close ();
          m_error = true;
          throw ex;
        }
        if (log.IsDebugEnabled) {
          log.Debug ($"GetIO IONum={ioNumber}: Bytes to read {SerialPort.BytesToRead}");
        }
      }
      while (SerialPort.BytesToRead > 0);

      if (response[0] > 0x80) { // Error !
        log.Error ($"GetIO IONum={ioNumber}: got error number={response[1]}");
        GetErrorMessage (response[1]);
        m_error = true;
        throw new Exception ("Perle device error");
      }

      if (log.IsDebugEnabled) {
        log.Debug ($"GetIO /E IONum={ioNumber}: ended with response[2] = {response[2]}");
      }

      return (response[2] != 0);
    }

    /// <summary>
    /// Get the value of an analog I/O given an I/O number
    /// 
    /// Use a number:
    /// <item>between 1 and 4 for A4D2</item>
    /// </summary>
    /// <param name="param">I/O number</param>
    /// <returns></returns>
    public double GetAnalogIO (string param)
    {
      byte ioNumber;
      try {
        ioNumber = byte.Parse (param);
      }
      catch (Exception ex) {
        log.Error ($"GetAnalogIO: invalid param {param} for IO number", ex);
        m_error = true;
        throw ex;
      }
      return GetAnalogIO (ioNumber);
    }

    /// <summary>
    /// Get the value of an analog I/O given an I/O number
    /// 
    /// Use a number:
    /// <item>between 1 and 4 for A4D2</item>
    /// </summary>
    /// <param name="ioNumber">I/O number</param>
    /// <returns></returns>
    public double GetAnalogIO (byte ioNumber)
    {
      if (SerialPort.IsOpen == false) {
        log.Warn ($"GetAnalogIO ({ioNumber}): IO Com port is not opened, try to open it");
        try {
          SerialPort.Open ();
        }
        catch (Exception ex) {
          log.Error ($"GetAnalogIO ({ioNumber}): Open IO COM Exception {ex.Message}", ex);
          m_error = true;
          throw ex;
        }
        if (SerialPort.IsOpen == false) {
          log.Fatal ($"GetAnalogIO ({ioNumber}): IO COM is not opened even after successfully opening the port");
          m_error = true;
          throw new Exception ("IO COM is not opened");
        }
      }
      // IONum = 1 is at adress 2086 = 0x0826
      // IONum = 2 is at adress 2118 = 0x0846
      // IONum = 3 is at adress 2150 = 0x0866
      // IONum = 4 is at adress 2182 = 0x0886
      if (log.IsDebugEnabled) {
        log.Debug ($"GetAnalogIO /B IO Number={ioNumber}");
      }

      byte[] command = new byte[5];
      // byte 0 : function type
      command[0] = 0x04; // read input registers
      // bytes 1 & 2 starting register adress
      command[1] = 0x08;
      switch (ioNumber) {
        case 1:
          command[2] = 0x26;
          break;
        case 2:
          command[2] = 0x46;
          break;
        case 3:
          command[2] = 0x66;
          break;
        case 4:
          command[2] = 0x86;
          break;
        default:
          log.Error ($"GetAnalogIO: I/O Number={ioNumber} > 4 is not supported");
          throw new ArgumentException ("Invalid I/O number");
      }
      // bytes 3 & 4 number of register to read = 1 (0x0001)
      command[3] = 0x00;
      command[4] = 0x01;
      try {
        SerialPort.Write (command, 0, 5);
        if (log.IsDebugEnabled) {
          log.Debug ($"GetAnalogIO IONum={ioNumber}: {command} has been written in IO Com");
        }
      }
      catch (Exception ex) {
        log.Error ($"GetAnalogIO IONum={ioNumber}: Write exception {ex.Message}", ex);
        SerialPort.Close ();
        m_error = true;
        throw ex;
      }

      const int MAX_BYTES_RES = 4;
      byte[] response = new byte[MAX_BYTES_RES];
      int totalread = 0;

      // Normally we should receive 3 bytes as a reply.
      do {
        try {
          int nb = SerialPort.Read (command, 0, MAX_BYTES_RES);
          if (log.IsDebugEnabled) {
            log.Debug ($"GetAnalogIO IONum={ioNumber}: read {nb} bytes");
          }
          for (int i = 0; i < nb; i++) {
            if (log.IsDebugEnabled) {
              log.Debug ($"GetAnalogIO IONum={ioNumber}: response[{totalread}] = {command[i]:X} ({command[i]})");
            }
            response[totalread] = command[i]; totalread++;
            if (totalread >= MAX_BYTES_RES) {
              break;
            }
          }
          if (totalread >= MAX_BYTES_RES) {
            break;
          }
        }
        catch (Exception ex) {
          log.Error ($"GetAnalogIO IONum={ioNumber}: Read exception", ex);
          SerialPort.Close ();
          m_error = true;
          throw ex;
        }
        if (log.IsDebugEnabled) {
          log.Debug ($"GetAnalogIO IONum={ioNumber}: Bytes to read {SerialPort.BytesToRead}");
        }
      }
      while (SerialPort.BytesToRead > 0);

      if (response[0] > 0x80) { // Error !
        log.Error ($"GetIO IONum={ioNumber}: got error number={response[1]}");
        GetErrorMessage (response[1]);
        m_error = true;
        throw new Exception ("Perle device error");
      }

      if (log.IsDebugEnabled) {
        log.Debug ($"GetAnalogIO /E IONum={ioNumber}: ended with response [2] = {response[2]:X}, response [3] = {response[3]:X}");
      }

      return (double)(response[2] * 0x100 + response[3]);
    }

    /// <summary>
    /// Start method: reset the error property
    /// </summary>
    public void Start ()
    {
      m_error = false;
    }

    private void GetErrorMessage (byte errorCode)
    {
      switch (errorCode) {
        case 0x01:
          log.Error ($"GetErrorMessage errorCode=1: Illegal Function, The function code received in the query is not an allowable action for the server (or slave).");
          break;
        case 0x02:
          log.Error ($"GetErrorMessage errorCode=2: Illegal Data, The data address received in the query is not an allowable address for the server (or slave).");
          break;
        case 0x03:
          log.Error ($"GetErrorMessage errorCode=3: Illegal Data Value, A value contained in the query data field is not an allowable value for the server (or slave).");
          break;
        case 0x04:
          log.Error ($"GetErrorMessage errorCode=4: Slave Device, An unrecoverable error occured while the server (or slave) was attempting to perform the requested action.");
          break;
        default:
          log.Error ($"GetErrorMessage errorCode={errorCode}: unknown error");
          break;
      }
    }
  }
}