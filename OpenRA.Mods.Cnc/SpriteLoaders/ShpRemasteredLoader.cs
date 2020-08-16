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
using System.Linq;
using System.Text.RegularExpressions;
using ICSharpCode.SharpZipLib.Zip;
using OpenRA.Graphics;
using OpenRA.Primitives;

namespace OpenRA.Mods.Cnc.SpriteLoaders
{
	public class ShpRemasteredLoader : ISpriteLoader
	{
		public static bool IsShpRemastered(Stream s)
		{
			var start = s.Position;
			var isZipFile = s.ReadUInt32() == 0x04034B50;
			s.Position = start;

			return isZipFile;
		}

		public bool TryParseSprite(Stream s, out ISpriteFrame[] frames, out TypeDictionary metadata)
		{
			metadata = null;
			if (!IsShpRemastered(s))
			{
				frames = null;
				return false;
			}

			frames = new ShpRemasteredSprite(s).Frames.ToArray();
			return true;
		}
	}

	public class ShpRemasteredSprite
	{
		static readonly Regex FilenameRegex = new Regex(@"^(?<prefix>.+?[\-_])(?<frame>\d{4})\.tga$");
		static readonly Regex MetaRegex = new Regex(@"^\{""size"":\[(?<width>\d+),(?<height>\d+)\],""crop"":\[(?<left>\d+),(?<top>\d+),(?<right>\d+),(?<bottom>\d+)\]\}$");

		class ShpRemasteredFrame : ISpriteFrame
		{
			public SpriteFrameType Type { get { return SpriteFrameType.BGRA; } }
			public Size Size { get; private set; }
			public Size FrameSize { get; private set; }
			public float2 Offset { get; private set; }
			public byte[] Data { get; private set; }
			public bool DisableExportPadding { get { return false; } }

			public ShpRemasteredFrame()
			{
				Data = new byte[0];
			}

			int ParseGroup(Match match, string group)
			{
				return int.Parse(match.Groups[group].Value);
			}

			public ShpRemasteredFrame(Stream tgaStream, Stream metaStream)
			{
				// TGA Header
				var idLength = tgaStream.ReadUInt8();
				var colorMapType = tgaStream.ReadUInt8();
				var imageType = tgaStream.ReadUInt8();

				// Color map specification
				tgaStream.ReadUInt16();
				var colorMapLength = tgaStream.ReadUInt16();
				tgaStream.ReadUInt8();

				// Image specification
				tgaStream.ReadUInt16();
				tgaStream.ReadUInt16();
				var width = tgaStream.ReadUInt16();
				var height = tgaStream.ReadUInt16();
				var bits = tgaStream.ReadUInt8();
				tgaStream.ReadUInt8();

				if (colorMapType != 0 || imageType != 2 || colorMapLength != 0 || bits != 32)
					throw new NotImplementedException("ShpRemasteredFrame only supports 32 bit uncompressed true-color tga.");

				// Skip image id and color map
				tgaStream.ReadBytes(idLength + colorMapLength);

				// Swap RGBA to BGRA and flip Y axis
				// PERF: Read compressed data into a buffer before moving bytes around
				Size = new Size(width, height);
				Data = new byte[width * height * 4];

				var k = 0;
				var data = tgaStream.ReadBytes(Data.Length);
				for (var j = 0; j < height; j++)
				{
					for (var i = 0; i < width; i++)
					{
						var o = 4 * (width * (height - j - 1) + i);
						Data[o + 2] = data[k++];
						Data[o + 1] = data[k++];
						Data[o + 0] = data[k++];
						Data[o + 3] = data[k++];
					}
				}

				if (metaStream != null)
				{
					var meta = MetaRegex.Match(metaStream.ReadAllText());
					var crop = Rectangle.FromLTRB(
						ParseGroup(meta, "left"), ParseGroup(meta, "top"),
						ParseGroup(meta, "right"), ParseGroup(meta, "bottom"));

					FrameSize = new Size(ParseGroup(meta, "width"), ParseGroup(meta, "height"));
					Offset = 0.5f * new float2(crop.Left + crop.Right - FrameSize.Width, crop.Top + crop.Bottom - FrameSize.Height);
				}
			}
		}

		public IReadOnlyList<ISpriteFrame> Frames { get; private set; }

		public ShpRemasteredSprite(Stream stream)
		{
			var container = new ZipFile(stream);

			string framePrefix = null;
			var frameCount = 0;
			foreach (ZipEntry entry in container)
			{
				var match = FilenameRegex.Match(entry.Name);
				if (!match.Success)
					continue;

				var prefix = match.Groups["prefix"].Value;
				if (framePrefix == null)
					framePrefix = prefix;

				if (prefix != framePrefix)
					throw new InvalidDataException("Frame prefix mismatch: `{0}` != `{1}`".F(prefix, framePrefix));

				frameCount = Math.Max(frameCount, int.Parse(match.Groups["frame"].Value) + 1);
			}

			var frames = new ISpriteFrame[frameCount];
			for (var i = 0; i < frames.Length; i++)
			{
				var tgaEntry = container.GetEntry("{0}{1:D4}.tga".F(framePrefix, i));

				// Blank frame
				if (tgaEntry == null)
				{
					frames[i] = new ShpRemasteredFrame();
					continue;
				}

				var metaEntry = container.GetEntry("{0}{1:D4}.meta".F(framePrefix, i));
				using (var tgaStream = container.GetInputStream(tgaEntry))
				{
					var metaStream = metaEntry != null ? container.GetInputStream(metaEntry) : null;
					frames[i] = new ShpRemasteredFrame(tgaStream, metaStream);
					metaStream?.Dispose();
				}
			}

			Frames = frames.AsReadOnly();
		}
	}
}
