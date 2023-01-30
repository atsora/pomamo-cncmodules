// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Net;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Web client with a timeout
  /// https://stackoverflow.com/questions/601861/set-timeout-for-webclient-downloadfile
  /// </summary>
  public sealed class TimeOutWebRequest
    : System.Net.WebClient
  {
    readonly int m_timeout;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="timeout"></param>
    public TimeOutWebRequest (int timeout)
    {
      m_timeout = timeout;
    }

    /// <summary>
    /// Override
    /// </summary>
    /// <param name="address"></param>
    /// <returns></returns>
    protected override WebRequest GetWebRequest (Uri address)
    {
      var request = base.GetWebRequest (address);
      if (request != null) {
        request.Timeout = m_timeout;
      }
      return request;
    }
  }
}
