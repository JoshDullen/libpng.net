// pngwrite.cs - general routines to write a PNG file
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

using System.Runtime.InteropServices;
using Free.Ports.zLib;

namespace Free.Ports.libpng
{
	public partial class png_struct
	{
		// Writes all the PNG information. This is the suggested way to use the
		// library. If you have a new chunk to add, make a function to write it,
		// and put it in the correct location here. If you want the chunk written
		// after the image data, put it in png_write_end(). I strongly encourage
		// you to supply a PNG_INFO_ flag, and check info_ptr->valid before writing
		// the chunk, as that will keep the code from breaking if you want to just
		// write a plain PNG file. If you have long comments, I suggest writing
		// them in png_write_end(), and compressing them.
		public void png_write_info_before_PLTE()
		{
			if((mode&PNG_MODE.WROTE_INFO_BEFORE_PLTE)==PNG_MODE.WROTE_INFO_BEFORE_PLTE) return;

			png_write_sig(); // write PNG signature
			if((mode&PNG_MODE.HAVE_PNG_SIGNATURE)==PNG_MODE.HAVE_PNG_SIGNATURE&&(mng_features_permitted!=PNG_FLAG_MNG.None))
			{
				Debug.WriteLine("MNG features are not allowed in a PNG datastream");
				mng_features_permitted=PNG_FLAG_MNG.None;
			}

			// write IHDR information.
			png_write_IHDR(info_ptr_width, info_ptr_height, info_ptr_bit_depth, info_ptr_color_type, info_ptr_compression_type, info_ptr_filter_type, PNG_INTERLACE.NONE);
			// the rest of these check to see if the valid field has the appropriate flag set, and if it does, writes the chunk.
			if((info_ptr_valid&PNG_INFO.gAMA)==PNG_INFO.gAMA) png_write_gAMA(info_ptr_gamma);
			if((info_ptr_valid&PNG_INFO.sRGB)==PNG_INFO.sRGB) png_write_sRGB(info_ptr_srgb_intent);
			if((info_ptr_valid&PNG_INFO.iCCP)==PNG_INFO.iCCP) png_write_iCCP(info_ptr_iccp_name, PNG_COMPRESSION_TYPE.BASE, info_ptr_iccp_profile);
			if((info_ptr_valid&PNG_INFO.sBIT)==PNG_INFO.sBIT) png_write_sBIT(info_ptr_sig_bit, info_ptr_color_type);
			if((info_ptr_valid&PNG_INFO.cHRM)==PNG_INFO.cHRM) png_write_cHRM(info_ptr_x_white, info_ptr_y_white, info_ptr_x_red, info_ptr_y_red, info_ptr_x_green, info_ptr_y_green, info_ptr_x_blue, info_ptr_y_blue);
			mode|=PNG_MODE.WROTE_INFO_BEFORE_PLTE;
		}

