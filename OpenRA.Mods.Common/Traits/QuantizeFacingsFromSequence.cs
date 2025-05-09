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
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Derive facings from sprite body sequence.")]
	public class QuantizeFacingsFromSequenceInfo : ConditionalTraitInfo, IQuantizeBodyOrientationInfo, Requires<RenderSpritesInfo>
	{
		[SequenceReference]
		[Desc("Defines sequence to derive facings from.")]
		public readonly string Sequence = "idle";

		public int QuantizedBodyFacings(ActorInfo ai, SequenceSet sequences, string faction)
		{
			if (string.IsNullOrEmpty(Sequence))
				throw new InvalidOperationException($"Actor {ai.Name} is missing sequence to quantize facings from.");

			var rsi = ai.TraitInfo<RenderSpritesInfo>();
			var image = rsi.GetImage(ai, faction);
			var facings = sequences.GetSequence(image, Sequence).Facings;
			if (facings == 0)
				throw new InvalidOperationException(
					$"Actor {ai.Name} defines a quantized body orientation with zero facings. Faction: {faction} Image: {image} Sequence: {Sequence}");
			return facings;
		}

		public override object Create(ActorInitializer init) { return new QuantizeFacingsFromSequence(this); }
	}

	public class QuantizeFacingsFromSequence : ConditionalTrait<QuantizeFacingsFromSequenceInfo>
	{
		public QuantizeFacingsFromSequence(QuantizeFacingsFromSequenceInfo info)
			: base(info) { }
	}
}
