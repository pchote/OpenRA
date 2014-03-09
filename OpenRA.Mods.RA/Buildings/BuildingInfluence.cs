#region Copyright & License Information
/*
 * Copyright 2007-2014 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System.Linq;
using OpenRA.Traits;

namespace OpenRA.Mods.RA.Buildings
{
	public class BuildingInfluenceInfo : ITraitInfo
	{
		public object Create(ActorInitializer init) { return new BuildingInfluence(init.world); }
	}

	public class BuildingInfluence
	{
		Map map;
		Actor[] influence;

		public BuildingInfluence(World world)
		{
			map = world.Map;
			var s = map.Size;

			influence = new Actor[s.Width * s.Height];

			world.ActorAdded +=	a =>
			{
				var b = a.TraitOrDefault<Building>();
				if (b == null)
					return;

				var cells = FootprintUtils.Tiles(a.Info.Name, b.Info, a.Location)
					.Select(u => new MapCell(map, u));

				foreach (var mc in cells)
					if (mc.IsInMap && influence[mc.Index] == null)
						influence[mc.Index] = a;
			};

			world.ActorRemoved += a =>
			{
				var b = a.TraitOrDefault<Building>();
				if (b == null)
					return;

				var cells = FootprintUtils.Tiles(a.Info.Name, b.Info, a.Location)
					.Select(u => new MapCell(map, u));

				foreach (var mc in cells)
					if (mc.IsInMap && influence[mc.Index] == a)
						influence[mc.Index] = null;
			};
		}

		public Actor GetBuildingAt(CPos c)
		{
			var mc = new MapCell(map, c);
			if (!mc.IsInMap)
				return null;

			return influence[mc.Index];
		}
	}
}
