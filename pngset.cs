// pngset.cs - storage of image information into info struct
//
// Based on libpng version 1.4.3 - June 26, 2010
// This code is released under the libpng license.
// For conditions of distribution and use, see copyright notice in License.txt
// Copyright (C) 2007-2010 by the Authors
// Copyright (c) 1998-2010 Glenn Randers-Pehrson
// (Version 0.96 Copyright (c) 1996, 1997 Andreas Dilger)
// (Version 0.88 Copyright (c) 1995, 1996 Guy Eric Schalnat, Group 42, Inc.)
//
// The functions here are used during reads to store data from the file
// into the info struct, and during writes to store application data
// into the info struct for writing into the file. This abstracts the
// info struct and allows us to change the structure in the future.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Free.Ports.libpng
{
	public partial class png_struct
	{
		public void png_set_bKGD(png_color_16 background)
		{
			info_ptr_background=background;
			info_ptr_valid|=PNG_INFO.bKGD;
		}

		public void png_set_cHRM(double white_x, double white_y, double red_x, double red_y,
			double green_x, double green_y, double blue_x, double blue_y)
		{
			info_ptr_x_white=white_x; info_ptr_y_white=white_y;
			info_ptr_x_red=red_x; info_ptr_y_red=red_y;
			info_ptr_x_green=green_x; info_ptr_y_green=green_y;
			info_ptr_x_blue=blue_x; info_ptr_y_blue=blue_y;
			info_ptr_valid|=PNG_INFO.cHRM;
		}

		public void png_set_gAMA(double file_gamma)
		{
			if(file_gamma>21474.83)
			{
				Debug.WriteLine("Limiting gamma to 21474.83");
				file_gamma=21474.83;
			}
			else if(file_gamma<0)
			{
				Debug.WriteLine("Setting negative gamma to zero");
				file_gamma=0;
			}

			info_ptr_gamma=file_gamma;
			info_ptr_valid|=PNG_INFO.gAMA;
			if(file_gamma==0) Debug.WriteLine("Setting gamma=0");
		}

		public void png_set_gAMA_fixed(int int_gamma)
		{
			if(int_gamma>PNG.UINT_31_MAX)
			{
				Debug.WriteLine("Limiting gamma to 21474.83");
				int_gamma=(int)PNG.UINT_31_MAX;
			}
			else if(int_gamma<0)
			{
				Debug.WriteLine("Setting negative gamma to zero");
				int_gamma=0;
			}

			gamma=int_gamma/100000.0;
			info_ptr_valid|=PNG_INFO.gAMA;
			if(int_gamma==0) Debug.WriteLine("Setting gamma=0");
		}

		public void png_set_gAMA_fixed(uint int_gamma)
		{
			if(int_gamma>PNG.UINT_31_MAX)
			{
				Debug.WriteLine("Limiting gamma to 21474.83");
				int_gamma=PNG.UINT_31_MAX;
			}

			gamma=int_gamma/100000.0;
			info_ptr_valid|=PNG_INFO.gAMA;
			if(int_gamma==0) Debug.WriteLine("Setting gamma=0");
		}

		public void png_clear_hIST()
		{
			hist=null;
			info_ptr_valid&=~PNG_INFO.hIST;
		}

		public void png_set_hIST(ushort[] hist)
		{
			if(info_ptr_palette.Length==0||info_ptr_palette.Length>PNG.MAX_PALETTE_LENGTH)
			{
				Debug.WriteLine("Invalid palette size, hIST allocation skipped");
				return;
			}

			this.hist=info_ptr_hist=null;
			info_ptr_valid&=~PNG_INFO.hIST;

			if(hist==null) return;
			if(hist.Length==0) return;
			if(hist.Length!=palette.Length)
			{
				Debug.WriteLine("Histogram has different size than palette, hIST allocation skipped");
				return;
			}

			this.hist=info_ptr_hist=hist;
			info_ptr_valid|=PNG_INFO.hIST;
		}

		public void png_set_IHDR(uint width, uint height, byte bit_depth, PNG_COLOR_TYPE color_type, PNG_INTERLACE interlace_type, PNG_COMPRESSION_TYPE compression_type, PNG_FILTER_TYPE filter_type)
		{
			info_ptr_width=width;
			info_ptr_height=height;
			info_ptr_bit_depth=bit_depth;
			info_ptr_color_type=color_type;
			info_ptr_compression_type=compression_type;
			info_ptr_filter_type=filter_type;
			info_ptr_interlace_type=interlace_type;

			png_check_IHDR(info_ptr_width, info_ptr_height, info_ptr_bit_depth, info_ptr_color_type, info_ptr_interlace_type,
				info_ptr_compression_type, info_ptr_filter_type);

			// find number of channels
			switch(info_ptr_color_type)
			{
				case PNG_COLOR_TYPE.GRAY:
				case PNG_COLOR_TYPE.PALETTE: info_ptr_channels=1; break;
				case PNG_COLOR_TYPE.RGB: info_ptr_channels=3; break;
				case PNG_COLOR_TYPE.GRAY_ALPHA: info_ptr_channels=2; break;
				case PNG_COLOR_TYPE.RGB_ALPHA: info_ptr_channels=4; break;
			}

			info_ptr_pixel_depth=(byte)(info_ptr_channels*info_ptr_bit_depth);

			// check for potential overflow
			if(width>(PNG.UINT_32_MAX>>3)	// 8-byte RGBA pixels
				-64							// bigrowbuf hack
				-1							// filter byte
				-7*8						// rounding of width to multiple of 8 pixels
				-8)							// extra max_pixel_depth pad
				info_ptr_rowbytes=0;
			else info_ptr_rowbytes=PNG_ROWBYTES(info_ptr_pixel_depth, width);
		}

		public void png_set_oFFs(int offset_x, int offset_y, PNG_OFFSET unit_type)
		{
			info_ptr_x_offset=offset_x;
			info_ptr_y_offset=offset_y;
			info_ptr_offset_unit_type=unit_type;
			info_ptr_valid|=PNG_INFO.oFFs;
		}

		public void png_clear_pCAL()
		{
			info_ptr_pcal_purpose=null;
			info_ptr_pcal_X0=0;
			info_ptr_pcal_X1=0;
			info_ptr_pcal_type=PNG_EQUATION.BASE_E;
			info_ptr_pcal_units=null;
			info_ptr_pcal_params=null;
			info_ptr_valid&=~PNG_INFO.pCAL;
		}

		public void png_set_pCAL(string purpose, int X0, int X1, PNG_EQUATION type, string units, string[] @params)
		{
			if(purpose==null||purpose.Length==0)
			{
				Debug.WriteLine("Ignoring attempt to set unnamed pCal calibration");
				return;
			}
			info_ptr_pcal_purpose=purpose;
			info_ptr_pcal_X0=X0;
			info_ptr_pcal_X1=X1;
			info_ptr_pcal_type=type;
			info_ptr_pcal_units=units;
			info_ptr_pcal_params=@params; // TODO: params in array must be set to valid parameters
			info_ptr_valid|=PNG_INFO.pCAL;
		}

		public void png_set_sCAL(PNG_SCALE unit, double width, double height)
		{
			info_ptr_scal_unit=unit;
			info_ptr_scal_pixel_width=width;
			info_ptr_scal_pixel_height=height;
			info_ptr_valid|=PNG_INFO.sCAL;
		}

		public void png_set_pHYs(uint res_x, uint res_y, PNG_RESOLUTION unit_type)
		{
			info_ptr_x_pixels_per_unit=res_x;
			info_ptr_y_pixels_per_unit=res_y;
			info_ptr_phys_unit_type=unit_type;
			info_ptr_valid|=PNG_INFO.pHYs;
		}

		public void png_clear_PLTE()
		{
			palette=info_ptr_palette=null;
			info_ptr_valid&=~PNG_INFO.PLTE;
		}

		public void png_set_PLTE(png_color[] palette)
		{
			if(palette==null||palette.Length==0)
			{
				this.palette=info_ptr_palette=null;
				info_ptr_valid&=~PNG_INFO.PLTE;
				return;
			}

			if(palette.Length>PNG.MAX_PALETTE_LENGTH)
			{
				if(info_ptr_color_type==PNG_COLOR_TYPE.PALETTE) throw new PNG_Exception("Invalid palette length");
				else
				{
					Debug.WriteLine("Invalid palette length");
					return;
				}
			}

			this.palette=info_ptr_palette=palette;
			info_ptr_valid|=PNG_INFO.PLTE;
		}

		public void png_set_sBIT(png_color_8 sig_bit)
		{
			info_ptr_sig_bit=sig_bit;
			info_ptr_valid|=PNG_INFO.sBIT;
		}

		public void png_set_sRGB(PNG_sRGB_INTENT intent)
		{
			info_ptr_srgb_intent=intent;
			info_ptr_valid|=PNG_INFO.sRGB;
		}

		public void png_set_sRGB_gAMA_and_cHRM(PNG_sRGB_INTENT intent)
		{
			png_set_sRGB(intent);
			png_set_gAMA(0.45455);
			png_set_cHRM(0.3127, 0.329, 0.64, 0.33, 0.3, 0.6, 0.15, 0.06);
		}

		public void png_clear_iCCP()
		{
			info_ptr_iccp_name=null;
			info_ptr_iccp_profile=null;
			// Compression is always zero but is here so the API and info structure
			// does not have to change if we introduce multiple compression types
			info_ptr_iccp_compression=0;
			info_ptr_valid&=~PNG_INFO.iCCP;
		}

		public void png_set_iCCP(string name, byte compression_type, byte[] profile)
		{
			if(name==null||name.Length==0)
			{
				Debug.WriteLine("Ignoring attempt to set unnamed iCCP profile");
				return;
			}

			if(profile==null||profile.Length==0)
			{
				Debug.WriteLine("Ignoring attempt to set empty iCCP profile");
				return;
			}

			info_ptr_iccp_name=name;
			info_ptr_iccp_profile=profile;
			// Compression is always zero but is here so the API and info structure
			// does not have to change if we introduce multiple compression types
			info_ptr_iccp_compression=compression_type;
			info_ptr_valid|=PNG_INFO.iCCP;
		}

		public void png_clear_text()
		{
			info_ptr_text.Clear();
		}

		public void png_set_text(png_text text_ptr)
		{
			if(text_ptr.key==null||text_ptr.key=="")
			{
				Debug.WriteLine("Ignoring attempt to set empty text");
				return;
			}

			if(text_ptr.compression<=0)
			{
				text_ptr.lang=null;
				text_ptr.lang_key=null;
			}

			if(text_ptr.text==null||text_ptr.text=="")
			{
				if(text_ptr.compression>0) text_ptr.compression=PNG_TEXT_COMPRESSION.ITXT_COMPRESSION_NONE;
				else text_ptr.compression=PNG_TEXT_COMPRESSION.NONE;
				text_ptr.text=null;
			}

			if(info_ptr_text==null) info_ptr_text=new List<png_text>();
			info_ptr_text.Add(text_ptr);
		}

		public void png_set_text(png_text[] text_ptr)
		{
			if(text_ptr==null||text_ptr.Length==0) return;
			foreach(png_text text_ptr_ in text_ptr) png_set_text(text_ptr_);
		}

		public void png_set_tIME(png_time mod_time)
		{
			info_ptr_mod_time=mod_time;
			info_ptr_valid|=PNG_INFO.tIME;
		}

		public void png_set_tRNS(byte[] trans_alpha)
		{
			if(trans_alpha==null||trans_alpha.Length==0)
			{
				this.trans_alpha=info_ptr_trans_alpha=null;
				num_trans=info_ptr_num_trans=0;
				info_ptr_valid&=~PNG_INFO.tRNS;
				return;
			}
			this.trans_alpha=info_ptr_trans_alpha=trans_alpha;
			num_trans=info_ptr_num_trans=(ushort)trans_alpha.Length;
			info_ptr_valid|=PNG_INFO.tRNS;
		}

		public void png_set_tRNS(png_color_16 trans_color)
		{
			int sample_max=(1<<info_ptr_bit_depth);
			if((info_ptr_color_type==PNG_COLOR_TYPE.GRAY&&
				(int)trans_color.gray>sample_max)||
				(info_ptr_color_type==PNG_COLOR_TYPE.RGB&&
				((int)trans_color.red>sample_max||
				(int)trans_color.green>sample_max||
				(int)trans_color.blue>sample_max)))
				Debug.WriteLine("tRNS chunk has out-of-range samples for bit_depth");

			this.trans_color=info_ptr_trans_color=trans_color;
			num_trans=info_ptr_num_trans=1;
			info_ptr_valid|=PNG_INFO.tRNS;
		}

		public void png_clear_sPLT()
		{
			info_ptr_splt_palettes=null;
			info_ptr_valid&=~PNG_INFO.sPLT;
		}

		public void png_set_sPLT(png_sPLT_t entry)
		{
			if(info_ptr_splt_palettes==null) info_ptr_splt_palettes=new List<png_sPLT_t>();
			info_ptr_splt_palettes.Add(entry);
			info_ptr_valid|=PNG_INFO.sPLT;
		}

		public void png_set_sPLT(png_sPLT_t[] entries)
		{
			if(entries==null||entries.Length==0)
			{
				Debug.WriteLine("Ignoring attempt to set empty sPLT");
				return;
			}

			if(info_ptr_splt_palettes==null) info_ptr_splt_palettes=new List<png_sPLT_t>();
			info_ptr_splt_palettes.AddRange(entries);
			info_ptr_valid|=PNG_INFO.sPLT;
		}

		public void png_clear_sTER()
		{
			info_ptr_valid&=~PNG_INFO.sTER;
		}

		public void png_set_sTER(PNG_STEREO stereo_layout)
		{
			if(stereo_layout!=PNG_STEREO.CROSS&&stereo_layout!=PNG_STEREO.DIVERGING) return;
			info_ptr_stereo_layout=stereo_layout;
			info_ptr_valid|=PNG_INFO.sTER;
		}

		public void png_clear_gIFg()
		{
			info_ptr_gIFgs=null;
			info_ptr_valid&=~PNG_INFO.gIFg;
		}

		public void png_set_gIFg(png_gIFg gIFg)
		{
			if(info_ptr_gIFgs==null) info_ptr_gIFgs=new List<png_gIFg>();
			info_ptr_gIFgs.Add(gIFg);
			info_ptr_valid|=PNG_INFO.gIFg;
		}

		public void png_set_gIFg(png_gIFg[] gIFg)
		{
			if(info_ptr_gIFgs==null) info_ptr_gIFgs=new List<png_gIFg>();
			info_ptr_gIFgs.AddRange(gIFg);
			info_ptr_valid|=PNG_INFO.gIFg;
		}

		public void png_clear_gIFx()
		{
			info_ptr_gIFxs=null;
			info_ptr_valid&=~PNG_INFO.gIFx;
		}

		public void png_set_gIFx(png_gIFx gIFx)
		{
			if(info_ptr_gIFxs==null) info_ptr_gIFxs=new List<png_gIFx>();
			info_ptr_gIFxs.Add(gIFx);
			info_ptr_valid|=PNG_INFO.gIFx;
		}

		public void png_set_gIFx(png_gIFx[] gIFx)
		{
			if(info_ptr_gIFxs==null) info_ptr_gIFxs=new List<png_gIFx>();
			info_ptr_gIFxs.AddRange(gIFx);
			info_ptr_valid|=PNG_INFO.gIFx;
		}

		public PNG_FLAG_MNG png_permit_mng_features(PNG_FLAG_MNG mng_features)
		{
			mng_features_permitted=mng_features&PNG_FLAG_MNG.ALL;
			return mng_features_permitted;
		}

		public void png_set_compression_buffer_size(uint size)
		{
			if(size==0)
			{
				Debug.WriteLine("Ignoring to set compression buffer size to zero");
				return;
			}
			zbuf_size=size;
			zbuf=new byte[size];
			zstream.out_buf=zbuf;
			zstream.next_out=0;
			zstream.avail_out=zbuf_size;
		}

		public void png_set_invalid(PNG_INFO mask)
		{
			info_ptr_valid&=~mask;
		}
	}
}
