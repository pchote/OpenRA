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
using OpenRA.Server;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Lint
{
	public class CheckConditions : ILintRulesPass, ILintServerMapPass
	{
		void ILintRulesPass.Run(Action<string> emitError, Action<string> emitWarning, ModData modData, Ruleset rules)
		{
			Run(emitError, emitWarning, rules);
		}

		void ILintServerMapPass.Run(Action<string> emitError, Action<string> emitWarning, ModData modData, MapPreview map, Ruleset mapRules)
		{
			Run(emitError, emitWarning, mapRules);
		}

		static void Run(Action<string> emitError, Action<string> emitWarning, Ruleset rules)
		{
			foreach (var actorInfo in rules.Actors)
			{
				var granted = new HashSet<string>();
				var consumed = new HashSet<string>();

				foreach (var trait in actorInfo.Value.TraitInfos<TraitInfo>())
				{
					var fields = Utility.GetFields(trait.GetType());
					var properties = trait.GetType().GetProperties();

					var fieldConsumed = fields
						.Where(Utility.HasAttribute<ConsumedConditionReferenceAttribute>)
						.SelectMany(f => LintExts.GetFieldValues(trait, f));

					var propertyConsumed = properties
						.Where(Utility.HasAttribute<ConsumedConditionReferenceAttribute>)
						.SelectMany(p => LintExts.GetPropertyValues(trait, p));

					var fieldGranted = fields
						.Where(Utility.HasAttribute<GrantedConditionReferenceAttribute>)
						.SelectMany(f => LintExts.GetFieldValues(trait, f));

					var propertyGranted = properties
						.Where(Utility.HasAttribute<GrantedConditionReferenceAttribute>)
						.SelectMany(f => LintExts.GetPropertyValues(trait, f));

					foreach (var c in fieldConsumed.Concat(propertyConsumed))
						if (!string.IsNullOrEmpty(c))
							consumed.Add(c);

					foreach (var g in fieldGranted.Concat(propertyGranted))
						if (!string.IsNullOrEmpty(g))
							granted.Add(g);
				}

				var unconsumed = granted.Except(consumed).ToList();
				if (unconsumed.Count != 0)
					emitWarning($"Actor type `{actorInfo.Key}` grants conditions that are not consumed: {unconsumed.JoinWith(", ")}.");

				var ungranted = consumed.Except(granted).ToList();
				if (ungranted.Count != 0)
					emitError($"Actor type `{actorInfo.Key}` consumes conditions that are not granted: {ungranted.JoinWith(", ")}.");
			}
		}
	}
}
