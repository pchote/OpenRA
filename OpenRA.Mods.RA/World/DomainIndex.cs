#region Copyright & License Information
/*
 * Copyright 2007-2013 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.RA.Move;
using OpenRA.Support;
using OpenRA.Traits;

namespace OpenRA.Mods.RA
{
	// Identify untraversable regions of the map for faster pathfinding, especially with AI
	class DomainIndexInfo : TraitInfo<DomainIndex> {}

	public class DomainIndex : IWorldLoaded
	{
		Dictionary<uint, MovementClassDomainIndex> domainIndexes;

		public void WorldLoaded(World world, WorldRenderer wr)
		{
			domainIndexes = new Dictionary<uint, MovementClassDomainIndex>();
			var movementClasses = new HashSet<uint>(
				Rules.Info.Where(ai => ai.Value.Traits.Contains<MobileInfo>())
				.Select(ai => (uint)ai.Value.Traits.Get<MobileInfo>().GetMovementClass(world.TileSet)));

			foreach (var mc in movementClasses)
				domainIndexes[mc] = new MovementClassDomainIndex(world, mc);
		}

		public bool IsPassable(CPos p1, CPos p2, uint movementClass)
		{
			return domainIndexes[movementClass].IsPassable(p1, p2);
		}

		/// Regenerate the domain index for a group of cells
		public void UpdateCells(World world, IEnumerable<CPos> cells)
		{
			var dirty = new HashSet<CPos>(cells);
			foreach (var index in domainIndexes)
				index.Value.UpdateCells(world, dirty);
		}
	}

	class MovementClassDomainIndex
	{
		Map map;

		uint movementClass;
		int[] domains;
		Dictionary<int, HashSet<int>> transientConnections;

		// Each terrain has an offset corresponding to its location in a
		// movement class bitmask.  This caches each offset.
		Dictionary<string, int> terrainOffsets;

		public MovementClassDomainIndex(World world, uint movementClass)
		{
			map = world.Map;
			this.movementClass = movementClass;
			domains = new int[map.Size.Width * map.Size.Height];
			transientConnections = new Dictionary<int, HashSet<int>>();

			terrainOffsets = new Dictionary<string, int>();
			var terrains = world.TileSet.Terrain.OrderBy(t => t.Key).ToList();
			foreach (var terrain in terrains)
			{
				var terrainOffset = terrains.FindIndex(x => x.Key == terrain.Key);
				terrainOffsets[terrain.Key] = terrainOffset;
			}

			BuildDomains(world);
		}

		public bool IsPassable(CPos p1, CPos p2)
		{
			var mc1 = new MapCell(map, p1);
			var mc2 = new MapCell(map, p2);

			if (!mc1.IsInMap || !mc2.IsInMap)
				return false;

			if (domains[mc1.Index] == domains[mc2.Index])
				return true;

			// Even though p1 and p2 are in different domains, it's possible
			// that some dynamic terrain (i.e. bridges) may connect them.
			return HasConnection(domains[mc1.Index], domains[mc2.Index]);
		}

		public void UpdateCells(World world, HashSet<CPos> dirtyCells)
		{
			var neighborDomains = new List<int>();
			var dirtyMap = dirtyCells.Select(c => new MapCell(map, c));

			foreach (var mc in dirtyMap)
			{
				// Select all neighbors inside the map boundries
				var neighbors = CVec.directions
					.Select(d => new MapCell(map, d + mc.Location))
					.Where(e => e.IsInMap);

				var found = false;
				foreach (var n in neighbors)
				{
					if (!dirtyMap.Contains(n))
					{
						var nd = domains[n.Index];
						if (CanTraverseTile(world, n))
							neighborDomains.Add(nd);

						// Set ourselves to the first non-dirty neighbor we find.
						if (!found)
						{
							domains[mc.Index] = nd;
							found = true;
						}
					}
				}
			}

			foreach (var c1 in neighborDomains)
				foreach (var c2 in neighborDomains)
					CreateConnection(c1, c2);
		}

		bool HasConnection(int d1, int d2)
		{
			// Search our connections graph for a possible route
			var visited = new HashSet<int>();
			var toProcess = new Stack<int>();
			toProcess.Push(d1);

			while (toProcess.Any())
			{
				var current = toProcess.Pop();
				if (!transientConnections.ContainsKey(current))
					continue;

				foreach (int neighbor in transientConnections[current])
				{
					if (neighbor == d2)
						return true;

					if (!visited.Contains(neighbor))
						toProcess.Push(neighbor);
				}

				visited.Add(current);
			}

			return false;
		}

		void CreateConnection(int d1, int d2)
		{
			if (!transientConnections.ContainsKey(d1))
				transientConnections[d1] = new HashSet<int>();
			if (!transientConnections.ContainsKey(d2))
				transientConnections[d2] = new HashSet<int>();

			transientConnections[d1].Add(d2);
			transientConnections[d2].Add(d1);
		}

		bool CanTraverseTile(World world, MapCell mc)
		{
			var currentTileType = WorldUtils.GetTerrainType(world, mc.Location);
			var terrainOffset = terrainOffsets[currentTileType];
			return (movementClass & (1 << terrainOffset)) > 0;
		}

		void BuildDomains(World world)
		{
			var timer = new Stopwatch();

			var domain = 1;

			var visited = new bool[map.Size.Width * map.Size.Height];

			var toProcess = new Queue<MapCell>();
			toProcess.Enqueue(new MapCell(map, map.Bounds.Left, map.Bounds.Top));

			// Flood-fill over each domain
			while (toProcess.Count != 0)
			{
				var start = toProcess.Dequeue();

				// Technically redundant with the check in the inner loop, but prevents
				// ballooning the domain counter.
				if (visited[start.Index])
					continue;

				var domainQueue = new Queue<MapCell>();
				domainQueue.Enqueue(start);

				var currentPassable = CanTraverseTile(world, start);

				// Add all contiguous cells to our domain, and make a note of
				// any non-contiguous cells for future domains
				while (domainQueue.Count != 0)
				{
					var n = domainQueue.Dequeue();
					if (visited[n.Index])
						continue;

					var candidatePassable = CanTraverseTile(world, n);
					if (candidatePassable != currentPassable)
					{
						toProcess.Enqueue(n);
						continue;
					}

					visited[n.Index] = true;
					domains[n.Index] = domain;

					// Don't crawl off the map, or add already-visited cells
					var neighbors = CVec.directions.Select(d => new MapCell(map, n.Location + d))
						.Where(mc => mc.IsInMap && !visited[mc.Index]);

					foreach (var neighbor in neighbors)
						domainQueue.Enqueue(neighbor);
				}

				domain += 1;
			}

			Log.Write("debug", "{0}: Found {1} domains.  Took {2} s", map.Title, domain-1, timer.ElapsedTime());
		}
	}
}
