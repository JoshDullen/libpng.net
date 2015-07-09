// png_gIF.cs - gIF? chunk structures
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
	public struct png_gIFg
	{
		public byte disposal_methode;
		public byte user_input_flag;
		public ushort delay_time;
	}

	public struct png_gIFx
	{
		public byte app_identifier1;
		public byte app_identifier2;
		public byte app_identifier3;
		public byte app_identifier4;
		public byte app_identifier5;
		public byte app_identifier6;
		public byte app_identifier7;
		public byte app_identifier8;
		public byte auth_code1;
		public byte auth_code2;
		public byte auth_code3;
		public byte[] app_data;
	}
}
