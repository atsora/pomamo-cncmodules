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

#include "adapter.hpp"
#include "device_datum.hpp"

using namespace System;
using namespace System::Collections;
using namespace Lemoine::Core::Log;

namespace Lemoine
{
  namespace Cnc
  {
    /// <summary>
    /// Managed MTConnect adapter for PULSE CNC V2
    /// </summary>
    public ref class PulseAdapter : public Lemoine::Cnc::ICncModule, public Adapter
    {
    private: // Constants

    private: // Members
      int cncAcquisitionId;
      String^ cncAcquisitionName;
      Lemoine::Threading::IChecked^ m_dataHandler;

      ILog^ log;

      Availability *availability;
      Execution *execution;
      ControllerMode *mode;
      Event *programName;
      Sample *x;
      Sample *y;
      Sample *z;
      Sample *u;
      Sample *v;
      Sample *w;
      Sample *a;
      Sample *b;
      Sample *c;
      Sample *feedrate;
      Sample *spindleSpeed;
      Sample *feedrateOverride;
      Sample *spindleSpeedOverride;
      IntEvent* cncPartCount;
      Event* toolNumber;

    public: // Getters / Setters
      /// <summary>
      /// Cnc Acquisition Id
      /// </summary>
      property int CncAcquisitionId
      {
        virtual int get () { return cncAcquisitionId; }
        virtual void set (int value)
        {
          cncAcquisitionId = value;
          log = LogManager::GetLogger (String::Format ("{0}.{1}",
            PulseAdapter::typeid->FullName,
            value));
        }
      }

      /// <summary>
      /// Cnc Acquisition name
      /// </summary>
      property String^ CncAcquisitionName
      {
        virtual String^ get () { return cncAcquisitionName; }
        virtual void set (String^ value) { cncAcquisitionName = value; }
      }

      /// <summary>
      /// Is the control available ? Could PULSE connect to the control ?
      ///
      /// Event name: avail
      /// </summary>
      property bool Available
      {
        void set (bool value);
      }

      /// <summary>
      /// Is the data in error ?
      ///
      /// In case of error, all the data are set unavailable
      /// </summary>
      property bool Error
      {
        void set (bool value);
      }

      /// <summary>
      /// Error code
      ///
      /// Because for the moment, there is no real error code,
      /// please use rather the Error property
      /// </summary>
      property long ErrorCode
      {
        void set (long value);
      }

      /// <summary>
      /// Position (set only X, Y and Z)
      ///
      /// Sample names: 
      /// </summary>
      property Lemoine::Cnc::Position PositionXYZ
      {
        void set (Lemoine::Cnc::Position value);
      }

      /// <summary>
      /// Position (set all the positions: X, Y, Z, U, V, W, A, B, C)
      ///
      /// Sample names: 
      /// </summary>
      property Lemoine::Cnc::Position Position
      {
        void set (Lemoine::Cnc::Position value);
      }

      /// <summary>
      /// X position
      ///
      /// Sample name: Xact
      /// </summary>
      property double X
      {
        void set (double value);
      }

      /// <summary>
      /// Y position
      ///
      /// Sample name: Yact
      /// </summary>
      property double Y
      {
        void set (double value);
      }

      /// <summary>
      /// Z position
      ///
      /// Sample name: Zact
      /// </summary>
      property double Z
      {
        void set (double value);
      }

      /// <summary>
      /// U position
      ///
      /// Sample name: Uact
      /// </summary>
      property double U
      {
        void set (double value);
      }

      /// <summary>
      /// V position
      ///
      /// Sample name: Vact
      /// </summary>
      property double V
      {
        void set (double value);
      }

      /// <summary>
      /// W position
      ///
      /// Sample name: Wact
      /// </summary>
      property double W
      {
        void set (double value);
      }

      /// <summary>
      /// A position
      ///
      /// Sample name: Apos
      /// </summary>
      property double A
      {
        void set (double value);
      }

      /// <summary>
      /// B position
      ///
      /// Sample name: Bpos
      /// </summary>
      property double B
      {
        void set (double value);
      }

      /// <summary>
      /// C position
      ///
      /// Sample name: Cpos
      /// </summary>
      property double C
      {
        void set (double value);
      }

      /// <summary>
      /// Feedrate
      ///
      /// Sample name: path_feedrate
      /// </summary>
      property double Feedrate
      {
        void set (double value);
      }

      /// <summary>
      /// Spindle speed
      ///
      /// Sample name: spindle_speed
      /// </summary>
      property double SpindleSpeed
      {
        void set (double value);
      }

      /// <summary>
      /// Auto status
      ///
      /// Event name: mode
      /// </summary>
      property bool Auto
      {
        void set(bool value);
      }

      /// <summary>
      /// MDI status
      ///
      /// Event name: mode
      /// </summary>
      property bool MDI
      {
        void set(bool value);
      }

      /// <summary>
      /// Jog status
      ///
      /// Event name: mode
      /// </summary>
      property bool Jog
      {
        void set(bool value);
      }

      /// <summary>
      /// ManualAny status
      ///
      /// Event name: mode
      /// </summary>
      property bool ManualAny
      {
        void set(bool value);
      }

      /// <summary>
      /// Manual status
      ///
      /// Event name: mode
      /// </summary>
      property bool Manual
      {
        void set (bool value);
      }

      /// <summary>
      /// Feedrate override
      ///
      /// Sample name: feed_ovr
      /// </summary>
      property long FeedrateOverride
      {
        void set (long value);
      }

      /// <summary>
      /// Spindle speed override
      ///
      /// Sample name: SSpeedOvr
      /// </summary>
      property long SpindleSpeedOverride
      {
        void set (long value);
      }

      /// <summary>
      /// Running status
      ///
      /// Event name: execution
      /// </summary>
      property bool Running
      {
        void set (bool value);
      }

      /// <summary>
      /// Program name
      ///
      /// Event name: program
      /// </summary>
      property String^ ProgramName
      {
        void set (String^ value);
      }

      /// <summary>
      /// Part count
      /// 
      /// Event name: 
      /// </summary>
      property int CncPartCount
      {
        void set (int value);
      }

      /// <summary>
      /// Tool number
      /// 
      /// Event name:
      /// </summary>
      property String^ ToolNumber
      {
        void set (String^ value);
      }

    public: // Constructors / Destructor / ToString methods
      /// <summary>
      /// Constructor
      /// </summary>
      PulseAdapter ();
      /// <summary>
      /// Destructor: cleans up all resources
      /// </summary>
      virtual ~PulseAdapter () { this->!PulseAdapter (); }
      /// <summary>
      /// Finalizer: cleans up unmanaged resources
      /// </summary>
      !PulseAdapter ();
      /// <summary>
      /// <see cref="Object.ToString" />
      /// </summary>
      /// <returns></returns>
      virtual String^ ToString () override;      
      /// <summary>
      /// Implements <see cref="ICncModule" />
      /// </summary>
      /// <param name="dataHandler"></param>
      virtual void SetDataHandler (Lemoine::Threading::IChecked^ dataHandler)
      {
        m_dataHandler = dataHandler;
      }
      /// <summary>
      /// Implements <see cref="Lemoine::Threading::IChecked" />
      /// </summary>
      virtual void SetActive ()
      {
        if (nullptr != m_dataHandler) {
          m_dataHandler->SetActive();
        }
      }
      /// <summary>
      /// Implements <see cref="Lemoine::Threading::IChecked" />
      /// </summary>
      virtual void PauseCheck ()
      {
        if (nullptr != m_dataHandler) {
          m_dataHandler->PauseCheck();
        }
      }
      /// <summary>
      /// Implements <see cref="Lemoine::Threading::IChecked" />
      /// </summary>
      virtual void ResumeCheck ()
      {
        if (nullptr != m_dataHandler) {
          m_dataHandler->ResumeCheck();
        }
      }

    public: // Public methods

    private: // Private methods
    };
  }
}
