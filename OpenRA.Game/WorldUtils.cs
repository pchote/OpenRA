#region Copyright & License Information
/*
 * Copyright 2007-2011 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using OpenRA.FileFormats;
using OpenRA.GameRules;
using OpenRA.Support;
using OpenRA.Traits;

namespace OpenRA
{
	public static class WorldUtils
	{
		public static IEnumerable<Actor> FindActorsInBox(this World world, CPos tl, CPos br)
		{
			// Expand to corners of cells
			var wtl = world.CenterOfCell(tl) - new WVec(512, 512, 0);
			var wbr = world.CenterOfCell(br) + new WVec(511, 511, 0);
			return world.FindActorsInBox(wtl, wbr);
		}

		public static IEnumerable<Actor> FindActorsInBox(this World world, WPos tl, WPos br)
		{
			return world.ActorMap.ActorsInBox(tl, br);
		}

		public static Actor ClosestTo(this IEnumerable<Actor> actors, Actor a)
		{
			var pos = a.CenterPosition;
			return actors.OrderBy(b => (b.CenterPosition - pos).LengthSquared).FirstOrDefault();
		}

		public static Actor ClosestTo(this IEnumerable<Actor> actors, WPos pos)
		{
			return actors.OrderBy(a => (a.CenterPosition - pos).LengthSquared).FirstOrDefault();
		}

		public static IEnumerable<Actor> FindActorsInCircle(this World world, WPos origin, WRange r)
		{
			using (new PerfSample("FindUnitsInCircle"))
			{
				// Target ranges are calculated in 2D, so ignore height differences
				var vec = new WVec(r, r, WRange.Zero);
				var rSq = r.Range*r.Range;
				return world.FindActorsInBox(origin - vec, origin + vec).Where(
					a => (a.CenterPosition - origin).HorizontalLengthSquared <= rSq);
			}
		}

		public static IEnumerable<CPos> FindTilesInCircle(this World world, CPos a, int r)
		{
			if (r >= TilesByDistance.Length)
				throw new InvalidOperationException("FindTilesInCircle supports queries for only <= {0}".F(MaxRange));

			for(var i = 0; i <= r; i++)
			{
				foreach (var offset in TilesByDistance[i])
				{
					var t = offset + a;
					if (world.Map.IsInMap(t))
						yield return t;
				}
			}
		}

		static List<CVec>[] InitTilesByDistance(int max)
		{
			var ts = new List<CVec>[max+1];
			for (var i = 0; i < max+1; i++)
				ts[i] = new List<CVec>();

			for (var j = -max; j <= max; j++)
				for (var i = -max; i <= max; i++)
					if (max * max >= i * i + j * j)
						ts[(int)Math.Ceiling(Math.Sqrt(i*i + j*j))].Add(new CVec(i,j));

			return ts;
		}

		public const int MaxRange = 50;
		static List<CVec>[] TilesByDistance = InitTilesByDistance(MaxRange);

		public static string GetTerrainType(this World world, MapCell c)
		{
			var custom = world.Map.CustomTerrain[c.U, c.V];
			return custom ?? world.TileSet.GetTerrainType(c.Tile);
		}

		public static string GetTerrainType(this World world, CPos c)
		{
			return world.GetTerrainType(new MapCell(world.Map, c));
		}

		public static TerrainTypeInfo GetTerrainInfo(this World world, CPos cell)
		{
			return world.TileSet.Terrain[world.GetTerrainType(cell)];
		}

		public static CPos ClampToWorld(this World world, CPos c)
		{
			return new MapCell(world.Map, c).Clamp(world.Map.Bounds).Location;
		}

		public static CPos ChooseRandomEdgeCell(this World w)
		{
			var isX = w.SharedRandom.Next(2) == 0;
			var edge = w.SharedRandom.Next(2) == 0;
			var b = w.Map.Bounds;
			return new MapCell(w.Map,
				isX ? w.SharedRandom.Next(b.Left, b.Right) : (edge ? b.Left : b.Right),
				!isX ? w.SharedRandom.Next(b.Top, b.Bottom)	: (edge ? b.Top : b.Bottom)).Location;
		}

		public static CPos ChooseRandomCell(this World w, Support.Random r)
		{
			var b = w.Map.Bounds;
			return new MapCell(w.Map, r.Next(b.Left, b.Right), r.Next(b.Top, b.Bottom)).Location;
		}

		public static WRange DistanceToMapEdge(this World w, WPos pos, WVec dir)
		{
			var b = w.Map.Bounds;
			var tl = w.CenterOfCell(new MapCell(w.Map, b.Left, b.Top).Location) - new WVec(512, 512, 0);
			var br = w.CenterOfCell(new MapCell(w.Map, b.Right, b.Bottom).Location) + new WVec(511, 511, 0);

			var x = dir.X == 0 ? int.MaxValue : ((dir.X < 0 ? tl.X : br.X) - pos.X) / dir.X;
			var y = dir.Y == 0 ? int.MaxValue : ((dir.Y < 0 ? tl.Y : br.Y) - pos.Y) / dir.Y;
			return new WRange(Math.Min(x, y) * dir.Length);
		}

		public static WPos CenterOfCell(this World w, CPos c)
		{
			if (w.Map.TileShape == TileShape.Rectangle)
				return new WPos(1024 * c.X + 512, 1024 * c.Y + 512, 0);

			return new WPos(512 * (c.X + c.Y + 1), 512 * (c.X - c.Y + 1), 0);
		}

		public static CPos CellContaining(this World w, WPos pos)
		{
			if (w.Map.TileShape == TileShape.Rectangle)
				return new CPos(pos.X / 1024, pos.Y / 1024);

			var u = (pos.X + pos.Y - 512) / 1024;
			var v = (pos.X - pos.Y + 512) / 1024;
			return new CPos(u, v);
		}

		public static int FacingBetween(this World world, CPos cell, CPos towards, int fallbackfacing)
		{
			return Util.GetFacing(world.CenterOfCell(towards) - world.CenterOfCell(cell), fallbackfacing);
		}

		public static bool HasVoices(this Actor a)
		{
			var selectable = a.Info.Traits.GetOrDefault<SelectableInfo>();
			return selectable != null && selectable.Voice != null;
		}

		public static bool HasVoice(this Actor a, string voice)
		{
			var v = GetVoices(a);
			return v != null && v.Voices.ContainsKey(voice);
		}

		public static SoundInfo GetVoices(this Actor a)
		{
			var selectable = a.Info.Traits.GetOrDefault<SelectableInfo>();
			if (selectable == null) return null;
			var v = selectable.Voice;
			return (v == null) ? null : Rules.Voices[v.ToLowerInvariant()];
		}

		public static void PlayVoiceForOrders(this World w, Order[] orders)
		{
			// Find an actor with a phrase to say
			foreach (var o in orders)
			{
				if (o == null)
					continue;

				if (o.Subject.Destroyed)
					continue;

				foreach (var v in o.Subject.TraitsImplementing<IOrderVoice>())
					if (Sound.PlayVoice(v.VoicePhraseForOrder(o.Subject, o),
						o.Subject, o.Subject.Owner.Country.Race))
						return;
			}
		}

		public static void DoTimed<T>(this IEnumerable<T> e, Action<T> a, string text, double time)
		{
			var sw = new Stopwatch();

			e.Do(x =>
			{
				var t = sw.ElapsedTime();
				a(x);
				var dt = sw.ElapsedTime() - t;
				if (dt > time)
					Log.Write("perf", text, x, dt * 1000, Game.LocalTick);
			});
		}

		public static bool AreMutualAllies( Player a, Player b )
		{
			return a.Stances[b] == Stance.Ally &&
				b.Stances[a] == Stance.Ally;
		}
	}
}
