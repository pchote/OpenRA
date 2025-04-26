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
using System.Threading.Tasks;
using OpenRA.FileFormats;
using OpenRA.FileSystem;
using OpenRA.Mods.Common.MapGenerator;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public class MapGeneratorLogic : ChromeLogic
	{
		[FluentReference]
		const string Tileset = "label-bg-tileset";

		[FluentReference]
		const string MapSize = "label-bg-map-size";

		[FluentReference]
		const string RandomMap = "label-mapchooser-random-map-title";

		[FluentReference]
		const string Generating = "label-mapchooser-random-generating";

		[FluentReference]
		const string GenerationFailed = "label-mapchooser-random-error";

		[FluentReference(LintDictionaryReference.Values)]
		public static readonly IReadOnlyDictionary<string, int2> MapSizes = new Dictionary<string, int2>()
		{
			{ "label-map-size-small", new int2(48, 60) },
			{ "label-map-size-medium", new int2(60, 90) },
			{ "label-map-size-large", new int2(90, 120) },
			{ "label-map-size-huge", new int2(120, 160) },
		};

		readonly ModData modData;
		readonly IEditorMapGeneratorInfo generator;
		readonly IMapGeneratorSettings settings;
		readonly Action<MapGenerationArgs> onGenerate;

		readonly GeneratedMapPreviewWidget preview;
		readonly ScrollPanelWidget settingsPanel;
		readonly Widget checkboxSettingTemplate;
		readonly Widget textSettingTemplate;
		readonly Widget dropdownSettingTemplate;
		readonly Widget tilesetSetting;
		readonly Widget sizeSetting;

		static ITerrainInfo selectedTerrain;
		static string selectedSize;

		volatile bool generating;
		volatile bool failed;

		[ObjectCreator.UseCtor]
		internal MapGeneratorLogic(Widget widget, ModData modData, Action<MapGenerationArgs> onGenerate)
		{
			this.modData = modData;
			this.onGenerate = onGenerate;

			generator = modData.DefaultRules.Actors[SystemActors.EditorWorld].TraitInfos<IEditorMapGeneratorInfo>().First();
			settings = generator.GetSettings();
			preview = widget.Get<GeneratedMapPreviewWidget>("PREVIEW");

			widget.Get("ERROR").IsVisible = () => failed;

			var title = new CachedTransform<string, string>(id => FluentProvider.GetMessage(id));
			var titleLabel = widget.Get<LabelWidget>("TITLE");
			titleLabel.GetText = () => title.Update(generating ? Generating : failed ? GenerationFailed : RandomMap);

			/*
			var detailsWidget = widget.GetOrNull<LabelWidget>("DETAILS");
			if (detailsWidget != null)
			{
				var type = preview.Categories.FirstOrDefault();
				var details = "";
				if (type != null)
					details = type + " ";

				details += FluentProvider.GetMessage(Players, "players", preview.PlayerCount);
				detailsWidget.GetText = () => details;
			}

			var authorWidget = item.GetOrNull<LabelWithTooltipWidget>("AUTHOR");
			if (authorWidget != null && !string.IsNullOrEmpty(preview.Author))
				WidgetUtils.TruncateLabelToTooltip(authorWidget, FluentProvider.GetMessage(CreatedBy, "author", preview.Author));

			var sizeWidget = item.GetOrNull<LabelWidget>("SIZE");
			if (sizeWidget != null)
			{
				var size = preview.Bounds.Width + "x" + preview.Bounds.Height;
				var numberPlayableCells = preview.Bounds.Width * preview.Bounds.Height;
				if (numberPlayableCells >= 120 * 120) size += " " + FluentProvider.GetMessage(MapSizeHuge);
				else if (numberPlayableCells >= 90 * 90) size += " " + FluentProvider.GetMessage(MapSizeLarge);
				else if (numberPlayableCells >= 60 * 60) size += " " + FluentProvider.GetMessage(MapSizeMedium);
				else size += " " + FluentProvider.GetMessage(MapSizeSmall);
				sizeWidget.GetText = () => size;
			}
			*/

			settingsPanel = widget.Get<ScrollPanelWidget>("SETTINGS_PANEL");
			checkboxSettingTemplate = settingsPanel.Get<Widget>("CHECKBOX_TEMPLATE");
			textSettingTemplate = settingsPanel.Get<Widget>("TEXT_TEMPLATE");
			dropdownSettingTemplate = settingsPanel.Get<Widget>("DROPDOWN_TEMPLATE");
			settingsPanel.Layout = new GridLayout(settingsPanel);

			// Tileset and map size are handled outside the generator logic so must be created manually
			var validTerrainInfos = generator.Tilesets.Select(t => modData.DefaultTerrainInfo[t]).ToList();
			var tilesetLabel = FluentProvider.GetMessage(Tileset);
			selectedTerrain ??= validTerrainInfos[0];
			tilesetSetting = dropdownSettingTemplate.Clone();
			tilesetSetting.Get<LabelWidget>("LABEL").GetText = () => tilesetLabel;

			var tilesetDropdown = tilesetSetting.Get<DropDownButtonWidget>("DROPDOWN");
			tilesetDropdown.GetText = () => selectedTerrain.Id;
			tilesetDropdown.OnMouseDown = _ =>
			{
				ScrollItemWidget SetupItem(ITerrainInfo terrainInfo, ScrollItemWidget template)
				{
					bool IsSelected() => terrainInfo == selectedTerrain;
					void OnClick()
					{
						selectedTerrain = terrainInfo;
						RefreshSettings();
						GenerateMap();
					}

					var item = ScrollItemWidget.Setup(template, IsSelected, OnClick);
					item.Get<LabelWidget>("LABEL").GetText = () => terrainInfo.Id;
					return item;
				}

				tilesetDropdown.ShowDropDown("LABEL_DROPDOWN_TEMPLATE", validTerrainInfos.Count * 30, validTerrainInfos, SetupItem);
			};

			var sizeLabel = FluentProvider.GetMessage(MapSize);
			selectedSize ??= MapSizes.Keys.Skip(1).First();
			sizeSetting = dropdownSettingTemplate.Clone();
			sizeSetting.Get<LabelWidget>("LABEL").GetText = () => sizeLabel;

			var sizeDropdown = sizeSetting.Get<DropDownButtonWidget>("DROPDOWN");
			var sizeDropdownLabel = new CachedTransform<string, string>(s => FluentProvider.GetMessage(s));
			sizeDropdown.GetText = () => sizeDropdownLabel.Update(selectedSize);
			sizeDropdown.OnMouseDown = _ =>
			{
				ScrollItemWidget SetupItem(string size, ScrollItemWidget template)
				{
					bool IsSelected() => size == selectedSize;
					void OnClick()
					{
						selectedSize = size;
						GenerateMap();
					}

					var item = ScrollItemWidget.Setup(template, IsSelected, OnClick);
					var label = FluentProvider.GetMessage(size);
					item.Get<LabelWidget>("LABEL").GetText = () => label;
					return item;
				}

				sizeDropdown.ShowDropDown("LABEL_DROPDOWN_TEMPLATE", MapSizes.Count * 30, MapSizes.Keys, SetupItem);
			};

			var generateButton = widget.Get<ButtonWidget>("BUTTON_GENERATE");
			generateButton.IsDisabled = () => generating;
			generateButton.OnClick = GenerateMap;

			RefreshSettings();
			GenerateMap();
		}

		void RefreshSettings()
		{
			settingsPanel.RemoveChildren();
			tilesetSetting.Bounds = sizeSetting.Bounds = dropdownSettingTemplate.Bounds;
			settingsPanel.AddChild(tilesetSetting);
			settingsPanel.AddChild(sizeSetting);

			var playerCount = settings.PlayerCount;
			foreach (var o in settings.Options)
			{
				if (o.Id == "Seed")
					continue;

				Widget settingWidget = null;
				switch (o)
				{
					case MapGeneratorBooleanOption bo:
					{
						settingWidget = checkboxSettingTemplate.Clone();
						var checkboxWidget = settingWidget.Get<CheckboxWidget>("CHECKBOX");
						var label = FluentProvider.GetMessage(bo.Label);
						checkboxWidget.GetText = () => label;
						checkboxWidget.IsChecked = () => bo.Value;
						checkboxWidget.OnClick = () =>
						{
							bo.Value ^= true;
							GenerateMap();
						};
						break;
					}

					case MapGeneratorIntegerOption io:
					{
						settingWidget = textSettingTemplate.Clone();
						var labelWidget = settingWidget.Get<LabelWidget>("LABEL");
						var label = FluentProvider.GetMessage(io.Label);
						labelWidget.GetText = () => label;
						var textFieldWidget = settingWidget.Get<TextFieldWidget>("INPUT");
						textFieldWidget.Type = TextFieldType.Integer;
						textFieldWidget.Text = FieldSaver.FormatValue(io.Value);
						textFieldWidget.OnTextEdited = () =>
						{
							var valid = int.TryParse(textFieldWidget.Text, out io.Value);
							textFieldWidget.IsValid = () => valid;
						};

						textFieldWidget.OnEscKey = _ => { textFieldWidget.YieldKeyboardFocus(); return true; };
						textFieldWidget.OnEnterKey = _ => { textFieldWidget.YieldKeyboardFocus(); return true; };
						textFieldWidget.OnLoseFocus = GenerateMap;
						break;
					}

					case MapGeneratorMultiIntegerChoiceOption mio:
					{
						settingWidget = dropdownSettingTemplate.Clone();
						var labelWidget = settingWidget.Get<LabelWidget>("LABEL");
						var label = FluentProvider.GetMessage(mio.Label);
						labelWidget.GetText = () => label;

						var labelCache = new CachedTransform<int, string>(v => FieldSaver.FormatValue(v));
						var dropDownWidget = settingWidget.Get<DropDownButtonWidget>("DROPDOWN");
						dropDownWidget.GetText = () => labelCache.Update(mio.Value);
						dropDownWidget.OnMouseDown = _ =>
						{
							ScrollItemWidget SetupItem(int choice, ScrollItemWidget template)
							{
								bool IsSelected() => choice == mio.Value;
								void OnClick()
								{
									mio.Value = choice;
									if (o.Id == "Players")
										RefreshSettings();
									GenerateMap();
								}

								var item = ScrollItemWidget.Setup(template, IsSelected, OnClick);
								var itemLabel = FieldSaver.FormatValue(choice);
								item.Get<LabelWidget>("LABEL").GetText = () => itemLabel;
								item.GetTooltipText = null;
								return item;
							}

							dropDownWidget.ShowDropDown("LABEL_DROPDOWN_WITH_TOOLTIP_TEMPLATE", mio.Choices.Length * 30, mio.Choices, SetupItem);
						};
						break;
					}

					case MapGeneratorMultiChoiceOption mo:
					{
						var validChoices = mo.ValidChoices(selectedTerrain, playerCount);
						if (!validChoices.Contains(mo.Value))
							mo.Value = mo.Default?.FirstOrDefault(validChoices.Contains) ?? validChoices.FirstOrDefault();

						if (mo.Label != null && validChoices.Count > 0)
						{
							settingWidget = dropdownSettingTemplate.Clone();
							var labelWidget = settingWidget.Get<LabelWidget>("LABEL");
							var label = FluentProvider.GetMessage(mo.Label);
							labelWidget.GetText = () => label;

							var labelCache = new CachedTransform<string, string>(v => FluentProvider.GetMessage(mo.Choices[v].Label + ".label"));
							var dropDownWidget = settingWidget.Get<DropDownButtonWidget>("DROPDOWN");
							dropDownWidget.GetText = () => labelCache.Update(mo.Value);
							dropDownWidget.OnMouseDown = _ =>
							{
								ScrollItemWidget SetupItem(string choice, ScrollItemWidget template)
								{
									bool IsSelected() => choice == mo.Value;
									void OnClick()
									{
										mo.Value = choice;
										GenerateMap();
									}

									var item = ScrollItemWidget.Setup(template, IsSelected, OnClick);

									var itemLabel = FluentProvider.GetMessage(mo.Choices[choice].Label + ".label");
									item.Get<LabelWidget>("LABEL").GetText = () => itemLabel;
									if (FluentProvider.TryGetMessage(mo.Choices[choice].Label + ".description", out var desc))
										item.GetTooltipText = () => desc;
									else
										item.GetTooltipText = null;

									return item;
								}

								dropDownWidget.ShowDropDown("LABEL_DROPDOWN_WITH_TOOLTIP_TEMPLATE", validChoices.Count * 30, validChoices, SetupItem);
							};
						}

						break;
					}

					default:
						throw new NotImplementedException($"Unhandled MapGeneratorOption type {o.GetType().Name}");
				}

				if (settingWidget == null)
					continue;

				settingWidget.IsVisible = () => true;
				settingsPanel.AddChild(settingWidget);
			}
		}

		void GenerateMap()
		{
			generating = true;
			failed = false;
			preview.Clear();
			Task.Run(() =>
			{
				for (var i = 0; i < 5; i++)
				{
					try
					{
						var sizeRange = MapSizes[selectedSize];
						var width = Game.CosmeticRandom.Next(sizeRange.X, sizeRange.Y);
						var height = Game.CosmeticRandom.Next(sizeRange.X, sizeRange.Y);
						var size = new Size(width + 2, height + 2);
						settings.Randomize(Game.CosmeticRandom);

						var args = settings.Compile(selectedTerrain, size);
						var map = generator.Generate(modData, args);

						// Map UID and preview image are generated on save
						var package = new ZipFileLoader.ReadWriteZipFile();
						map.Save(package);
						args.Uid = map.Uid;

						var minimap = new Png(package.GetStream("map.png"));
						Game.RunAfterTick(() =>
						{
							preview.Update(map, minimap);
							onGenerate(args);
							generating = false;
						});
						return;
					}
					catch (Exception)
					{
						// Ignore the exception
					}
				}

				failed = true;
				generating = false;
			});
		}
	}
}
