// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

using Lemoine.Net;
using Lemoine.Core.Log;


namespace Lemoine.Cnc
{
  /// <summary>
  /// Big endian short value
  /// </summary>
  [StructLayout (LayoutKind.Sequential, Pack = 1)]
  internal struct BigEndianShort
  {
    ushort m_bigEndianValue;
    /// <summary>
    /// Get the little endian ushort value
    /// </summary>
    /// <returns></returns>
    public ushort GetValue ()
    {
      return Convert.ToUInt16 (((m_bigEndianValue << 8) | (m_bigEndianValue >> 8)) & 0x0000ffff);
    }
  }

  /// <summary>
  /// Big endian int value
  /// </summary>
  [StructLayout (LayoutKind.Sequential, Pack = 1)]
  internal struct BigEndianInt
  {
    uint m_bigEndianValue;
    /// <summary>
    /// Get the little endian uint value
    /// </summary>
    /// <returns></returns>
    public uint GetValue ()
    {
      return (m_bigEndianValue << 24) | ((m_bigEndianValue >> 8) & 0x00ff0000)
        | ((m_bigEndianValue >> 8) & 0x0000ff00) | (m_bigEndianValue >> 24);
    }
  }

  /// <summary>
  /// UDP Broadcast Packet
  /// </summary>
  [StructLayout (LayoutKind.Sequential, Pack = 1)]
  internal struct UdpPacket
  {
    /// <summary>
    /// The current version of the format of I/O UDP broadcast packet
    /// </summary>
    public byte Version;
    /// <summary>
    /// The total length of the UDP packet
    /// </summary>
    public BigEndianShort Length;
    /// <summary>
    /// The Analog Section of the UDP packet, which contains data/status of the
    /// Analog I/O channels. The Analog Channel Data subsection (within the Analog Section) will only
    /// exist if the channel(s) is enabled
    /// </summary>
    public AnalogSection AnalogSection;
  }

  /// <summary>
  /// The Analog Section of the UDP packet, which contains data/status of the
  /// Analog I/O channels. The Analog Channel Data subsection (within the Analog Section) will only
  /// exist if the channel(s) is enabled
  /// </summary>
  [StructLayout (LayoutKind.Sequential, Pack = 1)]
  internal struct AnalogSection
  {
    /// <summary>
    /// The total length of the Analog section (this value will vary, the field length is 2
    /// Bytes). This value will vary because it will contain one Analog Channel Data subsection
    /// (18 bytes) for each Analog channel that is enabled
    /// 
    /// The value is 1 (only the ChannelEnabled byte) in case no channel is enabled
    /// </summary>
    public BigEndianShort SectionLength;
    /// <summary>
    /// The Channel Enabled field is 1 byte in least significant bit order, for each
    /// channel. If the channel is enabled, the bit is set to 1. If the channel is disabled, the bit is set to 0 (zero)
    /// </summary>
    public byte ChannelEnabled;
  }

  /// <summary>
  /// Consists of Analog Channel Data for each enabled Analog channel on
  /// the IOLAN. If an Analog channel is disabled, there is no data for that channel. Therefore, the
  /// Analog Section will contain the Section Length value, the Channel Enabled value, and 18 bytes
  /// of I/O data for each enabled Analog channel. For example, an IOLAN I/O model with four
  /// Analog channels that has only three of those Analog channels enabled will contain 54 bytes of
  /// Analog Channel Data (18 bytes * 3 Analog channels)
  /// </summary>
  [StructLayout (LayoutKind.Sequential, Pack = 1, Size = 18)]
  internal struct AnalogChannelData
  {
    /// <summary>
    /// The current raw value received by the channel
    /// </summary>
    public BigEndianShort CurRawValue;
    /// <summary>
    /// The minimum raw value received by the channel until it is cleared
    /// </summary>
    public BigEndianShort MinRawValue;
    /// <summary>
    /// The maximum raw value received by the channel until it is cleared
    /// </summary>
    public BigEndianShort MaxRawValue;
    /// <summary>
    /// The current raw value that has been converted to voltage/current for Analog
    /// or Celsius/Fahrenheit for Temperature
    /// </summary>
    public BigEndianInt CurEngValue;
    /// <summary>
    /// The minimum raw value that has been converted to voltage/current for
    /// Analog or Celsius/Fahrenheit for Temperature until it is cleared
    /// </summary>
    public BigEndianInt MinEngValue;
    /// <summary>
    /// The maximum raw value that has been converted to voltage/current for
    /// Analog or Celsius/Fahrenheit for Temperature until it is cleared
    /// </summary>
    public BigEndianInt MaxEngValue;
  }

