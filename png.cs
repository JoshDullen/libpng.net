// png.cs - Main file for PNG reference library
//
// Based on libpng version 1.4.3 - June 26, 2010
// This code is released under the libpng license.
// For conditions of distribution and use, see copyright notice in License.txt
// Copyright (C) 2007-2010 by the Authors
// Copyright (c) 1998-2010 Glenn Randers-Pehrson
// (Version 0.96 Copyright (c) 1996, 1997 Andreas Dilger)
// (Version 0.88 Copyright (c) 1995, 1996 Guy Eric Schalnat, Group 42, Inc.)

// The PNG specification is available as a W3C Recommendation and as an
// ISO Specification, http://www.w3.org/TR/2003/REC-PNG-20031110/

// The contributing authors would like to thank all those who helped
// with testing, bug fixes, and patience. This wouldn't have been
// possible without all of you.
//
// Thanks to Frank J. T. Wojcik for helping with the documentation.

using System;
using System.Collections.Generic;
using System.Text;

namespace Free.Ports.libpng
{
	#region Enums
	// color types. Note that not all combinations are legal
	[Flags]
	public enum PNG_COLOR_TYPE : byte
	{
		GRAY=0,

		//was PNG_COLOR_MASK_
		PALETTE_MASK=1,
		COLOR_MASK=2,
		ALPHA_MASK=4,

		PALETTE=COLOR_MASK|PALETTE_MASK,
		RGB=COLOR_MASK,
		RGB_ALPHA=COLOR_MASK|ALPHA_MASK,
		GRAY_ALPHA=ALPHA_MASK,

		// aliases
		//RGBA=COLOR_MASK|ALPHA_MASK,
		//GA=ALPHA_MASK,
	}

	// This is for compression type. PNG 1.0-1.2 only define the single type.
	public enum PNG_COMPRESSION_TYPE
	{
		BASE=0,	// Deflate method 8, 32K window
		DEFAULT=BASE,
	}

	// This is for filter type. PNG 1.0-1.2 only define the single type.
	public enum PNG_FILTER_TYPE
	{
		BASE=0,						// Single row per-byte filtering
		INTRAPIXEL_DIFFERENCING=64,	// Used only in MNG datastreams
		DEFAULT=BASE,
	}

	// These are for the interlacing type. These values should NOT be changed.
	public enum PNG_INTERLACE
	{
		NONE=0,						// Non-interlaced image
		ADAM7=1,					// Adam7 interlacing
		LAST=2						// Not a valid value
	}

	// These are for the oFFs chunk. These values should NOT be changed.
	public enum PNG_OFFSET
	{
		PIXEL=0,					// Offset in pixels
		MICROMETER=1,				// Offset in micrometers (1/10^6 meter)
		LAST=2,						// Not a valid value
	}

	// These are for the pCAL chunk. These values should NOT be changed.
	public enum PNG_EQUATION
	{
		LINEAR=0,					// Linear transformation
		BASE_E=1,					// Exponential base e transform
		ARBITRARY=2,				// Arbitrary base exponential transform
		HYPERBOLIC=3,				// Hyperbolic sine transformation
		LAST=4,						// Not a valid value
	}

	// These are for the sCAL chunk. These values should NOT be changed.
	public enum PNG_SCALE
	{
		UNKNOWN=0,					// unknown unit (image scale)
		METER=1,					// meters per pixel
		RADIAN=2,					// radians per pixel
		LAST=3,						// Not a valid value
	}

	// These are for the pHYs chunk. These values should NOT be changed.
	public enum PNG_RESOLUTION
	{
		UNKNOWN=0,					// pixels/unknown unit (aspect ratio)
		METER=1,					// pixels/meter
		LAST=2,						// Not a valid value
	}

	// These are for the sRGB chunk. These values should NOT be changed.
	public enum PNG_sRGB_INTENT
	{
		PERCEPTUAL=0,
		RELATIVE=1,
		SATURATION=2,
		ABSOLUTE=3,
		LAST=4,						// Not a valid value
	}

