#region Copyright & License Information
/*
 * Copyright 2007-2018 The OpenRA Developers (see AUTHORS)
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
using System.Text;
using OpenRA.FileSystem;

namespace OpenRA.Mods.Common.UtilityCommands
{
	public class MapMetadata : IUtilityCommand
	{
		string IUtilityCommand.Name { get { return "--map-metadata"; } }
		bool IUtilityCommand.ValidateArguments(string[] args) { return args.Length == 2; }

		MiniYaml MergeMapOverride(Map map, MiniYaml value)
		{
			var nodes = new List<MiniYamlNode>();
			var includes = new List<string>();
			if (value != null && value.Value != null)
			{
				// The order of the included files matter, so we can defer to system files
				// only as long as they are included first.
				var include = false;
				var files = FieldLoader.GetValue<string[]>("value", value.Value);
				foreach (var f in files)
				{
					include |= map.Package.Contains(f);
					if (include)
						nodes.AddRange(MiniYaml.FromStream(map.Open(f), f, false));
					else
						includes.Add(f);
				}
			}

			if (value != null)
				nodes.AddRange(value.Nodes);

			return new MiniYaml(includes.JoinWith(", "), nodes);
		}

		MiniYaml LoadRuleSection(Dictionary<string, MiniYaml> yaml, string section)
		{
			MiniYaml node;
			if (!yaml.TryGetValue(section, out node))
				return null;

			return node;
		}

		string EncodeOverrides(Map map)
		{
			var overrides = new List<MiniYamlNode>()
			{
				new MiniYamlNode("Rules", MergeMapOverride(map, map.RuleDefinitions)),
				new MiniYamlNode("Sequences", MergeMapOverride(map, map.SequenceDefinitions)),
				new MiniYamlNode("ModelSequences", MergeMapOverride(map, map.ModelSequenceDefinitions)),
				new MiniYamlNode("Weapons", MergeMapOverride(map, map.WeaponDefinitions)),
				new MiniYamlNode("Voices", MergeMapOverride(map, map.VoiceDefinitions)),
				new MiniYamlNode("Music", MergeMapOverride(map, map.MusicDefinitions)),
				new MiniYamlNode("Notifications", MergeMapOverride(map, map.NotificationDefinitions))
			};

			return EncodeYaml(overrides);
		}

		string EncodeYaml(List<MiniYamlNode> nodes)
		{
			return Convert.ToBase64String(Encoding.UTF8.GetBytes(nodes.ToLines().JoinWith("\n")));
		}

		[Desc("MAPFILE", "Reports map metadata used by the OpenRA resource center.")]
		void IUtilityCommand.Run(Utility utility, string[] args)
		{
			var modData = Game.ModData = utility.ModData;

			var map = new Map(modData, new Folder(".").OpenPackage(args[1], modData.ModFiles));
			var players = new MapPlayers(map.PlayerDefinitions).Players;

			var spawnPoints = new List<CPos>();
			foreach (var kv in map.ActorDefinitions.Where(d => d.Value.Value == "mpspawn"))
			{
				var s = new ActorReference(kv.Value.Value, kv.Value.ToDictionary());
				spawnPoints.Add(s.InitDict.Get<LocationInit>().Value(null));
			}

			var unsafeCustomRules = Ruleset.DefinesUnsafeCustomRules(modData, map, map.RuleDefinitions,
				map.WeaponDefinitions, map.VoiceDefinitions, map.NotificationDefinitions, map.SequenceDefinitions);

			var nodes = new List<MiniYamlNode>()
			{
				new MiniYamlNode("MetadataFormat", FieldSaver.FormatValue(1)),
				new MiniYamlNode("MapFormat", FieldSaver.FormatValue(map.MapFormat)),
				new MiniYamlNode("Title", map.Title),
				new MiniYamlNode("Author", map.Author),
				new MiniYamlNode("Categories", FieldSaver.FormatValue(map.Categories)),
				new MiniYamlNode("Players", FieldSaver.FormatValue(players.Values.Count(p => p.Playable))),
				new MiniYamlNode("RequiresMod", map.RequiresMod),
				new MiniYamlNode("MapSize", FieldSaver.FormatValue(map.MapSize)),
				new MiniYamlNode("Bounds", FieldSaver.FormatValue(map.Bounds)),
				new MiniYamlNode("SpawnPoints", FieldSaver.FormatValue(spawnPoints.ToArray())),
				new MiniYamlNode("Tileset", map.Tileset),
				new MiniYamlNode("UnsafeRules", FieldSaver.FormatValue(unsafeCustomRules)),
				new MiniYamlNode("Base64Players", EncodeYaml(map.PlayerDefinitions)),
				new MiniYamlNode("Base64Overrides", EncodeOverrides(map)),
			};

			Console.WriteLine(new MiniYaml("", nodes).ToLines(map.Uid).JoinWith("\n"));
		}
	}
}
