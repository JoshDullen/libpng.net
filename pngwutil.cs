// pngwutil.c-utilities to write a PNG file
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
using System.Text;

using Free.Ports.zLib;

namespace Free.Ports.libpng
{
	// This pair of functions encapsulates the operation of (a) compressing a
	// text string, and (b) issuing it later as a series of chunk data writes.
	// The compression_state structure is shared context for these functions
	// set up by the caller in order to make the whole mess thread-safe.
	internal struct compression_state
	{
		public byte[] input;			// the uncompressed input data
		public List<byte[]> output_ptr;	// array of pointers to output
	}

	public partial class png_struct
	{
		#region png_save methods
		// Place a 32-bit number into a buffer in PNG byte order. We work
		// with unsigned numbers for convenience, although one supported
		// ancillary chunk uses signed (two's complement) numbers.
		public static void png_save_uint_32(byte[] buf, uint i)
		{
			buf[0]=(byte)((i>>24)&0xff);
			buf[1]=(byte)((i>>16)&0xff);
			buf[2]=(byte)((i>>8)&0xff);
			buf[3]=(byte)(i&0xff);
		}

		public static void png_save_uint_32(byte[] buf, uint offset, uint i)
		{
			buf[offset+0]=(byte)((i>>24)&0xff);
			buf[offset+1]=(byte)((i>>16)&0xff);
			buf[offset+2]=(byte)((i>>8)&0xff);
			buf[offset+3]=(byte)(i&0xff);
		}

		// The png_save_int_32 function assumes integers are stored in two's
		// complement format. If this isn't the case, then this routine needs to
		// be modified to write data in two's complement format.
		public static void png_save_int_32(byte[] buf, int i)
		{
			buf[0]=(byte)((i>>24)&0xff);
			buf[1]=(byte)((i>>16)&0xff);
			buf[2]=(byte)((i>>8)&0xff);
			buf[3]=(byte)(i&0xff);
		}

		public static void png_save_int_32(byte[] buf, uint offset, int i)
		{
			buf[offset+0]=(byte)((i>>24)&0xff);
			buf[offset+1]=(byte)((i>>16)&0xff);
			buf[offset+2]=(byte)((i>>8)&0xff);
			buf[offset+3]=(byte)(i&0xff);
		}

		// Place a 16-bit number into a buffer in PNG byte order.
		// The parameter is declared uint, not ushort,
		// just to avoid potential problems on pre-ANSI C compilers.
		public static void png_save_uint_16(byte[] buf, uint i)
		{
			buf[0]=(byte)((i>>8)&0xff);
			buf[1]=(byte)(i&0xff);
		}

		public static void png_save_uint_16(byte[] buf, uint offset, uint i)
		{
			buf[offset+0]=(byte)((i>>8)&0xff);
			buf[offset+1]=(byte)(i&0xff);
		}
		#endregion

		// Simple function to write the signature.  If we have already written
		// the magic bytes of the signature, or more likely, the PNG stream is
		// being embedded into another stream and doesn't need its own signature,
		// we should call png_set_sig_bytes() to tell libpng how many of the
		// bytes have already been written.
		void png_write_sig()
		{
			// Inform the I/O callback that the signature is being written
			io_state=PNG_IO.WRITING|PNG_IO.SIGNATURE;

			// Write the rest of the 8 byte signature
			png_write_data(PNG.signature, sig_bytes, (uint)(8-sig_bytes));
			if(sig_bytes<3) mode|=PNG_MODE.HAVE_PNG_SIGNATURE;
		}

		// Write a PNG chunk all at once. The type is an array of ASCII characters
		// representing the chunk name. The array must be at least 4 bytes in
		// length, and does not need to be null terminated. To be safe, pass the
		// pre-defined chunk names here, and if you need a new one, define it
		// where the others are defined. The length is the length of the data.
		// All the data must be present. If that is not possible, use the
		// png_write_chunk_start(), png_write_chunk_data(), and png_write_chunk_end()
		// functions instead.
		public void png_write_chunk(byte[] chunk_name, byte[] data, uint length)
		{
			png_write_chunk_start(chunk_name, length);
			png_write_chunk_data(data, length);
			png_write_chunk_end();
		}

		// Write the start of a PNG chunk. The type is the chunk type.
		// The total_length is the sum of the lengths of all the data you will be
		// passing in png_write_chunk_data().
		void png_write_chunk_start(byte[] chunk_name, uint length)
		{
			// Inform the I/O callback that the chunk header is being written.
			// PNG_IO_CHUNK_HDR requires a single I/O call.
			io_state=PNG_IO.WRITING|PNG_IO.CHUNK_HDR;

			// Write the length
			png_save_uint_32(buf4, length);
			png_write_data(buf4, 0, 4);

			// Write the chunk name
			png_write_data(chunk_name, 0, 4);
			// Reset the crc and run it over the chunk name
			png_reset_crc();
			png_calculate_crc(chunk_name, 0, 4);

			// Inform the I/O callback that chunk data will (possibly) be written.
			// PNG_IO_CHUNK_DATA does NOT require a specific number of I/O calls.
			io_state=PNG_IO.WRITING|PNG_IO.CHUNK_DATA;
		}

		// Write the data of a PNG chunk started with png_write_chunk_start().
		// Note that multiple calls to this function are allowed, and that the
		// sum of the lengths from these calls *must* add up to the total_length
		// given to png_write_chunk_start().
		void png_write_chunk_data(byte[] data, uint length)
		{
			// Write the data, and run the CRC over it
			if(data!=null&&length>0)
			{
				png_write_data(data, 0, length);
				// Update the CRC after writing the data,
				// in case that the user I/O routine alters it.
				png_calculate_crc(data, 0, length);
			}
		}

		// Finish a chunk started with png_write_chunk_start().
		void png_write_chunk_end()
		{
			// Inform the I/O callback that the chunk CRC is being written.
			// PNG_IO_CHUNK_CRC requires a single I/O function call.
			io_state=PNG_IO.WRITING|PNG_IO.CHUNK_CRC;

			// Write the crc in a single operation
			png_save_uint_32(buf4, crc);
			png_write_data(buf4, 0, 4);
		}

		#region png_text_compress
		// compress given text into storage in the png_ptr structure
		uint png_text_compress(byte[] text, PNG_TEXT_COMPRESSION compression, ref compression_state comp)
		{
			comp.input=null;
			comp.output_ptr=null;

			// we may just want to pass the text right through
			if(compression==PNG_TEXT_COMPRESSION.NONE)
			{
				comp.input=text;
				return (uint)text.Length;
			}

			if(compression>=PNG_TEXT_COMPRESSION.LAST) Debug.WriteLine("Unknown compression type "+(int)compression);

			// We can't write the chunk until we find out how much data we have,
			// which means we need to run the compressor first and save the
			// output. This shouldn't be a problem, as the vast majority of
			// comments should be reasonable, but we will set up an array of
			// malloc'd pointers to be sure.
			//
			// If we knew the application was well behaved, we could simplify this
			// greatly by assuming we can always malloc an output buffer large
			// enough to hold the compressed text ((1001*text_len/1000)+12)
			// and malloc this directly. The only time this would be a bad idea is
			// if we can't malloc more than 64K and we have 64K of random input
			// data, or if the input string is incredibly large (although this
			// wouldn't cause a failure, just a slowdown due to swapping).

			// set up the compression buffers
			zstream.avail_in=(uint)text.Length;
			zstream.next_in=0;
			zstream.in_buf=text;
			zstream.avail_out=zbuf_size;
			zstream.next_out=0;
			zstream.out_buf=zbuf;

			comp.output_ptr=new List<byte[]>();

			// this is the same compression loop as in png_write_row()
			int ret;
			do
			{
				// compress the data
				ret=zlib.deflate(zstream, zlib.Z_NO_FLUSH);
				if(ret!=zlib.Z_OK)
				{ // error
					if(zstream.msg!=null&&zstream.msg.Length>0) throw new PNG_Exception(zstream.msg);
					throw new PNG_Exception("zlib error");
				}
				// check to see if we need more room
				if(zstream.avail_out==0)
				{
					// save the data
					byte[] buf=new byte[zbuf_size];
					zbuf.CopyTo(buf, 0);
					comp.output_ptr.Add(buf);

					// and reset the buffer
					zstream.avail_out=zbuf_size;
					zstream.next_out=0;
					zstream.out_buf=zbuf;
				}
				// continue until we don't have any more to compress
			} while(zstream.avail_in!=0);

			// finish the compression
			do
			{
				// tell zlib we are finished
				ret=zlib.deflate(zstream, zlib.Z_FINISH);
				if(ret!=zlib.Z_STREAM_END)
				{ // we got an error
					if(zstream.msg!=null&&zstream.msg.Length>0) throw new PNG_Exception(zstream.msg);
					throw new PNG_Exception("zlib error");
				}

				if(ret==zlib.Z_OK)
				{
					// check to see if we need more room
					if(zstream.avail_out==0)
					{
						// save off the data
						byte[] buf=new byte[zbuf_size];
						zbuf.CopyTo(buf, 0);
						comp.output_ptr.Add(buf);

						// and reset the buffer pointers
						zstream.avail_out=zbuf_size;
						zstream.next_out=0;
						zstream.out_buf=zbuf;
					}
				}
			} while(ret!=zlib.Z_STREAM_END);

			// text length is number of buffers plus last buffer
			uint text_len=zbuf_size*(uint)comp.output_ptr.Count;
			if(zstream.avail_out<zbuf_size) text_len+=zbuf_size-zstream.avail_out;

			return text_len;
		}

