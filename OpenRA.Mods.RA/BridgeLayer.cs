#region Copyright & License Information
/*
 * Copyright 2007-2014 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System.Collections.Generic;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.RA
{
	class BridgeLayerInfo : ITraitInfo
	{
		[ActorReference]
		public readonly string[] Bridges = { "bridge1", "bridge2" };

		public object Create(ActorInitializer init) { return new BridgeLayer(init.self, this); }
	}

	class BridgeLayer : IWorldLoaded
	{
		readonly BridgeLayerInfo info;
		readonly World world;
		Dictionary<ushort, Pair<string, float>> bridgeTypes = new Dictionary<ushort, Pair<string, float>>();
		Bridge[] bridges;

		public BridgeLayer(Actor self, BridgeLayerInfo info)
		{
			this.info = info;
			this.world = self.World;
		}

		public void WorldLoaded(World w, WorldRenderer wr)
		{
			var s = w.Map.Size;
			bridges = new Bridge[s.Width * s.Height];

			// Build a list of templates that should be overlayed with bridges
			foreach (var bridge in info.Bridges)
			{
				var bi = Rules.Info[bridge].Traits.Get<BridgeInfo>();
				foreach (var template in bi.Templates)
					bridgeTypes.Add(template.First, Pair.New(bridge, template.Second));
			}

			// Loop through the map looking for templates to overlay
			for (var j = 0; j < s.Height; j++)
			{
				for (var i = 0; i < s.Width; i++)
				{
					var mc = new MapCell(w.Map, i, j);
					if (bridgeTypes.Keys.Contains(mc.Tile.Type))
						ConvertBridgeToActor(w, mc);
				}
			}

			// Link adjacent (long)-bridges so that artwork is updated correctly
			foreach (var b in w.Actors.SelectMany(a => a.TraitsImplementing<Bridge>()))
				b.LinkNeighbouringBridges(w, this);
		}

		void ConvertBridgeToActor(World w, MapCell mc)
		{
			// This cell already has a bridge overlaying it from a previous iteration
			if (bridges[mc.Index] != null)
				return;

			// Correlate the tile "image" aka subtile with its position to find the template origin
			var tile = mc.Tile.Type;
			var index = mc.Tile.Index;
			var template = w.TileSet.Templates[tile];
			var ni = mc.U - index % template.Size.X;
			var nj = mc.V - index / template.Size.X;

			// Create a new actor for this bridge and keep track of which subtiles this bridge includes
			var bridge = w.CreateActor(bridgeTypes[tile].First, new TypeDictionary
			{
				new LocationInit(new CPos(ni, nj)),
				new OwnerInit(w.WorldActor.Owner),
				new HealthInit(bridgeTypes[tile].Second),
			}).Trait<Bridge>();

			var subTiles = new Dictionary<MapCell, byte>();

			// For each subtile in the template
			for (byte ind = 0; ind < template.Size.X * template.Size.Y; ind++)
			{
				// Where do we expect to find the subtile
				var du = ind % template.Size.X - index % template.Size.X;
				var dv = ind / template.Size.X - index / template.Size.X;
				var smc = mc.WithOffset(du, dv);

				// This isn't the bridge you're looking for
				if (!smc.IsInMap || smc.Tile.Type != tile || smc.Tile.Index != ind)
					continue;

				subTiles.Add(smc, ind);
				bridges[smc.Index] = bridge;
			}

			bridge.Create(tile, subTiles);
		}

		// Used to check for neighbouring bridges
		public Bridge GetBridge(CPos c)
		{
			var mc = new MapCell(world.Map, c);
			if (!mc.IsInMap)
				return null;

			return bridges[mc.Index];
		}
	}
}
