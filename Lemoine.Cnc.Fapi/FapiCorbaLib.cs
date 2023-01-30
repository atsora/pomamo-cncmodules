// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Text;
using System.Runtime.InteropServices;

namespace Lemoine.Cnc
{
  #region Enums
  /// <summary>
  /// Fanuc error codes
  /// </summary>
  enum FapiError
  {
    /// <summary>
    /// The license level does not allow this operation
    /// </summary>
    FCL_RET_INVALID_LICENSE = -9,
    /// <summary>
    /// The current connection id is not valid
    /// </summary>
    FCL_RET_INVALID_CON_ID = -8,
    /// <summary>
    /// Generic failure (tcp connection, crash, etc)
    /// </summary>
    FCL_RET_FAILED = -7,
    /// <summary>
    /// Parameter introspection of a non existing sub parameter
    /// </summary>
    FCL_RET_SUBPAR_NOT_FOUND = -6,
    /// <summary>
    /// Parameter introspection of a non existing parameter
    /// </summary>
    FCL_RET_PAR_NOT_FOUND = -5,
    /// <summary>
    /// Execution timeout ( 30 [s] for commands 1 [h] for Blocks and 100 [h] for files )
    /// </summary>
    FCL_RET_TIMEOUT = -4,
    /// <summary>
    /// The CNC aborts the operation after starts.
    /// </summary>
    FCL_RET_ABORTED = -3,
    /// <summary>
    /// The CNC cannot start the request at this time. It is refused before starts
    /// </summary>
    FCL_RET_REFUSED = -2,
    /// <summary>
    /// The function is only ACCEPTED by CNC (Asynchronous mode) but not yet EXECUTED
    /// </summary>
    FCL_RET_ACCEPTED = 0,
    /// <summary>
    /// Execution SUCCESSFULL
    /// </summary>
    FCL_RET_EXECUTED = 1,
    /// <summary>
    /// Every number more than this and less than FCL_RET_MAX_SES_ID is a valid session Id. (Asynchronous mode)
    /// </summary>
    FCL_RET_VALID_SES_ID = 5,
    /// <summary>
    /// Maximum of the session number
    /// </summary>
    FCL_RET_MAX_SES_ID = 32752
  };
  #endregion // Enums

  #region Imported methods from Import.FwLib32
  struct FapiCorbaLib
  {
    /// <summary>
    /// Default Connection TCP Port
    /// </summary>
    internal const int FCL_DEFAULT_SERVER_PORT = 2048;

    /// <summary>
    /// The max number of connections allowed.
    /// </summary>
    internal const int FCL_MAX_CONNECTIONS = 32;
    
    /// <summary>
    /// The start number of connection Identifier.
    /// </summary>
    internal const int FCL_START_CONNECTION_ID = 0;
    
    /// <summary>
    /// The return connection Id in case of failure. Used in FapiClose() for terminate all
    /// </summary>
    internal const int FCL_INVALID_CONNECTION_ID = -1;
    
    /// <summary>
    /// Length of a name
    /// </summary>
    public const int NAME_LENGTH = 64;
    
    /// <summary>
    /// Length of a value
    /// </summary>
    public const int VALUE_LENGTH = 128;
    
    /// <summary>
    /// Length of a description
    /// </summary>
    public const int DESCRIPTION_LENGTH = 128;
    
    
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    internal struct StringName
    {
      [MarshalAs(UnmanagedType.ByValTStr, SizeConst = NAME_LENGTH)]
      public string msg;
    }
    
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    internal struct StringValue
    {
      [MarshalAs(UnmanagedType.ByValTStr, SizeConst = VALUE_LENGTH)]
      public string msg;
    }
    
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    internal struct StringDescription
    {
      [MarshalAs(UnmanagedType.ByValTStr, SizeConst = DESCRIPTION_LENGTH)]
      public string msg;
    }
    

    /// <summary>
    /// Returns system time [ms] for calculations
    /// </summary>
    /// <returns>system time</returns>
    [DllImport("FapiCorbaLib.dll", EntryPoint = "GetTimeStamp")]
    internal static extern uint GetTimeStamp();
    