  /// <summary>
  /// Digital/Relay Section
  /// 
  /// The Digital/Relay Section of the UDP packet provides the status of Digital and Relay channels. The
  /// data for the status of each channel is represented by 1 byte, with each bit representing a channel (least
  /// significant bit format)
  /// 
  /// The Digital/Relay Channel Data subsection is present in the UDP packet regardless of
  /// whether or not the IOLAN model supports Digital/Relay channels
  /// </summary>
  [StructLayout (LayoutKind.Sequential, Pack = 1)]
  internal struct DigitalSection
  {
    /// <summary>
    /// The length of Digital/Relay Section within the UDP packet (this value will always be 2 Bytes)
    /// </summary>
    public BigEndianShort Length;
    /// <summary>
    /// This is based on the configuration of the Digital/Relay channels. The
    /// Channel Enabled field is 1 byte in least significant bit order, for each channel. If the channel is
    /// enabled, the bit is set to 1. If the channel is disabled, the bit is set to 0 (zero)
    /// </summary>
    public byte ChannelEnabled;
    /// <summary>
    /// Each bit represents a channel status, 1 for on or 0 for off (unless
    /// the channel has been configured to be inverted, in which case 0 is on and 1 if off)
    /// </summary>
    public byte DigitalData;
  }

  /// <summary>
  /// Serial Pin Signal Section
  /// 
  /// The Serial Pin Signal Section of the UDP packet provides the status of the serial pin signals from the
  /// IOLAN’s serial port. Each serial pin signal (DSR, DTR, CTS, etc.) is mapped to a bit in the 1-byte
  /// data section
  /// 
  /// The Serial Pin Signal Data subsection is present in the UDP packet regardless of whether or
  /// not the serial port is configured for the Control I/O profile or the serial pin signals are
  /// enabled
  /// </summary>
  [StructLayout (LayoutKind.Sequential, Pack = 1)]
  internal struct SerialSection
  {
    /// <summary>
    /// The total length of the Serial Pin Signal Data (this value will always be 2 Bytes)
    /// </summary>
    public BigEndianShort Length;
    /// <summary>
    /// This based upon the configuration of the signal pins on the serial port. When the
    /// serial port profile is set to Control I/O and a serial pin signal(s) is enabled, the bit is set to 1. For
    /// any serial pin signals that are disabled, the bit is set to 0 (zero) and any data associated with those
    /// serial pin signals should be ignored
    /// </summary>
    public byte PinEnabled;
    /// <summary>
    /// 1 byte with each bit being set to high (1) or low (0) for the appropriate
    /// serial pin signals
    /// 
    /// <item>Bit 1: DSR</item>
    /// <item>Bit 2: DSD</item>
    /// <item>Bit 3: CTS</item>
    /// <item>Bit 4: DTR</item>
    /// <item>Bit 5: RTS</item>
    /// </summary>
    public byte SerialPinSignalData;
  }


  /// <summary>
  /// CNC module to get the data from a Perle unit with the UDP protocol
  /// </summary>
  public class PerleToUDP : Lemoine.Cnc.BaseCncModule, Lemoine.Cnc.ICncModule, IDisposable
  {
    static readonly TimeSpan DEFAULT_TIME_OUT = TimeSpan.FromSeconds (60);

    #region Members
    int m_portNumber;
    TimeSpan m_timeOut = DEFAULT_TIME_OUT;
    bool m_error = false;

    UdpListener m_listener = null;
    Thread m_thread;

    Object m_dataLock = new Object ();
    byte[] m_receivedData;
    DateTime m_lastDataTimeStamp;
    #endregion // Members

    #region Getters / Setters
    /// <summary>
    /// UDP port number to listen
    /// </summary>
    public int PortNumber
    {
      get { return m_portNumber; }
      set { m_portNumber = value; }
    }

    /// <summary>
    /// Time (in s) after which a retrieved data is considered invalid.
    /// 
    /// The default time out is 60s = 1min
    /// </summary>
    public double TimeOutSeconds
    {
      get { return m_timeOut.TotalSeconds; }
      set { m_timeOut = TimeSpan.FromSeconds (value); }
    }

    /// <summary>
    /// Error ?
    /// </summary>
    public bool Error
    {
      get { return m_error; }
    }
    #endregion // Getters / Setters

    #region Constructors / Destructor / ToString methods
    /// <summary>
    /// Constructor
    /// </summary>
    public PerleToUDP ()
      : base ("Lemoine.Cnc.In.PerleToUDP")
    {
    }

