// png_sPLT.cs - sPLT chunk structures
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
	// The following two structures are used for the in-core representation
	// of sPLT chunks.
	public struct png_sPLT_entry
	{
		public ushort red;
		public ushort green;
		public ushort blue;
		public ushort alpha;
		public ushort frequency;
	}

	// When the depth of the sPLT palette is 8 bits, the color and alpha samples
	// occupy the LSB of their respective members, and the MSB of each member
	// is zero-filled. The frequency member always occupies the full 16 bits.
	public struct png_sPLT_t
	{
		public string name;					// palette name
		public byte depth;					// depth of palette samples
		public png_sPLT_entry[] entries;	// palette entries
		public uint nentries;				// number of palette entries
	}
}
