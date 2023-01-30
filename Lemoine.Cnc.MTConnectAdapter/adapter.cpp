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

#include "internal.hpp"
#include "adapter.hpp"
#include "device_datum.hpp"
#include "logger.hpp"

namespace Lemoine
{
  namespace Cnc
  {
    Adapter::Adapter()
      : mNumDeviceData(0)
      , mBuffer (new StringBuffer ())
    {
      mServer = 0;
      mPort = 7878;
      mHeartbeatFrequency = 10000;
      mDeviceData = gcnew array <DeviceDatum*> (128);
      log = LogManager::GetLogger (String::Format ("{0}",
        Adapter::typeid->FullName));
    }

    Adapter::~Adapter()
    {
      if (mServer) {
        delete mServer;
      }
      delete mBuffer;
    }

    /* Add a data value to the list of data values */
    void Adapter::addDatum(DeviceDatum &aValue)
    {
      mDeviceData[mNumDeviceData++] = &aValue;
      mDeviceData[mNumDeviceData] = 0;
    }

    void Adapter::Start ()
    {
      if (gLogger == NULL) {
        gLogger = new Logger();
      }

      if (mServer == NULL) {
        mServer = new Server(mPort, mHeartbeatFrequency);
      }

      /* Check if we have any new clients */
      Client **clients = mServer->connectToClients();
      bool hasClients = false;
      if (clients != 0) {
        hasClients = true;
        for (int i = 0; clients[i] != 0; i++) {
          /* If there are any new clients, send them the initial values for all the 
          * data values */
          sendInitialData(clients[i]);
        }
      }

      /* Read and all data from the clients */
      mServer->readFromClients();

      /* Don't bother getting data if we don't have anyone to read it */
      if (mServer->numClients() > 0) {
        mBuffer->timestamp();
      }
      else if (hasClients) {
        hasClients = false;
        clientsDisconnected();
      }
    }

    void Adapter::Finish ()
    {
      if (mServer->numClients() > 0) {
        sendChangedData();
        mBuffer->reset();
      }
    }

    /* Send a single value to the buffer. */
    void Adapter::sendDatum(DeviceDatum *aValue)
    {
      if (aValue->requiresFlush())
        sendBuffer();
      aValue->append(*mBuffer);
      if (aValue->requiresFlush())
        sendBuffer();
    }

    /* Send the buffer to the clients. Only sends if there is something in the buffer. */
    void Adapter::sendBuffer()
    {
      if (mServer != 0 && mBuffer->length() > 0)
      {
        mBuffer->append("\n");
        mServer->sendToClients(*mBuffer);
        mBuffer->reset();  
      }
    }

    /* Send the initial values to a client */
    void Adapter::sendInitialData(Client *aClient)
    {
      log->Debug ("sendInitialiData /B");
      mDisableFlush = true;
      mBuffer->timestamp();

      for (int i = 0; i < mNumDeviceData; i++) {
        DeviceDatum *value = mDeviceData[i];
        if (value->hasInitialValue())
          sendDatum(value);
      }
      sendBuffer();
      mDisableFlush = false;
    }

    /* Send the values that have changed to the clients */
    void Adapter::sendChangedData()
    {
      for (int i = 0; i < mNumDeviceData; i++)
      {
        DeviceDatum *value = mDeviceData[i];
        if (value->changed())
          sendDatum(value);
      }  
      sendBuffer();
    }

    void Adapter::flush()
    {
      if (!mDisableFlush)
      {
        sendChangedData();
        mBuffer->reset();
        mBuffer->timestamp();
      }
    }

    void Adapter::clientsDisconnected()
    {
      /* Do nothing for now ... */
      printf("All clients have disconnected\n");
    }

    void Adapter::unavailable()
    {
      for (int i = 0; i < mNumDeviceData; i++)
      {
        DeviceDatum *value = mDeviceData[i];
        value->unavailable();
      }
      flush();
    }
  }
}
