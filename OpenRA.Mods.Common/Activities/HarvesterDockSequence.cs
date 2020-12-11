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
using OpenRA.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Activities
{
	public abstract class HarvesterDockSequence : Activity
	{
		protected enum DockingState { Wait, Blink, Dock, Loop, Undock, Complete }

		protected readonly Actor Refinery;
		protected readonly Harvester Harv;
		protected readonly WAngle DockAngle;
		protected readonly WPos DockPos;
		readonly IFacing facing;

		protected DockingState dockingState;
		WAngle? preDockFacing;

		public HarvesterDockSequence(Actor self, Actor refinery, WAngle dockAngle, WPos dockPos)
		{
			dockingState = DockingState.Blink;
			Refinery = refinery;
			DockAngle = dockAngle;
			DockPos = dockPos;
			Harv = self.Trait<Harvester>();
			facing = self.Trait<IFacing>();
		}

		public override bool Tick(Actor self)
		{
			switch (dockingState)
			{
				case DockingState.Wait:
					return false;

				case DockingState.Blink:
					if (IsCanceling || !Refinery.IsInWorld || Refinery.IsDead)
						return true;

					dockingState = DockingState.Dock;
					self.Trait<IPositionable>().SetVisualPosition(self, DockPos);
					preDockFacing = facing.Facing;
					facing.Facing = DockAngle;
					return false;

				case DockingState.Dock:
					if (!IsCanceling && Refinery.IsInWorld && !Refinery.IsDead)
						OnStateDock(self);
					else
						dockingState = DockingState.Undock;

					return false;

				case DockingState.Loop:
					if (IsCanceling || !Refinery.IsInWorld || Refinery.IsDead || Harv.TickUnload(self, Refinery))
						dockingState = DockingState.Undock;

					return false;

				case DockingState.Undock:
					OnStateUndock(self);
					return false;

				case DockingState.Complete:
					Harv.LastLinkedProc = Harv.LinkedProc;
					Harv.LinkProc(self, null);
					self.Trait<IPositionable>().SetVisualPosition(self, self.World.Map.CenterOfCell(self.Location));
					if (preDockFacing.HasValue)
						facing.Facing = preDockFacing.Value + new WAngle(512);

					return true;
			}

			throw new InvalidOperationException("Invalid harvester dock state");
		}

		public override IEnumerable<Target> GetTargets(Actor self)
		{
			yield return Target.FromActor(Refinery);
		}

		public override IEnumerable<TargetLineNode> TargetLineNodes(Actor self)
		{
			yield return new TargetLineNode(Target.FromActor(Refinery), Color.Green);
		}

		public abstract void OnStateDock(Actor self);

		public abstract void OnStateUndock(Actor self);
	}
}
