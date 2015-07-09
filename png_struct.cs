// png_struct.cs - Another main file for PNG reference library
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
using System.Diagnostics;
using System.IO;
using System.Text;

using Free.Ports.zLib;

namespace Free.Ports.libpng
{
	// The structure that holds the information to read and write PNG files.
	// The only people who need to care about what is inside of this are the
	// people who will be modifying the library for their own special needs.
	public partial class png_struct
	{
		#region public class png_struct
		Stream io_ptr;						// ptr to application struct for I/O functions

		//png_error_ptr error_fn;			// function for printing errors and aborting
		//png_error_ptr warning_fn;			// function for printing warnings
		//object error_ptr;					// user supplied struct for error functions
		//png_rw_ptr write_data_fn;			// function for writing output data
		//png_rw_ptr read_data_fn;			// function for reading input data
		//object io_ptr;					// ptr to application struct for I/O functions

		png_user_transform_ptr read_user_transform_fn;	// user read transform
		png_user_transform_ptr write_user_transform_fn;	// user write transform

		object user_transform_ptr;			// user supplied struct for user transform
		byte user_transform_depth;			// bit depth of user transformed pixels
		byte user_transform_channels;		// channels in user transformed pixels

		PNG_MODE mode;						// tells us where we are in the PNG file
		PNG_FLAG flags;						// flags indicating various things to libpng
		PNG_TRANSFORMATION transformations;	// which transformations to perform

		zlib.z_stream zstream=new zlib.z_stream();	// pointer to decompression structure (below)
		byte[] zbuf;						// buffer for zlib
		uint zbuf_size;						// size of zbuf
		int zlib_level;						// holds zlib compression level
		int zlib_method;					// holds zlib compression method
		int zlib_window_bits;				// holds zlib compression window bits
		int zlib_mem_level;					// holds zlib compression memory level
		int zlib_strategy;					// holds zlib compression strategy

		uint width;							// width of image in pixels
		uint height;						// height of image in pixels
		uint num_rows;						// number of rows in current pass
		uint usr_width;						// width of row at start of write
		uint rowbytes;						// size of row in bytes
		uint iwidth;						// width of current interlaced row in pixels
		uint row_number;					// current row in interlace pass
		byte[] prev_row;					// buffer to save previous (unfiltered) row
		byte[] row_buf;						// buffer to save current (unfiltered) row
		byte[] sub_row;						// buffer to save "sub" row when filtering
		byte[] up_row;						// buffer to save "up" row when filtering
		byte[] avg_row;						// buffer to save "avg" row when filtering
		byte[] paeth_row;					// buffer to save "Paeth" row when filtering
		png_row_info row_info;				// used for transformation routines

		uint idat_size;						// current IDAT size for read
		uint crc;							// current chunk CRC value
		png_color[] palette;				// palette from the input file
		//ushort num_palette;				// number of color entries in palette
		ushort num_trans;					// number of transparency values
		byte[] chunk_name=new byte[4];		// not-null-terminated name of current chunk
		//PNG_COMPRESSION_TYPE compression;	// file compression type (always 0)
		//PNG_FILTER_TYPE filter;			// file filter type (always 0)
		PNG_INTERLACE interlaced;			// PNG_INTERLACE_NONE, PNG_INTERLACE_ADAM7
		byte pass;							// current interlace pass (0 - 6)
		PNG_FILTER do_filter;				// row filter flags (see PNG_FILTER)
		PNG_COLOR_TYPE color_type;			// color type of file
		byte bit_depth;						// bit depth of file
		byte usr_bit_depth;					// bit depth of users row
		byte pixel_depth;					// number of bits per pixel
		byte channels;						// number of channels in file
		byte usr_channels;					// channels at start of write
		byte sig_bytes;						// magic bytes read/written from start of file
		ushort filler;						// filler bytes for pixel expansion

		PNG_BACKGROUND_GAMMA background_gamma_type;
		double background_gamma;
		png_color_16 background;			// background color in screen gamma space
		png_color_16 background_1;			// background normalized to gamma 1.0

		int gamma_shift;					// number of "insignificant" bits 16-bit gamma
		double gamma;						// file gamma value
		double screen_gamma;				// screen gamma value (display_exponent)