    /// <summary>
    /// Returns license information (if the FapiCorbaLib is licenced)
    /// </summary>
    /// <param name="pLicencenseInfo_out">A string  containing the license info of FapiCorbaLib.dll</param>
    /// <param name="nLicenseInfoSize">nLicenseInfoSize the real size of pLicencenseInfo_out vector</param>
    /// <returns>FCL_RET_EXECUTED if the FapiCorbaLib is licensed; the pLicencenseInfo_out is valorized
    /// FCL_RET_FAILED if no license is available: not possible to use the DLL. The FapiConnect will return FCL_RET_REFUSED
    /// </returns>
    [DllImport("FapiCorbaLib.dll", EntryPoint = "GetFapiCorbaDllLicenseInfo")]
    internal static extern FapiError GetFapiCorbaDllLicenseInfo(StringBuilder pLicencenseInfo_out, ref int nLicenseInfoSize);
    
    /// <summary>
    /// Returns a string containing the FAPI CORBA DLL version
    /// </summary>
    /// <returns>A string containing the version of file FapiCorbaLib.dll</returns>
    [DllImport("FapiCorbaLib.dll", EntryPoint = "GetFapiCorbaDllVersion", CharSet = CharSet.Ansi)]
    internal static extern string GetFapiCorbaDllVersion();
    
    /// <summary>
    /// This function creates a connection to @b FapiCorbaServer
    /// </summary>
    /// <param name="pcHost">pcHost is a constant string containing the host name</param>
    /// <param name="nPort">nPort is a constant number indicating the port number (must be FCL_DEFAULT_SERVER_PORT)</param>
    /// <param name="connectionId">connection identifier stored if FCL_RET_EXECUTED is returned</param>
    /// <returns>FCL_RET_EXECUTED if the connection has been correctly established
    /// FCL_RET_FAILED if the connection could not be created correctly, for instance, due to a wrong IP address.
    /// FCL_RET_REFUSED if pConId is NULL or the license is not available</returns>
    [DllImport("FapiCorbaLib.dll", EntryPoint = "FapiConnect", CharSet = CharSet.Ansi)]
    internal static extern FapiError FapiConnect(string pcHost, int nPort, ref int connectionId);
    
    /// <summary>
    /// This function closes the connection to FapiCorbaServer, if previousely opened
    /// </summary>
    /// <param name="connectionId">The identifier of the connection that must be closed.
    /// Note: FCL_INVALID_CONNECTION_ID close all active connections</param>
    /// <returns>FCL_RET_EXECUTED if the connection has been correctly deleted
    /// FCL_RET_REFUSED if the connection was already closed</returns>
    [DllImport("FapiCorbaLib.dll", EntryPoint = "FapiClose")]
    internal static extern FapiError FapiClose(int connectionId);
    
    /// <summary>
    /// This function is used to execute a CNC block. The command is typed in a string.
    /// For instance, "G00X1.0Y1.0Z1.0" commands a rapid movement to the indicated position.
    /// The command is executed in synchronous mode: the function terminates when the block is executed
    /// </summary>
    /// <param name="connectionId">Connection Identifier returned by the FapiConnect() on success</param>
    /// <param name="pcBlock">command to be executed</param>
    /// <returns>FCL_RET_EXECUTED if the command has been executed correctly
    /// FCL_RET_TIMEOUT if the command is failed for timeout (for a block is around 1 hour).
    /// FCL_RET_FAILED if the connection falls
    /// FCL_RET_REFUSED if the command is not executable for the CNC (for example CNC not in automatic)
    /// FCL_RET_INVALID_LICENSE if the FapiCorbaLib has the READ_ONLY license level</returns>
    [DllImport("FapiCorbaLib.dll", EntryPoint = "FapiExecSyncBlock", CharSet = CharSet.Ansi)]
    internal static extern FapiError FapiExecSyncBlock(int connectionId, string pcBlock);
    
