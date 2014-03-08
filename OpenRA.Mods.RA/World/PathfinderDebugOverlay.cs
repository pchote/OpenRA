#region Copyright & License Information
/*
 * Copyright 2007-2014 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Drawing;
using OpenRA.Graphics;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.RA
{
	class PathfinderDebugOverlayInfo : TraitInfo<PathfinderDebugOverlay> { }
	class PathfinderDebugOverlay : IRenderOverlay, IWorldLoaded
	{
		public bool Visible;

		Dictionary<Player, int[]> layers;
		World world;
		int refreshTick;

		public void WorldLoaded(World w, WorldRenderer wr)
		{
			world = w;
			refreshTick = 0;
			layers = new Dictionary<Player, int[]>(8);

			// Enabled via Cheats menu
			Visible = false;
		}

		public void AddLayer(IEnumerable<Pair<CPos, int>> cellWeights, int maxWeight, Player pl)
		{
			if (maxWeight == 0)
				return;

			int[] layer;
			if (!layers.TryGetValue(pl, out layer))
			{
				var s = world.Map.Size;
				layer = new int[s.Width * s.Height];
				layers.Add(pl, layer);
			}

			foreach (var p in cellWeights)
			{
				var mc = new MapCell(world.Map, p.First);
				if (mc.IsInMap)
					layer[mc.Index] = Math.Min(128, layer[mc.Index] + (maxWeight - p.Second) * 64 / maxWeight);
			}
		}

		public void Render(WorldRenderer wr)
		{
			if (!Visible)
				return;

			var qr = Game.Renderer.WorldQuadRenderer;
			var doDim = refreshTick - world.WorldTick <= 0;
			if (doDim) refreshTick = world.WorldTick + 20;

			var clip = wr.Viewport.CellBounds;
			foreach (var pair in layers)
			{
				var c = (pair.Key != null) ? pair.Key.Color.RGB : Color.PaleTurquoise;
				var layer = pair.Value;

				// Only render quads in viewing range:
				for (var j = clip.Top; j <= clip.Bottom; ++j)
				{
					for (var i = clip.Left; i <= clip.Right; ++i)
					{
						var mc = new MapCell(world.Map, i, j);
						if (layer[mc.Index] <= 0)
							continue;

						var w = Math.Max(0, Math.Min(layer[mc.Index], 128));
						if (doDim)
							layer[mc.Index] = layer[mc.Index] * 5 / 6;

						// TODO: This doesn't make sense for isometric terrain
						var pos = world.CenterOfCell(mc.Location);
						var tl = wr.ScreenPxPosition(pos - new WVec(512, 512, 0));
						var br = wr.ScreenPxPosition(pos + new WVec(511, 511, 0));
						qr.FillRect(RectangleF.FromLTRB(tl.X, tl.Y, br.X, br.Y), Color.FromArgb(w, c));
					}
				}
			}
		}
	}
}