		// ship the compressed text out via chunk writes
		void png_write_compressed_data_out(ref compression_state comp)
		{
			// handle the no-compression case
			if(comp.input!=null)
			{
				png_write_chunk_data(comp.input, (uint)comp.input.Length);
				return;
			}

			// write saved output buffers, if any
			for(int i=0; i<comp.output_ptr.Count; i++)
			{
				png_write_chunk_data(comp.output_ptr[i], zbuf_size);
			}
			if(comp.output_ptr!=null) comp.output_ptr.Clear();
			// write anything left in zbuf
			if(zstream.avail_out<zbuf_size) png_write_chunk_data(zbuf, zbuf_size-zstream.avail_out);

			// reset zlib for another zTXt/iTXt or image data
			zlib.deflateReset(zstream);
		}
		#endregion

		#region png_write Chunks
		// Write the IHDR chunk, and update the png_struct with the necessary
		// information. Note that the rest of this code depends upon this
		// information being correct.
		void png_write_IHDR(uint width, uint height, byte bit_depth, PNG_COLOR_TYPE color_type, PNG_COMPRESSION_TYPE compression_type, PNG_FILTER_TYPE filter_type, PNG_INTERLACE interlace_type)
		{
			byte[] buf=new byte[13]; // buffer to store the IHDR info

			// Check that we have valid input data from the application info
			switch(color_type)
			{
				case PNG_COLOR_TYPE.GRAY:
					switch(bit_depth)
					{
						case 1:
						case 2:
						case 4:
						case 8:
						case 16: channels=1; break;
						default: throw new PNG_Exception("Invalid bit depth for grayscale image");
					}
					break;
				case PNG_COLOR_TYPE.RGB:
					if(bit_depth!=8&&bit_depth!=16) throw new PNG_Exception("Invalid bit depth for RGB image");
					channels=3;
					break;
				case PNG_COLOR_TYPE.PALETTE:
					switch(bit_depth)
					{
						case 1:
						case 2:
						case 4:
						case 8: channels=1; break;
						default: throw new PNG_Exception("Invalid bit depth for paletted image");
					}
					break;
				case PNG_COLOR_TYPE.GRAY_ALPHA:
					if(bit_depth!=8&&bit_depth!=16) throw new PNG_Exception("Invalid bit depth for grayscale+alpha image");
					channels=2;
					break;
				case PNG_COLOR_TYPE.RGB_ALPHA:
					if(bit_depth!=8&&bit_depth!=16) throw new PNG_Exception("Invalid bit depth for RGBA image");
					channels=4;
					break;
				default:
					throw new PNG_Exception("Invalid image color type specified");
			}

			if(compression_type!=PNG_COMPRESSION_TYPE.BASE)
			{
				Debug.WriteLine("Invalid compression type specified");
				compression_type=PNG_COMPRESSION_TYPE.BASE;
			}

			// Write filter_method 64 (intrapixel differencing) only if
			// 1.	Libpng did not write a PNG signature (this filter_method is only
			//		used in PNG datastreams that are embedded in MNG datastreams) and
			// 2.	The application called png_permit_mng_features with a mask that
			//		included PNG_FLAG_MNG_FILTER_64 and
			// 3.	The filter_method is 64 and
			// 4.	The color_type is RGB or RGBA
			if(!((mng_features_permitted&PNG_FLAG_MNG.FILTER_64)==PNG_FLAG_MNG.FILTER_64&&((mode&PNG_MODE.HAVE_PNG_SIGNATURE)==PNG_MODE.None)&&
				(color_type==PNG_COLOR_TYPE.RGB||color_type==PNG_COLOR_TYPE.RGB_ALPHA)&&(filter_type==PNG_FILTER_TYPE.INTRAPIXEL_DIFFERENCING))&&
				filter_type!=PNG_FILTER_TYPE.BASE)
			{
				Debug.WriteLine("Invalid filter type specified");
				filter_type=PNG_FILTER_TYPE.BASE;
			}

			interlace_type=PNG_INTERLACE.NONE;

			// Save the relevent information
			this.bit_depth=bit_depth;
			this.color_type=color_type;
			info_ptr_interlace_type=interlace_type;
			this.filter_type=filter_type;
			this.compression_type=compression_type;
			this.width=width;
			this.height=height;

			pixel_depth=(byte)(bit_depth*channels);
			rowbytes=PNG_ROWBYTES(pixel_depth, width);
			// set the usr info, so any transformations can modify it
			usr_width=width;
			usr_bit_depth=bit_depth;
			usr_channels=channels;

			// pack the header information into the buffer
			png_save_uint_32(buf, width);
			png_save_uint_32(buf, 4, height);
			buf[8]=bit_depth;
			buf[9]=(byte)color_type;
			buf[10]=(byte)compression_type;
			buf[11]=(byte)filter_type;
			buf[12]=(byte)interlace_type;

			// write the chunk
			png_write_chunk(PNG.IHDR, buf, 13);

			// initialize zlib with PNG info
			if(do_filter==PNG_FILTER.None)
			{
				if(color_type==PNG_COLOR_TYPE.PALETTE||bit_depth<8) do_filter=PNG_FILTER.NONE;
				else do_filter=PNG_FILTER.ALL;
			}
			if((flags&PNG_FLAG.ZLIB_CUSTOM_STRATEGY)!=PNG_FLAG.ZLIB_CUSTOM_STRATEGY)
			{
				if(do_filter!=PNG_FILTER.NONE) zlib_strategy=zlib.Z_FILTERED;
				else zlib_strategy=zlib.Z_DEFAULT_STRATEGY;
			}
			if((flags&PNG_FLAG.ZLIB_CUSTOM_LEVEL)!=PNG_FLAG.ZLIB_CUSTOM_LEVEL) zlib_level=zlib.Z_DEFAULT_COMPRESSION;
			if((flags&PNG_FLAG.ZLIB_CUSTOM_MEM_LEVEL)!=PNG_FLAG.ZLIB_CUSTOM_MEM_LEVEL) zlib_mem_level=8;
			if((flags&PNG_FLAG.ZLIB_CUSTOM_WINDOW_BITS)!=PNG_FLAG.ZLIB_CUSTOM_WINDOW_BITS) zlib_window_bits=15;
			if((flags&PNG_FLAG.ZLIB_CUSTOM_METHOD)!=PNG_FLAG.ZLIB_CUSTOM_METHOD) zlib_method=8;
			int ret=zlib.deflateInit2(zstream, zlib_level, zlib_method, zlib_window_bits, zlib_mem_level, zlib_strategy);
			if(ret!=zlib.Z_OK)
			{
				if(ret==zlib.Z_VERSION_ERROR) throw new PNG_Exception("zlib failed to initialize compressor -- version error");
				if(ret==zlib.Z_STREAM_ERROR) throw new PNG_Exception("zlib failed to initialize compressor -- stream error");
				if(ret==zlib.Z_MEM_ERROR) throw new PNG_Exception("zlib failed to initialize compressor -- mem error");
				throw new PNG_Exception("zlib failed to initialize compressor");
			}
			zstream.next_out=0;
			zstream.out_buf=zbuf;
			zstream.avail_out=zbuf_size;

			mode=PNG_MODE.HAVE_IHDR;
		}

