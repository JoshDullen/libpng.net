// pngrutil.cs - utilities to read a PNG file
//
// Based on libpng version 1.4.3 - June 26, 2010
// This code is released under the libpng license.
// For conditions of distribution and use, see copyright notice in License.txt
// Copyright (C) 2007-2010 by the Authors
// Copyright (c) 1998-2010 Glenn Randers-Pehrson
// (Version 0.96 Copyright (c) 1996, 1997 Andreas Dilger)
// (Version 0.88 Copyright (c) 1995, 1996 Guy Eric Schalnat, Group 42, Inc.)
//
// This file contains routines that are only called from within
// libpng itself during the course of reading an image.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

using Free.Ports.zLib;

namespace Free.Ports.libpng
{
	public partial class png_struct
	{
		static CultureInfo nc=new CultureInfo("");

		#region png_get methods
		public static uint png_get_uint_31(byte[] buf)
		{
			uint i=png_get_uint_32(buf);
			if(i>PNG.UINT_31_MAX) throw new PNG_Exception("PNG uint out of range");
			return i;
		}

		public static uint png_get_uint_31(byte[] buf, uint start)
		{
			uint i=png_get_uint_32(buf, start);
			if(i>PNG.UINT_31_MAX) throw new PNG_Exception("PNG uint out of range");
			return i;
		}

		// Grab an 32-bit uint from a buffer in big-endian format.
		public static uint png_get_uint_32(byte[] buf)
		{
			return ((uint)buf[0]<<24)+((uint)buf[1]<<16)+((uint)buf[2]<<8)+(uint)buf[3];
		}

		public static uint png_get_uint_32(byte[] buf, uint start)
		{
			return ((uint)buf[start]<<24)+((uint)buf[start+1]<<16)+((uint)buf[start+2]<<8)+(uint)buf[start+3];
		}

		// Grab a signed 32-bit integer from a buffer in big-endian format. The
		// data is stored in the PNG file in two's complement format, and it is
		// assumed that the machine format for signed integers is the same.
		public static int png_get_int_32(byte[] buf)
		{
			return ((int)buf[0]<<24)+((int)buf[1]<<16)+((int)buf[2]<<8)+(int)buf[3];
		}

		public static int png_get_int_32(byte[] buf, uint start)
		{
			return ((int)buf[start]<<24)+((int)buf[start+1]<<16)+((int)buf[start+2]<<8)+(int)buf[start+3];
		}

		// Grab an 16-bit uint from a buffer in big-endian format.
		public static ushort png_get_uint_16(byte[] buf)
		{
			return (ushort)(((ushort)buf[0]<<8)+(ushort)buf[1]);
		}

		public static ushort png_get_uint_16(byte[] buf, uint start)
		{
			return (ushort)(((ushort)buf[start]<<8)+(ushort)buf[start+1]);
		}
		#endregion

		// Read the chunk header (length + type name).
		// Put the type name into png_ptr->chunk_name, and return the length.
		uint png_read_chunk_header()
		{
			byte[] buf=new byte[8];
			// Inform the I/O callback that the chunk header is being read.
			// PNG_IO_CHUNK_HDR requires a single I/O call.
			io_state=PNG_IO.READING|PNG_IO.CHUNK_HDR;

			// Read the length and the chunk name
			png_read_data(buf, 0, 8);
			uint length=png_get_uint_31(buf);

			// Put the chunk name into chunk_name
			Array.Copy(buf, 4, chunk_name, 0, 4);

			// Reset the crc and run it over the chunk name
			png_reset_crc();
			png_calculate_crc(chunk_name, 0, 4);

			// Check to see if chunk name is valid
			png_check_chunk_name(chunk_name);

			// Inform the I/O callback that chunk data will (possibly) be read.
			// PNG_IO_CHUNK_DATA does NOT require a specific number of I/O calls.
			io_state=PNG_IO.READING|PNG_IO.CHUNK_DATA;

			return length;
		}

		#region png_crc methods
		// Read data, and (optionally) run it through the CRC.
		void png_crc_read(byte[] buf, uint length)
		{
			png_read_data(buf, 0, length);
			png_calculate_crc(buf, 0, length);
		}

		void png_crc_read(byte[] buf, uint start, uint length)
		{
			png_read_data(buf, start, length);
			png_calculate_crc(buf, start, length);
		}

		// Optionally skip data and then check the CRC. Depending on whether we
		// are reading a ancillary or critical chunk, and how the program has set
		// things up, we may calculate the CRC on the data and print a message.
		// Returns '1' if there was a CRC error, '0' otherwise.
		bool png_crc_finish(uint skip)
		{
			uint i;
			uint istop=zbuf_size;
			for(i=skip; i>istop; i-=istop) png_crc_read(zbuf, zbuf_size);
			if(i!=0) png_crc_read(zbuf, i);

			if(png_crc_error())
			{
				if(((chunk_name[0]&0x20)==0x20&&(flags&PNG_FLAG.CRC_ANCILLARY_NOWARN)!=PNG_FLAG.CRC_ANCILLARY_NOWARN)||
					((chunk_name[0]&0x20)!=0x20&&(flags&PNG_FLAG.CRC_CRITICAL_USE)==PNG_FLAG.CRC_CRITICAL_USE)) png_chunk_warning(chunk_name, "CRC error");
				else png_chunk_error(chunk_name, "CRC error");
				return true;
			}

			return false;
		}

		// Compare the CRC stored in the PNG file with that calculated by libpng from
		// the data it has read thus far.
		bool png_crc_error()
		{
			bool need_crc=true;

			if((chunk_name[0]&0x20)==0x20) // ancillary
			{
				if((flags&PNG_FLAG.CRC_ANCILLARY_MASK)==PNG_FLAG.CRC_ANCILLARY_MASK) need_crc=false;
			}
			else // critical
			{
				if((flags&PNG_FLAG.CRC_CRITICAL_IGNORE)==PNG_FLAG.CRC_CRITICAL_IGNORE) need_crc=false;
			}

			// Inform the I/O callback that the chunk CRC is being read
			// PNG_IO_CHUNK_CRC requires the I/O to be done at once
			io_state=PNG_IO.READING|PNG_IO.CHUNK_CRC;

			png_read_data(buf4crc, 0, 4);

			if(need_crc) return png_get_uint_32(buf4crc)!=crc;
			return false;
		}
		#endregion

		uint png_inflate(byte[] data, uint data_offset, uint size, byte[] output, uint output_offset, uint output_size)
		{
			uint count=0;

			zstream.in_buf=data;
			zstream.next_in=data_offset;
			zstream.avail_in=size;

			for(; ; )
			{
				// Reset the output buffer each time round - we empty it
				// after every inflate call.
				zstream.out_buf=zbuf;
				zstream.next_out=0;
				zstream.avail_out=zbuf_size;

				int ret=zlib.inflate(zstream, zlib.Z_NO_FLUSH);
				uint avail=zbuf_size-zstream.avail_out;

				// First copy/count any new output - but only if we didn't
				// get an error code.
				if((ret==zlib.Z_OK||ret==zlib.Z_STREAM_END)&&avail>0)
				{
					if(output!=null&&output_size>count)
					{
						uint copy=output_size-count;
						if(avail<copy) copy=avail;
						Array.Copy(zbuf, 0, output, output_offset+count, copy);
					}
					count+=avail;
				}

				if(ret==zlib.Z_OK) continue;

				// Termination conditions - always reset the zstream, it
				// must be left in inflateInit state.
				zstream.avail_in=0;
				zlib.inflateReset(zstream);

				if(ret==zlib.Z_STREAM_END) return count; // NOTE: may be zero.

				// Now handle the error codes - the API always returns 0
				// and the error message is dumped into the uncompressed
				// buffer if available.
				string msg;
				if(zstream.msg!=null&&zstream.msg.Length!=0) msg=zstream.msg;
				else
				{
					switch(ret)
					{
						case zlib.Z_BUF_ERROR: msg="Buffer error in compressed datastream in {0} chunk"; break;
						case zlib.Z_DATA_ERROR: msg="Data error in compressed datastream in {0} chunk"; break;
						default: msg="Incomplete compressed datastream in {0} chunk"; break;
					}

					msg=string.Format(msg, chunk_name);
				}

				Debug.WriteLine(msg);

				// 0 means an error - notice that this code simple ignores
				// zero length compressed chunks as a result.
				return 0;
			}
		}

		#region png_decompress_chunk
		// Decompress trailing data in a chunk. The assumption is that chunkdata
		// points at an allocated area holding the contents of a chunk with a
		// trailing compressed part. What we get back is an allocated area
		// holding the original prefix part and an uncompressed version of the
		// trailing part (the malloc area passed in is freed).
		byte[] png_decompress_chunk(PNG_COMPRESSION_TYPE comp_type, uint chunklength, uint prefix_size, ref uint newlength)
		{
			// The caller should guarantee this
			if(prefix_size>chunklength)
			{
				// The recovery is to delete the chunk.
				Debug.WriteLine("invalid chunklength");
				prefix_size=0; // To delete everything
			}
			else if(comp_type==PNG_COMPRESSION_TYPE.BASE)
			{
				uint expanded_size=png_inflate(chunkdata, prefix_size, chunklength-prefix_size, null, 0, 0);

				if(expanded_size>0)
				{
					// Success (maybe) - really uncompress the chunk.
					try
					{
						byte[] text=new byte[prefix_size+expanded_size+1];

						Array.Copy(chunkdata, text, prefix_size);
						uint new_size=png_inflate(chunkdata, prefix_size, chunklength-prefix_size, text, prefix_size, expanded_size);
						text[prefix_size+expanded_size]=0; // just in case

						if(new_size==expanded_size)
						{
							chunkdata=text;
							newlength=prefix_size+expanded_size;
							return chunkdata; // The success return!
						}

						Debug.WriteLine("png_inflate logic error");
					}
					catch
					{
						Debug.WriteLine("Not enough memory to decompress chunk");
					}
				}
			}
			else
			{
				Debug.WriteLine("Unknown zTXt compression type "+comp_type);

				// The recovery is to simply drop the data.
			}

			// Generic error return - leave the prefix, delete the compressed
			// data, reallocate the chunkdata to remove the potentially large
			// amount of compressed data.
			try
			{
				byte[] text=new byte[prefix_size+1];
				if(prefix_size>0) Array.Copy(chunkdata, text, prefix_size);
				chunkdata=text;

				// This is an extra zero in the 'uncompressed' part.
				chunkdata[prefix_size]=0x00;
			}
			catch
			{
			}

			// Ignore a malloc error here - it is safe.
			newlength=prefix_size;

			return chunkdata;
		}
		#endregion

