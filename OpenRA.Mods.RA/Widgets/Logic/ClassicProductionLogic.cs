#region Copyright & License Information
/*
 * Copyright 2007-2014 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System;
using System.Linq;
using OpenRA.Mods.RA.Widgets;
using OpenRA.Network;
using OpenRA.Widgets;

namespace OpenRA.Mods.RA.Widgets.Logic
{
	public class ClassicProductionLogic
	{
		ProductionPaletteWidget palette;
		readonly World world;

		void SetupProductionGroupButton(OrderManager orderManager, ProductionTypeButtonWidget button)
		{
			if (button == null)
				return;

			// Classic production queues are initialized at game start, and then never change.
			var queues = world.LocalPlayer.PlayerActor.TraitsImplementing<ProductionQueue>()
				.Where(q => q.Info.Type == button.ProductionGroup)
				.ToArray();

			Action<bool> selectTab = reverse =>
			{
				palette.CurrentQueue = queues.FirstOrDefault(q => q.Enabled);
			};

			button.IsDisabled = () => !queues.Any(q => q.BuildableItems().Any());
			button.OnMouseUp = mi => selectTab(mi.Modifiers.HasModifier(Modifiers.Shift));
			button.OnKeyPress = e => selectTab(e.Modifiers.HasModifier(Modifiers.Shift));
			button.IsHighlighted = () => queues.Any(q => q.CurrentDone) && orderManager.LocalFrameNumber / 9 % 2 == 1;

			var chromeName = button.ProductionGroup.ToLowerInvariant();
			var icon = button.Get<ImageWidget>("ICON");
			icon.GetImageName = () => button.IsDisabled() ? chromeName + "-disabled" :
				queues.Contains(palette.CurrentQueue) ? chromeName + "-alert" : chromeName;
		}

		[ObjectCreator.UseCtor]
		public ClassicProductionLogic(Widget widget, OrderManager orderManager, World world)
		{
			this.world = world;
			palette = widget.Get<ProductionPaletteWidget>("PRODUCTION_PALETTE");

			var background = widget.Get("PALETTE_BACKGROUND");
			if (background != null)
			{
				var rowtemplate = background.Get("ROW_TEMPLATE");
				var bottom = background.GetOrNull("BOTTOM_CAP");

				Action<int, int> updateBackground = (_, icons) =>
				{
					background.RemoveChildren();

					// Minimum of four rows to make space for the production buttons.
					var rows = Math.Max(4, (icons + palette.Columns - 1) / palette.Columns);
					var rowHeight = rowtemplate.Bounds.Height;
					for (var i = 0; i < rows; i++)
					{
						var row = rowtemplate.Clone();
						row.Bounds.Y = i * rowHeight;
						background.AddChild(row);
					}

					if (bottom == null)
						return;

					bottom.Bounds.Y = rows * rowHeight;
					background.AddChild(bottom);
				};

				palette.OnIconCountChanged += updateBackground;

				// Set the initial palette state
				updateBackground(0, 0);
			}

			var typesContainer = widget.Get("PRODUCTION_TYPES");
			foreach (var i in typesContainer.Children)
				SetupProductionGroupButton(orderManager, i as ProductionTypeButtonWidget);
		}
	}
}
