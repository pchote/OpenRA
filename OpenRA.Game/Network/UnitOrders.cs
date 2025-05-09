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
using System.Linq;
using OpenRA.Server;
using OpenRA.Traits;

namespace OpenRA.Network
{
	public static class UnitOrders
	{
		public const int ChatMessageMaxLength = 2500;

		[FluentReference("player")]
		const string Joined = "notification-joined";

		[FluentReference("player")]
		const string Left = "notification-lobby-disconnected";

		[FluentReference]
		const string GameStarted = "notification-game-has-started";

		[FluentReference]
		const string GameSaved = "notification-game-saved";

		[FluentReference("player")]
		const string GamePaused = "notification-game-paused";

		[FluentReference("player")]
		const string GameUnpaused = "notification-game-unpaused";

		public static int? KickVoteTarget { get; internal set; }

		static Player FindPlayerByClient(this World world, Session.Client c)
		{
			return world.Players.FirstOrDefault(p => p.ClientIndex == c.Index && p.PlayerReference.Playable);
		}

		static bool OrderNotFromServerOrWorldIsReplay(int clientId, World world) => clientId != 0 || (world != null && world.IsReplay);

		internal static void ProcessOrder(OrderManager orderManager, World world, int clientId, Order order)
		{
			switch (order.OrderString)
			{
				// Server message
				case "Message":
					TextNotificationsManager.AddSystemLine(order.TargetString);
					break;

				// Client side resolved server message
				case "FluentMessage":
				{
					if (string.IsNullOrEmpty(order.TargetString))
						break;

					var yaml = MiniYaml.FromString(order.TargetString, order.OrderString);
					foreach (var node in yaml)
					{
						var message = new FluentMessage(node.Value);
						if (message.Key == Joined)
							TextNotificationsManager.AddPlayerJoinedLine(message.Key, message.Arguments);
						else if (message.Key == Left)
							TextNotificationsManager.AddPlayerLeftLine(message.Key, message.Arguments);
						else
							TextNotificationsManager.AddSystemLine(message.Key, message.Arguments);
					}

					break;
				}

				case "DisableChatEntry":
				{
					if (OrderNotFromServerOrWorldIsReplay(clientId, world))
						break;

					// Server may send MaxValue to indicate that it is disabled until further notice
					if (order.ExtraData == uint.MaxValue)
						TextNotificationsManager.ChatDisabledUntil = uint.MaxValue;
					else
						TextNotificationsManager.ChatDisabledUntil = Game.RunTime + order.ExtraData;

					break;
				}

				case "StartKickVote":
				{
					if (OrderNotFromServerOrWorldIsReplay(clientId, world))
						break;

					KickVoteTarget = (int)order.ExtraData;
					break;
				}

				case "EndKickVote":
				{
					if (OrderNotFromServerOrWorldIsReplay(clientId, world))
						break;

					if (KickVoteTarget == (int)order.ExtraData)
						KickVoteTarget = null;

					break;
				}

				case "Chat":
				{
					var client = orderManager.LobbyInfo.ClientWithIndex(clientId);
					if (client == null)
						break;

					// Cut chat messages to the hard limit to avoid exploits
					var message = order.TargetString;
					if (message.Length > ChatMessageMaxLength)
						message = order.TargetString[..ChatMessageMaxLength];

					// ExtraData 0 means this is a normal chat order, everything else is team chat
					if (order.ExtraData == 0)
					{
						var p = world?.FindPlayerByClient(client);
						var suffix = (p != null && p.WinState == WinState.Lost) ? " (Dead)" : "";
						suffix = client.IsObserver ? " (Spectator)" : suffix;

						if (orderManager.LocalClient != null && client != orderManager.LocalClient && client.Team > 0 && client.Team == orderManager.LocalClient.Team)
							suffix += " (Ally)";

						TextNotificationsManager.AddChatLine(clientId, client.Name + suffix, message, client.Color);
						break;
					}

					// We are still in the lobby
					if (world == null)
					{
						var prefix = order.ExtraData == uint.MaxValue ? "[Spectators] " : "[Team] ";
						if (orderManager.LocalClient != null && client.Team == orderManager.LocalClient.Team)
							TextNotificationsManager.AddChatLine(clientId, prefix + client.Name, message, client.Color);

						break;
					}

					var player = world.FindPlayerByClient(client);
					var localClientIsObserver = world.IsReplay || (orderManager.LocalClient != null && orderManager.LocalClient.IsObserver)
						|| (world.LocalPlayer != null && world.LocalPlayer.WinState != WinState.Undefined);

					// ExtraData gives us the team number, uint.MaxValue means Spectators
					if (order.ExtraData == uint.MaxValue && localClientIsObserver)
					{
						// Validate before adding the line
						if (client.IsObserver || (player != null && player.WinState != WinState.Undefined))
							TextNotificationsManager.AddChatLine(clientId, "[Spectators] " + client.Name, message, client.Color);

						break;
					}

					var valid = client.Team == order.ExtraData && player != null && player.WinState == WinState.Undefined;
					var isSameTeam = orderManager.LocalClient != null && order.ExtraData == orderManager.LocalClient.Team
						&& world.LocalPlayer != null && world.LocalPlayer.WinState == WinState.Undefined;

					if (valid && (isSameTeam || world.IsReplay))
						TextNotificationsManager.AddChatLine(clientId, "[Team" + (world.IsReplay ? " " + order.ExtraData : "") + "] " + client.Name, message, client.Color);

					break;
				}

				case "StartGame":
				{
					if (Game.ModData.MapCache[orderManager.LobbyInfo.GlobalSettings.Map].Status != MapStatus.Available)
					{
						Game.Disconnect();
						Game.LoadShellMap();

						// TODO: After adding a startup error dialog, notify the replay load failure.
						break;
					}

					if (!string.IsNullOrEmpty(order.TargetString))
					{
						var data = MiniYaml.FromString(order.TargetString, order.OrderString).ToList();
						var saveLastOrdersFrame = data.FirstOrDefault(n => n.Key == "SaveLastOrdersFrame");
						if (saveLastOrdersFrame != null)
							orderManager.GameSaveLastFrame =
								FieldLoader.GetValue<int>("saveLastOrdersFrame", saveLastOrdersFrame.Value.Value);

						var saveSyncFrame = data.FirstOrDefault(n => n.Key == "SaveSyncFrame");
						if (saveSyncFrame != null)
							orderManager.GameSaveLastSyncFrame =
								FieldLoader.GetValue<int>("SaveSyncFrame", saveSyncFrame.Value.Value);
					}
					else
						TextNotificationsManager.AddSystemLine(GameStarted);

					Game.StartGame(orderManager.LobbyInfo.GlobalSettings.Map, WorldType.Regular);
					break;
				}

				case "SaveTraitData":
				{
					var data = MiniYaml.FromString(order.TargetString, order.OrderString).First();
					var traitIndex = Exts.ParseInt32Invariant(data.Key);

					world?.AddGameSaveTraitData(traitIndex, data.Value);

					break;
				}

				case "GameSaved":
					if (!orderManager.World.IsReplay)
						TextNotificationsManager.AddSystemLine(GameSaved);

					foreach (var nsr in orderManager.World.WorldActor.TraitsImplementing<INotifyGameSaved>())
						nsr.GameSaved(orderManager.World);
					break;

				case "PauseGame":
				{
					var client = orderManager.LobbyInfo.ClientWithIndex(clientId);
					if (client != null)
					{
						var pause = order.TargetString == "Pause";

						// Prevent injected unpause orders from restarting a finished game
						if (orderManager.World.IsGameOver && !pause)
							break;

						if (orderManager.World.Paused != pause && world != null && world.LobbyInfo.NonBotClients.Count() > 1)
							TextNotificationsManager.AddSystemLine(pause ? GamePaused : GameUnpaused, "player", client.Name);

						orderManager.World.Paused = pause;
						orderManager.World.PredictedPaused = pause;
					}

					break;
				}

				case "HandshakeRequest":
				{
					// Switch to the server's mod if we need and are able to
					var mod = Game.ModData.Manifest;
					var request = HandshakeRequest.Deserialize(order.TargetString, order.OrderString);

					var externalKey = ExternalMod.MakeKey(request.Mod, request.Version);
					if ((request.Mod != mod.Id || request.Version != mod.Metadata.Version) &&
						Game.ExternalMods.TryGetValue(externalKey, out var external))
					{
						// The ConnectionFailedLogic will prompt the user to switch mods
						CurrentServerSettings.ServerExternalMod = external;
						orderManager.Connection.Dispose();
						break;
					}

					Game.Settings.Player.Name = Settings.SanitizedPlayerName(Game.Settings.Player.Name);
					Game.Settings.Save();

					// Otherwise send the handshake with our current settings and let the server reject us
					var info = new Session.Client()
					{
						Name = Game.Settings.Player.Name,
						PreferredColor = Game.Settings.Player.Color,
						Color = Game.Settings.Player.Color,
						Faction = "Random",
						SpawnPoint = 0,
						Team = 0,
						State = Session.ClientState.Invalid
					};

					var localProfile = Game.LocalPlayerProfile;
					var response = new HandshakeResponse()
					{
						Client = info,
						Mod = mod.Id,
						Version = mod.Metadata.Version,
						Password = CurrentServerSettings.Password,
						Fingerprint = localProfile.Fingerprint,
						OrdersProtocol = ProtocolVersion.Orders
					};

					if (request.AuthToken != null && response.Fingerprint != null)
						response.AuthSignature = localProfile.Sign(request.AuthToken);

					orderManager.IssueOrder(new Order("HandshakeResponse", null, false)
					{
						Type = OrderType.Handshake,
						IsImmediate = true,
						TargetString = response.Serialize()
					});

					break;
				}

				case "ServerError":
				{
					orderManager.ServerError = order.TargetString;
					orderManager.AuthenticationFailed = false;
					break;
				}

				case "AuthenticationError":
				{
					// The ConnectionFailedLogic will prompt the user for the password
					orderManager.ServerError = order.TargetString;
					orderManager.AuthenticationFailed = true;
					break;
				}

				case "SyncInfo":
				{
					orderManager.LobbyInfo = Session.Deserialize(order.TargetString, order.OrderString);
					Game.SyncLobbyInfo();
					break;
				}

				case "SyncLobbyClients":
				{
					var clients = new List<Session.Client>();
					var nodes = MiniYaml.FromString(order.TargetString, order.OrderString);
					foreach (var node in nodes)
					{
						var strings = node.Key.Split('@');
						if (strings[0] == "Client")
							clients.Add(Session.Client.Deserialize(node.Value));
					}

					orderManager.LobbyInfo.Clients = clients;
					Game.SyncLobbyInfo();
					break;
				}

				case "SyncLobbySlots":
				{
					var slots = new Dictionary<string, Session.Slot>();
					var nodes = MiniYaml.FromString(order.TargetString, order.OrderString);
					foreach (var node in nodes)
					{
						var strings = node.Key.Split('@');
						if (strings[0] == "Slot")
						{
							var slot = Session.Slot.Deserialize(node.Value);
							slots.Add(slot.PlayerReference, slot);
						}
					}

					orderManager.LobbyInfo.Slots = slots;
					Game.SyncLobbyInfo();
					break;
				}

				case "SyncLobbyGlobalSettings":
				{
					var nodes = MiniYaml.FromString(order.TargetString, order.OrderString);
					foreach (var node in nodes)
					{
						var strings = node.Key.Split('@');
						if (strings[0] == "GlobalSettings")
							orderManager.LobbyInfo.GlobalSettings = Session.Global.Deserialize(node.Value);
					}

					Game.SyncLobbyInfo();
					break;
				}

				case "SyncConnectionQuality":
				{
					var nodes = MiniYaml.FromString(order.TargetString, order.OrderString);
					foreach (var node in nodes)
					{
						var strings = node.Key.Split('@');
						if (strings[0] == "ConnectionQuality")
						{
							var client = orderManager.LobbyInfo.Clients.FirstOrDefault(c => c.Index == Exts.ParseInt32Invariant(strings[1]));
							if (client != null)
								client.ConnectionQuality = FieldLoader.GetValue<Session.ConnectionQuality>("ConnectionQuality", node.Value.Value);
						}
					}

					break;
				}

				case "SyncMapPool":
				{
					orderManager.ServerMapPool = FieldLoader.GetValue<HashSet<string>>("SyncMapPool", order.TargetString);
					break;
				}

				default:
				{
					if (world == null)
						break;

					if (order.GroupedActors == null)
						ResolveOrder(order, world, orderManager, clientId);
					else
						foreach (var subject in order.GroupedActors)
							ResolveOrder(Order.FromGroupedOrder(order, subject), world, orderManager, clientId);

					break;
				}
			}
		}

		static void ResolveOrder(Order order, World world, OrderManager orderManager, int clientId)
		{
			if (order.Subject == null || order.Subject.IsDead)
				return;

			if (world.OrderValidators.All(vo => vo.OrderValidation(orderManager, world, clientId, order)))
				order.Subject.ResolveOrder(order);
		}

		public static void Clear()
		{
			KickVoteTarget = null;
		}
	}
}