		#region png_handle methods
		// read and check the IDHR chunk
		void png_handle_IHDR(uint length)
		{
			if((mode&PNG_MODE.HAVE_IHDR)==PNG_MODE.HAVE_IHDR) throw new PNG_Exception("Out of place IHDR");

			// check the length
			if(length!=13) throw new PNG_Exception("Invalid IHDR chunk");

			mode|=PNG_MODE.HAVE_IHDR;

			byte[] buf=new byte[13];
			png_crc_read(buf, 13);
			png_crc_finish(0);

			uint width=png_get_uint_31(buf);
			uint height=png_get_uint_31(buf, 4);
			byte bit_depth=buf[8];
			PNG_COLOR_TYPE color_type=(PNG_COLOR_TYPE)buf[9];
			PNG_COMPRESSION_TYPE compression_type=(PNG_COMPRESSION_TYPE)buf[10];
			PNG_FILTER_TYPE filter_type=(PNG_FILTER_TYPE)buf[11];
			PNG_INTERLACE interlace_type=(PNG_INTERLACE)buf[12];

			// Set internal variables
			this.width = width;
			this.height = height;
			this.bit_depth = bit_depth;
			this.interlaced = interlace_type;
			this.color_type = color_type;
			this.filter_type = filter_type;
			this.compression_type=compression_type;

			// find number of channels
			switch(color_type)
			{
				case PNG_COLOR_TYPE.GRAY:
				case PNG_COLOR_TYPE.PALETTE: channels=1; break;
				case PNG_COLOR_TYPE.RGB: channels=3; break;
				case PNG_COLOR_TYPE.GRAY_ALPHA: channels=2; break;
				case PNG_COLOR_TYPE.RGB_ALPHA: channels=4; break;
			}

			pixel_depth=(byte)(bit_depth*channels);
			rowbytes=PNG_ROWBYTES(pixel_depth, width);
			png_set_IHDR(width, height, bit_depth, color_type, interlace_type, compression_type, filter_type);
		}

		// read and check the palette
		void png_handle_PLTE(uint length)
		{
			if((mode&PNG_MODE.HAVE_IHDR)!=PNG_MODE.HAVE_IHDR) throw new PNG_Exception("Missing IHDR before PLTE");
			else if((mode&PNG_MODE.HAVE_IDAT)==PNG_MODE.HAVE_IDAT)
			{
				Debug.WriteLine("Invalid PLTE after IDAT");
				png_crc_finish(length);
				return;
			}
			else if((mode&PNG_MODE.HAVE_PLTE)==PNG_MODE.HAVE_PLTE) throw new PNG_Exception("Duplicate PLTE chunk");

			mode|=PNG_MODE.HAVE_PLTE;

			if((color_type&PNG_COLOR_TYPE.COLOR_MASK)!=PNG_COLOR_TYPE.COLOR_MASK)
			{
				Debug.WriteLine("Ignoring PLTE chunk in grayscale PNG");
				png_crc_finish(length);
				return;
			}

			png_color[] palette=new png_color[PNG.MAX_PALETTE_LENGTH];

			if((length>3*PNG.MAX_PALETTE_LENGTH)||length%3!=0)
			{
				if(color_type!=PNG_COLOR_TYPE.PALETTE)
				{
					Debug.WriteLine("Invalid palette chunk");
					png_crc_finish(length);
					return;
				}
				else throw new PNG_Exception("Invalid palette chunk");
			}

			ushort num=(ushort)(length/3);

			for(ushort i=0; i<num; i++)
			{
				png_crc_read(buf4, 3);
				palette[i].red=buf4[0];
				palette[i].green=buf4[1];
				palette[i].blue=buf4[2];
			}

			// If we actually NEED the PLTE chunk (ie for a paletted image), we do
			// whatever the normal CRC configuration tells us. However, if we
			// have an RGB image, the PLTE can be considered ancillary, so
			// we will act as though it is.
			png_crc_finish(0);

			png_set_PLTE(palette);

			if(color_type==PNG_COLOR_TYPE.PALETTE)
			{
				if((info_ptr_valid&PNG_INFO.tRNS)==PNG_INFO.tRNS)
				{
					if(num_trans>num)
					{
						Debug.WriteLine("Truncating incorrect tRNS chunk length");
						byte[] newtrans=new byte[num];
						Array.Copy(trans_alpha, newtrans, num);
						trans_alpha=newtrans;
						num_trans=num;
					}
					if(info_ptr_num_trans>num)
					{
						Debug.WriteLine("Truncating incorrect info tRNS chunk length");
						byte[] newtrans=new byte[num];
						Array.Copy(trans_alpha, newtrans, num);
						trans_alpha=newtrans;
						info_ptr_num_trans=num;
					}
				}
			}
		}

		void png_handle_IEND(uint length)
		{
			if((mode&PNG_MODE.HAVE_IHDR)!=PNG_MODE.HAVE_IHDR||(mode&PNG_MODE.HAVE_IDAT)!=PNG_MODE.HAVE_IDAT) throw new PNG_Exception("No image in file");

			mode|=(PNG_MODE.AFTER_IDAT|PNG_MODE.HAVE_IEND);

			if(length!=0) Debug.WriteLine("Incorrect IEND chunk length");
			png_crc_finish(length);
		}

		void png_handle_gAMA(uint length)
		{
			if((mode&PNG_MODE.HAVE_IHDR)!=PNG_MODE.HAVE_IHDR) throw new PNG_Exception("Missing IHDR before gAMA");
			else if((mode&PNG_MODE.HAVE_IDAT)==PNG_MODE.HAVE_IDAT)
			{
				Debug.WriteLine("Invalid gAMA after IDAT");
				png_crc_finish(length);
				return;
			}
			else if((mode&PNG_MODE.HAVE_PLTE)==PNG_MODE.HAVE_PLTE) Debug.WriteLine("Out of place gAMA chunk"); // Should be an error, but we can cope with it

			if((info_ptr_valid&PNG_INFO.gAMA)==PNG_INFO.gAMA&&(info_ptr_valid&PNG_INFO.sRGB)!=PNG_INFO.sRGB)
			{
				Debug.WriteLine("Duplicate gAMA chunk");
				png_crc_finish(length);
				return;
			}

			if(length!=4)
			{
				Debug.WriteLine("Incorrect gAMA chunk length");
				png_crc_finish(length);
				return;
			}

			png_crc_read(buf4, 4);
			if(png_crc_finish(0)) return;

			uint igamma=png_get_uint_32(buf4);
			if(igamma==0)
			{
				Debug.WriteLine("Ignoring gAMA chunk with gamma=0");
				return;
			}

			if((info_ptr_valid&PNG_INFO.sRGB)==PNG_INFO.sRGB)
			{
				if(PNG_OUT_OF_RANGE(igamma, 45500, 500))
				{
					Debug.WriteLine("Ignoring incorrect gAMA value when sRGB is also present");
					return;
				}
			}

			png_set_gAMA_fixed(igamma);
		}

		void png_handle_sBIT(uint length)
		{
			if((mode&PNG_MODE.HAVE_IHDR)!=PNG_MODE.HAVE_IHDR) throw new PNG_Exception("Missing IHDR before sBIT");
			else if((mode&PNG_MODE.HAVE_IDAT)==PNG_MODE.HAVE_IDAT)
			{
				Debug.WriteLine("Invalid sBIT after IDAT");
				png_crc_finish(length);
				return;
			}
			else if((mode&PNG_MODE.HAVE_PLTE)==PNG_MODE.HAVE_PLTE)
			{
				// Should be an error, but we can cope with it
				Debug.WriteLine("Out of place sBIT chunk");
			}
			if((info_ptr_valid&PNG_INFO.sBIT)==PNG_INFO.sBIT)
			{
				Debug.WriteLine("Duplicate sBIT chunk");
				png_crc_finish(length);
				return;
			}

			uint truelen=channels;
			if(color_type==PNG_COLOR_TYPE.PALETTE) truelen=3;

			if(length!=truelen||length>4)
			{
				Debug.WriteLine("Incorrect sBIT chunk length");
				png_crc_finish(length);
				return;
			}

			byte[] buf=new byte[truelen];
			png_crc_read(buf, truelen);
			if(png_crc_finish(0)) return;

			if((color_type&PNG_COLOR_TYPE.COLOR_MASK)!=PNG_COLOR_TYPE.COLOR_MASK)
			{
				sig_bit.red=buf[0];
				sig_bit.green=buf[1];
				sig_bit.blue=buf[2];
				sig_bit.alpha=buf[3];
				sig_bit.gray=0;
			}
			else
			{
				sig_bit.gray=buf[0];
				sig_bit.red=buf[0];
				sig_bit.green=buf[0];
				sig_bit.blue=buf[0];
				sig_bit.alpha=buf[1];
			}
			png_set_sBIT(sig_bit);
		}

		void png_handle_cHRM(uint length)
		{
			if((mode&PNG_MODE.HAVE_IHDR)!=PNG_MODE.HAVE_IHDR) throw new PNG_Exception("Missing IHDR before cHRM");
			else if((mode&PNG_MODE.HAVE_IDAT)==PNG_MODE.HAVE_IDAT)
			{
				Debug.WriteLine("Invalid cHRM after IDAT");
				png_crc_finish(length);
				return;
			}
			else if((mode&PNG_MODE.HAVE_PLTE)==PNG_MODE.HAVE_PLTE) Debug.WriteLine("Missing PLTE before cHRM"); // Should be an error, but we can cope with it

			if((info_ptr_valid&PNG_INFO.cHRM)==PNG_INFO.cHRM&&(info_ptr_valid&PNG_INFO.sRGB)!=PNG_INFO.sRGB)
			{
				Debug.WriteLine("Duplicate cHRM chunk");
				png_crc_finish(length);
				return;
			}

			if(length!=32)
			{
				Debug.WriteLine("Incorrect cHRM chunk length");
				png_crc_finish(length);
				return;
			}

			byte[] buf=new byte[32];

			png_crc_read(buf, 32);
			if(png_crc_finish(0)) return;

			uint x_white=png_get_uint_32(buf);
			uint y_white=png_get_uint_32(buf, 4);
			uint x_red=png_get_uint_32(buf, 8);
			uint y_red=png_get_uint_32(buf, 12);
			uint x_green=png_get_uint_32(buf, 16);
			uint y_green=png_get_uint_32(buf, 20);
			uint x_blue=png_get_uint_32(buf, 24);
			uint y_blue=png_get_uint_32(buf, 28);

			double white_x=x_white/100000.0;
			double white_y=y_white/100000.0;
			double red_x=x_red/100000.0;
			double red_y=y_red/100000.0;
			double green_x=x_green/100000.0;
			double green_y=y_green/100000.0;
			double blue_x=x_blue/100000.0;
			double blue_y=y_blue/100000.0;

			if((info_ptr_valid&PNG_INFO.sRGB)==PNG_INFO.sRGB)
			{
				if(PNG_OUT_OF_RANGE(x_white, 31270, 1000)||PNG_OUT_OF_RANGE(y_white, 32900, 1000)||
					PNG_OUT_OF_RANGE(x_red, 64000, 1000)||PNG_OUT_OF_RANGE(y_red, 33000, 1000)||
					PNG_OUT_OF_RANGE(x_green, 30000, 1000)||PNG_OUT_OF_RANGE(y_green, 60000, 1000)||
					PNG_OUT_OF_RANGE(x_blue, 15000, 1000)||PNG_OUT_OF_RANGE(y_blue, 6000, 1000)) Debug.WriteLine("Ignoring incorrect cHRM value when sRGB is also present");
				return;
			}

			png_set_cHRM(white_x, white_y, red_x, red_y, green_x, green_y, blue_x, blue_y);
		}