		// write the palette. We are careful not to trust png_color to be in the
		// correct order for PNG, so people can redefine it to any convenient
		// structure.
		void png_write_PLTE(png_color[] palette)
		{
			uint num_pal=0;
			if(palette!=null) num_pal=(uint)palette.Length;

			if(((mng_features_permitted&PNG_FLAG_MNG.EMPTY_PLTE)!=PNG_FLAG_MNG.EMPTY_PLTE&&num_pal==0)||num_pal>256)
			{
				if(color_type==PNG_COLOR_TYPE.PALETTE) throw new PNG_Exception("Invalid number of colors in palette");
				else
				{
					Debug.WriteLine("Invalid number of colors in palette");
					return;
				}
			}

			if((color_type&PNG_COLOR_TYPE.COLOR_MASK)!=PNG_COLOR_TYPE.COLOR_MASK)
			{
				Debug.WriteLine("Ignoring request to write a PLTE chunk in grayscale PNG");
				return;
			}

			png_write_chunk_start(PNG.PLTE, num_pal*3);
			for(uint i=0; i<num_pal; i++)
			{
				buf4[0]=palette[i].red;
				buf4[1]=palette[i].green;
				buf4[2]=palette[i].blue;
				png_write_chunk_data(buf4, 3);
			}
			png_write_chunk_end();
			mode|=PNG_MODE.HAVE_PLTE;
		}

		// write an IDAT chunk
		void png_write_IDAT(byte[] data, uint length)
		{
			// Optimize the CMF field in the zlib stream.
			// This hack of the zlib stream is compliant to the stream specification.
			if((mode&PNG_MODE.HAVE_IDAT)!=PNG_MODE.HAVE_IDAT&&compression_type==PNG_COMPRESSION_TYPE.BASE)
			{
				uint z_cmf=data[0]; // zlib compression method and flags
				if((z_cmf&0x0f)==8&&(z_cmf&0xf0)<=0x70)
				{
					// Avoid memory underflows and multiplication overflows.
					// The conditions below are practically always satisfied;
					// however, they still must be checked.
					if(length>=2&&height<16384&&width<16384)
					{
						uint uncompressed_idat_size=height*((width*channels*bit_depth+15)>>3);
						uint z_cinfo=z_cmf>>4;
						uint half_z_window_size=1u<<((int)z_cinfo+7);
						while(uncompressed_idat_size<=half_z_window_size&&half_z_window_size>=256)
						{
							z_cinfo--;
							half_z_window_size>>=1;
						}
						z_cmf=(z_cmf&0x0f)|(z_cinfo<<4);
						if(data[0]!=(byte)z_cmf)
						{
							data[0]=(byte)z_cmf;
							data[1]&=0xe0;
							data[1]+=(byte)(0x1f-((z_cmf<<8)+data[1])%0x1f);
						}
					}
				}
				else throw new PNG_Exception("Invalid zlib compression method or flags in IDAT");
			}

			png_write_chunk(PNG.IDAT, data, length);
			mode|=PNG_MODE.HAVE_IDAT;
		}

		// write an IEND chunk
		void png_write_IEND()
		{
			png_write_chunk(PNG.IEND, null, 0);
			mode|=PNG_MODE.HAVE_IEND;
		}

		// write a gAMA chunk
		void png_write_gAMA(double file_gamma)
		{
			// file_gamma is saved in 1/100,000ths
			uint igamma=(uint)(file_gamma*100000.0+0.5);
			png_save_uint_32(buf4, igamma);
			png_write_chunk(PNG.gAMA, buf4, 4);
		}

		// write a sRGB chunk
		void png_write_sRGB(PNG_sRGB_INTENT srgb_intent)
		{
			if(srgb_intent>=PNG_sRGB_INTENT.LAST) Debug.WriteLine("Invalid sRGB rendering intent specified");
			buf4[0]=(byte)srgb_intent;
			png_write_chunk(PNG.sRGB, buf4, 1);
		}

		// write an iCCP chunk
		void png_write_iCCP(string name, PNG_COMPRESSION_TYPE compression_type, byte[] profile)
		{
			uint name_len;
			byte[] new_name=null;
			compression_state comp;
			uint embedded_profile_len=0;

			comp.output_ptr=null;
			comp.input=null;

			if(name==null||(name_len=png_check_keyword(name, ref new_name))==0) return;

			if(compression_type!=PNG_COMPRESSION_TYPE.BASE) Debug.WriteLine("Unknown compression type in iCCP chunk");

			uint profile_len=0;
			if(profile!=null) profile_len=(uint)profile.Length;

			if(profile_len>3) embedded_profile_len=png_get_uint_32(profile);

			if(profile_len<embedded_profile_len)
			{
				Debug.WriteLine("Embedded profile length too large in iCCP chunk");
				return;
			}

			if(profile_len>embedded_profile_len)
			{
				Debug.WriteLine("Truncating profile to actual length in iCCP chunk");
				profile_len=embedded_profile_len;
				byte[] tmp=new byte[profile_len];
				Array.Copy(profile, tmp, profile_len);
				profile=tmp;
			}

			if(profile_len!=0) profile_len=png_text_compress(profile, (PNG_TEXT_COMPRESSION)compression_type, ref comp);

			// make sure we include the NULL after the name and the compression type
			png_write_chunk_start(PNG.iCCP, name_len+profile_len+2);
			new_name[name_len+1]=0x00;
			png_write_chunk_data(new_name, name_len+2);

			if(profile_len!=0) png_write_compressed_data_out(ref comp);

			png_write_chunk_end();
		}

		// write a sPLT chunk
		void png_write_sPLT(png_sPLT_t spalette)
		{
			uint name_len;
			byte[] new_name=null;
			byte[] entrybuf=new byte[10];
			uint entry_size=(uint)(spalette.depth==8?6:10);
			uint palette_size=entry_size*spalette.nentries;

			if((name_len=png_check_keyword(spalette.name, ref new_name))==0)
				return;

			// make sure we include the NULL after the name
			png_write_chunk_start(PNG.sPLT, (uint)(name_len+2+palette_size));
			png_write_chunk_data(new_name, name_len+1);
			entrybuf[0]=spalette.depth;
			png_write_chunk_data(entrybuf, 1);

			// loop through each palette entry, writing appropriately
			for(uint i=0; i<spalette.nentries; i++)
			{
				png_sPLT_entry ep=spalette.entries[i];
				if(spalette.depth==8)
				{
					entrybuf[0]=(byte)ep.red;
					entrybuf[1]=(byte)ep.green;
					entrybuf[2]=(byte)ep.blue;
					entrybuf[3]=(byte)ep.alpha;
					png_save_uint_16(entrybuf, 4, ep.frequency);
				}
				else
				{
					png_save_uint_16(entrybuf, ep.red);
					png_save_uint_16(entrybuf, 2, ep.green);
					png_save_uint_16(entrybuf, 4, ep.blue);
					png_save_uint_16(entrybuf, 6, ep.alpha);
					png_save_uint_16(entrybuf, 8, ep.frequency);
				}
				png_write_chunk_data(entrybuf, entry_size);
			}

			png_write_chunk_end();
		}

		// write the sBIT chunk
		void png_write_sBIT(png_color_8 sbit, PNG_COLOR_TYPE color_type)
		{
			uint size;

			// make sure we don't depend upon the order of PNG_COLOR_8
			if((color_type&PNG_COLOR_TYPE.COLOR_MASK)==PNG_COLOR_TYPE.COLOR_MASK)
			{
				byte maxbits=color_type==PNG_COLOR_TYPE.PALETTE?(byte)8:usr_bit_depth;
				if(sbit.red==0||sbit.red>maxbits||sbit.green==0||sbit.green>maxbits||sbit.blue==0||sbit.blue>maxbits)
				{
					Debug.WriteLine("Invalid sBIT depth specified");
					return;
				}
				buf4[0]=sbit.red;
				buf4[1]=sbit.green;
				buf4[2]=sbit.blue;
				size=3;
			}
			else
			{
				if(sbit.gray==0||sbit.gray>usr_bit_depth)
				{
					Debug.WriteLine("Invalid sBIT depth specified");
					return;
				}
				buf4[0]=sbit.gray;
				size=1;
			}

			if((color_type&PNG_COLOR_TYPE.ALPHA_MASK)==PNG_COLOR_TYPE.ALPHA_MASK)
			{
				if(sbit.alpha==0||sbit.alpha>usr_bit_depth)
				{
					Debug.WriteLine("Invalid sBIT depth specified");
					return;
				}
				buf4[size++]=sbit.alpha;
			}

			png_write_chunk(PNG.sBIT, buf4, size);
		}

