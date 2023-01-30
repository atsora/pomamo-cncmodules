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

#include "PulseAdapter.h"
#include "StringConversion.h"

namespace Lemoine
{
  namespace Cnc
  {
    PulseAdapter::PulseAdapter ()
      : availability (NULL)
      , execution (NULL)
      , mode (NULL)
      , programName (NULL)
      , x (NULL)
      , y (NULL)
      , z (NULL)
      , u (NULL)
      , v (NULL)
      , w (NULL)
      , a (NULL)
      , b (NULL)
      , c (NULL)
      , feedrate (NULL)
      , spindleLoad (NULL)
      , spindleSpeed (NULL)
      , feedrateOverride (NULL)
      , spindleSpeedOverride (NULL)
    {
      log = LogManager::GetLogger (String::Format ("{0}.{1}",
        PulseAdapter::typeid->FullName,
        this->cncAcquisitionId));
    }

    PulseAdapter::!PulseAdapter ()
    {
      if (NULL != availability) {
        delete availability;
      }
      if (NULL != execution) {
        delete execution;
      }
      if (NULL != mode) {
        delete mode;
      }
      if (NULL != programName) {
        delete programName;
      }
      if (NULL != x) {
        delete x;
      }
      if (NULL != y) {
        delete y;
      }
      if (NULL != z) {
        delete z;
      }
      if (NULL != u) {
        delete u;
      }
      if (NULL != v) {
        delete v;
      }
      if (NULL != w) {
        delete w;
      }
      if (NULL != a) {
        delete a;
      }
      if (NULL != b) {
        delete b;
      }
      if (NULL != c) {
        delete c;
      }
      if (NULL != feedrate) {
        delete feedrate;
      }
      if (NULL != spindleLoad) {
        delete spindleLoad;
      }
      if (NULL != spindleSpeed) {
        delete spindleSpeed;
      }
      if (NULL != feedrateOverride) {
        delete feedrateOverride;
      }
      if (NULL != spindleSpeedOverride) {
        delete spindleSpeedOverride;
      }
    }

    String^ PulseAdapter::ToString ()
    {
      return String::Format ("CNC module {0}.{1} [{2}]",
        this->GetType ()->FullName,
        this->CncAcquisitionId,
        this->CncAcquisitionName);
    }

    void PulseAdapter::Available::set (bool value)
    {
      if (NULL == availability) {
        availability = new Availability ("avail");
        addDatum (*availability);
      }
      if (true == value) {
        availability->available ();
      }
      else {
        availability->unavailable ();
      }
    }

    void PulseAdapter::ErrorCode::set (long value)
    {
      // Note: for the moment, there is only one error code UNKNOWN
      //       => do not process the error code
      if (0 == value) { // 0: CNC_NO_ERROR
        this->Error = false;
      }
      else {
        this->Error = true;
      }
    }

    void PulseAdapter::Error::set (bool value)
    {
      if (true == value) {
        this->unavailable ();
      }
    }

    void PulseAdapter::PositionXYZ::set (Lemoine::Cnc::Position value)
    {
      X = value.X;
      Y = value.Y;
      Z = value.Z;
    }

    void PulseAdapter::Position::set (Lemoine::Cnc::Position value)
    {
      X = value.X;
      Y = value.Y;
      Z = value.Z;
      U = value.U;
      V = value.V;
      W = value.W;
      A = value.A;
      B = value.B;
      C = value.C;
    }

    void PulseAdapter::X::set (double value)
    {
      if (NULL == x) {
        x = new Sample ("Xact");
        addDatum (*x);
      }
      x->setValue (value);
      Available = true;
    }

    void PulseAdapter::Y::set (double value)
    {
      if (NULL == y) {
        y = new Sample ("Yact");
        addDatum (*y);
      }
      y->setValue (value);
      Available = true;
    }

    void PulseAdapter::Z::set (double value)
    {
      if (NULL == z) {
        z = new Sample ("Zact");
        addDatum (*z);
      }
      z->setValue (value);
      Available = true;
    }

    void PulseAdapter::U::set (double value)
    {
      if (NULL == u) {
        u = new Sample ("Uact");
        addDatum (*u);
      }
      u->setValue (value);
      Available = true;
    }

    void PulseAdapter::V::set (double value)
    {
      if (NULL == v) {
        v = new Sample ("Vact");
        addDatum (*v);
      }
      v->setValue (value);
      Available = true;
    }

    void PulseAdapter::W::set (double value)
    {
      if (NULL == w) {
        w = new Sample ("Wact");
        addDatum (*w);
      }
      w->setValue (value);
      Available = true;
    }

    void PulseAdapter::A::set (double value)
    {
      if (NULL == a) {
        a = new Sample ("Apos");
        addDatum (*a);
      }
      a->setValue (value);
      Available = true;
    }

    void PulseAdapter::B::set (double value)
    {
      if (NULL == b) {
        b = new Sample ("Bpos");
        addDatum (*b);
      }
      b->setValue (value);
      Available = true;
    }

    void PulseAdapter::C::set (double value)
    {
      if (NULL == c) {
        c = new Sample ("Cpos");
        addDatum (*c);
      }
      c->setValue (value);
      Available = true;
    }

    void PulseAdapter::Feedrate::set (double value)
    {
      if (NULL == feedrate) {
        feedrate = new Sample ("path_feedrate");
        addDatum (*feedrate);
      }
      feedrate->setValue (value);
      Available = true;
    }

    void PulseAdapter::SpindleLoad::set (double value)
    {
      if (NULL == spindleLoad) {
        spindleLoad = new Sample ("spindle_load");
        addDatum (*spindleLoad);
      }
      spindleLoad->setValue (value);
      Available = true;
    }

    void PulseAdapter::SpindleSpeed::set (double value)
    {
      if (NULL == spindleSpeed) {
        spindleSpeed = new Sample ("spindle_speed");
        addDatum (*spindleSpeed);
      }
      spindleSpeed->setValue (value);
      Available = true;
    }

    void PulseAdapter::Manual::set (bool value)
    {
      if (NULL == mode) {
        mode = new ControllerMode ("mode");
        addDatum (*mode);
      }
      if (true == value) {
        mode->setValue (ControllerMode::eMANUAL);
      }
      else {
        mode->setValue (ControllerMode::eAUTOMATIC);
      }
      Available = true;
    }

    void PulseAdapter::FeedrateOverride::set (long value)
    {
      if (NULL == feedrateOverride) {
        feedrateOverride = new Sample ("feed_ovr");
        addDatum (*feedrateOverride);
      }
      feedrateOverride->setValue (value);
      Available = true;
    }

    void PulseAdapter::SpindleSpeedOverride::set (long value)
    {
      if (NULL == spindleSpeedOverride) {
        spindleSpeedOverride = new Sample ("SspeedOvr");
        addDatum (*spindleSpeedOverride);
      }
      spindleSpeedOverride->setValue (value);
      Available = true;
    }

    void PulseAdapter::Running::set (bool value)
    {
      if (NULL == execution) {
        execution = new Execution ("execution");
        addDatum (*execution);
      }
      if (true == value) {
        execution->setValue (Execution::eACTIVE);
      }
      else {
        execution->setValue (Execution::eINTERRUPTED);
      }
      Available = true;
    }

    void PulseAdapter::ProgramName::set (String^ value)
    {
      if (NULL == programName) {
        programName = new Event ("program");
        addDatum (*programName);
      }

      programName->setValue (Lemoine::Conversion::ConvertToStdString (value).c_str ());
      Available = true;
    }
  }
}