		void png_handle_sRGB(uint length)
		{
			if((mode&PNG_MODE.HAVE_IHDR)!=PNG_MODE.HAVE_IHDR) throw new PNG_Exception("Missing IHDR before sRGB");
			else if((mode&PNG_MODE.HAVE_IDAT)==PNG_MODE.HAVE_IDAT)
			{
				Debug.WriteLine("Invalid sRGB after IDAT");
				png_crc_finish(length);
				return;
			}
			else if((mode&PNG_MODE.HAVE_PLTE)==PNG_MODE.HAVE_PLTE) Debug.WriteLine("Out of place sRGB chunk"); // Should be an error, but we can cope with it

			if((info_ptr_valid&PNG_INFO.sRGB)==PNG_INFO.sRGB)
			{
				Debug.WriteLine("Duplicate sRGB chunk");
				png_crc_finish(length);
				return;
			}

			if(length!=1)
			{
				Debug.WriteLine("Incorrect sRGB chunk length");
				png_crc_finish(length);
				return;
			}

			png_crc_read(buf4, 1);
			if(png_crc_finish(0)) return;

			PNG_sRGB_INTENT intent=(PNG_sRGB_INTENT)buf4[0];
			// check for bad intent
			if(intent>=PNG_sRGB_INTENT.LAST)
			{
				Debug.WriteLine("Unknown sRGB intent");
				return;
			}

			if((info_ptr_valid&PNG_INFO.gAMA)==PNG_INFO.gAMA)
			{
				uint igamma=(uint)(gamma*100000.0);
				if(PNG_OUT_OF_RANGE(igamma, 45500, 500)) Debug.WriteLine("Ignoring incorrect gAMA value when sRGB is also present");
			}

			png_set_sRGB_gAMA_and_cHRM(intent);
		}

		void png_handle_iCCP(uint length)
		{
			if((mode&PNG_MODE.HAVE_IHDR)!=PNG_MODE.HAVE_IHDR) throw new PNG_Exception("Missing IHDR before iCCP");
			else if((mode&PNG_MODE.HAVE_IDAT)==PNG_MODE.HAVE_IDAT)
			{
				Debug.WriteLine("Invalid iCCP after IDAT");
				png_crc_finish(length);
				return;
			}
			else if((mode&PNG_MODE.HAVE_PLTE)==PNG_MODE.HAVE_PLTE) Debug.WriteLine("Out of place iCCP chunk"); // Should be an error, but we can cope with it

			if((info_ptr_valid&PNG_INFO.iCCP)==PNG_INFO.iCCP)
			{
				Debug.WriteLine("Duplicate iCCP chunk");
				png_crc_finish(length);
				return;
			}

			try
			{
				chunkdata=new byte[length+1];
			}
			catch(Exception)
			{
				Debug.WriteLine("No memory for iCCP");
				png_crc_finish(length);
				return;
			}

			png_crc_read(chunkdata, length);
			if(png_crc_finish(0))
			{
				chunkdata=null;
				return;
			}

			chunkdata[length]=0x00;

			uint profile=0;
			while(chunkdata[profile]!=0x00) profile++; // empty loop to find end of name

			if(profile==0)
			{
				chunkdata=null;
				Debug.WriteLine("Malformed iCCP chunk");
				return;
			}
			profile++;

			// there should be at least one zero (the compression type byte)
			// following the separator, and we should be on it
			if(profile>=length-1)
			{
				chunkdata=null;
				Debug.WriteLine("Malformed iCCP chunk");
				return;
			}

			string profile_name=Encoding.ASCII.GetString(chunkdata, 0, (int)profile-1);

			// compression_type should always be zero
			byte compression_type=chunkdata[profile++];
			if(compression_type!=0x00)
			{
				Debug.WriteLine("Ignoring nonzero compression type in iCCP chunk");
				compression_type=0x00; // Reset it to zero (libpng-1.0.6 through 1.0.8 wrote nonzero)
			}

			byte[] profile_data=new byte[length-profile];
			Array.Copy(chunkdata, profile, profile_data, 0, profile_data.Length);
			chunkdata=profile_data;

			uint data_length=0;
			profile_data=png_decompress_chunk(PNG_COMPRESSION_TYPE.BASE, (uint)profile_data.Length, 0, ref data_length);

			if(data_length<4)
			{
				chunkdata=profile_data=null;
				Debug.WriteLine("Profile size field missing from iCCP chunk");
				return;
			}

			// Check the profile_size recorded in the first 32 bits of the ICC profile
			uint profile_size=png_get_uint_32(profile_data);
			uint profile_length=(uint)profile_data.Length;

			if(profile_size<profile_length) profile_length=profile_size;
			else if(profile_size>profile_length)
			{
				chunkdata=profile_data=null;
				Debug.WriteLine("Ignoring truncated iCCP profile");
				Debug.WriteLine(string.Format("declared profile size = {0}", profile_size));
				Debug.WriteLine(string.Format("declared profile length = {0}", profile_length));
				return;
			}

			png_set_iCCP(profile_name, compression_type, profile_data);
			chunkdata=null;
		}

		void png_handle_sPLT(uint length)
		{
			if((mode&PNG_MODE.HAVE_IHDR)!=PNG_MODE.HAVE_IHDR) throw new PNG_Exception("Missing IHDR before sPLT");
			else if((mode&PNG_MODE.HAVE_IDAT)==PNG_MODE.HAVE_IDAT)
			{
				Debug.WriteLine("Invalid sPLT after IDAT");
				png_crc_finish(length);
				return;
			}

			try
			{
				chunkdata=new byte[length+1];
			}
			catch(Exception)
			{
				Debug.WriteLine("No memory for sPLT");
				png_crc_finish(length);
				return;
			}

			png_crc_read(chunkdata, length);
			if(png_crc_finish(0))
			{
				chunkdata=null;
				return;
			}

			chunkdata[length]=0x00;

			uint entry_start=0;
			while(chunkdata[entry_start]!=0x00) entry_start++; // empty loop to find end of name

			if(entry_start==0)
			{
				chunkdata=null;
				Debug.WriteLine("Malformed sPLT chunk");
				return;
			}
			entry_start++;

			// a sample depth should follow the separator, and we should be on it
			if(entry_start>=length-1)
			{
				chunkdata=null;
				Debug.WriteLine("Malformed sPLT chunk");
				return;
			}

			png_sPLT_t new_palette;

			// discard all chunk data except the name and stash that
			new_palette.name=Encoding.ASCII.GetString(chunkdata, 0, (int)entry_start-1);

			new_palette.depth=chunkdata[entry_start++];
			uint entry_size=(uint)(new_palette.depth==8?6:10);
			uint data_length=length-entry_start;

			// integrity-check the data length
			if((data_length%entry_size)!=0)
			{
				chunkdata=null;
				Debug.WriteLine("sPLT chunk has bad length");
				return;
			}

			new_palette.nentries=data_length/entry_size;
			if(new_palette.nentries>(PNG.SIZE_MAX/10))
			{
				chunkdata=null;
				Debug.WriteLine("sPLT chunk too long");
				return;
			}

			try
			{
				new_palette.entries=new png_sPLT_entry[new_palette.nentries];
			}
			catch(Exception)
			{
				chunkdata=null;
				Debug.WriteLine("sPLT chunk requires too much memory");
				return;
			}

			for(uint i=0; i<new_palette.nentries; i++)
			{
				png_sPLT_entry pp;

				if(new_palette.depth==8)
				{
					pp.red=chunkdata[entry_start++];
					pp.green=chunkdata[entry_start++];
					pp.blue=chunkdata[entry_start++];
					pp.alpha=chunkdata[entry_start++];
				}
				else
				{
					pp.red=png_get_uint_16(chunkdata, entry_start); entry_start+=2;
					pp.green=png_get_uint_16(chunkdata, entry_start); entry_start+=2;
					pp.blue=png_get_uint_16(chunkdata, entry_start); entry_start+=2;
					pp.alpha=png_get_uint_16(chunkdata, entry_start); entry_start+=2;
				}
				pp.frequency=png_get_uint_16(chunkdata, entry_start); entry_start+=2;
				new_palette.entries[i]=pp;
			}

			png_set_sPLT(new_palette);
			chunkdata=null;
		}

		void png_handle_tRNS(uint length)
		{
			if((mode&PNG_MODE.HAVE_IHDR)!=PNG_MODE.HAVE_IHDR) throw new PNG_Exception("Missing IHDR before tRNS");
			else if((mode&PNG_MODE.HAVE_IDAT)==PNG_MODE.HAVE_IDAT)
			{
				Debug.WriteLine("Invalid tRNS after IDAT");
				png_crc_finish(length);
				return;
			}
			else if((info_ptr_valid&PNG_INFO.tRNS)==PNG_INFO.tRNS)
			{
				Debug.WriteLine("Duplicate tRNS chunk");
				png_crc_finish(length);
				return;
			}

			trans_color=new png_color_16();

			if(color_type==PNG_COLOR_TYPE.GRAY)
			{
				if(length!=2)
				{
					Debug.WriteLine("Incorrect tRNS chunk length");
					png_crc_finish(length);
					return;
				}

				png_crc_read(buf4, 2);
				if(png_crc_finish(0)) return;

				num_trans=1;
				trans_color.gray=png_get_uint_16(buf4);
				png_set_tRNS(trans_color);
			}
			else if(color_type==PNG_COLOR_TYPE.RGB)
			{
				if(length!=6)
				{
					Debug.WriteLine("Incorrect tRNS chunk length");
					png_crc_finish(length);
					return;
				}

				byte[] buf=new byte[6];
				png_crc_read(buf, 6);
				if(png_crc_finish(0)) return;

				num_trans=1;
				trans_color.red=png_get_uint_16(buf);
				trans_color.green=png_get_uint_16(buf, 2);
				trans_color.blue=png_get_uint_16(buf, 4);
				png_set_tRNS(trans_color);
			}
			else if(color_type==PNG_COLOR_TYPE.PALETTE)
			{
				if(!((mode&PNG_MODE.HAVE_PLTE)==PNG_MODE.HAVE_PLTE)) Debug.WriteLine("Missing PLTE before tRNS"); // Should be an error, but we can cope with it.

				if(length>palette.Length||length>PNG.MAX_PALETTE_LENGTH)
				{
					Debug.WriteLine("Incorrect tRNS chunk length");
					png_crc_finish(length);
					return;
				}
				if(length==0)
				{
					Debug.WriteLine("Zero length tRNS chunk");
					png_crc_finish(length);
					return;
				}

				byte[] readbuf=new byte[length];
				png_crc_read(readbuf, length);
				if(png_crc_finish(0)) return;

				num_trans=(ushort)length;
				png_set_tRNS(readbuf);
			}
			else
			{
				Debug.WriteLine("tRNS chunk not allowed with alpha channel");
				png_crc_finish(length);
				num_trans=0;
			}
		}

