// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Collections.Generic;
using Lemoine.Model;

namespace Lemoine.Cnc
{
  /// <summary>
  /// Description of AntiDuplicate.
  /// </summary>
  public class AntiDuplicate
  {
    #region Members
    IDictionary<string, IList<EventToolLifeType>> m_previousEvents;
    IDictionary<string, IList<EventToolLifeType>> m_currentEvents;
    #endregion // Members

    #region Methods
    /// <summary>
    /// Return true if the event can be stored in the database
    /// </summary>
    /// <param name="toolId"></param>
    /// <param name="eventType"></param>
    /// <returns></returns>
    public bool IsAllowed (string toolId, EventToolLifeType eventType)
    {
      // The event must not be triggered in the previous acquisition
      bool isAllowed = m_previousEvents == null ||
        !m_previousEvents.ContainsKey (toolId) ||
        !m_previousEvents[toolId].Contains (eventType);

      // Store the event
      if (m_currentEvents == null) {
        m_currentEvents = new Dictionary<string, IList<EventToolLifeType>> ();
      }

      if (!m_currentEvents.ContainsKey (toolId)) {
        m_currentEvents[toolId] = new List<EventToolLifeType> ();
      }

      m_currentEvents[toolId].Add (eventType);

      return isAllowed;
    }

    /// <summary>
    /// End of the acquisition
    /// </summary>
    public void Finish ()
    {
      m_previousEvents = m_currentEvents;
      m_currentEvents = null;
    }
    #endregion // Methods
  }
}