		// write the cHRM chunk
		void png_write_cHRM(double white_x, double white_y, double red_x, double red_y, double green_x, double green_y, double blue_x, double blue_y)
		{
			byte[] buf=new byte[32];
			//uint itemp;

			uint int_white_x, int_white_y, int_red_x, int_red_y,
				int_green_x, int_green_y, int_blue_x, int_blue_y;

			int_white_x=(uint)(white_x*100000.0+0.5);
			int_white_y=(uint)(white_y*100000.0+0.5);
			int_red_x=(uint)(red_x*100000.0+0.5);
			int_red_y=(uint)(red_y*100000.0+0.5);
			int_green_x=(uint)(green_x*100000.0+0.5);
			int_green_y=(uint)(green_y*100000.0+0.5);
			int_blue_x=(uint)(blue_x*100000.0+0.5);
			int_blue_y=(uint)(blue_y*100000.0+0.5);

			if(png_check_cHRM_fixed(int_white_x, int_white_y, int_red_x, int_red_y, int_green_x, int_green_y, int_blue_x, int_blue_y))
			{
				// each value is saved in 1/100,000ths
				png_save_uint_32(buf, int_white_x);
				png_save_uint_32(buf, 4, int_white_y);

				png_save_uint_32(buf, 8, int_red_x);
				png_save_uint_32(buf, 12, int_red_y);

				png_save_uint_32(buf, 16, int_green_x);
				png_save_uint_32(buf, 20, int_green_y);

				png_save_uint_32(buf, 24, int_blue_x);
				png_save_uint_32(buf, 28, int_blue_y);

				png_write_chunk(PNG.cHRM, buf, 32);
			}
		}

		// write the tRNS chunk
		void png_write_tRNS(byte[] trans_alpha, png_color_16 tran, int num_trans, PNG_COLOR_TYPE color_type)
		{
			byte[] buf=new byte[6];

			if(color_type==PNG_COLOR_TYPE.PALETTE)
			{
				if(num_trans<=0||num_trans>palette.Length)
				{
					Debug.WriteLine("Invalid number of transparent colors specified");
					return;
				}
				// write the chunk out as it is
				png_write_chunk(PNG.tRNS, trans_alpha, (uint)num_trans);
			}
			else if(color_type==PNG_COLOR_TYPE.GRAY)
			{
				// one 16 bit value
				if(tran.gray>=(1<<bit_depth))
				{
					Debug.WriteLine("Ignoring attempt to write tRNS chunk out-of-range for bit_depth");
					return;
				}
				png_save_uint_16(buf, tran.gray);
				png_write_chunk(PNG.tRNS, buf, 2);
			}
			else if(color_type==PNG_COLOR_TYPE.RGB)
			{
				// three 16 bit values
				png_save_uint_16(buf, tran.red);
				png_save_uint_16(buf, 2, tran.green);
				png_save_uint_16(buf, 4, tran.blue);
				if(bit_depth==8&&(buf[0]|buf[2]|buf[4])!=0)
				{
					Debug.WriteLine("Ignoring attempt to write 16-bit tRNS chunk when bit_depth is 8");
					return;
				}
				png_write_chunk(PNG.tRNS, buf, 6);
			}
			else Debug.WriteLine("Can't write tRNS with an alpha channel");
		}

		// write the background chunk
		void png_write_bKGD(png_color_16 back, PNG_COLOR_TYPE color_type)
		{
			byte[] buf=new byte[6];

			if(color_type==PNG_COLOR_TYPE.PALETTE)
			{
				uint num_palette=0;
				if(palette!=null) num_palette=(uint)palette.Length;
				if((num_palette!=0||(mng_features_permitted&PNG_FLAG_MNG.EMPTY_PLTE)!=PNG_FLAG_MNG.EMPTY_PLTE)&&back.index>=num_palette)
				{
					Debug.WriteLine("Invalid background palette index");
					return;
				}
				buf[0]=back.index;
				png_write_chunk(PNG.bKGD, buf, 1);
			}
			else if((color_type&PNG_COLOR_TYPE.COLOR_MASK)==PNG_COLOR_TYPE.COLOR_MASK)
			{
				png_save_uint_16(buf, back.red);
				png_save_uint_16(buf, 2, back.green);
				png_save_uint_16(buf, 4, back.blue);
				if(bit_depth==8&&(buf[0]|buf[2]|buf[4])!=0)
				{
					Debug.WriteLine("Ignoring attempt to write 16-bit bKGD chunk when bit_depth is 8");
					return;
				}
				png_write_chunk(PNG.bKGD, buf, 6);
			}
			else
			{
				if(back.gray>=(1<<bit_depth))
				{
					Debug.WriteLine("Ignoring attempt to write bKGD chunk out-of-range for bit_depth");
					return;
				}
				png_save_uint_16(buf, back.gray);
				png_write_chunk(PNG.bKGD, buf, 2);
			}
		}

		// write the histogram
		void png_write_hIST(ushort[] hist)
		{
			if(hist==null||hist.Length==0) return;

			int num_palette=0;
			if(palette!=null) num_palette=palette.Length;

			if(hist.Length>(int)num_palette)
			{
				Debug.WriteLine("Invalid number of histogram entries specified");
				return;
			}

			png_write_chunk_start(PNG.hIST, (uint)hist.Length*2);
			for(int i=0; i<hist.Length; i++)
			{
				png_save_uint_16(buf4, hist[i]);
				png_write_chunk_data(buf4, 2);
			}
			png_write_chunk_end();
		}

		// Check that the tEXt or zTXt keyword is valid per PNG 1.0 specification,
		// and if invalid, correct the keyword rather than discarding the entire
		// chunk. The PNG 1.0 specification requires keywords 1-79 characters in
		// length, forbids leading or trailing whitespace, multiple internal spaces,
		// and the non-break space (0x80) from ISO 8859-1. Returns keyword length.
		//
		// The new_key is allocated to hold the corrected keyword and must be freed
		// by the calling routine. This avoids problems with trying to write to
		// static keywords without having to have duplicate copies of the strings.
		uint png_check_keyword(string key, ref byte[] new_key)
		{
			new_key=null;

			if(key==null)
			{
				Debug.WriteLine("zero length keyword");
				return 0;
			}

			uint key_len=(uint)key.Length;
			if(key_len==0)
			{
				Debug.WriteLine("zero length keyword");
				return 0;
			}

			try
			{
				new_key=new byte[key_len+2];
			}
			catch(Exception)
			{
				Debug.WriteLine("Out of memory while procesing keyword");
				return 0;
			}

			uint dp=0;

			// Replace non-printing characters with a blank and print a warning
			foreach(char kp in key)
			{
				if(kp<0x20||(kp>0x7E&&kp<0xA1)||kp>0xFF)
				{
					Debug.WriteLine("invalid keyword character 0x"+((ushort)kp).ToString("x4"));
					new_key[dp]=0x20;
				}
				else new_key[dp]=(byte)kp;

				dp++;
			}
			new_key[dp]=0;

			// Remove any trailing white space.
			dp=key_len-1;
			if(new_key[dp]==0x20)
			{
				Debug.WriteLine("trailing spaces removed from keyword");

				while(new_key[dp]==0x20)
				{
					new_key[dp--]=0;
					key_len--;
				}
			}

			// Remove any leading white space.
			dp=0;
			if(new_key[dp]==0x20)
			{
				Debug.WriteLine("leading spaces removed from keyword");

				while(new_key[dp]==0x20)
				{
					dp++;
					key_len--;
				}
			}

			bool kflag=false;
			int kwarn=0;

			// Remove multiple internal spaces.
			uint dp1=dp;
			for(dp=0; new_key[dp1]!=0; dp1++)
			{
				if(new_key[dp1]==0x20&&!kflag)
				{
					new_key[dp++]=new_key[dp1];
					kflag=true;
				}
				else if(new_key[dp1]==0x20)
				{
					key_len--;
					kwarn=1;
				}
				else
				{
					new_key[dp++]=new_key[dp1];
					kflag=false;
				}
			}
			new_key[dp]=0;
			if(kwarn!=0) Debug.WriteLine("extra interior spaces removed from keyword");

			if(key_len==0)
			{
				new_key=null;
				Debug.WriteLine("Zero length keyword");
				return 0;
			}

			if(key_len>79)
			{
				Debug.WriteLine("keyword length must be 1-79 characters");
				key_len=79;
			}

			if(key_len!=new_key.Length-2)
			{
				byte[] tmp=new byte[key_len+2];
				Array.Copy(new_key, tmp, key_len);
				new_key=tmp;
				new_key[key_len]=0;
			}

			return key_len;
		}

