#region Copyright & License Information
/*
 * Copyright 2007-2014 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

#define TEST2

using System;
using System.Drawing;
using System.Linq;
using OpenRA.Mods.RA;
using OpenRA.Mods.RA.Buildings;
using OpenRA.Mods.RA.Widgets;
using OpenRA.Traits;
using OpenRA.Widgets;

namespace OpenRA.Mods.RA.Widgets.Logic
{
	public class IngameRadarDisplayLogic
	{
		[ObjectCreator.UseCtor]
		public IngameRadarDisplayLogic(Widget widget, World world)
		{
			var radarEnabled = false;
			var cachedRadarEnabled = false;
			widget.Get<RadarWidget>("RADAR_MINIMAP").IsEnabled = () => radarEnabled;

			var ticker = widget.Get<LogicTickerWidget>("RADAR_TICKER");
			ticker.OnTick = () =>
			{
				radarEnabled = world.ActorsWithTrait<ProvidesRadar>()
					.Any(a => a.Actor.Owner == world.LocalPlayer && a.Trait.IsActive);

				if (radarEnabled != cachedRadarEnabled)
					Sound.PlayNotification(world.Map.Rules, null, "Sounds", radarEnabled ? "RadarUp" : "RadarDown", null);
				cachedRadarEnabled = radarEnabled;
			};
		}
	}

	public class IngameCashCounterLogic
	{
		[ObjectCreator.UseCtor]
		public IngameCashCounterLogic(Widget widget, World world)
		{
			var playerResources = world.LocalPlayer.PlayerActor.Trait<PlayerResources>();
			var cash = widget.Get<LabelWidget>("CASH");
			var label = cash.Text;

			cash.GetText = () => label.F(playerResources.DisplayCash + playerResources.DisplayResources);
		}
	}

	public class IngamePowerCounterLogic
	{
		int powerDelta;
		#if TEST1
		int deltaTimestamp;
		#endif

		[ObjectCreator.UseCtor]
		public IngamePowerCounterLogic(Widget widget, World world)
		{
			var powerManager = world.LocalPlayer.PlayerActor.Trait<PowerManager>();
			var power = widget.Get<LabelWidget>("POWER");
			var defaultColor = power.GetColor();
			var color = defaultColor;

			power.GetText = () =>
			{
				if (powerManager.PowerProvided >= 1000000)
					return "inf";

				#if TEST1
				var time = (Environment.TickCount - deltaTimestamp).Clamp(0, 100);
				var value = powerManager.ExcessPower + (powerDelta * time / 100);
				var finalValue = powerManager.ExcessPower + powerDelta;

				color = finalValue >= 0 ? (powerDelta <= 0 ? defaultColor : Color.Green) : Color.Red;

				return value.ToString();
				#endif

				#if TEST2
				if (powerDelta != 0)
				{
					var currentValue = powerManager.ExcessPower;
					var finalValue = currentValue + powerDelta;

					if (currentValue >= 0 && finalValue < 0)
						color = Color.Red;
					else if (currentValue < 0 && finalValue >= 0)
						color = Color.Green;
					else
						color = defaultColor;

					return string.Format("{0} → {1}", powerManager.ExcessPower, finalValue);
				}
				return powerManager.ExcessPower.ToString();
				#endif
			};

			power.GetColor = () => color;
		}

		public void SetPowerDelta(int delta)
		{
			if (delta == powerDelta)
				return;

			powerDelta = delta;
			#if TEST1
			deltaTimestamp = Environment.TickCount;
			#endif
		}
	}

	public class IngamePowerBarLogic
	{
		[ObjectCreator.UseCtor]
		public IngamePowerBarLogic(Widget widget, World world)
		{
			var powerManager = world.LocalPlayer.PlayerActor.Trait<PowerManager>();
			var powerBar = widget.Get<ResourceBarWidget>("POWERBAR");

			powerBar.GetProvided = () => powerManager.PowerProvided;
			powerBar.GetUsed = () => powerManager.PowerDrained;
			powerBar.TooltipFormat = "Power Usage: {0}/{1}";
			powerBar.GetBarColor = () =>
			{
				if (powerManager.PowerState == PowerState.Critical)
					return Color.Red;
				if (powerManager.PowerState == PowerState.Low)
					return Color.Orange;
				return Color.LimeGreen;
			};
		}
	}

	public class IngameSiloBarLogic
	{
		[ObjectCreator.UseCtor]
		public IngameSiloBarLogic(Widget widget, World world)
		{
			var playerResources = world.LocalPlayer.PlayerActor.Trait<PlayerResources>();
			var siloBar = widget.Get<ResourceBarWidget>("SILOBAR");

			siloBar.GetProvided = () => playerResources.ResourceCapacity;
			siloBar.GetUsed = () => playerResources.Resources;
			siloBar.TooltipFormat = "Silo Usage: {0}/{1}";
			siloBar.GetBarColor = () =>
			{
				if (playerResources.Resources == playerResources.ResourceCapacity)
					return Color.Red;

				if (playerResources.Resources >= 0.8 * playerResources.ResourceCapacity)
					return Color.Orange;

				return Color.LimeGreen;
			};
		}
	}
}
