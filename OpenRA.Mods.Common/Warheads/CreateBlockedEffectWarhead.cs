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
using OpenRA.Mods.Common.Effects;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Warheads
{
	[Desc("Displays a custom effect when the projectile is blocked by another actor.")]
	public class CreateBlockedEffectWarhead : CreateEffectWarhead
	{
		[Desc("Create the effect when blocked by these types")]
		public readonly BitSet<ProjectileBlockingType> BlockingTypes = new BitSet<ProjectileBlockingType>("wall");

		public override void DoImpact(Target target, Actor firedBy, IEnumerable<int> damageModifiers)
		{
			// Do nothing: this warhead is not triggered during normal impacts
		}

		public void DoBlockedImpact(WPos pos, Actor firedBy, BitSet<ProjectileBlockingType> blockingTypes)
		{
			if (BlockingTypes.Overlaps(blockingTypes))
				base.DoImpact(Target.FromPos(pos), firedBy, Enumerable.Empty<int>());
		}
	}
}
