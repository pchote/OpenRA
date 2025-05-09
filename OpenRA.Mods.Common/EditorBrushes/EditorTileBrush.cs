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
using System.IO;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Terrain;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Mods.Common.Widgets
{
	public sealed class EditorTileBrush : IEditorBrush
	{
		public readonly TerrainTemplateInfo TerrainTemplate;
		public readonly ushort Template;

		readonly WorldRenderer worldRenderer;
		readonly World world;
		readonly ITemplatedTerrainInfo terrainInfo;
		readonly EditorViewportControllerWidget editorWidget;
		readonly EditorActionManager editorActionManager;

		bool painting;

		readonly ITiledTerrainRenderer terrainRenderer;

		CPos cell;
		readonly List<IRenderable> preview = [];

		public EditorTileBrush(EditorViewportControllerWidget editorWidget, ushort id, WorldRenderer wr)
		{
			this.editorWidget = editorWidget;
			worldRenderer = wr;
			world = wr.World;
			terrainInfo = world.Map.Rules.TerrainInfo as ITemplatedTerrainInfo;
			if (terrainInfo == null)
				throw new InvalidDataException("EditorTileBrush can only be used with template-based tilesets");

			editorActionManager = world.WorldActor.Trait<EditorActionManager>();
			terrainRenderer = world.WorldActor.Trait<ITiledTerrainRenderer>();

			Template = id;
			TerrainTemplate = terrainInfo.Templates.First(t => t.Value.Id == id).Value;
			cell = wr.Viewport.ViewToWorld(wr.Viewport.WorldToViewPx(Viewport.LastMousePos));
			UpdatePreview();
		}

		public bool HandleMouseInput(MouseInput mi)
		{
			// Exclusively uses left and right mouse buttons, but nothing else
			if (mi.Button != MouseButton.Left && mi.Button != MouseButton.Right)
				return false;

			if (mi.Button == MouseButton.Right)
			{
				if (mi.Event == MouseInputEvent.Up)
				{
					editorWidget.ClearBrush();
					return true;
				}

				return false;
			}

			if (mi.Button == MouseButton.Left)
			{
				if (mi.Event == MouseInputEvent.Down)
					painting = true;
				else if (mi.Event == MouseInputEvent.Up)
					painting = false;
			}

			if (!painting)
				return true;

			if (mi.Event != MouseInputEvent.Down && mi.Event != MouseInputEvent.Move)
				return true;

			var cell = worldRenderer.Viewport.ViewToWorld(mi.Location);
			var isMoving = mi.Event == MouseInputEvent.Move;

			if (mi.Modifiers.HasModifier(Modifiers.Shift))
			{
				FloodFillWithBrush(cell);
				painting = false;
			}
			else
				PaintCell(cell, isMoving);

			return true;
		}

		void PaintCell(CPos cell, bool isMoving)
		{
			var template = terrainInfo.Templates[Template];
			if (isMoving && PlacementOverlapsSameTemplate(template, cell))
				return;

			editorActionManager.Add(new PaintTileEditorAction(Template, world.Map, cell));
		}

		void FloodFillWithBrush(CPos cell)
		{
			var map = world.Map;
			if (!map.Contains(cell))
				return;

			var mapTiles = map.Tiles;
			var replace = mapTiles[cell];

			if (replace.Type == Template)
				return;

			editorActionManager.Add(new FloodFillEditorAction(Template, map, cell));
		}

		bool PlacementOverlapsSameTemplate(TerrainTemplateInfo template, CPos cell)
		{
			var map = world.Map;
			var mapTiles = map.Tiles;
			var i = 0;
			for (var y = 0; y < template.Size.Y; y++)
			{
				for (var x = 0; x < template.Size.X; x++, i++)
				{
					if (template.Contains(i) && template[i] != null)
					{
						var c = cell + new CVec(x, y);
						if (mapTiles.Contains(c) && mapTiles[c].Type == template.Id)
							return true;
					}
				}
			}

			return false;
		}

		void UpdatePreview()
		{
			var pos = world.Map.CenterOfCell(cell);

			preview.Clear();
			preview.AddRange(terrainRenderer.RenderPreview(worldRenderer, TerrainTemplate, pos));
		}

		void IEditorBrush.TickRender(WorldRenderer wr, Actor self)
		{
			var currentCell = wr.Viewport.ViewToWorld(Viewport.LastMousePos);
			if (cell != currentCell)
			{
				cell = currentCell;
				UpdatePreview();
			}
		}

		IEnumerable<IRenderable> IEditorBrush.RenderAboveShroud(Actor self, WorldRenderer wr) { return preview; }
		IEnumerable<IRenderable> IEditorBrush.RenderAnnotations(Actor self, WorldRenderer wr) { yield break; }

		public void Tick() { }

		public void Dispose() { }
	}

	sealed class PaintTileEditorAction : IEditorAction
	{
		[FluentReference("id")]
		const string AddedTile = "notification-added-tile";

		public string Text { get; }

		readonly ushort template;
		readonly Map map;
		readonly CPos cell;

		readonly Queue<UndoTile> undoTiles = [];
		readonly TerrainTemplateInfo terrainTemplate;

		public PaintTileEditorAction(ushort template, Map map, CPos cell)
		{
			this.template = template;
			this.map = map;
			this.cell = cell;

			var terrainInfo = (ITemplatedTerrainInfo)map.Rules.TerrainInfo;
			terrainTemplate = terrainInfo.Templates[template];
			Text = FluentProvider.GetMessage(AddedTile, "id", terrainTemplate.Id);
		}

		public void Execute()
		{
			Do();
		}

		public void Do()
		{
			var mapTiles = map.Tiles;
			var mapHeight = map.Height;
			var baseHeight = mapHeight.Contains(cell) ? mapHeight[cell] : (byte)0;

			var i = 0;
			for (var y = 0; y < terrainTemplate.Size.Y; y++)
			{
				for (var x = 0; x < terrainTemplate.Size.X; x++, i++)
				{
					if (terrainTemplate.Contains(i) && terrainTemplate[i] != null)
					{
						var index = terrainTemplate.PickAny ? (byte)Game.CosmeticRandom.Next(0, terrainTemplate.TilesCount) : (byte)i;
						var c = cell + new CVec(x, y);
						if (!mapTiles.Contains(c))
							continue;

						undoTiles.Enqueue(new UndoTile(c, mapTiles[c], mapHeight[c]));

						mapTiles[c] = new TerrainTile(template, index);
						mapHeight[c] = (byte)(baseHeight + terrainTemplate[index].Height).Clamp(0, map.Grid.MaximumTerrainHeight);
					}
				}
			}
		}

		public void Undo()
		{
			var mapTiles = map.Tiles;
			var mapHeight = map.Height;

			while (undoTiles.Count > 0)
			{
				var undoTile = undoTiles.Dequeue();

				mapTiles[undoTile.Cell] = undoTile.MapTile;
				mapHeight[undoTile.Cell] = undoTile.Height;
			}
		}
	}

	sealed class FloodFillEditorAction : IEditorAction
	{
		[FluentReference("id")]
		const string FilledTile = "notification-filled-tile";

		public string Text { get; }

		readonly ushort template;
		readonly Map map;
		readonly CPos cell;

		readonly Queue<UndoTile> undoTiles = [];
		readonly TerrainTemplateInfo terrainTemplate;

		public FloodFillEditorAction(ushort template, Map map, CPos cell)
		{
			this.template = template;
			this.map = map;
			this.cell = cell;

			var terrainInfo = (ITemplatedTerrainInfo)map.Rules.TerrainInfo;
			terrainTemplate = terrainInfo.Templates[template];
			Text = FluentProvider.GetMessage(FilledTile, "id", terrainTemplate.Id);
		}

		public void Execute()
		{
			Do();
		}

		public void Do()
		{
			var queue = new Queue<CPos>();
			var touched = new CellLayer<bool>(map);
			var mapTiles = map.Tiles;
			var replace = mapTiles[cell];

			void MaybeEnqueue(CPos newCell)
			{
				if (map.Contains(cell) && !touched[newCell])
				{
					queue.Enqueue(newCell);
					touched[newCell] = true;
				}
			}

			bool ShouldPaint(CPos cellToCheck)
			{
				for (var y = 0; y < terrainTemplate.Size.Y; y++)
				{
					for (var x = 0; x < terrainTemplate.Size.X; x++)
					{
						var c = cellToCheck + new CVec(x, y);
						if (!map.Contains(c) || mapTiles[c].Type != replace.Type)
							return false;
					}
				}

				return true;
			}

			CPos FindEdge(CPos refCell, CVec direction)
			{
				while (true)
				{
					var newCell = refCell + direction;
					if (!ShouldPaint(newCell))
						return refCell;
					refCell = newCell;
				}
			}

			queue.Enqueue(cell);
			while (queue.Count > 0)
			{
				var queuedCell = queue.Dequeue();
				if (!ShouldPaint(queuedCell))
					continue;

				var previousCell = FindEdge(queuedCell, new CVec(-1 * terrainTemplate.Size.X, 0));
				var nextCell = FindEdge(queuedCell, new CVec(1 * terrainTemplate.Size.X, 0));

				for (var x = previousCell.X; x <= nextCell.X; x += terrainTemplate.Size.X)
				{
					PaintSingleCell(new CPos(x, queuedCell.Y));
					var upperCell = new CPos(x, queuedCell.Y - 1 * terrainTemplate.Size.Y);
					var lowerCell = new CPos(x, queuedCell.Y + 1 * terrainTemplate.Size.Y);

					if (ShouldPaint(upperCell))
						MaybeEnqueue(upperCell);
					if (ShouldPaint(lowerCell))
						MaybeEnqueue(lowerCell);
				}
			}
		}

		public void Undo()
		{
			var mapTiles = map.Tiles;
			var mapHeight = map.Height;

			while (undoTiles.Count > 0)
			{
				var undoTile = undoTiles.Dequeue();

				mapTiles[undoTile.Cell] = undoTile.MapTile;
				mapHeight[undoTile.Cell] = undoTile.Height;
			}
		}

		void PaintSingleCell(CPos cellToPaint)
		{
			var mapTiles = map.Tiles;
			var mapHeight = map.Height;
			var baseHeight = mapHeight.Contains(cellToPaint) ? mapHeight[cellToPaint] : (byte)0;

			var i = 0;
			for (var y = 0; y < terrainTemplate.Size.Y; y++)
			{
				for (var x = 0; x < terrainTemplate.Size.X; x++, i++)
				{
					if (terrainTemplate.Contains(i) && terrainTemplate[i] != null)
					{
						var index = terrainTemplate.PickAny ? (byte)Game.CosmeticRandom.Next(0, terrainTemplate.TilesCount) : (byte)i;
						var c = cellToPaint + new CVec(x, y);
						if (!mapTiles.Contains(c))
							continue;

						undoTiles.Enqueue(new UndoTile(c, mapTiles[c], mapHeight[c]));

						mapTiles[c] = new TerrainTile(template, index);
						mapHeight[c] = (byte)(baseHeight + terrainTemplate[index].Height).Clamp(0, map.Grid.MaximumTerrainHeight);
					}
				}
			}
		}
	}

	sealed record UndoTile(CPos Cell, TerrainTile MapTile, byte Height);
}
