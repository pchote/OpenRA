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
using System.Linq;
using OpenRA.FileFormats;
using OpenRA.Graphics;

namespace OpenRA.Traits
{
	public class ResourceLayerInfo : TraitInfo<ResourceLayer>, Requires<ResourceTypeInfo> { }

	public class ResourceLayer : IRenderOverlay, IWorldLoaded, ITickRender
	{
		static readonly CellContents EmptyCell = new CellContents();

		protected World world;
		protected CellContents[] content;
		protected CellContents[] render;
		List<MapCell> dirty;

		public void Render(WorldRenderer wr)
		{
			var clip = wr.Viewport.CellBounds;
			for (var j = clip.Top; j < clip.Bottom; j++)
			{
				for (var i = clip.Left; i < clip.Right; i++)
				{
					var mc = new MapCell(world.Map, i, j);
					var cell = mc.Location;
					if (world.ShroudObscures(cell))
						continue;

					var c = render[mc.Index];
					if (c.Sprite != null)
						new SpriteRenderable(c.Sprite, wr.world.CenterOfCell(cell),
							WVec.Zero, -511, c.Type.Palette, 1f, true).Render(wr);
				}
			}
		}

		int GetAdjacentCellsWith(MapCell mc, ResourceType t)
		{
			var sum = 0;
			for (var v = -1; v <= 1; v++)
				for (var u = -1; u <= 1; u++)
					if (content[mc.WithOffset(u, v).Index].Type == t)
						++sum;
			return sum;
		}

		public void WorldLoaded(World w, WorldRenderer wr)
		{
			this.world = w;
			var s = w.Map.Size;
			content = new CellContents[s.Width * s.Height];
			render = new CellContents[s.Width * s.Height];
			dirty = new List<MapCell>();

			var resources = w.WorldActor.TraitsImplementing<ResourceType>()
				.ToDictionary(r => r.Info.ResourceType, r => r);

			for (var j = 0; j < s.Height; j++)
			{
				for (var i = 0; i < s.Width; i++)
				{
					var mc = new MapCell(world.Map, i, j);
					var cell = mc.Location;

					ResourceType t;
					if (!resources.TryGetValue(mc.Resource.Type, out t))
						continue;

					if (!AllowResourceAt(t, cell))
						continue;

					content[mc.Index] = CreateResourceCell(t, mc);
				}
			}

			// Set initial density based on the number of neighboring resources
			for (var j = 0; j < s.Height; j++)
			{
				for (var i = 0; i < s.Width; i++)
				{
					var mc = new MapCell(w.Map, i, j);

					var type = content[mc.Index].Type;
					if (type != null)
					{
						// Adjacent includes the current cell, so is always >= 1
						var adjacent = GetAdjacentCellsWith(mc, type);
						var density = int2.Lerp(0, type.Info.MaxDensity, adjacent, 9);
						content[mc.Index].Density = density;

						render[mc.Index] = content[mc.Index];
						UpdateRenderedSprite(mc);
					}
				}
			}
		}

		protected virtual void UpdateRenderedSprite(MapCell mc)
		{
			var t = render[mc.Index];

			if (t.Density > 0)
			{
				var sprites = t.Type.Variants[t.Variant];
				var frame = int2.Lerp(0, sprites.Length - 1, t.Density - 1, t.Type.Info.MaxDensity);
				t.Sprite = sprites[frame];
			}
			else
				t.Sprite = null;

			render[mc.Index] = t;
		}

		protected virtual string ChooseRandomVariant(ResourceType t)
		{
			return t.Variants.Keys.Random(Game.CosmeticRandom);
		}

		public void TickRender(WorldRenderer wr, Actor self)
		{
			var remove = new List<MapCell>();
			foreach (var mc in dirty)
			{
				if (!self.World.FogObscures(mc.Location))
				{
					render[mc.Index] = content[mc.Index];
					UpdateRenderedSprite(mc);
					remove.Add(mc);
				}
			}

			foreach (var r in remove)
				dirty.Remove(r);
		}

		public bool AllowResourceAt(ResourceType rt, CPos a)
		{
			if (!world.Map.IsInMap(a))
				return false;

			if (!rt.Info.AllowedTerrainTypes.Contains(world.GetTerrainInfo(a).Type))
				return false;

			if (!rt.Info.AllowUnderActors && world.ActorMap.AnyUnitsAt(a))
				return false;

			return true;
		}

		CellContents CreateResourceCell(ResourceType t, MapCell mc)
		{
			world.Map.CustomTerrain[mc.U, mc.V] = t.Info.TerrainType;
			return new CellContents
			{
				Type = t,
				Variant = ChooseRandomVariant(t),
			};
		}

		public void AddResource(ResourceType t, CPos p, int n)
		{
			var mc = new MapCell(world.Map, p);
			var cell = content[mc.Index];
			if (cell.Type == null)
				cell = CreateResourceCell(t, mc);

			if (cell.Type != t)
				return;

			cell.Density = Math.Min(cell.Type.Info.MaxDensity, cell.Density + n);
			content[mc.Index] = cell;

			if (!dirty.Contains(mc))
				dirty.Add(mc);
		}

		public bool IsFull(CPos c)
		{
			var mc = new MapCell(world.Map, c);
			return content[mc.Index].Density == content[mc.Index].Type.Info.MaxDensity;
		}

		public ResourceType Harvest(CPos c)
		{
			var mc = new MapCell(world.Map, c);
			var type = content[mc.Index].Type;
			if (type == null)
				return null;

			if (--content[mc.Index].Density < 0)
			{
				content[mc.Index] = EmptyCell;
				world.Map.CustomTerrain[mc.U, mc.V] = null;
			}

			if (!dirty.Contains(mc))
				dirty.Add(mc);

			return type;
		}

		public void Destroy(CPos c)
		{
			var mc = new MapCell(world.Map, c);

			// Don't break other users of CustomTerrain if there are no resources
			if (content[mc.Index].Type == null)
				return;

			// Clear cell
			content[mc.Index] = EmptyCell;
			world.Map.CustomTerrain[mc.U, mc.V] = null;

			if (!dirty.Contains(mc))
				dirty.Add(mc);
		}

		public ResourceType GetResource(CPos c) { return content[new MapCell(world.Map, c).Index].Type; }
		public ResourceType GetRenderedResource(CPos c) { return render[new MapCell(world.Map, c).Index].Type; }
		public int GetResourceDensity(CPos c) { return content[new MapCell(world.Map, c).Index].Density; }
		public int GetMaxResourceDensity(CPos c)
		{
			var i = new MapCell(world.Map, c).Index;
			if (content[i].Type == null)
				return 0;

			return content[i].Type.Info.MaxDensity;
		}

		public struct CellContents
		{
			public ResourceType Type;
			public int Density;
			public string Variant;
			public Sprite Sprite;
		}
	}
}
