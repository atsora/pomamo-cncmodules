// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Wrapper for the library LibPlcTag
  /// </summary>
  static public class LibPlcTag
  {
    /// <summary>
    /// StatusCode returned by a plctag function
    /// </summary>
    public enum StatusCode
    {
      /// <summary>
      /// Operation in progress. Not an error
      /// </summary>
      STATUS_PENDING = 1,

      /// <summary>
      /// No error. The operation was successful or the state of the tag is good.
      /// </summary>
      STATUS_OK = 0,

      /// <summary>
      /// The operation was aborted
      /// </summary>
      ERR_ABORT = -1,

      /// <summary>
      /// The operation failed due to incorrect configuration. Usually returned from a remote system.
      /// </summary>
      ERR_BAD_CONFIG = -2,

      /// <summary>
      /// The connection failed for some reason. This can mean that the remote PLC was power cycled, for instance.
      /// </summary>
      ERR_BAD_CONNECTION = -3,

      /// <summary>
      /// Garbled or unexpected response from remote system
      /// 
      /// The data received from the remote PLC was undecipherable or otherwise not able to be processed.
      /// Can also be returned from a remote system that cannot process the data sent to it.
      /// </summary>
      ERR_BAD_DATA = -4,

      /// <summary>
      /// Illegal or unknown value for device type (CPU)
      /// 
      /// Usually returned from a remote system when something addressed does not exist
      /// </summary>
      ERR_BAD_DEVICE = -5,

      /// <summary>
      /// Garbled or unknown gateway IP
      /// 
      /// Usually returned when the library is unable to connect to a remote system
      /// </summary>
      ERR_BAD_GATEWAY = -6,

      /// <summary>
      /// Illegal or unknown parameter value
      /// 
      /// A common error return when something is not correct with the tag creation attribute string
      /// </summary>
      ERR_BAD_PARAM = -7,

      /// <summary>
      /// Usually returned when the remote system returned an unexpected response
      /// </summary>
      ERR_BAD_REPLY = -8,

      /// <summary>
      /// Usually returned by a remote system when something is not in a good state
      /// </summary>
      ERR_BAD_STATUS = -9,

      /// <summary>
      /// Error closing a socket or similar OS construct
      /// 
      /// An error occurred trying to close some resource
      /// </summary>
      ERR_CLOSE = -10,

      /// <summary>
      /// Error creating a tag or internal value
      /// 
      /// An error occurred trying to create some internal resource
      /// </summary>
      ERR_CREATE = -11,

      /// <summary>
      /// An error returned by a remote system when something is incorrectly duplicated (i.e. a duplicate connection ID)
      /// </summary>
      ERR_DUPLICATE = -12,

      /// <summary>
      /// Unable to encode part of the transaction
      /// 
      /// An error was returned when trying to encode some data such as a tag name.
      /// </summary>
      ERR_ENCODE = -13,

      /// <summary>
      /// Error while attempting to destroy OS-level mutex
      /// 
      /// An internal library error. It would be very unusual to see this
      /// </summary>
      ERR_MUTEX_DESTROY = -14,

      /// <summary>
      /// Error while attempting to initialize mutex
      /// 
      /// An internal library error. It would be very unusual to see this
      /// </summary>
      ERR_MUTEX_INIT = -15,

      /// <summary>
      /// Error while attempting to lock mutex
      /// 
      /// An internal library error. It would be very unusual to see this
      /// </summary>
      ERR_MUTEX_LOCK = -16,

      /// <summary>
      /// Error while attempting to unlock mutex
      /// 
      /// An internal library error. It would be very unusual to see this
      /// </summary>
      ERR_MUTEX_UNLOCK = -17,

      /// <summary>
      /// Operation not permitted
      /// 
      /// Often returned from the remote system when an operation is not permitted
      /// </summary>
      ERR_NOT_ALLOWED = -18,

      /// <summary>
      /// Operation failed due to target object not found
      /// 
      /// Often returned from the remote system when something is not found
      /// </summary>
      ERR_NOT_FOUND = -19,

      /// <summary>
      /// Tag operation not implemented
      /// 
      /// Returned when a valid operation is not implemented
      /// </summary>
      ERR_NOT_IMPLEMENTED = -20,

      /// <summary>
      /// No data received
      /// 
      /// Returned when expected data is not present
      /// </summary>
      ERR_NO_DATA = -21,

      /// <summary>
      /// Similar to NOT_FOUND
      /// </summary>
      ERR_NO_MATCH = -22,

      /// <summary>
      /// Unable to allocate memory
      /// 
      /// Returned by the library when memory allocation fails
      /// </summary>
      ERR_NO_MEM = -23,

      /// <summary>
      /// Returned by the remote system when some resource allocation fails
      /// </summary>
      ERR_NO_RESOURCES = -24,

      /// <summary>
      /// A null pointer was found during processing. Often this is returned when an argument is null
      /// 
      /// Usually an internal error, but can be returned when an invalid handle is used with an API call.
      /// </summary>
      ERR_NULL_PTR = -25,

      /// <summary>
      /// Error opening a socket or other OS-level item
      /// 
      /// Returned when an error occurs opening a resource such as a socket
      /// </summary>
      ERR_OPEN = -26,

      /// <summary>
      /// An attempt to access a value outside of the allow limits was made. Usually this is in conjunction with accessing a data word in a tag
      /// 
      /// Usually returned when trying to write a value into a tag outside of the tag data bounds
      /// </summary>
      ERR_OUT_OF_BOUNDS = -27,

      /// <summary>
      /// Error reading
      /// 
      /// Returned when an error occurs during a read operation. Usually related to socket problems
      /// </summary>
      ERR_READ = -28,

      /// <summary>
      /// Error reported from remote end
      /// 
      /// An unspecified or untranslatable remote error causes this
      /// </summary>
      ERR_REMOTE_ERR = -29,

      /// <summary>
      /// Unable to create thread
      /// 
      /// An internal library error. If you see this, it is likely that everything is about to crash
      /// </summary>
      ERR_THREAD_CREATE = -30,

      /// <summary>
      /// Unable to join with thread
      /// 
      /// Another internal library error. It is very unlikely that you will see this
      /// </summary>
      ERR_THREAD_JOIN = -31,

      /// <summary>
      /// Operation did not complete in the time allowed
      /// 
      /// An operation took too long and timed out
      /// </summary>
      ERR_TIMEOUT = -32,

      /// <summary>
      /// More data was returned than was expected
      /// </summary>
      ERR_TOO_LARGE = -33,

      /// <summary>
      /// Insufficient data was returned from the remote system
      /// </summary>
      ERR_TOO_SMALL = -34,

      /// <summary>
      /// Unsupported operation (i.e. tag type does not support the operation)
      /// 
      /// The operation is not supported on the remote system
      /// </summary>
      ERR_UNSUPPORTED = -35,

      /// <summary>
      /// (Windows only) Error initializing/terminating use of Windows sockets
      /// 
      /// A Winsock-specific error occurred (only on Windows)
      /// </summary>
      ERR_WINSOCK = -36,

      /// <summary>
      /// Error writing
      /// 
      /// An error occurred trying to write, usually to a socket
      /// </summary>
      ERR_WRITE = -37,

      /// <summary>
      /// Partial data was received or something was unexpectedly incomplete
      /// </summary>
      ERR_PARTIAL = -38,

      /// <summary>
      /// The operation cannot be performed as some other operation is taking place
      /// </summary>
      ERR_BUSY = -39,

      /// <summary>
      /// Extra state: tag just initialized and not reading yet
      /// </summary>
      INITIALIZING = -1000,
    }

    /// <summary>
    /// Create a new tag. Don't forget to use "Destroy"
    /// </summary>
    /// <param name="lpString"></param>
    /// <param name="timeout"></param>
    /// <returns></returns>
    [DllImport ("plctag.dll", EntryPoint = "plc_tag_create", CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr Create ([MarshalAs (UnmanagedType.LPStr)] string lpString, int timeout);

    /// <summary>
    /// Destroying a Tag
    /// 
    /// Tags handles use internal resources and must be freed.DO NOT use free ().
    /// Tags are more than a single block of memory internally (though we'd like that level of simplicity).
    /// To free a tag, the plc_tag_destroy() function must be called
    /// </summary>
    /// <param name="tag"></param>
    /// <returns></returns>
    [DllImport ("plctag.dll", EntryPoint = "plc_tag_destroy", CallingConvention = CallingConvention.Cdecl)]
    internal static extern StatusCode Destroy (IntPtr tag);

    /// <summary>
    /// Shutting Down the Library
    /// 
    /// Some wrappers and systems are not able to trigger the standard POSIX or Windows functions when the library is being unloaded or the program is shutting down.
    /// 
    /// After this function returns, the library will have cleaned up all internal threads and resources.You can immediately turn around and call plc_tag_create () again and the library will start up again.
    /// 
    /// Note: you must call plc_tag_destroy() on all open tag handles before calling plc_tag_shutdown().
    /// </summary>
    [DllImport ("plctag.dll", EntryPoint = "plc_tag_shutdown", CallingConvention = CallingConvention.Cdecl)]

    internal static extern void Shutdown ();

    /// <summary>
    /// Retrieving Tag Status
    /// 
    /// The status of a tag can be retrieved with the plc_tag_status () function.
    /// If there is an operation pending, this will return PLCTAG_STATUS_PENDING.
    /// All API operations set the tag status.
    /// </summary>
    /// <param name="tag"></param>
    /// <returns>the status code representing the current tag status</returns>
    [DllImport ("plctag.dll", EntryPoint = "plc_tag_status", CallingConvention = CallingConvention.Cdecl)]
    internal static extern StatusCode GetStatus (IntPtr tag);

    /// <summary>
    /// Decode an error
    /// 
    /// Use the plc_tag_decode_error() API function to print out status codes. It just translates the status code above to the string equivalent.
    /// 
    /// The string returned is a static C-style string. You may copy it, but do not attempt to free it in your program.
    /// 
    /// Not useful here due to the enum type ErrorCode that already associates a number to a textual description
    /// </summary>
    /// <param name="error"></param>
    /// <returns></returns>
    [DllImport ("plctag.dll", EntryPoint = "plc_tag_decode_error", CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr DecodeError (int error);

    /// <summary>
    /// Ask a tag to read its value
    /// </summary>
    /// <param name="tag"></param>
    /// <param name="timeout">If 0, the function returns immediately and the status can be "pending"</param>
    /// <returns></returns>
    [DllImport ("plctag.dll", EntryPoint = "plc_tag_read", CallingConvention = CallingConvention.Cdecl)]
    internal static extern StatusCode Read (IntPtr tag, int timeout);

    /// <summary>
    /// Read a UInt8 after the function "Read" is called and if the status is OK
    /// </summary>
    /// <param name="tag"></param>
    /// <param name="offset"></param>
    /// <returns></returns>
    [DllImport ("plctag.dll", EntryPoint = "plc_tag_get_uint8", CallingConvention = CallingConvention.Cdecl)]
    internal static extern byte GetUInt8 (IntPtr tag, int offset);

    /// <summary>
    /// Read a Int8 after the function "Read" is called and if the status is OK
    /// </summary>
    /// <param name="tag"></param>
    /// <param name="offset"></param>
    /// <returns></returns>
    [DllImport ("plctag.dll", EntryPoint = "plc_tag_get_int8", CallingConvention = CallingConvention.Cdecl)]
    internal static extern sbyte GetInt8 (IntPtr tag, int offset);

    /// <summary>
    /// Read a UInt16 after the function "Read" is called and if the status is OK
    /// </summary>
    /// <param name="tag"></param>
    /// <param name="offset"></param>
    /// <returns></returns>
    [DllImport ("plctag.dll", EntryPoint = "plc_tag_get_uint16", CallingConvention = CallingConvention.Cdecl)]
    internal static extern UInt16 GetUInt16 (IntPtr tag, int offset);

    /// <summary>
    /// Read a Int16 after the function "Read" is called and if the status is OK
    /// </summary>
    /// <param name="tag"></param>
    /// <param name="offset"></param>
    /// <returns></returns>
    [DllImport ("plctag.dll", EntryPoint = "plc_tag_get_int16", CallingConvention = CallingConvention.Cdecl)]
    internal static extern Int16 GetInt16 (IntPtr tag, int offset);

    /// <summary>
    /// Read a UInt32 after the function "Read" is called and if the status is OK
    /// </summary>
    /// <param name="tag"></param>
    /// <param name="offset"></param>
    /// <returns></returns>
    [DllImport ("plctag.dll", EntryPoint = "plc_tag_get_uint32", CallingConvention = CallingConvention.Cdecl)]
    internal static extern UInt32 GetUInt32 (IntPtr tag, int offset);

    /// <summary>
    /// Read a Int32 after the function "Read" is called and if the status is OK
    /// </summary>
    /// <param name="tag"></param>
    /// <param name="offset"></param>
    /// <returns></returns>
    [DllImport ("plctag.dll", EntryPoint = "plc_tag_get_int32", CallingConvention = CallingConvention.Cdecl)]
    internal static extern Int32 GetInt32 (IntPtr tag, int offset);

    /// <summary>
    /// Read a float after the function "Read" is called and if the status is OK
    /// </summary>
    /// <param name="tag"></param>
    /// <param name="offset"></param>
    /// <returns></returns>
    [DllImport ("plctag.dll", EntryPoint = "plc_tag_get_float32", CallingConvention = CallingConvention.Cdecl)]
    internal static extern float GetFloat (IntPtr tag, int offset);

    /// <summary>
    /// Read a bit after the function "Read" is called and if the status is OK
    /// </summary>
    /// <param name="tag"></param>
    /// <param name="offset"></param>
    /// <returns></returns>
    [DllImport ("plctag.dll", EntryPoint = "plc_tag_get_bit", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int GetBit (IntPtr tag, int offset);

    /// <summary>
    /// Read a string after the function "Read" is called and if the status is OK
    /// </summary>
    /// <param name="tag"></param>
    /// <param name="offset"></param>
    /// <returns></returns>
    [DllImport ("plctag.dll", EntryPoint = "plc_tag_get_string", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    internal static extern StatusCode GetString (IntPtr tag, int offset, StringBuilder buffer, int bufferLength);

    /// <summary>
    /// Get a string length after the function "Read" is called and if the status is OK
    /// </summary>
    /// <param name="tag"></param>
    /// <param name="offset"></param>
    /// <returns></returns>
    [DllImport ("plctag.dll", EntryPoint = "plc_tag_get_string_length", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int GetStringLength (IntPtr tag, int offset);

    /// <summary>
    /// Get a string capacity after the function "Read" is called and if the status is OK
    /// </summary>
    /// <param name="tag"></param>
    /// <param name="offset"></param>
    /// <returns></returns>
    [DllImport ("plctag.dll", EntryPoint = "plc_tag_get_string_capacity", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int GetStringCapacity (IntPtr tag, int offset);

    /// <summary>
    /// Get a string total length after the function "Read" is called and if the status is OK
    /// </summary>
    /// <param name="tag"></param>
    /// <param name="offset"></param>
    /// <returns></returns>
    [DllImport ("plctag.dll", EntryPoint = "plc_tag_get_string_total_length", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int GetStringTotalLength (IntPtr tag, int offset);

    /// <summary>
    /// Read raw bytes after the function "Read" is called and if the status is OK
    /// </summary>
    /// <param name="tag"></param>
    /// <param name="offset"></param>
    /// <returns></returns>
    [DllImport ("plctag.dll", EntryPoint = "plc_tag_get_raw_bytes", CallingConvention = CallingConvention.Cdecl)]
    internal static extern StatusCode GetRawBytes (IntPtr tag, int offset, [Out] byte[] buffer, int bufferLength);

    #region Write
    /// <summary>
    /// Ask a tag to write its value
    /// </summary>
    /// <param name="tag"></param>
    /// <param name="timeout"></param>
    /// <returns></returns>
    [DllImport ("plctag.dll", EntryPoint = "plc_tag_write", CallingConvention = CallingConvention.Cdecl)]
    internal static extern StatusCode Write (IntPtr tag, int timeout);

    /// <summary>
    /// Set a UInt8 value. The function "Write" must then be called.
    /// </summary>
    /// <param name="tag"></param>
    /// <param name="offset"></param>
    /// <param name="val"></param>
    /// <returns></returns>
    [DllImport ("plctag.dll", EntryPoint = "plc_tag_set_uint8", CallingConvention = CallingConvention.Cdecl)]
    internal static extern StatusCode SetUInt8 (IntPtr tag, int offset, byte val);

    /// <summary>
    /// Set a Int8 value. The function "Write" must then be called.
    /// </summary>
    /// <param name="tag"></param>
    /// <param name="offset"></param>
    /// <param name="val"></param>
    /// <returns></returns>
    [DllImport ("plctag.dll", EntryPoint = "plc_tag_set_int8", CallingConvention = CallingConvention.Cdecl)]
    internal static extern StatusCode SetInt8 (IntPtr tag, int offset, sbyte val);

    /// <summary>
    /// Set a UInt16 value. The function "Write" must then be called.
    /// </summary>
    /// <param name="tag"></param>
    /// <param name="offset"></param>
    /// <param name="val"></param>
    /// <returns></returns>
    [DllImport ("plctag.dll", EntryPoint = "plc_tag_set_uint16", CallingConvention = CallingConvention.Cdecl)]
    internal static extern StatusCode SetUInt16 (IntPtr tag, int offset, ushort val);

    /// <summary>
    /// Set a Int16 value. The function "Write" must then be called.
    /// </summary>
    /// <param name="tag"></param>
    /// <param name="offset"></param>
    /// <param name="val"></param>
    /// <returns></returns>
    [DllImport ("plctag.dll", EntryPoint = "plc_tag_set_int16", CallingConvention = CallingConvention.Cdecl)]
    internal static extern StatusCode SetInt16 (IntPtr tag, int offset, short val);

    /// <summary>
    /// Set a UInt32 value. The function "Write" must then be called.
    /// </summary>
    /// <param name="tag"></param>
    /// <param name="offset"></param>
    /// <param name="val"></param>
    /// <returns></returns>
    [DllImport ("plctag.dll", EntryPoint = "plc_tag_set_uint32", CallingConvention = CallingConvention.Cdecl)]
    internal static extern StatusCode SetUInt32 (IntPtr tag, int offset, uint val);

    /// <summary>
    /// Set a Int32 value. The function "Write" must then be called.
    /// </summary>
    /// <param name="tag"></param>
    /// <param name="offset"></param>
    /// <param name="val"></param>
    /// <returns></returns>
    [DllImport ("plctag.dll", EntryPoint = "plc_tag_set_int32", CallingConvention = CallingConvention.Cdecl)]
    internal static extern StatusCode SetInt32 (IntPtr tag, int offset, int val);

    /// <summary>
    /// Set a float value. The function "Write" must then be called.
    /// </summary>
    /// <param name="tag"></param>
    /// <param name="offset"></param>
    /// <param name="val"></param>
    /// <returns></returns>
    [DllImport ("plctag.dll", EntryPoint = "plc_tag_set_float32", CallingConvention = CallingConvention.Cdecl)]
    internal static extern StatusCode SetFloat (IntPtr tag, int offset, float val);

    /// <summary>
    /// Set a string value. The function "Write" must then be called.
    /// </summary>
    [DllImport ("plctag.dll", EntryPoint = "plc_tag_set_string", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    internal static extern StatusCode SetString (IntPtr tag, int offset, [MarshalAs (UnmanagedType.LPStr)] string val);

    /// <summary>
    /// Set raw bytes. The function "Write" must then be called.
    /// </summary>
    [DllImport ("plctag.dll", EntryPoint = "plc_tag_set_raw_bytes", CallingConvention = CallingConvention.Cdecl)]
    internal static extern StatusCode SetRawBytes (IntPtr tag, int offset, [In] byte[] buffer, int bufferLength);
    #endregion Write

    #region Debugging
    /// <summary>
    /// Set the debug level.
    ///
    /// This function takes values from the defined debug levels below.  It sets
    /// the debug level to the passed value.Higher numbers output increasing amounts
    /// of information.Input values not defined below will be ignored.
    /// </summary>
    /// <param name="debugLevel"></param>
    [DllImport ("plctag.dll", EntryPoint = "plc_tag_set_debug_level", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void SetDebugLevel (PlcTagDebugLevel debugLevel);

    /// <summary>
    /// Log callback
    /// </summary>
    /// <param name="tag"></param>
    /// <param name="debugLevel"></param>
    /// <param name="message"></param>
    [UnmanagedFunctionPointer (CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public delegate void LogCallbackFunc (Int32 tag, PlcTagDebugLevel debugLevel, [MarshalAs (UnmanagedType.LPStr)] string message);

    /// <summary>
    /// This function registers the passed callback function with the library.Only one callback function
    /// may be registered with the library at a time!
    ///
    /// Once registered, the function will be called with any logging message that is normally printed due
    /// to the current log level setting.
    ///
    /// WARNING: the callback will usually be called when the internal tag API mutex is held.You cannot
    /// call any tag functions within the callback!
    ///
    /// Return values:
    ///
    /// If there is already a callback registered, the function will return PLCTAG_ERR_DUPLICATE.Only one callback
    /// function may be registered at a time on each tag.
    ///
    /// If all is successful, the function will return PLCTAG_STATUS_OK. 
    /// </summary>
    /// <param name="func"></param>
    /// <returns></returns>
    [DllImport ("plctag.dll", EntryPoint = "plc_tag_register_logger", CallingConvention = CallingConvention.Cdecl)]
    internal static extern StatusCode RegisterLogger (LogCallbackFunc func);

    /// <summary>
    /// This function removes the callback already registered on the tag.
    ///
    /// Return values:
    ///
    /// The function returns PLCTAG_STATUS_OK if there was a registered callback and removing it went well.
    /// An error of PLCTAG_ERR_NOT_FOUND is returned if there was no registered callback.
    /// </summary>
    /// <param name="tag"></param>
    /// <returns></returns>
    [DllImport ("plctag.dll", EntryPoint = "plc_tag_unregister_logger", CallingConvention = CallingConvention.Cdecl)]
    internal static extern StatusCode UnregisterLogger (Int32 tag);
    #endregion // Debugging
  }
}
