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
using System.IO;
using System.Linq;
using OpenRA.Mods.Common.FileFormats;

namespace OpenRA.Mods.Cnc.UtilityCommands
{
	sealed class ImportTiberianDawnMapCommand : ImportGen1MapCommand, IUtilityCommand
	{
		// NOTE: 64x64 map size is a C&C95 engine limitation
		public ImportTiberianDawnMapCommand()
			: base(64) { }

		string IUtilityCommand.Name => "--import-td-map";
		bool IUtilityCommand.ValidateArguments(string[] args) { return ValidateArguments(args); }

		[Desc("FILENAME [AUTHOR]", "Convert a legacy Tiberian Dawn INI/MPR map to the OpenRA format.")]
		void IUtilityCommand.Run(Utility utility, string[] args) { Run(utility, args); }

		public override void ValidateMapFormat(int format)
		{
			if (format > 1)
				Console.WriteLine($"ERROR: Detected NewINIFormat {format}. Are you trying to import a Red Alert map?");
		}

		static readonly Dictionary<string, (byte Type, byte Index)> OverlayResourceMapping = new()
		{
			// Tiberium
			{ "ti1", (1, 0) },
			{ "ti2", (1, 1) },
			{ "ti3", (1, 2) },
			{ "ti4", (1, 3) },
			{ "ti5", (1, 4) },
			{ "ti6", (1, 5) },
			{ "ti7", (1, 6) },
			{ "ti8", (1, 7) },
			{ "ti9", (1, 8) },
			{ "ti10", (1, 9) },
			{ "ti11", (1, 10) },
			{ "ti12", (1, 11) },
		};

		void UnpackTileData(Stream ms)
		{
			for (var j = 0; j < MapSize; j++)
			{
				for (var i = 0; i < MapSize; i++)
				{
					var type = ms.ReadUInt8();
					var index = ms.ReadUInt8();
					Map.Tiles[new CPos(i, j)] = new TerrainTile(type, index);
				}
			}
		}

		static readonly string[] OverlayActors =
		[

			// Fences
			"sbag", "cycl", "brik", "barb", "wood",

			// Fields
			"v12", "v13", "v14", "v15", "v16", "v17", "v18",

			// Crates
			"wcrate", "scrate"
		];

		void ReadOverlay(IniFile file)
		{
			var overlay = file.GetSection("OVERLAY", true);
			if (overlay == null)
				return;

			var nodes = new List<MiniYamlNode>();
			foreach (var kv in overlay)
			{
				var loc = Exts.ParseInt32Invariant(kv.Key);
				var cell = new CPos(loc % MapSize, loc / MapSize);

				var res = (Type: (byte)0, Index: (byte)0);
				var type = kv.Value.ToLowerInvariant();
				if (OverlayResourceMapping.TryGetValue(type, out var r))
					res = r;

				Map.Resources[cell] = new ResourceTile(res.Type, res.Index);
				if (OverlayActors.Contains(type))
				{
					var ar = new ActorReference(type)
					{
						new LocationInit(cell),
						new OwnerInit("Neutral")
					};

					nodes.Add(new MiniYamlNode("Actor" + (Map.ActorDefinitions.Count + nodes.Count), ar.Save()));
				}
			}

			Map.ActorDefinitions = Map.ActorDefinitions.Concat(nodes).ToArray();
		}

		public override string ParseTreeActor(string input)
		{
			var tree = input.Split(',')[0].ToLowerInvariant();

			switch (tree)
			{
				case "split2":
					return "t03.transformable";
				case "split3":
					return "t13.transformable";
				default:
					return tree;
			}
		}

		public override CPos ParseActorLocation(string input, int loc)
		{
			var newLoc = new CPos(loc % MapSize, loc / MapSize);
			var vectorDown = new CVec(0, 1);

			if (input == "obli" || input == "atwr" || input == "weap" || input == "hand" || input == "tmpl" || input == "split2" || input == "split3")
				newLoc += vectorDown;

			return newLoc;
		}

		public override void LoadPlayer(IniFile file, string section)
		{
			string color;
			string faction;
			switch (section)
			{
				case "GoodGuy":
					color = "gold";
					faction = "gdi";
					break;
				case "BadGuy":
					color = "red";
					faction = "nod";
					break;
				case "Special":
				case "Neutral":
				default:
					color = "neutral";
					faction = "gdi";
					break;
			}

			SetMapPlayers(section, faction, color, file, Players, MapPlayers);
		}

		public override void ReadPacks(IniFile file, string filename)
		{
			using (var s = File.OpenRead(filename[..^4] + ".bin"))
				UnpackTileData(s);

			ReadOverlay(file);
		}

		public override MiniYamlNode SaveWaypoint(int waypointNumber, ActorReference waypointReference)
		{
			var waypointName = "waypoint" + waypointNumber;
			if (waypointNumber == 25)
				waypointName = "DefaultFlareLocation";
			else if (waypointNumber == 26)
				waypointName = "DefaultCameraPosition";
			else if (waypointNumber == 27)
				waypointName = "DefaultChinookTarget";
			return new MiniYamlNode(waypointName, waypointReference.Save());
		}
	}
}
