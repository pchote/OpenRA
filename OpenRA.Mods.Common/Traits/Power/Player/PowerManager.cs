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

using System.Collections.Generic;
using System.Linq;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[TraitLocation(SystemActors.Player)]
	[Desc("Attach this to the player actor.")]
	public class PowerManagerInfo : TraitInfo, Requires<DeveloperModeInfo>
	{
		[Desc("Interval (in milliseconds) at which to warn the player of low power.")]
		public readonly int AdviceInterval = 10000;

		[Desc("The speech notification to play when the player is low power.")]
		[NotificationReference("Speech")]
		public readonly string SpeechNotification = null;

		[Desc("The text notification to display when the player is low power.")]
		[FluentReference(optional: true)]
		public readonly string TextNotification = null;

		public override object Create(ActorInitializer init) { return new PowerManager(init.Self, this); }
	}

	public class PowerManager : INotifyCreated, ITick, ISync, IResolveOrder
	{
		readonly Actor self;
		readonly PowerManagerInfo info;
		readonly DeveloperMode devMode;

		readonly Dictionary<Actor, int> powerDrain = [];

		[Sync]
		public int PowerProvided { get; private set; }

		[Sync]
		public int PowerDrained { get; private set; }

		public int ExcessPower => PowerProvided - PowerDrained;

		public int PowerOutageRemainingTicks { get; private set; }
		public int PowerOutageTotalTicks { get; private set; }
		public bool PlayLowPowerNotification { get; set; }

		long lastPowerAdviceTime;
		bool isLowPower;
		bool wasLowPower;
		bool wasHackEnabled;

		public PowerManager(Actor self, PowerManagerInfo info)
		{
			this.self = self;
			this.info = info;

			devMode = self.Trait<DeveloperMode>();
			wasHackEnabled = devMode.UnlimitedPower;
			PlayLowPowerNotification = info.AdviceInterval > 0;
		}

		void INotifyCreated.Created(Actor self)
		{
			// Map placed actors will query an inconsistent power state when they are created
			// (it will depend on the order that they are spawned by the world)
			// Tell them to query the correct state once the world has been fully created
			self.World.AddFrameEndTask(_ => UpdatePowerRequiringActors());
		}

		public void UpdateActor(Actor a)
		{
			// Do not add power from actors that are not in the world
			if (!a.IsInWorld)
				return;

			// Old is 0 if a is not in powerDrain
			powerDrain.TryGetValue(a, out var old);

			var amount = a.TraitsImplementing<Power>().Where(t => !t.IsTraitDisabled).Sum(p => p.GetEnabledPower());
			powerDrain[a] = amount;

			if (amount == old || devMode.UnlimitedPower)
				return;

			if (old > 0)
				PowerProvided -= old;
			else if (old < 0)
				PowerDrained += old;

			if (amount > 0)
				PowerProvided += amount;
			else if (amount < 0)
				PowerDrained -= amount;

			UpdatePowerState();
		}

		public void RemoveActor(Actor a)
		{
			// Do not remove power from actors that are still in the world
			if (a.IsInWorld)
				return;

			if (!powerDrain.TryGetValue(a, out var amount))
				return;
			powerDrain.Remove(a);

			if (devMode.UnlimitedPower)
				return;

			if (amount > 0)
				PowerProvided -= amount;
			else if (amount < 0)
				PowerDrained += amount;

			UpdatePowerState();
		}

		void UpdatePowerState()
		{
			isLowPower = ExcessPower < 0;

			if (isLowPower != wasLowPower)
				UpdatePowerRequiringActors();

			// Force the notification to play immediately
			if (isLowPower && !wasLowPower)
				lastPowerAdviceTime = -info.AdviceInterval;

			wasLowPower = isLowPower;
		}

		void ITick.Tick(Actor self)
		{
			if (wasHackEnabled != devMode.UnlimitedPower)
			{
				PowerProvided = 0;
				PowerDrained = 0;

				if (!devMode.UnlimitedPower)
				{
					foreach (var kv in powerDrain)
					{
						if (kv.Value > 0)
							PowerProvided += kv.Value;
						else if (kv.Value < 0)
							PowerDrained -= kv.Value;
					}
				}

				wasHackEnabled = devMode.UnlimitedPower;
				UpdatePowerState();
			}

			if (PlayLowPowerNotification && isLowPower && Game.RunTime > lastPowerAdviceTime + info.AdviceInterval)
			{
				Game.Sound.PlayNotification(self.World.Map.Rules, self.Owner, "Speech", info.SpeechNotification, self.Owner.Faction.InternalName);
				TextNotificationsManager.AddTransientLine(self.Owner, info.TextNotification);

				lastPowerAdviceTime = Game.RunTime;
			}

			if (PowerOutageRemainingTicks > 0 && --PowerOutageRemainingTicks == 0)
				UpdatePowerOutageActors();
		}

		public PowerState PowerState
		{
			get
			{
				if (PowerProvided >= PowerDrained)
					return PowerState.Normal;

				if (PowerProvided > PowerDrained / 2)
					return PowerState.Low;

				return PowerState.Critical;
			}
		}

		public void TriggerPowerOutage(int totalTicks)
		{
			PowerOutageTotalTicks = PowerOutageRemainingTicks = totalTicks;
			UpdatePowerOutageActors();
		}

		void UpdatePowerOutageActors()
		{
			var traitPairs = self.World.ActorsWithTrait<AffectedByPowerOutage>()
				.Where(p => !p.Actor.IsDead && p.Actor.IsInWorld && p.Actor.Owner == self.Owner);

			foreach (var p in traitPairs)
				p.Trait.UpdateStatus(p.Actor);
		}

		void UpdatePowerRequiringActors()
		{
			var traitPairs = self.World.ActorsWithTrait<INotifyPowerLevelChanged>()
				.Where(p => !p.Actor.IsDead && p.Actor.IsInWorld && p.Actor.Owner == self.Owner);

			foreach (var p in traitPairs)
				p.Trait.PowerLevelChanged(p.Actor);
		}

		void IResolveOrder.ResolveOrder(Actor self, Order order)
		{
			if (devMode.Enabled && order.OrderString == "PowerOutage")
				TriggerPowerOutage((int)order.ExtraData);
		}
	}
}
