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
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public class EncyclopediaLogic : ChromeLogic
	{
		readonly World world;
		readonly ModData modData;
		readonly Dictionary<ActorInfo, EncyclopediaInfo> info = new();

		readonly ScrollPanelWidget descriptionPanel;
		readonly LabelWidget descriptionLabel;
		readonly SpriteFont descriptionFont;

		readonly ScrollPanelWidget actorList;
		readonly ScrollItemWidget headerTemplate;
		readonly ScrollItemWidget template;
		readonly ActorPreviewWidget previewWidget;

		ActorInfo selectedActor;
		ScrollItemWidget firstItem;

		[ObjectCreator.UseCtor]
		public EncyclopediaLogic(Widget widget, World world, ModData modData, Action onExit)
		{
			this.world = world;
			this.modData = modData;

			actorList = widget.Get<ScrollPanelWidget>("ACTOR_LIST");

			headerTemplate = widget.Get<ScrollItemWidget>("HEADER");
			template = widget.Get<ScrollItemWidget>("TEMPLATE");

			widget.Get("ACTOR_INFO").IsVisible = () => selectedActor != null;

			previewWidget = widget.Get<ActorPreviewWidget>("ACTOR_PREVIEW");
			previewWidget.IsVisible = () => selectedActor != null;

			descriptionPanel = widget.Get<ScrollPanelWidget>("ACTOR_DESCRIPTION_PANEL");

			descriptionLabel = descriptionPanel.Get<LabelWidget>("ACTOR_DESCRIPTION");
			descriptionFont = Game.Renderer.Fonts[descriptionLabel.Font];

			actorList.RemoveChildren();

			foreach (var actor in modData.DefaultRules.Actors.Values)
			{
				if (actor.TraitInfos<IRenderActorPreviewSpritesInfo>().Count == 0)
					continue;

				var statistics = actor.TraitInfoOrDefault<UpdatesPlayerStatisticsInfo>();
				if (statistics != null && !string.IsNullOrEmpty(statistics.OverrideActor))
					continue;

				var encyclopedia = actor.TraitInfoOrDefault<EncyclopediaInfo>();
				if (encyclopedia == null)
					continue;

				info.Add(actor, encyclopedia);
			}

			var categories = info.Select(a => a.Value.Category).Distinct().
				OrderBy(string.IsNullOrWhiteSpace).ThenBy(s => s);

			foreach (var category in categories)
			{
				CreateActorGroup(category, info
					.Where(a => a.Value.Category == category)
					.OrderBy(a => a.Value.Order)
					.Select(a => a.Key));
			}

			widget.Get<ButtonWidget>("BACK_BUTTON").OnClick = () =>
			{
				Game.Disconnect();
				Ui.CloseWindow();
				onExit();
			};
		}

		void CreateActorGroup(string title, IEnumerable<ActorInfo> actors)
		{
			var header = ScrollItemWidget.Setup(headerTemplate, () => false, () => { });
			header.Get<LabelWidget>("LABEL").GetText = () => title;
			actorList.AddChild(header);

			foreach (var actor in actors)
			{
				var item = ScrollItemWidget.Setup(template,
					() => selectedActor != null && selectedActor.Name == actor.Name,
					() => SelectActor(actor));

				var label = item.Get<LabelWithTooltipWidget>("TITLE");
				var name = actor.TraitInfos<TooltipInfo>().FirstOrDefault(info => info.EnabledByDefault)?.Name;
				if (!string.IsNullOrEmpty(name))
					WidgetUtils.TruncateLabelToTooltip(label, FluentProvider.GetMessage(name));

				if (firstItem == null)
				{
					firstItem = item;
					SelectActor(actor);
				}

				actorList.AddChild(item);
			}
		}

		void SelectActor(ActorInfo actor)
		{
			var selectedInfo = info[actor];
			selectedActor = actor;

			var typeDictionary = new TypeDictionary()
			{
				new OwnerInit(world.WorldActor.Owner),
				new FactionInit(world.WorldActor.Owner.PlayerReference.Faction)
			};

			foreach (var actorPreviewInit in actor.TraitInfos<IActorPreviewInitInfo>())
				foreach (var inits in actorPreviewInit.ActorPreviewInits(actor, ActorPreviewType.ColorPicker))
					typeDictionary.Add(inits);

			previewWidget.SetPreview(actor, typeDictionary);
			previewWidget.GetScale = () => selectedInfo.Scale;

			var text = "";

			var buildable = actor.TraitInfoOrDefault<BuildableInfo>();
			if (buildable != null)
			{
				var prerequisites = buildable.Prerequisites
					.Select(a => ActorName(modData.DefaultRules, a))
					.Where(s => !s.StartsWith('~') && !s.StartsWith('!'))
					.ToList();
				if (prerequisites.Count != 0)
					text += $"Requires {prerequisites.JoinWith(", ")}\n\n";
			}

			if (selectedInfo != null && !string.IsNullOrEmpty(selectedInfo.Description))
				text += WidgetUtils.WrapText(FluentProvider.GetMessage(selectedInfo.Description) + "\n\n", descriptionLabel.Bounds.Width, descriptionFont);

			var height = descriptionFont.Measure(text).Y;
			descriptionLabel.GetText = () => text;
			descriptionLabel.Bounds.Height = height;
			descriptionPanel.Layout.AdjustChildren();

			descriptionPanel.ScrollToTop();
		}

		static string ActorName(Ruleset rules, string name)
		{
			if (rules.Actors.TryGetValue(name.ToLowerInvariant(), out var actor))
			{
				var actorTooltip = actor.TraitInfos<TooltipInfo>().FirstOrDefault(info => info.EnabledByDefault);
				if (actorTooltip != null)
					return FluentProvider.GetMessage(actorTooltip.Name);
			}

			return name;
		}
	}
}