    /// <summary>
    /// This function is used to execute a CNC block. The command is typed in a string, for instance "G00X1.0Y1.0Z1.0".
    /// The command is executed in Asynchronous mode: The function terminates when the block is only ACCEPTED.
    /// Warning: The execution state and the session Id will be in the callback and can be FCL_RET_EXECUTED or FCL_RET_ABORTED
    /// </summary>
    /// <param name="connectionId">Connection Identifier returned by the FapiConnect() on success</param>
    /// <param name="pcBlock">block to be executed</param>
    /// <returns>FCL_RET_VALID_SES_ID if the command has been @b ACCEPTED correctly and is started.
    /// FCL_RET_TIMEOUT if the command is failed for timeout (around 30s)
    /// FCL_RET_FAILED if the connection falls.
    /// FCL_RET_REFUSED if the command is not executable for the CNC (for example CNC not in automatic)
    /// FCL_RET_INVALID_LICENSE if the FapiCorbaLib has the READ_ONLY license level</returns>
    [DllImport("FapiCorbaLib.dll", EntryPoint = "FapiExecAsyncBlock", CharSet = CharSet.Ansi)]
    internal static extern FapiError FapiExecAsyncBlock(int connectionId, string pcBlock);
    
    /// <summary>
    /// Abort the current block execution (if any)
    /// </summary>
    /// <param name="connectionId">Connection Identifier returned by the FapiConnect() on success</param>
    /// <returns>FCL_RET_EXECUTED if the block is actually aborted (or if no block is in execution)
    /// FCL_RET_FAILED if the connection falls.
    /// FCL_RET_INVALID_LICENSE if the FapiCorbaLib has the READ_ONLY license level</returns>
    [DllImport("FapiCorbaLib.dll", EntryPoint = "FapiAbortBlock")]
    internal static extern FapiError FapiAbortBlock(int connectionId);
    
    /// <summary>
    /// Set the batch execution mode. This means that if the CNC is in batch mode, only at the first block
    /// executed the START PUSHBUTTON press is requested; The subsequential blocks are executed whitout any operator request.
    /// </summary>
    /// <param name="connectionId">Connection Identifier returned by the FapiConnect() on success</param>
    /// <param name="nStatus">can take value 1 if batch mode is on, 0 if batch mode is off</param>
    /// <returns>FCL_RET_EXECUTED if the operation succeeds.
    /// FCL_RET_FAILED if the connection falls.
    /// FCL_RET_INVALID_LICENSE if the FapiCorbaLib has the READ_ONLY license level</returns>
    [DllImport("FapiCorbaLib.dll", EntryPoint = "FapiSetBatchMode")]
    internal static extern FapiError FapiSetBatchMode(int connectionId, int nStatus);
    
    /// <summary>
    /// Function for executing an .iso file in asynchronous mode
    /// Warning: The execution state and the session Id will be in the callback and can be FCL_RET_EXECUTED or FCL_RET_ABORTED
    /// </summary>
    /// <param name="connectionId">Connection Identifier returned by the FapiConnect() on success</param>
    /// <param name="pFilePath">pFilePath is the local path/directory in which the file is stored</param>
    /// <param name="pFileName">is the name of the .iso file to be executed; It can be also an absolute path</param>
    /// <returns>FCL_RET_MAX_SES_ID if the command has been @b ACCEPTED correctly and is started (Session Id).
    /// FCL_RET_FAILED if connection falls or pFileName is an empty string
    /// FCL_RET_REFUSED if the execution has been refused (e.g. file not found)
    /// FCL_RET_ABORTED if the execution has been refused by CNC (e.g. not in automatic mode)
    /// FCL_RET_INVALID_LICENSE if the FapiCorbaLib has the READ_ONLY license level</returns>
    [DllImport("FapiCorbaLib.dll", EntryPoint = "FapiExecAsyncFile", CharSet = CharSet.Ansi)]
    internal static extern FapiError FapiExecAsyncFile(int connectionId, string pFilePath, string pFileName);
    