		void png_handle_bKGD(uint length)
		{
			if((mode&PNG_MODE.HAVE_IHDR)!=PNG_MODE.HAVE_IHDR) throw new PNG_Exception("Missing IHDR before bKGD");
			else if((mode&PNG_MODE.HAVE_IDAT)==PNG_MODE.HAVE_IDAT)
			{
				Debug.WriteLine("Invalid bKGD after IDAT");
				png_crc_finish(length);
				return;
			}
			else if(color_type==PNG_COLOR_TYPE.PALETTE&&!((mode&PNG_MODE.HAVE_PLTE)==PNG_MODE.HAVE_PLTE))
			{
				Debug.WriteLine("Missing PLTE before bKGD");
				png_crc_finish(length);
				return;
			}
			else if((info_ptr_valid&PNG_INFO.bKGD)==PNG_INFO.bKGD)
			{
				Debug.WriteLine("Duplicate bKGD chunk");
				png_crc_finish(length);
				return;
			}

			uint truelen=2;
			if(color_type==PNG_COLOR_TYPE.PALETTE) truelen=1;
			else if((color_type&PNG_COLOR_TYPE.COLOR_MASK)==PNG_COLOR_TYPE.COLOR_MASK) truelen=6;

			if(length!=truelen)
			{
				Debug.WriteLine("Incorrect bKGD chunk length");
				png_crc_finish(length);
				return;
			}

			byte[] buf=new byte[truelen];
			png_crc_read(buf, truelen);
			if(png_crc_finish(0)) return;

			png_color_16 background=new png_color_16();

			// We convert the index value into RGB components so that we can allow
			// arbitrary RGB values for background when we have transparency, and
			// so it is easy to determine the RGB values of the background color
			// from the info_ptr struct.
			if(color_type==PNG_COLOR_TYPE.PALETTE)
			{
				background.index=buf[0];
				if(info_ptr_palette.Length!=0)
				{
					if(buf[0]>info_ptr_palette.Length)
					{
						Debug.WriteLine("Incorrect bKGD chunk index value");
						return;
					}
					background.red=(ushort)palette[buf[0]].red;
					background.green=(ushort)palette[buf[0]].green;
					background.blue=(ushort)palette[buf[0]].blue;
				}
			}
			else if((color_type&PNG_COLOR_TYPE.COLOR_MASK)!=PNG_COLOR_TYPE.COLOR_MASK)
			{
				background.red=background.green=background.blue=background.gray=png_get_uint_16(buf);
			}
			else
			{
				background.red=png_get_uint_16(buf);
				background.green=png_get_uint_16(buf, 2);
				background.blue=png_get_uint_16(buf, 4);
			}

			png_set_bKGD(background);
		}

		void png_handle_hIST(uint length)
		{
			if((mode&PNG_MODE.HAVE_IHDR)!=PNG_MODE.HAVE_IHDR) throw new PNG_Exception("Missing IHDR before hIST");
			else if((mode&PNG_MODE.HAVE_IDAT)==PNG_MODE.HAVE_IDAT)
			{
				Debug.WriteLine("Invalid hIST after IDAT");
				png_crc_finish(length);
				return;
			}
			else if(!((mode&PNG_MODE.HAVE_PLTE)==PNG_MODE.HAVE_PLTE))
			{
				Debug.WriteLine("Missing PLTE before hIST");
				png_crc_finish(length);
				return;
			}
			else if((info_ptr_valid&PNG_INFO.hIST)==PNG_INFO.hIST)
			{
				Debug.WriteLine("Duplicate hIST chunk");
				png_crc_finish(length);
				return;
			}

			uint num=length/2;
			if(length%2!=0||num!=palette.Length||num>PNG.MAX_PALETTE_LENGTH)
			{
				Debug.WriteLine("Incorrect hIST chunk length");
				png_crc_finish(length);
				return;
			}

			byte[] buf=new byte[length];
			png_crc_read(buf, length);
			if(png_crc_finish(0)) return;

			ushort[] readbuf=new ushort[num];
			for(uint i=0; i<num; i++) readbuf[i]=png_get_uint_16(buf, i*2);

			png_set_hIST(readbuf);
		}

		void png_handle_pHYs(uint length)
		{
			if((mode&PNG_MODE.HAVE_IHDR)!=PNG_MODE.HAVE_IHDR) throw new PNG_Exception("Missing IHDR before pHYs");
			else if((mode&PNG_MODE.HAVE_IDAT)==PNG_MODE.HAVE_IDAT)
			{
				Debug.WriteLine("Invalid pHYs after IDAT");
				png_crc_finish(length);
				return;
			}
			else if((info_ptr_valid&PNG_INFO.pHYs)==PNG_INFO.pHYs)
			{
				Debug.WriteLine("Duplicate pHYs chunk");
				png_crc_finish(length);
				return;
			}

			if(length!=9)
			{
				Debug.WriteLine("Incorrect pHYs chunk length");
				png_crc_finish(length);
				return;
			}

			byte[] buf=new byte[9];
			png_crc_read(buf, 9);
			if(png_crc_finish(0)) return;

			uint res_x=png_get_uint_32(buf);
			uint res_y=png_get_uint_32(buf, 4);
			PNG_RESOLUTION unit_type=(PNG_RESOLUTION)buf[8];

			png_set_pHYs(res_x, res_y, unit_type);
		}

		void png_handle_oFFs(uint length)
		{
			if((mode&PNG_MODE.HAVE_IHDR)!=PNG_MODE.HAVE_IHDR) throw new PNG_Exception("Missing IHDR before oFFs");
			else if((mode&PNG_MODE.HAVE_IDAT)==PNG_MODE.HAVE_IDAT)
			{
				Debug.WriteLine("Invalid oFFs after IDAT");
				png_crc_finish(length);
				return;
			}
			else if((info_ptr_valid&PNG_INFO.oFFs)==PNG_INFO.oFFs)
			{
				Debug.WriteLine("Duplicate oFFs chunk");
				png_crc_finish(length);
				return;
			}

			if(length!=9)
			{
				Debug.WriteLine("Incorrect oFFs chunk length");
				png_crc_finish(length);
				return;
			}

			byte[] buf=new byte[9];
			png_crc_read(buf, 9);
			if(png_crc_finish(0)) return;

			int offset_x=png_get_int_32(buf);
			int offset_y=png_get_int_32(buf, 4);
			PNG_OFFSET unit_type=(PNG_OFFSET)buf[8];

			png_set_oFFs(offset_x, offset_y, unit_type);
		}

		// read the pCAL chunk (described in the PNG Extensions document)
		void png_handle_pCAL(uint length)
		{
			if((mode&PNG_MODE.HAVE_IHDR)!=PNG_MODE.HAVE_IHDR) throw new PNG_Exception("Missing IHDR before pCAL");
			else if((mode&PNG_MODE.HAVE_IDAT)==PNG_MODE.HAVE_IDAT)
			{
				Debug.WriteLine("Invalid pCAL after IDAT");
				png_crc_finish(length);
				return;
			}
			else if((info_ptr_valid&PNG_INFO.pCAL)==PNG_INFO.pCAL)
			{
				Debug.WriteLine("Duplicate pCAL chunk");
				png_crc_finish(length);
				return;
			}

			try
			{
				chunkdata=new byte[length+1];
			}
			catch(Exception)
			{
				Debug.WriteLine("No memory for pCAL purpose");
				png_crc_finish(length);
				return;
			}

			png_crc_read(chunkdata, length);
			if(png_crc_finish(0))
			{
				chunkdata=null;
				return;
			}

			chunkdata[length]=0x00; // null terminate the last string

			uint buf=0;
			while(chunkdata[buf]!=0x00) buf++; // empty loop

			// We need to have at least 12 bytes after the purpose string
			// in order to get the parameter information.
			if(length<=buf+12)
			{
				chunkdata=null;
				Debug.WriteLine("Invalid pCAL data");
				return;
			}

			string purpose=Encoding.ASCII.GetString(chunkdata, 0, (int)buf);

			int X0=png_get_int_32(chunkdata, buf+1);
			int X1=png_get_int_32(chunkdata, buf+5);
			PNG_EQUATION type=(PNG_EQUATION)chunkdata[buf+9];
			byte nparams=chunkdata[buf+10];

			// Check that we have the right number of parameters for known equation types.
			if((type==PNG_EQUATION.LINEAR&&nparams!=2)||(type==PNG_EQUATION.BASE_E&&nparams!=3)||
				(type==PNG_EQUATION.ARBITRARY&&nparams!=3)||(type==PNG_EQUATION.HYPERBOLIC&&nparams!=4))
			{
				chunkdata=null;
				Debug.WriteLine("Invalid pCAL parameters for equation type");
				return;
			}
			else if(type>=PNG_EQUATION.LAST) Debug.WriteLine("Unrecognized equation type for pCAL chunk");

			uint units=buf+11;
			buf=units;
			while(chunkdata[buf]!=0x00) buf++; // Empty loop to move past the units string.

			string sUnits=Encoding.ASCII.GetString(chunkdata, (int)units, (int)(buf-units));

			string[] @params;

			try
			{
				@params=new string[nparams];
			}
			catch(Exception)
			{
				chunkdata=null;
				Debug.WriteLine("No memory for pCAL params");
				return;
			}

			// Get pointers to the start of each parameter string.
			for(uint i=0; i<nparams; i++)
			{
				buf++; // Skip the null string terminator from previous parameter.

				uint stringstart=buf;
				while(chunkdata[buf]!=0x00) buf++; // Empty loop to move past each parameter string

				@params[i]=Encoding.ASCII.GetString(chunkdata, (int)stringstart, (int)(buf-stringstart));

				// Make sure we haven't run out of data yet
				if(buf>length)
				{
					chunkdata=null;
					Debug.WriteLine("Invalid pCAL data");
					return;
				}
			}

			png_set_pCAL(purpose, X0, X1, type, sUnits, @params);
			chunkdata=null;
		}

