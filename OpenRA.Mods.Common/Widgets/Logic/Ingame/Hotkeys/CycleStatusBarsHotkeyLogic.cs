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
using OpenRA.Mods.Common.Lint;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic.Ingame
{
	[ChromeLogicArgsHotkeys("CycleStatusBarsKey")]
	public class CycleStatusBarsHotkeyLogic : SingleHotkeyBaseLogic
	{
		readonly StatusBarsType[] options = [StatusBarsType.Standard, StatusBarsType.DamageShow, StatusBarsType.AlwaysShow];
		readonly GameSettings gameSettings;

		[ObjectCreator.UseCtor]
		public CycleStatusBarsHotkeyLogic(Widget widget, ModData modData, Dictionary<string, MiniYaml> logicArgs)
			: base(widget, modData, "CycleStatusBarsKey", "WORLD_KEYHANDLER", logicArgs)
		{
			gameSettings = modData.GetSettings<GameSettings>();
		}

		protected override bool OnHotkeyActivated(KeyInput e)
		{
			gameSettings.StatusBars = options[(options.IndexOf(gameSettings.StatusBars) + 1) % options.Length];
			return true;
		}
	}
}
