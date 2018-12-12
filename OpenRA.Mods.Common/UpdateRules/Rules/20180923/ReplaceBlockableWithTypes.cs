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

using System.Collections.Generic;
using System.Linq;

namespace OpenRA.Mods.Common.UpdateRules.Rules
{
	public class ReplaceBlockableWithTypes : UpdateRule
	{
		public override string Name { get { return "Replace projectile Blockable field with BlockingTypes"; } }
		public override string Description
		{
			get
			{
				return "The Blockable field on projectiles has been replaced with a list of blocking types.\n" +
					"Uses of the Blockable field are listed for manual definitions.";
			}
		}

		readonly List<string> weaponLocations = new List<string>();
		readonly List<string> blocksProjectileLocations = new List<string>();
		readonly List<string> gateLocations = new List<string>();

		public override IEnumerable<string> AfterUpdate(ModData modData)
		{
			if (weaponLocations.Any())
				yield return "Review the following weapon definitions and define BlockingTypes\n" +
					"to maintain the previous blocking behaviour:\n" +
					UpdateUtils.FormatMessageList(weaponLocations);

			if (blocksProjectileLocations.Any())
				yield return "Review the following actor definitions and define Types on the\n" +
				             "BlocksProjectile trait to maintain the previous blocking behaviour:\n" +
				             UpdateUtils.FormatMessageList(blocksProjectileLocations);

			if (gateLocations.Any())
				yield return "Review the following actor definitions and define BlocksProjectilesTypes\n" +
				             "on theGate trait to maintain the previous blocking behaviour:\n" +
				             UpdateUtils.FormatMessageList(gateLocations);

			weaponLocations.Clear();
			blocksProjectileLocations.Clear();
			gateLocations.Clear();
		}

		public override IEnumerable<string> UpdateActorNode(ModData modData, MiniYamlNode actorNode)
		{
			if (actorNode.LastChildMatching("BlocksProjectiles") != null)
				blocksProjectileLocations.Add("{0} ({1})".F(actorNode.Key, actorNode.Location.Filename));

			if (actorNode.LastChildMatching("Gate") != null)
				gateLocations.Add("{0} ({1})".F(actorNode.Key, actorNode.Location.Filename));

			yield break;
		}

		public override IEnumerable<string> UpdateWeaponNode(ModData modData, MiniYamlNode weaponNode)
		{
			foreach (var node in weaponNode.ChildrenMatching("Projectile"))
			{
				var blockable = node.LastChildMatching("Blockable");
				if (blockable != null)
				{
					var value = blockable.NodeValue<bool>();

					// Blocking: false can be replaced by an empty list
					if (!value)
					{
						blockable.Key = "-BlockingTypes";
						blockable.ReplaceValue("");
					}

					// Blocking: true should be fixed manually
					else
						weaponLocations.Add("{0} ({1})".F(weaponNode.Key, weaponNode.Location.Filename));

					node.RemoveNodes("Blockable");
				}
				else
				{
					// Missile and Bullet were blockable by default
					var nodeValue = node.NodeValue<string>();
					if (nodeValue == "Missile" || nodeValue == "Bullet")
						weaponLocations.Add("{0} ({1})".F(weaponNode.Key, weaponNode.Location.Filename));
				}
			}

			yield break;
		}
	}
}
