#region Copyright & License Information
/*
 * Copyright 2007-2020 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.IO;

namespace OpenRA.Mods.Cnc.FileFormats
{
	/// <summary>
	/// Parses an uncompressed, DXT1 or DXT5 encoded DDS file into raw BGRA data
	/// </summary>
	class Dds
	{
		public int Width { get; set; }
		public int Height { get; set; }
		public byte[] Data { get; set; }

		public Dds(Stream s)
		{
			if (s.ReadUInt32() != 0x20534444)
				throw new InvalidDataException("DDS Signature is bogus");

			// Read the minimal header flags we need
			// https://docs.microsoft.com/en-us/windows/win32/direct3ddds/dx-graphics-dds-pguide
			s.Position += 8;
			Height = (int)s.ReadUInt32();
			Width = (int)s.ReadUInt32();
			s.Position += 60;
			var formatFlags = s.ReadUInt32();
			var formatCode = s.ReadASCII(4);
			s.Position += 40;

			if (formatFlags == 0x41)
			{
				Data = s.ReadBytes(4 * Width * Height);

				// Convert RGBA to BGRA
				for (var i = 0; i < Data.Length; i += 4)
				{
					var temp = Data[i];
					Data[i] = Data[i + 2];
					Data[i + 2] = temp;
				}
			}
			else if (formatFlags == 0x04)
			{
				// DXT1 and DXT5 decoding is described in
				// https://www.khronos.org/registry/OpenGL/extensions/EXT/EXT_texture_compression_s3tc.txt
				// TODO: add native S3TC support to OpenRA's SheetBuilder and rendering code
				// Decoding images to raw pixel data is very wasteful!
				Data = new byte[Width * Height * 4];
				switch (formatCode)
				{
					case "DXT1": DecodeDXT1(s); break;
					case "DXT5": DecodeDXT5(s); break;
					default: throw new NotImplementedException("Unknown FourCC pixel format `{0}`.".F(formatCode));
				}
			}
			else
				throw new NotImplementedException("Unknown pixel format 0x{0:X}.".F(formatFlags));
		}

		void DecodeDXT1(Stream s)
		{
			var blockWidth = (Width + 3) / 4;
			var blockHeight = (Height + 3) / 4;
			var raw = s.ReadBytes(8 * blockWidth * blockHeight);

			for (var by = 0; by < blockHeight; by++)
			{
				for (var bx = 0; bx < blockWidth; bx++)
				{
					var input = 8 * (blockWidth * by + bx);
					var color0 = BitConverter.ToUInt16(raw, input);
					var color1 = BitConverter.ToUInt16(raw, input + 2);
					var bits = BitConverter.ToUInt32(raw, input + 4);
					var rgb0 = Unpack565(color0);
					var rgb1 = Unpack565(color1);

					// Decode pixels from block
					for (var j = 0; j < 4; j++)
					{
						for (var i = 0; i < 4; i++)
						{
							var x = 4 * bx + i;
							var y = 4 * by + j;
							var output = 4 * (y * Width + x);
							switch ((bits >> (2 * (4 * j + i))) & 0x03)
							{
								case 0:
									Data[output++] = rgb0.R;
									Data[output++] = rgb0.G;
									Data[output++] = rgb0.B;
									break;
								case 1:
									Data[output++] = rgb1.R;
									Data[output++] = rgb1.G;
									Data[output++] = rgb1.B;
									break;
								case 2:
								{
									if (color0 > color1)
									{
										Data[output++] = (byte)((2 * rgb0.R + rgb1.R) / 3);
										Data[output++] = (byte)((2 * rgb0.G + rgb1.G) / 3);
										Data[output++] = (byte)((2 * rgb0.B + rgb1.B) / 3);
									}
									else
									{
										Data[output++] = (byte)((rgb0.R + rgb1.R) / 2);
										Data[output++] = (byte)((rgb0.G + rgb1.G) / 2);
										Data[output++] = (byte)((rgb0.B + rgb1.B) / 2);
									}

									break;
								}

								case 3:
								{
									if (color0 > color1)
									{
										Data[output++] = (byte)((rgb0.R + 2 * rgb1.R) / 3);
										Data[output++] = (byte)((rgb0.G + 2 * rgb1.G) / 3);
										Data[output++] = (byte)((rgb0.B + 2 * rgb1.B) / 3);
									}
									else
									{
										Data[output++] = 0x00;
										Data[output++] = 0x00;
										Data[output++] = 0x00;
									}

									break;
								}
							}

							Data[output] = 0xFF;
						}
					}
				}
			}
		}

		void DecodeDXT5(Stream s)
		{
			var blockWidth = (Width + 3) / 4;
			var blockHeight = (Height + 3) / 4;
			var raw = s.ReadBytes(16 * blockWidth * blockHeight);

			for (var by = 0; by < blockHeight; by++)
			{
				for (var bx = 0; bx < blockWidth; bx++)
				{
					var input = 16 * (blockWidth * by + bx);
					var alpha0 = raw[input];
					var alpha1 = raw[input + 1];
					var alphaBits = (ulong)BitConverter.ToUInt32(raw, input + 2);
					alphaBits |= (ulong)BitConverter.ToUInt16(raw, input + 6) << 32;
					var color0 = BitConverter.ToUInt16(raw, input + 8);
					var color1 = BitConverter.ToUInt16(raw, input + 10);
					var bits = BitConverter.ToUInt32(raw, input + 12);
					var rgb0 = Unpack565(color0);
					var rgb1 = Unpack565(color1);

					// Decode pixels from block
					for (var j = 0; j < 4; j++)
					{
						for (var i = 0; i < 4; i++)
						{
							var x = 4 * bx + i;
							var y = 4 * by + j;
							var output = 4 * (y * Width + x);

							switch ((bits >> (2 * (4 * j + i))) & 0x03)
							{
								case 0:
									Data[output++] = rgb0.R;
									Data[output++] = rgb0.G;
									Data[output++] = rgb0.B;
									break;
								case 1:
									Data[output++] = rgb1.R;
									Data[output++] = rgb1.G;
									Data[output++] = rgb1.B;
									break;
								case 2:
									Data[output++] = (byte)((2 * rgb0.R + rgb1.R) / 3);
									Data[output++] = (byte)((2 * rgb0.G + rgb1.G) / 3);
									Data[output++] = (byte)((2 * rgb0.B + rgb1.B) / 3);
									break;
								case 3:
									Data[output++] = (byte)((rgb0.R + 2 * rgb1.R) / 3);
									Data[output++] = (byte)((rgb0.G + 2 * rgb1.G) / 3);
									Data[output++] = (byte)((rgb0.B + 2 * rgb1.B) / 3);
									break;
							}

							switch ((alphaBits >> (3 * (4 * j + i))) & 0x07)
							{
								case 0: Data[output] = alpha0; break;
								case 1: Data[output] = alpha1; break;
								case 2: Data[output] = (byte)(alpha0 > alpha1 ? (6 * alpha0 + alpha1) / 7 : (4 * alpha0 + alpha1) / 5); break;
								case 3: Data[output] = (byte)(alpha0 > alpha1 ? (5 * alpha0 + 2 * alpha1) / 7 : (3 * alpha0 + 2 * alpha1) / 5); break;
								case 4: Data[output] = (byte)(alpha0 > alpha1 ? (4 * alpha0 + 3 * alpha1) / 7 : (2 * alpha0 + 3 * alpha1) / 5); break;
								case 5: Data[output] = (byte)(alpha0 > alpha1 ? (3 * alpha0 + 4 * alpha1) / 7 : (alpha0 + 4 * alpha1) / 5); break;
								case 6: Data[output] = (byte)(alpha0 > alpha1 ? (2 * alpha0 + 5 * alpha1) / 7 : 0x00); break;
								case 7: Data[output] = (byte)(alpha0 > alpha1 ? (alpha0 + 6 * alpha1) / 7 : 0xFF); break;
							}
						}
					}
				}
			}
		}

		static (byte R, byte G, byte B) Unpack565(ushort packed)
		{
			var r5 = packed >> 11;
			var g6 = (packed >> 5) & 0x3F;
			var b5 = packed & 0x1F;

			// Expand bits to cover the full range
			// https://developer.apple.com/documentation/accelerate/1533159-vimageconvert_rgb565toargb8888
			var r = (byte)((r5 * 255 + 15) / 31);
			var g = (byte)((g6 * 255 + 31) / 63);
			var b = (byte)((b5 * 255 + 15) / 31);

			return (r, g, b);
		}
	}
}
