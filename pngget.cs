// pngget.cs - retrieval of values from info struct
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

namespace Free.Ports.libpng
{
	public partial class png_struct
	{
		public bool png_get_valid(PNG_INFO flag)
		{
			if(flag==PNG_INFO.None) return false;
			return (info_ptr_valid&flag)==flag;
		}

		public uint png_get_rowbytes()
		{
			return info_ptr_rowbytes;
		}

		public uint png_get_image_width()
		{
			return info_ptr_width;
		}

		public uint png_get_image_height()
		{
			return info_ptr_height;
		}

		public byte png_get_bit_depth()
		{
			return info_ptr_bit_depth;
		}

		public PNG_COLOR_TYPE png_get_color_type()
		{
			return info_ptr_color_type;
		}

		public PNG_FILTER_TYPE png_get_filter_type()
		{
			return info_ptr_filter_type;
		}

		public PNG_INTERLACE png_get_interlace_type()
		{
			return info_ptr_interlace_type;
		}

		public PNG_COMPRESSION_TYPE png_get_compression_type()
		{
			return info_ptr_compression_type;
		}

		public uint png_get_x_pixels_per_meter()
		{
			if((info_ptr_valid&PNG_INFO.pHYs)!=PNG_INFO.pHYs) return 0;
			if(info_ptr_phys_unit_type!=PNG_RESOLUTION.METER) return 0;
			return info_ptr_x_pixels_per_unit;
		}

		public uint png_get_y_pixels_per_meter()
		{
			if((info_ptr_valid&PNG_INFO.pHYs)!=PNG_INFO.pHYs) return 0;
			if(info_ptr_phys_unit_type!=PNG_RESOLUTION.METER) return 0;
			return info_ptr_y_pixels_per_unit;
		}

		public uint png_get_pixels_per_meter()
		{
			if((info_ptr_valid&PNG_INFO.pHYs)!=PNG_INFO.pHYs) return 0;
			if(info_ptr_phys_unit_type!=PNG_RESOLUTION.METER||info_ptr_x_pixels_per_unit!=info_ptr_y_pixels_per_unit) return 0;
			return info_ptr_x_pixels_per_unit;
		}

		public double png_get_pixel_aspect_ratio()
		{
			if((info_ptr_valid&PNG_INFO.pHYs)!=PNG_INFO.pHYs) return 0;
			if(info_ptr_x_pixels_per_unit==0) return 0;
			return ((double)info_ptr_y_pixels_per_unit)/info_ptr_x_pixels_per_unit;
		}

		public int png_get_x_offset_microns()
		{
			if((info_ptr_valid&PNG_INFO.oFFs)!=PNG_INFO.oFFs) return 0;
			if(info_ptr_offset_unit_type!=PNG_OFFSET.MICROMETER) return 0;
			return info_ptr_x_offset;
		}

		public int png_get_y_offset_microns()
		{
			if((info_ptr_valid&PNG_INFO.oFFs)!=PNG_INFO.oFFs) return 0;
			if(info_ptr_offset_unit_type!=PNG_OFFSET.MICROMETER) return 0;
			return info_ptr_y_offset;
		}

		public int png_get_x_offset_pixels()
		{
			if((info_ptr_valid&PNG_INFO.oFFs)!=PNG_INFO.oFFs) return 0;
			if(info_ptr_offset_unit_type!=PNG_OFFSET.PIXEL) return 0;
			return info_ptr_x_offset;
		}

		public int png_get_y_offset_pixels()
		{
			if((info_ptr_valid&PNG_INFO.oFFs)!=PNG_INFO.oFFs) return 0;
			if(info_ptr_offset_unit_type!=PNG_OFFSET.PIXEL) return 0;
			return info_ptr_y_offset;
		}

		public byte png_get_channels()
		{
			return info_ptr_channels;
		}

		public byte[] png_get_signature()
		{
			return info_ptr_signature;
		}

		public PNG_INFO png_get_bKGD(ref png_color_16 background)
		{
			if((info_ptr_valid&PNG_INFO.bKGD)!=PNG_INFO.bKGD) return PNG_INFO.None;
			background=info_ptr_background;
			return PNG_INFO.bKGD;
		}

