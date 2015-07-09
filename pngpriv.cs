// pngpriv.cs
//
// Based on libpng version 1.4.3 - June 26, 2010
// This code is released under the libpng license.
// For conditions of distribution and use, see copyright notice in License.txt
// Copyright (C) 2007-2010 by the Authors
// Copyright (c) 1998-2010 Glenn Randers-Pehrson
// (Version 0.96 Copyright (c) 1996, 1997 Andreas Dilger)
// (Version 0.88 Copyright (c) 1995, 1996 Guy Eric Schalnat, Group 42, Inc.)

// The symbols declared in this file (including the functions declared
// as PNG_EXTERN) are PRIVATE. They are not part of the libpng public
// interface, and are not recommended for use by regular applications.
// Some of them may become public in the future; others may stay private,
// change in an incompatible way, or even disappear.
// Although the libpng users are not forbidden to include this header,
// they should be well aware of the issues that may arise from doing so.

using System;
using System.Collections.Generic;
using System.Text;

namespace Free.Ports.libpng
{
	[Flags]
	enum PNG_MODE
	{
		// Various modes of operation, that are visible to applications because
		// they are used for unknown chunk location.
		None=0x00,
		HAVE_IHDR=0x01,
		HAVE_PLTE=0x02,
		HAVE_IDAT=0x04,
		AFTER_IDAT=0x08,	// Have complete zlib datastream
		HAVE_IEND=0x10,

		// More modes of operation. Note that after an init, mode is set to
		// zero automatically when the structure is created.
		HAVE_gAMA=0x0020,
		HAVE_cHRM=0x0040,
		HAVE_sRGB=0x0080,
		HAVE_CHUNK_HEADER=0x0100,
		//WROTE_tIME=0x0200, // Not used in this port
		WROTE_INFO_BEFORE_PLTE=0x0400,
		BACKGROUND_IS_GRAY=0x0800,
		HAVE_PNG_SIGNATURE=0x1000,
		HAVE_CHUNK_AFTER_IDAT=0x2000,	// Have another chunk after IDAT
	}

	// flags for the transformations the PNG library does on the image data
	[Flags]
	internal enum PNG_TRANSFORMATION
	{
		None=0x00000000,
		BGR=0x00000001,
		INTERLACE=0x00000002,
		PACK=0x00000004,
		SHIFT=0x00000008,
		SWAP_BYTES=0x00000010,
		INVERT_MONO=0x00000020,
		QUANTIZE=0x00000040, // formerly PNG_DITHER
		BACKGROUND=0x00000080,
		BACKGROUND_EXPAND=0x00000100,
		//_UNUSED_1=0x00000200,	// unused
		_16_TO_8=0x00000400,
		RGBA=0x00000800,
		EXPAND=0x00001000,
		GAMMA=0x00002000,
		GRAY_TO_RGB=0x00004000,
		FILLER=0x00008000,
		PACKSWAP=0x00010000,
		SWAP_ALPHA=0x00020000,
		STRIP_ALPHA=0x00040000,
		INVERT_ALPHA=0x00080000,
		USER_TRANSFORM=0x00100000,
		RGB_TO_GRAY_ERR=0x00200000,
		RGB_TO_GRAY_WARN=0x00400000,
		//_UNUSED_2=0x00800000,	// unused
		ADD_ALPHA=0x01000000,
		EXPAND_tRNS=0x02000000,
		PREMULTIPLY_ALPHA=0x4000000, // Added to libpng-1.2.41 by volker
		//_UNUSED_4=0x08000000,	// unused
		//_UNUSED_5=0x10000000,	// unused
		//_UNUSED_6=0x20000000,	// unused
		//_UNUSED_7=0x40000000,	// unused

		RGB_TO_GRAY=RGB_TO_GRAY_ERR|RGB_TO_GRAY_WARN
	}

	public static partial class PNG
	{
		// Scaling factor for filter heuristic weighting calculations
		internal const int WEIGHT_SHIFT=8;
		internal const int WEIGHT_FACTOR=1<<WEIGHT_SHIFT;
		internal const int COST_SHIFT=3;
		internal const int COST_FACTOR=1<<COST_SHIFT;
	}