		public void png_write_info()
		{
			png_write_info_before_PLTE();

			if((info_ptr_valid&PNG_INFO.PLTE)==PNG_INFO.PLTE) png_write_PLTE(info_ptr_palette);
			else if(info_ptr_color_type==PNG_COLOR_TYPE.PALETTE) throw new PNG_Exception("Valid palette required for paletted images");

			if((info_ptr_valid&PNG_INFO.tRNS)==PNG_INFO.tRNS)
			{
				// invert the alpha channel (in tRNS)
				if((transformations&PNG_TRANSFORMATION.INVERT_ALPHA)==PNG_TRANSFORMATION.INVERT_ALPHA&&
					info_ptr_color_type==PNG_COLOR_TYPE.PALETTE)
				{
					for(int j=0; j<info_ptr_num_trans; j++) info_ptr_trans_alpha[j]=(byte)(255-info_ptr_trans_alpha[j]);
				}
				png_write_tRNS(info_ptr_trans_alpha, info_ptr_trans_color, info_ptr_num_trans, info_ptr_color_type);
			}
			if((info_ptr_valid&PNG_INFO.bKGD)==PNG_INFO.bKGD) png_write_bKGD(info_ptr_background, info_ptr_color_type);
			if((info_ptr_valid&PNG_INFO.hIST)==PNG_INFO.hIST) png_write_hIST(info_ptr_hist);
			if((info_ptr_valid&PNG_INFO.oFFs)==PNG_INFO.oFFs) png_write_oFFs(info_ptr_x_offset, info_ptr_y_offset, info_ptr_offset_unit_type);
			if((info_ptr_valid&PNG_INFO.pCAL)==PNG_INFO.pCAL) png_write_pCAL(info_ptr_pcal_purpose, info_ptr_pcal_X0, info_ptr_pcal_X1, info_ptr_pcal_type, info_ptr_pcal_units, info_ptr_pcal_params);
			if((info_ptr_valid&PNG_INFO.sCAL)==PNG_INFO.sCAL) png_write_sCAL(info_ptr_scal_unit, info_ptr_scal_pixel_width, info_ptr_scal_pixel_height);
			if((info_ptr_valid&PNG_INFO.pHYs)==PNG_INFO.pHYs) png_write_pHYs(info_ptr_x_pixels_per_unit, info_ptr_y_pixels_per_unit, info_ptr_phys_unit_type);
			if((info_ptr_valid&PNG_INFO.sPLT)==PNG_INFO.sPLT)
			{
				for(int i=0; i<info_ptr_splt_palettes.Count; i++) png_write_sPLT(info_ptr_splt_palettes[i]);
			}
		}

		// Writes the end of the PNG file. Texts and tIME.
		public void png_write_end()
		{
			if((mode&PNG_MODE.HAVE_IDAT)!=PNG_MODE.HAVE_IDAT) throw new PNG_Exception("No IDATs written into file");

			// check to see if user has supplied a time chunk
			if((info_ptr_valid&PNG_INFO.tIME)==PNG_INFO.tIME) png_write_tIME(info_ptr_mod_time);

			// Check to see if we need to write text chunks
			if(info_ptr_text!=null)
			{
				for(int i=0; i<info_ptr_text.Count; i++)
				{
					// an internationalized chunk?
					if(info_ptr_text[i].compression>0)
					{
						// write international chunk
						png_write_iTXt(info_ptr_text[i].compression, info_ptr_text[i].key, info_ptr_text[i].lang, info_ptr_text[i].lang_key, info_ptr_text[i].text);

						// Mark this chunk as written
						png_text t=info_ptr_text[i];
						t.compression=PNG_TEXT_COMPRESSION.NONE_WR;
						info_ptr_text[i]=t;
					}
					// If we want a compressed text chunk
					else if(info_ptr_text[i].compression==PNG_TEXT_COMPRESSION.zTXt)
					{
						// write compressed chunk
						png_write_zTXt(info_ptr_text[i].key, info_ptr_text[i].text, info_ptr_text[i].compression);

						// Mark this chunk as written
						png_text t=info_ptr_text[i];
						t.compression=PNG_TEXT_COMPRESSION.zTXt_WR;
						info_ptr_text[i]=t;
					}
					else if(info_ptr_text[i].compression==PNG_TEXT_COMPRESSION.NONE)
					{
						// write uncompressed chunk
						png_write_tEXt(info_ptr_text[i].key, info_ptr_text[i].text);

						// Mark this chunk as written
						png_text t=info_ptr_text[i];
						t.compression=PNG_TEXT_COMPRESSION.NONE_WR;
						info_ptr_text[i]=t;
					}
				}
			}

			mode|=PNG_MODE.AFTER_IDAT;

			// write end of PNG file
			png_write_IEND();
		}

