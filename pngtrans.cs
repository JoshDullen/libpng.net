// pngtrans.cs - transforms the data in a row (used by both readers and writers)
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
		// turn on BGR-to-RGB mapping
		public void png_set_bgr()
		{
			transformations|=PNG_TRANSFORMATION.BGR;
		}

		// turn on 16 bit byte swapping
		public void png_set_swap()
		{
			if(bit_depth==16) transformations|=PNG_TRANSFORMATION.SWAP_BYTES;
		}

		// turn on pixel packing
		public void png_set_packing()
		{
			if(bit_depth<8)
			{
				transformations|=PNG_TRANSFORMATION.PACK;
				usr_bit_depth=8;
			}
		}

		// turn on packed pixel swapping
		public void png_set_packswap()
		{
			if(bit_depth<8) transformations|=PNG_TRANSFORMATION.PACKSWAP;
		}

		public void png_set_shift(png_color_8 true_bits)
		{
			transformations|=PNG_TRANSFORMATION.SHIFT;
			shift=true_bits;
		}

		public int png_set_interlace_handling()
		{
			if(info_ptr_interlace_type!=PNG_INTERLACE.NONE)
			{
				transformations|=PNG_TRANSFORMATION.INTERLACE;
				return 7;
			}

			return 1;
		}

		// Add a filler byte on read, or remove a filler or alpha byte on write.
		// The filler type has changed in v0.95 to allow future 2-byte fillers
		// for 48-bit input data, as well as to avoid problems with some compilers
		// that don't like bytes as parameters.
		public void png_set_filler(uint filler, PNG_FILLER filler_loc)
		{
			transformations|=PNG_TRANSFORMATION.FILLER;
			this.filler=(ushort)filler;
			if(filler_loc==PNG_FILLER.AFTER) flags|=PNG_FLAG.FILLER_AFTER;
			else flags&=~PNG_FLAG.FILLER_AFTER;

			// This should probably go in the "do_read_filler" routine.
			// I attempted to do that in libpng-1.0.1a but that caused problems
			// so I restored it in libpng-1.0.2a
			if(color_type==PNG_COLOR_TYPE.RGB) usr_channels=4;

			// Also I added this in libpng-1.0.2a (what happens when we expand
			// a less-than-8-bit grayscale to GA?

			if(color_type==PNG_COLOR_TYPE.GRAY&&bit_depth>=8) usr_channels=2;
		}

		public void png_set_add_alpha(uint filler, PNG_FILLER filler_loc)
		{
			png_set_filler(filler, filler_loc);
			transformations|=PNG_TRANSFORMATION.ADD_ALPHA;
		}

		public void png_set_swap_alpha()
		{
			transformations|=PNG_TRANSFORMATION.SWAP_ALPHA;
		}

		public void png_set_invert_alpha()
		{
			transformations|=PNG_TRANSFORMATION.INVERT_ALPHA;
		}

		public void png_set_invert_mono()
		{
			transformations|=PNG_TRANSFORMATION.INVERT_MONO;
		}

		// invert monochrome grayscale data
		static unsafe void png_do_invert(ref png_row_info row_info, byte[] row)
		{
			uint istop=row_info.rowbytes;

			fixed(byte* row_=row)
			{
				byte* rp=row_+1;// skip filter value

				// This test removed from libpng version 1.0.13 and 1.2.0:
				// if(row_info->bit_depth==1 &&
				if(row_info.color_type==PNG_COLOR_TYPE.GRAY)
				{
					for(uint i=0; i<istop; i++)
					{
						*rp=(byte)(~(*rp));
						rp++;
					}
				}
				else if(row_info.color_type==PNG_COLOR_TYPE.GRAY_ALPHA&&row_info.bit_depth==8)
				{
					for(uint i=0; i<istop; i+=2)
					{
						*rp=(byte)(~(*rp));
						rp+=2;
					}
				}
				else if(row_info.color_type==PNG_COLOR_TYPE.GRAY_ALPHA&&row_info.bit_depth==16)
				{
					for(uint i=0; i<istop; i+=4)
					{
						*rp=(byte)(~(*rp));
						*(rp+1)=(byte)(~(*(rp+1)));
						rp+=4;
					}
				}
			}
		}

		// swaps byte order on 16 bit depth images
		static unsafe void png_do_swap(ref png_row_info row_info, byte[] row)
		{
			if(row_info.bit_depth==16)
			{
				fixed(byte* row_=row)
				{
					byte* rp=row_+1;// skip filter value

					uint istop=row_info.width*row_info.channels;

					for(uint i=0; i<istop; i++, rp+=2)
					{
						byte t=*rp;
						*rp=*(rp+1);
						*(rp+1)=t;
					}
				}
			}
		}

		#region swaptables
		static readonly byte[] onebppswaptable=new byte[]
		{
			0x00, 0x80, 0x40, 0xC0, 0x20, 0xA0, 0x60, 0xE0,
			0x10, 0x90, 0x50, 0xD0, 0x30, 0xB0, 0x70, 0xF0,
			0x08, 0x88, 0x48, 0xC8, 0x28, 0xA8, 0x68, 0xE8,
			0x18, 0x98, 0x58, 0xD8, 0x38, 0xB8, 0x78, 0xF8,
			0x04, 0x84, 0x44, 0xC4, 0x24, 0xA4, 0x64, 0xE4,
			0x14, 0x94, 0x54, 0xD4, 0x34, 0xB4, 0x74, 0xF4,
			0x0C, 0x8C, 0x4C, 0xCC, 0x2C, 0xAC, 0x6C, 0xEC,
			0x1C, 0x9C, 0x5C, 0xDC, 0x3C, 0xBC, 0x7C, 0xFC,
			0x02, 0x82, 0x42, 0xC2, 0x22, 0xA2, 0x62, 0xE2,
			0x12, 0x92, 0x52, 0xD2, 0x32, 0xB2, 0x72, 0xF2,
			0x0A, 0x8A, 0x4A, 0xCA, 0x2A, 0xAA, 0x6A, 0xEA,
			0x1A, 0x9A, 0x5A, 0xDA, 0x3A, 0xBA, 0x7A, 0xFA,
			0x06, 0x86, 0x46, 0xC6, 0x26, 0xA6, 0x66, 0xE6,
			0x16, 0x96, 0x56, 0xD6, 0x36, 0xB6, 0x76, 0xF6,
			0x0E, 0x8E, 0x4E, 0xCE, 0x2E, 0xAE, 0x6E, 0xEE,
			0x1E, 0x9E, 0x5E, 0xDE, 0x3E, 0xBE, 0x7E, 0xFE,
			0x01, 0x81, 0x41, 0xC1, 0x21, 0xA1, 0x61, 0xE1,
			0x11, 0x91, 0x51, 0xD1, 0x31, 0xB1, 0x71, 0xF1,
			0x09, 0x89, 0x49, 0xC9, 0x29, 0xA9, 0x69, 0xE9,
			0x19, 0x99, 0x59, 0xD9, 0x39, 0xB9, 0x79, 0xF9,
			0x05, 0x85, 0x45, 0xC5, 0x25, 0xA5, 0x65, 0xE5,
			0x15, 0x95, 0x55, 0xD5, 0x35, 0xB5, 0x75, 0xF5,
			0x0D, 0x8D, 0x4D, 0xCD, 0x2D, 0xAD, 0x6D, 0xED,
			0x1D, 0x9D, 0x5D, 0xDD, 0x3D, 0xBD, 0x7D, 0xFD,
			0x03, 0x83, 0x43, 0xC3, 0x23, 0xA3, 0x63, 0xE3,
			0x13, 0x93, 0x53, 0xD3, 0x33, 0xB3, 0x73, 0xF3,
			0x0B, 0x8B, 0x4B, 0xCB, 0x2B, 0xAB, 0x6B, 0xEB,
			0x1B, 0x9B, 0x5B, 0xDB, 0x3B, 0xBB, 0x7B, 0xFB,
			0x07, 0x87, 0x47, 0xC7, 0x27, 0xA7, 0x67, 0xE7,
			0x17, 0x97, 0x57, 0xD7, 0x37, 0xB7, 0x77, 0xF7,
			0x0F, 0x8F, 0x4F, 0xCF, 0x2F, 0xAF, 0x6F, 0xEF,
			0x1F, 0x9F, 0x5F, 0xDF, 0x3F, 0xBF, 0x7F, 0xFF
		};

		static readonly byte[] twobppswaptable=new byte[]
		{
			0x00, 0x40, 0x80, 0xC0, 0x10, 0x50, 0x90, 0xD0,
			0x20, 0x60, 0xA0, 0xE0, 0x30, 0x70, 0xB0, 0xF0,
			0x04, 0x44, 0x84, 0xC4, 0x14, 0x54, 0x94, 0xD4,
			0x24, 0x64, 0xA4, 0xE4, 0x34, 0x74, 0xB4, 0xF4,
			0x08, 0x48, 0x88, 0xC8, 0x18, 0x58, 0x98, 0xD8,
			0x28, 0x68, 0xA8, 0xE8, 0x38, 0x78, 0xB8, 0xF8,
			0x0C, 0x4C, 0x8C, 0xCC, 0x1C, 0x5C, 0x9C, 0xDC,
			0x2C, 0x6C, 0xAC, 0xEC, 0x3C, 0x7C, 0xBC, 0xFC,
			0x01, 0x41, 0x81, 0xC1, 0x11, 0x51, 0x91, 0xD1,
			0x21, 0x61, 0xA1, 0xE1, 0x31, 0x71, 0xB1, 0xF1,
			0x05, 0x45, 0x85, 0xC5, 0x15, 0x55, 0x95, 0xD5,
			0x25, 0x65, 0xA5, 0xE5, 0x35, 0x75, 0xB5, 0xF5,
			0x09, 0x49, 0x89, 0xC9, 0x19, 0x59, 0x99, 0xD9,
			0x29, 0x69, 0xA9, 0xE9, 0x39, 0x79, 0xB9, 0xF9,
			0x0D, 0x4D, 0x8D, 0xCD, 0x1D, 0x5D, 0x9D, 0xDD,
			0x2D, 0x6D, 0xAD, 0xED, 0x3D, 0x7D, 0xBD, 0xFD,
			0x02, 0x42, 0x82, 0xC2, 0x12, 0x52, 0x92, 0xD2,
			0x22, 0x62, 0xA2, 0xE2, 0x32, 0x72, 0xB2, 0xF2,
			0x06, 0x46, 0x86, 0xC6, 0x16, 0x56, 0x96, 0xD6,
			0x26, 0x66, 0xA6, 0xE6, 0x36, 0x76, 0xB6, 0xF6,
			0x0A, 0x4A, 0x8A, 0xCA, 0x1A, 0x5A, 0x9A, 0xDA,
			0x2A, 0x6A, 0xAA, 0xEA, 0x3A, 0x7A, 0xBA, 0xFA,
			0x0E, 0x4E, 0x8E, 0xCE, 0x1E, 0x5E, 0x9E, 0xDE,
			0x2E, 0x6E, 0xAE, 0xEE, 0x3E, 0x7E, 0xBE, 0xFE,
			0x03, 0x43, 0x83, 0xC3, 0x13, 0x53, 0x93, 0xD3,
			0x23, 0x63, 0xA3, 0xE3, 0x33, 0x73, 0xB3, 0xF3,
			0x07, 0x47, 0x87, 0xC7, 0x17, 0x57, 0x97, 0xD7,
			0x27, 0x67, 0xA7, 0xE7, 0x37, 0x77, 0xB7, 0xF7,
			0x0B, 0x4B, 0x8B, 0xCB, 0x1B, 0x5B, 0x9B, 0xDB,
			0x2B, 0x6B, 0xAB, 0xEB, 0x3B, 0x7B, 0xBB, 0xFB,
			0x0F, 0x4F, 0x8F, 0xCF, 0x1F, 0x5F, 0x9F, 0xDF,
			0x2F, 0x6F, 0xAF, 0xEF, 0x3F, 0x7F, 0xBF, 0xFF
		};

		static readonly byte[] fourbppswaptable=new byte[]
		{
			0x00, 0x10, 0x20, 0x30, 0x40, 0x50, 0x60, 0x70,
			0x80, 0x90, 0xA0, 0xB0, 0xC0, 0xD0, 0xE0, 0xF0,
			0x01, 0x11, 0x21, 0x31, 0x41, 0x51, 0x61, 0x71,
			0x81, 0x91, 0xA1, 0xB1, 0xC1, 0xD1, 0xE1, 0xF1,
			0x02, 0x12, 0x22, 0x32, 0x42, 0x52, 0x62, 0x72,
			0x82, 0x92, 0xA2, 0xB2, 0xC2, 0xD2, 0xE2, 0xF2,
			0x03, 0x13, 0x23, 0x33, 0x43, 0x53, 0x63, 0x73,
			0x83, 0x93, 0xA3, 0xB3, 0xC3, 0xD3, 0xE3, 0xF3,
			0x04, 0x14, 0x24, 0x34, 0x44, 0x54, 0x64, 0x74,
			0x84, 0x94, 0xA4, 0xB4, 0xC4, 0xD4, 0xE4, 0xF4,
			0x05, 0x15, 0x25, 0x35, 0x45, 0x55, 0x65, 0x75,
			0x85, 0x95, 0xA5, 0xB5, 0xC5, 0xD5, 0xE5, 0xF5,
			0x06, 0x16, 0x26, 0x36, 0x46, 0x56, 0x66, 0x76,
			0x86, 0x96, 0xA6, 0xB6, 0xC6, 0xD6, 0xE6, 0xF6,
			0x07, 0x17, 0x27, 0x37, 0x47, 0x57, 0x67, 0x77,
			0x87, 0x97, 0xA7, 0xB7, 0xC7, 0xD7, 0xE7, 0xF7,
			0x08, 0x18, 0x28, 0x38, 0x48, 0x58, 0x68, 0x78,
			0x88, 0x98, 0xA8, 0xB8, 0xC8, 0xD8, 0xE8, 0xF8,
			0x09, 0x19, 0x29, 0x39, 0x49, 0x59, 0x69, 0x79,
			0x89, 0x99, 0xA9, 0xB9, 0xC9, 0xD9, 0xE9, 0xF9,
			0x0A, 0x1A, 0x2A, 0x3A, 0x4A, 0x5A, 0x6A, 0x7A,
			0x8A, 0x9A, 0xAA, 0xBA, 0xCA, 0xDA, 0xEA, 0xFA,
			0x0B, 0x1B, 0x2B, 0x3B, 0x4B, 0x5B, 0x6B, 0x7B,
			0x8B, 0x9B, 0xAB, 0xBB, 0xCB, 0xDB, 0xEB, 0xFB,
			0x0C, 0x1C, 0x2C, 0x3C, 0x4C, 0x5C, 0x6C, 0x7C,
			0x8C, 0x9C, 0xAC, 0xBC, 0xCC, 0xDC, 0xEC, 0xFC,
			0x0D, 0x1D, 0x2D, 0x3D, 0x4D, 0x5D, 0x6D, 0x7D,
			0x8D, 0x9D, 0xAD, 0xBD, 0xCD, 0xDD, 0xED, 0xFD,
			0x0E, 0x1E, 0x2E, 0x3E, 0x4E, 0x5E, 0x6E, 0x7E,
			0x8E, 0x9E, 0xAE, 0xBE, 0xCE, 0xDE, 0xEE, 0xFE,
			0x0F, 0x1F, 0x2F, 0x3F, 0x4F, 0x5F, 0x6F, 0x7F,
			0x8F, 0x9F, 0xAF, 0xBF, 0xCF, 0xDF, 0xEF, 0xFF
		};
		#endregion

		// swaps pixel packing order within bytes
		static unsafe void png_do_packswap(ref png_row_info row_info, byte[] row)
		{
			if(row_info.bit_depth<8)
			{
				byte[] table;
				if(row_info.bit_depth==1) table=onebppswaptable;
				else if(row_info.bit_depth==2) table=twobppswaptable;
				else if(row_info.bit_depth==4) table=fourbppswaptable;
				else return;

				fixed(byte* row_=row)
				{
					byte* rp=row_+1;// skip filter value
					byte* end=rp+row_info.rowbytes;

					for(; rp<end; rp++) *rp=table[*rp];
				}
			}
		}

		// remove filler or alpha byte(s)
		static unsafe void png_do_strip_filler(ref png_row_info row_info, byte[] row, PNG_FLAG flags)
		{
			fixed(byte* row_=row)
			{
				byte* sp=row_+1;// skip filter value
				byte* dp=row_+1;// skip filter value

				uint row_width=row_info.width;
				uint i;

				if(row_info.channels==4&&(row_info.color_type==PNG_COLOR_TYPE.RGB||
					(row_info.color_type==PNG_COLOR_TYPE.RGB_ALPHA&&(flags&PNG_FLAG.STRIP_ALPHA)==PNG_FLAG.STRIP_ALPHA)))
				{
					if(row_info.bit_depth==8)
					{
						// This converts from RGBX or RGBA to RGB
						if((flags&PNG_FLAG.FILLER_AFTER)==PNG_FLAG.FILLER_AFTER)
						{
							dp+=3; sp+=4;
							for(i=1; i<row_width; i++)
							{
								*dp++=*sp++;
								*dp++=*sp++;
								*dp++=*sp++;
								sp++;
							}
						}
						// This converts from XRGB or ARGB to RGB
						else
						{
							for(i=0; i<row_width; i++)
							{
								sp++;
								*dp++=*sp++;
								*dp++=*sp++;
								*dp++=*sp++;
							}
						}
						row_info.pixel_depth=24;
						row_info.rowbytes=row_width*3;
					}
					else // if(row_info->bit_depth==16)
					{
						if((flags&PNG_FLAG.FILLER_AFTER)==PNG_FLAG.FILLER_AFTER)
						{
							// This converts from RRGGBBXX or RRGGBBAA to RRGGBB
							sp+=8; dp+=6;
							for(i=1; i<row_width; i++)
							{
								*dp++=*sp++;
								*dp++=*sp++;
								*dp++=*sp++;
								*dp++=*sp++;
								*dp++=*sp++;
								*dp++=*sp++;
								sp+=2;
							}
						}
						else
						{
							// This converts from XXRRGGBB or AARRGGBB to RRGGBB
							for(i=0; i<row_width; i++)
							{
								sp+=2;
								*dp++=*sp++;
								*dp++=*sp++;
								*dp++=*sp++;
								*dp++=*sp++;
								*dp++=*sp++;
								*dp++=*sp++;
							}
						}
						row_info.pixel_depth=48;
						row_info.rowbytes=row_width*6;
					}
					row_info.channels=3;
				}
				else if(row_info.channels==2&&(row_info.color_type==PNG_COLOR_TYPE.GRAY||
					(row_info.color_type==PNG_COLOR_TYPE.GRAY_ALPHA&&(flags&PNG_FLAG.STRIP_ALPHA)==PNG_FLAG.STRIP_ALPHA)))
				{
					if(row_info.bit_depth==8)
					{
						// This converts from GX or GA to G
						if((flags&PNG_FLAG.FILLER_AFTER)==PNG_FLAG.FILLER_AFTER)
						{
							for(i=0; i<row_width; i++)
							{
								*dp++=*sp++;
								sp++;
							}
						}
						// This converts from XG or AG to G
						else
						{
							for(i=0; i<row_width; i++)
							{
								sp++;
								*dp++=*sp++;
							}
						}
						row_info.pixel_depth=8;
						row_info.rowbytes=row_width;
					}
					else // if(row_info->bit_depth==16)
					{
						if((flags&PNG_FLAG.FILLER_AFTER)==PNG_FLAG.FILLER_AFTER)
						{
							// This converts from GGXX or GGAA to GG
							sp+=4; dp+=2;
							for(i=1; i<row_width; i++)
							{
								*dp++=*sp++;
								*dp++=*sp++;
								sp+=2;
							}
						}
						else
						{
							// This converts from XXGG or AAGG to GG
							for(i=0; i<row_width; i++)
							{
								sp+=2;
								*dp++=*sp++;
								*dp++=*sp++;
							}
						}
						row_info.pixel_depth=16;
						row_info.rowbytes=row_width*2;
					}
					row_info.channels=1;
				}
			}
			if((flags&PNG_FLAG.STRIP_ALPHA)==PNG_FLAG.STRIP_ALPHA) row_info.color_type&=~PNG_COLOR_TYPE.ALPHA_MASK;
		}

		// swaps red and blue bytes within a pixel
		static unsafe void png_do_bgr(ref png_row_info row_info, byte[] row)
		{
			fixed(byte* row_=row)
			{
				byte* rp=row_+1;// skip filter value
				if((row_info.color_type&PNG_COLOR_TYPE.COLOR_MASK)==PNG_COLOR_TYPE.COLOR_MASK)
				{
					uint row_width=row_info.width;
					if(row_info.bit_depth==8)
					{
						if(row_info.color_type==PNG_COLOR_TYPE.RGB)
						{
							for(uint i=0; i<row_width; i++, rp+=3)
							{
								byte save=*rp;
								*rp=*(rp+2);
								*(rp+2)=save;
							}
						}
						else if(row_info.color_type==PNG_COLOR_TYPE.RGB_ALPHA)
						{
							for(uint i=0; i<row_width; i++, rp+=4)
							{
								byte save=*rp;
								*rp=*(rp+2);
								*(rp+2)=save;
							}
						}
					}
					else if(row_info.bit_depth==16)
					{
						if(row_info.color_type==PNG_COLOR_TYPE.RGB)
						{
							for(uint i=0; i<row_width; i++, rp+=6)
							{
								byte save=*rp;
								*rp=*(rp+4);
								*(rp+4)=save;
								save=*(rp+1);
								*(rp+1)=*(rp+5);
								*(rp+5)=save;
							}
						}
						else if(row_info.color_type==PNG_COLOR_TYPE.RGB_ALPHA)
						{
							for(uint i=0; i<row_width; i++, rp+=8)
							{
								byte save=*rp;
								*rp=*(rp+4);
								*(rp+4)=save;
								save=*(rp+1);
								*(rp+1)=*(rp+5);
								*(rp+5)=save;
							}
						}
					}
				}
			}
		}

		public void png_set_user_transform_info(object user_transform_ptr, byte user_transform_depth, byte user_transform_channels)
		{
			this.user_transform_ptr=user_transform_ptr;
			this.user_transform_depth=user_transform_depth;
			this.user_transform_channels=user_transform_channels;
		}

		// This function returns a pointer to the user_transform_ptr associated with
		// the user transform functions. The application should free any memory
		// associated with this pointer before png_write_destroy and png_read_destroy
		// are called.
		public object png_get_user_transform_ptr()
		{
			return user_transform_ptr;
		}
	}
}
