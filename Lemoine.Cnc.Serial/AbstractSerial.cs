// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.IO.Ports;
using Lemoine.Core.Log;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Abstract class for all the classes that are using the serial interface
  /// </summary>
  public abstract class AbstractSerial: Lemoine.Cnc.BaseCncModule, IDisposable
  {
    #region Members
    SerialPort serialPort = new SerialPort ();
    #endregion

    #region Getters / Setters
    /// <summary>
    /// Serial port that is used
    /// </summary>
    protected SerialPort SerialPort {
      get { return serialPort; }
    }
    
    /// <summary>
    /// Name of the serial port. Default is COM1.
    /// </summary>
    public string PortName {
      get { return serialPort.PortName; }
      set { serialPort.PortName = value; }
    }
    
    /// <summary>
    /// Baud rate of the serial port
    /// </summary>
    public int BaudRate {
      get { return serialPort.BaudRate; }
      set { serialPort.BaudRate = value; }
    }
    
    /// <summary>
    /// Parity of the serial port.
    /// This is one of the following values:
    /// <item>Even</item>
    /// <item>Odd</item>
    /// <item>None</item>
    /// <item>Mark</item>
    /// <item>Space</item>
    /// </summary>
    public string Parity {
      get
      {
        switch (serialPort.Parity) {
          case System.IO.Ports.Parity.Even:
            return "Even";
          case System.IO.Ports.Parity.Odd:
            return "Odd";
          case System.IO.Ports.Parity.None:
            return "None";
          case System.IO.Ports.Parity.Mark:
            return "Mark";
          case System.IO.Ports.Parity.Space:
            return "Space";
          default:
            log.FatalFormat ("Parity.set: " +
                             "unknown parity {0}",
                             serialPort.Parity);
            throw new Exception ("Unknown parity");
        }
      }
      set
      {
        if (value.Equals ("Even")) {
          serialPort.Parity = System.IO.Ports.Parity.Even;
        }
        else if (value.Equals ("Odd")) {
          serialPort.Parity = System.IO.Ports.Parity.Odd;
        }
        else if (value.Equals ("None")) {
          serialPort.Parity = System.IO.Ports.Parity.None;
        }
        else if (value.Equals ("Mark")) {
          serialPort.Parity = System.IO.Ports.Parity.Mark;
        }
        else if (value.Equals ("Space")) {
          serialPort.Parity = System.IO.Ports.Parity.Space;
        }
        else {
          log.ErrorFormat ("Parity.set: " +
                           "invalid parity {0}",
                           value);
          throw new ArgumentException ("Invalid serial parity");
        }
      }
    }
    
    /// <summary>
    /// Data bits of the serial port
    /// </summary>
    public int DataBits {
      get { return serialPort.DataBits; }
      set { serialPort.DataBits = value; }
    }
    
    /// <summary>
    /// Stop bits of the serial port.
    /// This is one of the following values:
    /// <item>1</item>
    /// <item>1.5</item>
    /// <item>2</item>
    /// </summary>
    public double StopBits {
      get
      {
        switch (serialPort.StopBits) {
          case System.IO.Ports.StopBits.One:
            return 1;
          case System.IO.Ports.StopBits.OnePointFive:
            return 1.5;
          case System.IO.Ports.StopBits.Two:
            return 2;
          default:
            log.FatalFormat ("StopBits.get: " +
                             "unknown stop bits {0}",
                             serialPort.StopBits);
            throw new Exception ("Unknown stop bits");
        }
      }
      set
      {
        if (1 == value) {
          serialPort.StopBits = System.IO.Ports.StopBits.One;
        }
        else if (1.5 == value) {
          serialPort.StopBits = System.IO.Ports.StopBits.OnePointFive;
        }
        else if (2 == value) {
          serialPort.StopBits = System.IO.Ports.StopBits.Two;
        }
        else {
          log.ErrorFormat ("StopBits.set: " +
                           "invalid value {0}",
                           value);
          throw new ArgumentException ("Invalid stop bits");
        }
      }
    }

    /// <summary>
    /// Handshake of the serial port.
    /// This is one of the following values:
    /// <item>None</item>
    /// <item>XOnXOff</item>
    /// <item>RequestToSend</item>
    /// <item>RequestToSendXOnXOff</item>
    /// </summary>
    public string Handshake {
      get
      {
        switch (serialPort.Handshake) {
          case System.IO.Ports.Handshake.None:
            return "None";
          case System.IO.Ports.Handshake.XOnXOff:
            return "XOnXOff";
          case System.IO.Ports.Handshake.RequestToSend:
            return "RequestToSend";
          case System.IO.Ports.Handshake.RequestToSendXOnXOff:
            return "RequestToSendXOnXOff";
          default:
            log.FatalFormat ("Handshake.set: " +
                             "unknown handshake {0}",
                             serialPort.Handshake);
            throw new Exception ("Unknown handshake");
        }
      }
      set
      {
        if (string.IsNullOrEmpty (value)) { // Default => None
          serialPort.Handshake = System.IO.Ports.Handshake.None;
        }
        else if (value.Equals ("None")) {
          serialPort.Handshake = System.IO.Ports.Handshake.None;
        }
        else if (value.Equals ("XOnXOff")) {
          serialPort.Handshake = System.IO.Ports.Handshake.XOnXOff;
        }
        else if (value.Equals ("RequestToSend")) {
          serialPort.Handshake = System.IO.Ports.Handshake.RequestToSend;
        }
        else if (value.Equals ("RequestToSendXOnXOff")) {
          serialPort.Handshake = System.IO.Ports.Handshake.RequestToSendXOnXOff;
        }
        else {
          log.ErrorFormat ("Handshake.set: " +
                           "invalid handshake {0}",
                           value);
          throw new ArgumentException ("Invalid handshake");
        }
      }
    }
    
    /// <summary>
    /// Character that is used to interpret the end of a line.
    /// 
    /// Special strings CRLF and LF are accepted
    /// 
    /// By default: System.Environment.NewLine
    /// </summary>
    public string NewLine {
      get { return serialPort.NewLine; }
      set
      {
        if (string.IsNullOrEmpty (value)) {
          serialPort.NewLine = System.Environment.NewLine;
        }
        if (value.Equals ("CRLF")) {
          serialPort.NewLine = "\r\n";
        }
        else if (value.Equals ("LF")) {
          serialPort.NewLine = "\n";
        }
        else {
          serialPort.NewLine = value;
        }
      }
    }
    
    /// <summary>
    /// Read timeout of the serial port in milliseconds
    /// 
    /// Default is infinite
    /// </summary>
    public int ReadTimeout {
      get { return serialPort.ReadTimeout; }
      set { serialPort.WriteTimeout = value; }
    }
    
    /// <summary>
    /// Write timeout of the serial port in milliseconds
    /// 
    /// Default is infinite
    /// </summary>
    public int WriteTimeout {
      get { return serialPort.WriteTimeout; }
      set { serialPort.WriteTimeout = value; }
    }
    #endregion

    #region Constructors / Destructors
    /// <summary>
    /// Description of the constructor
    /// </summary>
    public AbstractSerial ()
      : base(typeof(AbstractSerial).FullName)
    {
    }

    /// <summary>
    /// Description of the constructor
    /// </summary>
    public AbstractSerial (string name)
      : base (name)
    {
    }

    /// <summary>
    /// <see cref="IDisposable.Dispose" />
    /// </summary>
    public void Dispose ()
    {
      this.serialPort.Close ();
      
      GC.SuppressFinalize (this);
    }
    #endregion

    #region Methods
    #endregion
  }
}
