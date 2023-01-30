// Copyright (C) 2009-2023 Lemoine Automation Technologies
//
// SPDX-License-Identifier: GPL-2.0-or-later

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// This sets the default COM visibility of types in the assembly to invisible.
// If you need to expose a type to COM, use [ComVisible(true)] on that type.
[assembly: ComVisible(false)]

[assembly:InternalsVisibleTo("Lemoine.Cnc.DataQueue.UnitTests, PublicKey=002400000480000094000000060200000024000052534131000400000100010015234DBBA0DA3F2DAF34F84658E780324E556C05A73040756D76F5195F025756000F2FBA9747BC1AAC7D90666DB8DF32FE003B41F4453FBC33F955281FEA1FB6598CEB59E768047104CD0F78DCFFEE72560914FB2AA63597F81486396A04CA66C6A9D8BCE924F4109C9FC023601B0579CC0C8B998707131624F862BB0A533CAC")]