// pngread.cs - read a PNG file
//
// Based on libpng version 1.4.3 - June 26, 2010
// This code is released under the libpng license.
// For conditions of distribution and use, see copyright notice in License.txt
// Copyright (C) 2007-2010 by the Authors
// Copyright (c) 1998-2010 Glenn Randers-Pehrson
// (Version 0.96 Copyright (c) 1996, 1997 Andreas Dilger)
// (Version 0.88 Copyright (c) 1995, 1996 Guy Eric Schalnat, Group 42, Inc.)
//
// This file contains routines that an application calls directly to
// read a PNG file or stream.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

using Free.Ports.zLib;

namespace Free.Ports.libpng
{
	public partial class png_struct
	{
		// Create a PNG structure for reading, and allocate any memory needed.
		public static png_struct png_create_read_struct()
		{
			try
			{
				png_struct png_ptr=new png_struct();

				try
				{
					// initialize zbuf - compression buffer
					png_ptr.zbuf_size=PNG.ZBUF_SIZE;
					png_ptr.zbuf=new byte[png_ptr.zbuf_size];

					switch(zlib.inflateInit(png_ptr.zstream))
					{
						case zlib.Z_OK: break;
						case zlib.Z_MEM_ERROR:
						case zlib.Z_STREAM_ERROR: throw new PNG_Exception("zlib memory error");
						default: throw new PNG_Exception("Unknown zlib error");
					}

					png_ptr.zstream.out_buf=png_ptr.zbuf;
					png_ptr.zstream.next_out=0;
					png_ptr.zstream.avail_out=png_ptr.zbuf_size;
				}
				catch(Exception)
				{
					png_ptr.zbuf=null;
					png_ptr=null;
				}
				return png_ptr;
			}
			catch(Exception)
			{
				return null;
			}
		}

		// Read the information before the actual image data. This has been
		// changed in v0.90 to allow reading a file that already has the magic
		// bytes read from the stream. You can tell libpng how many bytes have
		// been read from the beginning of the stream (up to the maximum of 8)
		// via png_set_sig_bytes(), and we will only check the remaining bytes
		// here. The application can then have access to the signature bytes we
		// read if it is determined that this isn't a valid PNG file.
		public void png_read_info()
		{
			// If we haven't checked all of the PNG signature bytes, do so now.
			if(sig_bytes<8)
			{
				uint num_checked=sig_bytes, num_to_check=8-num_checked;

				io_state=PNG_IO.READING|PNG_IO.SIGNATURE;

				png_read_data(info_ptr_signature, num_checked, num_to_check);
				sig_bytes=8;

				if(png_sig_cmp(info_ptr_signature, num_checked, num_to_check)!=0)
				{
					if(num_checked<4&&png_sig_cmp(info_ptr_signature, num_checked, num_to_check-4)!=0) throw new PNG_Exception("Not a PNG file");
					else throw new PNG_Exception("PNG file corrupted by ASCII conversion");
				}
				if(num_checked<3) mode|=PNG_MODE.HAVE_PNG_SIGNATURE;
			}

			for(; ; )
			{
				uint length=png_read_chunk_header();

				// This should be a binary subdivision search or a hash for
				// matching the chunk name rather than a linear search.
				if(memcmp(chunk_name, PNG.IDAT, 4)==0)
					if((mode&PNG_MODE.AFTER_IDAT)==PNG_MODE.AFTER_IDAT) mode|=PNG_MODE.HAVE_CHUNK_AFTER_IDAT;

				if(memcmp(chunk_name, PNG.IHDR, 4)==0) png_handle_IHDR(length);
				else if(memcmp(chunk_name, PNG.IEND, 4)==0) png_handle_IEND(length);
				else if(memcmp(chunk_name, PNG.PLTE, 4)==0) png_handle_PLTE(length);
				else if(memcmp(chunk_name, PNG.IDAT, 4)==0)
				{
					if((mode&PNG_MODE.HAVE_IHDR)!=PNG_MODE.HAVE_IHDR) throw new PNG_Exception("Missing IHDR before IDAT");
					else if(color_type==PNG_COLOR_TYPE.PALETTE&&(mode&PNG_MODE.HAVE_PLTE)!=PNG_MODE.HAVE_PLTE) throw new PNG_Exception("Missing PLTE before IDAT");

					idat_size=length;
					mode|=PNG_MODE.HAVE_IDAT;
					break;
				}
				else if(memcmp(chunk_name, PNG.bKGD, 4)==0) png_handle_bKGD(length);
				else if(memcmp(chunk_name, PNG.cHRM, 4)==0) png_handle_cHRM(length);
				else if(memcmp(chunk_name, PNG.gAMA, 4)==0) png_handle_gAMA(length);
				else if(memcmp(chunk_name, PNG.hIST, 4)==0) png_handle_hIST(length);
				else if(memcmp(chunk_name, PNG.oFFs, 4)==0) png_handle_oFFs(length);
				else if(memcmp(chunk_name, PNG.pCAL, 4)==0) png_handle_pCAL(length);
				else if(memcmp(chunk_name, PNG.sCAL, 4)==0) png_handle_sCAL(length);
				else if(memcmp(chunk_name, PNG.pHYs, 4)==0) png_handle_pHYs(length);
				else if(memcmp(chunk_name, PNG.sBIT, 4)==0) png_handle_sBIT(length);
				else if(memcmp(chunk_name, PNG.sRGB, 4)==0) png_handle_sRGB(length);
				else if(memcmp(chunk_name, PNG.iCCP, 4)==0) png_handle_iCCP(length);
				else if(memcmp(chunk_name, PNG.sPLT, 4)==0) png_handle_sPLT(length);
				else if(memcmp(chunk_name, PNG.tEXt, 4)==0) png_handle_tEXt(length);
				else if(memcmp(chunk_name, PNG.tIME, 4)==0) png_handle_tIME(length);
				else if(memcmp(chunk_name, PNG.tRNS, 4)==0) png_handle_tRNS(length);
				else if(memcmp(chunk_name, PNG.zTXt, 4)==0) png_handle_zTXt(length);
				else if(memcmp(chunk_name, PNG.iTXt, 4)==0) png_handle_iTXt(length);
				else if(memcmp(chunk_name, PNG.sTER, 4)==0) png_handle_sTER(length);
				else if(memcmp(chunk_name, PNG.gIFg, 4)==0) png_handle_gIFg(length);
				else if(memcmp(chunk_name, PNG.gIFx, 4)==0) png_handle_gIFx(length);
				else png_handle_unknown(length);
			}
		}