		public PNG_INFO png_get_cHRM(ref double white_x, ref double white_y, ref double red_x, ref double red_y,
			ref double green_x, ref double green_y, ref double blue_x, ref double blue_y)
		{
			if((info_ptr_valid&PNG_INFO.cHRM)!=PNG_INFO.cHRM) return PNG_INFO.None;
			white_x=info_ptr_x_white;
			white_y=info_ptr_y_white;
			red_x=info_ptr_x_red;
			red_y=info_ptr_y_red;
			green_x=info_ptr_x_green;
			green_y=info_ptr_y_green;
			blue_x=info_ptr_x_blue;
			blue_y=info_ptr_y_blue;
			return PNG_INFO.cHRM;
		}

		public PNG_INFO png_get_gAMA(ref double file_gamma)
		{
			if((info_ptr_valid&PNG_INFO.gAMA)!=PNG_INFO.gAMA) return PNG_INFO.None;
			file_gamma=info_ptr_gamma;
			return PNG_INFO.gAMA;
		}

		public PNG_INFO png_get_sRGB(ref PNG_sRGB_INTENT file_srgb_intent)
		{
			if((info_ptr_valid&PNG_INFO.sRGB)!=PNG_INFO.sRGB) return PNG_INFO.None;
			file_srgb_intent=info_ptr_srgb_intent;
			return PNG_INFO.sRGB;
		}

		public PNG_INFO png_get_iCCP(ref string name, ref int compression_type, ref byte[] profile)
		{
			if((info_ptr_valid&PNG_INFO.iCCP)!=PNG_INFO.iCCP) return PNG_INFO.None;
			name=info_ptr_iccp_name;
			profile=info_ptr_iccp_profile;
			// compression_type is a dummy so the API won't have to change
			// if we introduce multiple compression types later.
			compression_type=info_ptr_iccp_compression;
			return PNG_INFO.iCCP;
		}

		public uint png_get_sPLT(ref png_sPLT_t[] spalettes)
		{
			if((info_ptr_valid&PNG_INFO.sPLT)!=PNG_INFO.sPLT) return 0;
			spalettes=info_ptr_splt_palettes.ToArray();
			return (uint)info_ptr_splt_palettes.Count;
		}

		public PNG_INFO png_get_hIST(ref ushort[] hist)
		{
			if((info_ptr_valid&PNG_INFO.hIST)!=PNG_INFO.hIST) return PNG_INFO.None;
			hist=info_ptr_hist;
			return PNG_INFO.hIST;
		}

		public bool png_get_IHDR(ref uint width, ref uint height, ref byte bit_depth, ref PNG_COLOR_TYPE color_type,
			ref PNG_INTERLACE interlace_type, ref PNG_COMPRESSION_TYPE compression_type, ref PNG_FILTER_TYPE filter_type)
		{
			// This is redundant if we can be sure that the info_ptr values were all
			// assigned in png_set_IHDR(). We do the check anyhow in case an
			// application has ignored our advice not to mess with the members
			// of info_ptr directly.
			png_check_IHDR(info_ptr_width, info_ptr_height, info_ptr_bit_depth, info_ptr_color_type, info_ptr_interlace_type,
				info_ptr_compression_type, info_ptr_filter_type);

			width=info_ptr_width;
			height=info_ptr_height;
			bit_depth=info_ptr_bit_depth;
			color_type=info_ptr_color_type;
			compression_type=info_ptr_compression_type;
			filter_type=info_ptr_filter_type;
			interlace_type=info_ptr_interlace_type;

			return true;
		}

		public bool png_get_IHDR(ref uint width, ref uint height, ref byte bit_depth, ref PNG_COLOR_TYPE color_type)
		{
			// This is redundant if we can be sure that the info_ptr values were all
			// assigned in png_set_IHDR(). We do the check anyhow in case an
			// application has ignored our advice not to mess with the members
			// of info_ptr directly.
			png_check_IHDR(info_ptr_width, info_ptr_height, info_ptr_bit_depth, info_ptr_color_type, info_ptr_interlace_type,
				info_ptr_compression_type, info_ptr_filter_type);

			width=info_ptr_width;
			height=info_ptr_height;
			bit_depth=info_ptr_bit_depth;
			color_type=info_ptr_color_type;

			return true;
		}

		public PNG_INFO png_get_oFFs(ref int offset_x, ref int offset_y, ref PNG_OFFSET unit_type)
		{
			if((info_ptr_valid&PNG_INFO.oFFs)!=PNG_INFO.oFFs) return PNG_INFO.None;
			offset_x=info_ptr_x_offset;
			offset_y=info_ptr_y_offset;
			unit_type=info_ptr_offset_unit_type;
			return PNG_INFO.oFFs;
		}

