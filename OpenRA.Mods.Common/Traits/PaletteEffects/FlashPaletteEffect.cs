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
using OpenRA.Graphics;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[TraitLocation(SystemActors.World | SystemActors.EditorWorld)]
	[Desc("Used for bursted one-colored whole screen effects. Add this to the world actor.")]
	public class FlashPaletteEffectInfo : TraitInfo
	{
		[Desc("Measured in ticks.")]
		public readonly int Length = 20;

		public readonly Color Color = Color.White;

		[Desc("Set this when using multiple independent flash effects.")]
		public readonly string Type = null;

		public override object Create(ActorInitializer init) { return new FlashPaletteEffect(this); }
	}

	public class FlashPaletteEffect : RenderPostProcessPassBase, ITick
	{
		public readonly FlashPaletteEffectInfo Info;
		int remainingFrames;
		float frac;

		public FlashPaletteEffect(FlashPaletteEffectInfo info)
			: base("flash", PostProcessPassType.AfterWorld)
		{
			Info = info;
		}

		public void Enable(int ticks)
		{
			if (ticks == -1)
				remainingFrames = Info.Length;
			else
				remainingFrames = ticks;
		}

		void ITick.Tick(Actor self)
		{
			if (remainingFrames > 0)
				frac = Math.Min((float)--remainingFrames / Info.Length, 1);
		}

		protected override bool Enabled => remainingFrames > 0;
		protected override void PrepareRender(WorldRenderer wr, IShader shader)
		{
			shader.SetVec("Frac", frac);
			shader.SetVec("Color", (float)Info.Color.B / 255, (float)Info.Color.G / 255, (float)Info.Color.R / 255);
		}
	}
}