		// optional call to update the users info_ptr structure
		public void png_read_update_info()
		{
			if((flags&PNG_FLAG.ROW_INIT)!=PNG_FLAG.ROW_INIT) png_read_start_row();
			else Debug.WriteLine("Ignoring extra png_read_update_info() call; row buffer not reallocated");
			png_read_transform_info();
		}

		// Initialize palette, background, etc, after transformations
		// are set, but before any reading takes place. This allows
		// the user to obtain a gamma-corrected palette, for example.
		// If the user doesn't call this, we will do it ourselves.
		public void png_start_read_image()
		{
			if((flags&PNG_FLAG.ROW_INIT)!=PNG_FLAG.ROW_INIT) png_read_start_row();
		}

		public void png_read_row(byte[] row, byte[] dsp_row)
		{
			int[] png_pass_dsp_mask=new int[] { 0xff, 0x0f, 0xff, 0x33, 0xff, 0x55, 0xff };
			int[] png_pass_mask=new int[] { 0x80, 0x08, 0x88, 0x22, 0xaa, 0x55, 0xff };

			if((flags&PNG_FLAG.ROW_INIT)!=PNG_FLAG.ROW_INIT) png_read_start_row();

			// if interlaced and we do not need a new row, combine row and return
			if(info_ptr_interlace_type!=PNG_INTERLACE.NONE&&(transformations&PNG_TRANSFORMATION.INTERLACE)==PNG_TRANSFORMATION.INTERLACE)
			{
				switch(pass)
				{
					case 0: if((row_number&0x07)!=0)
						{
							if(dsp_row!=null) png_combine_row(dsp_row, png_pass_dsp_mask[pass]);
							png_read_finish_row();
							return;
						}
						break;
					case 1: if((row_number&0x07)!=0||width<5)
						{
							if(dsp_row!=null) png_combine_row(dsp_row, png_pass_dsp_mask[pass]);
							png_read_finish_row();
							return;
						}
						break;
					case 2: if((row_number&0x07)!=4)
						{
							if(dsp_row!=null&&(row_number&4)!=0) png_combine_row(dsp_row, png_pass_dsp_mask[pass]);
							png_read_finish_row();
							return;
						}
						break;
					case 3: if((row_number&3)!=0||width<3)
						{
							if(dsp_row!=null) png_combine_row(dsp_row, png_pass_dsp_mask[pass]);
							png_read_finish_row();
							return;
						}
						break;
					case 4: if((row_number&3)!=2)
						{
							if(dsp_row!=null&&(row_number&2)!=0) png_combine_row(dsp_row, png_pass_dsp_mask[pass]);
							png_read_finish_row();
							return;
						}
						break;
					case 5: if((row_number&1)!=0||width<2)
						{
							if(dsp_row!=null) png_combine_row(dsp_row, png_pass_dsp_mask[pass]);
							png_read_finish_row();
							return;
						}
						break;
					case 6: if((row_number&1)==0)
						{
							png_read_finish_row();
							return;
						}
						break;
				} // switch(pass)
			} // if(interlace_type!=PNG_INTERLACE.NONE...)

			if((mode&PNG_MODE.HAVE_IDAT)!=PNG_MODE.HAVE_IDAT) throw new PNG_Exception("Invalid attempt to read row data");

			zstream.out_buf=row_buf;
			zstream.next_out=0;
			zstream.avail_out=PNG_ROWBYTES(pixel_depth, iwidth)+1;

			do
			{
				if((zstream.avail_in)==0)
				{
					while(idat_size==0)
					{
						png_crc_finish(0);
						idat_size=png_read_chunk_header();

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
					if(zstream.avail_out!=0||zstream.avail_in!=0||idat_size!=0) throw new PNG_Exception("Extra compressed data");
					mode|=PNG_MODE.AFTER_IDAT;
					flags|=PNG_FLAG.ZLIB_FINISHED;
					break;
				}
				if(ret!=zlib.Z_OK) throw new PNG_Exception((zstream.msg!=null&&zstream.msg.Length>0)?zstream.msg:"Decompression error");
			} while(zstream.avail_out!=0);

			row_info.color_type=color_type;
			row_info.width=iwidth;
			row_info.channels=channels;
			row_info.bit_depth=bit_depth;
			row_info.pixel_depth=pixel_depth;
			row_info.rowbytes=PNG_ROWBYTES(row_info.pixel_depth, row_info.width);

			if(row_buf[0]!=0) png_read_filter_row(row_info, row_buf, prev_row, (PNG_FILTER_VALUE)row_buf[0]);

			//was memcpy(prev_row, row_buf, png_ptr->rowbytes+1);
			Array.Copy(row_buf, prev_row, rowbytes+1);

			if((mng_features_permitted&PNG_FLAG_MNG.FILTER_64)==PNG_FLAG_MNG.FILTER_64&&filter_type==PNG_FILTER_TYPE.INTRAPIXEL_DIFFERENCING)
			{ // Intrapixel differencing
				png_do_read_intrapixel(row_info, row_buf);
			}

			if(transformations!=PNG_TRANSFORMATION.None||(flags&PNG_FLAG.STRIP_ALPHA)==PNG_FLAG.STRIP_ALPHA) png_do_read_transformations();

			// blow up interlaced rows to full size
			if(info_ptr_interlace_type!=PNG_INTERLACE.NONE&&(transformations&PNG_TRANSFORMATION.INTERLACE)==PNG_TRANSFORMATION.INTERLACE)
			{
				if(pass<6) png_do_read_interlace();
				if(dsp_row!=null) png_combine_row(dsp_row, png_pass_dsp_mask[pass]);
				if(row!=null) png_combine_row(row, png_pass_mask[pass]);
			}
			else
			{
				if(row!=null) png_combine_row(row, 0xff);
				if(dsp_row!=null) png_combine_row(dsp_row, 0xff);
			}
			png_read_finish_row();

			if(read_row_fn!=null) read_row_fn(this, row_number, pass);
		}

		public unsafe void png_read_row(byte* row, byte* dsp_row)
		{
			int[] png_pass_dsp_mask=new int[] { 0xff, 0x0f, 0xff, 0x33, 0xff, 0x55, 0xff };
			int[] png_pass_mask=new int[] { 0x80, 0x08, 0x88, 0x22, 0xaa, 0x55, 0xff };

			if((flags&PNG_FLAG.ROW_INIT)!=PNG_FLAG.ROW_INIT) png_read_start_row();

			// if interlaced and we do not need a new row, combine row and return
			if(info_ptr_interlace_type!=PNG_INTERLACE.NONE&&(transformations&PNG_TRANSFORMATION.INTERLACE)==PNG_TRANSFORMATION.INTERLACE)
			{
				switch(pass)
				{
					case 0: if((row_number&0x07)!=0)
						{
							if(dsp_row!=null) png_combine_row(dsp_row, png_pass_dsp_mask[pass]);
							png_read_finish_row();
							return;
						}
						break;
					case 1: if((row_number&0x07)!=0||width<5)
						{
							if(dsp_row!=null) png_combine_row(dsp_row, png_pass_dsp_mask[pass]);
							png_read_finish_row();
							return;
						}
						break;
					case 2: if((row_number&0x07)!=4)
						{
							if(dsp_row!=null&&(row_number&4)!=0) png_combine_row(dsp_row, png_pass_dsp_mask[pass]);
							png_read_finish_row();
							return;
						}
						break;
					case 3: if((row_number&3)!=0||width<3)
						{
							if(dsp_row!=null) png_combine_row(dsp_row, png_pass_dsp_mask[pass]);
							png_read_finish_row();
							return;
						}
						break;
					case 4: if((row_number&3)!=2)
						{
							if(dsp_row!=null&&(row_number&2)!=0) png_combine_row(dsp_row, png_pass_dsp_mask[pass]);
							png_read_finish_row();
							return;
						}
						break;
					case 5: if((row_number&1)!=0||width<2)
						{
							if(dsp_row!=null) png_combine_row(dsp_row, png_pass_dsp_mask[pass]);
							png_read_finish_row();
							return;
						}
						break;
					case 6: if((row_number&1)==0)
						{
							png_read_finish_row();
							return;
						}
						break;
				}
			} // switch(pass)

			if((mode&PNG_MODE.HAVE_IDAT)!=PNG_MODE.HAVE_IDAT) throw new PNG_Exception("Invalid attempt to read row data");

			zstream.out_buf=row_buf;
			zstream.next_out=0;
			zstream.avail_out=PNG_ROWBYTES(pixel_depth, iwidth)+1;

			do
			{
				if((zstream.avail_in)==0)
				{
					while(idat_size==0)
					{
						png_crc_finish(0);
						idat_size=png_read_chunk_header();

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
					if(zstream.avail_out!=0||zstream.avail_in!=0||idat_size!=0) throw new PNG_Exception("Extra compressed data");
					mode|=PNG_MODE.AFTER_IDAT;
					flags|=PNG_FLAG.ZLIB_FINISHED;
					break;
				}
				if(ret!=zlib.Z_OK) throw new PNG_Exception((zstream.msg!=null&&zstream.msg.Length>0)?zstream.msg:"Decompression error");
			} while(zstream.avail_out!=0);

			row_info.color_type=color_type;
			row_info.width=iwidth;
			row_info.channels=channels;
			row_info.bit_depth=bit_depth;
			row_info.pixel_depth=pixel_depth;
			row_info.rowbytes=PNG_ROWBYTES(row_info.pixel_depth, row_info.width);

			if(row_buf[0]!=0) png_read_filter_row(row_info, row_buf, prev_row, (PNG_FILTER_VALUE)row_buf[0]);

			//was memcpy(prev_row, row_buf, png_ptr->rowbytes+1);
			Array.Copy(row_buf, prev_row, rowbytes+1);

			if((mng_features_permitted&PNG_FLAG_MNG.FILTER_64)==PNG_FLAG_MNG.FILTER_64&&filter_type==PNG_FILTER_TYPE.INTRAPIXEL_DIFFERENCING)
			{ // Intrapixel differencing
				png_do_read_intrapixel(row_info, row_buf);
			}

			if(transformations!=PNG_TRANSFORMATION.None||(flags&PNG_FLAG.STRIP_ALPHA)==PNG_FLAG.STRIP_ALPHA) png_do_read_transformations();

			// blow up interlaced rows to full size
			if(info_ptr_interlace_type!=PNG_INTERLACE.NONE&&(transformations&PNG_TRANSFORMATION.INTERLACE)==PNG_TRANSFORMATION.INTERLACE)
			{
				if(pass<6) png_do_read_interlace();
				if(dsp_row!=null) png_combine_row(dsp_row, png_pass_dsp_mask[pass]);
				if(row!=null) png_combine_row(row, png_pass_mask[pass]);
			}
			else
			{
				if(row!=null) png_combine_row(row, 0xff);
				if(dsp_row!=null) png_combine_row(dsp_row, 0xff);
			}
			png_read_finish_row();

			if(read_row_fn!=null) read_row_fn(this, row_number, pass);
		}

		// Read one or more rows of image data. If the image is interlaced,
		// and png_set_interlace_handling() has been called, the rows need to
		// contain the contents of the rows from the previous pass. If the
		// image has alpha or transparency, and png_handle_alpha()[*] has been
		// called, the rows contents must be initialized to the contents of the
		// screen.
		//
		// "row" holds the actual image, and pixels are placed in it
		// as they arrive. If the image is displayed after each pass, it will
		// appear to "sparkle" in. "display_row" can be used to display a
		// "chunky" progressive image, with finer detail added as it becomes
		// available. If you do not want this "chunky" display, you may pass
		// NULL for display_row. If you do not want the sparkle display, and
		// you have not called png_handle_alpha(), you may pass NULL for rows.
		// If you have called png_handle_alpha(), and the image has either an
		// alpha channel or a transparency chunk, you must provide a buffer for
		// rows. In this case, you do not have to provide a display_row buffer
		// also, but you may. If the image is not interlaced, or if you have
		// not called png_set_interlace_handling(), the display_row buffer will
		// be ignored, so pass NULL to it.
		//
		// [*] png_handle_alpha() does not exist yet, as of this version of libpng
		public void png_read_rows(byte[][] row, byte[][] display_row, uint num_rows)
		{
			byte[][] rp=row;
			byte[][] dp=display_row;
			if(rp!=null&&dp!=null)
			{
				if(rp.Length==0||dp.Length==0) throw new PNG_Exception("row or display_row buffer empty");
				if(dp.Length!=rp.Length) throw new PNG_Exception("row buffers have not the same size");
				for(uint i=0; i<rp.Length; i++)
				{
					byte[] rptr=rp[i];
					byte[] dptr=dp[i];
					png_read_row(rptr, dptr);
				}
			}
			else if(rp!=null)
			{
				if(rp.Length==0) throw new PNG_Exception("row buffer empty");
				for(uint i=0; i<rp.Length; i++)
				{
					byte[] rptr=rp[i];
					png_read_row(rptr, null);
				}
			}
			else if(dp!=null)
			{
				if(dp.Length==0) throw new PNG_Exception("display_row buffer empty");
				for(uint i=0; i<dp.Length; i++)
				{
					byte[] dptr=dp[i];
					png_read_row(null, dptr);
				}
			}
		}

		// Read the entire image. If the image has an alpha channel or a tRNS
		// chunk, and you have called png_handle_alpha()[*], you will need to
		// initialize the image to the current image that PNG will be overlaying.
		// We set the num_rows again here, in case it was incorrectly set in
		// png_read_start_row() by a call to png_read_update_info() or
		// png_start_read_image() if png_set_interlace_handling() wasn't called
		// prior to either of these functions like it should have been. You can
		// only call this function once. If you desire to have an image for
		// each pass of a interlaced image, use png_read_rows() instead.
		//
		// [*] png_handle_alpha() does not exist yet, as of this version of libpng
		public void png_read_image(byte[][] image)
		{
			int pass=png_set_interlace_handling();
			uint image_height=height;
			num_rows=image_height;	// Make sure this is set correctly

			for(int j=0; j<pass; j++)
			{
				for(uint i=0; i<image_height; i++) png_read_row(image[i], null);
			}
		}

		public unsafe void png_read_image(byte*[] image)
		{
			int pass=png_set_interlace_handling();
			uint image_height=height;
			num_rows=image_height;	// Make sure this is set correctly

			for(int j=0; j<pass; j++)
			{
				for(uint i=0; i<image_height; i++) png_read_row(image[i], null);
			}
		}

		// Read the end of the PNG file. Will not read past the end of the
		// file, will verify the end is accurate, and will read any comments
		// or time information at the end of the file, if info is not NULL.
		public void png_read_end()
		{
			png_crc_finish(0); // Finish off CRC from last IDAT chunk

			do
			{
				uint length=png_read_chunk_header();

				if(memcmp(chunk_name, PNG.IHDR, 4)==0) png_handle_IHDR(length);
				else if(memcmp(chunk_name, PNG.IEND, 4)==0) png_handle_IEND(length);
				else if(memcmp(chunk_name, PNG.IDAT, 4)==0)
				{
					// Zero length IDATs are legal after the last IDAT has been
					// read, but not after other chunks have been read.
					if((length>0)||(mode&PNG_MODE.HAVE_CHUNK_AFTER_IDAT)==PNG_MODE.HAVE_CHUNK_AFTER_IDAT)
						throw new PNG_Exception("Too many IDAT's found");
					png_crc_finish(length);
				}
				else if(memcmp(chunk_name, PNG.PLTE, 4)==0) png_handle_PLTE(length);
				else if(memcmp(chunk_name, PNG.bKGD, 4)==0) png_handle_bKGD(length);
				else if(memcmp(chunk_name, PNG.cHRM, 4)==0) png_handle_cHRM(length);
				else if(memcmp(chunk_name, PNG.gAMA, 4)==0) png_handle_gAMA(length);
				else if(memcmp(chunk_name, PNG.hIST, 4)==0) png_handle_hIST(length);
				else if(memcmp(chunk_name, PNG.oFFs, 4)==0) png_handle_oFFs(length);
				else if(memcmp(chunk_name, PNG.pCAL, 4)==0) png_handle_pCAL(length);
				else if(memcmp(chunk_name, PNG.sCAL, 4)==0) png_handle_sCAL(length);
				else if(memcmp(chunk_name, PNG.pHYs, 4)==0) png_handle_pHYs(length);
				else if(memcmp(chunk_name, PNG.sBIT, 4)==0) png_handle_sBIT(length);
				else if(memcmp(chunk_name, PNG.sRGB, 4)==0) png_handle_sRGB(length);
				else if(memcmp(chunk_name, PNG.iCCP, 4)==0) png_handle_iCCP(length);
				else if(memcmp(chunk_name, PNG.sPLT, 4)==0) png_handle_sPLT(length);
				else if(memcmp(chunk_name, PNG.tEXt, 4)==0) png_handle_tEXt(length);
				else if(memcmp(chunk_name, PNG.tIME, 4)==0) png_handle_tIME(length);
				else if(memcmp(chunk_name, PNG.tRNS, 4)==0) png_handle_tRNS(length);
				else if(memcmp(chunk_name, PNG.zTXt, 4)==0) png_handle_zTXt(length);
				else if(memcmp(chunk_name, PNG.iTXt, 4)==0) png_handle_iTXt(length);
				else if(memcmp(chunk_name, PNG.sTER, 4)==0) png_handle_sTER(length);
				else if(memcmp(chunk_name, PNG.gIFg, 4)==0) png_handle_gIFg(length);
				else if(memcmp(chunk_name, PNG.gIFx, 4)==0) png_handle_gIFx(length);
				else png_handle_unknown(length);
			} while((mode&PNG_MODE.HAVE_IEND)!=PNG_MODE.HAVE_IEND);
		}

		// free all memory used by the read
		public void png_destroy_read_struct()
		{
			png_read_destroy();
		}

		// free all memory used by the read (old method)
		public void png_read_destroy()
		{
			zbuf=null;
			row_buf=null;
			prev_row=null;
			palette_lookup=null;
			quantize_index=null;
			gamma_table=null;
			gamma_from_1=null;
			gamma_to_1=null;

			palette=null;
			trans_alpha=null;
			hist=null;

			if(gamma_16_table!=null)
			{
				int istop=(1<<(8-gamma_shift));
				for(int i=0; i<istop; i++) gamma_16_table[i]=null;
				gamma_16_table=null;
			}

			if(gamma_16_from_1!=null)
			{
				int istop=(1<<(8-gamma_shift));
				for(int i=0; i<istop; i++) gamma_16_from_1[i]=null;
				gamma_16_from_1=null;
			}

			if(gamma_16_to_1!=null)
			{
				int istop=(1<<(8-gamma_shift));
				for(int i=0; i<istop; i++) gamma_16_to_1[i]=null;
				gamma_16_to_1=null;
			}

			zlib.inflateEnd(zstream);
		}

		public void png_set_read_status_fn(png_read_status_ptr read_row_fn)
		{
			this.read_row_fn=read_row_fn;
		}
	}
}