		byte[] gamma_table;					// gamma table for 8-bit depth files
		byte[] gamma_from_1;				// converts from 1.0 to screen
		byte[] gamma_to_1;					// converts from file to 1.0
		ushort[][] gamma_16_table;			// gamma table for 16-bit depth files
		ushort[][] gamma_16_from_1;			// converts from 1.0 to screen
		ushort[][] gamma_16_to_1;			// converts from file to 1.0

		png_color_8 sig_bit;				// significant bits in each available channel

		png_color_8 shift;					// shift for significant bit tranformation

		byte[] trans_alpha;					// transparency values for paletted files
		png_color_16 trans_color;			// transparency values for non-paletted files

		png_read_status_ptr read_row_fn;	// called after each row is decoded
		png_write_status_ptr write_row_fn;	// called after each row is encoded

		byte[] palette_lookup;				// lookup table for quantizing
		byte[] quantize_index;				// index translation for palette files

		ushort[] hist;						// histogram

		PNG_FILTER_HEURISTIC heuristic_method;	// heuristic for row filter selection
		byte num_prev_filters;					// number of weights for previous rows
		byte[] prev_filters;					// filter type(s) of previous row(s)
		ushort[] filter_weights;				// weight(s) for previous line(s)
		ushort[] inv_filter_weights;			// 1/weight(s) for previous line(s)
		ushort[] filter_costs;					// relative filter calculation cost
		ushort[] inv_filter_costs;				// 1/relative filter calculation cost

		// New members added in libpng-1.0.3
		bool rgb_to_gray_status;
		// These were changed from png_byte in libpng-1.0.6
		ushort rgb_to_gray_red_coeff;
		ushort rgb_to_gray_green_coeff;
		ushort rgb_to_gray_blue_coeff;

		// New member added in libpng-1.0.4 (renamed in 1.0.9)
		// Changed from png_byte to png_uint_32 at version 1.2.0
		PNG_FLAG_MNG mng_features_permitted;

		// New member added in libpng-1.0.9, ifdef'ed out in 1.0.12, enabled in 1.2.0
		PNG_FILTER_TYPE filter_type;

		// New member added in libpng-1.0.13 and 1.2.0
		//byte[] big_row_buf;					// buffer to save current (unfiltered) row

		// The following three members were added at version 1.0.14 and 1.2.4
		//byte[] quantize_sort;					// working sort array
		//byte[] index_to_palette;				// where the original index currently is in the palette
		//byte[] palette_to_index;				// which original index points to this palette color

		PNG_COMPRESSION_TYPE compression_type;

		// New members added in libpng-1.2.26
		//uint old_big_row_buf_size;
		//uint old_prev_row_size;

		// New member added in libpng-1.2.30
		byte[] chunkdata;					// buffer for reading chunk data

		// New member added in libpng-1.4.0
		PNG_IO io_state;
		#endregion

		#region public class png_info
		// png_info is a structure that holds the information in a PNG file so
		// that the application can find out the characteristics of the image.
		// If you are reading the file, this structure will tell you what is
		// in the PNG file. If you are writing the file, fill in the information
		// you want to put into the PNG file, then call png_write_info().
		// The names chosen should be very close to the PNG specification, so
		// consult that document for information about the meaning of each field.
		//
		// The following members may have allocated storage attached that should be
		// cleaned up before the structure is discarded: palette, trans_alpha, text,
		// pcal_purpose, pcal_units, pcal_params, hist, iccp_name, iccp_profile,
		// splt_palettes, scal_unit, row_pointers, and unknowns. By default, these
		// are automatically freed when the info structure is deallocated, if they were
		// allocated internally by libpng. This behavior can be changed by means
		// of the png_data_freer() function.
		//
		// More allocation details: all the chunk-reading functions that
		// change these members go through the corresponding png_set_*
		// functions. A function to clear these members is available: see
		// png_free_data(). The png_set_* functions do not depend on being
		// able to point info structure members to any of the storage they are
		// passed (they make their own copies), EXCEPT that the png_set_text
		// functions use the same storage passed to them in the text_ptr or
		// itxt_ptr structure argument, and the png_set_rows and png_set_unknowns
		// functions do not make their own copies.