    /// <summary>
    /// Function for executing an .iso file in Synchronous mode
    /// </summary>
    /// <param name="connectionId">Connection Identifier returned by the FapiConnect() on success</param>
    /// <param name="pFilePath">local path/directory in which the file is stored</param>
    /// <param name="pFileName">name of the .iso file to be executed. It can be also an absolute path</param>
    /// <returns>FCL_RET_EXECUTED if success
    /// FCL_RET_FAILED if connection falls or pFileName is an empty string
    /// FCL_RET_REFUSED if the execution has been refused (e.g. file not found)
    /// FCL_RET_ABORTED if the execution has been refused by CNC (e.g. not in automatic mode)
    /// FCL_RET_INVALID_LICENSE if the FapiCorbaLib has the READ_ONLY license level</returns>
    [DllImport("FapiCorbaLib.dll", EntryPoint = "FapiExecSyncFile", CharSet = CharSet.Ansi)]
    internal static extern FapiError FapiExecSyncFile(int connectionId, string pFilePath, string pFileName);
    
    /// <summary>
    /// Abort the Current iso file execution (if any).
    /// </summary>
    /// <param name="connectionId">Connection Identifier returned by the FapiConnect() on success</param>
    /// <returns>FCL_RET_EXECUTED if the file is actually aborted (or if no file is in execution)
    /// FCL_RET_FAILED if the connection falls.
    /// FCL_RET_INVALID_LICENSE if the FapiCorbaLib has the READ_ONLY license level</returns>
    [DllImport("FapiCorbaLib.dll", EntryPoint = "FapiAbortFileExe")]
    internal static extern FapiError FapiAbortFileExe(int connectionId);
    
    /// <summary>
    /// The function is used to execute a CNC command, for instance the RESET CNC
    /// </summary>
    /// <param name="connectionId">Connection Identifier returned by the FapiConnect() on success</param>
    /// <param name="pCommand">command to be sent (e.g. RESET)</param>
    /// <param name="pFirst">sub command (e.g. ALL, CNC, LNK, ETC...)</param>
    /// <param name="pSecond">not always necessary; it indicates the value of the sub command</param>
    /// <returns>FCL_RET_EXECUTED if the execution of the command was accepted and executed successfully
    /// FCL_RET_FAILED if the connection falls or command not available
    /// FCL_RET_INVALID_LICENSE if the FapiCorbaLib has the READ_ONLY license level</returns>
    [DllImport("FapiCorbaLib.dll", EntryPoint = "FapiExecCommand", CharSet = CharSet.Ansi)]
    internal static extern FapiError FapiExecCommand(int connectionId, string pCommand, string pFirst, string pSecond);
    
    /// <summary>
    /// Parameter Info introspection. Return the number of available CNC Parameters
    /// </summary>
    /// <param name="connectionId">Connection Identifier returned by the FapiConnect() on success</param>
    /// <returns>Return the number of CNC Parameters, -1 if the process failed</returns>
    [DllImport("FapiCorbaLib.dll", EntryPoint = "GetParameterNum")]
    internal static extern int GetParameterNum(int connectionId);
    
    /// <summary>
    /// Parameter Info introspection. Return the number of sub Parameter for a CNC parameter name
    /// </summary>
    /// <param name="connectionId">Connection Identifier returned by the FapiConnect() on success</param>
    /// <param name="pName">The Name of the parameter</param>
    /// <param name="pParNum">The subParameter size</param>
    /// <returns>FCL_RET_EXECUTED if successful
    /// FCL_RET_FAILED if parameters not found</returns>
    [DllImport("FapiCorbaLib.dll", EntryPoint = "GetSubParameterNum", CharSet = CharSet.Ansi)]
    internal static extern FapiError GetSubParameterNum(int connectionId, string pName, [Out] int pParNum);
    
