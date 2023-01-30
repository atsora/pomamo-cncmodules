/*
* Copyright (c) 2008, AMT – The Association For Manufacturing Technology (“AMT”)
* 2009-2023 Lemoine Automation Technologies
* All rights reserved.
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * Neither the name of the AMT nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* DISCLAIMER OF WARRANTY. ALL MTCONNECT MATERIALS AND SPECIFICATIONS PROVIDED
* BY AMT, MTCONNECT OR ANY PARTICIPANT TO YOU OR ANY PARTY ARE PROVIDED "AS IS"
* AND WITHOUT ANY WARRANTY OF ANY KIND. AMT, MTCONNECT, AND EACH OF THEIR
* RESPECTIVE MEMBERS, OFFICERS, DIRECTORS, AFFILIATES, SPONSORS, AND AGENTS
* (COLLECTIVELY, THE "AMT PARTIES") AND PARTICIPANTS MAKE NO REPRESENTATION OR
* WARRANTY OF ANY KIND WHATSOEVER RELATING TO THESE MATERIALS, INCLUDING, WITHOUT
* LIMITATION, ANY EXPRESS OR IMPLIED WARRANTY OF NONINFRINGEMENT,
* MERCHANTABILITY, OR FITNESS FOR A PARTICULAR PURPOSE. 

* LIMITATION OF LIABILITY. IN NO EVENT SHALL AMT, MTCONNECT, ANY OTHER AMT
* PARTY, OR ANY PARTICIPANT BE LIABLE FOR THE COST OF PROCURING SUBSTITUTE GOODS
* OR SERVICES, LOST PROFITS, LOSS OF USE, LOSS OF DATA OR ANY INCIDENTAL,
* CONSEQUENTIAL, INDIRECT, SPECIAL OR PUNITIVE DAMAGES OR OTHER DIRECT DAMAGES,
* WHETHER UNDER CONTRACT, TORT, WARRANTY OR OTHERWISE, ARISING IN ANY WAY OUT OF
* THIS AGREEMENT, USE OR INABILITY TO USE MTCONNECT MATERIALS, WHETHER OR NOT
* SUCH PARTY HAD ADVANCE NOTICE OF THE POSSIBILITY OF SUCH DAMAGES.
*/
#pragma once

#include <Windows.h>

#include "server.hpp"
#include "string_buffer.hpp"

using namespace System;
using namespace Lemoine::Core::Log;

class DeviceDatum;

namespace Lemoine
{
  namespace Cnc
  {
    /*
    * Abstract adapter that manages all the data values and writing them
    * to the clients.
    *
    * Subclasses of this class will add the data values and interact with the
    * vendor specifc API. This class provides all the common functionality.
    */
    public ref class Adapter abstract
    {
    public:
      /// <summary>
      /// Port number of the Adapter (default: 7878)
      /// </summary>
      property int Port
      {
        int get () { return mPort; }
        void set (int value) { mPort = value; }
      }

    private: // Members
      ILog^ log;

    protected:
      Server *mServer;         /* The socket server */
      StringBuffer *mBuffer;    /* A string buffer to hold the string we write to the streams */
      array <DeviceDatum*>^ mDeviceData;/* A 0 terminated array of data value objects */
      int mNumDeviceData;     /* The number of data values */
      int mPort;              /* The server port we bind to */
      bool mDisableFlush;     /* Used for initial data collection */
      int mHeartbeatFrequency; /* The frequency (ms) to heartbeat
                               * server. Responds to Ping. Default 10 sec */

    protected:
      void addDatum(DeviceDatum &aValue);

      /* Internal buffer sending methods */
      void sendBuffer();
      void sendDatum(DeviceDatum *aValue);
      virtual void sendInitialData(Client *aClient);
      virtual void sendChangedData();
      virtual void flush();
      virtual void unavailable();

    public:
      Adapter();
      ~Adapter();

      /// <summary>
      /// Start method: making everything ready to get some data
      /// </summary>
      void Start ();

      /// <summary>
      /// Finish method: once the data has been gathered, send them
      /// </summary>
      void Finish ();

      /* Overload this method to handle situation when all clients disconnect */
      virtual void clientsDisconnected();
    };

  }
}