		// The following are necessary for every PNG file
		uint info_ptr_width;					// width of image in pixels (from IHDR)
		uint info_ptr_height;					// height of image in pixels (from IHDR)
		PNG_INFO info_ptr_valid;				// valid chunk data (see PNG_INFO below)
		uint info_ptr_rowbytes;					// bytes needed to hold an untransformed row
		png_color[] info_ptr_palette;			// array of color values (valid & PNG_INFO.PLTE)
		//ushort info_ptr_num_palette;			// number of color entries in "palette" (PLTE)
		ushort info_ptr_num_trans;				// number of transparent palette color (tRNS)
		byte info_ptr_bit_depth;				// 1, 2, 4, 8, or 16 bits/channel (from IHDR)
		PNG_COLOR_TYPE info_ptr_color_type;		// see PNG_COLOR_TYPE below (from IHDR)

		// The following three should have been named *_method not *_type
		PNG_COMPRESSION_TYPE info_ptr_compression_type;	// must be PNG_COMPRESSION_TYPE_BASE (IHDR)
		PNG_FILTER_TYPE info_ptr_filter_type;	// must be PNG_FILTER_TYPE_BASE (from IHDR)
		PNG_INTERLACE info_ptr_interlace_type;	// PNG_INTERLACE_NONE, PNG_INTERLACE_ADAM7

		// The following is informational only on read, and not used on writes.
		byte info_ptr_channels;					// number of data channels per pixel (1, 2, 3, 4)
		byte info_ptr_pixel_depth;				// number of bits per pixel
		//byte info_ptr_spare_byte;				// to align the data, and for future use
		byte[] info_ptr_signature=new byte[8];	// magic bytes read by libpng from start of file

		// The rest of the data is optional. If you are reading, check the
		// valid field to see if the information in these are valid. If you
		// are writing, set the valid field to those chunks you want written,
		// and initialize the appropriate fields below.

		// The gAMA chunk describes the gamma characteristics of the system
		// on which the image was created, normally in the range [1.0, 2.5].
		// Data is valid if (valid & PNG_INFO_gAMA) is non-zero.
		double info_ptr_gamma;					// gamma value of image, if (valid & PNG_INFO_gAMA)

		// Data valid if (valid & PNG_INFO_sRGB) non-zero.
		PNG_sRGB_INTENT info_ptr_srgb_intent;	// sRGB rendering intent [0, 1, 2, or 3]

		// The tEXt, and zTXt chunks contain human-readable textual data in
		// uncompressed, compressed, and optionally compressed forms, respectively.
		// The data in "text" is an array of pointers to uncompressed,
		// null-terminated C strings. Each chunk has a keyword that describes the
		// textual data contained in that chunk. Keywords are not required to be
		// unique, and the text string may be empty. Any number of text chunks may
		// be in an image.
		List<png_text> info_ptr_text;			// array of comments read/to write

		// The tIME chunk holds the last time the displayed image data was
		// modified. See the png_time struct for the contents of this struct.
		png_time info_ptr_mod_time;

		// The sBIT chunk specifies the number of significant high-order bits
		// in the pixel data. Values are in the range [1, bit_depth], and are
		// only specified for the channels in the pixel data. The contents of
		// the low-order bits is not specified. Data is valid if
		// (valid & PNG_INFO_sBIT) is non-zero.
		png_color_8 info_ptr_sig_bit;			// significant bits in each available channel

		// The tRNS chunk supplies transparency data for paletted images and
		// other image types that don't need a full alpha channel. There are
		// "num_trans" transparency values for a paletted image, stored in the
		// same order as the palette colors, starting from index 0. Values
		// for the data are in the range [0, 255], ranging from fully transparent
		// to fully opaque, respectively. For non-paletted images, there is a
		// single color specified that should be treated as fully transparent.
		// Data is valid if (valid&PNG_INFO_tRNS) is non-zero.
		byte[] info_ptr_trans_alpha;			// transparency values for paletted files
		png_color_16 info_ptr_trans_color;		// transparency values for non-paletted files

