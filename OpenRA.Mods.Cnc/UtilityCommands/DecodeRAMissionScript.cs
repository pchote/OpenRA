#region Copyright & License Information
/*
 * Copyright 2007-2020 The OpenRA Developers (see AUTHORS)
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
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Mods.Cnc.UtilityCommands
{
	class DecodeRAMissionScript : IUtilityCommand
	{
		// References:
		// https://github.com/electronicarts/CnC_Remastered_Collection/
		// https://gamefaqs.gamespot.com/pc/196962-command-and-conquer-red-alert/faqs/1701
		public static readonly string[] HouseTypes =
		{
			"Spain", "Greece", "USSR", "England", "Ukraine", "Germany", "France", "Turkey",
			"Good", "Bad", "Neutral", "Special",
			"Multi1", "Multi2", "Multi3", "Multi4", "Multi5", "Multi6", "Multi7", "Multi8"
		};

		public static readonly string[] EventTypes =
		{
			"NONE", "PLAYER_ENTERED", "SPIED", "THIEVED", "DISCOVERED", "HOUSE_DISCOVERED", "ATTACKED", "DESTROYED", "ANY", "UNITS_DESTROYED",
			"BUILDINGS_DESTROYED", "ALL_DESTROYED", "CREDITS", "TIME", "MISSION_TIMER_EXPIRED", "NBUILDINGS_DESTROYED", "NUNITS_DESTROYED",
			"NOFACTORIES", "EVAC_CIVILIAN", "BUILD", "BUILD_UNIT", "BUILD_INFANTRY", "BUILD_AIRCRAFT", "LEAVES_MAP", "ENTERS_ZONE", "CROSS_HORIZONTAL",
			"CROSS_VERTICAL", "GLOBAL_SET", "GLOBAL_CLEAR", "FAKES_DESTROYED", "LOW_POWER", "ALL_BRIDGES_DESTROYED", "BUILDING_EXISTS"
		};

		public static readonly string[] BuildingTypes =
		{
			"ATEK", "IRON", "WEAP", "PDOX", "PBOX", "HBOX", "DOME", "GAP", "GUN", "AGUN", "FTUR", "FACT", "PROC", "SILO", "HPAD", "SAM",
			"AFLD", "POWR", "APWR", "STEK", "HOSP", "BARR", "TENT", "KENN", "FIX", "BIO", "MISS", "SYRD", "SPEN", "MSLO", "FCOM", "TSLA",
			"WEAF", "FACF", "SYRF", "SPEF", "DOMF"
		};

		public static readonly string[] UnitTypes =
		{
			"4TNK", "3TNK", "2TNK", "1TNK", "APC", "MNLY", "JEEP", "HARV", "ARTY", "MRJ", "MGG", "MCV", "V2RL", "TRUK", "ANT1", "ANT2", "ANT3"
		};

		public static readonly string[] InfantryTypes =
		{
			"E1", "E2", "E3", "E4", "E6", "E7", "SPY", "THF", "MEDI", "GNRL", "DOG",
			"C1", "C2", "C3", "C4", "C5", "C6", "C7", "C8", "C9", "C10", "EINSTEIN", "DELPHI", "CHAN"
		};

		public static readonly string[] AircraftTypes =
		{
			"TRAN", "BADR", "U2", "MIG", "YAK", "HELI", "HIND"
		};

		public static readonly string[] ActionTypes =
		{
			"NONE", "WIN", "LOSE", "BEGIN_PRODUCTION", "CREATE_TEAM", "DESTROY_TEAM", "ALL_HUNT", "REINFORCEMENTS", "DZ",
			"FIRE_SALE", "PLAY_MOVIE", "TEXT_TRIGGER", "DESTROY_TRIGGER", "AUTOCREATE", "WINLOSE", "ALLOWWIN", "REVEAL_ALL",
			"REVEAL_SOME", "REVEAL_ZONE", "PLAY_SOUND", "PLAY_MUSIC", "PLAY_SPEECH", "FORCE_TRIGGER", "START_TIMER",
			"STOP_TIMER", "ADD_TIMER", "SUB_TIMER", "SET_TIMER", "SET_GLOBAL", "CLEAR_GLOBAL", "BASE_BUILDING",
			"CREEP_SHADOW", "DESTROY_OBJECT", "ONE_SPECIAL", "FULL_SPECIAL", "PREFERRED_TARGET", "LAUNCH_NUKES"
		};

		public static readonly string[] MovieTypes =
		{
			"AAGUN", "MIG", "SFROZEN", "AIRFIELD", "BATTLE", "BMAP", "BOMBRUN", "DPTHCHRG", "GRVESTNE", "MONTPASS", "MTNKFACT", "CRONTEST",
			"OILDRUM", "ALLYEND", "RADRRAID", "SHIPYARD", "SHORBOMB", "SITDUCK", "SLNTSRVC", "SNOWBASE", "EXECUTE", "REDINTRO", "NUKESTOK",
			"V2ROCKET", "SEARCH", "BINOC", "ELEVATOR", "FROZEN", "MCV", "SHIPSINK", "SOVMCV", "TRINITY", "ALLYMORF", "APCESCPE", "BRDGTILT",
			"CRONFAIL", "STRAFE", "DESTROYR", "DOUBLE", "FLARE", "SNSTRAFE", "LANDING", "ONTHPRWL", "OVERRUN", "SNOWBOMB", "SOVCEMET",
			"TAKE_OFF", "TESLA", "SOVIET8", "SPOTTER", "ALLY1", "ALLY2", "ALLY4", "SOVFINAL", "ASSESS", "SOVIET10", "DUD", "MCV_LAND",
			"MCVBRDGE", "PERISCOP", "SHORBOM1", "SHORBOM2", "SOVBATL", "SOVTSTAR", "AFTRMATH", "SOVIET11", "MASASSLT", "ENGLISH",
			"SOVIET1", "SOVIET2", "SOVIET3", "SOVIET4", "SOVIET5", "SOVIET6", "SOVIET7", "PROLOG", "AVERTED", "COUNTDWN", "MOVINGIN",
			"ALLY10", "ALLY12", "ALLY5", "ALLY6", "ALLY8", "TANYA1", "TANYA2", "ALLY10B", "ALLY11", "ALLY14", "ALLY9", "SPY", "TOOFAR",
			"SOVIET12", "SOVIET13", "SOVIET9", "BEACHEAD", "SOVIET14", "SIZZLE", "SIZZLE2", "ANTEND", "ANTINTRO"
		};

		public static readonly string[] SoundTypes =
		{
			"GIRLOKAY", "GIRLYEAH", "GUYOKAY1", "GUYYEAH1", "MINELAY1", "ACKNO", "AFFIRM1", "AWAIT1", "EAFFIRM1", "EENGIN1", "NOPROB",
			"READY", "REPORT1", "RITAWAY", "ROGER", "UGOTIT", "VEHIC1", "YESSIR1", "DEDMAN1", "DEDMAN2", "DEDMAN3", "DEDMAN4", "DEDMAN5",
			"DEDMAN6", "DEDMAN7", "DEDMAN8", "DEDMAN10", "CHRONO2", "CANNON1", "CANNON2", "IRONCUR9", "EMOVOUT1", "SONPULSE", "SANDBAG2",
			"MINEBLO1", "CHUTE1", "DOGY1", "DOGW5", "DOGG5P", "FIREBL3", "FIRETRT1", "GRENADE1", "GUN11", "GUN13", "EYESSIR1", "GUN27",
			"HEAL2", "HYDROD1", "INVUL2", "KABOOM1", "KABOOM12", "KABOOM15", "SPLASH9", "KABOOM22", "AACANON3", "TANDETH1", "MGUNINF1",
			"MISSILE1", "MISSILE6", "MISSILE7", "UNUSED1", "PILLBOX1", "RABEEP1", "RAMENU1", "SILENCER", "TANK5", "TANK6", "TORPEDO1",
			"TURRET1", "TSLACHG2", "TESLA1", "SQUISHY2", "SCOLDY1", "RADARON2", "RADARDN1", "PLACBLDG", "KABOOM30", "KABOOM25",
			"UNUSED2", "DOGW7", "DOGW3PX", "CRMBLE2", "CASHUP1", "CASHDN1", "BUILD5", "BLEEP9", "BLEEP6", "BLEEP5", "BLEEP17", "BLEEP13",
			"BLEEP12", "BLEEP11", "H2OBOMB2", "CASHTURN", "TUFFGUY1", "ROKROLL1", "LAUGH1", "CMON1", "BOMBIT1", "GOTIT1", "KEEPEM1",
			"ONIT1", "LEFTY1", "YEAH1", "YES1", "YO1", "WALLKIL2", "UNUSED3", "GUN5", "SUBSHOW1", "EINAH1", "EINOK1", "EINYES1", "MINE1",
			"SCOMND1", "SYESSIR1", "SINDEED1", "SONWAY1", "SKING1", "MRESPON1", "MYESSIR1", "MAFFIRM1", "MMOVOUT1", "BEEPSLCT", "SYEAH1",
			"UNUSED4", "UNUSED5", "SMOUT1", "SOKAY1", "UNUSED6", "SWHAT1", "SAFFIRM1", "STAVCMDR", "STAVCRSE", "STAVYES", "STAVMOV",
			"BUZY1", "RAMBO1", "RAMBO2", "RAMBO3"
		};

		public static readonly string[] MusicTypes =
		{
			"BIGF226M", "CRUS226M", "FAC1226M", "FAC2226M", "HELL226M", "RUN1226M", "SMSH226M", "TREN226M", "WORK226M", "DENSE_R",
			"FOGGER1A", "MUD1A", "RADIO2", "ROLLOUT", "SNAKE", "TERMINAT", "TWIN", "VERTOR1A", "MAP", "SCORE", "INTRO", "CREDITS",
			"_2ND_HAND", "ARAZOID", "BACKSTAB", "CHAOS2", "SHUT_IT", "TWINMIX1", "UNDER3", "VR2"
		};

		public static readonly string[] SpeechTypes =
		{
			"MISNWON1", "MISNLST1", "PROGRES1", "CONSCMP1", "UNITRDY1", "NEWOPT1", "NODEPLY1", "STRCKIL1", "NOPOWR1", "NOFUNDS1", "BCT1",
			"REINFOR1", "CANCLD1", "ABLDGIN1", "LOPOWER1", "NOFUNDS1_", "BASEATK1", "NOBUILD1", "PRIBLDG1", "UNUSED1", "UNUSED2",
			"UNITLST1", "SLCTTGT1", "ENMYAPP1", "SILOND1", "ONHOLD1", "REPAIR1", "UNUSED3", "UNUSED4", "AUNITL1", "UNUSED5", "AAPPRO1",
			"AARRIVE1", "UNUSED6", "UNUSED7", "BLDGINF1", "CHROCHR1", "CHRORDY1", "CHROYES1", "CMDCNTR1", "CNTLDED1", "CONVYAP1",
			"CONVLST1", "XPLOPLC1", "CREDIT1", "NAVYLST1", "SATLNCH1", "PULSE1", "UNUSED8", "SOVFAPP1", "SOVREIN1", "TRAIN1", "AREADY1",
			"ALAUNCH1", "AARRIVN1", "AARRIVS1", "AARIVE1", "AARRIVW1", "_1OBJMET1", "_2OBJMET1", "_3OBJMET1", "IRONCHG1", "IRONRDY1",
			"KOSYRES1", "OBJNMET1", "FLAREN1", "FLARES1", "FLAREE1", "FLAREW1", "SPYPLN1", "TANYAF1", "ARMORUP1", "FIREPO1", "UNITSPD1",
			"MTIMEIN1", "UNITFUL1", "UNITREP1", "_40MINR", "_30MINR", "_20MINR", "_10MINR", "_5MINR", "_4MINR", "_3MINR", "_2MINR",
			"_1MINR", "TIMERNO1", "UNITSLD1", "TIMERGO1", "TARGRES1", "TARGFRE1", "TANYAR1", "STRUSLD1", "SOVFORC1", "SOVEMP1",
			"SOVEFAL1", "OPTERM1", "OBJRCH1", "OBJNRCH1", "OBJMET1", "MERCR1", "MERCF1", "KOSYFRE1", "FLARE1", "COMNDOR1", "COMNDOF1",
			"BLDGPRG1", "ATPREP1", "ASELECT1", "APREP1", "ATLNCH1", "AFALLEN1", "AAVAIL1", "AARRIVE1_", "SAVE1", "LOAD1"
		};

		public static readonly string[] SpecialWeaponTypes =
		{
			"SONAR_PULSE", "NUCLEAR_BOMB", "CHRONOSPHERE", "PARA_BOMB",
			"PARA_INFANTRY", "SPY_MISSION", "IRON_CURTAIN", "GPS"
		};

		public static readonly string[] QuarryTypes =
		{
			"NONE", "ANYTHING", "BUILDINGS", "HARVESTERS", "INFANTRY", "VEHICLES",
			"VESSELS", "FACTORIES", "DEFENSE", "THREAT", "POWER", "FAKES"
		};

		public static readonly string[] TargetTypes =
		{
			"Unknown", "Buildings (any)", "Harvesters", "Infantry", "Vehicles (any)", "Ships (any)",
			"Factories", "Base Defences", "Base Threats", "Power Facilities", "Fake Buildings"
		};

		public static readonly string[] UnitCommands =
		{
			"Sleep", "Attack", "Move", "QMove", "Retreat", "Guard", "Sticky", "Enter", "Capture", "Harvest", "Area Guard", "Return",
			"Stop", "Ambush", "Hunt", "Unload", "Sabotage", "Construction", "Selling", "Repair", "Rescue", "Missile", "Harmless"
		};

		public static readonly string[] Formations =
		{
			"NONE", "TIGHT", "LOOSE", "WEDGE_N", "WEDGE_E",
			"WEDGE_S", "WEDGE_W", "LINE_NS", "LINE_EW",
		};

		public static string FormatListValue(string[] list, int index)
		{
			if (index < 0 || index >= list.Length)
				return $"UNKNOWN ({index})";

			return list[index];
		}

		static string FormatTeam(List<Team> teams, int index)
		{
			if (index < 0 || index >= teams.Count)
				return "UNKNOWN ({0})".F(index);

			return teams[index].Name;
		}

		static string FormatTrigger(List<Trigger> triggers, int index)
		{
			if (index < 0 || index >= triggers.Count)
				return "UNKNOWN ({0})".F(index);

			return triggers[index].Name;
		}

		static string FormatWaypoint(Dictionary<int, CPos> waypoints, int index)
		{
			if (!waypoints.TryGetValue(index, out var waypoint))
				return "UNKNOWN ({0})".F(index);

			return $"\"waypoint{index}\" ({waypoint})";
		}

		public class Event
		{
			readonly string type;
			readonly int team;
			readonly int data;

			public Event(string typeToken, string teamToken, string dataToken)
			{
				type = FormatListValue(EventTypes, FieldLoader.GetValue<int>("type", typeToken));
				team = FieldLoader.GetValue<int>("team", teamToken);
				data = FieldLoader.GetValue<int>("data", dataToken);

				// Fix weirdly formatted data caused by union usage in the original game
				if (type != "CREDITS" && type != "TIME" && type != "NBUILDINGS_DESTROYED" && type != "NUNITS_DESTROYED")
					data = (byte)data;
			}

			public string ToString(string house, List<Team> teams)
			{
				switch (type)
				{
					case "NONE": return null;
					case "PLAYER_ENTERED": return $"Attached cell entered or attached building captured by house \"{FormatListValue(HouseTypes, data)}\"";
					case "SPIED": return "Attached building Infiltrated by spy";
					case "THIEVED": return $"Attached building Infiltrated by thief owned by house \"{FormatListValue(HouseTypes, data)}\"";
					case "DISCOVERED": return "Attached unit or building discovered by the player";
					case "HOUSE_DISCOVERED": return $"Any unit or building owned by house \"{FormatListValue(HouseTypes, data)}\" discovered by the player";
					case "ATTACKED": return "Attached unit or building is attacked";
					case "DESTROYED": return "Attached unit or building is destroyed";
					case "ANY": return "Any other event triggers";
					case "UNITS_DESTROYED": return $"All units owned by house \"{FormatListValue(HouseTypes, data)}\" are destroyed";
					case "BUILDINGS_DESTROYED": return $"All buildings (excluding civilian) owned by house \"{FormatListValue(HouseTypes, data)}\" are destroyed";
					case "ALL_DESTROYED": return $"All buildings (excluding civilian) and units owned by house \"{FormatListValue(HouseTypes, data)}\" are destroyed";
					case "CREDITS": return $"House \"{house}\" has more than {data} credits";
					case "TIME": return $"{data / 10f:F1} minutes have elapsed";
					case "MISSION_TIMER_EXPIRED": return "Mission timer expired";
					case "NBUILDINGS_DESTROYED": return $"{data} buildings (including civilian) owned by house \"{house}\" are destroyed";
					case "NUNITS_DESTROYED": return $"{data} units owned by house \"{house}\" are destroyed";
					case "NOFACTORIES": return $"House \"{house}\" has not buildings of type FACT,AFLD,BARR,TENT,WEAP remaining";
					case "EVAC_CIVILIAN": return $"Civilian unit owned by \"{house}\" left the map or loaded into helicopter.";
					case "BUILD": return $"House \"{house}\" has built building of type \"{FormatListValue(BuildingTypes, data)}\"";
					case "BUILD_UNIT": return $"House \"{house}\" has built vehicle of type \"{FormatListValue(UnitTypes, data)}\"";
					case "BUILD_INFANTRY": return $"House \"{house}\" has built infantry of type \"{FormatListValue(InfantryTypes, data)}\"";
					case "BUILD_AIRCRAFT": return $"House \"{house}\" has built aircraft of type \"{FormatListValue(AircraftTypes, data)}\"";
					case "LEAVES_MAP": return $"Team \"{FormatTeam(teams, team)}\" leaves the map";
					case "ENTERS_ZONE": return $"A unit owned by house \"{FormatListValue(HouseTypes, data)}\" enters zone of attached cell";
					case "CROSS_HORIZONTAL": return $"A unit owned by house \"{FormatListValue(HouseTypes, data)}\" crosses a horizontal line through the attached cell";
					case "CROSS_VERTICAL": return $"A unit owned by house \"{FormatListValue(HouseTypes, data)}\" crosses a vertical line through the attached cell";
					case "GLOBAL_SET": return $"Global scenario variable {data} is set";
					case "GLOBAL_CLEAR": return $"Global scenario variable {data} is not set";
					case "FAKES_DESTROYED": return "All fake structures have been destroyed (does not work!)";
					case "LOW_POWER": return $"House \"{FormatListValue(HouseTypes, data)}\" is low power";
					case "ALL_BRIDGES_DESTROYED": return "All bridges on map destroyed";
					case "BUILDING_EXISTS": return $"House \"{house}\" owns building of type \"{FormatListValue(BuildingTypes, data)}\"";
					default:
						return $"UNKNOWN EVENT - Type: {type} Team: {team} Data: {data}";
				}
			}
		}

		public class Action
		{
			readonly string type;
			readonly int trigger;
			readonly int team;
			readonly int data;

			public Action(string typeToken, string teamToken, string triggerToken, string dataToken)
			{
				type = FormatListValue(ActionTypes, FieldLoader.GetValue<int>("type", typeToken));
				team = FieldLoader.GetValue<int>("team", teamToken);
				trigger = FieldLoader.GetValue<int>("trigger", triggerToken);
				data = FieldLoader.GetValue<int>("data", dataToken);

				// Fix weirdly formatted data caused by union usage in the original game
				if (type != "TEXT_TRIGGER")
					data = (byte)data;
			}

			public string ToString(string house, List<Trigger> allTriggers, Dictionary<int, CPos> waypoints, List<Team> teams)
			{
				switch (type)
				{
					case "NONE": return null;
					case "WIN": return $"House \"{FormatListValue(HouseTypes, data)}\" is victorious";
					case "LOSE": return $"House \"{FormatListValue(HouseTypes, data)}\" is defeated";
					case "BEGIN_PRODUCTION": return $"Enable production for house \"{FormatListValue(HouseTypes, data)}\"";
					case "CREATE_TEAM": return $"Create team \"{FormatTeam(teams, team)}\"";
					case "DESTROY_TEAM": return $"Disband team \"{FormatTeam(teams, team)}\"";
					case "ALL_HUNT": return $"All units owned by house \"{FormatListValue(HouseTypes, data)}\" hunt for enemies";
					case "REINFORCEMENTS": return $"Reinforce with team \"{FormatTeam(teams, team)}\"";
					case "DZ": return $"Spawn flare with small vision radius at {FormatWaypoint(waypoints, data)}";
					case "FIRE_SALE": return $"House \"{FormatListValue(HouseTypes, data)}\" sells all buildings";
					case "PLAY_MOVIE": return $"Play Movie {FormatListValue(MovieTypes, data)}";
					case "TEXT_TRIGGER": return $"Display text message {data} from tutorial.ini";
					case "DESTROY_TRIGGER": return $"Disable trigger \"{FormatTrigger(allTriggers, trigger)}\"";
					case "AUTOCREATE": return $"Enable AI autocreate for house \"{FormatListValue(HouseTypes, data)}\"";
					case "WINLOSE": return "~don't use~ (does not work!)";
					case "ALLOWWIN": return "Allow player to win";
					case "REVEAL_ALL": return "Reveal entire map to player";
					case "REVEAL_SOME": return $"Reveal area around {FormatWaypoint(waypoints, data)}";
					case "REVEAL_ZONE": return $"Reveal zone of {FormatWaypoint(waypoints, data)}";
					case "PLAY_SOUND": return $"Play sound effect \"{FormatListValue(SoundTypes, data)}\"";
					case "PLAY_MUSIC": return $"Play music track \"{FormatListValue(MusicTypes, data)}\"";
					case "PLAY_SPEECH": return $"Play speech message \"{FormatListValue(SpeechTypes, data)}\"";
					case "FORCE_TRIGGER": return $"Run trigger \"{FormatTrigger(allTriggers, trigger)}\"";
					case "START_TIMER": return "Start mission timer";
					case "STOP_TIMER": return "Stop mission timer";
					case "ADD_TIMER": return $"Add {data / 10f:F1} minutes to the mission timer";
					case "SUB_TIMER": return $"Subtract {data / 10f:F1} minutes from the mission timer";
					case "SET_TIMER": return $"Start mission timer with {data / 10f:F1} minutes remaining";
					case "SET_GLOBAL": return $"Set global scenario variable {data}";
					case "CLEAR_GLOBAL": return $"Clear global scenario variable {data}";
					case "BASE_BUILDING": return $"Enable automatic base building for house \"{FormatListValue(HouseTypes, data)}\"";
					case "CREEP_SHADOW": return "Grow shroud one step";
					case "DESTROY_OBJECT": return "Destroy attached buildings";
					case "ONE_SPECIAL": return $"Enable one-shot superweapon \"{FormatListValue(SpecialWeaponTypes, data)}\" for house \"{house}\"";
					case "FULL_SPECIAL": return $"Enable repeating superweapon \"{FormatListValue(SpecialWeaponTypes, data)}\" for house \"{house}\"";
					case "PREFERRED_TARGET": return $"Set preferred target for house \"{house}\" to  \"{FormatListValue(QuarryTypes, data)}\"";
					case "LAUNCH_NUKES": return "Animate A-bomb launch";
					default:
						return $"UNKNOWN ACTION - Action {type} Team: {team} Trigger: {trigger} Data: {data}";
				}
			}
		}

		public class Trigger
		{
			enum TriggerPersistantType { Volatile, SemiPersistant, Persistant }
			enum TriggerMultiStyleType { Only, And, Or, Linked }

			public readonly string Name;
			readonly TriggerPersistantType persistantType;
			readonly string house;
			readonly TriggerMultiStyleType eventControl;
			readonly Event event1;
			readonly Event event2;
			readonly Action action1;
			readonly Action action2;

			public Trigger(string key, string value)
			{
				var tokens = value.Split(',');
				if (tokens.Length != 18)
					throw new InvalidDataException("Trigger {0} does not have 18 tokens.".F(key));

				Name = key;
				persistantType = (TriggerPersistantType)int.Parse(tokens[0]);
				house = tokens[1];
				eventControl = (TriggerMultiStyleType)int.Parse(tokens[2]);
				event1 = new Event(tokens[4], tokens[5], tokens[6]);
				event2 = new Event(tokens[7], tokens[8], tokens[9]);
				action1 = new Action(tokens[10], tokens[11], tokens[12], tokens[13]);
				action2 = new Action(tokens[14], tokens[15], tokens[16], tokens[17]);
			}

			public MiniYaml Serialize(List<Trigger> triggers, Dictionary<int, CPos> waypoints, List<Team> teams)
			{
				var yaml = new MiniYaml("");

				var eventNode = new MiniYamlNode("On", "");
				yaml.Nodes.Add(eventNode);
				eventNode.Value.Nodes.Add(new MiniYamlNode("", event1.ToString(house, teams) ?? "Manual Trigger"));
				if (eventControl != TriggerMultiStyleType.Only)
				{
					var type = eventControl == TriggerMultiStyleType.And ? "AND" :
						eventControl == TriggerMultiStyleType.Or ? "OR" : "LINKED";

					eventNode.Value.Nodes.Add(new MiniYamlNode("", type));
					eventNode.Value.Nodes.Add(new MiniYamlNode("", event2.ToString(house, teams)));
				}

				var actionsNode = new MiniYamlNode("Actions", "");
				yaml.Nodes.Add(actionsNode);

				var action1Label = action1.ToString(house, triggers, waypoints, teams);
				if (action1Label != null)
					actionsNode.Value.Nodes.Add(new MiniYamlNode("", action1Label));

				var action2Label = action2.ToString(house, triggers, waypoints, teams);
				if (action2Label != null)
					actionsNode.Value.Nodes.Add(new MiniYamlNode("", action2Label));

				var expires = persistantType == TriggerPersistantType.Persistant ? "Never" :
					persistantType == TriggerPersistantType.SemiPersistant ? "After running on all attached objects" :
					"After running once";
				yaml.Nodes.Add(new MiniYamlNode("Disable", expires));

				return yaml;
			}
		}

		public class Team
		{
			string FormatMission(int type, int data, Dictionary<int, CPos> waypoints)
			{
				switch (type)
				{
					case 0: return $"Attack target type \"{FormatListValue(TargetTypes, data)}\"";
					case 1: return $"Attack {FormatWaypoint(waypoints, data)}";
					case 2: return $"Change formation to \"{FormatListValue(Formations, data)}\"";
					case 3: return $"Move to {FormatWaypoint(waypoints, data)}";
					case 4: return $"Move to cell ({new CPos(data % 128, data / 128)})";
					case 5: return $"Guard area for {data / 10f:1f} minutes";
					case 6: return $"Jump to line {data}";
					case 7: return $"Attack Tarcom";
					case 8: return $"Transports unload cargo / Minelayers lay mine";
					case 9: return $"Deploy MCV";
					case 10: return $"Follow nearest friendly unit";
					case 11: return $"Do \"{FormatListValue(UnitCommands, data)}\"";
					case 12: return $"Set global variable \"{data}\"";
					case 13: return $"Become invulnerable (iron curtain effect)";
					case 14: return $"Load into team transport";
					case 15: return $"Spy on building at {FormatWaypoint(waypoints, data)}";
					case 16: return $"Patrol (attack-move) to {FormatWaypoint(waypoints, data)}";
				}

				return "Unknown";
			}

			[Flags]
			enum FlagsType
			{
				None = 0,
				IsRoundAbout = 0x01,
				IsSuicide = 0x02,
				IsAutocreate = 0x04,
				IsPrebuilt = 0x08,
				IsReinforcable = 0x10,
			}

			public readonly string Name;
			public readonly int Trigger;
			readonly string house;
			readonly FlagsType flags;
			readonly List<string> units = new List<string>();
			readonly List<(int Type, int Data)> mission = new List<(int, int)>();

			readonly int recruitPriority;
			readonly int initNum;
			readonly int maxAllowed;
			readonly int origin;

			public Team(string key, string value)
			{
				Name = key;

				var tokens = value.Split(',');
				house = tokens[0];
				flags = FieldLoader.GetValue<FlagsType>("Flags", tokens[1]);

				recruitPriority = int.Parse(tokens[2]);
				initNum = byte.Parse(tokens[3]);
				maxAllowed = byte.Parse(tokens[4]);
				origin = int.Parse(tokens[5]);
				Trigger = int.Parse(tokens[6]);

				var numClasses = int.Parse(tokens[7]);
				for (int i = 0; i < numClasses; i++)
				{
					var classTokens = tokens[8 + i].Split(':');
					var count = FieldLoader.GetValue<int>("token", classTokens[1]);
					for (var j = 0; j < count; j++)
						units.Add(classTokens[0]);
				}

				var numMissions = int.Parse(tokens[8 + numClasses]);
				for (int i = 0; i < numMissions; i++)
				{
					var missionTokens = tokens[9 + numClasses + i].Split(':');
					mission.Add((
						FieldLoader.GetValue<int>("Type", missionTokens[0]),
						FieldLoader.GetValue<int>("Data", missionTokens[1])));
				}
			}

			public MiniYaml Serialize(List<Trigger> triggers, Dictionary<int, CPos> waypoints, List<Team> teams)
			{
				var yaml = new MiniYaml("");
				yaml.Nodes.Add(new MiniYamlNode("Units", FieldSaver.FormatValue(units)));
				yaml.Nodes.Add(new MiniYamlNode("House", FieldSaver.FormatValue(house)));

				if (Trigger != -1)
					yaml.Nodes.Add(new MiniYamlNode("Trigger", triggers[Trigger].Name));

				yaml.Nodes.Add(new MiniYamlNode("Flags", FieldSaver.FormatValue(flags)));
				yaml.Nodes.Add(new MiniYamlNode("RecruitPriority", FieldSaver.FormatValue(recruitPriority)));
				yaml.Nodes.Add(new MiniYamlNode("InitNum", FieldSaver.FormatValue(initNum)));
				yaml.Nodes.Add(new MiniYamlNode("MaxAllowed", FieldSaver.FormatValue(maxAllowed)));
				if (origin != -1)
					yaml.Nodes.Add(new MiniYamlNode("Origin", FormatWaypoint(waypoints, origin)));

				if (mission.Any())
				{
					var missionYaml = new MiniYaml("");
					yaml.Nodes.Add(new MiniYamlNode("Mission", missionYaml));
					for (var i = 0; i < mission.Count; i++)
						missionYaml.Nodes.Add(new MiniYamlNode(i.ToString(), FormatMission(mission[i].Type, mission[i].Data, waypoints)));
				}

				return yaml;
			}
		}

		string IUtilityCommand.Name { get { return "--decode-ra-mission"; } }
		bool IUtilityCommand.ValidateArguments(string[] args)
		{
			return args.Length >= 2;
		}

		static CPos ParseCell(string token)
		{
			var cellNumber = FieldLoader.GetValue<int>("value", token);
			return new CPos(cellNumber % 128, cellNumber / 128);
		}

		[Desc("FILENAME", "Describe the triggers and teamptypes from a legacy Red Alert INI/MPR map.")]
		void IUtilityCommand.Run(Utility utility, string[] args)
		{
			// HACK: The engine code assumes that Game.modData is set.
			Game.ModData = utility.ModData;

			var filename = args[1];
			using (var stream = File.OpenRead(filename))
			{
				var file = new IniFile(stream);
				var waypoints = new Dictionary<int, CPos>();
				var cellTriggers = new Dictionary<string, List<CPos>>();
				var triggers = new List<Trigger>();
				var teams = new List<Team>();

				foreach (var t in file.GetSection("Waypoints"))
				{
					var waypointNumber = FieldLoader.GetValue<int>("key", t.Key);
					var cellNumber = FieldLoader.GetValue<int>("value", t.Value);
					waypoints[waypointNumber] = new CPos(cellNumber % 128, cellNumber / 128);
				}

				foreach (var t in file.GetSection("CellTriggers"))
				{
					var cellNumber = FieldLoader.GetValue<int>("key", t.Key);
					cellTriggers.GetOrAdd(t.Value).Add(new CPos(cellNumber % 128, cellNumber / 128));
				}

				foreach (var t in file.GetSection("Trigs"))
					triggers.Add(new Trigger(t.Key, t.Value));

				foreach (var t in file.GetSection("TeamTypes"))
					teams.Add(new Team(t.Key, t.Value));

				// NOTE: Must be kept in sync with ImportLegacyMapCommand for Actor* names to match
				var actorCount = 0;
				var scriptedActors = new List<MiniYamlNode>();
				foreach (var section in new[] { "STRUCTURES", "UNITS", "INFANTRY", "SHIPS" })
				{
					foreach (var s in file.GetSection(section, true))
					{
						try
						{
							var parts = s.Value.Split(',');
							var owner = parts[0];
							var actorType = parts[1].ToLowerInvariant();
							var location = ParseCell(parts[3]);

							var action = "None";
							var trigger = "None";
							var sellable = false;
							var rebuildable = false;
							switch (section)
							{
								case "STRUCTURES":
									trigger = parts[5];
									sellable = FieldLoader.GetValue<int>("sellable", parts[6]) > 0;
									rebuildable = FieldLoader.GetValue<int>("rebuildable", parts[7]) > 0;
									break;
								case "INFANTRY":
									action = parts[5];
									trigger = parts[7];
									break;
								case "UNITS":
								case "SHIPS":
									action = parts[5];
									trigger = parts[6];
									break;
							}

							var actorNode = new MiniYamlNode($"ScriptedActor@Actor{actorCount}", "", $" {actorType} owned by house \"{owner}\" at cell {location}");

							if (!Game.ModData.DefaultRules.Actors.ContainsKey(actorType))
								Console.WriteLine("Ignoring unknown actor type: `{0}`".F(parts[1].ToLowerInvariant()));
							else
							{
								if (action != "None")
									actorNode.Value.Nodes.Add(new MiniYamlNode("Action", action));

								if (trigger != "None")
									actorNode.Value.Nodes.Add(new MiniYamlNode("Trigger", trigger));

								if (section == "STRUCTURES" && owner != "Neutral")
									actorNode.Value.Nodes.Add(new MiniYamlNode("Sellable", FieldSaver.FormatValue(sellable)));

								if (rebuildable)
									actorNode.Value.Nodes.Add(new MiniYamlNode("Rebuildable", "true"));

								if (actorNode.Value.Nodes.Any())
									scriptedActors.Add(actorNode);

								actorCount += 1;
							}
						}
						catch (Exception)
						{
							Console.WriteLine("Malformed actor definition: `{0}`".F(s));
						}
					}
				}

				foreach (var l in scriptedActors.ToLines())
					Console.WriteLine(l);

				foreach (var kv in cellTriggers)
				{
					var node = new MiniYamlNode($"CellTrigger@{kv.Key}", "");
					node.Value.Nodes.Add(new MiniYamlNode("Trigger", kv.Key));
					node.Value.Nodes.Add(new MiniYamlNode("Cells", FieldSaver.FormatValue(kv.Value)));

					foreach (var l in node.Value.ToLines(node.Key))
						Console.WriteLine(l);
				}

				foreach (var t in teams)
					foreach (var l in t.Serialize(triggers, waypoints, teams).ToLines("TeamType@" + t.Name))
						Console.WriteLine(l);

				foreach (var t in triggers)
					foreach (var l in t.Serialize(triggers, waypoints, teams).ToLines("Trigger@" + t.Name))
						Console.WriteLine(l);

				var baseHouse = "Unknown";
				foreach (var s in file.GetSection("Base"))
				{
					if (s.Key == "Player")
					{
						baseHouse = s.Value;
						continue;
					}

					if (s.Key == "Count")
					{
						if (s.Value != "0")
							Console.WriteLine("ScriptedBuildingProduction: house \"{0}\"", baseHouse);
						continue;
					}

					var parts = s.Value.Split(',');
					Console.WriteLine("\t{0} at cell {1}", parts[0], ParseCell(parts[1]));
				}
			}
		}
	}
}
