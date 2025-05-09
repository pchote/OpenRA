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
using OpenRA.Graphics;
using OpenRA.Mods.Common.Orders;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Renders target lines between order waypoints.")]
	public class DrawLineToTargetInfo : TraitInfo
	{
		[Desc("Delay (in milliseconds) before the target lines disappear.")]
		public readonly int Delay = 2400;

		[Desc("Width (in pixels) of the target lines.")]
		public readonly int LineWidth = 1;

		[Desc("Width (in pixels) of the queued target lines.")]
		public readonly int QueuedLineWidth = 1;

		[Desc("Width (in pixels) of the end node markers.")]
		public readonly int MarkerWidth = 2;

		[Desc("Width (in pixels) of the queued end node markers.")]
		public readonly int QueuedMarkerWidth = 2;

		[PaletteReference]
		[Desc("Palette used for rendering sprites.")]
		public readonly string Palette = TileSet.TerrainPaletteInternalName;

		public override object Create(ActorInitializer init) { return new DrawLineToTarget(this); }
	}

	public class DrawLineToTarget : IRenderAboveShroud, IRenderAnnotationsWhenSelected, INotifySelected
	{
		readonly DrawLineToTargetInfo info;
		readonly List<IRenderable> renderableCache = [];
		long lifetime;

		public DrawLineToTarget(DrawLineToTargetInfo info)
		{
			this.info = info;
		}

		public void ShowTargetLines(Actor self)
		{
			if (Game.Settings.Game.TargetLines < TargetLinesType.Automatic || self.IsIdle)
				return;

			// Reset the order line timeout.
			lifetime = Game.RunTime + info.Delay;
		}

		void INotifySelected.Selected(Actor self)
		{
			ShowTargetLines(self);
		}

		bool ShouldRender(Actor self)
		{
			if (!self.Owner.IsAlliedWith(self.World.LocalPlayer) || Game.Settings.Game.TargetLines == TargetLinesType.Disabled)
				return false;

			// Players want to see the lines when in waypoint mode.
			var force = Game.GetModifierKeys().HasModifier(Modifiers.Shift) || self.World.OrderGenerator is ForceModifiersOrderGenerator;

			return force || Game.RunTime <= lifetime;
		}

		IEnumerable<IRenderable> IRenderAboveShroud.RenderAboveShroud(Actor self, WorldRenderer wr)
		{
			if (!ShouldRender(self))
				return [];

			return RenderAboveShroud(self, wr);
		}

		IEnumerable<IRenderable> RenderAboveShroud(Actor self, WorldRenderer wr)
		{
			var pal = wr.Palette(info.Palette);
			var a = self.CurrentActivity;
			for (; a != null; a = a.NextActivity)
				if (!a.IsCanceling)
					foreach (var n in a.TargetLineNodes(self))
						if (n.Tile != null && n.Target.Type != TargetType.Invalid)
							yield return new SpriteRenderable(n.Tile, n.Target.CenterPosition, WVec.Zero, -511, pal, 1f, 1f, float3.Ones, TintModifiers.IgnoreWorldTint, true);
		}

		bool IRenderAboveShroud.SpatiallyPartitionable => false;

		IEnumerable<IRenderable> IRenderAnnotationsWhenSelected.RenderAnnotations(Actor self, WorldRenderer wr)
		{
			if (!ShouldRender(self))
				return [];

			renderableCache.Clear();
			var prev = self.CenterPosition;
			var a = self.CurrentActivity;
			for (; a != null; a = a.NextActivity)
			{
				if (a.IsCanceling)
					continue;

				foreach (var n in a.TargetLineNodes(self))
				{
					if (n.Target.Type != TargetType.Invalid && n.Tile == null)
					{
						var lineWidth = renderableCache.Count > 0 ? info.QueuedLineWidth : info.LineWidth;
						var markerWidth = renderableCache.Count > 0 ? info.QueuedMarkerWidth : info.MarkerWidth;

						var pos = n.Target.CenterPosition;
						renderableCache.Add(new TargetLineRenderable([prev, pos], n.Color, lineWidth, markerWidth));
						prev = pos;
					}
				}
			}

			if (renderableCache.Count == 0)
				return [];

			// Reverse draw order so target markers are drawn on top of the next line
			renderableCache.Reverse();
			return renderableCache.ToArray();
		}

		bool IRenderAnnotationsWhenSelected.SpatiallyPartitionable => false;
	}

	public static class LineTargetExts
	{
		public static void ShowTargetLines(this Actor self)
		{
			// Target lines are only automatically shown for the owning player
			// Spectators and allies must use the force-display modifier
			if (self.Owner != self.World.LocalPlayer)
				return;

			// Draw after frame end so that all the queueing of activities are done before drawing.
			var line = self.TraitOrDefault<DrawLineToTarget>();
			if (line != null)
				self.World.AddFrameEndTask(w => line.ShowTargetLines(self));
		}
	}
}