	// These determine if an ancillary chunk's data has been successfully read
	// from the PNG header, or if the application has filled in the corresponding
	// data in the info_struct to be written into the output file. The values
	// of the PNG_INFO.<chunk> defines should NOT be changed.
	[Flags]
	public enum PNG_INFO
	{
		None=0x0000,
		gAMA=0x0001,
		sBIT=0x0002,
		cHRM=0x0004,
		PLTE=0x0008,
		tRNS=0x0010,
		bKGD=0x0020,
		hIST=0x0040,
		pHYs=0x0080,
		oFFs=0x0100,
		tIME=0x0200,
		pCAL=0x0400,
		sRGB=0x0800,
		iCCP=0x1000,
		sPLT=0x2000,
		sCAL=0x4000,
		IDAT=0x8000,
		sTER=0x10000,
		gIFg=0x20000,
		gIFx=0x40000,
	}

	// Transform masks for the high-level interface
	[Flags]
	public enum PNG_TRANSFORM
	{
		None=0x0000,
		IDENTITY=0x0000,			// read and write
		STRIP_16=0x0001,			// read only
		STRIP_ALPHA=0x0002,			// read only
		PACKING=0x0004,				// read and write
		PACKSWAP=0x0008,			// read and write
		EXPAND=0x0010,				// read only
		INVERT_MONO=0x0020,			// read and write
		SHIFT=0x0040,				// read and write
		BGR=0x0080,					// read and write
		SWAP_ALPHA=0x0100,			// read and write
		SWAP_ENDIAN=0x0200,			// read and write
		INVERT_ALPHA=0x0400,		// read and write
		STRIP_FILLER=0x0800,		// write only, deprecated

		// Added to libpng-1.2.34
		STRIP_FILLER_BEFORE=0x0800,	// write only
		STRIP_FILLER_AFTER=0x1000,	// write only

		//Added to libpng-1.4.0
		GRAY_TO_RGB=0x2000,			// read only
	}

	// Flags for MNG supported features
	[Flags]
	public enum PNG_FLAG_MNG
	{
		None=0x00,
		EMPTY_PLTE=0x01,
		FILTER_64=0x04,
		ALL=0x05
	}

	// The values of the PNG_FILLER_ defines should NOT be changed
	public enum PNG_FILLER
	{
		BEFORE=0,
		AFTER=1
	}

	public enum PNG_BACKGROUND_GAMMA
	{
		UNKNOWN=0,
		SCREEN=1,
		FILE=2,
		UNIQUE=3,
	}

	// Values for png_set_crc_action() to say how to handle CRC errors in
	// ancillary and critical chunks, and whether to use the data contained
	// therein. Note that it is impossible to "discard" data in a critical
	// chunk. For versions prior to 0.90, the action was always error/quit,
	// whereas in version 0.90 and later, the action for CRC errors in ancillary
	// chunks is warn/discard. These values should NOT be changed.
	public enum PNG_CRC
	{ //value				action:critical		action:ancillary
		DEFAULT=0,		//	error/quit			warn/discard data
		ERROR_QUIT=1,	//	error/quit			error/quit
		WARN_DISCARD=2,	//	(INVALID)			warn/discard data
		WARN_USE=3,		//	warn/use data		warn/use data
		QUIET_USE=4,	//	quiet/use data		quiet/use data
		NO_CHANGE=5,	//	use current value	use current value
	}

	// Flags for png_set_filter() to say which filters to use. The flags
	// are chosen so that they don't conflict with real filter types
	// below, in case they are supplied instead of the #defined constants.
	// These values should NOT be changed.
	[Flags]
	public enum PNG_FILTER
	{
		None=0x00,
		NO=0x00,
		NONE=0x08,
		SUB=0x10,
		UP=0x20,
		AVG=0x40,
		PAETH=0x80,
		ALL=NONE|SUB|UP|AVG|PAETH,
	}