    /// <summary>
    /// Parameter Info introspection. Used to have a complete list of all CNC parameters available.
    /// The vectors aParNames_out and aDescriptions_out must have the same size, and this size must be
    /// greater or equal than the value returned by GetParameterNum()
    /// </summary>
    /// <param name="connectionId">Connection Identifier returned by the FapiConnect() on success</param>
    /// <param name="aParNames_out">vector that has nEleSizes size. Each element is a CNC parameter name (it can be NULL).</param>
    /// <param name="aDescriptions_out">vector that has nEleSizes size. Each element is a CNC parameter
    /// description for the same index aParNames_out (it can be NULL).</param>
    /// <param name="aSubParNum_out">vector that has nEleSizes size. Each element is the number of sub
    /// parameter for each parameter (it can be NULL).</param>
    /// <param name="nEleSizes">number of vector's elements</param>
    /// <returns>FCL_RET_EXECUTED if the vector has been succesfully written
    /// FCL_RET_PAR_NOT_FOUND if nEleSizes smaller than the value returned by GetParameterNum()</returns>
    [DllImport("FapiCorbaLib.dll", EntryPoint = "GetParList", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
    internal static extern FapiError GetParList(int connectionId,
                                                [In, Out] StringName[] aParNames_out,
                                                [In, Out] StringDescription[] aDescriptions_out,
                                                [In, Out] Int32[] aSubParNum_out,
                                                int nEleSizes);
    
    /// <summary>
    /// Sub-Parameter Info introspection. Used to have a complete description for a Parameter: the list of all
    /// availables sub-parameters, with type name, a list of all allowed values (in case of discrete parameters), the
    /// parameter description, and the size in bytes. The vector must have a size greater than the value returned by
    /// GetSubParameterNum(); all the vectors must have the same size equal to nEleSizes.
    /// </summary>
    /// <param name="connectionId">Connection Identifier returned by the FapiConnect() on success</param>
    /// <param name="pParName">the parameter name</param>
    /// <param name="aSubNames_out">a vector that has nEleSizes size (it can be NULL)</param>
    /// <param name="aTypeNames_out">a vector that has nEleSizes size (it can be NULL)</param>
    /// <param name="aTypeValues_out">a vector that has nEleSizes size (it can be NULL)</param>
    /// <param name="aDescriptions_out">a vector that has nEleSizes size (it can be NULL)</param>
    /// <param name="aSizes_out">a vector that has nEleSizes size (it can be NULL)</param>
    /// <param name="nEleSizes">the number of Vector elements</param>
    /// <returns>FCL_RET_EXECUTED if successful
    /// FCL_RET_SUBPAR_NOT_FOUND if nEleSizes is smaller than the value returned by GetSubParameterNum() or there are connection problems</returns>
    [DllImport("FapiCorbaLib.dll", EntryPoint = "GetSubParameterInfo", CharSet = CharSet.Ansi)]
    internal static extern FapiError GetSubParameterInfo(int connectionId, string pParName,
                                                         [In, Out] StringName[] aSubNames_out,
                                                         [In, Out] StringName[] aTypeNames_out,
                                                         [In, Out] StringValue[] aTypeValues_out,
                                                         [In, Out] StringDescription[] aDescriptions_out,
                                                         [In, Out] Int32[] aSizes_out,
                                                         int nEleSizes);
    
    /// <summary>
    /// Read a single CNC Parameter (with sub-parameter s).
    /// To retrieve the parameters' name, it is possible to use the GetParList();
    /// to retrieve the sub parameters' names is possible to use GetSubParameterInfo().
    /// </summary>
    /// <param name="connectionId">Connection Identifier returned by the FapiConnect() on success</param>
    /// <param name="pParName">the parameter name</param>
    /// <param name="pSubName">the subParameter Name (if any)</param>
    /// <param name="pValue_out">value of the parameter</param>
    /// <param name="nValueSize">the real size of pValue_out vector</param>
    /// <returns>FCL_RET_EXECUTED if the function successfully read the parameter is read
    /// FCL_RET_ABORTED if the read is failed (wrong subParameter name or communication problem)
    /// FCL_RET_TIMEOUT in case of operation timeout
    /// FCL_RET_FAILED if there are connection problems</returns>
    [DllImport("FapiCorbaLib.dll", EntryPoint = "FapiReadParameter", CharSet = CharSet.Ansi)]
    internal static extern FapiError FapiReadParameter(int connectionId, string pParName, string pSubName,
                                                       StringBuilder pValue_out, int nValueSize);
    
    /// <summary>
    /// Read CNC Parameter with more than one subParameter at the same time. The vectors aSubParNames and
    /// aValues_out must have the same size; To retrieve the parameter names is possible to use the GetParList();
    /// to retrieve the sub parameter names is possible to use GetSubParameterInfo()
    /// </summary>
    /// <param name="connectionId">Connection Identifier returned by the FapiConnect() on success</param>
    /// <param name="pParName">the parameter name</param>
    /// <param name="aSubParNames">a char vector that has nEleNames size and the sub parameters names must be formatted (Example "XM")</param>
    /// <param name="nEleNames">number of aSubParNames elements (between 1 and GetSubParameterNum); Must be equal to nEleValues;</param>
    /// <param name="aValues_out">a char vector that has nEleValues size and on success, contains the returned values</param>
    /// <param name="nEleValues">the number of @c aValues_out elements (between 1 and GetSubParameterNum); Must be equal to nEleNames;</param>
    /// <returns>FCL_RET_EXECUTED if the function successfully read the parameter is read
    /// FCL_RET_ABORTED if the read is failed (wrong subParameter name or communication problem)
    /// FCL_RET_TIMEOUT in case of operation timeout
    /// FCL_RET_FAILED if there are connection problems</returns>
    [DllImport("FapiCorbaLib.dll", EntryPoint = "FapiReadParameters", CharSet = CharSet.Ansi)]
    internal static extern FapiError FapiReadParameters(int connectionId, string pParName,
                                                        [In] StringName[] aSubParNames, int nEleNames,
                                                        [In, Out] StringValue[] aValues_out, int nEleValues);
    
    /// <summary>
    /// Write a CNC Parameter
    /// </summary>
    /// <param name="connectionId">Connection Identifier returned by the FapiConnect() on success</param>
    /// <param name="pParName">the parameter name (e.g. "POSITION")</param>
    /// <param name="pSubParName">the sub Parameter name (e.g. "XM")</param>
    /// <param name="pValue">the value as to be written (e.g. "123.456")</param>
    /// <returns>FCL_RET_EXECUTED if the function was successful and the writing executed
    /// FCL_RET_TIMEOUT in case of operation timeout
    /// FCL_RET_PAR_NOT_FOUND if parameter does not exist
    /// FCL_RET_SUBPAR_NOT_FOUND if parameter size is 0
    /// FCL_RET_FAILED if there are connection problems
    /// FCL_RET_INVALID_LICENSE if the FapiCorbaLib has the READ_ONLY license level</returns>
    [DllImport("FapiCorbaLib.dll", EntryPoint = "FapiWriteParameter", CharSet = CharSet.Ansi)]
    internal static extern FapiError FapiWriteParameter(int connectionId, string pParName, string pSubParName, string pValue);
    
    /// <summary>
    /// User data going through the listener
    /// (the content can change according to our needs)
    /// </summary>
    internal struct UserData {}
    
    /// <summary>
    /// Pointer to a callback function used when a new CNC message is generated.
    /// MsgListenerCallBack is a pointer to a function that takes one char * and a void * as input.
    /// The first char * is the message generated by CNC, the second is a general purpose void *.
    /// Example: void OnMessage(const char pMessage, void *pUserData)
    /// </summary>
    public delegate void MsgListenerCallBack(string code, UserData userData);
    
    /// <summary>
    /// This function is used to register a listener for the CNC messages.
    /// </summary>
    /// <param name="connectionId">Connection Identifier returned by the FapiConnect() on success</param>
    /// <param name="callBack">Function of type MsgListenerCallBack</param>
    /// <param name="userData">General purpose pointer; when a new messagge happens the callback is called with the same pointed data</param>
    /// <returns>1 or more if the function succesfully added a new listener and returns the number of currently active listeners
    /// below 0 if the function failed in adding the new listener</returns>
    [DllImport("FapiCorbaLib.dll", EntryPoint = "AddMessageListener", CharSet = CharSet.Ansi)]
    internal static extern int AddMessageListener(int connectionId, MsgListenerCallBack callBack, UserData userData);
    
    /// <summary>
    /// This function is used to de-register a listener for the CNC messages.
    /// </summary>
    /// <param name="connectionId">Connection Identifier returned by the FapiConnect() on success</param>
    /// <param name="callBack">Function to de-register; if NULL, all message listeners are de-registered</param>
    /// <returns>number of message listeners still active</returns>
    [DllImport("FapiCorbaLib.dll", EntryPoint = "RemoveMessageListener", CharSet = CharSet.Ansi)]
    internal static extern int RemoveMessageListener(int connectionId, MsgListenerCallBack callBack);
  }
  #endregion // Imported methods from Import.FwLib32
}