		// write a tEXt chunk
		void png_write_tEXt(string key, string text)
		{
			uint key_len;
			byte[] new_key=null;

			if((key_len=png_check_keyword(key, ref new_key))==0) return;

			uint text_len=0;
			if(text!=null) text_len=(uint)text.Length;

			// make sure we include the 0 after the key
			png_write_chunk_start(PNG.tEXt, key_len+text_len+1);

			// We leave it to the application to meet PNG-1.0 requirements on the
			// contents of the text. PNG-1.0 through PNG-1.2 discourage the use of
			// any non-Latin-1 characters except for NEWLINE. ISO PNG will forbid them.
			// The NUL character is forbidden by PNG-1.0 through PNG-1.2 and ISO PNG.
			png_write_chunk_data(new_key, key_len+1);
			if(text_len!=0) png_write_chunk_data(Encoding.ASCII.GetBytes(text), text_len);

			png_write_chunk_end();
		}

		// write a compressed text chunk
		void png_write_zTXt(string key, string text, PNG_TEXT_COMPRESSION compression)
		{
			uint key_len;
			byte[] new_key=null;

			if((key_len=png_check_keyword(key, ref new_key))==0) return;

			if(text==null||text.Length==0||compression==PNG_TEXT_COMPRESSION.NONE)
			{
				png_write_tEXt(key, text);
				return;
			}

			compression_state comp;
			comp.output_ptr=null;
			comp.input=null;

			// compute the compressed data; do it now for the length
			uint text_len=png_text_compress(Encoding.ASCII.GetBytes(text), compression, ref comp);

			// write start of chunk
			png_write_chunk_start(PNG.zTXt, key_len+text_len+2);
			new_key[key_len+1]=(byte)compression;
			// write key and compression
			png_write_chunk_data(new_key, key_len+2);
			// write the compressed data
			png_write_compressed_data_out(ref comp);

			// close the chunk
			png_write_chunk_end();
		}

		// write an iTXt chunk
		void png_write_iTXt(PNG_TEXT_COMPRESSION compression, string key, string lang, string lang_key, string text)
		{
			uint lang_len, key_len;
			byte[] new_lang=null, new_key=null;

			if((key_len=png_check_keyword(key, ref new_key))==0) return;

			if((lang_len=png_check_keyword(lang, ref new_lang))==0)
			{
				Debug.WriteLine("Empty language field in iTXt chunk");
				new_lang=null;
				lang_len=0;
			}

			uint lang_key_len=0;
			if(lang_key!=null) lang_key_len=(uint)lang_key.Length;

			uint text_len=0;
			if(text!=null) text_len=(uint)text.Length;

			compression_state comp;
			comp.output_ptr=null;
			comp.input=null;

			// compute the compressed data; do it now for the length
			text_len=png_text_compress(text_len==0?null:Encoding.UTF8.GetBytes(text), compression-2, ref comp);

			// make sure we include the compression flag, the compression byte,
			// and the NULs after the key, lang, and lang_key parts
			png_write_chunk_start(PNG.iTXt, 5+key_len+lang_len+lang_key_len+text_len); // 5: comp byte, comp flag, terminators for key, lang and lang_key

			// We leave it to the application to meet PNG-1.0 requirements on the
			// contents of the text. PNG-1.0 through PNG-1.2 discourage the use of
			// any non-Latin-1 characters except for NEWLINE. ISO PNG will forbid them.
			// The NUL character is forbidden by PNG-1.0 through PNG-1.2 and ISO PNG.
			png_write_chunk_data(new_key, key_len+1);

			byte[] cbuf=new byte[2];

			// set the compression flag
			if(compression==PNG_TEXT_COMPRESSION.ITXT_COMPRESSION_NONE||compression==PNG_TEXT_COMPRESSION.NONE) cbuf[0]=0;
			else cbuf[0]=1; // compression==PNG_TEXT_COMPRESSION.ITXT_COMPRESSION_zTXt

			// set the compression method
			cbuf[1]=0;
			png_write_chunk_data(cbuf, 2);

			cbuf[0]=0;
			png_write_chunk_data((new_lang!=null?new_lang:cbuf), lang_len+1);
			png_write_chunk_data((lang_key!=null?Encoding.UTF8.GetBytes(lang_key):cbuf), lang_key_len+1);
			png_write_compressed_data_out(ref comp);

			png_write_chunk_end();
		}

		// write the oFFs chunk
		void png_write_oFFs(int x_offset, int y_offset, PNG_OFFSET unit_type)
		{
			byte[] buf=new byte[9];

			if(unit_type>=PNG_OFFSET.LAST) Debug.WriteLine("Unrecognized unit type for oFFs chunk");

			png_save_int_32(buf, x_offset);
			png_save_int_32(buf, 4, y_offset);
			buf[8]=(byte)unit_type;

			png_write_chunk(PNG.oFFs, buf, 9);
		}

		// write the pCAL chunk (described in the PNG extensions document)
		void png_write_pCAL(string purpose, int X0, int X1, PNG_EQUATION type, string units, string[] @params)
		{
			if(units==null||@params==null) return;

			if(type>=PNG_EQUATION.LAST) Debug.WriteLine("Unrecognized equation type for pCAL chunk");

			byte[] new_purpose=null;
			uint purpose_len=png_check_keyword(purpose, ref new_purpose)+1;
			uint units_len=(uint)(units.Length+(@params.Length==0?0:1));
			uint total_len=purpose_len+units_len+10;

			uint[] params_len=new uint[@params.Length];

			// Find the length of each parameter, making sure we don't count the
			// null terminator for the last parameter.
			for(int i=0; i<@params.Length; i++)
			{
				params_len[i]=(uint)(@params[i].Length+(i==@params.Length-1?0:1));
				total_len+=params_len[i];
			}

			byte[] buf=new byte[10];

			png_write_chunk_start(PNG.pCAL, (uint)total_len);
			png_write_chunk_data(new_purpose, purpose_len);
			png_save_int_32(buf, X0);
			png_save_int_32(buf, 4, X1);
			buf[8]=(byte)type;
			buf[9]=(byte)@params.Length;
			png_write_chunk_data(buf, 10);
			png_write_chunk_data(Encoding.ASCII.GetBytes(units), units_len);

			new_purpose=null;

			for(int i=0; i<@params.Length; i++) png_write_chunk_data(Encoding.ASCII.GetBytes(@params[i]), params_len[i]);

			png_write_chunk_end();
		}

		// write the sCAL chunk
		void png_write_sCAL(PNG_SCALE unit, double width, double height)
		{
			byte[] strWidth=Encoding.ASCII.GetBytes(width.ToString("G12", nc));
			byte[] strHeight=Encoding.ASCII.GetBytes(height.ToString("G12", nc));

			byte[] buf=new byte[strWidth.Length+strHeight.Length+2];
			buf[0]=(byte)unit;
			Array.Copy(strWidth, 0, buf, 1, strWidth.Length);
			buf[strWidth.Length+1]=0;
			Array.Copy(strHeight, 0, buf, strWidth.Length+2, strHeight.Length);

			png_write_chunk(PNG.sCAL, buf, (uint)buf.Length);
		}

		// write the pHYs chunk
		void png_write_pHYs(uint x_pixels_per_unit, uint y_pixels_per_unit, PNG_RESOLUTION unit_type)
		{
			byte[] buf=new byte[9];

			if(unit_type>=PNG_RESOLUTION.LAST) Debug.WriteLine("Unrecognized unit type for pHYs chunk");

			png_save_uint_32(buf, x_pixels_per_unit);
			png_save_uint_32(buf, 4, y_pixels_per_unit);
			buf[8]=(byte)unit_type;

			png_write_chunk(PNG.pHYs, buf, 9);
		}

