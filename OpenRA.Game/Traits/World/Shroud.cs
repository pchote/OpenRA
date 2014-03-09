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
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace OpenRA.Traits
{
	public class ShroudInfo : ITraitInfo
	{
		public object Create(ActorInitializer init) { return new Shroud(init.self); }
	}

	public class Shroud
	{
		[Sync] public bool Disabled = false;

		Actor self;
		Map map;

		int[] visibleCount;
		int[] generatedShroudCount;
		bool[] explored;

		// Cache of visibility that was added, so no matter what crazy trait code does, it
		// can't make us invalid.
		Dictionary<Actor, MapCell[]> visibility = new Dictionary<Actor, MapCell[]>();
		Dictionary<Actor, MapCell[]> generation = new Dictionary<Actor, MapCell[]>();

		public Rectangle ExploredBounds { get; private set; }

		public int Hash { get; private set; }

		public Shroud(Actor self)
		{
			this.self = self;
			map = self.World.Map;
			var s = map.Size;
			visibleCount = new int[s.Width * s.Height];
			generatedShroudCount = new int[s.Width * s.Height];
			explored = new bool[s.Width * s.Height];

			self.World.ActorAdded += AddVisibility;
			self.World.ActorRemoved += RemoveVisibility;

			self.World.ActorAdded += AddShroudGeneration;
			self.World.ActorRemoved += RemoveShroudGeneration;

			if (!self.World.LobbyInfo.GlobalSettings.Shroud)
				ExploredBounds = map.Bounds;
		}

		void Invalidate()
		{
			Hash = Sync.hash_player(self.Owner) + self.World.WorldTick * 3;
		}

		static IEnumerable<MapCell> FindVisibleTiles(World world, CPos c, WRange r)
		{
			var cr = (r.Range + 1023) / 1024;
			var rSq = r.Range * r.Range;
			var pos = world.CenterOfCell(c);

			for (var j = -cr; j <= cr; j++)
			{
				for (var i = -cr; i <= cr; i++)
				{
					var n = new CPos(c.X + i, c.Y + j);

					// Exclude cells outside the visible range
					var dp = world.CenterOfCell(n) - pos;
					if (dp.HorizontalLengthSquared > rSq)
						continue;

					// Exclude cells outside the map
					var mc = new MapCell(world.Map, n);
					if (mc.IsInMap)
						yield return mc;
				}
			}
		}

		void AddVisibility(Actor a)
		{
			var rs = a.TraitOrDefault<RevealsShroud>();
			if (rs == null || !a.Owner.IsAlliedWith(self.Owner) || rs.Range == WRange.Zero)
				return;

			var origins = GetVisOrigins(a);
			var visible = origins.SelectMany(o => FindVisibleTiles(a.World, o, rs.Range))
				.Distinct()
				.ToArray();

			// Update bounding rect
			var r = (rs.Range.Range + 1023) / 1024;

			foreach (var o in origins.Select(c => new MapCell(map, c)))
			{
				var box = new Rectangle(o.U - r, o.V - r, 2 * r + 1, 2 * r + 1);
				ExploredBounds = Rectangle.Union(ExploredBounds, box);
			}

			// Update visibility
			foreach (var c in visible)
			{
				visibleCount[c.Index]++;
				explored[c.Index] = true;
			}

			if (visibility.ContainsKey(a))
				throw new InvalidOperationException("Attempting to add duplicate actor visibility");

			visibility[a] = visible;
			Invalidate();
		}

		void RemoveVisibility(Actor a)
		{
			MapCell[] visible;
			if (!visibility.TryGetValue(a, out visible))
				return;

			foreach (var c in visible)
				visibleCount[c.Index]--;

			visibility.Remove(a);
			Invalidate();
		}

		public void UpdateVisibility(Actor a)
		{
			// Actors outside the world don't have any vis
			if (!a.IsInWorld)
				return;

			RemoveVisibility(a);
			AddVisibility(a);
		}

		void AddShroudGeneration(Actor a)
		{
			var cs = a.TraitOrDefault<CreatesShroud>();
			if (cs == null || a.Owner.IsAlliedWith(self.Owner) || cs.Range == WRange.Zero)
				return;

			var shrouded = GetVisOrigins(a).SelectMany(o => FindVisibleTiles(a.World, o, cs.Range))
				.Distinct()
				.ToArray();

			foreach (var c in shrouded)
				generatedShroudCount[c.Index]++;

			if (generation.ContainsKey(a))
				throw new InvalidOperationException("Attempting to add duplicate shroud generation");

			generation[a] = shrouded;
			Invalidate();
		}

		void RemoveShroudGeneration(Actor a)
		{
			MapCell[] shrouded;
			if (!generation.TryGetValue(a, out shrouded))
				return;

			foreach (var c in shrouded)
				generatedShroudCount[c.Index]--;

			generation.Remove(a);
			Invalidate();
		}

		public void UpdateShroudGeneration(Actor a)
		{
			RemoveShroudGeneration(a);
			AddShroudGeneration(a);
		}

		public void UpdatePlayerStance(World w, Player player, Stance oldStance, Stance newStance)
		{
			if (oldStance == newStance)
				return;

			foreach (var a in w.Actors.Where(a => a.Owner == player))
			{
				UpdateVisibility(a);
				UpdateShroudGeneration(a);
			}
		}

		public static IEnumerable<CPos> GetVisOrigins(Actor a)
		{
			var ios = a.OccupiesSpace;
			if (ios != null)
			{
				var cells = ios.OccupiedCells();
				if (cells.Any())
					return cells.Select(c => c.First);
			}

			return new[] { a.World.CellContaining(a.CenterPosition) };
		}

		public void Explore(World world, CPos center, WRange range)
		{
			foreach (var q in FindVisibleTiles(world, center, range))
				explored[q.Index] = true;

			var r = (range.Range + 1023) / 1024;
			var foo = new MapCell(map, center);
			var box = new Rectangle(foo.U - r, foo.V - r, 2 * r + 1, 2 * r + 1);
			ExploredBounds = Rectangle.Union(ExploredBounds, box);

			Invalidate();
		}

		public void Explore(Shroud s)
		{
			for (var i = 0; i < s.explored.Length; i++)
				if (s.explored[i])
					explored[i] = true;

			ExploredBounds = Rectangle.Union(ExploredBounds, s.ExploredBounds);
		}

		public void ExploreAll(World world)
		{
			var b = map.Bounds;
			var stride = map.Size.Width;
			for (var j = b.Top; j < b.Bottom; j++)
				for (var i = b.Left; i < b.Right; i++)
					explored[i + j * stride] = true;

			ExploredBounds = world.Map.Bounds;

			Invalidate();
		}

		public void ResetExploration()
		{
			for (var i = 0; i < explored.Length; i++)
				explored[i] = visibleCount[i] > 0;

			Invalidate();
		}

		public bool IsExplored(CPos xy)
		{
			var mc = new MapCell(map, xy);
			return IsExplored(mc.U, mc.V);
		}

		public bool IsExplored(int u, int v)
		{
			var mc = new MapCell(map, u, v);
			if (!mc.IsInMap)
				return false;

			if (Disabled || !self.World.LobbyInfo.GlobalSettings.Shroud)
				return true;

			return explored[mc.Index] && (generatedShroudCount[mc.Index] == 0 || visibleCount[mc.Index] > 0);
		}

		public bool IsExplored(Actor a)
		{
			return GetVisOrigins(a).Any(o => IsExplored(o));
		}

		public bool IsVisible(CPos xy)
		{
			var mc = new MapCell(map, xy);
			return IsVisible(mc.U, mc.V);
		}

		public bool IsVisible(int u, int v)
		{
			var mc = new MapCell(map, u, v);
			if (!mc.IsInMap)
				return false;

			if (Disabled || !self.World.LobbyInfo.GlobalSettings.Fog)
				return true;

			return visibleCount[mc.Index] > 0;
		}

		// Actors are hidden under shroud, but not under fog by default
		public bool IsVisible(Actor a)
		{
			if (a.TraitsImplementing<IVisibilityModifier>().Any(t => !t.IsVisible(a, self.Owner)))
				return false;

			return a.Owner.IsAlliedWith(self.Owner) || IsExplored(a);
		}

		public bool IsTargetable(Actor a)
		{
			if (a.TraitsImplementing<IVisibilityModifier>().Any(t => !t.IsVisible(a, self.Owner)))
				return false;

			return GetVisOrigins(a).Any(o => IsVisible(o));
		}
	}
}
