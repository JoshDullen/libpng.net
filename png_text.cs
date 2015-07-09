// png_text.cs - Text chunks struct
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
	// png_text holds the contents of a text/ztxt/itxt chunk in a PNG file,
	// and whether that contents is compressed or not. The "key" field
	// points to a regular zero-terminated C string. The "text", "lang", and
	// "lang_key" fields can be regular C strings, empty strings, or NULL pointers.
	// However, the * structure returned by png_get_text() will always contain
	// regular zero-terminated C strings (possibly empty), never NULL pointers,
	// so they can be safely used in printf() and other string-handling functions.
	public struct png_text
	{
		public PNG_TEXT_COMPRESSION compression; // compression value (see below)
		public string key;			// keyword, 1-79 character description of "text"
		public string text;			// comment, may be an empty string (ie "") or a NULL pointer
		public string lang;			// language code, 0-79 characters or a NULL pointer
		public string lang_key;		// keyword translated UTF-8 string, 0 or more chars or a NULL pointer
	}

	// Supported compression types for text in PNG files (tEXt, and zTXt).
	// The values of the PNG_TEXT_COMPRESSION_ defines should NOT be changed.
	public enum PNG_TEXT_COMPRESSION : int
	{
		//tEXt=-1,
		//zTXt=0,
		//iTXt=1,
		//iTXt_deflate=2,

		NONE_WR=-3,
		zTXt_WR=-2,
		NONE=-1,
		zTXt=0,
		ITXT_COMPRESSION_NONE=1,
		ITXT_COMPRESSION_zTXt=2,
		LAST=3 // Not a valid value
	}
}
