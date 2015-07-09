// png_color.cs - color structures
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
	// Three color definitions. The order of the red, green, and blue, (and the
	// exact size) is not important, although the size of the fields need to
	// be byte or ushort (as defined below).
	public struct png_color
	{
		public byte red;
		public byte green;
		public byte blue;
	}

	public struct png_color_16
	{
		public byte index;	// used for palette files
		public ushort red;	// for use in red green blue files
		public ushort green;
		public ushort blue;
		public ushort gray;	// for use in grayscale files
	}

	public struct png_color_8
	{
		public byte red;	// for use in red green blue files
		public byte green;
		public byte blue;
		public byte gray;	// for use in grayscale files
		public byte alpha;	// for alpha channel files
	}
}
