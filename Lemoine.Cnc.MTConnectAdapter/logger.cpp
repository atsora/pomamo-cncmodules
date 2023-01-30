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
#include "logger.hpp"

Logger *gLogger = NULL;

const char *Logger::format(char *aBuffer, int aLen, const char *aFormat, va_list args)
{
  vsprintf(aBuffer, aFormat, args);
  aBuffer[aLen - 1] = '\0';
  return aBuffer;
}

const char *Logger::timestamp(char *aBuffer)
{
#ifdef WIN32
  SYSTEMTIME st;
  GetSystemTime(&st);
  sprintf(aBuffer, "%4d-%02d-%02dT%02d:%02d:%02d.%04dZ", st.wYear, st.wMonth, st.wDay, st.wHour, 
          st.wMinute, st.wSecond, st.wMilliseconds);
#else
  struct timeval tv;
  struct timezone tz;

  gettimeofday(&tv, &tz);

  strftime(aBuffer, 64, "%Y-%m-%dT%H:%M:%S", gmtime(&tv.tv_sec));
  sprintf(aBuffer + strlen(aBuffer), ".%06dZ", tv.tv_usec);
#endif
  
  return aBuffer;
}

#pragma unmanaged // Following code explicitely not managed: avoid C4793 warnings
void Logger::error(const char *aFormat, ...)
{
  char buffer[LOGGER_BUFFER_SIZE];
  char ts[32];
  va_list args;
  va_start(args, aFormat);
  fprintf(stderr, "%s - Error: %s\n", timestamp(ts), format(buffer, LOGGER_BUFFER_SIZE, aFormat, args));
  va_end(args);
}

void Logger::warning(const char *aFormat, ...)
{
  if (mLogLevel > eWARNING) return;

  char buffer[LOGGER_BUFFER_SIZE];
  char ts[32];
  va_list args;
  va_start(args, aFormat);
  fprintf(stderr, "%s - Warning: %s\n", timestamp(ts), format(buffer, LOGGER_BUFFER_SIZE, aFormat, args));
  va_end(args);
}

void Logger::info(const char *aFormat, ...)
{
  if (mLogLevel > eINFO) return;

  char buffer[LOGGER_BUFFER_SIZE];
  char ts[32];
  va_list args;
  va_start(args, aFormat);
  fprintf(stderr, "%s - Info: %s\n", timestamp(ts), format(buffer, LOGGER_BUFFER_SIZE, aFormat, args));
  va_end(args);
}

void Logger::debug(const char *aFormat, ...)
{
  if (mLogLevel > eDEBUG) return;

  char buffer[LOGGER_BUFFER_SIZE];
  char ts[32];
  va_list args;
  va_start(args, aFormat);
  fprintf(stderr, "%s - Debug: %s\n", timestamp(ts), format(buffer, LOGGER_BUFFER_SIZE, aFormat, args));
  va_end(args);
}
#pragma managed // End of the unmanaged section