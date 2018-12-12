#region Copyright & License Information
/*
 * Copyright 2007-2018 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System.Collections.Generic;
using System.Linq;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	public sealed class ProjectileBlockingType { ProjectileBlockingType() { } }

	[Desc("This actor blocks bullets and missiles with 'Blockable' property.")]
	public class BlocksProjectilesInfo : ConditionalTraitInfo, IBlocksProjectilesInfo
	{
		[Desc("Block projectiles with overlapping BlockingTypes.")]
		public readonly BitSet<ProjectileBlockingType> Types = default(BitSet<ProjectileBlockingType>);

		[Desc("Projectile owner stances to block.")]
		public readonly Stance Stances = Stance.Ally | Stance.Neutral | Stance.Enemy;

		public readonly WDist Height = WDist.FromCells(1);

		public override object Create(ActorInitializer init) { return new BlocksProjectiles(init.Self, this); }
	}

	public class BlocksProjectiles : ConditionalTrait<BlocksProjectilesInfo>, IBlocksProjectiles
	{
		public BlocksProjectiles(Actor self, BlocksProjectilesInfo info)
			: base(info) { }

		WDist IBlocksProjectiles.BlockingHeight { get { return Info.Height; } }
		Stance IBlocksProjectiles.BlockingStances { get { return Info.Stances; } }
		BitSet<ProjectileBlockingType> IBlocksProjectiles.BlockingTypes { get { return Info.Types; } }

		public static IBlocksProjectiles FirstBlockerOnLineOrDefault(World world, Player owner, BitSet<ProjectileBlockingType> types,
			WPos start, WPos end, WDist width, out WPos blockerPos)
		{
			var actors = world.FindBlockingActorsOnLine(start, end, width);
			var length = (end - start).Length;

			IBlocksProjectiles blocker = null;
			int blockerRange = 0;
			blockerPos = WPos.Zero;

			foreach (var a in actors)
			{
				var stance = owner.Stances[a.Owner];
				var blockers = a.TraitsImplementing<IBlocksProjectiles>()
					.Where(t => t.IsTraitEnabled() && t.BlockingStances.HasStance(stance) && t.BlockingTypes.Overlaps(types));

				if (!blockers.Any())
					continue;

				var hitPos = WorldExtensions.MinimumPointLineProjection(start, end, a.CenterPosition);
				var dat = world.Map.DistanceAboveTerrain(hitPos);

				var range = (hitPos - start).Length;
				if (range >= length || (blocker != null && range > blockerRange))
					continue;

				var testBlocker = blockers.FirstOrDefault(b => b.BlockingHeight > dat);
				if (testBlocker != null)
				{
					blocker = testBlocker;
					blockerPos = hitPos;
					blockerRange = range;
				}
			}

			return blocker;
		}
	}
}
