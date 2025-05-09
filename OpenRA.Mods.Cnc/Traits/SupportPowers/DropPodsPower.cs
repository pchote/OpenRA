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
using OpenRA.GameRules;
using OpenRA.Mods.Cnc.Effects;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Cnc.Traits
{
	public class DropPodsPowerInfo : SupportPowerInfo, IRulesetLoaded
	{
		[FieldLoader.Require]
		[Desc("Drop pod unit")]
		[ActorReference([typeof(AircraftInfo), typeof(FallsToEarthInfo)])]
		public readonly string[] UnitTypes = null;

		[Desc("Number of drop pods spawned.")]
		public readonly int2 Drops = new(5, 8);

		[Desc("Sets the approach direction.")]
		public readonly WAngle PodFacing = new(128);

		[Desc("Maximum offset from targetLocation")]
		public readonly int PodScatter = 3;

		[Desc("Effect sequence sprite image")]
		public readonly string EntryEffect = "podring";

		[Desc("Effect sequence to display in the air.")]
		[SequenceReference(nameof(EntryEffect))]
		public readonly string EntryEffectSequence = "idle";

		[PaletteReference]
		public readonly string EntryEffectPalette = "effect";

		[ActorReference]
		[Desc("Actor to spawn when the attack starts")]
		public readonly string CameraActor = null;

		[Desc("Number of ticks to keep the camera alive")]
		public readonly int CameraRemoveDelay = 25;

		[Desc("Which weapon to fire")]
		[WeaponReference]
		public readonly string Weapon = "Vulcan2";

		public WeaponInfo WeaponInfo { get; private set; }

		[Desc("Apply the weapon impact this many ticks into the effect")]
		public readonly int WeaponDelay = 0;

		public override object Create(ActorInitializer init) { return new DropPodsPower(init.Self, this); }

		public override void RulesetLoaded(Ruleset rules, ActorInfo ai)
		{
			var weaponToLower = (Weapon ?? string.Empty).ToLowerInvariant();
			if (!rules.Weapons.TryGetValue(weaponToLower, out var weapon))
				throw new YamlException($"Weapons Ruleset does not contain an entry '{weaponToLower}'");

			WeaponInfo = weapon;

			base.RulesetLoaded(rules, ai);
		}
	}

	public class DropPodsPower : SupportPower
	{
		readonly DropPodsPowerInfo info;
		readonly string[] unitTypes;
		readonly Dictionary<string, Func<CPos, WPos>> getLaunchLocation = [];
		readonly Dictionary<string, HashSet<string>> landableTerrainTypes = [];

		public DropPodsPower(Actor self, DropPodsPowerInfo info)
			: base(self, info)
		{
			this.info = info;

			unitTypes = info.UnitTypes.Select(unit => unit.ToLowerInvariant()).ToArray();

			foreach (var unitType in unitTypes)
			{
				if (!self.World.Map.Rules.Actors.TryGetValue(unitType, out var actorInfo))
					throw new NotImplementedException("No rules definition for unit " + unitType);

				var aircraftInfo = actorInfo.TraitInfo<AircraftInfo>();
				var altitude = aircraftInfo.CruiseAltitude.Length;

				var delta =
					new WVec(0, -altitude * aircraftInfo.Speed / actorInfo.TraitInfo<FallsToEarthInfo>().Velocity.Length, 0)
					.Rotate(WRot.FromYaw(info.PodFacing));

				// PERF: Cache constant values.
				getLaunchLocation[unitType] = pos => self.World.Map.CenterOfCell(pos) - delta + new WVec(0, 0, altitude);
				landableTerrainTypes[unitType] = aircraftInfo.LandableTerrainTypes;
			}
		}

		public override void Activate(Actor self, Order order, SupportPowerManager manager)
		{
			SendDropPods(self, self.World.Map.CellContaining(order.Target.CenterPosition), () => base.Activate(self, order, manager));
		}

		public bool CanActivate(World world, CPos cell)
		{
			return world.Map.Contains(cell) && world.Map.FindTilesInCircle(cell, info.PodScatter)
				.Any(c => landableTerrainTypes.Any(ltr => ltr.Value.Contains(world.Map.GetTerrainInfo(c).Type))
					&& !world.ActorMap.GetActorsAt(c).Any());
		}

		public void SendDropPods(Actor self, CPos targetCell, Action onSuccess)
		{
			self.World.AddFrameEndTask(world =>
			{
				if (!CanActivate(self.World, targetCell))
					return;

				PlayLaunchSounds();
				onSuccess();

				if (info.CameraActor != null)
				{
					var camera = world.CreateActor(info.CameraActor,
					[
						new LocationInit(targetCell),
						new OwnerInit(self.Owner),
					]);

					camera.QueueActivity(new Wait(info.CameraRemoveDelay));
					camera.QueueActivity(new RemoveSelf());
				}

				var dropAmount = world.SharedRandom.Next(info.Drops.X, info.Drops.Y);
				var validUnitTypes = unitTypes.ToList();
				for (var i = 0; i < dropAmount; i++)
				{
					if (validUnitTypes.Count == 0)
						return;

					var unitType = validUnitTypes.Random(world.SharedRandom);
					var validDropLocations = world.Map.FindTilesInCircle(targetCell, info.PodScatter)
						.Where(c => landableTerrainTypes[unitType].Contains(world.Map.GetTerrainInfo(c).Type)
							&& !world.ActorMap.GetActorsAt(c).Any());

					if (!validDropLocations.Any())
					{
						validUnitTypes.Remove(unitType);
						i--;
						continue;
					}

					var dropLocation = validDropLocations.Random(world.SharedRandom);
					var launchLocation = getLaunchLocation[unitType](dropLocation);

					var pod = world.CreateActor(false, unitType,
					[
						new CenterPositionInit(launchLocation),
						new OwnerInit(self.Owner),
						new FacingInit(info.PodFacing)
					]);

					var aircraft = pod.Trait<Aircraft>();
					if (!aircraft.CanLand(dropLocation))
						pod.Dispose();
					else
					{
						world.Add(new DropPodImpact(self.Owner, info.WeaponInfo, world, launchLocation, Target.FromCell(world, dropLocation),
							info.WeaponDelay, info.EntryEffect, info.EntryEffectSequence, info.EntryEffectPalette));

						world.Add(pod);
					}
				}
			});
		}
	}
}