		public void png_convert_from_DateTime(ref png_time ptime, DateTime ttime)
		{
			ptime.year=(ushort)ttime.Year;
			ptime.month=(byte)(ttime.Month+1);
			ptime.day=(byte)ttime.Day;
			ptime.hour=(byte)ttime.Hour;
			ptime.minute=(byte)ttime.Minute;
			ptime.second=(byte)ttime.Second;
		}

		// Initialize png_ptr structure, and allocate any memory needed
		public static png_struct png_create_write_struct()
		{
			try
			{
				png_struct png_ptr=new png_struct();

				try
				{
					// initialize zbuf-compression buffer
					png_ptr.zbuf_size=PNG.ZBUF_SIZE;
					png_ptr.zbuf=new byte[png_ptr.zbuf_size];

					png_ptr.png_set_filter_heuristics(PNG_FILTER_HEURISTIC.DEFAULT, 1, null, null);
				}
				catch(Exception)
				{
					png_ptr.zbuf=null;
					png_ptr=null;
					return null;
				}
				return png_ptr;
			}
			catch(Exception)
			{
				return null;
			}
		}

		// Write a few rows of image data. If the image is interlaced,
		// either you will have to write the 7 sub images, or, if you
		// have called png_set_interlace_handling(), you will have to
		// "write" the image seven times.
		public void png_write_rows(byte[][] row, uint num_rows)
		{
			// loop through the rows
			for(uint i=0; i<num_rows; i++) png_write_row(row[i]);
		}

		// Write the image. You only need to call this function once, even
		// if you are writing an interlaced image.
		public void png_write_image(byte[][] image)
		{
			// loop through the image
			for(uint i=0; i<height; i++) png_write_row(image[i]);
		}

		public unsafe void png_write_image(byte*[] image)
		{
			for(uint i=0; i<height; i++) png_write_row(image[i]);
		}

		// called by user to write a row of image data
		public void png_write_row(byte[] row)
		{
			// initialize transformations and other stuff if first time
			if(row_number==0&&pass==0)
			{
				// make sure we wrote the header info
				if((mode&PNG_MODE.WROTE_INFO_BEFORE_PLTE)!=PNG_MODE.WROTE_INFO_BEFORE_PLTE) throw new PNG_Exception("png_write_info was never called before png_write_row");

				png_write_start_row();
			}

			// set up row info for transformations
			row_info.color_type=color_type;
			row_info.width=usr_width;
			row_info.channels=usr_channels;
			row_info.bit_depth=usr_bit_depth;
			row_info.pixel_depth=(byte)(row_info.bit_depth*row_info.channels);
			row_info.rowbytes=PNG_ROWBYTES(row_info.pixel_depth, row_info.width);

			// Copy user's row into buffer, leaving room for filter byte.
			Array.Copy(row, 0, row_buf, 1, row_info.rowbytes);

			// handle other transformations
			if(transformations!=PNG_TRANSFORMATION.None) png_do_write_transformations();

			// Write filter_method 64 (intrapixel differencing) only if
			// 1.	Libpng did not write a PNG signature (this filter_method is only
			//		used in PNG datastreams that are embedded in MNG datastreams) and
			// 2.	The application called png_permit_mng_features with a mask that
			//		included PNG_FLAG_MNG.FILTER_64 and
			// 3.	The filter_method is 64 and
			// 4.	The color_type is RGB or RGBA
			if((mng_features_permitted&PNG_FLAG_MNG.FILTER_64)==PNG_FLAG_MNG.FILTER_64&&filter_type==PNG_FILTER_TYPE.INTRAPIXEL_DIFFERENCING)
				png_do_write_intrapixel(row_info, row_buf); // Intrapixel differencing

			// Find a filter if necessary, filter the row and write it out.
			png_write_find_filter(row_info);

			if(write_row_fn!=null) write_row_fn(this, row_number, pass);
		}

