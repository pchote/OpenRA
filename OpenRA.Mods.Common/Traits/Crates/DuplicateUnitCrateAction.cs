#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Creates duplicates of the actor that collects the crate.")]
	sealed class DuplicateUnitCrateActionInfo : CrateActionInfo
	{
		[Desc("The maximum number of duplicates to make.")]
		public readonly int MaxAmount = 2;

		[Desc("The minimum number of duplicates to make. Overrules MaxDuplicatesWorth.")]
		public readonly int MinAmount = 1;

		[Desc("The maximum total value allowed for the duplicates.", "Duplication stops if the total worth will exceed this number.", "-1 = no limit")]
		public readonly int MaxDuplicateValue = -1;

		[Desc("The maximum radius (in cells) that duplicates can be spawned.")]
		public readonly int MaxRadius = 4;

		[Desc("The list of unit target types we are allowed to duplicate.")]
		public readonly BitSet<TargetableType> ValidTargets = new("Ground", "Water");

		[Desc("Which factions this crate action can occur for.")]
		public readonly HashSet<string> ValidFactions = [];

		[Desc("Is the new duplicates given to a specific owner, regardless of whom collected it?")]
		public readonly string Owner = null;

		public override object Create(ActorInitializer init) { return new DuplicateUnitCrateAction(init.Self, this); }
	}

	sealed class DuplicateUnitCrateAction : CrateAction
	{
		readonly DuplicateUnitCrateActionInfo info;

		public DuplicateUnitCrateAction(Actor self, DuplicateUnitCrateActionInfo info)
			: base(self, info)
		{
			this.info = info;
		}

		public bool CanGiveTo(Actor collector)
		{
			if (collector.Owner.NonCombatant)
				return false;

			if (info.ValidFactions.Count > 0 && !info.ValidFactions.Contains(collector.Owner.Faction.InternalName))
				return false;

			if (!info.ValidTargets.Overlaps(collector.GetEnabledTargetTypes()))
				return false;

			if (collector.OccupiesSpace is not IPositionable positionable)
				return false;

			return collector.World.Map.FindTilesInCircle(collector.Location, info.MaxRadius)
				.Any(c => positionable.CanEnterCell(c));
		}

		public override int GetSelectionShares(Actor collector)
		{
			if (!CanGiveTo(collector))
				return 0;

			return base.GetSelectionShares(collector);
		}

		public override void Activate(Actor collector)
		{
			var positionable = collector.OccupiesSpace as IPositionable;
			collector.World.AddFrameEndTask(w =>
			{
				var candidateCells = collector.World.Map.FindTilesInCircle(collector.Location, info.MaxRadius)
					.Where(c => positionable.CanEnterCell(c));

				var pathFinder = w.WorldActor.TraitOrDefault<IPathFinder>();
				if (pathFinder != null)
				{
					var actorRules = w.Map.Rules.Actors[collector.Info.Name];
					var locomotorName = actorRules.TraitInfoOrDefault<MobileInfo>()?.Locomotor;
					if (locomotorName != null)
					{
						var locomotor = w.WorldActor.TraitsImplementing<Locomotor>().Single(l => l.Info.Name == locomotorName);
						candidateCells = candidateCells
							.Where(c => pathFinder.PathMightExistForLocomotorBlockedByImmovable(locomotor, c, collector.Location));
					}
				}

				var shuffledCandidateCells = candidateCells
					.Shuffle(collector.World.SharedRandom)
					.ToArray();

				var duplicates = Math.Min(shuffledCandidateCells.Length, info.MaxAmount);

				// Restrict duplicate count to a maximum value
				if (info.MaxDuplicateValue > 0)
				{
					var vi = collector.Info.TraitInfoOrDefault<ValuedInfo>();
					if (vi != null && vi.Cost > 0)
						duplicates = Math.Min(duplicates, info.MaxDuplicateValue / vi.Cost);
				}

				for (var i = 0; i < duplicates; i++)
				{
					var actor = w.CreateActor(collector.Info.Name,
					[
						new LocationInit(shuffledCandidateCells[i]),
						new OwnerInit(info.Owner ?? collector.Owner.InternalName)
					]);

					// Set the subcell and make sure to crush actors beneath.
					var positionable = actor.OccupiesSpace as IPositionable;
					positionable.SetPosition(actor, actor.Location, positionable.GetAvailableSubCell(actor.Location, ignoreActor: actor));
				}
			});

			base.Activate(collector);
		}
	}
}
