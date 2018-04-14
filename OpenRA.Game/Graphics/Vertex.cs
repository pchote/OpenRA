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

using System.Runtime.InteropServices;

namespace OpenRA.Graphics
{
	[StructLayout(LayoutKind.Sequential)]
	public struct Vertex
	{
		// Vertex position
		public readonly float X, Y, Z;
		
		// Sprites: Texcoords for color and depth textures
		// Colors: RGBA color
		// Voxels: Texcoords for color and normal textures
		public readonly float S, T, U, V;
		
		// Sprites: Palette row
		// Colors: Unused
		// Voxels: Unused
		public readonly float P;
		
		// Sprites: bitfield
		//   0: Uses color sprite
		// 1-2: Color channel select
		//   3: Uses depth sprite
		// 4-5: Depth channel select
		// Colors: Unused
		// Voxels: bitfield
		//   0: Uses color sprite
		// 1-2: Color channel select
		//   3: Uses normal sprite
		// 4-5: Normal channel select
		public readonly uint C;

		public Vertex(float3 xyz, float s, float t, float u, float v, float p, uint c)
			: this(xyz.X, xyz.Y, xyz.Z, s, t, u, v, p, c) { }

		public Vertex(float x, float y, float z, float s, float t, float u, float v, float p, uint c)
		{
			X = x; Y = y; Z = z;
			S = s; T = t;
			U = u; V = v;
			P = p; C = c;
		}
	}
}