		public unsafe void png_write_row(byte* row)
		{
			// initialize transformations and other stuff if first time
			if(row_number==0&&pass==0)
			{
				// make sure we wrote the header info
				if((mode&PNG_MODE.WROTE_INFO_BEFORE_PLTE)!=PNG_MODE.WROTE_INFO_BEFORE_PLTE) throw new PNG_Exception("png_write_info was never called before png_write_row");

				png_write_start_row();
			}

			// set up row info for transformations
			row_info.color_type=color_type;
			row_info.width=usr_width;
			row_info.channels=usr_channels;
			row_info.bit_depth=usr_bit_depth;
			row_info.pixel_depth=(byte)(row_info.bit_depth*row_info.channels);
			row_info.rowbytes=PNG_ROWBYTES(row_info.pixel_depth, row_info.width);

			// Copy user's row into buffer, leaving room for filter byte.
			Marshal.Copy((IntPtr)row, row_buf, 1, (int)row_info.rowbytes);

			// handle other transformations
			if(transformations!=PNG_TRANSFORMATION.None) png_do_write_transformations();

			// Write filter_method 64 (intrapixel differencing) only if
			// 1.	Libpng did not write a PNG signature (this filter_method is only
			//		used in PNG datastreams that are embedded in MNG datastreams) and
			// 2.	The application called png_permit_mng_features with a mask that
			//		included PNG_FLAG_MNG.FILTER_64 and
			// 3.	The filter_method is 64 and
			// 4.	The color_type is RGB or RGBA
			if((mng_features_permitted&PNG_FLAG_MNG.FILTER_64)==PNG_FLAG_MNG.FILTER_64&&filter_type==PNG_FILTER_TYPE.INTRAPIXEL_DIFFERENCING)
				png_do_write_intrapixel(row_info, row_buf); // Intrapixel differencing

			// Find a filter if necessary, filter the row and write it out.
			png_write_find_filter(row_info);

			if(write_row_fn!=null) write_row_fn(this, row_number, pass);
		}

		// free all memory used by the write
		public void png_destroy_write_struct()
		{
			png_write_destroy();
		}

		// Free any memory used in png_ptr struct (old method)
		public void png_write_destroy()
		{
			// free any memory zlib uses
			zlib.deflateEnd(zstream);

			// free our memory. png_free checks NULL for us.
			zbuf=null;
			row_buf=null;
			prev_row=null;
			sub_row=null;
			up_row=null;
			avg_row=null;
			paeth_row=null;
			prev_filters=null;
			filter_weights=null;
			inv_filter_weights=null;
			filter_costs=null;
			inv_filter_costs=null;
		}

		// Allow the application to select one or more row filters to use.
		public void png_set_filter(PNG_FILTER_TYPE method, PNG_FILTER filters)
		{
			if((mng_features_permitted&PNG_FLAG_MNG.FILTER_64)==PNG_FLAG_MNG.FILTER_64&&
				method==PNG_FILTER_TYPE.INTRAPIXEL_DIFFERENCING) method=PNG_FILTER_TYPE.BASE;

			if(method!=PNG_FILTER_TYPE.BASE) throw new PNG_Exception("Unknown custom filter method");

			if((filters&PNG_FILTER.ALL)==PNG_FILTER.None)
			{
				Debug.WriteLine("Unknown row filter for method 0");
				do_filter=PNG_FILTER.NONE;
			}
			else do_filter=filters;

			png_set_filter2();
		}

		public void png_set_filter(PNG_FILTER_TYPE method, PNG_FILTER_VALUE filters)
		{
			if((mng_features_permitted&PNG_FLAG_MNG.FILTER_64)==PNG_FLAG_MNG.FILTER_64&&
				method==PNG_FILTER_TYPE.INTRAPIXEL_DIFFERENCING) method=PNG_FILTER_TYPE.BASE;

			if(method!=PNG_FILTER_TYPE.BASE) throw new PNG_Exception("Unknown custom filter method");
			switch(filters)
			{
				case PNG_FILTER_VALUE.NONE: do_filter=PNG_FILTER.NONE; break;
				case PNG_FILTER_VALUE.SUB: do_filter=PNG_FILTER.SUB; break;
				case PNG_FILTER_VALUE.UP: do_filter=PNG_FILTER.UP; break;
				case PNG_FILTER_VALUE.AVG: do_filter=PNG_FILTER.AVG; break;
				case PNG_FILTER_VALUE.PAETH: do_filter=PNG_FILTER.PAETH; break;
				default: Debug.WriteLine("Unknown row filter for method 0"); do_filter=PNG_FILTER.NONE; break;
			}

			png_set_filter2();
		}