		// The bKGD chunk gives the suggested image background color if the
		// display program does not have its own background color and the image
		// is needs to composited onto a background before display. The colors
		// in "background" are normally in the same color space/depth as the
		// pixel data. Data is valid if (valid & PNG_INFO_bKGD) is non-zero.
		png_color_16 info_ptr_background;

		// The oFFs chunk gives the offset in "offset_unit_type" units rightwards
		// and downwards from the top-left corner of the display, page, or other
		// application-specific co-ordinate space. See the PNG_OFFSET_ defines
		// below for the unit types. Valid if (valid & PNG_INFO_oFFs) non-zero.
		int info_ptr_x_offset;					// x offset on page
		int info_ptr_y_offset;					// y offset on page
		PNG_OFFSET info_ptr_offset_unit_type;	// offset units type

		// The pHYs chunk gives the physical pixel density of the image for
		// display or printing in "phys_unit_type" units (see PNG_RESOLUTION_
		// defines below). Data is valid if (valid & PNG_INFO_pHYs) is non-zero.
		uint info_ptr_x_pixels_per_unit;		// horizontal pixel density
		uint info_ptr_y_pixels_per_unit;		// vertical pixel density
		PNG_RESOLUTION info_ptr_phys_unit_type;	// resolution type (see PNG_RESOLUTION_ below)

		// The hIST chunk contains the relative frequency or importance of the
		// various palette entries, so that a viewer can intelligently select a
		// reduced-color palette, if required. Data is an array of "num_palette"
		// values in the range [0,65535]. Data valid if (valid & PNG_INFO_hIST)
		// is non-zero.
		ushort[] info_ptr_hist;

		// The cHRM chunk describes the CIE color characteristics of the monitor
		// on which the PNG was created. This data allows the viewer to do gamut
		// mapping of the input image to ensure that the viewer sees the same
		// colors in the image as the creator. Values are in the range
		// [0.0, 0.8]. Data valid if (valid & PNG_INFO_cHRM) non-zero.
		double info_ptr_x_white, info_ptr_y_white;
		double info_ptr_x_red, info_ptr_y_red;
		double info_ptr_x_green, info_ptr_y_green;
		double info_ptr_x_blue, info_ptr_y_blue;

		// The pCAL chunk describes a transformation between the stored pixel
		// values and original physical data values used to create the image.
		// The integer range [0, 2^bit_depth - 1] maps to the floating-point
		// range given by [pcal_X0, pcal_X1], and are further transformed by a
		// (possibly non-linear) transformation function given by "pcal_type"
		// and "pcal_params" into "pcal_units". Please see the PNG_EQUATION_
		// defines below, and the PNG-Group's PNG extensions document for a
		// complete description of the transformations and how they should be
		// implemented, and for a description of the ASCII parameter strings.
		// Data values are valid if (valid & PNG_INFO_pCAL) non-zero.
		string info_ptr_pcal_purpose;			// pCAL chunk description string
		int info_ptr_pcal_X0;					// minimum value
		int info_ptr_pcal_X1;					// maximum value
		string info_ptr_pcal_units;				// Latin-1 string giving physical units
		string[] info_ptr_pcal_params;			// ASCII strings containing parameter values
		PNG_EQUATION info_ptr_pcal_type;		// equation type (see PNG_EQUATION_ below)

		// iCCP chunk data.
		string info_ptr_iccp_name;				// profile name
		byte[] info_ptr_iccp_profile;			// International Color Consortium profile data
		//uint info_ptr_iccp_proflen;			// ICC profile data length
		byte info_ptr_iccp_compression;			// Always zero

		// data on sPLT chunks (there may be more than one).
		List<png_sPLT_t> info_ptr_splt_palettes;
		//uint info_ptr_splt_palettes_num;

		// The sCAL chunk describes the actual physical dimensions of the
		// subject matter of the graphic. The chunk contains a unit specification
		// a byte value, and two ASCII strings representing floating-point
		// values. The values are width and height corresponsing to one pixel
		// in the image. This external representation is converted to double
		// here. Data values are valid if (valid & PNG_INFO_sCAL) is non-zero.
		PNG_SCALE info_ptr_scal_unit;			// unit of physical scale
		double info_ptr_scal_pixel_width;		// width of one pixel
		double info_ptr_scal_pixel_height;		// height of one pixel

