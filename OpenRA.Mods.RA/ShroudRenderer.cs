#region Copyright & License Information
/*
 * Copyright 2007-2013 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Traits;

namespace OpenRA.Mods.RA
{
	public class ShroudRendererInfo : ITraitInfo
	{
		public string Sequence = "shroud";
		public string[] Variants = new[] { "shroud" };

		[Desc("Bitfield of shroud directions for each frame. Lower four bits are",
		      "corners clockwise from TL; upper four are edges clockwise from top")]
		public int[] Index = new[] { 12, 9, 8, 3, 1, 6, 4, 2, 13, 11, 7, 14 };

		[Desc("Use the upper four bits when calculating frame")]
		public bool UseExtendedIndex = false;

		[Desc("Palette index for synthesized unexplored tile")]
		public int ShroudColor = 12;
		public BlendMode ShroudBlend = BlendMode.Alpha;
		public object Create(ActorInitializer init) { return new ShroudRenderer(init.world, this); }
	}

	public class ShroudRenderer : IRenderShroud, IWorldLoaded
	{
		struct ShroudTile
		{
			public CPos Position;
			public float2 ScreenPosition;
			public int Variant;

			public Sprite Fog;
			public Sprite Shroud;
		}

		Sprite[] sprites;
		Sprite unexploredTile;
		int[] spriteMap;

		ShroudTile[] tiles;
		int tileStride, variantStride;

		int shroudHash;
		PaletteReference fogPalette, shroudPalette;
		Rectangle bounds;
		bool useExtendedIndex;

		public ShroudRenderer(World world, ShroudRendererInfo info)
		{
			var map = world.Map;
			bounds = map.Bounds;
			useExtendedIndex = info.UseExtendedIndex;

			tiles = new ShroudTile[map.Size.Width * map.Size.Height];
			tileStride = map.Size.Width;

			// Force update on first render
			shroudHash = -1;

			// Load sprite variants
			sprites = new Sprite[info.Variants.Length * info.Index.Length];
			variantStride = info.Index.Length;
			for (var j = 0; j < info.Variants.Length; j++)
			{
				var seq = SequenceProvider.GetSequence(info.Sequence, info.Variants[j]);
				for (var i = 0; i < info.Index.Length; i++)
					sprites[j * variantStride + i] = seq.GetSprite(i);
			}

			// Mapping of shrouded directions -> sprite index
			spriteMap = new int[useExtendedIndex ? 256 : 16];
			for (var i = 0; i < info.Index.Length; i++)
			{
				/*
				var ha = info.Index[i] & 0x80;
				var hb = info.Index[i] & 0x40;
				var hc = info.Index[i] & 0x20;
				var hd = info.Index[i] & 0x10;
				var la = info.Index[i] & 0x8;
				var lb = info.Index[i] & 0x4;
				var lc = info.Index[i] & 0x2;
				var ld = info.Index[i] & 0x1;
				//var index = (ha >> 3) | (hb >> 1) | (hc << 1) | (hd << 3) | la | lb | lc | ld;
				//Console.WriteLine("{0} -> {1} {2} {3} {4} {5} {6} {7} {8} -> {9}", info.Index[i], ha, hb, hc, hd, la, lb, lc, ld, index);
				Console.Write(", {0}", index);
				*/
				spriteMap[info.Index[i]] = i;
			}
			// Set individual tile variants to reduce tiling
			for (var i = 0; i < tiles.Length; i++)
				tiles[i].Variant = Game.CosmeticRandom.Next(info.Variants.Length);

			// Synthesize unexplored tile if it isn't defined
			if (!info.Index.Contains(240))
			{
				var ts = Game.modData.Manifest.TileSize;
				var data = Exts.MakeArray<byte>(ts.Width * ts.Height, _ => (byte)info.ShroudColor);
				var s = Game.modData.SheetBuilder.Add(data, ts);
				unexploredTile = new Sprite(s.sheet, s.bounds, s.offset, s.channel, info.ShroudBlend);
			}
			else
				unexploredTile = sprites[spriteMap[240]];
		}

		static int FoggedEdges(Shroud s, CPos c, bool useExtendedIndex)
		{
			if (!s.IsVisible(c))
				return 15;

			// If a side is shrouded then we also count the corners
			var u = 0;
			if (!s.IsVisible(c + new CVec(0, -1))) u |= 0x13;
			if (!s.IsVisible(c + new CVec(1, 0))) u |= 0x26;
			if (!s.IsVisible(c + new CVec(0, 1))) u |= 0x4C;
			if (!s.IsVisible(c + new CVec(-1, 0))) u |= 0x89;

			var uside = u & 0x0F;
			if (!s.IsVisible(c + new CVec(-1, -1))) u |= 0x01;
			if (!s.IsVisible(c + new CVec(1, -1))) u |= 0x02;
			if (!s.IsVisible(c + new CVec(1, 1))) u |= 0x04;
			if (!s.IsVisible(c + new CVec(-1, 1))) u |= 0x08;

			// RA provides a set of frames for tiles with shrouded
			// corners but unshrouded edges. We want to detect this
			// situation without breaking the edge -> corner enabling
			// in other combinations. The XOR turns off the corner
			// bits that are enabled twice, which gives the behavior
			// we want here.
			return useExtendedIndex ? u ^ uside : u & 0x0F;
		}

		static int ShroudedEdges(Shroud s, CPos c, bool useExtendedIndex)
		{
			if (!s.IsExplored(c))
				return 15;

			// If a side is shrouded then we also count the corners
			var u = 0;
			if (!s.IsExplored(c + new CVec(0, -1))) u |= 0x13;
			if (!s.IsExplored(c + new CVec(1, 0))) u |= 0x26;
			if (!s.IsExplored(c + new CVec(0, 1))) u |= 0x4C;
			if (!s.IsExplored(c + new CVec(-1, 0))) u |= 0x89;

			var uside = u & 0x0F;
			if (!s.IsExplored(c + new CVec(-1, -1))) u |= 0x01;
			if (!s.IsExplored(c + new CVec(1, -1))) u |= 0x02;
			if (!s.IsExplored(c + new CVec(1, 1))) u |= 0x04;
			if (!s.IsExplored(c + new CVec(-1, 1))) u |= 0x08;

			// RA provides a set of frames for tiles with shrouded
			// corners but unshrouded edges. We want to detect this
			// situation without breaking the edge -> corner enabling
			// in other combinations. The XOR turns off the corner
			// bits that are enabled twice, which gives the behavior
			// we want here.
			return useExtendedIndex ? u ^ uside : u & 0x0F;
		}

		static int ObserverShroudedEdges(Map map, CPos c, Rectangle bounds, bool useExtendedIndex)
		{
			var mc = new MapCell(map, c);
			var u = 0;
			if (mc.V == bounds.Top) u |= 0x13;
			if (mc.U == bounds.Right - 1) u |= 0x26;
			if (mc.V == bounds.Bottom - 1) u |= 0x4C;
			if (mc.U == bounds.Left) u |= 0x89;

			var uside = u & 0x0F;
			if (mc.U == bounds.Left && mc.V == bounds.Top) u |= 0x01;
			if (mc.U == bounds.Right - 1 && mc.V == bounds.Top) u |= 0x02;
			if (mc.U == bounds.Right - 1 && mc.V == bounds.Bottom - 1) u |= 0x04;
			if (mc.U == bounds.Left && mc.V == bounds.Bottom - 1) u |= 0x08;

			return useExtendedIndex ? u ^ uside : u & 0x0F;
		}

		public void WorldLoaded(World w, WorldRenderer wr)
		{
			// Cache the tile positions to avoid unnecessary calculations
			for (var i = bounds.Left; i < bounds.Right; i++)
			{
				for (var j = bounds.Top; j < bounds.Bottom; j++)
				{
					var k = j * tileStride + i;
					tiles[k].Position = new MapCell(w.Map, i, j).Location;
					tiles[k].ScreenPosition = wr.ScreenPosition(w.CenterOfCell(tiles[k].Position));
				}
			}

			fogPalette = wr.Palette("fog");
			shroudPalette = wr.Palette("shroud");
		}

		Sprite GetTile(int flags, int variant)
		{
			if (flags == 0)
				return null;

			if (flags == 15)
				return unexploredTile;

			return sprites[variant * variantStride + spriteMap[flags]];
		}

		void Update(Map map, Shroud shroud)
		{
			var hash = shroud != null ? shroud.Hash : 0;
			if (shroudHash == hash)
				return;

			shroudHash = hash;
			if (shroud == null)
			{
				// Players with no shroud see the whole map so we only need to set the edges
				for (var k = 0; k < tiles.Length; k++)
				{
					var shrouded = ObserverShroudedEdges(map, tiles[k].Position, bounds, useExtendedIndex);
					tiles[k].Shroud = GetTile(shrouded, tiles[k].Variant);
					tiles[k].Fog = GetTile(shrouded, tiles[k].Variant);
				}
			}
			else
			{
				for (var k = 0; k < tiles.Length; k++)
				{
					var shrouded = ShroudedEdges(shroud, tiles[k].Position, useExtendedIndex);
					var fogged = FoggedEdges(shroud, tiles[k].Position, useExtendedIndex);

					tiles[k].Shroud = GetTile(shrouded, tiles[k].Variant);
					tiles[k].Fog = GetTile(fogged, tiles[k].Variant);
				}
			}
		}

		public void RenderShroud(WorldRenderer wr, Shroud shroud)
		{
			Update(wr.world.Map, shroud);

			var clip = wr.Viewport.CellBounds;
			var width = clip.Width;

			for (var j = clip.Top; j < clip.Bottom; j++)
			{
				var start = j * tileStride + clip.Left;
				for (var k = 0; k < width; k++)
				{
					var s = tiles[start + k].Shroud;
					var f = tiles[start + k].Fog;

					if (s != null)
					{
						var pos = tiles[start + k].ScreenPosition - 0.5f * s.size;
						Game.Renderer.WorldSpriteRenderer.DrawSprite(s, pos, shroudPalette);
					}

					if (f != null)
					{
						var pos = tiles[start + k].ScreenPosition - 0.5f * f.size;
						Game.Renderer.WorldSpriteRenderer.DrawSprite(f, pos, fogPalette);
					}
				}
			}
		}
	}
}