		void png_set_filter2()
		{
			// If we have allocated the row_buf, this means we have already started
			// with the image and we should have allocated all of the filter buffers
			// that have been selected. If prev_row isn't already allocated, then
			// it is too late to start using the filters that need it, since we
			// will be missing the data in the previous row. If an application
			// wants to start and stop using particular filters during compression,
			// it should start out with all of the filters, and then add and
			// remove them after the start of compression.
			if(row_buf==null) return;

			if((do_filter&PNG_FILTER.SUB)==PNG_FILTER.SUB&&sub_row==null)
			{
				sub_row=new byte[rowbytes+1];
				sub_row[0]=(byte)PNG_FILTER_VALUE.SUB;
			}

			if((do_filter&PNG_FILTER.UP)==PNG_FILTER.UP&&up_row==null)
			{
				if(prev_row==null)
				{
					Debug.WriteLine("Can't add Up filter after starting");
					do_filter&=~PNG_FILTER.UP;
				}
				else
				{
					up_row=new byte[rowbytes+1];
					up_row[0]=(byte)PNG_FILTER_VALUE.UP;
				}
			}

			if((do_filter&PNG_FILTER.AVG)==PNG_FILTER.AVG&&avg_row==null)
			{
				if(prev_row==null)
				{
					Debug.WriteLine("Can't add Average filter after starting");
					do_filter&=~PNG_FILTER.AVG;
				}
				else
				{
					avg_row=new byte[rowbytes+1];
					avg_row[0]=(byte)PNG_FILTER_VALUE.AVG;
				}
			}

			if((do_filter&PNG_FILTER.PAETH)==PNG_FILTER.PAETH&&paeth_row==null)
			{
				if(prev_row==null)
				{
					Debug.WriteLine("Can't add Paeth filter after starting");
					do_filter&=~PNG_FILTER.PAETH;
				}
				else
				{
					paeth_row=new byte[rowbytes+1];
					paeth_row[0]=(byte)PNG_FILTER_VALUE.PAETH;
				}
			}

			if(do_filter==PNG_FILTER.NO) do_filter=PNG_FILTER.NONE;
		}

