#region Copyright & License Information
/*
 * Copyright 2007-2019 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Mods.Common.Widgets
{
	public sealed class EditorResourceBrush : IEditorBrush
	{
		public readonly ResourceTypeInfo ResourceType;

		readonly WorldRenderer worldRenderer;
		readonly World world;
		readonly EditorViewportControllerWidget editorWidget;
		readonly EditorCursorLayer editorCursor;
		readonly int cursorToken;

		public EditorResourceBrush(EditorViewportControllerWidget editorWidget, ResourceTypeInfo resource, WorldRenderer wr)
		{
			this.editorWidget = editorWidget;
			ResourceType = resource;
			worldRenderer = wr;
			world = wr.World;
			editorCursor = world.WorldActor.Trait<EditorCursorLayer>();

			cursorToken = editorCursor.SetResource(wr, resource);
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

			if (editorCursor.CurrentToken != cursorToken)
				return false;

			var cell = worldRenderer.Viewport.ViewToWorld(mi.Location);

			if (mi.Button == MouseButton.Left && AllowResourceAt(cell))
			{
				var type = (byte)ResourceType.ResourceType;
				var index = (byte)ResourceType.MaxDensity;
				world.Map.Resources[cell] = new ResourceTile(type, index);
			}

			return true;
		}

		public bool AllowResourceAt(CPos cell)
		{
			var mapResources = world.Map.Resources;
			if (!mapResources.Contains(cell))
				return false;

			var tile = world.Map.Tiles[cell];
			var tileInfo = world.Map.Rules.TileSet.GetTileInfo(tile);
			if (tileInfo == null)
				return false;

			var terrainType = world.Map.Rules.TileSet.TerrainInfo[tileInfo.TerrainType];

			if (mapResources[cell].Type == ResourceType.ResourceType)
				return false;

			if (!ResourceType.AllowedTerrainTypes.Contains(terrainType.Type))
				return false;

			return ResourceType.AllowOnRamps || tileInfo.RampType == 0;
		}

		public void Tick() { }

		public void Dispose()
		{
			editorCursor.Clear(cursorToken);
		}
	}
}
