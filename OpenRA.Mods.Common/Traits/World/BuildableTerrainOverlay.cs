#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System.Collections.Generic;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[TraitLocation(SystemActors.EditorWorld)]
	public class BuildableTerrainOverlayInfo : TraitInfo
	{
		[FieldLoader.Require]
		public readonly HashSet<string> AllowedTerrainTypes = null;

		[PaletteReference]
		[Desc("Palette to use for rendering the sprite.")]
		public readonly string Palette = TileSet.TerrainPaletteInternalName;

		[Desc("Sprite definition.")]
		public readonly string Image = "overlay";

		[SequenceReference("Image")]
		[Desc("Sequence to use for unbuildable area.")]
		public readonly string Sequence = "build-invalid";

		[Desc("Custom opacity to apply to the overlay sprite.")]
		public readonly float Alpha = 1f;

		public override object Create(ActorInitializer init)
		{
			return new BuildableTerrainOverlay(init.Self, this);
		}
	}

	public class BuildableTerrainOverlay : IRenderAboveWorld, IWorldLoaded, INotifyActorDisposing
	{
		readonly BuildableTerrainOverlayInfo info;
		readonly World world;
		readonly Sprite disabledSprite;

		public bool Enabled = false;
		TerrainSpriteLayer render;
		PaletteReference palette;

		bool disposed;

		public BuildableTerrainOverlay(Actor self, BuildableTerrainOverlayInfo info)
		{
			this.info = info;
			world = self.World;

			var rules = self.World.Map.Rules;
			disabledSprite = rules.Sequences.GetSequence(info.Image, info.Sequence).GetSprite(0);
		}

		void IWorldLoaded.WorldLoaded(World w, WorldRenderer wr)
		{
			render = new TerrainSpriteLayer(w, wr, disabledSprite, BlendMode.Alpha);

			world.Map.Tiles.CellEntryChanged += UpdateTerrainCell;
			world.Map.CustomTerrain.CellEntryChanged += UpdateTerrainCell;
			palette = wr.Palette(info.Palette);

			foreach (var cell in w.Map.AllCells)
				UpdateTerrainCell(cell);
		}

		void UpdateTerrainCell(CPos cell)
		{
			if (!world.Map.Contains(cell) || !info.AllowedTerrainTypes.Contains(world.Map.GetTerrainInfo(cell).Type))
				render.Update(cell, disabledSprite, palette, 1f, info.Alpha);
		}

		void IRenderAboveWorld.RenderAboveWorld(Actor self, WorldRenderer wr)
		{
			if (Enabled)
				render.Draw(wr.Viewport.RenderableRegion);
		}

		void INotifyActorDisposing.Disposing(Actor self)
		{
			if (disposed)
				return;

			render.Dispose();
			disposed = true;
		}
	}
}