		// This allows us to influence the way in which libpng chooses the "best"
		// filter for the current scanline. While the "minimum-sum-of-absolute-
		// differences metric is relatively fast and effective, there is some
		// question as to whether it can be improved upon by trying to keep the
		// filtered data going to zlib more consistent, hopefully resulting in
		// better compression.
		public void png_set_filter_heuristics(PNG_FILTER_HEURISTIC heuristic_method, int num_weights, double[] filter_weights, double[] filter_costs)
		{
			if(heuristic_method>=PNG_FILTER_HEURISTIC.LAST)
			{
				Debug.WriteLine("Unknown filter heuristic method");
				return;
			}

			if(heuristic_method==PNG_FILTER_HEURISTIC.DEFAULT) heuristic_method=PNG_FILTER_HEURISTIC.UNWEIGHTED;

			if(num_weights<0||filter_weights==null||heuristic_method==PNG_FILTER_HEURISTIC.UNWEIGHTED) num_weights=0;

			num_prev_filters=(byte)num_weights;
			this.heuristic_method=heuristic_method;

			if(num_weights>0)
			{
				if(prev_filters==null)
				{
					prev_filters=new byte[num_weights];

					// To make sure that the weighting starts out fairly
					for(int i=0; i<num_weights; i++) prev_filters[i]=255;
				}

				if(this.filter_weights==null)
				{
					this.filter_weights=new ushort[num_weights];

					inv_filter_weights=new ushort[num_weights];
					for(int i=0; i<num_weights; i++) inv_filter_weights[i]=this.filter_weights[i]=PNG.WEIGHT_FACTOR;
				}

				for(int i=0; i<num_weights; i++)
				{
					if(filter_weights[i]<0.0)
					{
						inv_filter_weights[i]=this.filter_weights[i]=PNG.WEIGHT_FACTOR;
					}
					else
					{
						inv_filter_weights[i]=(ushort)(PNG.WEIGHT_FACTOR*filter_weights[i]+0.5);
						this.filter_weights[i]=(ushort)(PNG.WEIGHT_FACTOR/filter_weights[i]+0.5);
					}
				}
			}

			// If, in the future, there are other filter methods, this would
			// need to be based on png_ptr->filter.
			if(this.filter_costs==null)
			{
				this.filter_costs=new ushort[(int)PNG_FILTER_VALUE.LAST];
				inv_filter_costs=new ushort[(int)PNG_FILTER_VALUE.LAST];

				for(int i=0; i<(int)PNG_FILTER_VALUE.LAST; i++) inv_filter_costs[i]=this.filter_costs[i]=PNG.COST_FACTOR;
			}

			// Here is where we set the relative costs of the different filters. We
			// should take the desired compression level into account when setting
			// the costs, so that Paeth, for instance, has a high relative cost at low
			// compression levels, while it has a lower relative cost at higher
			// compression settings. The filter types are in order of increasing
			// relative cost, so it would be possible to do this with an algorithm.
			for(int i=0; i<(int)PNG_FILTER_VALUE.LAST; i++)
			{
				if(filter_costs==null||filter_costs[i]<0.0)
				{
					inv_filter_costs[i]=this.filter_costs[i]=PNG.COST_FACTOR;
				}
				else if(filter_costs[i]>=1.0)
				{
					inv_filter_costs[i]=(ushort)(PNG.COST_FACTOR/filter_costs[i]+0.5);
					this.filter_costs[i]=(ushort)(PNG.COST_FACTOR*filter_costs[i]+0.5);
				}
			}
		}

		public void png_set_compression_level(int level)
		{
			flags|=PNG_FLAG.ZLIB_CUSTOM_LEVEL;
			zlib_level=level;
		}

		public void png_set_compression_mem_level(int mem_level)
		{
			flags|=PNG_FLAG.ZLIB_CUSTOM_MEM_LEVEL;
			zlib_mem_level=mem_level;
		}

		public void png_set_compression_strategy(int strategy)
		{
			flags|=PNG_FLAG.ZLIB_CUSTOM_STRATEGY;
			zlib_strategy=strategy;
		}

		public void png_set_compression_window_bits(int window_bits)
		{
			if(window_bits>15) Debug.WriteLine("Only compression windows <= 32k supported by PNG");
			else if(window_bits<9) Debug.WriteLine("Only compression windows >= 512 supported by PNG");
			flags|=PNG_FLAG.ZLIB_CUSTOM_WINDOW_BITS;
			zlib_window_bits=window_bits;
		}

		public void png_set_compression_method(int method)
		{
			if(method!=8) Debug.WriteLine("Only compression method 8 is supported by PNG");
			flags|=PNG_FLAG.ZLIB_CUSTOM_METHOD;
			zlib_method=method;
		}

		public void png_set_write_status_fn(png_write_status_ptr write_row_fn)
		{
			this.write_row_fn=write_row_fn;
		}

		public void png_set_write_user_transform_fn(png_user_transform_ptr write_user_transform_fn)
		{
			transformations|=PNG_TRANSFORMATION.USER_TRANSFORM;
			this.write_user_transform_fn=write_user_transform_fn;
		}
	}
}
