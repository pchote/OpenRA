#region Copyright notice
/* C# port of the crude minilzo source version 2.06 by Frank Razenberg
 
  Beware, you should never want to see C# code like this. You were warned.
  I simply ran the MSVC preprocessor on the original source, changed the datatypes 
  to their C# counterpart and fixed changed some control flow stuff to amend for
  the different goto semantics between C and C#.

  Original copyright notice is included below.
*/

/* minilzo.c -- mini subset of the LZO real-time data compression library

   This file is part of the LZO real-time data compression library.

   Copyright (C) 2011 Markus Franz Xaver Johannes Oberhumer
   Copyright (C) 2010 Markus Franz Xaver Johannes Oberhumer
   Copyright (C) 2009 Markus Franz Xaver Johannes Oberhumer
   Copyright (C) 2008 Markus Franz Xaver Johannes Oberhumer
   Copyright (C) 2007 Markus Franz Xaver Johannes Oberhumer
   Copyright (C) 2006 Markus Franz Xaver Johannes Oberhumer
   Copyright (C) 2005 Markus Franz Xaver Johannes Oberhumer
   Copyright (C) 2004 Markus Franz Xaver Johannes Oberhumer
   Copyright (C) 2003 Markus Franz Xaver Johannes Oberhumer
   Copyright (C) 2002 Markus Franz Xaver Johannes Oberhumer
   Copyright (C) 2001 Markus Franz Xaver Johannes Oberhumer
   Copyright (C) 2000 Markus Franz Xaver Johannes Oberhumer
   Copyright (C) 1999 Markus Franz Xaver Johannes Oberhumer
   Copyright (C) 1998 Markus Franz Xaver Johannes Oberhumer
   Copyright (C) 1997 Markus Franz Xaver Johannes Oberhumer
   Copyright (C) 1996 Markus Franz Xaver Johannes Oberhumer
   All Rights Reserved.

   The LZO library is free software; you can redistribute it and/or
   modify it under the terms of the GNU General Public License as
   published by the Free Software Foundation; either version 2 of
   the License, or (at your option) any later version.

   The LZO library is distributed in the hope that it will be useful,
   but WITHOUT ANY WARRANTY; without even the implied warranty of
   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
   GNU General Public License for more details.

   You should have received a copy of the GNU General Public License
   along with the LZO library; see the file COPYING.
   If not, write to the Free Software Foundation, Inc.,
   51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.

   Markus F.X.J. Oberhumer
   <markus@oberhumer.com>
   http://www.oberhumer.com/opensource/lzo/
 */

/*
 * NOTE:
 *   the full LZO package can be found at
 *   http://www.oberhumer.com/opensource/lzo/
 */

#endregion

using System;

