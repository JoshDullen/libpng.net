// png_time.cs - tIME chunk struct
//
// Based on libpng version 1.4.3 - June 26, 2010
// This code is released under the libpng license.
// For conditions of distribution and use, see copyright notice in License.txt
// Copyright (C) 2007-2010 by the Authors
// Copyright (c) 1998-2010 Glenn Randers-Pehrson
// (Version 0.96 Copyright (c) 1996, 1997 Andreas Dilger)
// (Version 0.88 Copyright (c) 1995, 1996 Guy Eric Schalnat, Group 42, Inc.)

using System;
using System.Collections.Generic;
using System.Text;

namespace Free.Ports.libpng
{
	// png_time is a way to hold the time in an machine independent way.
	// Two conversions are provided, both from time_t and struct tm. There
	// is no portable way to convert to either of these structures, as far
	// as I know. If you know of a portable way, send it to me.
	public struct png_time
	{
		public ushort year;	// full year, as in, 1995
		public byte month;	// month of year, 1 - 12
		public byte day;	// day of month, 1 - 31
		public byte hour;	// hour of day, 0 - 23
		public byte minute;	// minute of hour, 0 - 59
		public byte second;	// second of minute, 0 - 60 (for leap seconds)
	}
}
