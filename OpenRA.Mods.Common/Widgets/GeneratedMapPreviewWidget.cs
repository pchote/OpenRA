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
using OpenRA.FileFormats;
using OpenRA.Graphics;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets
{
	public class GeneratedMapPreviewWidget : Widget
	{
		public readonly bool ShowSpawnPoints = true;

		readonly Sprite spawnUnclaimed;
		readonly SpriteFont spawnFont;
		readonly Color spawnColor, spawnContrastColor;
		readonly int2 spawnLabelOffset;

		Sheet mapSheet;
		Sprite mapSprite;
		Rectangle mapRect;

		public GeneratedMapPreviewWidget()
		{
			spawnUnclaimed = ChromeProvider.GetImage("lobby-bits", "spawn-unclaimed");
			spawnFont = Game.Renderer.Fonts[ChromeMetrics.Get<string>("SpawnFont")];
			spawnColor = ChromeMetrics.Get<Color>("SpawnColor");
			spawnContrastColor = ChromeMetrics.Get<Color>("SpawnContrastColor");
			spawnLabelOffset = ChromeMetrics.Get<int2>("SpawnLabelOffset");
		}

		protected GeneratedMapPreviewWidget(GeneratedMapPreviewWidget other)
			: base(other)
		{
			ShowSpawnPoints = other.ShowSpawnPoints;
			spawnUnclaimed = ChromeProvider.GetImage("lobby-bits", "spawn-unclaimed");
			spawnFont = Game.Renderer.Fonts[ChromeMetrics.Get<string>("SpawnFont")];
			spawnColor = ChromeMetrics.Get<Color>("SpawnColor");
			spawnContrastColor = ChromeMetrics.Get<Color>("SpawnContrastColor");
			spawnLabelOffset = ChromeMetrics.Get<int2>("SpawnLabelOffset");
		}

		public override GeneratedMapPreviewWidget Clone() { return new GeneratedMapPreviewWidget(this); }

		(int2 Pos, string Label, int2 LabelOffset)[] spawns = [];

		public void Update(Map map, Png preview)
		{
			if (mapSheet == null || mapSheet.Size.Width < preview.Width || mapSheet.Size.Height < preview.Height)
			{
				mapSheet?.Dispose();
				mapSheet = new Sheet(SheetType.BGRA, new Size(preview.Width, preview.Height).NextPowerOf2());
			}

			var spriteRect = new Rectangle(0, 0, preview.Width, preview.Height);
			mapSprite = new Sprite(mapSheet, spriteRect, TextureChannel.RGBA);
			OpenRA.Graphics.Util.FastCopyIntoSprite(mapSprite, preview);
			mapSheet.CommitBufferedData();

			// Update map rect
			var previewScale = Math.Min(RenderBounds.Width * 1f / spriteRect.Width, RenderBounds.Height * 1f / spriteRect.Height);
			var w = (int)(previewScale * spriteRect.Width);
			var h = (int)(previewScale * spriteRect.Height);
			var x = RenderBounds.X + (RenderBounds.Width - w) / 2;
			var y = RenderBounds.Y + (RenderBounds.Height - h) / 2;
			mapRect = new Rectangle(x, y, w, h);

			if (ShowSpawnPoints)
			{
				var s = new List<(int2, string, int2)>();
				foreach (var kv in map.ActorDefinitions.Where(d => d.Value.Value == "mpspawn"))
				{
					var p = new ActorReference(kv.Value.Value, kv.Value.ToDictionary()).Get<LocationInit>().Value;
					var pos = ConvertToPreview(p, map, previewScale);

					var sprite = spawnUnclaimed;
					var offset = sprite.Size.XY.ToInt2() / 2;
					WidgetUtils.DrawSprite(sprite, pos - offset);

					var number = Convert.ToChar('A' + s.Count).ToString();
					var textOffset = spawnFont.Measure(number) / 2 + spawnLabelOffset;
					s.Add((pos, number, textOffset));
				}

				spawns = s.ToArray();
			}
		}

		public void Clear()
		{
			mapSprite = null;
		}

		public int2 ConvertToPreview(CPos cell, Map map, float previewScale)
		{
			var point = cell.ToMPos(map.Grid.Type);
			var cellWidth = map.Grid.Type == MapGridType.RectangularIsometric ? 2 : 1;
			var dx = (int)(previewScale * cellWidth * (point.U - map.Bounds.Left));
			var dy = (int)(previewScale * (point.V - map.Bounds.Top));

			// Odd rows are shifted right by 1px
			if ((point.V & 1) == 1)
				dx++;

			return new int2(mapRect.X + dx, mapRect.Y + dy);
		}

		public override void Draw()
		{
			if (mapSprite == null)
				return;

			WidgetUtils.DrawSprite(mapSprite, mapRect.Location, mapRect.Size);
			var offset = spawnUnclaimed.Size.XY.ToInt2() / 2;
			foreach (var (pos, label, labelOffset) in spawns)
			{
				WidgetUtils.DrawSprite(spawnUnclaimed, pos - offset);
				spawnFont.DrawTextWithContrast(label, pos - labelOffset, spawnColor, spawnContrastColor, 1);
			}
		}

		public bool Loaded => mapSprite != null;
	}
}