		// sTER chunk data.
		PNG_STEREO info_ptr_stereo_layout;

		// gIFg chunks.
		List<png_gIFg> info_ptr_gIFgs;

		// gIFx chunks.
		List<png_gIFx> info_ptr_gIFxs;
		#endregion

		#region memcmp
		static int memcmp(byte[] buf1, byte[] buf2, uint count)
		{
			if(buf1==null) throw new ArgumentNullException("buf1");
			if(buf2==null) throw new ArgumentNullException("buf1");

			for(uint i=0; i<count; i++)
			{
				if(buf1.Length<=i&&buf2.Length<=i) return 0;
				if(buf1.Length<=i) return -1;
				if(buf2.Length<=i) return 1;
				if(buf1[i]==buf2[i]) continue;
				if(buf1[i]<buf2[i]) return -1;
				return 1;
			}
			return 0;
		}

		static int memcmp(byte[] buf1, uint start1, byte[] buf2, uint start2, uint count)
		{
			if(buf1==null) throw new ArgumentNullException("buf1");
			if(buf2==null) throw new ArgumentNullException("buf1");

			for(uint i=0; i<count; i++, start1++, start2++)
			{
				if(buf1.Length<=start1&&buf2.Length<=start2) return 0;
				if(buf1.Length<=start1) return -1;
				if(buf2.Length<=start2) return 1;
				if(buf1[start1]==buf2[start2]) continue;
				if(buf1[start1]<buf2[start2]) return -1;
				return 1;
			}
			return 0;
		}
		#endregion

		// Simple signature checking function.  This is the same as calling
		// png_check_sig(sig, n) := !png_sig_cmp(sig, 0, n).
		public bool png_check_sig(byte[] sig, uint n)
		{
			return png_sig_cmp(sig, 0, n)==0;
		}

		// Tells libpng that we have already handled the first "num_bytes" bytes
		// of the PNG file signature. If the PNG data is embedded into another
		// stream we can set num_bytes = 8 so that libpng will not attempt to read
		// or write any of the magic bytes before it starts on the IHDR.
		public void png_set_sig_bytes(int num_bytes)
		{
			if(num_bytes>8) throw new PNG_Exception("Too many bytes for PNG signature");
			sig_bytes=(byte)(num_bytes<0?0:num_bytes);
		}

		// Checks whether the supplied bytes match the PNG signature. We allow
		// checking less than the full 8-byte signature so that those apps that
		// already read the first few bytes of a file to determine the file type
		// can simply check the remaining bytes for extra assurance. Returns
		// an integer less than, equal to, or greater than zero if sig is found,
		// respectively, to be less than, to match, or be greater than the correct
		// PNG signature (this is the same behaviour as strcmp, memcmp, etc).
		public static int png_sig_cmp(byte[] sig, uint start, uint num_to_check)
		{
			if(num_to_check>8) num_to_check=8;
			else if(num_to_check<1) return -1;

			if(start>7) return -1;
			if(start+num_to_check>8) num_to_check=8-start;

			return memcmp(sig, start, PNG.signature, start, num_to_check);
		}

		// Reset the CRC variable to 32 bits of 1's. Care must be taken
		// in case CRC is > 32 bits to leave the top bits 0.
		void png_reset_crc()
		{
			crc=zlib.crc32(0, null, 0);
		}

		// Calculate the CRC over a section of data. We can only pass as
		// much data to this routine as the largest single buffer size. We
		// also check that this data will actually be used before going to the
		// trouble of calculating it.
		void png_calculate_crc(byte[] ptr, uint start, uint length)
		{
			bool need_crc=true;

			if((chunk_name[0]&0x20)!=0) // ancillary
			{
				if((flags&PNG_FLAG.CRC_ANCILLARY_MASK)==PNG_FLAG.CRC_ANCILLARY_MASK) need_crc=false;
			}
			else // critical
			{
				if((flags&PNG_FLAG.CRC_CRITICAL_IGNORE)==PNG_FLAG.CRC_CRITICAL_IGNORE) need_crc=false;
			}

			if(need_crc) crc=zlib.crc32(crc, ptr, start, length);
		}