    /// <summary>
    /// <see cref="IDisposable.Dispose" />
    /// </summary>
    public void Dispose ()
    {
      // Stop the UDP listener
      m_listener.Stop ();
      Thread.Sleep (100); // Sleep 100ms
      m_thread.Abort ();

      GC.SuppressFinalize (this);
    }
    #endregion // Constructors / Destructor / ToString methods

    #region Methods
    /// <summary>
    /// Start method
    /// </summary>
    public void Start ()
    {
      // Reset the error
      m_error = false;

      if (null == m_listener) { // Not started yet
        m_listener = new UdpListener (m_portNumber, new UdpListener.Callback (UdpListenerCallback));
        m_thread = new Thread (new ThreadStart (m_listener.Run));
        m_thread.Start ();
      }
    }

    void UdpListenerCallback (byte[] receivedData)
    {
      lock (m_dataLock) {
        if (log.IsDebugEnabled) {
          for (var i = 0; i < receivedData.Length; ++i) {
            log.Debug ($"UdpListenerCallback: received data[{i}]={receivedData[i]}");
          }
        }
        m_receivedData = receivedData;
        m_lastDataTimeStamp = DateTime.UtcNow;
      }
    }

    /// <summary>
    /// Get a digital I/O status given an I/O number
    /// 
    /// Use a number:
    /// <item>between 1 and 4 for D4, D2R2 or A4D2</item>
    /// </summary>
    /// <param name="param">I/O number</param>
    /// <returns></returns>
    public bool GetDigitalIO (string param)
    {
      if (string.IsNullOrEmpty (param)) {
        if (log.IsDebugEnabled) {
          log.Debug ($"GetDigitalIO: empty param, discard the call");
        }
        throw new ArgumentOutOfRangeException ("param", "empty");
      }

      byte ioNumber;
      try {
        ioNumber = byte.Parse (param);
      }
      catch (Exception ex) {
        log.Error ($"GetDigitalIO: invalid param {param} for IO number", ex);
        throw;
      }
      return GetDigitalIO (ioNumber);
    }

    /// <summary>
    /// Get a digital I/O status given an I/O number
    /// 
    /// Use always here a number between 1 and 4
    /// </summary>
    /// <param name="ioNumber">I/O number</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException">The specified I/O number is invalid, it is &lt;= 0</exception>
    public bool GetDigitalIO (byte ioNumber)
    {
      if (0 == ioNumber) {
        log.Debug ("GetDigitalIO: ioNumber is 0, skip this instruction");
        throw new ArgumentException ("Default I/O number", "ioNumber");
      }

      if (ioNumber <= 0) {
        log.Error ($"GetDigitalIO: ioNumber {ioNumber} is invalid, it is <= 0");
        throw new ArgumentException ("Invalid I/O number, less or equal to 0", "ioNumber");
      }

      if ((ioNumber < 1) || (4 < ioNumber)) {
        log.Warn ($"GetDigitalIO: the ioNumber {ioNumber} is probably invalid, it should be between 1 and 4");
      }

      lock (m_dataLock) {
        if (m_timeOut < DateTime.UtcNow.Subtract (m_lastDataTimeStamp)) {
          // TimeOut. The data is too old
          log.Error ($"GetDigitalIO: Time out. The data is too old. Time stamp={m_lastDataTimeStamp}");
          m_error = true;
          throw new TimeoutException ();
        }

        // Parse the UDP packet
        GCHandle udpPacketHandle = GCHandle.Alloc (m_receivedData, GCHandleType.Pinned);
        UdpPacket udpPacket = (UdpPacket)Marshal.PtrToStructure (udpPacketHandle.AddrOfPinnedObject (),
                                                                 typeof (UdpPacket));
        ushort analogSectionLength = udpPacket.AnalogSection.SectionLength.GetValue ();
        IntPtr digitalSectionAddress =
          new IntPtr (udpPacketHandle.AddrOfPinnedObject ().ToInt32 ()
                      + 5 // The analog data section begins at byte 5 (after Version, PacketLength and AnalogSectionLength)
                      + analogSectionLength);
        DigitalSection digitalSection = (DigitalSection)Marshal.PtrToStructure (digitalSectionAddress,
                                                                               typeof (DigitalSection));
        udpPacketHandle.Free ();

        // Check the channel is enabled, else raise an exception not to store the data
        if (!GetBit (digitalSection.ChannelEnabled, ioNumber)) {
          log.WarnFormat ($"GetDigitalIO: channel {ioNumber} is not enabled ! ChannelEnabled={0:X}",
                          digitalSection.ChannelEnabled);
          throw new InvalidOperationException ("Channel not enabled");
        }

        // Read the data
        byte digitalData = digitalSection.DigitalData;
        if (log.IsDebugEnabled) {
          log.DebugFormat ("GetDigitalIO: digital data is {0:X}",
                           digitalData);
        }
        bool result = GetBit (digitalData, ioNumber);
        return result;
      }
    }

