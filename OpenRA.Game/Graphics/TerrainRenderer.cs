#region Copyright & License Information
/*
 * Copyright 2007-2011 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using OpenRA.Traits;

namespace OpenRA.Graphics
{
	class TerrainRenderer
	{
		IVertexBuffer<Vertex> vertexBuffer;

		World world;
		Map map;

		public TerrainRenderer(World world, WorldRenderer wr)
		{
			this.world = world;
			this.map = world.Map;

			var terrainPalette = wr.Palette("terrain").Index;
			var vertices = new Vertex[4 * map.Size.Width * map.Size.Height];
			int nv = 0;

			for (var j = 0; j < map.Size.Height; j++)
			{
				for (var i = 0; i < map.Size.Width; i++)
				{
					var mc = new MapCell(map, i, j);
					var tile = wr.Theater.TileSprite(mc.Tile);
					var pos = wr.ScreenPosition(world.CenterOfCell(mc.Location)) - 0.5f * tile.size;
					Util.FastCreateQuad(vertices, pos, tile, terrainPalette, nv, tile.size);
					nv += 4;
				}
			}

			vertexBuffer = Game.Renderer.Device.CreateVertexBuffer(vertices.Length);
			vertexBuffer.SetData(vertices, nv);
		}

		public void Draw(WorldRenderer wr, Viewport viewport)
		{
			var verticesPerRow = 4*map.Size.Width;
			var bounds = viewport.CellBounds;
			if (bounds.Bottom < 0 || bounds.Top > map.Bounds.Height)
				return;

			Game.Renderer.WorldSpriteRenderer.DrawVertexBuffer(
				vertexBuffer, verticesPerRow * bounds.Top, verticesPerRow * bounds.Height,
				PrimitiveType.QuadList, wr.Theater.Sheet);

			foreach (var r in world.WorldActor.TraitsImplementing<IRenderOverlay>())
				r.Render(wr);
		}
	}
}
