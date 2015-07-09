// pngrtran.cs - transforms the data in a row for PNG readers
//
// Based on libpng version 1.4.3 - June 26, 2010
// This code is released under the libpng license.
// For conditions of distribution and use, see copyright notice in License.txt
// Copyright (C) 2007-2010 by the Authors
// Copyright (c) 1998-2010 Glenn Randers-Pehrson
// (Version 0.96 Copyright (c) 1996, 1997 Andreas Dilger)
// (Version 0.88 Copyright (c) 1995, 1996 Guy Eric Schalnat, Group 42, Inc.)
//
// This file contains functions optionally called by an application
// in order to tell libpng how to handle data when reading a PNG.
// Transformations that are used in both reading and writing are
// in pngtrans.cs.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Free.Ports.libpng
{
	// Quantize file to 8 bit. Supply a palette, the current number
	// of elements in the palette, the maximum number of elements
	// allowed, and a histogram if possible. If the current number
	// of colors is greater then the maximum number, the palette will be
	// modified to fit in the maximum number. "full_quantize" indicates
	// whether we need a quantizeing cube set up for RGB images, or if we
	// simply are reducing the number of colors in a paletted image.
	class png_dsort
	{
		public png_dsort next;
		public byte left;
		public byte right;
	}

	public partial class png_struct
	{
		// This controls how fine the quantizing gets. As this allocates
		// a largish chunk of memory (32K), those who are not as concerned
		// with quantizing quality can decrease some or all of these.
		const int PNG_QUANTIZE_RED_BITS=5;
		const int PNG_QUANTIZE_GREEN_BITS=5;
		const int PNG_QUANTIZE_BLUE_BITS=5;

		[Obsolete("These migration aids will be removed from libpng-1.5.0. Use 'PNG_DITHER_*_BITS' instead.")]
		const int PNG_DITHER_RED_BITS=PNG_QUANTIZE_RED_BITS;
		[Obsolete("These migration aids will be removed from libpng-1.5.0. Use 'PNG_DITHER_*_BITS' instead.")]
		const int PNG_DITHER_GREEN_BITS=PNG_QUANTIZE_GREEN_BITS;
		[Obsolete("These migration aids will be removed from libpng-1.5.0. Use 'PNG_DITHER_*_BITS' instead.")]
		const int PNG_DITHER_BLUE_BITS=PNG_QUANTIZE_BLUE_BITS;

		// This controls how fine the gamma correction becomes when you
		// are only interested in 8 bits anyway. Increasing this value
		// results in more memory being used, and more pow() functions
		// being called to fill in the gamma tables. Don't set this value
		// less then 8, and even that may not work (I haven't tested it).
		const int PNG_MAX_GAMMA_8=11;

		// This controls how much a difference in gamma we can tolerate before
		// we actually start doing gamma conversion.
		const double PNG_GAMMA_THRESHOLD=0.05;

		#region some png_set methods
		// Set the action on getting a CRC error for an ancillary or critical chunk.
		public void png_set_crc_action(PNG_CRC crit_action, PNG_CRC ancil_action)
		{
			// Tell libpng how we react to CRC errors in critical chunks
			switch(crit_action)
			{
				case PNG_CRC.NO_CHANGE: break; // leave setting as is
				case PNG_CRC.WARN_USE: // warn/use data
					flags&=~PNG_FLAG.CRC_CRITICAL_MASK;
					flags|=PNG_FLAG.CRC_CRITICAL_USE;
					break;
				case PNG_CRC.QUIET_USE: // quiet/use data
					flags&=~PNG_FLAG.CRC_CRITICAL_MASK;
					flags|=PNG_FLAG.CRC_CRITICAL_MASK;
					break;
				case PNG_CRC.WARN_DISCARD: // not a valid action for critical data
					Debug.WriteLine("Can't discard critical data on CRC error");
					flags&=~PNG_FLAG.CRC_CRITICAL_MASK;
					break;
				case PNG_CRC.ERROR_QUIT: // error/quit
				case PNG_CRC.DEFAULT:
				default:
					flags&=~PNG_FLAG.CRC_CRITICAL_MASK;
					break;
			}

			switch(ancil_action)
			{
				case PNG_CRC.NO_CHANGE: break; // leave setting as is
				case PNG_CRC.WARN_USE: // warn/use data
					flags&=~PNG_FLAG.CRC_ANCILLARY_MASK;
					flags|=PNG_FLAG.CRC_ANCILLARY_USE;
					break;
				case PNG_CRC.QUIET_USE: // quiet/use data
					flags&=~PNG_FLAG.CRC_ANCILLARY_MASK;
					flags|=PNG_FLAG.CRC_ANCILLARY_MASK;
					break;
				case PNG_CRC.ERROR_QUIT: // error/quit
					flags&=~PNG_FLAG.CRC_ANCILLARY_MASK;
					flags|=PNG_FLAG.CRC_ANCILLARY_NOWARN;
					break;
				case PNG_CRC.WARN_DISCARD: // warn/discard data
				case PNG_CRC.DEFAULT:
				default:
					flags&=~PNG_FLAG.CRC_ANCILLARY_MASK;
					break;
			}
		}

		// handle alpha and tRNS via a background color
		public void png_set_background(png_color_16 background_color, PNG_BACKGROUND_GAMMA background_gamma_code, bool need_expand, double background_gamma)
		{
			if(background_gamma_code==PNG_BACKGROUND_GAMMA.UNKNOWN)
			{
				Debug.WriteLine("Application must supply a known background gamma");
				return;
			}

			transformations|=PNG_TRANSFORMATION.BACKGROUND;
			background=background_color;
			this.background_gamma=background_gamma;
			background_gamma_type=background_gamma_code;
			transformations|=(need_expand?PNG_TRANSFORMATION.BACKGROUND_EXPAND:PNG_TRANSFORMATION.None);
		}

		// strip 16 bit depth files to 8 bit depth
		public void png_set_strip_16()
		{
			transformations|=PNG_TRANSFORMATION._16_TO_8;
		}

		public void png_set_strip_alpha()
		{
			flags|=PNG_FLAG.STRIP_ALPHA;
		}

		#region png_set_quantize (formely known as png_set_dither)
		// Dither file to 8 bit. Supply a palette, the current number
		// of elements in the palette, the maximum number of elements
		// allowed, and a histogram if possible. If the current number
		// of colors is greater then the maximum number, the palette will be
		// modified to fit in the maximum number. "full_quantize" indicates
		// whether we need a quantizing cube set up for RGB images, or if we
		// simply are reducing the number of colors in a paletted image.

		// This migration aid will be removed from libpng-1.5.0
		[Obsolete("This migration aid will be removed from libpng-1.5.0, Use 'png_set_quantize' instead.")]
		public void png_set_dither(png_color[] palette, byte maximum_colors, ushort[] histogram, bool full_dither)
		{
			png_set_quantize(palette, maximum_colors, histogram, full_dither);
		}

		// Prior to libpng-1.4.2, this was png_set_dither().
		public void png_set_quantize(png_color[] palette, byte maximum_colors, ushort[] histogram, bool full_quantize)
		{
			transformations|=PNG_TRANSFORMATION.QUANTIZE;

			if(!full_quantize)
			{
				quantize_index=new byte[palette.Length];
				for(int i=0; i<palette.Length; i++) quantize_index[i]=(byte)i;
			}

			if(palette.Length>maximum_colors)
			{
				if(histogram!=null)
				{
					// This is easy enough, just throw out the least used colors.
					// Perhaps not the best solution, but good enough.

					// initialize an array to sort colors
					byte[] quantize_sort=new byte[palette.Length];

					// initialize the quantize_sort array
					for(int i=0; i<palette.Length; i++) quantize_sort[i]=(byte)i;

					// Find the least used palette entries by starting a
					// bubble sort, and running it until we have sorted
					// out enough colors. Note that we don't care about
					// sorting all the colors, just finding which are
					// least used.
					for(int i=palette.Length-1; i>=maximum_colors; i--)
					{
						bool done=true; // to stop early if the list is pre-sorted

						for(int j=0; j<i; j++)
						{
							if(histogram[quantize_sort[j]]<histogram[quantize_sort[j+1]])
							{
								byte t=quantize_sort[j];
								quantize_sort[j]=quantize_sort[j+1];
								quantize_sort[j+1]=t;
								done=false;
							}
						}
						if(done) break;
					}

					// swap the palette around, and set up a table, if necessary
					if(full_quantize)
					{
						int j=palette.Length;

						// put all the useful colors within the max, but don't move the others
						for(int i=0; i<maximum_colors; i++)
						{
							if(quantize_sort[i]>=maximum_colors)
							{
								do { j--; } while(quantize_sort[j]>=maximum_colors);
								palette[i]=palette[j];
							}
						}
					}
					else
					{
						int j=palette.Length;

						// move all the used colors inside the max limit, and develop a translation table
						for(int i=0; i<maximum_colors; i++)
						{
							// only move the colors we need to
							if(quantize_sort[i]>=maximum_colors)
							{
								do { j--; } while(quantize_sort[j]>=maximum_colors);

								png_color tmp_color=palette[j];
								palette[j]=palette[i];
								palette[i]=tmp_color;

								// indicate where the color went
								quantize_index[j]=(byte)i;
								quantize_index[i]=(byte)j;
							}
						}

						// find closest color for those colors we are not using
						for(int i=0; i<palette.Length; i++)
						{
							if(quantize_index[i]>=maximum_colors)
							{
								// find the closest color to one we threw out
								int d_index=quantize_index[i];
								int min_d=PNG_COLOR_DIST(palette[d_index], palette[0]);
								int min_k=0;
								for(int k=1; k<maximum_colors; k++)
								{
									int d=PNG_COLOR_DIST(palette[d_index], palette[k]);

									if(d<min_d)
									{
										min_d=d;
										min_k=k;
									}
								}
								// point to closest color
								quantize_index[i]=(byte)min_k;
							}
						}
					}
					quantize_sort=null;
				}
				else // if(histogram!=null)
				{
					// This is much harder to do simply (and quickly). Perhaps
					// we need to go through a median cut routine, but those
					// don't always behave themselves with only a few colors
					// as input. So we will just find the closest two colors,
					// and throw out one of them (chosen somewhat randomly).
					// [We don't understand this at all, so if someone wants to
					// work on improving it, be our guest - AED, GRP]

					// initialize palette index arrays
					byte[] index_to_palette=new byte[palette.Length];
					byte[] palette_to_index=new byte[palette.Length];

					// initialize the sort array
					for(int i=0; i<palette.Length; i++)
					{
						index_to_palette[i]=(byte)i;
						palette_to_index[i]=(byte)i;
					}

					png_dsort[] hash=new png_dsort[769];

					int num_new_palette=palette.Length;

					// initial wild guess at how far apart the farthest pixel
					// pair we will be eliminating will be. Larger
					// numbers mean more areas will be allocated, Smaller
					// numbers run the risk of not saving enough data, and
					// having to do this all over again.
					//
					// I have not done extensive checking on this number.
					int max_d=96;

					png_dsort t=null;

					while(num_new_palette>maximum_colors)
					{
						for(int i=0; i<num_new_palette-1; i++)
						{
							for(int j=i+1; j<num_new_palette; j++)
							{
								int d=PNG_COLOR_DIST(palette[i], palette[j]);

								if(d<=max_d)
								{
									try
									{
										t=new png_dsort();
									}
									catch(Exception)
									{
										t=null;
										break;
									}
									t.next=hash[d];
									t.left=(byte)i;
									t.right=(byte)j;
									hash[d]=t;
								}
							}
							if(t==null) break;
						}

						if(t!=null)
						{
							for(int i=0; i<=max_d; i++)
							{
								if(hash[i]!=null)
								{
									for(png_dsort p=hash[i]; p!=null; p=p.next)
									{
										if(index_to_palette[p.left]<num_new_palette&&index_to_palette[p.right]<num_new_palette)
										{
											int j, next_j;

											if((num_new_palette&0x01)==0x01)
											{
												j=p.left;
												next_j=p.right;
											}
											else
											{
												j=p.right;
												next_j=p.left;
											}

											num_new_palette--;
											palette[index_to_palette[j]]=palette[num_new_palette];
											if(!full_quantize)
											{
												for(int k=0; k<palette.Length; k++)
												{
													if(quantize_index[k]==index_to_palette[j]) quantize_index[k]=index_to_palette[next_j];
													if(quantize_index[k]==num_new_palette) quantize_index[k]=index_to_palette[j];
												}
											}

											index_to_palette[palette_to_index[num_new_palette]]=index_to_palette[j];
											palette_to_index[index_to_palette[j]]=palette_to_index[num_new_palette];

											index_to_palette[j]=(byte)num_new_palette;
											palette_to_index[num_new_palette]=(byte)j;
										}
										if(num_new_palette<=maximum_colors) break;
									}
									if(num_new_palette<=maximum_colors) break;
								}
							} // for
						}

						for(int i=0; i<769; i++)
						{
							if(hash[i]!=null)
							{
								png_dsort p=hash[i];
								while(p!=null)
								{
									t=p.next;
									p.next=null;
									p=t;
								}
							}
							hash[i]=null;
						}
						max_d+=96;
					}
					t=null;
					hash=null;
					palette_to_index=null;
					index_to_palette=null;
				}

				png_color[] tmppal=new png_color[maximum_colors];
				Array.Copy(palette, tmppal, maximum_colors);
				palette=tmppal;

			} // if(num_palette>maximum_colors)

			if(this.palette==null) this.palette=palette;
			if(!full_quantize) return;

			int total_bits=PNG_QUANTIZE_RED_BITS+PNG_QUANTIZE_GREEN_BITS+PNG_QUANTIZE_BLUE_BITS;
			int num_red=1<<PNG_QUANTIZE_RED_BITS;
			int num_green=1<<PNG_QUANTIZE_GREEN_BITS;
			int num_blue=1<<PNG_QUANTIZE_BLUE_BITS;
			uint num_entries=(uint)(1<<total_bits);

			palette_lookup=new byte[num_entries];

			byte[] distance=new byte[num_entries];
			for(int i=0; i<num_entries; i++) distance[i]=0xff; //memset(distance, 0xff, num_entries*sizeof(byte));

			for(int i=0; i<palette.Length; i++)
			{
				int r=palette[i].red>>(8-PNG_QUANTIZE_RED_BITS);
				int g=palette[i].green>>(8-PNG_QUANTIZE_GREEN_BITS);
				int b=palette[i].blue>>(8-PNG_QUANTIZE_BLUE_BITS);

				for(int ir=0; ir<num_red; ir++)
				{
					// int dr=abs(ir-r);
					int dr=(ir>r)?ir-r:r-ir;
					int index_r=ir<<(PNG_QUANTIZE_BLUE_BITS+PNG_QUANTIZE_GREEN_BITS);

					for(int ig=0; ig<num_green; ig++)
					{
						// int dg=abs(ig-g);
						int dg=(ig>g)?ig-g:g-ig;
						int dt=dr+dg;
						int dm=(dr>dg)?dr:dg;
						int index_g=index_r|ig<<PNG_QUANTIZE_BLUE_BITS;

						for(int ib=0; ib<num_blue; ib++)
						{
							int d_index=index_g|ib;
							// int db=abs(ib-b);
							int db=(ib>b)?ib-b:b-ib;
							int dmax=(dm>db)?dm:db;
							int d=dmax+dt+db;

							if(d<distance[d_index])
							{
								distance[d_index]=(byte)d;
								palette_lookup[d_index]=(byte)i;
							}
						}
					}
				}
			}

			distance=null;
		}
		#endregion

		// Transform the image from the file_gamma to the screen_gamma. We
		// only do transformations on images where the file_gamma and screen_gamma
		// are not close reciprocals, otherwise it slows things down slightly, and
		// also needlessly introduces small errors.
		//
		// We will turn off gamma transformation later if no semitransparent entries
		// are present in the tRNS array for palette images. We can't do it here
		// because we don't necessarily have the tRNS chunk yet.
		public void png_set_gamma(double scrn_gamma, double file_gamma)
		{
			if(Math.Abs(scrn_gamma*file_gamma-1.0)>PNG_GAMMA_THRESHOLD||
				(color_type&PNG_COLOR_TYPE.ALPHA_MASK)==PNG_COLOR_TYPE.ALPHA_MASK||color_type==PNG_COLOR_TYPE.PALETTE)
				transformations|=PNG_TRANSFORMATION.GAMMA;
			gamma=file_gamma;
			screen_gamma=scrn_gamma;
		}

		// Expand paletted images to RGB, expand grayscale images of
		// less than 8-bit depth to 8-bit depth, and expand tRNS chunks
		// to alpha channels.
		public void png_set_expand()
		{
			transformations|=(PNG_TRANSFORMATION.EXPAND|PNG_TRANSFORMATION.EXPAND_tRNS);
			flags&=~PNG_FLAG.ROW_INIT;
		}

		// GRR 19990627: The following three functions currently are identical
		// to png_set_expand(). However, it is entirely reasonable that someone
		// might wish to expand an indexed image to RGB but *not* expand a single,
		// fully transparent palette entry to a full alpha channel--perhaps instead
		// convert tRNS to the grayscale/RGB format (16-bit RGB value), or replace
		// the transparent color with a particular RGB value, or drop tRNS entirely.
		// IOW, a future version of the library may make the transformations flag
		// a bit more fine-grained, with separate bits for each of these three
		// functions.
		//
		// More to the point, these functions make it obvious what libpng will be
		// doing, whereas "expand" can (and does) mean any number of things.
		//
		// GRP 20060307: In libpng-1.2.9, png_set_gray_1_2_4_to_8() was modified
		// to expand only the sample depth but not to expand the tRNS to alpha
		// and its name was changed to png_set_expand_gray_1_2_4_to_8().

		// Expand paletted images to RGB.
		public void png_set_palette_to_rgb()
		{
			transformations|=(PNG_TRANSFORMATION.EXPAND|PNG_TRANSFORMATION.EXPAND_tRNS);
			flags&=~PNG_FLAG.ROW_INIT;
		}

		// Expand grayscale images of less than 8-bit depth to 8 bits.
		public void png_set_expand_gray_1_2_4_to_8()
		{
			transformations|=PNG_TRANSFORMATION.EXPAND;
			flags&=~PNG_FLAG.ROW_INIT;
		}

		// Expand tRNS chunks to alpha channels.
		public void png_set_tRNS_to_alpha()
		{
			transformations|=(PNG_TRANSFORMATION.EXPAND|PNG_TRANSFORMATION.EXPAND_tRNS);
			flags&=~PNG_FLAG.ROW_INIT;
		}

		public void png_set_gray_to_rgb()
		{
			transformations|=PNG_TRANSFORMATION.GRAY_TO_RGB;
			flags&=~PNG_FLAG.ROW_INIT;
		}

		// Convert a RGB image to a grayscale of the same width. This allows us,
		// for example, to convert a 24 bpp RGB image into an 8 bpp grayscale image.
		public void png_set_rgb_to_gray(int error_action, double red, double green)
		{
			int red_fixed=(int)(red*100000.0+0.5);
			int green_fixed=(int)(green*100000.0+0.5);
			png_set_rgb_to_gray_fixed(error_action, red_fixed, green_fixed);
		}

		public void png_set_rgb_to_gray_fixed(int error_action, int red, int green)
		{
			switch(error_action)
			{
				case 1: transformations|=PNG_TRANSFORMATION.RGB_TO_GRAY; break;
				case 2: transformations|=PNG_TRANSFORMATION.RGB_TO_GRAY_WARN; break;
				case 3: transformations|=PNG_TRANSFORMATION.RGB_TO_GRAY_ERR; break;
			}
			if(color_type==PNG_COLOR_TYPE.PALETTE) transformations|=PNG_TRANSFORMATION.EXPAND;

			ushort red_int, green_int;
			if(red<0||green<0)
			{
				red_int=6968;		// 0.212671*32768+0.5
				green_int=23434;	// 0.715160*32768+0.5
			}
			else if(red+green<100000)
			{
				red_int=(ushort)(((uint)red*32768)/100000);
				green_int=(ushort)(((uint)green*32768)/100000);
			}
			else
			{
				Debug.WriteLine("ignoring out of range rgb_to_gray coefficients");
				red_int=6968;
				green_int=23434;
			}
			rgb_to_gray_red_coeff=red_int;
			rgb_to_gray_green_coeff=green_int;
			rgb_to_gray_blue_coeff=(ushort)(32768-red_int-green_int);
		}

		public void png_set_read_user_transform_fn(png_user_transform_ptr read_user_transform_fn)
		{
			transformations|=PNG_TRANSFORMATION.USER_TRANSFORM;
			this.read_user_transform_fn=read_user_transform_fn;
		}
		#endregion

		#region png_init_read_transformations
		// Initialize everything needed for the read. This includes modifying
		// the palette.
		void png_init_read_transformations()
		{
			// Detect gray background and attempt to enable optimization
			// for gray --> RGB case
			// Note: f PNG_BACKGROUND_EXPAND is set and color_type is either RGB or
			// RGB_ALPHA (in which case need_expand is superfluous anyway), the
			// background color might actually be gray yet not be flagged as such.
			// This is not a problem for the current code, which uses
			// PNG_BACKGROUND_IS_GRAY only to decide when to do the
			// png_do_gray_to_rgb() transformation.
			if((transformations&PNG_TRANSFORMATION.BACKGROUND_EXPAND)==PNG_TRANSFORMATION.BACKGROUND_EXPAND&&
				(color_type&PNG_COLOR_TYPE.COLOR_MASK)!=PNG_COLOR_TYPE.COLOR_MASK)
			{
				mode|=PNG_MODE.BACKGROUND_IS_GRAY;
			}
			else if((transformations&PNG_TRANSFORMATION.BACKGROUND)==PNG_TRANSFORMATION.BACKGROUND&&
				(transformations&PNG_TRANSFORMATION.BACKGROUND_EXPAND)!=PNG_TRANSFORMATION.BACKGROUND_EXPAND&&
				(transformations&PNG_TRANSFORMATION.GRAY_TO_RGB)==PNG_TRANSFORMATION.GRAY_TO_RGB&&
				background.red==background.green&&background.red==background.blue)
			{
				mode|=PNG_MODE.BACKGROUND_IS_GRAY;
				background.gray=background.red;
			}

			if((transformations&PNG_TRANSFORMATION.BACKGROUND_EXPAND)==PNG_TRANSFORMATION.BACKGROUND_EXPAND&&
				(transformations&PNG_TRANSFORMATION.EXPAND)==PNG_TRANSFORMATION.EXPAND)
			{
				if((color_type&PNG_COLOR_TYPE.COLOR_MASK)!=PNG_COLOR_TYPE.COLOR_MASK) // i.e., GRAY or GRAY_ALPHA
				{
					// expand background and tRNS chunks
					switch(bit_depth)
					{
						case 1:
							background.gray*=(ushort)0xff;
							background.red=background.green=background.blue=background.gray;
							if((transformations&PNG_TRANSFORMATION.EXPAND_tRNS)!=PNG_TRANSFORMATION.EXPAND_tRNS)
							{
								trans_color.gray*=(ushort)0xff;
								trans_color.red=trans_color.green=trans_color.blue=trans_color.gray;
							}
							break;
						case 2:
							background.gray*=(ushort)0x55;
							background.red=background.green=background.blue=background.gray;
							if((transformations&PNG_TRANSFORMATION.EXPAND_tRNS)!=PNG_TRANSFORMATION.EXPAND_tRNS)
							{
								trans_color.gray*=(ushort)0x55;
								trans_color.red=trans_color.green=trans_color.blue=trans_color.gray;
							}
							break;
						case 4:
							background.gray*=(ushort)0x11;
							background.red=background.green=background.blue=background.gray;
							if((transformations&PNG_TRANSFORMATION.EXPAND_tRNS)!=PNG_TRANSFORMATION.EXPAND_tRNS)
							{
								trans_color.gray*=(ushort)0x11;
								trans_color.red=trans_color.green=trans_color.blue=trans_color.gray;
							}
							break;
						case 8:
						case 16:
							background.red=background.green=background.blue=background.gray;
							break;
					}
				}
				else if(color_type==PNG_COLOR_TYPE.PALETTE)
				{
					background.red=palette[background.index].red;
					background.green=palette[background.index].green;
					background.blue=palette[background.index].blue;

					if((transformations&PNG_TRANSFORMATION.INVERT_ALPHA)==PNG_TRANSFORMATION.INVERT_ALPHA)
					{
						if((transformations&PNG_TRANSFORMATION.EXPAND_tRNS)!=PNG_TRANSFORMATION.EXPAND_tRNS)
						{
							// invert the alpha channel (in tRNS) unless the pixels are
							// going to be expanded, in which case leave it for later
							for(int i=0; i<num_trans; i++) trans_alpha[i]=(byte)(255-trans_alpha[i]);
						}
					}
				}
			}

			background_1=background;

			if((color_type==PNG_COLOR_TYPE.PALETTE&&num_trans!=0)&&Math.Abs(screen_gamma*gamma-1)<PNG_GAMMA_THRESHOLD)
			{
				int k=0;
				for(int i=0; i<num_trans; i++)
				{
					if(trans_alpha[i]!=0&&trans_alpha[i]!=0xff) k=1; // partial transparency is present
				}
				if(k==0) transformations&=~PNG_TRANSFORMATION.GAMMA;
			}

			if((transformations&(PNG_TRANSFORMATION.GAMMA|PNG_TRANSFORMATION.RGB_TO_GRAY))!=PNG_TRANSFORMATION.None&&gamma!=0.0)
			{
				png_build_gamma_table(bit_depth);
				if((transformations&PNG_TRANSFORMATION.BACKGROUND)==PNG_TRANSFORMATION.BACKGROUND)
				{
					if(color_type==PNG_COLOR_TYPE.PALETTE)
					{
						// Could skip if no transparency and
						png_color back, back_1;
						if(background_gamma_type==PNG_BACKGROUND_GAMMA.FILE)
						{
							back.red=gamma_table[background.red];
							back.green=gamma_table[background.green];
							back.blue=gamma_table[background.blue];

							back_1.red=gamma_to_1[background.red];
							back_1.green=gamma_to_1[background.green];
							back_1.blue=gamma_to_1[background.blue];
						}
						else
						{
							double g, gs;

							switch(background_gamma_type)
							{
								case PNG_BACKGROUND_GAMMA.SCREEN:
									g=screen_gamma;
									gs=1.0;
									break;
								case PNG_BACKGROUND_GAMMA.FILE:
									g=1/gamma;
									gs=1/(gamma*screen_gamma);
									break;
								case PNG_BACKGROUND_GAMMA.UNIQUE:
									g=1/background_gamma;
									gs=1/(background_gamma*screen_gamma);
									break;
								default:
									g=1.0;	// back_1
									gs=1.0;	// back
									break;
							}

							if(Math.Abs(gs-1.0)<PNG_GAMMA_THRESHOLD)
							{
								back.red=(byte)background.red;
								back.green=(byte)background.green;
								back.blue=(byte)background.blue;
							}
							else
							{
								back.red=(byte)(Math.Pow(background.red/255.0, gs)*255.0+.5);
								back.green=(byte)(Math.Pow(background.green/255.0, gs)*255.0+.5);
								back.blue=(byte)(Math.Pow(background.blue/255.0, gs)*255.0+.5);
							}

							back_1.red=(byte)(Math.Pow(background.red/255.0, g)*255.0+.5);
							back_1.green=(byte)(Math.Pow(background.green/255.0, g)*255.0+.5);
							back_1.blue=(byte)(Math.Pow(background.blue/255.0, g)*255.0+.5);
						}
						for(int i=0; i<palette.Length; i++)
						{
							if(i<num_trans&&trans_alpha[i]!=0xff)
							{
								if(trans_alpha[i]==0)
								{
									palette[i]=back;
								}
								else
								{
									byte v=gamma_to_1[palette[i].red];
									//png_composite(w, v, png_ptr->trans_alpha[i], back_1.red);
									byte w=(byte)(((ushort)v*(ushort)trans_alpha[i]+(ushort)back_1.red*(ushort)(255-(ushort)trans_alpha[i])+(ushort)127)/255);
									palette[i].red=gamma_from_1[w];

									v=gamma_to_1[palette[i].green];
									//png_composite(w, v, png_ptr->trans_alpha[i], back_1.green);
									w=(byte)(((ushort)v*(ushort)trans_alpha[i]+(ushort)back_1.green*(ushort)(255-(ushort)trans_alpha[i])+(ushort)127)/255);
									palette[i].green=gamma_from_1[w];

									v=gamma_to_1[palette[i].blue];
									//png_composite(w, v, png_ptr->trans_alpha[i], back_1.blue);
									w=(byte)(((ushort)v*(ushort)trans_alpha[i]+(ushort)back_1.blue*(ushort)(255-(ushort)trans_alpha[i])+(ushort)127)/255);
									palette[i].blue=gamma_from_1[w];
								}
							}
							else
							{
								palette[i].red=gamma_table[palette[i].red];
								palette[i].green=gamma_table[palette[i].green];
								palette[i].blue=gamma_table[palette[i].blue];
							}
							// Prevent the transformations being done again, and make sure
							// that the now spurious alpha channel is stripped - the code
							// has just reduced background composition and gamma correction
							// to a simple alpha channel strip.
							transformations&=~PNG_TRANSFORMATION.BACKGROUND;
							transformations&=~PNG_TRANSFORMATION.GAMMA;
							transformations|=PNG_TRANSFORMATION.STRIP_ALPHA;
						}
					} // if(color_type==PNG_COLOR_TYPE_PALETTE)
					else
					{ // color_type!=PNG_COLOR_TYPE_PALETTE
						double m=((uint)1<<bit_depth)-1;
						double g=1.0;
						double gs=1.0;

						switch(background_gamma_type)
						{
							case PNG_BACKGROUND_GAMMA.SCREEN:
								g=(screen_gamma);
								gs=1.0;
								break;
							case PNG_BACKGROUND_GAMMA.FILE:
								g=1/gamma;
								gs=1/(gamma*screen_gamma);
								break;
							case PNG_BACKGROUND_GAMMA.UNIQUE:
								g=1/background_gamma;
								gs=1/(background_gamma*screen_gamma);
								break;
						}

						background_1.gray=(ushort)(Math.Pow(background.gray/m, g)*m+.5);
						background.gray=(ushort)(Math.Pow(background.gray/m, gs)*m+.5);

						if(background.red!=background.green||background.red!=background.blue||background.red!=background.gray)
						{
							// RGB or RGBA with color background
							background_1.red=(ushort)(Math.Pow(background.red/m, g)*m+.5);
							background_1.green=(ushort)(Math.Pow(background.green/m, g)*m+.5);
							background_1.blue=(ushort)(Math.Pow(background.blue/m, g)*m+.5);
							background.red=(ushort)(Math.Pow(background.red/m, gs)*m+.5);
							background.green=(ushort)(Math.Pow(background.green/m, gs)*m+.5);
							background.blue=(ushort)(Math.Pow(background.blue/m, gs)*m+.5);
						}
						else
						{
							// GRAY, GRAY ALPHA, RGB, or RGBA with gray background
							background_1.red=background_1.green=background_1.blue=background_1.gray;
							background.red=background.green=background.blue=background.gray;
						}
					}
				}
				else // transformation does not include PNG_BACKGROUND
				{
					if(color_type==PNG_COLOR_TYPE.PALETTE)
					{
						for(int i=0; i<palette.Length; i++)
						{
							palette[i].red=gamma_table[palette[i].red];
							palette[i].green=gamma_table[palette[i].green];
							palette[i].blue=gamma_table[palette[i].blue];
						}
					}
					// Done the gamma correction.
					transformations&=~PNG_TRANSFORMATION.GAMMA;
				}
			}
			else // No GAMMA transformation
			{
				if((transformations&PNG_TRANSFORMATION.BACKGROUND)==PNG_TRANSFORMATION.BACKGROUND&&color_type==PNG_COLOR_TYPE.PALETTE)
				{
					int istop=num_trans;
					png_color back;

					back.red=(byte)background.red;
					back.green=(byte)background.green;
					back.blue=(byte)background.blue;

					for(int i=0; i<istop; i++)
					{
						if(trans_alpha[i]==0)
						{
							palette[i]=back;
						}
						else if(trans_alpha[i]!=0xff)
						{ // The png_composite() macro is defined in png.h
							//png_composite(palette[i].red, palette[i].red, png_ptr->trans_alpha[i], back.red);
							palette[i].red=(byte)(((ushort)palette[i].red*(ushort)trans_alpha[i]+(ushort)back.red*(ushort)(255-(ushort)trans_alpha[i])+(ushort)127)/255);
							//png_composite(palette[i].green, palette[i].green, png_ptr->trans_alpha[i], back.green);
							palette[i].green=(byte)(((ushort)palette[i].green*(ushort)trans_alpha[i]+(ushort)back.green*(ushort)(255-(ushort)trans_alpha[i])+(ushort)127)/255);
							//png_composite(palette[i].blue, palette[i].blue, png_ptr->trans_alpha[i], back.blue);
							palette[i].blue=(byte)(((ushort)palette[i].blue*(ushort)trans_alpha[i]+(ushort)back.blue*(ushort)(255-(ushort)trans_alpha[i])+(ushort)127)/255);
						}
					}
					// Handled alpha, still need to strip the channel.
					transformations&=~PNG_TRANSFORMATION.BACKGROUND;
					transformations|=PNG_TRANSFORMATION.STRIP_ALPHA;
				}
			}

			if((transformations&PNG_TRANSFORMATION.SHIFT)==PNG_TRANSFORMATION.SHIFT&&color_type==PNG_COLOR_TYPE.PALETTE)
			{
				int istop=palette.Length;
				int sr=8-sig_bit.red;
				int sg=8-sig_bit.green;
				int sb=8-sig_bit.blue;

				if(sr<0||sr>8) sr=0;
				if(sg<0||sg>8) sg=0;
				if(sb<0||sb>8) sb=0;
				for(int i=0; i<istop; i++)
				{
					palette[i].red>>=sr;
					palette[i].green>>=sg;
					palette[i].blue>>=sb;
				}
			}
		}
		#endregion

		#region png_read_transform_info
		// Modify the info structure to reflect the transformations. The
		// info should be updated so a PNG file could be written with it,
		// assuming the transformations result in valid PNG data.
		void png_read_transform_info()
		{
			if((transformations&PNG_TRANSFORMATION.EXPAND)==PNG_TRANSFORMATION.EXPAND)
			{
				if(info_ptr_color_type==PNG_COLOR_TYPE.PALETTE)
				{
					if(num_trans!=0&&(transformations&PNG_TRANSFORMATION.EXPAND_tRNS)==PNG_TRANSFORMATION.EXPAND_tRNS)
						info_ptr_color_type=PNG_COLOR_TYPE.RGB_ALPHA;
					else info_ptr_color_type=PNG_COLOR_TYPE.RGB;
					info_ptr_bit_depth=8;
					info_ptr_num_trans=0;
				}
				else
				{
					if(num_trans!=0)
					{
						if((transformations&PNG_TRANSFORMATION.EXPAND_tRNS)==PNG_TRANSFORMATION.EXPAND_tRNS) info_ptr_color_type|=PNG_COLOR_TYPE.ALPHA_MASK;
					}
					if(info_ptr_bit_depth<8) info_ptr_bit_depth=8;
					info_ptr_num_trans=0;
				}
			}

			if((transformations&PNG_TRANSFORMATION.BACKGROUND)==PNG_TRANSFORMATION.BACKGROUND)
			{
				info_ptr_color_type&=~PNG_COLOR_TYPE.ALPHA_MASK;
				info_ptr_num_trans=0;
				info_ptr_background=background;
			}

			if ((transformations&PNG_TRANSFORMATION.GAMMA)==PNG_TRANSFORMATION.GAMMA)
			{
				info_ptr_gamma=gamma;
			}

			if((transformations&PNG_TRANSFORMATION._16_TO_8)==PNG_TRANSFORMATION._16_TO_8&&info_ptr_bit_depth==16) info_ptr_bit_depth=8;
			if((transformations&PNG_TRANSFORMATION.GRAY_TO_RGB)==PNG_TRANSFORMATION.GRAY_TO_RGB) info_ptr_color_type|=PNG_COLOR_TYPE.COLOR_MASK;
			if((transformations&PNG_TRANSFORMATION.RGB_TO_GRAY)!=PNG_TRANSFORMATION.None) info_ptr_color_type&=~PNG_COLOR_TYPE.COLOR_MASK;

			if((transformations&PNG_TRANSFORMATION.QUANTIZE)==PNG_TRANSFORMATION.QUANTIZE)
			{
				if((info_ptr_color_type==PNG_COLOR_TYPE.RGB||info_ptr_color_type==PNG_COLOR_TYPE.RGB_ALPHA)&&palette_lookup!=null&&info_ptr_bit_depth==8)
					info_ptr_color_type=PNG_COLOR_TYPE.PALETTE;
			}

			if((transformations&PNG_TRANSFORMATION.PACK)==PNG_TRANSFORMATION.PACK&&info_ptr_bit_depth<8) info_ptr_bit_depth=8;

			if(info_ptr_color_type==PNG_COLOR_TYPE.PALETTE) info_ptr_channels=1;
			else if((info_ptr_color_type&PNG_COLOR_TYPE.COLOR_MASK)==PNG_COLOR_TYPE.COLOR_MASK) info_ptr_channels=3;
			else info_ptr_channels=1;

			if((flags&PNG_FLAG.STRIP_ALPHA)==PNG_FLAG.STRIP_ALPHA) info_ptr_color_type&=~PNG_COLOR_TYPE.ALPHA_MASK;

			if((info_ptr_color_type&PNG_COLOR_TYPE.ALPHA_MASK)==PNG_COLOR_TYPE.ALPHA_MASK) info_ptr_channels++;

			// STRIP_ALPHA and FILLER allowed: MASK_ALPHA bit stripped above
			if((transformations&PNG_TRANSFORMATION.FILLER)==PNG_TRANSFORMATION.FILLER&&
				(info_ptr_color_type==PNG_COLOR_TYPE.RGB||info_ptr_color_type==PNG_COLOR_TYPE.GRAY))
			{
				info_ptr_channels++;
				// if adding a true alpha channel not just filler
				if((transformations&PNG_TRANSFORMATION.ADD_ALPHA)==PNG_TRANSFORMATION.ADD_ALPHA) info_ptr_color_type|=PNG_COLOR_TYPE.ALPHA_MASK;
			}

			if((transformations&PNG_TRANSFORMATION.USER_TRANSFORM)==PNG_TRANSFORMATION.USER_TRANSFORM)
			{
				if(info_ptr_bit_depth<user_transform_depth) info_ptr_bit_depth=user_transform_depth;
				if(info_ptr_channels<user_transform_channels) info_ptr_channels=user_transform_channels;
			}

			info_ptr_pixel_depth=(byte)(info_ptr_channels*info_ptr_bit_depth);
			info_ptr_rowbytes=PNG_ROWBYTES(info_ptr_pixel_depth, width);
		}
		#endregion

		#region png_do_read_transformations
		// Transform the row. The order of transformations is significant,
		// and is very touchy. If you add a transformation, take care to
		// decide how it fits in with the other transformations here.
		void png_do_read_transformations()
		{
			if(row_buf==null) throw new PNG_Exception(string.Format("NULL row buffer for row {0}, pass {1}", row_number, pass));

			if((flags&PNG_FLAG.ROW_INIT)!=PNG_FLAG.ROW_INIT)
			{
				// Application has failed to call either png_read_start_image()
				// or png_read_update_info() after setting transforms that expand
				// pixels. This check added to libpng-1.2.19
				throw new PNG_Exception("Uninitialized row");
			}

			if((transformations&PNG_TRANSFORMATION.EXPAND)==PNG_TRANSFORMATION.EXPAND)
			{
				if(row_info.color_type==PNG_COLOR_TYPE.PALETTE)
				{
					png_do_expand_palette(ref row_info, row_buf, palette, trans_alpha);
				}
				else
				{
					if(num_trans!=0&&(transformations&PNG_TRANSFORMATION.EXPAND_tRNS)==PNG_TRANSFORMATION.EXPAND_tRNS)
						png_do_expand_with_trans_values(ref row_info, row_buf, ref trans_color);
					else png_do_expand(ref row_info, row_buf);
				}
			}

			if((flags&PNG_FLAG.STRIP_ALPHA)==PNG_FLAG.STRIP_ALPHA) png_do_strip_filler(ref row_info, row_buf, PNG_FLAG.FILLER_AFTER|flags&PNG_FLAG.STRIP_ALPHA);

			if((transformations&PNG_TRANSFORMATION.RGB_TO_GRAY)!=PNG_TRANSFORMATION.None)
			{
				bool rgb_error=png_do_rgb_to_gray(ref row_info, row_buf);
				if(rgb_error)
				{
					rgb_to_gray_status=true;
					if((transformations&PNG_TRANSFORMATION.RGB_TO_GRAY)==PNG_TRANSFORMATION.RGB_TO_GRAY_WARN) Debug.WriteLine("png_do_rgb_to_gray found nongray pixel");
					if((transformations&PNG_TRANSFORMATION.RGB_TO_GRAY)==PNG_TRANSFORMATION.RGB_TO_GRAY_ERR) throw new PNG_Exception("png_do_rgb_to_gray found nongray pixel");
				}
			}

			//From Andreas Dilger e-mail to png-implement, 26 March 1998:
			//
			// In most cases, the "simple transparency" should be done prior to doing
			// gray-to-RGB, or you will have to test 3x as many bytes to check if a
			// pixel is transparent. You would also need to make sure that the
			// transparency information is upgraded to RGB.
			//
			// To summarize, the current flow is:
			//	- Gray + simple transparency -> compare 1 or 2 gray bytes and composite with background "in place" if transparent,
			//		convert to RGB if necessary
			//	- Gray + alpha -> composite with gray background and remove alpha bytes,
			//		convert to RGB if necessary
			//
			// To support RGB backgrounds for gray images we need:
			//	- Gray + simple transparency -> convert to RGB + simple transparency, compare 3 or 6 bytes and composite with background
			//		"in place" if transparent (3x compare/pixel compared to doing composite with gray bkgrnd)
			//	- Gray + alpha -> convert to RGB + alpha, composite with background and remove alpha bytes (3x float operations/pixel
			//		compared with composite on gray background)
			//
			// Greg's change will do this. The reason it wasn't done before is for
			// performance, as this increases the per-pixel operations. If we would check
			// in advance if the background was gray or RGB, and position the gray-to-RGB
			// transform appropriately, then it would save a lot of work/time.

			// if gray->RGB, do so now only if background is non-gray; else do later for performance reasons
			if((transformations&PNG_TRANSFORMATION.GRAY_TO_RGB)!=PNG_TRANSFORMATION.None&&(mode&PNG_MODE.BACKGROUND_IS_GRAY)!=PNG_MODE.BACKGROUND_IS_GRAY)
				png_do_gray_to_rgb(ref row_info, row_buf);

			if((transformations&PNG_TRANSFORMATION.BACKGROUND)==PNG_TRANSFORMATION.BACKGROUND&&
				((num_trans!=0)||(color_type&PNG_COLOR_TYPE.ALPHA_MASK)==PNG_COLOR_TYPE.ALPHA_MASK))
				png_do_background(ref row_info, row_buf, ref trans_color, ref background, ref background_1, gamma_table,
					gamma_from_1, gamma_to_1, gamma_16_table, gamma_16_from_1, gamma_16_to_1, gamma_shift);

			if((transformations&PNG_TRANSFORMATION.GAMMA)==PNG_TRANSFORMATION.GAMMA&&
				!((transformations&PNG_TRANSFORMATION.BACKGROUND)==PNG_TRANSFORMATION.BACKGROUND&&
				((num_trans!=0)||(color_type&PNG_COLOR_TYPE.ALPHA_MASK)==PNG_COLOR_TYPE.ALPHA_MASK))&&
				color_type!=PNG_COLOR_TYPE.PALETTE)
				png_do_gamma(ref row_info, row_buf, gamma_table, gamma_16_table, gamma_shift);

			if((transformations&PNG_TRANSFORMATION._16_TO_8)==PNG_TRANSFORMATION._16_TO_8) png_do_chop(ref row_info, row_buf);

			if((transformations&PNG_TRANSFORMATION.QUANTIZE)==PNG_TRANSFORMATION.QUANTIZE)
			{
				png_do_quantize(ref row_info, row_buf, palette_lookup, quantize_index);
				if(row_info.rowbytes==0) throw new PNG_Exception("png_do_quantize returned rowbytes=0");
			}

			if((transformations&PNG_TRANSFORMATION.INVERT_MONO)==PNG_TRANSFORMATION.INVERT_MONO) png_do_invert(ref row_info, row_buf);
			if((transformations&PNG_TRANSFORMATION.SHIFT)==PNG_TRANSFORMATION.SHIFT) png_do_unshift(ref row_info, row_buf, ref shift);
			if((transformations&PNG_TRANSFORMATION.PACK)==PNG_TRANSFORMATION.PACK) png_do_unpack(ref row_info, row_buf);
			if((transformations&PNG_TRANSFORMATION.BGR)==PNG_TRANSFORMATION.BGR) png_do_bgr(ref row_info, row_buf);
			if((transformations&PNG_TRANSFORMATION.PACKSWAP)==PNG_TRANSFORMATION.PACKSWAP) png_do_packswap(ref row_info, row_buf);

			// if gray -> RGB, do so now only if we did not do so above
			if((transformations&PNG_TRANSFORMATION.GRAY_TO_RGB)!=PNG_TRANSFORMATION.None&&
				(mode&PNG_MODE.BACKGROUND_IS_GRAY)==PNG_MODE.BACKGROUND_IS_GRAY) png_do_gray_to_rgb(ref row_info, row_buf);
			if((transformations&PNG_TRANSFORMATION.FILLER)==PNG_TRANSFORMATION.FILLER) png_do_read_filler(ref row_info, row_buf, filler, flags);
			if((transformations&PNG_TRANSFORMATION.INVERT_ALPHA)==PNG_TRANSFORMATION.INVERT_ALPHA) png_do_read_invert_alpha(ref row_info, row_buf);
			if((transformations&PNG_TRANSFORMATION.SWAP_ALPHA)==PNG_TRANSFORMATION.SWAP_ALPHA) png_do_read_swap_alpha(ref row_info, row_buf);
			if((transformations&PNG_TRANSFORMATION.SWAP_BYTES)==PNG_TRANSFORMATION.SWAP_BYTES) png_do_swap(ref row_info, row_buf);

			if((transformations&PNG_TRANSFORMATION.USER_TRANSFORM)==PNG_TRANSFORMATION.USER_TRANSFORM)
			{
				// user read transform function
				if(read_user_transform_fn!=null) read_user_transform_fn(this, ref row_info, row_buf);
				if(user_transform_depth!=0) row_info.bit_depth=user_transform_depth;
				if(user_transform_channels!=0) row_info.channels=user_transform_channels;
				row_info.pixel_depth=(byte)(row_info.bit_depth*row_info.channels);
				row_info.rowbytes=PNG_ROWBYTES(row_info.pixel_depth, row_info.width);
			}
		}
		#endregion

		#region more png_do methods
		// Unpack pixels of 1, 2, or 4 bits per pixel into 1 byte per pixel,
		// without changing the actual values. Thus, if you had a row with
		// a bit depth of 1, you would end up with bytes that only contained
		// the numbers 0 or 1. If you would rather they contain 0 and 255, use
		// png_do_shift() after this.
		static unsafe void png_do_unpack(ref png_row_info row_info, byte[] row)
		{
			if(row_info.bit_depth>=8) return;
			uint row_width=row_info.width;

			fixed(byte* row_=row)
			{
				byte* sp=row_+1; // skip filter value
				byte* dp=row_+1; // skip filter value
				dp+=row_width-1;

				switch(row_info.bit_depth)
				{
					case 1:
						{
							sp+=(row_width-1)>>3;
							int shift=7-(int)((row_width+7)&0x07);
							for(uint i=0; i<row_width; i++)
							{
								*dp=(byte)((*sp>>shift)&0x01);
								if(shift==7)
								{
									shift=0;
									sp--;
								}
								else shift++;

								dp--;
							}
						}
						break;
					case 2:
						{
							sp+=(row_width-1)>>2;
							int shift=(int)((3-((row_width+3)&0x03))<<1);
							for(uint i=0; i<row_width; i++)
							{
								*dp=(byte)((*sp>>shift)&0x03);
								if(shift==6)
								{
									shift=0;
									sp--;
								}
								else shift+=2;

								dp--;
							}
						}
						break;
					case 4:
						{
							sp+=(row_width-1)>>1;
							int shift=(int)((1-((row_width+1)&0x01))<<2);
							for(uint i=0; i<row_width; i++)
							{
								*dp=(byte)((*sp>>shift)&0x0f);
								if(shift==4)
								{
									shift=0;
									sp--;
								}
								else shift=4;

								dp--;
							}
						}
						break;
				}
			}
			row_info.bit_depth=8;
			row_info.pixel_depth=(byte)(8*row_info.channels);
			row_info.rowbytes=row_width*row_info.channels;
		}

		// Reverse the effects of png_do_shift. This routine merely shifts the
		// pixels back to their significant bits values. Thus, if you have
		// a row of bit depth 8, but only 5 are significant, this will shift
		// the values back to 0 through 31.
		static unsafe void png_do_unshift(ref png_row_info row_info, byte[] row, ref png_color_8 sig_bits)
		{
			if(row_info.color_type==PNG_COLOR_TYPE.PALETTE) return;

			int[] shift=new int[4];
			uint channels=0;
			ushort value=0;
			uint row_width=row_info.width;

			if((row_info.color_type&PNG_COLOR_TYPE.COLOR_MASK)==PNG_COLOR_TYPE.COLOR_MASK)
			{
				shift[channels++]=row_info.bit_depth-sig_bits.red;
				shift[channels++]=row_info.bit_depth-sig_bits.green;
				shift[channels++]=row_info.bit_depth-sig_bits.blue;
			}
			else shift[channels++]=row_info.bit_depth-sig_bits.gray;

			if((row_info.color_type&PNG_COLOR_TYPE.ALPHA_MASK)==PNG_COLOR_TYPE.ALPHA_MASK)
				shift[channels++]=row_info.bit_depth-sig_bits.alpha;

			for(uint c=0; c<channels; c++)
			{
				if(shift[c]<=0) shift[c]=0;
				else value=1;
			}

			if(value==0) return;

			fixed(byte* row_=row)
			{
				byte* bp=row_+1; // skip filter value

				switch(row_info.bit_depth)
				{
					case 2:
						{
							uint istop=row_info.rowbytes;
							for(uint i=0; i<istop; i++)
							{
								*bp>>=1;
								*bp++&=0x55;
							}
						}
						break;
					case 4:
						{
							uint istop=row_info.rowbytes;
							byte mask=(byte)((((int)0xf0>>shift[0])&(int)0xf0)|(byte)((int)0xf>>shift[0]));
							for(uint i=0; i<istop; i++)
							{
								*bp>>=shift[0];
								*bp++&=mask;
							}
						}
						break;
					case 8:
						{
							uint istop=row_width*channels;
							for(uint i=0; i<istop; i++) *bp++>>=shift[i%channels];
						}
						break;
					case 16:
						{
							uint istop=row_width*channels;
							for(uint i=0; i<istop; i++)
							{
								value=(ushort)((*bp<<8)+*(bp+1));
								value>>=shift[i%channels];
								*bp++=(byte)(value>>8);
								*bp++=(byte)(value&0xff);
							}
						}
						break;
				}
			}
		}

		// chop rows of bit depth 16 down to 8
		static unsafe void png_do_chop(ref png_row_info row_info, byte[] row)
		{
			if(row_info.bit_depth!=16) return;

			uint istop=row_info.width*row_info.channels;

			fixed(byte* row_=row)
			{
				byte* sp=row_+1; // skip filter value
				byte* dp=row_+1; // skip filter value

				for(uint i=0; i<istop; i++, sp+=2, dp++) *dp=*sp;
			}

			row_info.bit_depth=8;
			row_info.pixel_depth=(byte)(8*row_info.channels);
			row_info.rowbytes=row_info.width*row_info.channels;
		}

		static unsafe void png_do_read_swap_alpha(ref png_row_info row_info, byte[] row)
		{
			uint row_width=row_info.width;

			fixed(byte* row_=row)
			{
				byte* sp=row_+row_info.rowbytes+1; // skip filter value
				byte* dp=row_+row_info.rowbytes+1; // skip filter value

				if(row_info.color_type==PNG_COLOR_TYPE.RGB_ALPHA)
				{
					// This converts from RGBA to ARGB
					if(row_info.bit_depth==8)
					{
						byte save;
						for(uint i=0; i<row_width; i++)
						{
							save=*(--sp);
							*(--dp)=*(--sp);
							*(--dp)=*(--sp);
							*(--dp)=*(--sp);
							*(--dp)=save;
						}
					}
					else // This converts from RRGGBBAA to AARRGGBB
					{
						byte save0, save1;
						for(uint i=0; i<row_width; i++)
						{
							save0=*(--sp);
							save1=*(--sp);
							*(--dp)=*(--sp);
							*(--dp)=*(--sp);
							*(--dp)=*(--sp);
							*(--dp)=*(--sp);
							*(--dp)=*(--sp);
							*(--dp)=*(--sp);
							*(--dp)=save0;
							*(--dp)=save1;
						}
					}
				}
				else if(row_info.color_type==PNG_COLOR_TYPE.GRAY_ALPHA)
				{
					// This converts from GA to AG
					if(row_info.bit_depth==8)
					{
						byte save;
						for(uint i=0; i<row_width; i++)
						{
							save=*(--sp);
							*(--dp)=*(--sp);
							*(--dp)=save;
						}
					}
					else // This converts from GGAA to AAGG
					{
						byte save0, save1;
						for(uint i=0; i<row_width; i++)
						{
							save0=*(--sp);
							save1=*(--sp);
							*(--dp)=*(--sp);
							*(--dp)=*(--sp);
							*(--dp)=save0;
							*(--dp)=save1;
						}
					}
				}
			}
		}

		static unsafe void png_do_read_invert_alpha(ref png_row_info row_info, byte[] row)
		{
			uint row_width=row_info.width;

			fixed(byte* row_=row)
			{
				byte* sp=row_+row_info.rowbytes+1; // skip filter value
				byte* dp=row_+row_info.rowbytes+1; // skip filter value

				if(row_info.color_type==PNG_COLOR_TYPE.RGB_ALPHA)
				{
					// This inverts the alpha channel in RGBA
					if(row_info.bit_depth==8)
					{
						for(uint i=0; i<row_width; i++)
						{
							*(--dp)=(byte)(255-*(--sp));
							sp-=3;
							dp=sp;
						}
					}
					else // This inverts the alpha channel in RRGGBBAA
					{
						for(uint i=0; i<row_width; i++)
						{
							*(--dp)=(byte)(255-*(--sp));
							*(--dp)=(byte)(255-*(--sp));
							sp-=6;
							dp=sp;
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
							*(--dp)=(byte)(255-*(--sp));
							*(--dp)=*(--sp);
						}
					}
					else // This inverts the alpha channel in GGAA
					{
						for(uint i=0; i<row_width; i++)
						{
							*(--dp)=(byte)(255-*(--sp));
							*(--dp)=(byte)(255-*(--sp));
							sp-=2;
							dp=sp;
						}
					}
				}
			}
		}

		// Add filler channel if we have RGB color
		static unsafe void png_do_read_filler(ref png_row_info row_info, byte[] row, uint filler, PNG_FLAG flags)
		{
			uint i;
			uint row_width=row_info.width;

			byte hi_filler=(byte)((filler>>8)&0xff);
			byte lo_filler=(byte)(filler&0xff);

			fixed(byte* row_=row)
			{
				byte* sp=row_+1; // skip filter value

				if(row_info.color_type==PNG_COLOR_TYPE.GRAY)
				{
					if(row_info.bit_depth==8)
					{
						// This changes the data from G to GX
						if((flags&PNG_FLAG.FILLER_AFTER)==PNG_FLAG.FILLER_AFTER)
						{
							sp+=row_width;
							byte* dp=sp+row_width;
							for(i=1; i<row_width; i++)
							{
								*(--dp)=lo_filler;
								*(--dp)=*(--sp);
							}
							*(--dp)=lo_filler;
							row_info.channels=2;
							row_info.pixel_depth=16;
							row_info.rowbytes=row_width*2;
						}
						else // This changes the data from G to XG
						{
							sp+=row_width;
							byte* dp=sp+row_width;
							for(i=0; i<row_width; i++)
							{
								*(--dp)=*(--sp);
								*(--dp)=lo_filler;
							}
							row_info.channels=2;
							row_info.pixel_depth=16;
							row_info.rowbytes=row_width*2;
						}
					}
					else if(row_info.bit_depth==16)
					{
						// This changes the data from GG to GGXX
						if((flags&PNG_FLAG.FILLER_AFTER)==PNG_FLAG.FILLER_AFTER)
						{
							sp+=row_width*2;
							byte* dp=sp+row_width*2;
							for(i=1; i<row_width; i++)
							{
								*(--dp)=hi_filler;
								*(--dp)=lo_filler;
								*(--dp)=*(--sp);
								*(--dp)=*(--sp);
							}
							*(--dp)=hi_filler;
							*(--dp)=lo_filler;
							row_info.channels=2;
							row_info.pixel_depth=32;
							row_info.rowbytes=row_width*4;
						}
						else // This changes the data from GG to XXGG
						{
							sp+=row_width*2;
							byte* dp=sp+row_width*2;
							for(i=0; i<row_width; i++)
							{
								*(--dp)=*(--sp);
								*(--dp)=*(--sp);
								*(--dp)=hi_filler;
								*(--dp)=lo_filler;
							}
							row_info.channels=2;
							row_info.pixel_depth=32;
							row_info.rowbytes=row_width*4;
						}
					}
				} // COLOR_TYPE==GRAY
				else if(row_info.color_type==PNG_COLOR_TYPE.RGB)
				{
					if(row_info.bit_depth==8)
					{
						// This changes the data from RGB to RGBX
						if((flags&PNG_FLAG.FILLER_AFTER)==PNG_FLAG.FILLER_AFTER)
						{
							sp+=row_width*3;
							byte* dp=sp+row_width;
							for(i=1; i<row_width; i++)
							{
								*(--dp)=lo_filler;
								*(--dp)=*(--sp);
								*(--dp)=*(--sp);
								*(--dp)=*(--sp);
							}
							*(--dp)=lo_filler;
							row_info.channels=4;
							row_info.pixel_depth=32;
							row_info.rowbytes=row_width*4;
						}
						else // This changes the data from RGB to XRGB
						{
							sp+=row_width*3;
							byte* dp=sp+row_width;
							for(i=0; i<row_width; i++)
							{
								*(--dp)=*(--sp);
								*(--dp)=*(--sp);
								*(--dp)=*(--sp);
								*(--dp)=lo_filler;
							}
							row_info.channels=4;
							row_info.pixel_depth=32;
							row_info.rowbytes=row_width*4;
						}
					}
					else if(row_info.bit_depth==16)
					{
						// This changes the data from RRGGBB to RRGGBBXX
						if((flags&PNG_FLAG.FILLER_AFTER)==PNG_FLAG.FILLER_AFTER)
						{
							sp+=row_width*6;
							byte* dp=sp+row_width*2;
							for(i=1; i<row_width; i++)
							{
								*(--dp)=hi_filler;
								*(--dp)=lo_filler;
								*(--dp)=*(--sp);
								*(--dp)=*(--sp);
								*(--dp)=*(--sp);
								*(--dp)=*(--sp);
								*(--dp)=*(--sp);
								*(--dp)=*(--sp);
							}
							*(--dp)=hi_filler;
							*(--dp)=lo_filler;
							row_info.channels=4;
							row_info.pixel_depth=64;
							row_info.rowbytes=row_width*8;
						}
						else // This changes the data from RRGGBB to XXRRGGBB
						{
							sp+=row_width*6;
							byte* dp=sp+row_width*2;
							for(i=0; i<row_width; i++)
							{
								*(--dp)=*(--sp);
								*(--dp)=*(--sp);
								*(--dp)=*(--sp);
								*(--dp)=*(--sp);
								*(--dp)=*(--sp);
								*(--dp)=*(--sp);
								*(--dp)=hi_filler;
								*(--dp)=lo_filler;
							}
							row_info.channels=4;
							row_info.pixel_depth=64;
							row_info.rowbytes=row_width*8;
						}
					}
				} // COLOR_TYPE==RGB
			}
		}

		// expand grayscale files to RGB, with or without alpha
		static unsafe void png_do_gray_to_rgb(ref png_row_info row_info, byte[] row)
		{
			uint i;
			uint row_width=row_info.width;

			if(!(row_info.bit_depth>=8&&(row_info.color_type&PNG_COLOR_TYPE.COLOR_MASK)!=PNG_COLOR_TYPE.COLOR_MASK)) return;

			fixed(byte* row_=row)
			{
				byte* sp=row_+1; // skip filter value

				if(row_info.color_type==PNG_COLOR_TYPE.GRAY)
				{
					if(row_info.bit_depth==8)
					{
						sp+=row_width-1;
						byte* dp=sp+row_width*2;
						for(i=0; i<row_width; i++)
						{
							*(dp--)=*sp;
							*(dp--)=*sp;
							*(dp--)=*(sp--);
						}
					}
					else
					{
						sp+=row_width*2-1;
						byte* dp=sp+row_width*4;
						for(i=0; i<row_width; i++)
						{
							*(dp--)=*sp;
							*(dp--)=*(sp-1);
							*(dp--)=*sp;
							*(dp--)=*(sp-1);
							*(dp--)=*(sp--);
							*(dp--)=*(sp--);
						}
					}
				}
				else if(row_info.color_type==PNG_COLOR_TYPE.GRAY_ALPHA)
				{
					if(row_info.bit_depth==8)
					{
						sp+=row_width*2-1;
						byte* dp=sp+row_width*2;
						for(i=0; i<row_width; i++)
						{
							*(dp--)=*(sp--);
							*(dp--)=*sp;
							*(dp--)=*sp;
							*(dp--)=*(sp--);
						}
					}
					else
					{
						sp+=row_width*4-1;
						byte* dp=sp+row_width*4;
						for(i=0; i<row_width; i++)
						{
							*(dp--)=*(sp--);
							*(dp--)=*(sp--);
							*(dp--)=*sp;
							*(dp--)=*(sp-1);
							*(dp--)=*sp;
							*(dp--)=*(sp-1);
							*(dp--)=*(sp--);
							*(dp--)=*(sp--);
						}
					}
				}
			}
			row_info.channels+=(byte)2;
			row_info.color_type|=PNG_COLOR_TYPE.COLOR_MASK;
			row_info.pixel_depth=(byte)(row_info.channels*row_info.bit_depth);
			row_info.rowbytes=PNG_ROWBYTES(row_info.pixel_depth, row_width);
		}

		// reduce RGB files to grayscale, with or without alpha
		// using the equation given in Poynton's ColorFAQ at
		// <http://www.inforamp.net/~poynton/>  (THIS LINK IS DEAD June 2008)
		// New link:
		// <http://www.poynton.com/notes/colour_and_gamma/>
		// Charles Poynton poynton at poynton.com
		//
		//		Y=0.212671*R+0.715160*G+0.072169*B
		//
		//	We approximate this with
		//
		//		Y=0.21268*R+0.7151*G+0.07217*B
		//
		//	which can be expressed with integers as
		//
		//		Y=(6969*R+23434*G+2365*B)/32768
		//
		//	The calculation is to be done in a linear colorspace.
		//
		//	Other integer coefficents can be used via png_set_rgb_to_gray().
		unsafe bool png_do_rgb_to_gray(ref png_row_info row_info, byte[] row)
		{
			uint row_width=row_info.width;
			bool rgb_error=false;

			if((row_info.color_type&PNG_COLOR_TYPE.COLOR_MASK)!=PNG_COLOR_TYPE.COLOR_MASK) return rgb_error;

			uint rc=rgb_to_gray_red_coeff;
			uint gc=rgb_to_gray_green_coeff;
			uint bc=rgb_to_gray_blue_coeff;

			fixed(byte* row_=row)
			{
				byte* sp=row_+1; // skip filter value
				byte* dp=row_+1; // skip filter value

				if(row_info.color_type==PNG_COLOR_TYPE.RGB)
				{
					if(row_info.bit_depth==8)
					{
						if(gamma_from_1!=null&&gamma_to_1!=null)
						{
							for(uint i=0; i<row_width; i++)
							{
								byte red=gamma_to_1[*(sp++)];
								byte green=gamma_to_1[*(sp++)];
								byte blue=gamma_to_1[*(sp++)];
								if(red!=green||red!=blue)
								{
									rgb_error=true;
									*(dp++)=gamma_from_1[(rc*red+gc*green+bc*blue)>>15];
								}
								else *(dp++)=*(sp-1);
							}
						}
						else
						{
							for(uint i=0; i<row_width; i++)
							{
								byte red=*(sp++);
								byte green=*(sp++);
								byte blue=*(sp++);
								if(red!=green||red!=blue)
								{
									rgb_error=true;
									*(dp++)=(byte)((rc*red+gc*green+bc*blue)>>15);
								}
								else *(dp++)=*(sp-1);
							}
						}
					}
					else // RGB bit_depth==16
					{
						if(gamma_16_to_1!=null&&gamma_16_from_1!=null)
						{
							for(uint i=0; i<row_width; i++)
							{
								ushort red, green, blue, w;
								red=(ushort)(((*(sp))<<8)|*(sp+1)); sp+=2;
								green=(ushort)(((*(sp))<<8)|*(sp+1)); sp+=2;
								blue=(ushort)(((*(sp))<<8)|*(sp+1)); sp+=2;

								if(red==green&&red==blue) w=red;
								else
								{
									ushort red_1=gamma_16_to_1[(red&0xff)>>gamma_shift][red>>8];
									ushort green_1=gamma_16_to_1[(green&0xff)>>gamma_shift][green>>8];
									ushort blue_1=gamma_16_to_1[(blue&0xff)>>gamma_shift][blue>>8];
									ushort gray16=(ushort)((rc*red_1+gc*green_1+bc*blue_1)>>15);
									w=gamma_16_from_1[(gray16&0xff)>>gamma_shift][gray16>>8];
									rgb_error=true;
								}

								*(dp++)=(byte)((w>>8)&0xff);
								*(dp++)=(byte)(w&0xff);
							}
						}
						else
						{
							for(uint i=0; i<row_width; i++)
							{
								ushort red, green, blue, gray16;
								red=(ushort)(((*(sp))<<8)|*(sp+1)); sp+=2;
								green=(ushort)(((*(sp))<<8)|*(sp+1)); sp+=2;
								blue=(ushort)(((*(sp))<<8)|*(sp+1)); sp+=2;

								if(red!=green||red!=blue) rgb_error=true;
								gray16=(ushort)((rc*red+gc*green+bc*blue)>>15);
								*(dp++)=(byte)((gray16>>8)&0xff);
								*(dp++)=(byte)(gray16&0xff);
							}
						}
					}
				}
				else if(row_info.color_type==PNG_COLOR_TYPE.RGB_ALPHA)
				{
					if(row_info.bit_depth==8)
					{
						if(gamma_from_1!=null&&gamma_to_1!=null)
						{
							for(uint i=0; i<row_width; i++)
							{
								byte red=gamma_to_1[*(sp++)];
								byte green=gamma_to_1[*(sp++)];
								byte blue=gamma_to_1[*(sp++)];
								if(red!=green||red!=blue) rgb_error=true;
								*(dp++)=gamma_from_1[(rc*red+gc*green+bc*blue)>>15];
								*(dp++)=*(sp++); // alpha
							}
						}
						else
						{
							for(uint i=0; i<row_width; i++)
							{
								byte red=*(sp++);
								byte green=*(sp++);
								byte blue=*(sp++);
								if(red!=green||red!=blue) rgb_error=true;
								*(dp++)=(byte)((rc*red+gc*green+bc*blue)>>15);
								*(dp++)=*(sp++); // alpha
							}
						}
					}
					else // RGBA bit_depth==16
					{
						if(gamma_16_to_1!=null&&gamma_16_from_1!=null)
						{
							for(uint i=0; i<row_width; i++)
							{
								ushort red, green, blue, w;
								red=(ushort)(((*(sp))<<8)|*(sp+1)); sp+=2;
								green=(ushort)(((*(sp))<<8)|*(sp+1)); sp+=2;
								blue=(ushort)(((*(sp))<<8)|*(sp+1)); sp+=2;

								if(red==green&&red==blue) w=red;
								else
								{
									ushort red_1=gamma_16_to_1[(red&0xff)>>gamma_shift][red>>8];
									ushort green_1=gamma_16_to_1[(green&0xff)>>gamma_shift][green>>8];
									ushort blue_1=gamma_16_to_1[(blue&0xff)>>gamma_shift][blue>>8];
									ushort gray16=(ushort)((rc*red_1+gc*green_1+bc*blue_1)>>15);
									w=gamma_16_from_1[(gray16&0xff)>>gamma_shift][gray16>>8];
									rgb_error=true;
								}

								*(dp++)=(byte)((w>>8)&0xff);
								*(dp++)=(byte)(w&0xff);
								*(dp++)=*(sp++); // alpha
								*(dp++)=*(sp++);
							}
						}
						else
						{
							for(uint i=0; i<row_width; i++)
							{
								ushort red, green, blue, gray16;
								red=(ushort)((*(sp)<<8)|*(sp+1)); sp+=2;
								green=(ushort)((*(sp)<<8)|*(sp+1)); sp+=2;
								blue=(ushort)((*(sp)<<8)|*(sp+1)); sp+=2;
								if(red!=green||red!=blue) rgb_error=true;
								gray16=(ushort)((rc*red+gc*green+bc*blue)>>15);
								*(dp++)=(byte)((gray16>>8)&0xff);
								*(dp++)=(byte)(gray16&0xff);
								*(dp++)=*(sp++); // alpha
								*(dp++)=*(sp++);
							}
						}
					}
				}
			}

			row_info.channels-=(byte)2;
			row_info.color_type&=~PNG_COLOR_TYPE.COLOR_MASK;
			row_info.pixel_depth=(byte)(row_info.channels*row_info.bit_depth);
			row_info.rowbytes=PNG_ROWBYTES(row_info.pixel_depth, row_width);

			return rgb_error;
		}

		// Build a grayscale palette. Palette is assumed to be 1 << bit_depth
		// large of png_color. This lets grayscale images be treated as
		// paletted. Most useful for gamma correction and simplification
		// of code.
		public static void png_build_grayscale_palette(int bit_depth, png_color[] palette)
		{
			if(palette==null||palette.Length==0) return;

			int num_palette;
			int color_inc;

			switch(bit_depth)
			{
				case 1:
					num_palette=2;
					color_inc=0xff;
					break;
				case 2:
					num_palette=4;
					color_inc=0x55;
					break;
				case 4:
					num_palette=16;
					color_inc=0x11;
					break;
				case 8:
					num_palette=256;
					color_inc=1;
					break;
				default:
					num_palette=0;
					color_inc=0;
					break;
			}

			for(int i=0, v=0; i<num_palette; i++, v+=color_inc)
			{
				palette[i].red=(byte)v;
				palette[i].green=(byte)v;
				palette[i].blue=(byte)v;
			}
		}

		// Replace any alpha or transparency with the supplied background color.
		// "background" is already in the screen gamma, while "background_1" is
		// at a gamma of 1.0. Paletted files have already been taken care of.
		static unsafe void png_do_background(ref png_row_info row_info, byte[] row, ref png_color_16 trans_color, ref png_color_16 background,
			ref png_color_16 background_1, byte[] gamma_table, byte[] gamma_from_1, byte[] gamma_to_1, ushort[][] gamma_16,
			ushort[][] gamma_16_from_1, ushort[][] gamma_16_to_1, int gamma_shift)
		{
			uint i;
			uint row_width=row_info.width;
			int shift;

			if(!((row_info.color_type&PNG_COLOR_TYPE.ALPHA_MASK)!=PNG_COLOR_TYPE.ALPHA_MASK||
				row_info.color_type!=PNG_COLOR_TYPE.PALETTE)) return;

			fixed(byte* row_=row)
			{
				byte* sp=row_+1; // skip filter value
				byte* dp=row_+1; // skip filter value

				switch(row_info.color_type)
				{
					case PNG_COLOR_TYPE.GRAY:
						{
							switch(row_info.bit_depth)
							{
								case 1:
									{
										shift=7;
										for(i=0; i<row_width; i++)
										{
											if((ushort)((*sp>>shift)&0x01)==trans_color.gray)
											{
												*sp&=(byte)((0x7f7f>>(7-shift))&0xff);
												*sp|=(byte)(background.gray<<shift);
											}
											if(shift==0)
											{
												shift=7;
												sp++;
											}
											else shift--;
										}
									}
									break;
								case 2:
									{
										shift=6;
										if(gamma_table!=null)
										{
											for(i=0; i<row_width; i++)
											{
												if((ushort)((*sp>>shift)&0x03)==trans_color.gray)
												{
													*sp&=(byte)((0x3f3f>>(6-shift))&0xff);
													*sp|=(byte)(background.gray<<shift);
												}
												else
												{
													byte p=(byte)((*sp>>shift)&0x03);
													byte g=(byte)((gamma_table[p|(p<<2)|(p<<4)|(p<<6)]>>6)&0x03);
													*sp&=(byte)((0x3f3f>>(6-shift))&0xff);
													*sp|=(byte)(g<<shift);
												}
												if(shift==0)
												{
													shift=6;
													sp++;
												}
												else shift-=2;
											}
										}
										else
										{
											for(i=0; i<row_width; i++)
											{
												if((ushort)((*sp>>shift)&0x03)==trans_color.gray)
												{
													*sp&=(byte)((0x3f3f>>(6-shift))&0xff);
													*sp|=(byte)(background.gray<<shift);
												}
												if(shift==0)
												{
													shift=6;
													sp++;
												}
												else shift-=2;
											}
										}
									}
									break;
								case 4:
									{
										shift=4;
										if(gamma_table!=null)
										{
											for(i=0; i<row_width; i++)
											{
												if((ushort)((*sp>>shift)&0x0f)==trans_color.gray)
												{
													*sp&=(byte)((0xf0f>>(4-shift))&0xff);
													*sp|=(byte)(background.gray<<shift);
												}
												else
												{
													byte p=(byte)((*sp>>shift)&0x0f);
													byte g=(byte)((gamma_table[p|(p<<4)]>>4)&0x0f);
													*sp&=(byte)((0xf0f>>(4-shift))&0xff);
													*sp|=(byte)(g<<shift);
												}
												if(shift==0)
												{
													shift=4;
													sp++;
												}
												else shift-=4;
											}
										}
										else
										{
											for(i=0; i<row_width; i++)
											{
												if((ushort)((*sp>>shift)&0x0f)==trans_color.gray)
												{
													*sp&=(byte)((0xf0f>>(4-shift))&0xff);
													*sp|=(byte)(background.gray<<shift);
												}
												if(shift==0)
												{
													shift=4;
													sp++;
												}
												else shift-=4;
											}
										}
									}
									break;
								case 8:
									{
										if(gamma_table!=null)
										{
											for(i=0; i<row_width; i++, sp++)
											{
												if(*sp==trans_color.gray) *sp=(byte)background.gray;
												else *sp=gamma_table[*sp];
											}
										}
										else
										{
											for(i=0; i<row_width; i++, sp++)
											{
												if(*sp==trans_color.gray) *sp=(byte)background.gray;
											}
										}
										break;
									}
								case 16:
									{
										if(gamma_16!=null)
										{
											for(i=0; i<row_width; i++, sp+=2)
											{
												ushort v=(ushort)(((*sp)<<8)+*(sp+1));
												if(v==trans_color.gray)
												{ // background is already in screen gamma
													*sp=(byte)((background.gray>>8)&0xff);
													*(sp+1)=(byte)(background.gray&0xff);
												}
												else
												{
													v=gamma_16[*(sp+1)>>gamma_shift][*sp];
													*sp=(byte)((v>>8)&0xff);
													*(sp+1)=(byte)(v&0xff);
												}
											}
										}
										else
										{
											for(i=0; i<row_width; i++, sp+=2)
											{
												ushort v=(ushort)(((*sp)<<8)+*(sp+1));
												if(v==trans_color.gray)
												{
													*sp=(byte)((background.gray>>8)&0xff);
													*(sp+1)=(byte)(background.gray&0xff);
												}
											}
										}
									}
									break;
							} // switch(row_info->bit_depth)
						}
						break;
					case PNG_COLOR_TYPE.RGB:
						{
							if(row_info.bit_depth==8)
							{
								if(gamma_table!=null)
								{
									for(i=0; i<row_width; i++, sp+=3)
									{
										if(*sp==trans_color.red&&*(sp+1)==trans_color.green&&*(sp+2)==trans_color.blue)
										{
											*sp=(byte)background.red;
											*(sp+1)=(byte)background.green;
											*(sp+2)=(byte)background.blue;
										}
										else
										{
											*sp=gamma_table[*sp];
											*(sp+1)=gamma_table[*(sp+1)];
											*(sp+2)=gamma_table[*(sp+2)];
										}
									}
								}
								else
								{
									for(i=0; i<row_width; i++, sp+=3)
									{
										if(*sp==trans_color.red&&*(sp+1)==trans_color.green&&*(sp+2)==trans_color.blue)
										{
											*sp=(byte)background.red;
											*(sp+1)=(byte)background.green;
											*(sp+2)=(byte)background.blue;
										}
									}
								}
							}
							else // if(row_info->bit_depth==16)
							{
								if(gamma_16!=null)
								{
									for(i=0; i<row_width; i++, sp+=6)
									{
										ushort r=(ushort)(((*sp)<<8)+*(sp+1));
										ushort g=(ushort)(((*(sp+2))<<8)+*(sp+3));
										ushort b=(ushort)(((*(sp+4))<<8)+*(sp+5));
										if(r==trans_color.red&&g==trans_color.green&&b==trans_color.blue)
										{ // background is already in screen gamma
											*sp=(byte)((background.red>>8)&0xff);
											*(sp+1)=(byte)(background.red&0xff);
											*(sp+2)=(byte)((background.green>>8)&0xff);
											*(sp+3)=(byte)(background.green&0xff);
											*(sp+4)=(byte)((background.blue>>8)&0xff);
											*(sp+5)=(byte)(background.blue&0xff);
										}
										else
										{
											ushort v=gamma_16[*(sp+1)>>gamma_shift][*sp];
											*sp=(byte)((v>>8)&0xff);
											*(sp+1)=(byte)(v&0xff);
											v=gamma_16[*(sp+3)>>gamma_shift][*(sp+2)];
											*(sp+2)=(byte)((v>>8)&0xff);
											*(sp+3)=(byte)(v&0xff);
											v=gamma_16[*(sp+5)>>gamma_shift][*(sp+4)];
											*(sp+4)=(byte)((v>>8)&0xff);
											*(sp+5)=(byte)(v&0xff);
										}
									}
								}
								else
								{
									for(i=0; i<row_width; i++, sp+=6)
									{
										ushort r=(ushort)(((*sp)<<8)+*(sp+1));
										ushort g=(ushort)(((*(sp+2))<<8)+*(sp+3));
										ushort b=(ushort)(((*(sp+4))<<8)+*(sp+5));

										if(r==trans_color.red&&g==trans_color.green&&b==trans_color.blue)
										{
											*sp=(byte)((background.red>>8)&0xff);
											*(sp+1)=(byte)(background.red&0xff);
											*(sp+2)=(byte)((background.green>>8)&0xff);
											*(sp+3)=(byte)(background.green&0xff);
											*(sp+4)=(byte)((background.blue>>8)&0xff);
											*(sp+5)=(byte)(background.blue&0xff);
										}
									}
								}
							}
						}
						break;
					case PNG_COLOR_TYPE.GRAY_ALPHA:
						{
							if(row_info.bit_depth==8)
							{
								if(gamma_to_1!=null&&gamma_from_1!=null&&gamma_table!=null)
								{
									for(i=0; i<row_width; i++, sp+=2, dp++)
									{
										ushort a=*(sp+1);
										if(a==0xff) *dp=gamma_table[*sp];
										else if(a==0) *dp=(byte)background.gray; // background is already in screen gamma
										else
										{
											byte w, v=gamma_to_1[*sp];
											//png_composite(w, v, a, background_1->gray);
											w=(byte)(((ushort)v*a+background_1.gray*(ushort)(255-a)+(ushort)127)/255);
											*dp=gamma_from_1[w];
										}
									}
								}
								else
								{
									for(i=0; i<row_width; i++, sp+=2, dp++)
									{
										byte a=*(sp+1);
										if(a==0xff) *dp=*sp;
										else if(a==0) *dp=(byte)background.gray;
										else //png_composite(*dp, *sp, a, background_1->gray);
											*dp=(byte)(((ushort)(*sp)*a+background_1.gray*(ushort)(255-a)+(ushort)127)/255);
									}
								}
							}
							else // if(png_ptr->bit_depth==16)
							{
								if(gamma_16!=null&&gamma_16_from_1!=null&&gamma_16_to_1!=null)
								{
									for(i=0; i<row_width; i++, sp+=4, dp+=2)
									{
										ushort a=(ushort)(((*(sp+2))<<8)+*(sp+3));
										if(a==(ushort)0xffff)
										{
											ushort v=gamma_16[*(sp+1)>>gamma_shift][*sp];
											*dp=(byte)((v>>8)&0xff);
											*(dp+1)=(byte)(v&0xff);
										}
										else if(a==0)
										{ // background is already in screen gamma
											*dp=(byte)((background.gray>>8)&0xff);
											*(dp+1)=(byte)(background.gray&0xff);
										}
										else
										{
											ushort v, w, g=gamma_16_to_1[*(sp+1)>>gamma_shift][*sp];
											//png_composite_16(v, g, a, background_1->gray);
											v=(ushort)(((uint)g*(uint)a+(uint)background_1.gray*(uint)(65535-(uint)a)+(uint)32767)/(uint)65535);
											w=gamma_16_from_1[(v&0xff)>>gamma_shift][v>>8];
											*dp=(byte)((w>>8)&0xff);
											*(dp+1)=(byte)(w&0xff);
										}
									}
								}
								else
								{
									for(i=0; i<row_width; i++, sp+=4, dp+=2)
									{
										ushort a=(ushort)(((*(sp+2))<<8)+*(sp+3));
										if(a==(ushort)0xffff)
										{
											//memcpy(dp, sp, 2);
											*dp=*sp;
											*(dp+1)=*(sp+1);
										}
										else if(a==0)
										{
											*dp=(byte)((background.gray>>8)&0xff);
											*(dp+1)=(byte)(background.gray&0xff);
										}
										else
										{
											ushort v, g=(ushort)(((*sp)<<8)+*(sp+1));
											//png_composite_16(v, g, a, background_1->gray);
											v=(ushort)(((uint)g*(uint)a+(uint)(background_1.gray)*(uint)(65535-(uint)a)+(uint)32767)/(uint)65535);
											*dp=(byte)((v>>8)&0xff);
											*(dp+1)=(byte)(v&0xff);
										}
									}
								}
							}
						}
						break;
					case PNG_COLOR_TYPE.RGB_ALPHA:
						{
							if(row_info.bit_depth==8)
							{
								if(gamma_to_1!=null&&gamma_from_1!=null&&gamma_table!=null)
								{
									for(i=0; i<row_width; i++, sp+=4, dp+=3)
									{
										byte a=*(sp+3);
										if(a==0xff)
										{
											*dp=gamma_table[*sp];
											*(dp+1)=gamma_table[*(sp+1)];
											*(dp+2)=gamma_table[*(sp+2)];
										}
										else if(a==0)
										{ // background is already in screen gamma
											*dp=(byte)background.red;
											*(dp+1)=(byte)background.green;
											*(dp+2)=(byte)background.blue;
										}
										else
										{
											byte w, v=gamma_to_1[*sp];
											// png_composite(w, v, a, background_1->red);
											w=(byte)(((ushort)v*(ushort)a+background_1.red*(ushort)(255-(ushort)a)+(ushort)127)/255);
											*dp=gamma_from_1[w];
											v=gamma_to_1[*(sp+1)];
											//png_composite(w, v, a, background_1->green);
											w=(byte)(((ushort)v*(ushort)a+background_1.green*(ushort)(255-(ushort)a)+(ushort)127)/255);
											*(dp+1)=gamma_from_1[w];
											v=gamma_to_1[*(sp+2)];
											//png_composite(w, v, a, background_1->blue);
											w=(byte)(((ushort)v*(ushort)a+background_1.blue*(ushort)(255-(ushort)a)+(ushort)127)/255);
											*(dp+2)=gamma_from_1[w];
										}
									}
								}
								else
								{
									for(i=0; i<row_width; i++, sp+=4, dp+=3)
									{
										byte a=*(sp+3);
										if(a==0xff)
										{
											*dp=*sp;
											*(dp+1)=*(sp+1);
											*(dp+2)=*(sp+2);
										}
										else if(a==0)
										{
											*dp=(byte)background.red;
											*(dp+1)=(byte)background.green;
											*(dp+2)=(byte)background.blue;
										}
										else
										{
											//png_composite(*dp, *sp, a, background->red);
											//png_composite(*(dp+1), *(sp+1), a, background->green);
											//png_composite(*(dp+2), *(sp+2), a, background->blue);
											*dp=(byte)(((ushort)(*sp)*(ushort)a+background.red*(ushort)(255-(ushort)a)+(ushort)127)/255);
											*(dp+1)=(byte)(((ushort)(*(sp+1))*(ushort)a+background.green*(ushort)(255-(ushort)a)+(ushort)127)/255);
											*(dp+2)=(byte)(((ushort)(*(sp+2))*(ushort)a+background.blue*(ushort)(255-(ushort)a)+(ushort)127)/255);
										}
									}
								}
							}
							else //if (row_info->bit_depth==16)
							{
								if(gamma_16!=null&&gamma_16_from_1!=null&&gamma_16_to_1!=null)
								{
									for(i=0; i<row_width; i++, sp+=8, dp+=6)
									{
										ushort a=(ushort)(((ushort)(*(sp+6))<<8)+(ushort)(*(sp+7)));
										if(a==(ushort)0xffff)
										{
											ushort v=gamma_16[*(sp+1)>>gamma_shift][*sp];
											*dp=(byte)((v>>8)&0xff);
											*(dp+1)=(byte)(v&0xff);
											v=gamma_16[*(sp+3)>>gamma_shift][*(sp+2)];
											*(dp+2)=(byte)((v>>8)&0xff);
											*(dp+3)=(byte)(v&0xff);
											v=gamma_16[*(sp+5)>>gamma_shift][*(sp+4)];
											*(dp+4)=(byte)((v>>8)&0xff);
											*(dp+5)=(byte)(v&0xff);
										}
										else if(a==0)
										{ // background is already in screen gamma
											*dp=(byte)((background.red>>8)&0xff);
											*(dp+1)=(byte)(background.red&0xff);
											*(dp+2)=(byte)((background.green>>8)&0xff);
											*(dp+3)=(byte)(background.green&0xff);
											*(dp+4)=(byte)((background.blue>>8)&0xff);
											*(dp+5)=(byte)(background.blue&0xff);
										}
										else
										{
											ushort w, x, v=gamma_16_to_1[*(sp+1)>>gamma_shift][*sp];

											//png_composite_16(w, v, a, background_1->red);
											w=(ushort)(((uint)v*(uint)a+(uint)background_1.red*(uint)(65535-(uint)a)+(uint)32767)/(uint)65535);

											x=gamma_16_from_1[((w&0xff)>>gamma_shift)][w>>8];
											*dp=(byte)((x>>8)&0xff);
											*(dp+1)=(byte)(x&0xff);
											v=gamma_16_to_1[*(sp+3)>>gamma_shift][*(sp+2)];

											//png_composite_16(w, v, a, background_1->green);
											w=(ushort)(((uint)v*(uint)a+(uint)background_1.green*(uint)(65535-(uint)a)+(uint)32767)/(uint)65535);

											x=gamma_16_from_1[((w&0xff)>>gamma_shift)][w>>8];
											*(dp+2)=(byte)((x>>8)&0xff);
											*(dp+3)=(byte)(x&0xff);
											v=gamma_16_to_1[*(sp+5)>>gamma_shift][*(sp+4)];

											//png_composite_16(w, v, a, background_1->blue);
											w=(ushort)(((uint)v*(uint)a+(uint)background_1.blue*(uint)(65535-(uint)a)+(uint)32767)/(uint)65535);

											x=gamma_16_from_1[(w&0xff)>>gamma_shift][w>>8];
											*(dp+4)=(byte)((x>>8)&0xff);
											*(dp+5)=(byte)(x&0xff);
										}
									}
								}
								else
								{
									for(i=0; i<row_width; i++, sp+=8, dp+=6)
									{
										ushort a=(ushort)(((ushort)(*(sp+6))<<8)+(ushort)(*(sp+7)));
										if(a==(ushort)0xffff)
										{
											//memcpy(dp, sp, 6);
											*dp=*sp;
											*(dp+1)=*(sp+1);
											*(dp+2)=*(sp+2);
											*(dp+3)=*(sp+3);
											*(dp+4)=*(sp+4);
											*(dp+5)=*(sp+5);
										}
										else if(a==0)
										{
											*dp=(byte)((background.red>>8)&0xff);
											*(dp+1)=(byte)(background.red&0xff);
											*(dp+2)=(byte)((background.green>>8)&0xff);
											*(dp+3)=(byte)(background.green&0xff);
											*(dp+4)=(byte)((background.blue>>8)&0xff);
											*(dp+5)=(byte)(background.blue&0xff);
										}
										else
										{
											ushort v;
											ushort r=(ushort)(((*sp)<<8)+*(sp+1));
											ushort g=(ushort)(((*(sp+2))<<8)+*(sp+3));
											ushort b=(ushort)(((*(sp+4))<<8)+*(sp+5));

											//png_composite_16(v, r, a, background->red);
											v=(ushort)(((uint)r*(uint)a+(uint)background.red*(uint)(65535-(uint)a)+(uint)32767)/(uint)65535);

											*dp=(byte)((v>>8)&0xff);
											*(dp+1)=(byte)(v&0xff);
											//png_composite_16(v, g, a, background->green);
											v=(ushort)(((uint)g*(uint)a+(uint)background.green*(uint)(65535-(uint)a)+(uint)32767)/(uint)65535);

											*(dp+2)=(byte)((v>>8)&0xff);
											*(dp+3)=(byte)(v&0xff);
											//png_composite_16(v, b, a, background->blue);
											v=(ushort)(((uint)b*(uint)a+(uint)background.blue*(uint)(65535-(uint)a)+(uint)32767)/(uint)65535);

											*(dp+4)=(byte)((v>>8)&0xff);
											*(dp+5)=(byte)(v&0xff);
										}
									}
								}
							}
						}
						break;
				} // switch(row_info->color_type)
			} // fixed

			if((row_info.color_type&PNG_COLOR_TYPE.ALPHA_MASK)==PNG_COLOR_TYPE.ALPHA_MASK)
			{
				row_info.color_type&=~PNG_COLOR_TYPE.ALPHA_MASK;
				row_info.channels--;
				row_info.pixel_depth=(byte)(row_info.channels*row_info.bit_depth);
				row_info.rowbytes=PNG_ROWBYTES(row_info.pixel_depth, row_width);
			}
		}

		// Gamma correct the image, avoiding the alpha channel. Make sure
		// you do this after you deal with the transparency issue on grayscale
		// or RGB images. If your bit depth is 8, use gamma_table, if it
		// is 16, use gamma_16_table and gamma_shift. Build these with
		// build_gamma_table().
		static unsafe void png_do_gamma(ref png_row_info row_info, byte[] row, byte[] gamma_table, ushort[][] gamma_16_table, int gamma_shift)
		{
			if(((row_info.bit_depth>8||gamma_table==null)&&(row_info.bit_depth!=16||gamma_16_table==null))) return;

			uint row_width=row_info.width;

			fixed(byte* row_=row)
			{
				byte* sp=row_+1; // skip filter value

				switch(row_info.color_type)
				{
					case PNG_COLOR_TYPE.RGB:
						{
							if(row_info.bit_depth==8)
							{
								for(uint i=0; i<row_width; i++)
								{
									*sp=gamma_table[*sp]; sp++;
									*sp=gamma_table[*sp]; sp++;
									*sp=gamma_table[*sp]; sp++;
								}
							}
							else // if(row_info->bit_depth==16)
							{
								for(uint i=0; i<row_width; i++)
								{
									ushort v=gamma_16_table[*(sp+1)>>gamma_shift][*sp];
									*sp=(byte)((v>>8)&0xff);
									*(sp+1)=(byte)(v&0xff); sp+=2;
									v=gamma_16_table[*(sp+1)>>gamma_shift][*sp];
									*sp=(byte)((v>>8)&0xff);
									*(sp+1)=(byte)(v&0xff); sp+=2;
									v=gamma_16_table[*(sp+1)>>gamma_shift][*sp];
									*sp=(byte)((v>>8)&0xff);
									*(sp+1)=(byte)(v&0xff); sp+=2;
								}
							}
						}
						break;
					case PNG_COLOR_TYPE.RGB_ALPHA:
						{
							if(row_info.bit_depth==8)
							{
								for(uint i=0; i<row_width; i++)
								{
									*sp=gamma_table[*sp]; sp++;
									*sp=gamma_table[*sp]; sp++;
									*sp=gamma_table[*sp]; sp++;
									sp++;
								}
							}
							else // if (row_info->bit_depth==16)
							{
								for(uint i=0; i<row_width; i++)
								{
									ushort v=gamma_16_table[*(sp+1)>>gamma_shift][*sp];
									*sp=(byte)((v>>8)&0xff);
									*(sp+1)=(byte)(v&0xff); sp+=2;
									v=gamma_16_table[*(sp+1)>>gamma_shift][*sp];
									*sp=(byte)((v>>8)&0xff);
									*(sp+1)=(byte)(v&0xff); sp+=2;
									v=gamma_16_table[*(sp+1)>>gamma_shift][*sp];
									*sp=(byte)((v>>8)&0xff);
									*(sp+1)=(byte)(v&0xff); sp+=4;
								}
							}
						}
						break;
					case PNG_COLOR_TYPE.GRAY_ALPHA:
						{
							if(row_info.bit_depth==8)
							{
								for(uint i=0; i<row_width; i++, sp+=2) *sp=gamma_table[*sp];
							}
							else // if (row_info->bit_depth==16)
							{
								for(uint i=0; i<row_width; i++)
								{
									ushort v=gamma_16_table[*(sp+1)>>gamma_shift][*sp];
									*sp=(byte)((v>>8)&0xff);
									*(sp+1)=(byte)(v&0xff); sp+=4;
								}
							}
						}
						break;
					case PNG_COLOR_TYPE.GRAY:
						{
							if(row_info.bit_depth==2)
							{
								for(uint i=0; i<row_width; i+=4)
								{
									int a=*sp&0xc0;
									int b=*sp&0x30;
									int c=*sp&0x0c;
									int d=*sp&0x03;

									*sp=(byte)(((((int)gamma_table[a|(a>>2)|(a>>4)|(a>>6)]))&0xc0)|((((int)gamma_table[(b<<2)|b|(b>>2)|(b>>4)])>>2)&0x30)|
										((((int)gamma_table[(c<<4)|(c<<2)|c|(c>>2)])>>4)&0x0c)|((((int)gamma_table[(d<<6)|(d<<4)|(d<<2)|d])>>6)));
									sp++;
								}
							}
							else if(row_info.bit_depth==4)
							{
								for(uint i=0; i<row_width; i+=2)
								{
									int msb=*sp&0xf0;
									int lsb=*sp&0x0f;

									*sp=(byte)((((int)gamma_table[msb|(msb>>4)])&0xf0)|(((int)gamma_table[(lsb<<4)|lsb])>>4));
									sp++;
								}
							}
							else if(row_info.bit_depth==8)
							{
								for(uint i=0; i<row_width; i++, sp++) *sp=gamma_table[*sp];
							}
							else if(row_info.bit_depth==16)
							{
								for(uint i=0; i<row_width; i++)
								{
									ushort v=gamma_16_table[*(sp+1)>>gamma_shift][*sp];
									*sp=(byte)((v>>8)&0xff);
									*(sp+1)=(byte)(v&0xff); sp+=2;
								}
							}
						}
						break;
				}
			}
		}

		// Expands a palette row to an RGB or RGBA row depending
		// upon whether you supply trans_alpha and num_trans.
		static unsafe void png_do_expand_palette(ref png_row_info row_info, byte[] row, png_color[] palette, byte[] trans_alpha)
		{
			int shift, value;
			uint row_width=row_info.width;

			if(row_info.color_type!=PNG_COLOR_TYPE.PALETTE) return;

			fixed(byte* row_=row)
			{
				byte* sp=row_+1; // skip filter value
				byte* dp=row_+1; // skip filter value

				if(row_info.bit_depth<8)
				{
					switch(row_info.bit_depth)
					{
						case 1:
							{
								sp+=(row_width-1)>>3;
								dp+=row_width-1;
								shift=7-(int)((row_width+7)&0x07);
								for(uint i=0; i<row_width; i++)
								{
									if(((*sp>>shift)&0x01)==0x01) *dp=1;
									else *dp=0;
									if(shift==7)
									{
										shift=0;
										sp--;
									}
									else shift++;

									dp--;
								}
							}
							break;
						case 2:
							{
								sp+=(row_width-1)>>2;
								dp+=row_width-1;
								shift=(int)((3-((row_width+3)&0x03))<<1);
								for(uint i=0; i<row_width; i++)
								{
									value=(*sp>>shift)&0x03;
									*dp=(byte)value;
									if(shift==6)
									{
										shift=0;
										sp--;
									}
									else shift+=2;

									dp--;
								}
							}
							break;
						case 4:
							{
								sp+=(row_width-1)>>1;
								dp+=row_width-1;
								shift=(int)((row_width&0x01)<<2);
								for(uint i=0; i<row_width; i++)
								{
									value=(*sp>>shift)&0x0f;
									*dp=(byte)value;
									if(shift==4)
									{
										shift=0;
										sp--;
									}
									else shift+=4;

									dp--;
								}
							}
							break;
					}
					row_info.bit_depth=8;
					row_info.pixel_depth=8;
					row_info.rowbytes=row_width;
				} // if(row_info.bit_depth<8)

				if(row_info.bit_depth==8)
				{
					sp=row_+1;
					dp=row_+1;
					if(trans_alpha!=null&&trans_alpha.Length!=0)
					{
						sp+=row_width-1;
						dp+=(row_width<<2)-1;

						for(uint i=0; i<row_width; i++)
						{
							if(*sp>=trans_alpha.Length) *dp--=0xff;
							else *dp--=trans_alpha[*sp];
							*dp--=palette[*sp].blue;
							*dp--=palette[*sp].green;
							*dp--=palette[*sp].red;
							sp--;
						}
						row_info.bit_depth=8;
						row_info.pixel_depth=32;
						row_info.rowbytes=row_width*4;
						row_info.color_type=PNG_COLOR_TYPE.RGB_ALPHA;
						row_info.channels=4;
					}
					else
					{
						sp+=row_width-1;
						dp+=(row_width*3)-1;

						for(uint i=0; i<row_width; i++)
						{
							*dp--=palette[*sp].blue;
							*dp--=palette[*sp].green;
							*dp--=palette[*sp].red;
							sp--;
						}
						row_info.bit_depth=8;
						row_info.pixel_depth=24;
						row_info.rowbytes=row_width*3;
						row_info.color_type=PNG_COLOR_TYPE.RGB;
						row_info.channels=3;
					}
				}
			}
		}

		// If the bit depth < 8, it is expanded to 8. Also, if the already
		// expanded transparency value is supplied, an alpha channel is built.
		static unsafe void png_do_expand_with_trans_values(ref png_row_info row_info, byte[] row, ref png_color_16 trans_value)
		{
			int shift, value;
			uint row_width=row_info.width;

			fixed(byte* row_=row)
			{
				byte* sp=row_+1; // skip filter value
				byte* dp=row_+1; // skip filter value

				if(row_info.color_type==PNG_COLOR_TYPE.GRAY)
				{
					ushort gray=trans_value.gray;

					if(row_info.bit_depth<8)
					{
						switch(row_info.bit_depth)
						{
							case 1:
								{
									gray=(ushort)((gray&0x01)*0xff);
									sp+=(row_width-1)>>3;
									dp+=row_width-1;
									shift=7-(int)((row_width+7)&0x07);
									for(uint i=0; i<row_width; i++)
									{
										if(((*sp>>shift)&0x01)==0x01) *dp=0xff;
										else *dp=0;
										if(shift==7)
										{
											shift=0;
											sp--;
										}
										else shift++;

										dp--;
									}
								}
								break;
							case 2:
								{
									gray=(ushort)((gray&0x03)*0x55);
									sp+=(row_width-1)>>2;
									dp+=row_width-1;
									shift=(int)((3-((row_width+3)&0x03))<<1);
									for(uint i=0; i<row_width; i++)
									{
										value=(*sp>>shift)&0x03;
										*dp=(byte)(value|(value<<2)|(value<<4)|(value<<6));
										if(shift==6)
										{
											shift=0;
											sp--;
										}
										else shift+=2;

										dp--;
									}
								}
								break;
							case 4:
								{
									gray=(ushort)((gray&0x0f)*0x11);
									sp+=(row_width-1)>>1;
									dp+=row_width-1;
									shift=(int)((1-((row_width+1)&0x01))<<2);
									for(uint i=0; i<row_width; i++)
									{
										value=(*sp>>shift)&0x0f;
										*dp=(byte)(value|(value<<4));
										if(shift==4)
										{
											shift=0;
											sp--;
										}
										else shift=4;

										dp--;
									}
								}
								break;
						}
						row_info.bit_depth=8;
						row_info.pixel_depth=8;
						row_info.rowbytes=row_width;

						// reset to start values
						sp=row_+1; // skip filter value
						dp=row_+1; // skip filter value
					} // if(row_info.bit_depth<8)

					if(row_info.bit_depth==8)
					{
						gray=(byte)(gray&0xff);
						sp+=row_width-1;
						dp+=(row_width<<1)-1;
						for(uint i=0; i<row_width; i++)
						{
							if(*sp==gray) *dp--=0;
							else *dp--=0xff;
							*dp--=*sp--;
						}
					}
					else if(row_info.bit_depth==16)
					{
						byte gray_high=(byte)((gray>>8)&0xff);
						byte gray_low=(byte)(gray&0xff);
						sp+=row_info.rowbytes-1;
						dp+=(row_info.rowbytes<<1)-1;
						for(uint i=0; i<row_width; i++)
						{
							if(*(sp-1)==gray_high&&*(sp)==gray_low)
							{
								*dp--=0;
								*dp--=0;
							}
							else
							{
								*dp--=0xff;
								*dp--=0xff;
							}
							*dp--=*sp--;
							*dp--=*sp--;
						}
					}
					row_info.color_type=PNG_COLOR_TYPE.GRAY_ALPHA;
					row_info.channels=2;
					row_info.pixel_depth=(byte)(row_info.bit_depth<<1);
					row_info.rowbytes=PNG_ROWBYTES(row_info.pixel_depth, row_width);
				}
				else if(row_info.color_type==PNG_COLOR_TYPE.RGB)
				{
					if(row_info.bit_depth==8)
					{
						byte red=(byte)(trans_value.red&0xff);
						byte green=(byte)(trans_value.green&0xff);
						byte blue=(byte)(trans_value.blue&0xff);
						sp+=row_info.rowbytes-1;
						dp+=(row_width<<2)-1;
						for(uint i=0; i<row_width; i++)
						{
							if(*(sp-2)==red&&*(sp-1)==green&&*(sp)==blue) *dp--=0;
							else *dp--=0xff;
							*dp--=*sp--;
							*dp--=*sp--;
							*dp--=*sp--;
						}
					}
					else if(row_info.bit_depth==16)
					{
						byte red_high=(byte)((trans_value.red>>8)&0xff);
						byte green_high=(byte)((trans_value.green>>8)&0xff);
						byte blue_high=(byte)((trans_value.blue>>8)&0xff);
						byte red_low=(byte)(trans_value.red&0xff);
						byte green_low=(byte)(trans_value.green&0xff);
						byte blue_low=(byte)(trans_value.blue&0xff);
						sp+=row_info.rowbytes-1;
						dp+=(row_width<<3)-1;
						for(uint i=0; i<row_width; i++)
						{
							if(*(sp-5)==red_high&&*(sp-4)==red_low&&*(sp-3)==green_high&&*(sp-2)==green_low&&*(sp-1)==blue_high&&*(sp)==blue_low)
							{
								*dp--=0;
								*dp--=0;
							}
							else
							{
								*dp--=0xff;
								*dp--=0xff;
							}
							*dp--=*sp--;
							*dp--=*sp--;
							*dp--=*sp--;
							*dp--=*sp--;
							*dp--=*sp--;
							*dp--=*sp--;
						}
					}
					row_info.color_type=PNG_COLOR_TYPE.RGB_ALPHA;
					row_info.channels=4;
					row_info.pixel_depth=(byte)(row_info.bit_depth<<2);
					row_info.rowbytes=PNG_ROWBYTES(row_info.pixel_depth, row_width);
				}
			}
		}

		// If the bit depth < 8, it is expanded to 8. Also, if the already
		// expanded transparency value is supplied, an alpha channel is built.
		static unsafe void png_do_expand(ref png_row_info row_info, byte[] row)
		{
			int shift, value;
			uint row_width=row_info.width;

			if(row_info.color_type==PNG_COLOR_TYPE.GRAY)
			{
				if(row_info.bit_depth<8)
				{
					fixed(byte* row_=row)
					{
						byte* sp=row_+1; // skip filter value
						byte* dp=row_+1; // skip filter value

						ushort gray=0;

						switch(row_info.bit_depth)
						{
							case 1:
								{
									gray=(ushort)((gray&0x01)*0xff);
									sp+=(row_width-1)>>3;
									dp+=row_width-1;
									shift=7-(int)((row_width+7)&0x07);
									for(uint i=0; i<row_width; i++)
									{
										if(((*sp>>shift)&0x01)==0x01) *dp=0xff;
										else *dp=0;
										if(shift==7)
										{
											shift=0;
											sp--;
										}
										else shift++;

										dp--;
									}
								}
								break;
							case 2:
								{
									gray=(ushort)((gray&0x03)*0x55);
									sp+=(row_width-1)>>2;
									dp+=row_width-1;
									shift=(int)((3-((row_width+3)&0x03))<<1);
									for(uint i=0; i<row_width; i++)
									{
										value=(*sp>>shift)&0x03;
										*dp=(byte)(value|(value<<2)|(value<<4)|(value<<6));
										if(shift==6)
										{
											shift=0;
											sp--;
										}
										else shift+=2;

										dp--;
									}
								}
								break;
							case 4:
								{
									gray=(ushort)((gray&0x0f)*0x11);
									sp+=(row_width-1)>>1;
									dp+=row_width-1;
									shift=(int)((1-((row_width+1)&0x01))<<2);
									for(uint i=0; i<row_width; i++)
									{
										value=(*sp>>shift)&0x0f;
										*dp=(byte)(value|(value<<4));
										if(shift==4)
										{
											shift=0;
											sp--;
										}
										else shift=4;

										dp--;
									}
								}
								break;
						}
						row_info.bit_depth=8;
						row_info.pixel_depth=8;
						row_info.rowbytes=row_width;
					}
				}
			}
		}

		static unsafe void png_do_quantize(ref png_row_info row_info, byte[] row, byte[] palette_lookup, byte[] quantize_lookup)
		{
			uint row_width=row_info.width;

			fixed(byte* row_=row)
			{
				byte* sp=row_+1; // skip filter value
				byte* dp=row_+1; // skip filter value

				if(row_info.color_type==PNG_COLOR_TYPE.RGB&&palette_lookup!=null&&row_info.bit_depth==8)
				{
					int r, g, b, p;
					for(uint i=0; i<row_width; i++)
					{
						r=*sp++;
						g=*sp++;
						b=*sp++;

						// this looks real messy, but the compiler will reduce
						// it down to a reasonable formula. For example, with
						// 5 bits per color, we get:
						// p=(((r>>3)&0x1f)<<10)|(((g>>3)&0x1f)<<5)|((b>>3)&0x1f);
						p=(((r>>(8-PNG_QUANTIZE_RED_BITS))&((1<<PNG_QUANTIZE_RED_BITS)-1))<<(PNG_QUANTIZE_GREEN_BITS+PNG_QUANTIZE_BLUE_BITS))|
							(((g>>(8-PNG_QUANTIZE_GREEN_BITS))&((1<<PNG_QUANTIZE_GREEN_BITS)-1))<<(PNG_QUANTIZE_BLUE_BITS))|
							((b>>(8-PNG_QUANTIZE_BLUE_BITS))&((1<<PNG_QUANTIZE_BLUE_BITS)-1));

						*dp++=palette_lookup[p];
					}
					row_info.color_type=PNG_COLOR_TYPE.PALETTE;
					row_info.channels=1;
					row_info.pixel_depth=row_info.bit_depth;
					row_info.rowbytes=PNG_ROWBYTES(row_info.pixel_depth, row_width);
				}
				else if(row_info.color_type==PNG_COLOR_TYPE.RGB_ALPHA&&palette_lookup!=null&&row_info.bit_depth==8)
				{
					int r, g, b, p;
					for(uint i=0; i<row_width; i++)
					{
						r=*sp++;
						g=*sp++;
						b=*sp++;
						sp++;

						p=(((r>>(8-PNG_QUANTIZE_RED_BITS))&((1<<PNG_QUANTIZE_RED_BITS)-1))<<(PNG_QUANTIZE_GREEN_BITS+PNG_QUANTIZE_BLUE_BITS))|
							(((g>>(8-PNG_QUANTIZE_GREEN_BITS))&((1<<PNG_QUANTIZE_GREEN_BITS)-1))<<(PNG_QUANTIZE_BLUE_BITS))|
							((b>>(8-PNG_QUANTIZE_BLUE_BITS))&((1<<PNG_QUANTIZE_BLUE_BITS)-1));

						*dp++=palette_lookup[p];
					}
					row_info.color_type=PNG_COLOR_TYPE.PALETTE;
					row_info.channels=1;
					row_info.pixel_depth=row_info.bit_depth;
					row_info.rowbytes=PNG_ROWBYTES(row_info.pixel_depth, row_width);
				}
				else if(row_info.color_type==PNG_COLOR_TYPE.PALETTE&&quantize_lookup!=null&&row_info.bit_depth==8)
				{
					for(uint i=0; i<row_width; i++, sp++) *sp=quantize_lookup[*sp];
				}
			}
		}
		#endregion

		#region png_build_gamma_table
		static readonly int[] png_gamma_shift=new int[] { 0x10, 0x21, 0x42, 0x84, 0x110, 0x248, 0x550, 0xff0, 0x00 };

		// We build the 8- or 16-bit gamma tables here. Note that for 16-bit
		// tables, we don't make a full table if we are reducing to 8-bit in
		// the future. Note also how the gamma_16 tables are segmented so that
		// we don't need to allocate > 64K chunks for a full 16-bit table.

		// See the PNG extensions document for an integer algorithm for creating
		// the gamma tables.  Maybe we will implement that here someday.
		//
		// We should only reach this point if
		//
		//      the file_gamma is known (i.e., the gAMA or sRGB chunk is present,
		//      or the application has provided a file_gamma)
		//
		//   AND
		//      {
		//         the screen_gamma is known
		//
		//      OR
		//
		//         RGB_to_gray transformation is being performed
		//      }
		//
		//   AND
		//      {
		//         the screen_gamma is different from the reciprocal of the
		//         file_gamma by more than the specified threshold
		//
		//      OR
		//
		//         a background color has been specified and the file_gamma
		//         and screen_gamma are not 1.0, within the specified threshold.
		//      }
		void png_build_gamma_table(byte bit_depth)
		{
			if(bit_depth<=8)
			{
				int i;
				double g;

				if(screen_gamma>0.000001) g=1.0/(gamma*screen_gamma);
				else g=1.0;

				gamma_table=new byte[256];

				for(i=0; i<256; i++) gamma_table[i]=(byte)(Math.Pow((double)i/255.0, g)*255.0+0.5);

				if((transformations&(PNG_TRANSFORMATION.BACKGROUND|PNG_TRANSFORMATION.RGB_TO_GRAY))!=PNG_TRANSFORMATION.None)
				{
					g=1.0/gamma;
					gamma_to_1=new byte[256];
					for(i=0; i<256; i++) gamma_to_1[i]=(byte)(Math.Pow((double)i/255.0, g)*255.0+0.5);

					if(screen_gamma>0.000001) g=1.0/screen_gamma;
					else g=gamma; // probably doing rgb_to_gray
					gamma_from_1=new byte[256];
					for(i=0; i<256; i++) gamma_from_1[i]=(byte)(Math.Pow((double)i/255.0, g)*255.0+0.5);
				}
			}
			else
			{
				double g;
				int i, j, shift, num;
				int sig_bit;
				uint ig;

				if((color_type&PNG_COLOR_TYPE.COLOR_MASK)==PNG_COLOR_TYPE.COLOR_MASK)
				{
					sig_bit=(int)this.sig_bit.red;
					if((int)this.sig_bit.green>sig_bit) sig_bit=this.sig_bit.green;
					if((int)this.sig_bit.blue>sig_bit) sig_bit=this.sig_bit.blue;
				}
				else sig_bit=(int)this.sig_bit.gray;

				if(sig_bit>0) shift=16-sig_bit;
				else shift=0;

				if((transformations&PNG_TRANSFORMATION._16_TO_8)==PNG_TRANSFORMATION._16_TO_8)
				{
					if(shift<(16-PNG_MAX_GAMMA_8)) shift=(16-PNG_MAX_GAMMA_8);
				}

				if(shift>8) shift=8;
				else if(shift<0) shift=0;

				gamma_shift=(byte)shift;

				num=(1<<(8-shift));

				if(screen_gamma>0.000001) g=1.0/(gamma*screen_gamma);
				else g=1.0;

				gamma_16_table=new ushort[num][];

				if((transformations&(PNG_TRANSFORMATION._16_TO_8|PNG_TRANSFORMATION.BACKGROUND))!=PNG_TRANSFORMATION.None)
				{
					double fin, fout;
					uint last, max;

					for(i=0; i<num; i++) gamma_16_table[i]=new ushort[256];

					g=1.0/g;
					last=0;
					for(i=0; i<256; i++)
					{
						fout=(i+0.5)/256.0;
						fin=Math.Pow(fout, g);
						max=(uint)(fin*(double)((uint)num<<8));
						while(last<=max)
						{
							gamma_16_table[(int)(last&(0xff>>shift))][(int)(last>>(8-shift))]=(ushort)((ushort)i|((ushort)i<<8));
							last++;
						}
					}
					while(last<((uint)num<<8))
					{
						gamma_16_table[(int)(last&(0xff>>shift))][(int)(last>>(8-shift))]=65535;
						last++;
					}
				}
				else
				{
					for(i=0; i<num; i++)
					{
						gamma_16_table[i]=new ushort[256];

						ig=(((uint)i*(uint)png_gamma_shift[shift])>>4);
						for(j=0; j<256; j++) gamma_16_table[i][j]=(ushort)(Math.Pow((double)(ig+((uint)j<<8))/65535.0, g)*65535.0+0.5);
					}
				}

				if((transformations&(PNG_TRANSFORMATION.BACKGROUND|PNG_TRANSFORMATION.RGB_TO_GRAY))!=PNG_TRANSFORMATION.None)
				{
					g=1.0/gamma;

					gamma_16_to_1=new ushort[num][];

					for(i=0; i<num; i++)
					{
						gamma_16_to_1[i]=new ushort[256];

						ig=(((uint)i*(uint)png_gamma_shift[shift])>>4);
						for(j=0; j<256; j++) gamma_16_to_1[i][j]=(ushort)(Math.Pow((double)(ig+((uint)j<<8))/65535.0, g)*65535.0+0.5);
					}

					if(screen_gamma>0.000001) g=1.0/screen_gamma;
					else g=gamma; // probably doing rgb_to_gray

					gamma_16_from_1=new ushort[num][];

					for(i=0; i<num; i++)
					{
						gamma_16_from_1[i]=new ushort[256];

						ig=(((uint)i*(uint)png_gamma_shift[shift])>>4);
						for(j=0; j<256; j++) gamma_16_from_1[i][j]=(ushort)(Math.Pow((double)(ig+((uint)j<<8))/65535.0, g)*65535.0+0.5);
					}
				}
			}
		}
		#endregion

		// Todo: install integer version of png_build_gamma_table here

		// undoes intrapixel differencing
		static unsafe void png_do_read_intrapixel(png_row_info row_info, byte[] row)
		{
			if((row_info.color_type&PNG_COLOR_TYPE.COLOR_MASK)!=PNG_COLOR_TYPE.COLOR_MASK) return;

			int bytes_per_pixel;
			uint row_width=row_info.width;

			fixed(byte* row_=row)
			{
				byte* rp=row_+1; // skip filter value

				if(row_info.bit_depth==8)
				{
					if(row_info.color_type==PNG_COLOR_TYPE.RGB) bytes_per_pixel=3;
					else if(row_info.color_type==PNG_COLOR_TYPE.RGB_ALPHA) bytes_per_pixel=4;
					else return;

					for(uint i=0; i<row_width; i++, rp+=bytes_per_pixel)
					{
						*(rp)=(byte)((256+*rp+*(rp+1))&0xff);
						*(rp+2)=(byte)((256+*(rp+2)+*(rp+1))&0xff);
					}
				}
				else if(row_info.bit_depth==16)
				{
					if(row_info.color_type==PNG_COLOR_TYPE.RGB) bytes_per_pixel=6;
					else if(row_info.color_type==PNG_COLOR_TYPE.RGB_ALPHA) bytes_per_pixel=8;
					else return;

					for(uint i=0; i<row_width; i++, rp+=bytes_per_pixel)
					{
						uint s0=(uint)((*(rp)<<8)|*(rp+1));
						uint s1=(uint)((*(rp+2)<<8)|*(rp+3));
						uint s2=(uint)((*(rp+4)<<8)|*(rp+5));
						uint red=(uint)((s0+s1+65536)&0xffff);
						uint blue=(uint)((s2+s1+65536)&0xffff);
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