		public PNG_INFO png_get_pCAL(ref string purpose, ref int X0, ref int X1, ref PNG_EQUATION type, ref string units, ref string[] @params)
		{
			if((info_ptr_valid&PNG_INFO.pCAL)!=PNG_INFO.pCAL) return PNG_INFO.None;
			purpose=info_ptr_pcal_purpose;
			X0=info_ptr_pcal_X0;
			X1=info_ptr_pcal_X1;
			type=info_ptr_pcal_type;
			units=info_ptr_pcal_units;
			@params=info_ptr_pcal_params;
			return PNG_INFO.pCAL;
		}

		public PNG_INFO png_get_sCAL(ref PNG_SCALE unit, ref double width, ref double height)
		{
			if((info_ptr_valid&PNG_INFO.sCAL)!=PNG_INFO.sCAL) return PNG_INFO.None;
			unit=info_ptr_scal_unit;
			width=info_ptr_scal_pixel_width;
			height=info_ptr_scal_pixel_height;
			return PNG_INFO.sCAL;
		}

		public PNG_INFO png_get_pHYs(ref uint res_x, ref uint res_y, ref PNG_RESOLUTION unit_type)
		{
			if((info_ptr_valid&PNG_INFO.pHYs)!=PNG_INFO.pHYs) return PNG_INFO.None;
			res_x=info_ptr_x_pixels_per_unit;
			res_y=info_ptr_y_pixels_per_unit;
			unit_type=info_ptr_phys_unit_type;
			return PNG_INFO.pHYs;
		}

		public PNG_INFO png_get_PLTE(ref png_color[] palette)
		{
			if((info_ptr_valid&PNG_INFO.PLTE)!=PNG_INFO.PLTE) return PNG_INFO.None;
			palette=info_ptr_palette;
			return PNG_INFO.PLTE;
		}

		public PNG_INFO png_get_sBIT(ref png_color_8 sig_bit)
		{
			if((info_ptr_valid&PNG_INFO.sBIT)!=PNG_INFO.sBIT) return PNG_INFO.None;
			sig_bit=info_ptr_sig_bit;
			return PNG_INFO.sBIT;
		}

		public uint png_get_text(ref png_text[] texts)
		{
			if(info_ptr_text==null||info_ptr_text.Count==0) return 0;
			texts=info_ptr_text.ToArray();
			return (uint)info_ptr_text.Count;
		}

		public PNG_INFO png_get_tIME(ref png_time mod_time)
		{
			if((info_ptr_valid&PNG_INFO.tIME)!=PNG_INFO.tIME) return PNG_INFO.None;
			mod_time=info_ptr_mod_time;
			return PNG_INFO.tIME;
		}

		public PNG_INFO png_get_tRNS(ref byte[] trans_alpha, ref ushort num_trans, ref png_color_16 trans_color)
		{
			if((info_ptr_valid&PNG_INFO.tRNS)!=PNG_INFO.tRNS) return PNG_INFO.None;
			trans_color=info_ptr_trans_color;
			trans_alpha=(info_ptr_color_type==PNG_COLOR_TYPE.PALETTE)?info_ptr_trans_alpha:null;
			num_trans=info_ptr_num_trans;
			return PNG_INFO.tRNS;
		}

		public PNG_INFO png_get_sTER(ref PNG_STEREO stereo_layout)
		{
			if((info_ptr_valid&PNG_INFO.sTER)!=PNG_INFO.sTER) return PNG_INFO.None;
			stereo_layout=info_ptr_stereo_layout;
			return PNG_INFO.sTER;
		}

		public uint png_get_gIFg(ref png_gIFg[] gIFgs)
		{
			if((info_ptr_valid&PNG_INFO.gIFg)!=PNG_INFO.gIFg) return 0;
			gIFgs=info_ptr_gIFgs.ToArray();
			return (uint)info_ptr_gIFgs.Count;
		}

		public uint png_get_gIFg(ref png_gIFx[] gIFxs)
		{
			if((info_ptr_valid&PNG_INFO.gIFx)!=PNG_INFO.gIFx) return 0;
			gIFxs=info_ptr_gIFxs.ToArray();
			return (uint)info_ptr_gIFxs.Count;
		}

		public bool png_get_rgb_to_gray_status()
		{
			return rgb_to_gray_status;
		}

		public uint png_get_compression_buffer_size()
		{
			return zbuf_size;
		}

		// These functions were added to libpng 1.4.0
		public PNG_IO png_get_io_state()
		{
			return io_state;
		}

		public byte[] png_get_io_chunk_name()
		{
			return chunk_name;
		}
	}
}
