// pngerror.cs - stub functions for i/o and memory allocation
//
// Based on libpng version 1.4.3 - June 26, 2010
// This code is released under the libpng license.
// For conditions of distribution and use, see copyright notice in License.txt
// Copyright (C) 2007-2010 by the Authors
// Copyright (c) 1998-2010 Glenn Randers-Pehrson
// (Version 0.96 Copyright (c) 1996, 1997 Andreas Dilger)
// (Version 0.88 Copyright (c) 1995, 1996 Guy Eric Schalnat, Group 42, Inc.)
//
// Original error and warning handling is completely replaced.
// * Errors are now thrown as PNG_Exceptions with message, if out of memory OutOfMemoryException is thrown,
// * Warnings now appear on the debug console.
// * No filtering of error no.

using System;
using System.Diagnostics;

namespace Free.Ports.libpng
{
	public class PNG_Exception : Exception
	{
		public PNG_Exception() { }
		public PNG_Exception(string message): base(message) { }
		public PNG_Exception(string message, Exception innerException) : base(message, innerException) { }
	}

	public partial class png_struct
	{
		// These utilities are used internally to build an error message that relates
		// to the current chunk. The chunk name comes from png_ptr.chunk_name,
		// this is used to prefix the message. The message is limited in length
		// to 63 bytes, the name characters are output as hex digits wrapped in []
		// if the character is invalid.
		static readonly char[] png_digit={ '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F' };

		static string png_format_buffer(byte[] chunk_name, string error_message)
		{
			int i=0;
			string buffer="";
			while(i<4)
			{
				int c=chunk_name[i++];
				if(c<65||c>122||(c>90&&c<97))
				{
					buffer+='[';
					buffer+=png_digit[(c&0xf0)>>4];
					buffer+=png_digit[c&0x0f];
					buffer+=']';
				}
				else buffer+=(char)c;
			}

			if(error_message!=null&&error_message.Length!=0) buffer+=": "+error_message;
			return buffer;
		}

		public static void png_chunk_error(byte[] chunk_name, string error_message)
		{
			if(chunk_name==null||chunk_name.Length!=4) throw new PNG_Exception(error_message);
			throw new PNG_Exception(png_format_buffer(chunk_name, error_message));
		}

		public static void png_chunk_warning(byte[] chunk_name, string warning_message)
		{
			if(chunk_name==null||chunk_name.Length!=4) Debug.WriteLine(warning_message);
			else Debug.WriteLine(png_format_buffer(chunk_name, warning_message));
		}
	}
}
