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
using System.Globalization;
using OpenRA.Support;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public class SystemInfoPromptLogic : ChromeLogic
	{
		// Increment the version number when adding new stats
		public const int SystemInformationVersion = 6;

		static Dictionary<string, (string Label, string Value)> GetSystemInformation(ModData modData)
		{
			var debugSettings = modData.GetSettings<DebugSettings>();
			var graphicSettings = modData.GetSettings<GraphicSettings>();
			return new Dictionary<string, (string, string)>
			{
				{ "id", ("Anonymous ID", debugSettings.UUID) },
				{ "platform", ("OS Type", Platform.CurrentPlatform.ToString()) },
				{ "os", ("OS Version", Platform.OperatingSystem) },
				{ "arch", ("Architecture", Platform.CurrentArchitecture.ToString()) },
				{ "runtime", (".NET Runtime", Platform.RuntimeVersion) },
				{ "gl", ("OpenGL Version", Game.Renderer.GLVersion) },
				{ "windowsize", ("Window Size", $"{Game.Renderer.NativeResolution.Width}x{Game.Renderer.NativeResolution.Height}") },
				{ "windowscale", ("Window Scale", Game.Renderer.NativeWindowScale.ToString("F2", CultureInfo.InvariantCulture)) },
				{ "uiscale", ("UI Scale", graphicSettings.UIScale.ToString("F2", CultureInfo.InvariantCulture)) },
				{ "lang", ("System Language", CultureInfo.InstalledUICulture.TwoLetterISOLanguageName) }
			};
		}

		public static void AddSystemInformation(ModData modData, HttpQueryBuilder queryBuilder)
		{
			queryBuilder.Add("sysinfoversion", SystemInformationVersion);
			foreach (var kv in GetSystemInformation(modData))
				queryBuilder.Add(kv.Key, kv.Value.Value);
		}

		[ObjectCreator.UseCtor]
		public SystemInfoPromptLogic(Widget widget, ModData modData, Action onComplete)
		{
			var debugSettings = modData.GetSettings<DebugSettings>();
			var sysInfoCheckbox = widget.Get<CheckboxWidget>("SYSINFO_CHECKBOX");
			sysInfoCheckbox.IsChecked = () => debugSettings.SendSystemInformation;
			sysInfoCheckbox.OnClick = () => debugSettings.SendSystemInformation ^= true;

			var sysInfoData = widget.Get<ScrollPanelWidget>("SYSINFO_DATA");
			var template = sysInfoData.Get<LabelWidget>("DATA_TEMPLATE");
			sysInfoData.RemoveChildren();

			foreach (var (name, value) in GetSystemInformation(modData).Values)
			{
				var label = template.Clone();
				var text = name + ": " + value;
				label.GetText = () => text;
				sysInfoData.AddChild(label);
			}

			widget.Get<ButtonWidget>("CONTINUE_BUTTON").OnClick = () =>
			{
				debugSettings.SystemInformationVersionPrompt = SystemInformationVersion;
				debugSettings.Save();
				Ui.CloseWindow();
				onComplete();
			};
		}
	}
}