		// read the sCAL chunk 
		void png_handle_sCAL(uint length)
		{
			if((mode&PNG_MODE.HAVE_IHDR)!=PNG_MODE.HAVE_IHDR) throw new PNG_Exception("Missing IHDR before sCAL");
			else if((mode&PNG_MODE.HAVE_IDAT)==PNG_MODE.HAVE_IDAT)
			{
				Debug.WriteLine("Invalid sCAL after IDAT");
				png_crc_finish(length);
				return;
			}
			else if((info_ptr_valid&PNG_INFO.sCAL)==PNG_INFO.sCAL)
			{
				Debug.WriteLine("Duplicate sCAL chunk");
				png_crc_finish(length);
				return;
			}

			try
			{
				chunkdata=new byte[length+1];
			}
			catch(Exception)
			{
				Debug.WriteLine("Out of memory while processing sCAL chunk");
				png_crc_finish(length);
				return;
			}

			png_crc_read(chunkdata, length);
			if(png_crc_finish(0))
			{
				chunkdata=null;
				return;
			}

			chunkdata[length]=0x00; // null terminate the last string

			uint ep=1; // skip unit byte
			while(chunkdata[ep]!=0x00) ep++; // empty loop
			ep++; // ship 0x00
			uint hStart=ep;
			while(chunkdata[ep]!=0x00) ep++; // empty loop
			if(ep>length)
			{
				chunkdata=null;
				Debug.WriteLine("Truncated sCAL chunk");
				return;
			}

			string strWidth=Encoding.ASCII.GetString(chunkdata, (int)1, (int)hStart-2);
			string strHeight=Encoding.ASCII.GetString(chunkdata, (int)hStart, (int)(ep-hStart));

			double width, height;

			try
			{
				width=double.Parse(strWidth, nc);
				height=double.Parse(strHeight, nc);
			}
			catch(Exception)
			{
				chunkdata=null;
				Debug.WriteLine("malformed height string in sCAL chunk");
				return;
			}

			if(width<=0.0||height<=0.0)
			{
				chunkdata=null;
				Debug.WriteLine("Invalid sCAL data");
				return;
			}

			png_set_sCAL((PNG_SCALE)chunkdata[0], width, height);
			chunkdata=null;
		}

		void png_handle_tIME(uint length)
		{
			if((mode&PNG_MODE.HAVE_IHDR)!=PNG_MODE.HAVE_IHDR) throw new PNG_Exception("Out of place tIME chunk");
			else if((info_ptr_valid&PNG_INFO.tIME)==PNG_INFO.tIME)
			{
				Debug.WriteLine("Duplicate tIME chunk");
				png_crc_finish(length);
				return;
			}

			if((mode&PNG_MODE.HAVE_IDAT)==PNG_MODE.HAVE_IDAT) mode|=PNG_MODE.AFTER_IDAT;

			if(length!=7)
			{
				Debug.WriteLine("Incorrect tIME chunk length");
				png_crc_finish(length);
				return;
			}

			byte[] buf=new byte[7];
			png_crc_read(buf, 7);
			if(png_crc_finish(0)) return;

			png_time mod_time;
			mod_time.second=buf[6];
			mod_time.minute=buf[5];
			mod_time.hour=buf[4];
			mod_time.day=buf[3];
			mod_time.month=buf[2];
			mod_time.year=png_get_uint_16(buf);

			png_set_tIME(mod_time);
		}

		void png_handle_tEXt(uint length)
		{
			if((mode&PNG_MODE.HAVE_IHDR)!=PNG_MODE.HAVE_IHDR) throw new PNG_Exception("Missing IHDR before tEXt");
			if((mode&PNG_MODE.HAVE_IDAT)==PNG_MODE.HAVE_IDAT) mode|=PNG_MODE.AFTER_IDAT;

			try
			{
				chunkdata=new byte[length+1];
			}
			catch(Exception)
			{
				Debug.WriteLine("No memory to process text chunk");
				png_crc_finish(length);
				return;
			}

			png_crc_read(chunkdata, length);
			if(png_crc_finish(0))
			{
				chunkdata=null;
				return;
			}

			chunkdata[length]=0x00;

			int p=0;
			while(chunkdata[p]!=0x00) p++; // empty loop to find end of key
			string key=Encoding.ASCII.GetString(chunkdata, 0, p);

			string text="";
			if(p<length)
			{
				p++;
				text=Encoding.ASCII.GetString(chunkdata, p, (int)(length-p));
			}

			png_text text_ptr;
			text_ptr.compression=PNG_TEXT_COMPRESSION.NONE;
			text_ptr.key=key;
			text_ptr.lang=null;
			text_ptr.lang_key=null;
			text_ptr.text=text;

			png_set_text(text_ptr);
			chunkdata=null;
		}

		void png_handle_zTXt(uint length)
		{
			if((mode&PNG_MODE.HAVE_IHDR)!=PNG_MODE.HAVE_IHDR) throw new PNG_Exception("Missing IHDR before zTXt");
			if((mode&PNG_MODE.HAVE_IDAT)==PNG_MODE.HAVE_IDAT) mode|=PNG_MODE.AFTER_IDAT;

			try
			{
				chunkdata=new byte[length+1];
			}
			catch(Exception)
			{
				Debug.WriteLine("No memory to process zTXt chunk");
				png_crc_finish(length);
				return;
			}

			png_crc_read(chunkdata, length);
			if(png_crc_finish(0))
			{
				chunkdata=null;
				return;
			}

			chunkdata[length]=0x00;

			uint p=0;
			while(chunkdata[p]!=0x00) p++; // empty loop to find end of key
			string key=Encoding.ASCII.GetString(chunkdata, 0, (int)p);

			// zTXt must have some text after the chunkdataword
			if(p>length-1)
			{
				Debug.WriteLine("Truncated zTXt chunk");
				chunkdata=null;
				return;
			}
			p++;
			PNG_TEXT_COMPRESSION comp_type=(PNG_TEXT_COMPRESSION)chunkdata[p];
			if(comp_type!=PNG_TEXT_COMPRESSION.zTXt)
			{
				Debug.WriteLine("Unknown compression type in zTXt chunk");
				comp_type=PNG_TEXT_COMPRESSION.zTXt;
			}
			p++; // skip the compression_method byte

			uint data_len=0;
			uint prefix_len=(uint)p;

			chunkdata=png_decompress_chunk((PNG_COMPRESSION_TYPE)comp_type, length, prefix_len, ref data_len);

			png_text text_ptr;
			text_ptr.compression=comp_type;
			text_ptr.key=key;
			text_ptr.lang=null;
			text_ptr.lang_key=null;
			text_ptr.text=Encoding.ASCII.GetString(chunkdata, (int)prefix_len, (int)(data_len-prefix_len));

			png_set_text(text_ptr);
			chunkdata=null;
		}

		void png_handle_iTXt(uint length)
		{
			if((mode&PNG_MODE.HAVE_IHDR)!=PNG_MODE.HAVE_IHDR) throw new PNG_Exception("Missing IHDR before iTXt");
			if((mode&PNG_MODE.HAVE_IDAT)==PNG_MODE.HAVE_IDAT) mode|=PNG_MODE.AFTER_IDAT;

			try
			{
				chunkdata=new byte[length+1];
			}
			catch(Exception)
			{
				Debug.WriteLine("No memory to process iTXt chunk");
				png_crc_finish(length);
				return;
			}

			png_crc_read(chunkdata, length);
			if(png_crc_finish(0))
			{
				chunkdata=null;
				return;
			}

			chunkdata[length]=0x00;

			uint p=0;
			while(chunkdata[p]!=0x00) p++; // empty loop to find end of key
			string key=Encoding.ASCII.GetString(chunkdata, 0, (int)p);

			// iTXt must have a language tag (possibly empty), two compression bytes,
			// translated keyword (possibly empty), and possibly some text after the keyword
			if(p>length-4)
			{
				Debug.WriteLine("Truncated iTXt chunk");
				chunkdata=null;
				return;
			}
			p++;
			byte comp_flag=chunkdata[p];

			p++;
			PNG_TEXT_COMPRESSION comp_type=(PNG_TEXT_COMPRESSION)chunkdata[p];
			if(comp_flag!=0)
			{
				if(comp_type!=PNG_TEXT_COMPRESSION.zTXt)
				{
					Debug.WriteLine("Unknown compression type in iTXt chunk");
					comp_type=PNG_TEXT_COMPRESSION.zTXt;
				}
			}
			p++; // skip the compression_method byte

			uint langstart=p;
			while(chunkdata[p]!=0x00) p++; // empty loop to find end of language tag

			string lang=Encoding.ASCII.GetString(chunkdata, (int)langstart, (int)(p-langstart));

			p++;
			if(p>=length)
			{
				Debug.WriteLine("Truncated iTXt chunk");
				chunkdata=null;
				return;
			}

			uint lang_key_start=p;
			while(chunkdata[p]!=0x00) p++; // empty loop to find end of language tag

			string lang_key=Encoding.UTF8.GetString(chunkdata, (int)lang_key_start, (int)(p-lang_key_start));

			p++;
			if(p>=length)
			{
				Debug.WriteLine("Truncated iTXt chunk");
				chunkdata=null;
				return;
			}

			uint data_len=0;
			uint prefix_len=p;

			if(comp_flag!=0) chunkdata=png_decompress_chunk((PNG_COMPRESSION_TYPE)comp_type, length, prefix_len, ref data_len);
			else data_len=length-p;

			string text=Encoding.UTF8.GetString(chunkdata, (int)prefix_len, (int)(data_len-prefix_len));

			png_text text_ptr;
			text_ptr.compression=(PNG_TEXT_COMPRESSION)(comp_flag+1);
			text_ptr.lang_key=lang_key;
			text_ptr.lang=lang;
			text_ptr.key=key;
			text_ptr.text=text;

			png_set_text(text_ptr);
			chunkdata=null;
		}