		// Write the tIME chunk. Use either png_convert_from_struct_tm()
		// or png_convert_from_time_t(), or fill in the structure yourself.
		void png_write_tIME(png_time mod_time)
		{
			byte[] buf=new byte[7];

			if(mod_time.month>12||mod_time.month<1||mod_time.day>31||mod_time.day<1||mod_time.hour>23||mod_time.second>60)
			{
				Debug.WriteLine("Invalid time specified for tIME chunk");
				return;
			}

			png_save_uint_16(buf, mod_time.year);
			buf[2]=mod_time.month;
			buf[3]=mod_time.day;
			buf[4]=mod_time.hour;
			buf[5]=mod_time.minute;
			buf[6]=mod_time.second;

			png_write_chunk(PNG.tIME, buf, 7);
		}
		#endregion

		// initializes the row writing capability of libpng
		void png_write_start_row()
		{
			uint buf_size=PNG_ROWBYTES(usr_channels*usr_bit_depth, width)+1;

			// set up row buffer
			row_buf=new byte[buf_size];
			row_buf[0]=(byte)PNG_FILTER_VALUE.NONE;

			// set up filtering buffer, if using this filter
			if((do_filter&PNG_FILTER.SUB)==PNG_FILTER.SUB)
			{
				sub_row=new byte[rowbytes+1];
				sub_row[0]=(byte)PNG_FILTER_VALUE.SUB;
			}

			// We only need to keep the previous row if we are using one of these.
			if((do_filter&(PNG_FILTER.AVG|PNG_FILTER.UP|PNG_FILTER.PAETH))!=PNG_FILTER.None)
			{
				// set up previous row buffer
				prev_row=new byte[buf_size];

				if((do_filter&PNG_FILTER.UP)==PNG_FILTER.UP)
				{
					up_row=new byte[rowbytes+1];
					up_row[0]=(byte)PNG_FILTER_VALUE.UP;
				}

				if((do_filter&PNG_FILTER.AVG)==PNG_FILTER.AVG)
				{
					avg_row=new byte[rowbytes+1];
					avg_row[0]=(byte)PNG_FILTER_VALUE.AVG;
				}

				if((do_filter&PNG_FILTER.PAETH)==PNG_FILTER.PAETH)
				{
					paeth_row=new byte[rowbytes+1];
					paeth_row[0]=(byte)PNG_FILTER_VALUE.PAETH;
				}
			}

			num_rows=height;
			usr_width=width;
			zstream.avail_out=zbuf_size;
			zstream.next_out=0;
			zstream.out_buf=zbuf;
		}

		// Internal use only. Called when finished processing a row of data.
		void png_write_finish_row()
		{
			int ret;

			// next row
			row_number++;

			// see if we are done
			if(row_number<num_rows) return;

			// if we get here, we've just written the last row, so we need
			// to flush the compressor
			do
			{
				// tell the compressor we are done
				ret=zlib.deflate(zstream, zlib.Z_FINISH);
				// check for an error
				if(ret==zlib.Z_OK)
				{
					// check to see if we need more room
					if(zstream.avail_out==0)
					{
						png_write_IDAT(zbuf, zbuf_size);
						zstream.next_out=0;
						zstream.out_buf=zbuf;
						zstream.avail_out=zbuf_size;
					}
				}
				else if(ret!=zlib.Z_STREAM_END)
				{
					if(zstream.msg!=null||zstream.msg.Length==0) throw new PNG_Exception(zstream.msg);
					throw new PNG_Exception("zlib error");
				}
			} while(ret!=zlib.Z_STREAM_END);

			// write any extra space
			if(zstream.avail_out<zbuf_size) png_write_IDAT(zbuf, zbuf_size-zstream.avail_out);

			zlib.deflateReset(zstream);
		}

		// This filters the row, chooses which filter to use, if it has not already
		// been specified by the application, and then writes the row out with the
		// chosen filter.
		const uint PNG_MAXSUM=(uint.MaxValue)>>1;
		const int PNG_HISHIFT=10;
		const uint PNG_LOMASK=0xffff;
		const uint PNG_HIMASK=(uint)(~PNG_LOMASK>>PNG_HISHIFT);