	// flags for the png_ptr.flags rather than declaring a byte for each one
	[Flags]
	internal enum PNG_FLAG
	{
		ZLIB_CUSTOM_STRATEGY=0x00000001,
		ZLIB_CUSTOM_LEVEL=0x00000002,
		ZLIB_CUSTOM_MEM_LEVEL=0x00000004,
		ZLIB_CUSTOM_WINDOW_BITS=0x00000008,
		ZLIB_CUSTOM_METHOD=0x00000010,
		ZLIB_FINISHED=0x00000020,
		ROW_INIT=0x00000040,
		FILLER_AFTER=0x00000080,
		CRC_ANCILLARY_USE=0x00000100,
		CRC_ANCILLARY_NOWARN=0x00000200,
		CRC_CRITICAL_USE=0x00000400,
		CRC_CRITICAL_IGNORE=0x00000800,
		//_UNUSED_1=0x00001000,	// unused
		//_UNUSED_2=0x00002000,	// unused
		//_UNUSED_3=0x00004000,	// unused
		KEEP_UNKNOWN_CHUNKS=0x00008000,
		KEEP_UNSAFE_CHUNKS=0x00010000,
		LIBRARY_MISMATCH=0x00020000,
		STRIP_ERROR_NUMBERS=0x00040000,
		STRIP_ERROR_TEXT=0x00080000,
		MALLOC_NULL_MEM_OK=0x00100000,
		ADD_ALPHA=0x00200000,			// Added to libpng-1.2.8
		STRIP_ALPHA=0x00400000,			// Added to libpng-1.2.8
		BENIGN_ERRORS_WARN=0x800000,	// Added to libpng-1.4.0
		//_UNUSED_4=0x01000000,	// unused
		//_UNUSED_5=0x02000000,	// unused
		//_UNUSED_6=0x04000000,	// unused
		//_UNUSED_7=0x08000000,	// unused
		//_UNUSED_8=0x10000000,	// unused
		//_UNUSED_9=0x20000000,	// unused
		//_UNUSED_10=0x40000000,// unused

		CRC_ANCILLARY_MASK=CRC_ANCILLARY_USE|CRC_ANCILLARY_NOWARN,
		CRC_CRITICAL_MASK=CRC_CRITICAL_USE|CRC_CRITICAL_IGNORE,
		CRC_MASK=CRC_ANCILLARY_MASK|CRC_CRITICAL_MASK
	}

	public partial class png_struct
	{
		// Save typing and make code easier to understand
		static int PNG_COLOR_DIST(png_color c1, png_color c2)
		{
			return Math.Abs((int)c1.red-c2.red)+Math.Abs((int)c1.green-c2.green)+Math.Abs((int)c1.blue-c2.blue);
		}

		static uint PNG_ROWBYTES(int pixel_bits, uint width)
		{
			return (pixel_bits>=8?width*(((uint)pixel_bits)>>3):(width*((uint)pixel_bits)+7)>>3);
		}

		// PNG_OUT_OF_RANGE returns true if value is outside the range
		// ideal-delta..ideal+delta. Each argument is evaluated twice.
		// "ideal" and "delta" should be constants, normally simple
		// integers, "value" a variable.
		static bool PNG_OUT_OF_RANGE(uint value, uint ideal, uint delta) { return value<(ideal-delta)||value>(ideal+delta); }
	}

	public static partial class PNG
	{
		// Constant strings for known chunk types.  If you need to add a chunk,
		// define the name here, and add an invocation of the macro in png.c and
		// wherever it's needed.
		internal static readonly byte[] IHDR=new byte[] { 73, 72, 68, 82, 0 };
		internal static readonly byte[] IDAT=new byte[] { 73, 68, 65, 84, 0 };
		internal static readonly byte[] IEND=new byte[] { 73, 69, 78, 68, 0 };
		internal static readonly byte[] PLTE=new byte[] { 80, 76, 84, 69, 0 };

		internal static readonly byte[] bKGD=new byte[] { 98, 75, 71, 68, 0 };
		internal static readonly byte[] cHRM=new byte[] { 99, 72, 82, 77, 0 };
		//internal static readonly byte[] fRAc=new byte[] { 102, 82, 65, 99, 0 };
		internal static readonly byte[] gAMA=new byte[] { 103, 65, 77, 65, 0 };
		internal static readonly byte[] gIFg=new byte[] { 103, 73, 70, 103, 0 };
		//internal static readonly byte[] gIFt=new byte[] { 103, 73, 70, 116, 0 };
		internal static readonly byte[] gIFx=new byte[] { 103, 73, 70, 120, 0 };
		internal static readonly byte[] hIST=new byte[] { 104, 73, 83, 84, 0 };
		internal static readonly byte[] iCCP=new byte[] { 105, 67, 67, 80, 0 };
		internal static readonly byte[] iTXt=new byte[] { 105, 84, 88, 116, 0 };
		internal static readonly byte[] oFFs=new byte[] { 111, 70, 70, 115, 0 };
		internal static readonly byte[] pCAL=new byte[] { 112, 67, 65, 76, 0 };
		internal static readonly byte[] pHYs=new byte[] { 112, 72, 89, 115, 0 };
		internal static readonly byte[] sBIT=new byte[] { 115, 66, 73, 84, 0 };
		internal static readonly byte[] sCAL=new byte[] { 115, 67, 65, 76, 0 };
		internal static readonly byte[] sPLT=new byte[] { 115, 80, 76, 84, 0 };
		internal static readonly byte[] sRGB=new byte[] { 115, 82, 71, 66, 0 };
		internal static readonly byte[] sTER=new byte[] { 115, 84, 69, 82, 0 };
		internal static readonly byte[] tEXt=new byte[] { 116, 69, 88, 116, 0 };
		internal static readonly byte[] tIME=new byte[] { 116, 73, 77, 69, 0 };
		internal static readonly byte[] tRNS=new byte[] { 116, 82, 78, 83, 0 };
		internal static readonly byte[] zTXt=new byte[] { 122, 84, 88, 116, 0 };
	}
}