namespace OpenRA.Mods.TS.UtilityCommands
{
	public static class MiniLZO
	{
		unsafe static int lzo1x_decompress(byte* @in, uint in_len, byte* @out, ref uint out_len, void* wrkmem)
		{
			byte* op;
			byte* ip;
			uint t;
			byte* m_pos;
			byte* ip_end = @in + in_len;
			out_len = 0;
			op = @out;
			ip = @in;
			bool gt_first_literal_run = false;
			bool gt_match_done = false;
			if (*ip > 17) {
				t = (uint)(*ip++ - 17);
				if (t < 4) {
					match_next(ref op, ref ip, ref t);
				}
				else {
					do *op++ = *ip++; while (--t > 0);
					gt_first_literal_run = true;
				}
			}
			while (true) {
				if (gt_first_literal_run) {
					gt_first_literal_run = false;
					goto first_literal_run;
				}

				t = *ip++;
				if (t >= 16)
					goto match;
				if (t == 0) {
					while (*ip == 0) {
						t += 255;
						ip++;
					}
					t += (uint)(15 + *ip++);
				}
				*(uint*)op = *(uint*)ip;
				op += 4; ip += 4;
				if (--t > 0) {
					if (t >= 4) {
						do {
							*(uint*)op = *(uint*)ip;
							op += 4; ip += 4; t -= 4;
						} while (t >= 4);
						if (t > 0) do *op++ = *ip++; while (--t > 0);
					}
					else
						do *op++ = *ip++; while (--t > 0);
				}
			first_literal_run:
				t = *ip++;
				if (t >= 16)
					goto match;
				m_pos = op - (1 + 0x0800);
				m_pos -= t >> 2;
				m_pos -= *ip++ << 2;

				*op++ = *m_pos++; *op++ = *m_pos++; *op++ = *m_pos;
				gt_match_done = true;

			match:
				do {
					if (gt_match_done) {
						gt_match_done = false;
						goto match_done;
						;
					}
					if (t >= 64) {
						m_pos = op - 1;
						m_pos -= (t >> 2) & 7;
						m_pos -= *ip++ << 3;
						t = (t >> 5) - 1;

						copy_match(ref op, ref m_pos, ref t);
						goto match_done;
					}
					else if (t >= 32) {
						t &= 31;
						if (t == 0) {
							while (*ip == 0) {
								t += 255;
								ip++;
							}
							t += (uint)(31 + *ip++);
						}
						m_pos = op - 1;
						m_pos -= (*(ushort*)(void*)(ip)) >> 2;
						ip += 2;
					}
					else if (t >= 16) {
						m_pos = op;
						m_pos -= (t & 8) << 11;
						t &= 7;
						if (t == 0) {
							while (*ip == 0) {
								t += 255;
								ip++;
							}
							t += (uint)(7 + *ip++);
						}
						m_pos -= (*(ushort*)ip) >> 2;
						ip += 2;
						if (m_pos == op)
							goto eof_found;
						m_pos -= 0x4000;
					}
					else {
						m_pos = op - 1;
						m_pos -= t >> 2;
						m_pos -= *ip++ << 2;
						*op++ = *m_pos++; *op++ = *m_pos;
						goto match_done;
					}

					if (t >= 2 * 4 - (3 - 1) && (op - m_pos) >= 4) {
						*(uint*)op = *(uint*)m_pos;
						op += 4; m_pos += 4; t -= 4 - (3 - 1);
						do {
							*(uint*)op = *(uint*)m_pos;
							op += 4; m_pos += 4; t -= 4;
						} while (t >= 4);
						if (t > 0) do *op++ = *m_pos++; while (--t > 0);
					}
					else {
					// copy_match:
						*op++ = *m_pos++; *op++ = *m_pos++;
						do *op++ = *m_pos++; while (--t > 0);
					}
				match_done:
					t = (uint)(ip[-2] & 3);
					if (t == 0)
						break;
				// match_next:
					*op++ = *ip++;
					if (t > 1) { *op++ = *ip++; if (t > 2) { *op++ = *ip++; } }
					t = *ip++;
				} while (true);
			}
		eof_found:

			out_len = ((uint)((op) - (@out)));
			return (ip == ip_end ? 0 :
				   (ip < ip_end ? (-8) : (-4)));
		}

		static unsafe void match_next(ref byte* op, ref byte* ip, ref uint t)
		{
			do *op++ = *ip++; while (--t > 0);
			t = *ip++;
		}

		static unsafe void copy_match(ref byte* op, ref byte* m_pos, ref uint t)
		{
			*op++ = *m_pos++; *op++ = *m_pos++;
			do *op++ = *m_pos++; while (--t > 0);
		}

		public static void DecodeInto(byte[] src, uint srcOffset, uint srcLength, byte[] dest, uint destOffset, ref uint destLength)
		{
			unsafe
			{
				fixed (byte* r = src, w = dest, wrkmem = new byte[IntPtr.Size * 16384])
				{
					lzo1x_decompress(r + srcOffset, srcLength, w + destOffset, ref destLength, wrkmem);
				}
			}
		}
	}
}
