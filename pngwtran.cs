// pngwtran.cs - transforms the data in a row for PNG writers
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
	public partial class png_struct
	{
		// Transform the data according to the user's wishes. The order of
		// transformations is significant.
		void png_do_write_transformations()
		{
			if((transformations&PNG_TRANSFORMATION.USER_TRANSFORM)==PNG_TRANSFORMATION.USER_TRANSFORM)
			{ // user write transform function
				if(write_user_transform_fn!=null) write_user_transform_fn(this, ref row_info, row_buf);
			}
			if((transformations&PNG_TRANSFORMATION.FILLER)==PNG_TRANSFORMATION.FILLER)
				png_do_strip_filler(ref row_info, row_buf, flags);
			if((transformations&PNG_TRANSFORMATION.PACKSWAP)==PNG_TRANSFORMATION.PACKSWAP)
				png_do_packswap(ref row_info, row_buf);
			if((transformations&PNG_TRANSFORMATION.PACK)==PNG_TRANSFORMATION.PACK)
				png_do_pack(ref row_info, row_buf, bit_depth);
			if((transformations&PNG_TRANSFORMATION.SWAP_BYTES)==PNG_TRANSFORMATION.SWAP_BYTES)
				png_do_swap(ref row_info, row_buf);
			if((transformations&PNG_TRANSFORMATION.SHIFT)==PNG_TRANSFORMATION.SHIFT)
				png_do_shift(row_info, row_buf, shift);
			if((transformations&PNG_TRANSFORMATION.SWAP_ALPHA)==PNG_TRANSFORMATION.SWAP_ALPHA)
				png_do_write_swap_alpha(row_info, row_buf);
			if((transformations&PNG_TRANSFORMATION.INVERT_ALPHA)==PNG_TRANSFORMATION.INVERT_ALPHA)
				png_do_write_invert_alpha(row_info, row_buf);
			if((transformations&PNG_TRANSFORMATION.BGR)==PNG_TRANSFORMATION.BGR)
				png_do_bgr(ref row_info, row_buf);
			if((transformations&PNG_TRANSFORMATION.INVERT_MONO)==PNG_TRANSFORMATION.INVERT_MONO)
				png_do_invert(ref row_info, row_buf);
		}

		// Pack pixels into bytes. Pass the true bit depth in bit_depth. The
		// row_info bit depth should be 8 (one pixel per byte). The channels
		// should be 1 (this only happens on grayscale and paletted images).
		unsafe void png_do_pack(ref png_row_info row_info, byte[] row, uint bit_depth)
		{
			if(row_info.bit_depth!=8||row_info.channels!=1) return;

			fixed(byte* row_=row)
			{
				byte* sp=row_+1, dp=row_+1; // skip filter value

				uint row_width=row_info.width;
				int v=0;
				switch((int)bit_depth)
				{
					case 1:
						{
							int mask=0x80;
							for(uint i=0; i<row_width; i++)
							{
								if(*sp!=0) v|=mask;
								sp++;
								if(mask>1) mask>>=1;
								else
								{
									mask=0x80;
									*dp=(byte)v;
									dp++;
									v=0;
								}
							}
							if(mask!=0x80) *dp=(byte)v;
						}
						break;
					case 2:
						{
							int shift=6;
							for(uint i=0; i<row_width; i++)
							{
								byte value=(byte)(*sp&0x03);
								v|=(value<<shift);
								if(shift==0)
								{
									shift=6;
									*dp=(byte)v;
									dp++;
									v=0;
								}
								else shift-=2;
								sp++;
							}
							if(shift!=6) *dp=(byte)v;
						}
						break;
					case 4:
						{
							int shift=4;
							for(uint i=0; i<row_width; i++)
							{
								byte value=(byte)(*sp&0x0f);
								v|=(value<<shift);

								if(shift==0)
								{
									shift=4;
									*dp=(byte)v;
									dp++;
									v=0;
								}
								else shift-=4;
								sp++;
							}
							if(shift!=4) *dp=(byte)v;
						}
						break;
				}
				row_info.bit_depth=(byte)bit_depth;
				row_info.pixel_depth=(byte)(bit_depth*row_info.channels);
				row_info.rowbytes=PNG_ROWBYTES(row_info.pixel_depth, row_info.width);
			}
		}

		// Shift pixel values to take advantage of whole range. Pass the
		// true number of bits in bit_depth. The row should be packed
		// according to row_info->bit_depth. Thus, if you had a row of
		// bit depth 4, but the pixels only had values from 0 to 7, you
		// would pass 3 as bit_depth, and this routine would translate the
		// data to 0 to 15.
		unsafe void png_do_shift(png_row_info row_info, byte[] row, png_color_8 bit_depth)
		{
			if(row_info.color_type==PNG_COLOR_TYPE.PALETTE) return;

			int[] shift_start=new int[4], shift_dec=new int[4];
			uint channels=0;

			if((row_info.color_type&PNG_COLOR_TYPE.COLOR_MASK)==PNG_COLOR_TYPE.COLOR_MASK)
			{
				shift_start[channels]=row_info.bit_depth-bit_depth.red;
				shift_dec[channels]=bit_depth.red; channels++;
				shift_start[channels]=row_info.bit_depth-bit_depth.green;
				shift_dec[channels]=bit_depth.green; channels++;
				shift_start[channels]=row_info.bit_depth-bit_depth.blue;
				shift_dec[channels]=bit_depth.blue; channels++;
			}
			else
			{
				shift_start[channels]=row_info.bit_depth-bit_depth.gray;
				shift_dec[channels]=bit_depth.gray; channels++;
			}
			if((row_info.color_type&PNG_COLOR_TYPE.ALPHA_MASK)==PNG_COLOR_TYPE.ALPHA_MASK)
			{
				shift_start[channels]=row_info.bit_depth-bit_depth.alpha;
				shift_dec[channels]=bit_depth.alpha; channels++;
			}

			fixed(byte* row_=row)
			{
				// with low row depths, could only be grayscale, so one channel
				byte* bp=row_+1; // skip filter value
				if(row_info.bit_depth<8)
				{
					byte mask;
					uint row_bytes=row_info.rowbytes;

					if(bit_depth.gray==1&&row_info.bit_depth==2) mask=0x55;
					else if(row_info.bit_depth==4&&bit_depth.gray==3) mask=0x11;
					else mask=0xff;

					for(uint i=0; i<row_bytes; i++, bp++)
					{
						ushort v=*bp;
						*bp=0;
						for(int j=shift_start[0]; j>-shift_dec[0]; j-=shift_dec[0])
						{
							if(j>0) *bp|=(byte)((v<<j)&0xff);
							else *bp|=(byte)((v>>(-j))&mask);
						}
					}
				}
				else if(row_info.bit_depth==8)
				{
					uint istop=channels*row_info.width;

					for(uint i=0; i<istop; i++, bp++)
					{
						int c=(int)(i%channels);

						ushort v=*bp;
						*bp=0;
						for(int j=shift_start[c]; j>-shift_dec[c]; j-=shift_dec[c])
						{
							if(j>0) *bp|=(byte)((v<<j)&0xff);
							else *bp|=(byte)((v>>(-j))&0xff);
						}
					}
				}
				else
				{
					uint istop=channels*row_info.width;

					for(uint i=0; i<istop; i++)
					{
						int c=(int)(i%channels);

						ushort v=(ushort)(((ushort)(*bp)<<8)+*(bp+1));
						ushort value=0;
						for(int j=shift_start[c]; j>-shift_dec[c]; j-=shift_dec[c])
						{
							if(j>0) value|=(ushort)((v<<j)&(ushort)0xffff);
							else value|=(ushort)((v>>(-j))&(ushort)0xffff);
						}
						*bp++=(byte)(value>>8);
						*bp++=(byte)(value&0xff);
					}
				}
			}
		}

		unsafe void png_do_write_swap_alpha(png_row_info row_info, byte[] row)
		{
			fixed(byte* row_=row)
			{
				byte* sp=row_+1, dp=row_+1; // skip filter value
				uint row_width=row_info.width;
				if(row_info.color_type==PNG_COLOR_TYPE.RGB_ALPHA)
				{
					// This converts from ARGB to RGBA
					if(row_info.bit_depth==8)
					{
						for(uint i=0; i<row_width; i++)
						{
							byte save=*(sp++);
							*(dp++)=*(sp++);
							*(dp++)=*(sp++);
							*(dp++)=*(sp++);
							*(dp++)=save;
						}
					}
					else // This converts from AARRGGBB to RRGGBBAA
					{
						for(uint i=0; i<row_width; i++)
						{
							byte save0, save1;
							save0=*(sp++);
							save1=*(sp++);
							*(dp++)=*(sp++);
							*(dp++)=*(sp++);
							*(dp++)=*(sp++);
							*(dp++)=*(sp++);
							*(dp++)=*(sp++);
							*(dp++)=*(sp++);
							*(dp++)=save0;
							*(dp++)=save1;
						}
					}
				}
				else if(row_info.color_type==PNG_COLOR_TYPE.GRAY_ALPHA)
				{
					// This converts from AG to GA
					if(row_info.bit_depth==8)
					{
						for(uint i=0; i<row_width; i++)
						{
							byte save=*(sp++);
							*(dp++)=*(sp++);
							*(dp++)=save;
						}
					}
					else // This converts from AAGG to GGAA
					{
						for(uint i=0; i<row_width; i++)
						{
							byte save0, save1;
							save0=*(sp++);
							save1=*(sp++);
							*(dp++)=*(sp++);
							*(dp++)=*(sp++);
							*(dp++)=save0;
							*(dp++)=save1;
						}
					}
				}
			}
		}

		unsafe void png_do_write_invert_alpha(png_row_info row_info, byte[] row)
		{
			fixed(byte* row_=row)
			{
				byte* sp=row_+1, dp=row_+1; // skip filter value
				uint row_width=row_info.width;

				if(row_info.color_type==PNG_COLOR_TYPE.RGB_ALPHA)
				{
					// This inverts the alpha channel in RGBA
					if(row_info.bit_depth==8)
					{
						for(uint i=0; i<row_width; i++)
						{
							sp+=3; dp=sp;
							*(dp++)=(byte)(255-*(sp++));
						}
					}
					else // This inverts the alpha channel in RRGGBBAA
					{
						for(uint i=0; i<row_width; i++)
						{
							sp+=6; dp=sp;
							*(dp++)=(byte)(255-*(sp++));
							*(dp++)=(byte)(255-*(sp++));
						}
					}
				}
				else if(row_info.color_type==PNG_COLOR_TYPE.GRAY_ALPHA)
				{
					// This inverts the alpha channel in GA
					if(row_info.bit_depth==8)
					{
						for(uint i=0; i<row_width; i++)
						{
							*(dp++)=*(sp++);
							*(dp++)=(byte)(255-*(sp++));
						}
					}
					else // This inverts the alpha channel in GGAA
					{
						for(uint i=0; i<row_width; i++)
						{
							sp+=2; dp=sp;
							*(dp++)=(byte)(255-*(sp++));
							*(dp++)=(byte)(255-*(sp++));
						}
					}
				}
			}
		}

		// undoes intrapixel differencing
		unsafe void png_do_write_intrapixel(png_row_info row_info, byte[] row)
		{
			if((row_info.color_type&PNG_COLOR_TYPE.COLOR_MASK)!=PNG_COLOR_TYPE.COLOR_MASK) return;

			fixed(byte* row_=row)
			{
				byte* rp=row_+1; // skip filter value
				int bytes_per_pixel;
				uint row_width=row_info.width;
				if(row_info.bit_depth==8)
				{
					if(row_info.color_type==PNG_COLOR_TYPE.RGB) bytes_per_pixel=3;
					else if(row_info.color_type==PNG_COLOR_TYPE.RGB_ALPHA) bytes_per_pixel=4;
					else return;

					for(uint i=0; i<row_width; i++, rp+=bytes_per_pixel)
					{
						*(rp)=(byte)((*rp-*(rp+1))&0xff);
						*(rp+2)=(byte)((*(rp+2)-*(rp+1))&0xff);
					}
				}
				else if(row_info.bit_depth==16)
				{
					if(row_info.color_type==PNG_COLOR_TYPE.RGB) bytes_per_pixel=6;
					else if(row_info.color_type==PNG_COLOR_TYPE.RGB_ALPHA) bytes_per_pixel=8;
					else return;

					for(uint i=0; i<row_width; i++, rp+=bytes_per_pixel)
					{
						uint s0=(uint)(*(rp)<<8)|*(rp+1);
						uint s1=(uint)(*(rp+2)<<8)|*(rp+3);
						uint s2=(uint)(*(rp+4)<<8)|*(rp+5);
						uint red=(uint)((s0-s1)&0xffffL);
						uint blue=(uint)((s2-s1)&0xffffL);
						*(rp)=(byte)((red>>8)&0xff);
						*(rp+1)=(byte)(red&0xff);
						*(rp+4)=(byte)((blue>>8)&0xff);
						*(rp+5)=(byte)(blue&0xff);
					}
				}
			}
		}
	}
}