    /// <summary>
    /// Get the raw value of an analog I/O given an I/O number
    /// 
    /// Use a number:
    /// <item>between 1 and 4 for A4D2</item>
    /// </summary>
    /// <param name="param">I/O number</param>
    /// <returns></returns>
    public double GetAnalogRaw (string param)
    {
      if (string.IsNullOrEmpty (param)) {
        if (log.IsDebugEnabled) {
          log.Debug ($"GetAnalogRaw: empty param, discard the call");
        }
        throw new ArgumentOutOfRangeException ("param", "empty");
      }

      byte ioNumber;
      try {
        ioNumber = byte.Parse (param);
      }
      catch (Exception ex) {
        log.Error ($"GetAnalogRaw: invalid param {param} for IO number", ex);
        throw;
      }
      return GetAnalogRaw (ioNumber);
    }

    /// <summary>
    /// Get the raw value of an analog I/O given an I/O number
    /// 
    /// Use always here a number between 1 and 4
    /// </summary>
    /// <param name="ioNumber">I/O number</param>
    /// <returns></returns>
    public double GetAnalogRaw (byte ioNumber)
    {
      if ((ioNumber < 1) || (4 < ioNumber)) {
        log.Warn ($"GetAnalogRaw: the ioNumber {ioNumber} is probably invalid, it should be between 1 and 4");
      }

      lock (m_dataLock) {
        if (m_timeOut < DateTime.UtcNow.Subtract (m_lastDataTimeStamp)) {
          // TimeOut. The data is too old
          log.Error ($"GetAnalogRaw: Time out. The data is too old. Time stamp={m_lastDataTimeStamp}");
          m_error = true;
          throw new TimeoutException ();
        }

        // Parse the UDP packet
        GCHandle udpPacketHandle = GCHandle.Alloc (m_receivedData, GCHandleType.Pinned);
        UdpPacket udpPacket = (UdpPacket)Marshal.PtrToStructure (udpPacketHandle.AddrOfPinnedObject (),
                                                                 typeof (UdpPacket));
        IntPtr analogSectionAddress =
          new IntPtr (udpPacketHandle.AddrOfPinnedObject ().ToInt32 ()
                      + 3); // To directly the the analog secion at byte 3 (after Version and PacketLength);
        AnalogSection analogSection = (AnalogSection)Marshal.PtrToStructure (analogSectionAddress,
                                                                             typeof (AnalogSection));
        // Check the channel is enabled, else raise an exception not to store the data
        if (!GetBit (analogSection.ChannelEnabled, ioNumber)) {
          log.WarnFormat ($"GetAnalogRaw: channel {ioNumber} is not enabled ! ChannelEnabled={0:X}",
                          analogSection.ChannelEnabled);
          throw new InvalidOperationException ("Channel not enabled");
        }
        // Get the data position from the the ChannelEnabled property
        int dataPosition = 0;
        for (int i = 1; i < ioNumber; i++) {
          if (GetBit (analogSection.ChannelEnabled, i)) {
            // A previous ioNumber was active, increase dataPosition
            ++dataPosition;
          }
        }
        IntPtr analogChannelDataAddress =
          new IntPtr (udpPacketHandle.AddrOfPinnedObject ().ToInt32 ()
                      + 6 // The analog channel data sections begin at byte 6 (after Version, PacketLength, AnalogSectionLength and AnalogChannelEnabled)
                      + dataPosition * 18);
        AnalogChannelData analogChannelData = (AnalogChannelData)Marshal.PtrToStructure (analogChannelDataAddress,
                                                                                        typeof (AnalogChannelData));
        udpPacketHandle.Free ();

        return analogChannelData.CurRawValue.GetValue ();
      }
    }

    /// <summary>
    /// Get the n-th bit in a byte
    /// </summary>
    /// <param name="data"></param>
    /// <param name="bitNumber">Number between 1 and 8</param>
    /// <returns></returns>
    bool GetBit (byte data, int bitNumber)
    {
      if ((bitNumber < 1) || (8 < bitNumber)) {
        log.Error ($"GetBit: bitNumber {bitNumber} is invalid, it must be between 1 and 8");
        Debug.Assert (false);
      }

      return (data & (1 << bitNumber - 1)) != 0;
    }
    #endregion // Methods
  }
}