		// This function returns a pointer to the io_ptr associated with the user
		// functions.The application should free any memory associated with this
		// pointer before png_write_destroy() or png_read_destroy() are called.
		public Stream png_get_io_ptr()
		{
			return io_ptr;
		}

		// Initialize the default input/output functions for the PNG file. If you
		// use your own read or write routines, you can call either png_set_read_fn()
		// or png_set_write_fn() instead of png_init_io().
		public void png_init_io(Stream fp)
		{
			io_ptr=fp;
		}

		// Convert the supplied time into an RFC 1123 string suitable for use in
		// a "Creation Time" or other text-based time string.
		public static string png_convert_to_rfc1123(DateTime ptime)
		{
			return ptime.ToUniversalTime().ToString("dd MMM yyyy HH:mm:ss +0000");
		}

		public static string png_get_copyright()
		{
			return "\nlibpng version 1.4.3 - June 26, 2010\n"+
				"Copyright (c) 1998-2010 Glenn Randers-Pehrson\n"+
				"Copyright (c) 1996-1997 Andreas Dilger\n"+
				"Copyright (c) 1995-1996 Guy Eric Schalnat, Group 42, Inc.\n";
		}

		// This function, added to libpng-1.0.6g, is untested.
		public int png_reset_zstream()
		{
			return zlib.inflateReset(zstream);
		}

		// Added at libpng version 1.2.34 and 1.4.0 (moved from pngset.c)

		// Multiply two 32-bit numbers, V1 and V2, to produce a 64 bit result.
		static ulong png_64bit_product(uint v1, uint v2)
		{
			return (ulong)v1*v2;
		}

		static bool png_check_cHRM_fixed(uint white_x, uint white_y, uint red_x, uint red_y, uint green_x, uint green_y, uint blue_x, uint blue_y)
		{
			bool ret=true;

			if(white_x<0||white_y<=0||red_x<0||red_y<0||green_x<0||green_y<0||blue_x<0||blue_y<0)
			{
				Debug.WriteLine("Ignoring attempt to set negative chromaticity value");
				ret=false;
			}
			if(white_x> PNG.UINT_31_MAX||white_y>PNG.UINT_31_MAX||
				  red_x>PNG.UINT_31_MAX||  red_y>PNG.UINT_31_MAX||
				green_x>PNG.UINT_31_MAX||green_y>PNG.UINT_31_MAX||
				 blue_x>PNG.UINT_31_MAX|| blue_y>PNG.UINT_31_MAX)
			{
				Debug.WriteLine("Ignoring attempt to set chromaticity value exceeding 21474.83");
				ret=false;
			}
			if(white_x>100000-white_y)
			{
				Debug.WriteLine("Invalid cHRM white point");
				ret=false;
			}
			if(red_x>100000-red_y)
			{
				Debug.WriteLine("Invalid cHRM red point");
				ret=false;
			}
			if(green_x>100000-green_y)
			{
				Debug.WriteLine("Invalid cHRM green point");
				ret=false;
			}
			if(blue_x>100000-blue_y)
			{
				Debug.WriteLine("Invalid cHRM blue point");
				ret=false;
			}

			ulong xy=png_64bit_product(green_x-red_x, blue_y-red_y);
			ulong yx=png_64bit_product(green_y-red_y, blue_x-red_x);

			if(xy==yx)
			{
				Debug.WriteLine("Ignoring attempt to set cHRM RGB triangle with zero area");
				ret=false;
			}

			return ret;
		}