		#region png_write_find_filter
		unsafe void png_write_find_filter(png_row_info row_info)
		{
			PNG_FILTER filter_to_do=do_filter;
			uint row_bytes=rowbytes;
			int num_p_filters=(int)num_prev_filters;

			// find out how many bytes offset each pixel is
			uint bpp=(uint)(pixel_depth+7)>>3;

			byte[] prev_row=this.prev_row;
			byte[] best_row=this.row_buf;
			byte[] row_buf=best_row;
			uint mins=PNG_MAXSUM;

			// The prediction method we use is to find which method provides the
			// smallest value when summing the absolute values of the distances
			// from zero, using anything>=128 as negative numbers. This is known
			// as the "minimum sum of absolute differences" heuristic. Other
			// heuristics are the "weighted minimum sum of absolute differences"
			// (experimental and can in theory improve compression), and the "zlib
			// predictive" method (not implemented yet), which does test compressions
			// of lines using different filter methods, and then chooses the
			// (series of) filter(s) that give minimum compressed data size (VERY
			// computationally expensive).
			//
			// GRR 980525: consider also
			//	(1)	minimum sum of absolute differences from running average (i.e.,
			//		keep running sum of non-absolute differences&count of bytes)
			//		[track dispersion, too? restart average if dispersion too large?]
			//	(1b)minimum sum of absolute differences from sliding average, probably
			//		with window size<=deflate window (usually 32K)
			//	(2)	minimum sum of squared differences from zero or running average
			//		(i.e., ~ root-mean-square approach)

			// We don't need to test the 'no filter' case if this is the only filter
			// that has been chosen, as it doesn't actually do anything to the data.
			if((filter_to_do&PNG_FILTER.NONE)==PNG_FILTER.NONE&&filter_to_do!=PNG_FILTER.NONE)
			{
				uint sum=0;
				fixed(byte* row_buf_=row_buf)
				{
					byte* rp=row_buf_+1;

					for(uint i=0; i<row_bytes; i++, rp++)
					{
						uint v=*rp;
						sum+=(v<128)?v:256-v;
					}
				}

				if(heuristic_method==PNG_FILTER_HEURISTIC.WEIGHTED)
				{
					uint sumlo=sum&PNG_LOMASK;
					uint sumhi=(sum>>PNG_HISHIFT)&PNG_HIMASK; // Gives us some footroom

					// Reduce the sum if we match any of the previous rows
					for(int j=0; j<num_p_filters; j++)
					{
						if(prev_filters[j]==(byte)PNG_FILTER_VALUE.NONE)
						{
							sumlo=(sumlo*filter_weights[j])>>PNG.WEIGHT_SHIFT;
							sumhi=(sumhi*filter_weights[j])>>PNG.WEIGHT_SHIFT;
						}
					}

					// Factor in the cost of this filter (this is here for completeness,
					// but it makes no sense to have a "cost" for the NONE filter, as
					// it has the minimum possible computational cost-none).
					sumlo=(sumlo*filter_costs[(byte)PNG_FILTER_VALUE.NONE])>>PNG.COST_SHIFT;
					sumhi=(sumhi*filter_costs[(byte)PNG_FILTER_VALUE.NONE])>>PNG.COST_SHIFT;

					if(sumhi>PNG_HIMASK) sum=PNG_MAXSUM;
					else sum=(sumhi<<PNG_HISHIFT)+sumlo;
				}
				mins=sum;
			}

			// sub filter
			if(filter_to_do==PNG_FILTER.SUB) // it's the only filter so no testing is needed
			{
				fixed(byte* row_buf_=row_buf, sub_row_=sub_row)
				{
					byte* rp=row_buf_+1, dp=sub_row_+1, lp=row_buf_+1;
					uint i=0;
					for(; i<bpp; i++, rp++, dp++) *dp=*rp;
					for(; i<row_bytes; i++, rp++, lp++, dp++) *dp=(byte)(((int)*rp-(int)*lp)&0xff);
				}
				best_row=sub_row;
			}
			else if((filter_to_do&PNG_FILTER.SUB)==PNG_FILTER.SUB)
			{
				uint sum=0, lmins=mins;

				// We temporarily increase the "minimum sum" by the factor we
				// would reduce the sum of this filter, so that we can do the
				// early exit comparison without scaling the sum each time.
				if(heuristic_method==PNG_FILTER_HEURISTIC.WEIGHTED)
				{
					uint lmlo=lmins&PNG_LOMASK;
					uint lmhi=(lmins>>PNG_HISHIFT)&PNG_HIMASK;

					for(int j=0; j<num_p_filters; j++)
					{
						if(prev_filters[j]==(byte)PNG_FILTER_VALUE.SUB)
						{
							lmlo=(lmlo*inv_filter_weights[j])>>PNG.WEIGHT_SHIFT;
							lmhi=(lmhi*inv_filter_weights[j])>>PNG.WEIGHT_SHIFT;
						}
					}

					lmlo=(lmlo*inv_filter_costs[(byte)PNG_FILTER_VALUE.SUB])>>PNG.COST_SHIFT;
					lmhi=(lmhi*inv_filter_costs[(byte)PNG_FILTER_VALUE.SUB])>>PNG.COST_SHIFT;

					if(lmhi>PNG_HIMASK) lmins=PNG_MAXSUM;
					else lmins=(lmhi<<PNG_HISHIFT)+lmlo;
				}

				fixed(byte* row_buf_=row_buf, sub_row_=sub_row)
				{
					byte* rp=row_buf_+1, dp=sub_row_+1, lp=row_buf_+1;
					uint i=0;

					for(; i<bpp; i++, rp++, dp++)
					{
						uint v=*dp=*rp;
						sum+=(v<128)?v:256-v;
					}
					for(; i<row_bytes; i++, rp++, lp++, dp++)
					{
						uint v=*dp=(byte)(((int)*rp-(int)*lp)&0xff);
						sum+=(v<128)?v:256-v;

						if(sum>lmins) break; // We are already worse, don't continue.
					}
				}

				if(heuristic_method==PNG_FILTER_HEURISTIC.WEIGHTED)
				{
					uint sumlo=sum&PNG_LOMASK;
					uint sumhi=(sum>>PNG_HISHIFT)&PNG_HIMASK;

					for(int j=0; j<num_p_filters; j++)
					{
						if(prev_filters[j]==(byte)PNG_FILTER_VALUE.SUB)
						{
							sumlo=(sumlo*inv_filter_weights[j])>>PNG.WEIGHT_SHIFT;
							sumhi=(sumhi*inv_filter_weights[j])>>PNG.WEIGHT_SHIFT;
						}
					}

					sumlo=(sumlo*inv_filter_costs[(byte)PNG_FILTER_VALUE.SUB])>>PNG.COST_SHIFT;
					sumhi=(sumhi*inv_filter_costs[(byte)PNG_FILTER_VALUE.SUB])>>PNG.COST_SHIFT;

					if(sumhi>PNG_HIMASK) sum=PNG_MAXSUM;
					else sum=(sumhi<<PNG_HISHIFT)+sumlo;
				}

				if(sum<mins)
				{
					mins=sum;
					best_row=sub_row;
				}
			}

			// up filter
			if(filter_to_do==PNG_FILTER.UP)
			{
				fixed(byte* row_buf_=row_buf, up_row_=up_row, prev_row_=prev_row)
				{
					byte* rp=row_buf_+1, dp=up_row_+1, pp=prev_row_+1;
					for(uint i=0; i<row_bytes; i++, rp++, pp++, dp++) *dp=(byte)(((int)*rp-(int)*pp)&0xff);
				}
				best_row=up_row;
			}
			else if((filter_to_do&PNG_FILTER.UP)==PNG_FILTER.UP)
			{
				uint sum=0, lmins=mins;

				if(heuristic_method==PNG_FILTER_HEURISTIC.WEIGHTED)
				{
					uint lmlo=lmins&PNG_LOMASK;
					uint lmhi=(lmins>>PNG_HISHIFT)&PNG_HIMASK;

					for(int j=0; j<num_p_filters; j++)
					{
						if(prev_filters[j]==(byte)PNG_FILTER_VALUE.UP)
						{
							lmlo=(lmlo*inv_filter_weights[j])>>PNG.WEIGHT_SHIFT;
							lmhi=(lmhi*inv_filter_weights[j])>>PNG.WEIGHT_SHIFT;
						}
					}

					lmlo=(lmlo*inv_filter_costs[(byte)PNG_FILTER_VALUE.UP])>>PNG.COST_SHIFT;
					lmhi=(lmhi*inv_filter_costs[(byte)PNG_FILTER_VALUE.UP])>>PNG.COST_SHIFT;

					if(lmhi>PNG_HIMASK) lmins=PNG_MAXSUM;
					else lmins=(lmhi<<PNG_HISHIFT)+lmlo;
				}

				fixed(byte* row_buf_=row_buf, up_row_=up_row, prev_row_=prev_row)
				{
					byte* rp=row_buf_+1, dp=up_row_+1, pp=prev_row_+1;

					for(uint i=0; i<row_bytes; i++)
					{
						uint v=*dp++=(byte)(((int)*rp++-(int)*pp++)&0xff);
						sum+=(v<128)?v:256-v;

						if(sum>lmins) break; // We are already worse, don't continue.
					}
				}

				if(heuristic_method==PNG_FILTER_HEURISTIC.WEIGHTED)
				{
					uint sumlo=sum&PNG_LOMASK;
					uint sumhi=(sum>>PNG_HISHIFT)&PNG_HIMASK;

					for(int j=0; j<num_p_filters; j++)
					{
						if(prev_filters[j]==(byte)PNG_FILTER_VALUE.UP)
						{
							sumlo=(sumlo*filter_weights[j])>>PNG.WEIGHT_SHIFT;
							sumhi=(sumhi*filter_weights[j])>>PNG.WEIGHT_SHIFT;
						}
					}

					sumlo=(sumlo*filter_costs[(byte)PNG_FILTER_VALUE.UP])>>PNG.COST_SHIFT;
					sumhi=(sumhi*filter_costs[(byte)PNG_FILTER_VALUE.UP])>>PNG.COST_SHIFT;

					if(sumhi>PNG_HIMASK) sum=PNG_MAXSUM;
					else sum=(sumhi<<PNG_HISHIFT)+sumlo;
				}

				if(sum<mins)
				{
					mins=sum;
					best_row=up_row;
				}
			}

			// avg filter
			if(filter_to_do==PNG_FILTER.AVG)
			{
				fixed(byte* row_buf_=row_buf, avg_row_=avg_row, prev_row_=prev_row)
				{
					byte* rp=row_buf_+1, dp=avg_row_+1, pp=prev_row_+1, lp=row_buf_+1;
					uint i=0;
					for(; i<bpp; i++) *dp++=(byte)(((int)*rp++-((int)*pp++/2))&0xff);
					for(; i<row_bytes; i++) *dp++=(byte)(((int)*rp++-(((int)*pp+++(int)*lp++)/2))&0xff);
				}
				best_row=avg_row;
			}
			else if((filter_to_do&PNG_FILTER.AVG)==PNG_FILTER.AVG)
			{
				uint sum=0, lmins=mins;

				if(heuristic_method==PNG_FILTER_HEURISTIC.WEIGHTED)
				{
					uint lmlo=lmins&PNG_LOMASK;
					uint lmhi=(lmins>>PNG_HISHIFT)&PNG_HIMASK;

					for(int j=0; j<num_p_filters; j++)
					{
						if(prev_filters[j]==(byte)PNG_FILTER_VALUE.AVG)
						{
							lmlo=(lmlo*inv_filter_weights[j])>>PNG.WEIGHT_SHIFT;
							lmhi=(lmhi*inv_filter_weights[j])>>PNG.WEIGHT_SHIFT;
						}
					}

					lmlo=(lmlo*inv_filter_costs[(byte)PNG_FILTER_VALUE.AVG])>>PNG.COST_SHIFT;
					lmhi=(lmhi*inv_filter_costs[(byte)PNG_FILTER_VALUE.AVG])>>PNG.COST_SHIFT;

					if(lmhi>PNG_HIMASK) lmins=PNG_MAXSUM;
					else lmins=(lmhi<<PNG_HISHIFT)+lmlo;
				}

				fixed(byte* row_buf_=row_buf, avg_row_=avg_row, prev_row_=prev_row)
				{
					byte* rp=row_buf_+1, dp=avg_row_+1, pp=prev_row_+1, lp=row_buf_+1;
					uint i=0;

					for(; i<bpp; i++)
					{
						uint v=*dp++=(byte)(((int)*rp++-((int)*pp++/2))&0xff);
						sum+=(v<128)?v:256-v;
					}
					for(; i<row_bytes; i++)
					{
						uint v=*dp++=(byte)(((int)*rp++-(((int)*pp+++(int)*lp++)/2))&0xff);
						sum+=(v<128)?v:256-v;

						if(sum>lmins) break; // We are already worse, don't continue.
					}
				}

				if(heuristic_method==PNG_FILTER_HEURISTIC.WEIGHTED)
				{
					uint sumlo=sum&PNG_LOMASK;
					uint sumhi=(sum>>PNG_HISHIFT)&PNG_HIMASK;

					for(int j=0; j<num_p_filters; j++)
					{
						if(prev_filters[j]==(byte)PNG_FILTER_VALUE.NONE)
						{
							sumlo=(sumlo*filter_weights[j])>>PNG.WEIGHT_SHIFT;
							sumhi=(sumhi*filter_weights[j])>>PNG.WEIGHT_SHIFT;
						}
					}

					sumlo=(sumlo*filter_costs[(byte)PNG_FILTER_VALUE.AVG])>>PNG.COST_SHIFT;
					sumhi=(sumhi*filter_costs[(byte)PNG_FILTER_VALUE.AVG])>>PNG.COST_SHIFT;

					if(sumhi>PNG_HIMASK) sum=PNG_MAXSUM;
					else sum=(sumhi<<PNG_HISHIFT)+sumlo;
				}

				if(sum<mins)
				{
					mins=sum;
					best_row=avg_row;
				}
			}

			// Paeth filter
			if(filter_to_do==PNG_FILTER.PAETH)
			{
				fixed(byte* row_buf_=row_buf, paeth_row_=paeth_row, prev_row_=prev_row)
				{
					byte* rp=row_buf_+1, dp=paeth_row_+1, pp=prev_row_+1, cp=prev_row_+1, lp=row_buf_+1;
					uint i=0;
					for(; i<bpp; i++) *dp++=(byte)(((int)*rp++-(int)*pp++)&0xff);
					for(; i<row_bytes; i++)
					{
						int b=*pp++;
						int c=*cp++;
						int a=*lp++;

						int p=b-c;
						int pc=a-c;

						//int pa=abs(p);
						//int pb=abs(pc);
						//pc=abs(p+pc);
						int pa=p<0?-p:p;
						int pb=pc<0?-pc:pc;
						pc=(p+pc)<0?-(p+pc):p+pc;

						p=(pa<=pb&&pa<=pc)?a:(pb<=pc)?b:c;

						*dp++=(byte)(((int)*rp++-p)&0xff);
					}
				}
				best_row=paeth_row;
			}
			else if((filter_to_do&PNG_FILTER.PAETH)==PNG_FILTER.PAETH)
			{
				uint sum=0, lmins=mins;

				if(heuristic_method==PNG_FILTER_HEURISTIC.WEIGHTED)
				{
					uint lmlo=lmins&PNG_LOMASK;
					uint lmhi=(lmins>>PNG_HISHIFT)&PNG_HIMASK;

					for(int j=0; j<num_p_filters; j++)
					{
						if(prev_filters[j]==(byte)PNG_FILTER_VALUE.PAETH)
						{
							lmlo=(lmlo*inv_filter_weights[j])>>PNG.WEIGHT_SHIFT;
							lmhi=(lmhi*inv_filter_weights[j])>>PNG.WEIGHT_SHIFT;
						}
					}

					lmlo=(lmlo*inv_filter_costs[(byte)PNG_FILTER_VALUE.PAETH])>>PNG.COST_SHIFT;
					lmhi=(lmhi*inv_filter_costs[(byte)PNG_FILTER_VALUE.PAETH])>>PNG.COST_SHIFT;

					if(lmhi>PNG_HIMASK) lmins=PNG_MAXSUM;
					else lmins=(lmhi<<PNG_HISHIFT)+lmlo;
				}

				fixed(byte* row_buf_=row_buf, paeth_row_=paeth_row, prev_row_=prev_row)
				{
					byte* rp=row_buf_+1, dp=paeth_row_+1, pp=prev_row_+1, cp=prev_row_+1, lp=row_buf_+1;

					uint i=0;
					for(; i<bpp; i++)
					{
						uint v=*dp++=(byte)(((int)*rp++-(int)*pp++)&0xff);
						sum+=(v<128)?v:256-v;
					}

					for(; i<row_bytes; i++)
					{
						int b=*pp++;
						int c=*cp++;
						int a=*lp++;

						int p=b-c;
						int pc=a-c;
						//int pa=abs(p);
						//int pb=abs(pc);
						//pc=abs(p+pc);
						int pa=p<0?-p:p;
						int pb=pc<0?-pc:pc;
						pc=(p+pc)<0?-(p+pc):p+pc;

						p=(pa<=pb&&pa<=pc)?a:(pb<=pc)?b:c;

						uint v=*dp++=(byte)(((int)*rp++-p)&0xff);

						sum+=(v<128)?v:256-v;

						if(sum>lmins) break; // We are already worse, don't continue.
					}
				}

				if(heuristic_method==PNG_FILTER_HEURISTIC.WEIGHTED)
				{
					uint sumlo=sum&PNG_LOMASK;
					uint sumhi=(sum>>PNG_HISHIFT)&PNG_HIMASK;

					for(int j=0; j<num_p_filters; j++)
					{
						if(prev_filters[j]==(byte)PNG_FILTER_VALUE.PAETH)
						{
							sumlo=(sumlo*filter_weights[j])>>PNG.WEIGHT_SHIFT;
							sumhi=(sumhi*filter_weights[j])>>PNG.WEIGHT_SHIFT;
						}
					}

					sumlo=(sumlo*filter_costs[(byte)PNG_FILTER_VALUE.PAETH])>>PNG.COST_SHIFT;
					sumhi=(sumhi*filter_costs[(byte)PNG_FILTER_VALUE.PAETH])>>PNG.COST_SHIFT;

					if(sumhi>PNG_HIMASK) sum=PNG_MAXSUM;
					else sum=(sumhi<<PNG_HISHIFT)+sumlo;
				}

				if(sum<mins) best_row=paeth_row;
			}

			// Do the actual writing of the filtered row data from the chosen filter.
			png_write_filtered_row(best_row);

			// Save the type of filter we picked this time for future calculations
			if(num_prev_filters>0)
			{
				int j;
				for(j=1; j<num_p_filters; j++) prev_filters[j]=prev_filters[j-1];
				prev_filters[j]=best_row[0];
			}
		}
		#endregion

