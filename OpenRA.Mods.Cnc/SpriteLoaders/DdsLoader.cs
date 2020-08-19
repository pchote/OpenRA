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

using System.IO;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Cnc.FileFormats;
using OpenRA.Primitives;

namespace OpenRA.Mods.Cnc.SpriteLoaders
{
	public class DdsLoader : ISpriteLoader
	{
		public static bool IsDds(Stream s)
		{
			var start = s.Position;
			var isDds = s.ReadUInt32() == 0x20534444;
			s.Position = start;

			return isDds;
		}

		public bool TryParseSprite(Stream s, out ISpriteFrame[] frames, out TypeDictionary metadata)
		{
			metadata = null;
			if (!IsDds(s))
			{
				frames = null;
				return false;
			}

			frames = new DdsSprite(s).Frames.ToArray();
			return true;
		}
	}

	public class DdsSprite
	{
		class DdsFrame : ISpriteFrame
		{
			public SpriteFrameType Type { get { return SpriteFrameType.BGRA; } }
			public Size Size { get; private set; }
			public Size FrameSize { get { return Size; } }
			public float2 Offset { get { return float2.Zero; } }
			public byte[] Data { get; private set; }
			public bool DisableExportPadding { get { return false; } }

			public DdsFrame(Stream stream)
			{
				var dds = new Dds(stream);
				Size = new Size(dds.Width, dds.Height);
				Data = dds.Data;
			}
		}

		public IReadOnlyList<ISpriteFrame> Frames { get; private set; }

		public DdsSprite(Stream stream)
		{
			Frames = new ISpriteFrame[] { new DdsFrame(stream) }.AsReadOnly();
		}
	}
}