	// Filter values (not flags) - used in pngwrite.c, pngwutil.c for now.
	// These defines should NOT be changed.
	public enum PNG_FILTER_VALUE
	{
		NONE=0,
		SUB=1,
		UP=2,
		AVG=3,
		PAETH=4,
		LAST=5,
	}

	// Heuristic used for row filter selection. These defines should NOT be changed.
	public enum PNG_FILTER_HEURISTIC
	{
		DEFAULT=0,		// Currently "UNWEIGHTED"
		UNWEIGHTED=1,	// Used by libpng < 0.95
		WEIGHTED=2,		// Experimental feature
		LAST=3,			// Not a valid value
	}

	// For use in png_set_keep_unknown
	public enum PNG_HANDLE_CHUNK
	{
		AS_DEFAULT=0,
		NEVER=1,
		IF_SAFE=2,
		ALWAYS=3,
	}

	// The values of the PNG_STEREO defines should NOT be changed
	public enum PNG_STEREO
	{
		CROSS=0,
		DIVERGING=1
	}

	// The flags returned by png_get_io_state() are the following:
	[Flags]
	public enum PNG_IO
	{
		NONE=0x0000,		// no I/O at this moment
		READING=0x0001,		// currently reading
		WRITING=0x0002,		// currently writing
		SIGNATURE=0x0010,	// currently at the file signature
		CHUNK_HDR=0x0020,	// currently at the chunk header
		CHUNK_DATA=0x0040,	// currently at the chunk data
		CHUNK_CRC=0x0080,	// currently at the chunk crc
		MASK_OP=0x000f,		// current operation: reading/writing
		MASK_LOC=0x00f0,	// current location: sig/hdr/data/crc
	}
	#endregion

	#region Delegates
	// These are the function types for the I/O functions and for the functions
	// that allow the user to override the default I/O functions with his or her
	// own. The png_error_ptr type should match that of user-supplied warning
	// and error functions, while the png_rw_ptr type should match that of the
	// user read/write data functions.
	public delegate void png_read_status_ptr(png_struct png, uint row_number, byte pass);
	public delegate void png_write_status_ptr(png_struct png, uint row_number, byte pass);
	public delegate void png_user_transform_ptr(png_struct png, ref png_row_info row_info, byte[] row);
	#endregion

	public static partial class PNG
	{
		public const string VERSION_STRING="Based on libpng version 1.4.3 - June 26, 2010\n";

		// This is the size of the compression buffer, and thus the size of
		// an IDAT chunk. Make this whatever size you feel is best for your
		// machine. One of these will be allocated per png_struct. When this
		// is full, it writes the data to the disk, and does some other
		// calculations. Making this an extremely small size will slow
		// the library down, but you may want to experiment to determine
		// where it becomes significant, if you are concerned with memory
		// usage. Note that zlib allocates at least 32Kb also. For readers,
		// this describes the size of the buffer available to read the data in.
		// Unless this gets smaller than the size of a row (compressed),
		// it should not make much difference how big this is.
		public const uint ZBUF_SIZE=8192;

		// Maximum positive integer used in PNG is (2^31)-1
		public const uint UINT_31_MAX=0x7fffffff;
		public const uint UINT_32_MAX=0xffffffff;
		public const uint SIZE_MAX=0xffffffff;

		// This is for text chunks
		//public const int KEYWORD_MAX_LENGTH=79;

		// Maximum number of entries in PLTE/sPLT/tRNS arrays
		public const int MAX_PALETTE_LENGTH=256;

		// Added at libpng-1.0.16 and 1.2.6. To accept all valid PNGS no matter
		// how large, set these limits to 0x7fffffff
		internal const uint USER_WIDTH_MAX=0x7fffffff;
		internal const uint USER_HEIGHT_MAX=0x7fffffff;

		//internal const uint USER_CHUNK_CACHE_MAX=0x7fffffff;

		internal static readonly byte[] signature=new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };
	}
}
