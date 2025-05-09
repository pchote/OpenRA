﻿#region Copyright & License Information
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
using OpenRA.Mods.Common.UtilityCommands.Documentation;

namespace OpenRA.Mods.Common.Scripting
{
	/// <summary>
	/// Used to override the Emmy Lua type generated by the <see cref="ExtractEmmyLuaAPI"/> utility command.
	/// </summary>
	[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.ReturnValue)]
	public sealed class ScriptEmmyTypeOverrideAttribute(string typeDeclaration, string genericTypeDeclaration = null) : Attribute
	{
		public readonly string TypeDeclaration = typeDeclaration;
		public readonly string GenericTypeDeclaration = genericTypeDeclaration;
	}
}
