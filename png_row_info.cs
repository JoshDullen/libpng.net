// png_row_info.cs - png_row_info struct
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
	// This is used for the transformation routines, as some of them
	// change these values for the row. It also should enable using
	// the routines for other purposes.
	public struct png_row_info
	{
		public uint width;					// width of row
		public uint rowbytes;				// number of bytes in row
		public PNG_COLOR_TYPE color_type;	// color type of row
		public byte bit_depth;				// bit depth of row
		public byte channels;				// number of channels (1, 2, 3, or 4)
		public byte pixel_depth;			// bits per pixel (depth * channels)
	}
}