		void png_handle_sTER(uint length)
		{
			if((mode&PNG_MODE.HAVE_IHDR)!=PNG_MODE.HAVE_IHDR) throw new PNG_Exception("Missing IHDR before sTER");
			else if((mode&PNG_MODE.HAVE_IDAT)==PNG_MODE.HAVE_IDAT)
			{
				Debug.WriteLine("Invalid sTER after IDAT");
				png_crc_finish(length);
				return;
			}
			else if((info_ptr_valid&PNG_INFO.pHYs)==PNG_INFO.pHYs)
			{
				Debug.WriteLine("Duplicate sTER chunk");
				png_crc_finish(length);
				return;
			}

			if(length!=1)
			{
				Debug.WriteLine("Incorrect sTER chunk length");
				png_crc_finish(length);
				return;
			}

			png_crc_read(buf4, 1);
			if(png_crc_finish(0)) return;

			png_set_sTER((PNG_STEREO)buf4[0]);
		}

		void png_handle_gIFg(uint length)
		{
			if((mode&PNG_MODE.HAVE_IHDR)!=PNG_MODE.HAVE_IHDR) throw new PNG_Exception("Out of place gIFg chunk");

			if(length!=4)
			{
				Debug.WriteLine("Incorrect gIFg chunk length");
				png_crc_finish(length);
				return;
			}

			png_crc_read(buf4, 4);
			if(png_crc_finish(0)) return;

			png_gIFg gIFg;
			gIFg.disposal_methode=buf4[0];
			gIFg.user_input_flag=buf4[1];
			gIFg.delay_time=png_get_uint_16(buf4, 2);

			png_set_gIFg(gIFg);
		}

		void png_handle_gIFx(uint length)
		{
			if((mode&PNG_MODE.HAVE_IHDR)!=PNG_MODE.HAVE_IHDR) throw new PNG_Exception("Out of place gIFx chunk");

			if(length<11)
			{
				Debug.WriteLine("Incorrect gIFx chunk length");
				png_crc_finish(length);
				return;
			}

			byte[] buf=new byte[length];

			png_crc_read(buf, length);
			if(png_crc_finish(0)) return;

			png_gIFx gIFx;
			gIFx.app_identifier1=buf[0];
			gIFx.app_identifier2=buf[1];
			gIFx.app_identifier3=buf[2];
			gIFx.app_identifier4=buf[3];
			gIFx.app_identifier5=buf[4];
			gIFx.app_identifier6=buf[5];
			gIFx.app_identifier7=buf[6];
			gIFx.app_identifier8=buf[7];
			gIFx.auth_code1=buf[8];
			gIFx.auth_code2=buf[9];
			gIFx.auth_code3=buf[10];
			gIFx.app_data=null;
			if(length>11)
			{
				gIFx.app_data=new byte[length-11];
				Array.Copy(buf, 11, gIFx.app_data, 0, length-11);
			}

			png_set_gIFx(gIFx);
		}

		// This function is called when we haven't found a handler for a
		// chunk. If there isn't a problem with the chunk itself (ie bad
		// chunk name, CRC, or a critical chunk), the chunk is silently ignored
		// -- unless the PNG_FLAG_UNKNOWN_CHUNKS_SUPPORTED flag is on in which
		// case it will be saved away to be written out later.
		void png_handle_unknown(uint length)
		{
			if((mode&PNG_MODE.HAVE_IDAT)==PNG_MODE.HAVE_IDAT)
				if(memcmp(chunk_name, PNG.IDAT, 4)!=0) mode|=PNG_MODE.AFTER_IDAT;

			if((chunk_name[0]&0x20)!=0x20) png_chunk_error(chunk_name, "unknown critical chunk");

			png_crc_finish(length);
		}

		// This function is called to verify that a chunk name is valid.
		// This function can't have the "critical chunk check" incorporated
		// into it, since in the future we will need to be able to call user
		// functions to handle unknown critical chunks after we check that
		// the chunk name itself is valid.
		static void png_check_chunk_name(byte[] chunk_name)
		{
			if(chunk_name[0]<65||chunk_name[0]>122||(chunk_name[0]>90&&chunk_name[0]<97)) png_chunk_error(chunk_name, "invalid chunk type");
			if(chunk_name[1]<65||chunk_name[1]>122||(chunk_name[1]>90&&chunk_name[1]<97)) png_chunk_error(chunk_name, "invalid chunk type");
			if(chunk_name[2]<65||chunk_name[2]>122||(chunk_name[2]>90&&chunk_name[2]<97)) png_chunk_error(chunk_name, "invalid chunk type");
			if(chunk_name[3]<65||chunk_name[3]>122||(chunk_name[3]>90&&chunk_name[3]<97)) png_chunk_error(chunk_name, "invalid chunk type");
		}
		#endregion

		#region Unsafe Pixel Handling Stuff
		// Combines the row recently read in with the existing pixels in the
		// row. This routine takes care of alpha and transparency if requested.
		// This routine also handles the two methods of progressive display
		// of interlaced images, depending on the mask value.
		// The mask value describes which pixels are to be combined with
		// the row. The pattern always repeats every 8 pixels, so just 8
		// bits are needed. A one indicates the pixel is to be combined,
		// a zero indicates the pixel is to be skipped. This is in addition
		// to any alpha or transparency value associated with the pixel. If
		// you want all pixels to be combined, pass 0xff (255) in mask.
		unsafe void png_combine_row(byte[] row, int mask)
		{
			if(mask==0xff)
			{
				Array.Copy(row_buf, 1, row, 0, PNG_ROWBYTES(row_info.pixel_depth, width));
				return;
			}

			byte m=0x80;
			int s_start, s_end, s_inc;
			int shift;
			uint row_width=width;

			fixed(byte* row_buf_=row_buf, row_=row)
			{
				byte* sp=row_buf_+1;
				byte* dp=row_;

				switch(row_info.pixel_depth)
				{
					case 1:
						{
							if((transformations&PNG_TRANSFORMATION.PACKSWAP)==PNG_TRANSFORMATION.PACKSWAP)
							{
								s_start=0;
								s_end=7;
								s_inc=1;
							}
							else
							{
								s_start=7;
								s_end=0;
								s_inc=-1;
							}

							shift=s_start;

							for(uint i=0; i<row_width; i++)
							{
								if((m&mask)==m)
								{
									int value=(*sp>>shift)&0x01;
									*dp&=(byte)((0x7f7f>>(7-shift))&0xff);
									*dp|=(byte)(value<<shift);
								}

								if(shift==s_end)
								{
									shift=s_start;
									sp++;
									dp++;
								}
								else shift+=s_inc;

								if(m==1) m=0x80;
								else m>>=1;
							}

						}
						break;
					case 2:
						{
							if((transformations&PNG_TRANSFORMATION.PACKSWAP)==PNG_TRANSFORMATION.PACKSWAP)
							{
								s_start=0;
								s_end=6;
								s_inc=2;
							}
							else
							{
								s_start=6;
								s_end=0;
								s_inc=-2;
							}

							shift=s_start;

							for(uint i=0; i<row_width; i++)
							{
								if((m&mask)==m)
								{
									int value=(*sp>>shift)&0x03;
									*dp&=(byte)((0x3f3f>>(6-shift))&0xff);
									*dp|=(byte)(value<<shift);
								}

								if(shift==s_end)
								{
									shift=s_start;
									sp++;
									dp++;
								}
								else shift+=s_inc;

								if(m==1) m=0x80;
								else m>>=1;
							}
						}
						break;
					case 4:
						{
							if((transformations&PNG_TRANSFORMATION.PACKSWAP)==PNG_TRANSFORMATION.PACKSWAP)
							{
								s_start=0;
								s_end=4;
								s_inc=4;
							}
							else
							{
								s_start=4;
								s_end=0;
								s_inc=-4;
							}
							shift=s_start;

							for(uint i=0; i<row_width; i++)
							{
								if((m&mask)==m)
								{
									int value=(*sp>>shift)&0xf;
									*dp&=(byte)((0xf0f>>(4-shift))&0xff);
									*dp|=(byte)(value<<shift);
								}

								if(shift==s_end)
								{
									shift=s_start;
									sp++;
									dp++;
								}
								else shift+=s_inc;

								if(m==1) m=0x80;
								else m>>=1;
							}
						}
						break;
					default:
						{
							uint pixel_bytes=(uint)(row_info.pixel_depth>>3);

							for(uint i=0; i<row_width; i++)
							{
								if((m&mask)==m)
								{
									uint a=0;
									while((a++)<pixel_bytes) *(dp++)=*(sp++);
								}
								else
								{
									sp+=pixel_bytes;
									dp+=pixel_bytes;
								}

								if(m==1) m=0x80;
								else m>>=1;
							}
						}
						break;
				}
			}
		}

		unsafe void png_combine_row(byte* row, int mask)
		{
			if(mask==0xff)
			{
				Marshal.Copy(row_buf, 1, (IntPtr)row, (int)PNG_ROWBYTES(row_info.pixel_depth, width));
				//Array.Copy(row_buf, 1, row, 0, PNG_ROWBYTES(row_info.pixel_depth, width));
				return;
			}

			byte m=0x80;
			int s_start, s_end, s_inc;
			int shift;
			uint row_width=width;

			fixed(byte* row_buf_=row_buf)
			{
				byte* sp=row_buf_+1;
				byte* dp=row;

				switch(row_info.pixel_depth)
				{
					case 1:
						{
							if((transformations&PNG_TRANSFORMATION.PACKSWAP)==PNG_TRANSFORMATION.PACKSWAP)
							{
								s_start=0;
								s_end=7;
								s_inc=1;
							}
							else
							{
								s_start=7;
								s_end=0;
								s_inc=-1;
							}

							shift=s_start;

							for(uint i=0; i<row_width; i++)
							{
								if((m&mask)==m)
								{
									int value=(*sp>>shift)&0x01;
									*dp&=(byte)((0x7f7f>>(7-shift))&0xff);
									*dp|=(byte)(value<<shift);
								}

								if(shift==s_end)
								{
									shift=s_start;
									sp++;
									dp++;
								}
								else shift+=s_inc;

								if(m==1) m=0x80;
								else m>>=1;
							}

						}
						break;
					case 2:
						{
							if((transformations&PNG_TRANSFORMATION.PACKSWAP)==PNG_TRANSFORMATION.PACKSWAP)
							{
								s_start=0;
								s_end=6;
								s_inc=2;
							}
							else
							{
								s_start=6;
								s_end=0;
								s_inc=-2;
							}

							shift=s_start;

							for(uint i=0; i<row_width; i++)
							{
								if((m&mask)==m)
								{
									int value=(*sp>>shift)&0x03;
									*dp&=(byte)((0x3f3f>>(6-shift))&0xff);
									*dp|=(byte)(value<<shift);
								}

								if(shift==s_end)
								{
									shift=s_start;
									sp++;
									dp++;
								}
								else shift+=s_inc;

								if(m==1) m=0x80;
								else m>>=1;
							}
						}
						break;
					case 4:
						{
							if((transformations&PNG_TRANSFORMATION.PACKSWAP)==PNG_TRANSFORMATION.PACKSWAP)
							{
								s_start=0;
								s_end=4;
								s_inc=4;
							}
							else
							{
								s_start=4;
								s_end=0;
								s_inc=-4;
							}
							shift=s_start;

							for(uint i=0; i<row_width; i++)
							{
								if((m&mask)==m)
								{
									int value=(*sp>>shift)&0xf;
									*dp&=(byte)((0xf0f>>(4-shift))&0xff);
									*dp|=(byte)(value<<shift);
								}

								if(shift==s_end)
								{
									shift=s_start;
									sp++;
									dp++;
								}
								else shift+=s_inc;

								if(m==1) m=0x80;
								else m>>=1;
							}
						}
						break;
					default:
						{
							uint pixel_bytes=(uint)(row_info.pixel_depth>>3);

							for(uint i=0; i<row_width; i++)
							{
								if((m&mask)==m)
								{
									uint a=0;
									while((a++)<pixel_bytes) *(dp++)=*(sp++);
								}
								else
								{
									sp+=pixel_bytes;
									dp+=pixel_bytes;
								}

								if(m==1) m=0x80;
								else m>>=1;
							}
						}
						break;
				}
			}
		}