		void png_check_IHDR(uint width, uint height, int bit_depth, PNG_COLOR_TYPE color_type, PNG_INTERLACE interlace_type, PNG_COMPRESSION_TYPE compression_type, PNG_FILTER_TYPE filter_type)
		{
			bool error=false;

			// Check for width and height valid values
			if(width==0)
			{
				Debug.WriteLine("Image width is zero in IHDR");
				error=true;
			}

			if(height==0)
			{
				Debug.WriteLine("Image height is zero in IHDR");
				error=true;
			}

			if(width>PNG.USER_WIDTH_MAX)
			{
				Debug.WriteLine("Image width exceeds user limit in IHDR");
				error=true;
			}

			if(height>PNG.USER_HEIGHT_MAX)
			{
				Debug.WriteLine("Image height exceeds user limit in IHDR");
				error=true;
			}

			if(width>PNG.UINT_31_MAX)
			{
				Debug.WriteLine("Invalid image width in IHDR");
				error=true;
			}

			if(height>PNG.UINT_31_MAX)
			{
				Debug.WriteLine("Invalid image height in IHDR");
				error=true;
			}

			if(width>(PNG.UINT_32_MAX>>3)	// 8-byte RGBA pixels
				-64							// bigrowbuf hack
				-1							// filter byte
				-7*8						// rounding of width to multiple of 8 pixels
				-8)							// extra max_pixel_depth pad
				Debug.WriteLine("Width is too large for libpng to process pixels");

			// Check other values
			if(bit_depth!=1&&bit_depth!=2&&bit_depth!=4&&bit_depth!=8&&bit_depth!=16)
			{
				Debug.WriteLine("Invalid bit depth in IHDR");
				error=true;
			}

			if(color_type!=PNG_COLOR_TYPE.GRAY&&color_type!=PNG_COLOR_TYPE.PALETTE&&color_type!=PNG_COLOR_TYPE.RGB&&
				color_type!=PNG_COLOR_TYPE.RGB_ALPHA&&color_type!=PNG_COLOR_TYPE.GRAY_ALPHA)
			{
				Debug.WriteLine("Invalid color type in IHDR");
				error=true;
			}

			if(((color_type==PNG_COLOR_TYPE.PALETTE)&&bit_depth>8)||
				((color_type==PNG_COLOR_TYPE.RGB||color_type==PNG_COLOR_TYPE.GRAY_ALPHA||color_type==PNG_COLOR_TYPE.RGB_ALPHA)&&bit_depth<8))
			{
				Debug.WriteLine("Invalid color type/bit depth combination in IHDR");
				error=true;
			}

			if(interlace_type>=PNG_INTERLACE.LAST)
			{
				Debug.WriteLine("Unknown interlace method in IHDR");
				error=true;
			}

			if(compression_type!=PNG_COMPRESSION_TYPE.BASE)
			{
				Debug.WriteLine("Unknown compression method in IHDR");
				error=true;
			}

			// Accept filter_method 64 (intrapixel differencing) only if
			// 1.	Libpng did not read a PNG signature (this filter_method is only
			//		used in PNG datastreams that are embedded in MNG datastreams) and
			// 2.	The application called png_permit_mng_features with a mask that
			//		included PNG_FLAG_MNG_FILTER_64 and
			// 3.	The filter_method is 64 and
			// 4.	The color_type is RGB or RGBA
			if((mode&PNG_MODE.HAVE_PNG_SIGNATURE)==PNG_MODE.HAVE_PNG_SIGNATURE&&mng_features_permitted!=PNG_FLAG_MNG.None)
				Debug.WriteLine("MNG features are not allowed in a PNG datastream");

			if(filter_type!=PNG_FILTER_TYPE.BASE)
			{
				if(!((mng_features_permitted&PNG_FLAG_MNG.FILTER_64)==PNG_FLAG_MNG.FILTER_64&&
					(filter_type==PNG_FILTER_TYPE.INTRAPIXEL_DIFFERENCING)&&
					((mode&PNG_MODE.HAVE_PNG_SIGNATURE)!=PNG_MODE.HAVE_PNG_SIGNATURE)&&
					(color_type==PNG_COLOR_TYPE.RGB||color_type==PNG_COLOR_TYPE.RGB_ALPHA)))
				{
					Debug.WriteLine("Unknown filter method in IHDR");
					error=true;
				}

				if((mode&PNG_MODE.HAVE_PNG_SIGNATURE)==PNG_MODE.HAVE_PNG_SIGNATURE)
				{
					Debug.WriteLine("Invalid filter method in IHDR");
					error=true;
				}
			}

			if(error) throw new PNG_Exception("Invalid IHDR data");
		}

		// often/sometimes used temporary arrays
		byte[] buf4=new byte[4];
		byte[] buf4crc=new byte[4];
		byte[] extra=new byte[1];
	}
}