		// Do the actual writing of a previously filtered row.
		void png_write_filtered_row(byte[] filtered_row)
		{
			// set up the zlib input buffer
			zstream.next_in=0;
			zstream.in_buf=filtered_row;
			zstream.avail_in=row_info.rowbytes+1;

			// repeat until we have compressed all the data
			do
			{
				// compress the data
				int ret=zlib.deflate(zstream, zlib.Z_NO_FLUSH);
				// check for compression errors
				if(ret!=zlib.Z_OK)
				{
					if(zstream.msg!=null) throw new PNG_Exception(zstream.msg);
					throw new PNG_Exception("zlib error");
				}

				// see if it is time to write another IDAT
				if(zstream.avail_out==0)
				{
					// write the IDAT and reset the zlib output buffer
					png_write_IDAT(zbuf, zbuf_size);
					zstream.next_out=0;
					zstream.out_buf=zbuf;
					zstream.avail_out=zbuf_size;
				}
				// repeat until all data has been compressed
			} while(zstream.avail_in!=0);

			// swap the current and previous rows
			if(prev_row!=null)
			{
				Byte[] tptr=prev_row;
				prev_row=row_buf;
				row_buf=tptr;
			}

			// finish row-updates counters and flushes zlib if last row
			png_write_finish_row();
		}
	}
}
