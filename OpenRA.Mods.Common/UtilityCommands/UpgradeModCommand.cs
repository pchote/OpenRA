#region Copyright & License Information
/*
 * Copyright 2007-2015 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenRA.FileSystem;

namespace OpenRA.Mods.Common.UtilityCommands
{
	class UpgradeModCommand : IUtilityCommand
	{
		public string Name { get { return "--upgrade-mod"; } }

		public bool ValidateArguments(string[] args)
		{
			return args.Length >= 2;
		}

		delegate void UpgradeAction(int engineVersion, ref List<MiniYamlNode> nodes, MiniYamlNode parent, int depth);

		void ProcessYaml(string type, IEnumerable<string> files, ModData modData, int engineDate, UpgradeAction processFile)
		{
			Console.WriteLine("Processing {0}:", type);
			foreach (var filename in files)
			{
				Console.WriteLine("\t" + filename);
				string name;
				IReadOnlyPackage package;
				if (!modData.ModFiles.TryGetPackageContaining(filename, out package, out name) || !(package is Folder))
				{
					Console.WriteLine("\t\tFile cannot be opened for writing! Ignoring...");
					continue;
				}

				var yaml = MiniYaml.FromStream(package.GetStream(name));
				processFile(engineDate, ref yaml, null, 0);

				// Generate the on-disk path
				var path = Path.Combine(package.Name, name);
				using (var file = new StreamWriter(path))
					file.Write(yaml.WriteToString());
			}
		}

		[Desc("CURRENTENGINE", "Upgrade mod rules to the latest engine version.")]
		public void Run(ModData modData, string[] args)
		{
			// HACK: The engine code assumes that Game.modData is set.
			Game.ModData = modData;
			modData.MapCache.LoadMaps();

			var engineDate = Exts.ParseIntegerInvariant(args[1]);

			ProcessYaml("Rules", modData.Manifest.Rules, modData, engineDate, UpgradeRules.UpgradeActorRules);
			ProcessYaml("Weapons", modData.Manifest.Weapons, modData, engineDate, UpgradeRules.UpgradeWeaponRules);
			ProcessYaml("Tilesets", modData.Manifest.TileSets, modData, engineDate, UpgradeRules.UpgradeTileset);
			ProcessYaml("Cursors", modData.Manifest.Cursors, modData, engineDate, UpgradeRules.UpgradeCursors);
			ProcessYaml("Chrome Metrics", modData.Manifest.ChromeMetrics, modData, engineDate, UpgradeRules.UpgradeChromeMetrics);
			ProcessYaml("Chrome Layout", modData.Manifest.ChromeLayout, modData, engineDate, UpgradeRules.UpgradeChromeLayout);

			Console.WriteLine("Processing Maps:");
			var mapPaths = modData.MapCache
				.Where(m => m.Status == MapStatus.Available)
				.Select(m => m.Path);

			foreach (var path in mapPaths)
			{
				Console.WriteLine("\t" + path);
				UpgradeRules.UpgradeMapFormat(modData, path);

				var map = new Map(path);
				UpgradeRules.UpgradeActorRules(engineDate, ref map.RuleDefinitions, null, 0);
				UpgradeRules.UpgradeWeaponRules(engineDate, ref map.WeaponDefinitions, null, 0);
				UpgradeRules.UpgradePlayers(engineDate, ref map.PlayerDefinitions, null, 0);
				UpgradeRules.UpgradeActors(engineDate, ref map.ActorDefinitions, null, 0);
				map.Save(map.Path);
			}
		}
	}
}
