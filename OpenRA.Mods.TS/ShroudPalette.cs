#region Copyright & License Information
/*
 * Copyright 2007-2011 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System;
using System.Drawing;
using OpenRA.FileFormats;
using OpenRA.Graphics;
using OpenRA.Traits;

namespace OpenRA.Mods.TS
{
	[Desc("Adds the hard-coded shroud palette to the game")]
	class TSShroudPaletteInfo : ITraitInfo
	{
		[Desc("Internal palette name")]
		public readonly string Name = "shroud";

		[Desc("Palette type")]
		public readonly bool Fog = false;

		public object Create(ActorInitializer init) { return new TSShroudPalette(this); }
	}

	class TSShroudPalette : IPalette
	{
		readonly TSShroudPaletteInfo info;

		public TSShroudPalette(TSShroudPaletteInfo info) { this.info = info; }

		public void InitPalette(WorldRenderer wr)
		{
			Func<int, uint> makeColor = i =>
			{
				if (i >= 64 && i < 128)
					return (uint)(int2.Lerp(info.Fog ? 128 : 256, 0, i - 64, 127 - 64) << 24);
				return 0;
			};

			wr.AddPalette(info.Name, new Palette(Exts.MakeArray(256, i => makeColor(i))), false);
		}
	}
}
