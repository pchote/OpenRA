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

using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Mods.Common.Widgets
{
	public sealed class EditorActorBrush : IEditorBrush
	{
		readonly World world;
		readonly EditorActorLayer editorLayer;
		readonly EditorCursorLayer editorCursor;
		readonly EditorViewportControllerWidget editorWidget;
		readonly int cursorToken;

		public EditorActorBrush(EditorViewportControllerWidget editorWidget, ActorInfo actor, PlayerReference owner, WorldRenderer wr)
		{
			this.editorWidget = editorWidget;
			world = wr.World;
			editorLayer = world.WorldActor.Trait<EditorActorLayer>();
			editorCursor = world.WorldActor.Trait<EditorCursorLayer>();

			cursorToken = editorCursor.SetActor(wr, actor, owner);
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

			if (mi.Button == MouseButton.Left && mi.Event == MouseInputEvent.Down)
			{
				// Check the actor is inside the map
				var actor = editorCursor.Actor;
				if (!actor.Footprint.All(c => world.Map.Tiles.Contains(c.Key)))
					return true;

				editorLayer.Add(actor.Actor);
			}

			return true;
		}

		public void Tick() { }

		public void Dispose()
		{
			editorCursor.Clear(cursorToken);
		}
	}
}