		unsafe void png_do_read_interlace()
		{
			// arrays to facilitate easy interlacing-use pass (0-6) as index
			// offset to next interlace block
			uint[] png_pass_inc=new uint[] { 8, 8, 4, 4, 2, 2, 1 };

			if(row_buf==null) return;
			uint final_width=row_info.width*png_pass_inc[pass];

			int sshift, dshift;
			int s_start, s_end, s_inc;
			uint jstop=png_pass_inc[pass];

			fixed(byte* row_buf_=row_buf)
			{
				byte* row=row_buf_+1;

				switch(row_info.pixel_depth)
				{
					case 1:
						{
							byte* sp=row+((row_info.width-1)>>3);
							byte* dp=row+((final_width-1)>>3);

							if((transformations&PNG_TRANSFORMATION.PACKSWAP)==PNG_TRANSFORMATION.PACKSWAP)
							{
								sshift=(int)((row_info.width+7)&0x07);
								dshift=(int)((final_width+7)&0x07);
								s_start=7;
								s_end=0;
								s_inc=-1;
							}
							else
							{
								sshift=7-(int)((row_info.width+7)&0x07);
								dshift=7-(int)((final_width+7)&0x07);
								s_start=0;
								s_end=7;
								s_inc=1;
							}

							for(uint i=0; i<row_info.width; i++)
							{
								byte v=(byte)((*sp>>sshift)&0x01);
								for(uint j=0; j<jstop; j++)
								{
									*dp&=(byte)((0x7f7f>>(7-dshift))&0xff);
									*dp|=(byte)(v<<dshift);
									if(dshift==s_end)
									{
										dshift=s_start;
										dp--;
									}
									else dshift+=s_inc;
								}
								if(sshift==s_end)
								{
									sshift=s_start;
									sp--;
								}
								else sshift+=s_inc;
							}
						}
						break;
					case 2:
						{
							byte* sp=row+((row_info.width-1)>>2);
							byte* dp=row+((final_width-1)>>2);

							if((transformations&PNG_TRANSFORMATION.PACKSWAP)==PNG_TRANSFORMATION.PACKSWAP)
							{
								sshift=(int)(((row_info.width+3)&0x03)<<1);
								dshift=(int)(((final_width+3)&0x03)<<1);
								s_start=6;
								s_end=0;
								s_inc=-2;
							}
							else
							{
								sshift=(int)((3-((row_info.width+3)&0x03))<<1);
								dshift=(int)((3-((final_width+3)&0x03))<<1);
								s_start=0;
								s_end=6;
								s_inc=2;
							}

							for(uint i=0; i<row_info.width; i++)
							{
								byte v=(byte)((*sp>>sshift)&0x03);
								for(uint j=0; j<jstop; j++)
								{
									*dp&=(byte)((0x3f3f>>(6-dshift))&0xff);
									*dp|=(byte)(v<<dshift);
									if(dshift==s_end)
									{
										dshift=s_start;
										dp--;
									}
									else dshift+=s_inc;
								}
								if(sshift==s_end)
								{
									sshift=s_start;
									sp--;
								}
								else sshift+=s_inc;
							}
						}
						break;
					case 4:
						{
							byte* sp=row+((row_info.width-1)>>1);
							byte* dp=row+((final_width-1)>>1);

							if((transformations&PNG_TRANSFORMATION.PACKSWAP)==PNG_TRANSFORMATION.PACKSWAP)
							{
								sshift=(int)(((row_info.width+1)&0x01)<<2);
								dshift=(int)(((final_width+1)&0x01)<<2);
								s_start=4;
								s_end=0;
								s_inc=-4;
							}
							else
							{
								sshift=(int)((1-((row_info.width+1)&0x01))<<2);
								dshift=(int)((1-((final_width+1)&0x01))<<2);
								s_start=0;
								s_end=4;
								s_inc=4;
							}

							for(uint i=0; i<row_info.width; i++)
							{
								byte v=(byte)((*sp>>sshift)&0xf);

								for(uint j=0; j<jstop; j++)
								{
									*dp&=(byte)((0xf0f>>(4-dshift))&0xff);
									*dp|=(byte)(v<<dshift);
									if(dshift==s_end)
									{
										dshift=s_start;
										dp--;
									}
									else dshift+=s_inc;
								}
								if(sshift==s_end)
								{
									sshift=s_start;
									sp--;
								}
								else sshift+=s_inc;
							}
						}
						break;
					default:
						{
							int pixel_bytes=(row_info.pixel_depth>>3);
							byte* sp=row+(row_info.width-1)*pixel_bytes;
							byte* dp=row+(final_width-1)*pixel_bytes;

							byte v0, v1, v2, v3, v4, v5, v6, v7;

							if(pixel_bytes==1)
							{
								for(uint i=0; i<row_info.width; i++)
								{
									v0=*(sp--);

									for(uint j=0; j<jstop; j++)
									{
										*(dp--)=v0;
									}
								}
							}
							else if(pixel_bytes==2)
							{
								sp+=1;
								dp+=1;
								for(uint i=0; i<row_info.width; i++)
								{
									v1=*(sp--); v0=*(sp--);

									for(uint j=0; j<jstop; j++)
									{
										*(dp--)=v1; *(dp--)=v0;
									}
								}
							}
							else if(pixel_bytes==3)
							{
								sp+=2;
								dp+=2;
								for(uint i=0; i<row_info.width; i++)
								{
									v2=*(sp--); v1=*(sp--); v0=*(sp--);

									for(uint j=0; j<jstop; j++)
									{
										*(dp--)=v2; *(dp--)=v1; *(dp--)=v0;
									}
								}
							}
							else if(pixel_bytes==4)
							{
								sp+=3;
								dp+=3;
								for(uint i=0; i<row_info.width; i++)
								{
									v3=*(sp--); v2=*(sp--); v1=*(sp--); v0=*(sp--);

									for(uint j=0; j<jstop; j++)
									{
										*(dp--)=v3; *(dp--)=v2; *(dp--)=v1; *(dp--)=v0;
									}
								}
							}
							else if(pixel_bytes==8)
							{
								sp+=7;
								dp+=7;
								for(uint i=0; i<row_info.width; i++)
								{
									v7=*(sp--); v6=*(sp--); v5=*(sp--); v4=*(sp--);
									v3=*(sp--); v2=*(sp--); v1=*(sp--); v0=*(sp--);

									for(uint j=0; j<jstop; j++)
									{
										*(dp--)=v7; *(dp--)=v6; *(dp--)=v5; *(dp--)=v4;
										*(dp--)=v3; *(dp--)=v2; *(dp--)=v1; *(dp--)=v0;
									}
								}
							}
							#region pixel_bytes==5..7
							//else if(pixel_bytes==5)
							//{
							//    sp+=4;
							//	  dp+=4;
							//    for(uint i=0; i<row_info.width; i++)
							//    {
							//        v4=*(sp--); v3=*(sp--); v2=*(sp--); v1=*(sp--); v0=*(sp--);

							//        for(uint j=0; j<jstop; j++)
							//        {
							//            *(dp--)=v4; *(dp--)=v3; *(dp--)=v2; *(dp--)=v1; *(dp--)=v0;
							//        }
							//    }
							//}
							//else if(pixel_bytes==6)
							//{
							//    sp+=5;
							//	  dp+=5;
							//    for(uint i=0; i<row_info.width; i++)
							//    {
							//        v5=*(sp--); v4=*(sp--); v3=*(sp--); v2=*(sp--); v1=*(sp--); v0=*(sp--);

							//        for(uint j=0; j<jstop; j++)
							//        {
							//            *(dp--)=v5; *(dp--)=v4; *(dp--)=v3; *(dp--)=v2; *(dp--)=v1; *(dp--)=v0;
							//        }
							//    }
							//}
							//else if(pixel_bytes==7)
							//{
							//    sp+=6;
							//	  dp+=6;
							//    for(uint i=0; i<row_info.width; i++)
							//    {
							//        v6=*(sp--); v5=*(sp--); v4=*(sp--); v3=*(sp--); v2=*(sp--); v1=*(sp--); v0=*(sp--);

							//        for(uint j=0; j<jstop; j++)
							//        {
							//            *(dp--)=v6; *(dp--)=v5; *(dp--)=v4; *(dp--)=v3; *(dp--)=v2; *(dp--)=v1; *(dp--)=v0;
							//        }
							//    }
							//}
							#endregion
							else throw new PNG_Exception("unknown pixel_bytes value");
						}
						break;
				}
			}
			row_info.width=final_width;
			row_info.rowbytes=PNG_ROWBYTES(row_info.pixel_depth, final_width);
		}

		unsafe void png_read_filter_row(png_row_info row_info, byte[] row, byte[] prev_row, PNG_FILTER_VALUE filter)
		{
			fixed(byte* row__=row, prev_row__=prev_row)
			{
				byte* row_=row__+1, prev_row_=prev_row__+1;

				switch(filter)
				{
					case PNG_FILTER_VALUE.NONE: break;
					case PNG_FILTER_VALUE.SUB:
						{
							uint istop=row_info.rowbytes;
							uint bpp=(uint)(row_info.pixel_depth+7)>>3;
							byte* rp=row_+bpp, lp=row_;

							for(uint i=bpp; i<istop; i++)
							{
								*rp=(byte)(((int)(*rp)+(int)(*lp++))&0xff);
								rp++;
							}
						}
						break;
					case PNG_FILTER_VALUE.UP:
						{
							uint istop=row_info.rowbytes;
							byte* rp=row_, pp=prev_row_;

							for(uint i=0; i<istop; i++)
							{
								*rp=(byte)(((int)(*rp)+(int)(*pp++))&0xff);
								rp++;
							}
						}
						break;
					case PNG_FILTER_VALUE.AVG:
						{
							byte* rp=row_, pp=prev_row_, lp=row_;
							uint bpp=(uint)(row_info.pixel_depth+7)>>3;
							uint istop=row_info.rowbytes-bpp;

							for(uint i=0; i<bpp; i++)
							{
								*rp=(byte)(((int)(*rp)+((int)(*pp++)/2))&0xff);
								rp++;
							}

							for(uint i=0; i<istop; i++)
							{
								*rp=(byte)(((int)(*rp)+(int)(*pp+++*lp++)/2)&0xff);
								rp++;
							}
						}
						break;
					case PNG_FILTER_VALUE.PAETH:
						{
							byte* rp=row_, pp=prev_row_, lp=row_, cp=prev_row_;
							uint bpp=(uint)(row_info.pixel_depth+7)>>3;
							uint istop=row_info.rowbytes-bpp;

							for(uint i=0; i<bpp; i++)
							{
								*rp=(byte)(((int)(*rp)+(int)(*pp++))&0xff);
								rp++;
							}

							for(uint i=0; i<istop; i++) // use leftover rp, pp
							{
								int a=*lp++;
								int b=*pp++;
								int c=*cp++;

								int p=b-c;
								int pc=a-c;

								//int pa=abs(p);
								//int pb=abs(pc);
								//pc=abs(p+pc);
								int pa=p<0?-p:p;
								int pb=pc<0?-pc:pc;
								pc=(p+pc)<0?-(p+pc):p+pc;

								p=(pa<=pb&&pa<=pc)?a:(pb<=pc)?b:c;

								*rp=(byte)(((int)(*rp)+p)&0xff);
								rp++;
							}
						}
						break;
					default:
						Debug.WriteLine("Ignoring bad adaptive filter type");
						*row_=0;
						break;
				}

			}
		}
		#endregion

		void png_read_finish_row()
		{
			// arrays to facilitate easy interlacing-use pass (0-6) as index

			// start of interlace block
			uint[] png_pass_start=new uint[] { 0, 4, 0, 2, 0, 1, 0 };

			// offset to next interlace block
			uint[] png_pass_inc=new uint[] { 8, 8, 4, 4, 2, 2, 1 };

			// start of interlace block in the y direction
			uint[] png_pass_ystart=new uint[] { 0, 0, 4, 0, 2, 0, 1 };

			// offset to next interlace block in the y direction
			uint[] png_pass_yinc=new uint[] { 8, 8, 8, 4, 4, 2, 2 };

			row_number++;
			if(row_number<num_rows) return;

			if(interlaced!=PNG_INTERLACE.NONE)
			{
				row_number=0;
				for(int i=0; i<rowbytes+1; i++) prev_row[i]=0;
				do
				{
					pass++;
					if(pass>=7) break;
					iwidth=(width+png_pass_inc[pass]-1-png_pass_start[pass])/png_pass_inc[pass];

					if((transformations&PNG_TRANSFORMATION.INTERLACE)!=PNG_TRANSFORMATION.INTERLACE)
					{
						num_rows=(height+png_pass_yinc[pass]-1-png_pass_ystart[pass])/png_pass_yinc[pass];
						if(num_rows==0) continue;
					}
					else break;
				} while(iwidth==0);

				if(pass<7) return;
			}

			if((flags&PNG_FLAG.ZLIB_FINISHED)!=PNG_FLAG.ZLIB_FINISHED)
			{
				zstream.out_buf=extra;
				zstream.next_out=0;
				zstream.avail_out=1;

				for(; ; )
				{
					if(zstream.avail_in==0)
					{
						while(idat_size==0)
						{
							png_crc_finish(0);

							png_read_data(buf4, 0, 4);
							idat_size=png_get_uint_31(buf4);
							png_reset_crc();
							png_crc_read(chunk_name, 4);
							if(memcmp(chunk_name, PNG.IDAT, 4)!=0) throw new PNG_Exception("Not enough image data");
						}
						zstream.avail_in=zbuf_size;
						zstream.in_buf=zbuf;
						zstream.next_in=0;
						if(zbuf_size>idat_size) zstream.avail_in=idat_size;
						png_crc_read(zbuf, zstream.avail_in);
						idat_size-=zstream.avail_in;
					}
					int ret=zlib.inflate(zstream, zlib.Z_PARTIAL_FLUSH);
					if(ret==zlib.Z_STREAM_END)
					{
						if(zstream.avail_out==0||zstream.avail_in!=0||idat_size!=0) Debug.WriteLine("Extra compressed data");
						mode|=PNG_MODE.AFTER_IDAT;
						flags|=PNG_FLAG.ZLIB_FINISHED;
						break;
					}
					if(ret!=zlib.Z_OK) throw new PNG_Exception((zstream.msg!=null&&zstream.msg.Length>0)?zstream.msg:"Decompression Error");

					if(zstream.avail_out==0)
					{
						Debug.WriteLine("Extra compressed data");
						mode|=PNG_MODE.AFTER_IDAT;
						flags|=PNG_FLAG.ZLIB_FINISHED;
						break;
					}
				}
				zstream.avail_out=0;
			}

			if(idat_size!=0||zstream.avail_in!=0) Debug.WriteLine("Extra compression data");
			zlib.inflateReset(zstream);

			mode|=PNG_MODE.AFTER_IDAT;
		}

		void png_read_start_row() // ushort num_trans aus png_struct nach png_info gewandert
		{
			// arrays to facilitate easy interlacing-use pass (0-6) as index

			// start of interlace block
			uint[] png_pass_start=new uint[] { 0, 4, 0, 2, 0, 1, 0 };

			// offset to next interlace block
			uint[] png_pass_inc=new uint[] { 8, 8, 4, 4, 2, 2, 1 };

			// start of interlace block in the y direction
			uint[] png_pass_ystart=new uint[] { 0, 0, 4, 0, 2, 0, 1 };

			// offset to next interlace block in the y direction
			uint[] png_pass_yinc=new uint[] { 8, 8, 8, 4, 4, 2, 2 };

			// Height of interlace block. This is not currently used - if you need
			// it, uncomment and copy it to were you need it.
			//uint[] png_pass_height=new uint[] { 8, 8, 4, 4, 2, 2, 1 };

			int max_pixel_depth;
			uint row_bytes;

			zstream.avail_in=0;
			png_init_read_transformations();

			if(interlaced!=PNG_INTERLACE.NONE)
			{
				if((transformations&PNG_TRANSFORMATION.INTERLACE)!=PNG_TRANSFORMATION.INTERLACE) num_rows=(height+png_pass_yinc[0]-1-png_pass_ystart[0])/png_pass_yinc[0];
				else num_rows=height;

				iwidth=(width+png_pass_inc[pass]-1-png_pass_start[pass])/png_pass_inc[pass];
			}
			else
			{
				num_rows=height;
				iwidth=width;
			}
			max_pixel_depth=pixel_depth;

			if((transformations&PNG_TRANSFORMATION.PACK)==PNG_TRANSFORMATION.PACK&&bit_depth<8) max_pixel_depth=8;

			if((transformations&PNG_TRANSFORMATION.EXPAND)==PNG_TRANSFORMATION.EXPAND)
			{
				if(color_type==PNG_COLOR_TYPE.PALETTE)
				{
					if(num_trans!=0) max_pixel_depth=32;
					else max_pixel_depth=24;
				}
				else if(color_type==PNG_COLOR_TYPE.GRAY)
				{
					if(max_pixel_depth<8) max_pixel_depth=8;
					if(num_trans!=0) max_pixel_depth*=2;
				}
				else if(color_type==PNG_COLOR_TYPE.RGB)
				{
					if(num_trans!=0)
					{
						max_pixel_depth*=4;
						max_pixel_depth/=3;
					}
				}
			}

			if((transformations&PNG_TRANSFORMATION.FILLER)==PNG_TRANSFORMATION.FILLER)
			{
				if(color_type==PNG_COLOR_TYPE.PALETTE) max_pixel_depth=32;
				else if(color_type==PNG_COLOR_TYPE.GRAY)
				{
					if(max_pixel_depth<=8) max_pixel_depth=16;
					else max_pixel_depth=32;
				}
				else if(color_type==PNG_COLOR_TYPE.RGB)
				{
					if(max_pixel_depth<=32) max_pixel_depth=32;
					else max_pixel_depth=64;
				}
			}

			if((transformations&PNG_TRANSFORMATION.GRAY_TO_RGB)==PNG_TRANSFORMATION.GRAY_TO_RGB)
			{
				if((num_trans!=0&&(transformations&PNG_TRANSFORMATION.EXPAND)==PNG_TRANSFORMATION.EXPAND)||
					(transformations&PNG_TRANSFORMATION.FILLER)==PNG_TRANSFORMATION.FILLER||
					color_type==PNG_COLOR_TYPE.GRAY_ALPHA)
				{
					if(max_pixel_depth<=16) max_pixel_depth=32;
					else max_pixel_depth=64;
				}
				else
				{
					if(max_pixel_depth<=8)
					{
						if(color_type==PNG_COLOR_TYPE.RGB_ALPHA) max_pixel_depth=32;
						else max_pixel_depth=24;
					}
					else if(color_type==PNG_COLOR_TYPE.RGB_ALPHA) max_pixel_depth=64;
					else max_pixel_depth=48;
				}
			}

			if((transformations&PNG_TRANSFORMATION.USER_TRANSFORM)==PNG_TRANSFORMATION.USER_TRANSFORM)
			{
				int user_pixel_depth=user_transform_depth*user_transform_channels;
				if(user_pixel_depth>max_pixel_depth) max_pixel_depth=user_pixel_depth;
			}

			// align the width on the next larger 8 pixels. Mainly used for interlacing
			row_bytes=((width+7)&~((uint)7));
			// calculate the maximum bytes needed, adding a byte and a pixel for safety's sake
			row_bytes=PNG_ROWBYTES(max_pixel_depth, row_bytes)+1;//+((max_pixel_depth+7)>>3));

			row_buf=new byte[row_bytes];

			if((uint)rowbytes>(uint)(PNG.SIZE_MAX-1u)) throw new PNG_Exception("Row has too many bytes to allocate in memory");
			prev_row=new byte[rowbytes+1];

			flags|=PNG_FLAG.ROW_INIT;
		}
	}
}
