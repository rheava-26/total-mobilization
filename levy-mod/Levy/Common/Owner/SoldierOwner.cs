using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Weapons;
using Levy.Content.NPCs;

namespace Levy.Common.Owner
{
	/// <summary>Options of one owner frame.</summary>
	public struct OwnerFrameOptions
	{
		/// <summary>World point the cursor is aimed at for the frame (QuickGrapple, ItemCheck read Main.mouseX / Y + Main.screenPosition). Null: the cursor is left alone.</summary>
		public Vector2? Aim;
		/// <summary>Full transform sync (SoldierNPC.SyncDummyTransform: life, biome mirror, mana). Otherwise only position, velocity and facing are copied (what a projectile frame needs).</summary>
		public bool FullSync;
	}

	/// <summary>
	/// One OWNER FRAME: the span in which a soldier's hidden <see cref="Player"/> (the dummy) stands in the soldier's own player slot and is "the local player", so vanilla and mod code that acts for a player
	/// (QuickGrapple, a hook projectile's AI, Player.GrappleMovement, later: real item use) runs unmodified and acts for the soldier.
	/// <para/>Use it as <c>using (var f = SoldierOwner.Enter(body, opts)) { if (!f.Ok) return; ... }</c>. Enter does, in this order:
	/// takes the net-thread lock (dedicated server, slot below 255: <see cref="NetThreadGuard"/>), puts the dummy in the soldier's slot (<see cref="OwnerSlots"/>), makes that slot <c>Main.myPlayer</c>
	/// (while <see cref="SoldierMinions.TrueMyPlayer"/> still answers with the real one), aims the cursor, moves the dummy onto the soldier, points Thorium's local-player cache at the dummy
	/// (<see cref="OwnerPuppet.SwapThorium"/>), and forces <c>Main.dedServ</c> and mutes sound so "!dedServ &amp;&amp; whoAmI == myPlayer" blocks (UI, shaders, music, screen shake) cannot fire in single player.
	/// Dispose undoes all of it in reverse, inside a finally. Frames nest: each remembers exactly what it replaced (a depth stack), and a frame left open by an exception is unwound by the one below it.
	/// A <see cref="KillMeGuard"/> keeps the dummy from ever running the local-player death path.
	/// </summary>
	public static class SoldierOwner
	{
		private struct Rec
		{
			public SoldierNPC Body; public Player Dummy; public int Slot, MyPlayer, FakeToken, SavedWho, MouseX, MouseY;
			public Player BackupSlot; public bool Ded, Locked, MouseTouched; public float Volume; public Vector2 ScreenPos; public object Thorium;
		}

		private static readonly List<Rec> _stack = new();
		private static readonly ConditionalWeakTable<Player, object> _dummies = new();
		private static readonly object _mark = new();

		/// <summary>Frames currently open (0 outside any).</summary>
		public static int Depth => _stack.Count;
		/// <summary>The soldier of the innermost open frame, or null.</summary>
		public static SoldierNPC CurrentBody => _stack.Count == 0 ? null : _stack[^1].Body;
		public static long Frames, Refused;

		/// <summary>Remembers that <paramref name="p"/> is a soldier's dummy (called where the dummy is created).</summary>
		public static void RegisterDummy(Player p) { if (p != null) _dummies.AddOrUpdate(p, _mark); }
		public static bool IsDummy(Player p) => p != null && _dummies.TryGetValue(p, out _);

		/// <summary>Opens a frame for <paramref name="body"/>. Check <see cref="OwnerFrame.Ok"/>: false means no slot was free or the net lock could not be had, and nothing was changed.</summary>
		public static OwnerFrame Enter(SoldierNPC body, OwnerFrameOptions opts = default)
		{
			Player d = body?.Dummy;
			if (d == null || body.NPC == null || !body.NPC.active) return default;
			int slot = OwnerSlots.For(body);
			if (slot < 0) { Refused++; return default; }
			var r = new Rec { Body = body, Dummy = d, Slot = slot };
			if (NetThreadGuard.Needed)
			{
				if (!NetThreadGuard.TryEnter()) { Refused++; return default; }
				r.Locked = true;
			}
			try
			{
				r.MyPlayer = Main.myPlayer; r.SavedWho = d.whoAmI; r.Ded = Main.dedServ; r.Volume = Main.soundVolume;
				r.ScreenPos = Main.screenPosition; r.MouseX = Main.mouseX; r.MouseY = Main.mouseY;
				r.BackupSlot = Main.player[slot];
				// the dummy first takes the soldier's body (its whoAmI is rewritten to the slot below)
				if (opts.FullSync) body.SyncDummyTransform();
				else
				{
					NPC n = body.NPC;
					d.position = n.position; d.velocity = n.velocity; d.direction = n.direction == 0 ? 1 : n.direction; d.gravDir = 1f;
					d.active = true; d.dead = false; d.ghost = false; d.statLife = Math.Max(1, n.life);
				}
				d.whoAmI = slot;
				Main.player[slot] = d;
				r.FakeToken = SoldierMinions.BeginFake(r.MyPlayer);
				Main.myPlayer = slot;
				Main.dedServ = true;
				if (!r.Ded) Main.soundVolume = 0f;
				if (opts.Aim.HasValue)
				{
					Main.screenPosition = Vector2.Zero;
					Main.mouseX = (int)opts.Aim.Value.X; Main.mouseY = (int)opts.Aim.Value.Y; r.MouseTouched = true;
				}
				r.Thorium = OwnerPuppet.SwapThorium(d, true);
			}
			catch (Exception)
			{
				Unwind(r);
				throw;
			}
			_stack.Add(r);
			Frames++;
			return new OwnerFrame(_stack.Count);
		}

		internal static void Leave(int depth)
		{
			// a frame left open above this one (an exception that skipped its Dispose): unwind it first
			while (_stack.Count > depth) { Rec top = _stack[^1]; _stack.RemoveAt(_stack.Count - 1); Unwind(top); }
			if (_stack.Count == depth)
			{
				Rec r = _stack[^1]; _stack.RemoveAt(_stack.Count - 1);
				Unwind(r);
			}
		}

		private static void Unwind(Rec r)
		{
			Player d = r.Dummy;
			try
			{
				Main.myPlayer = r.MyPlayer;                   // before the Thorium restore: it finds the commander through Main.myPlayer / Main.player
				SoldierMinions.EndFake(r.FakeToken);
				if (!d.active) d.active = true;               // the network thread may have cleared it if the lock was not held
				Main.player[r.Slot] = r.BackupSlot ?? new Player();
				d.whoAmI = r.SavedWho;
				d.dead = false; d.ghost = false; d.frozen = false; d.webbed = false; d.stoned = false;
				Main.dedServ = r.Ded;
				Main.soundVolume = r.Volume;
				if (r.MouseTouched) { Main.screenPosition = r.ScreenPos; Main.mouseX = r.MouseX; Main.mouseY = r.MouseY; }
				OwnerPuppet.RestoreThorium(r.Thorium);
			}
			finally
			{
				if (r.Locked) NetThreadGuard.Exit();
			}
		}

		/// <summary>Called at the top of a tick and on world unload: no frame survives a tick. Unwinds anything left open.</summary>
		public static void Reset() { while (_stack.Count > 0) { Rec top = _stack[^1]; _stack.RemoveAt(_stack.Count - 1); try { Unwind(top); } catch (Exception) { } } }
	}

	/// <summary>A handle to an open owner frame. <c>using</c> it; <see cref="Ok"/> false means the frame did not open and Dispose does nothing.</summary>
	public readonly struct OwnerFrame : IDisposable
	{
		private readonly int _depth;
		internal OwnerFrame(int depth) { _depth = depth; }
		public bool Ok => _depth > 0;
		public void Dispose() { if (_depth > 0) SoldierOwner.Leave(_depth); }
	}

	/// <summary>
	/// Stops a soldier's dummy from running the local-player death path. A real item can kill its user (Martyr's Chalice spends life), and vanilla then runs tombstone, coin drop and, on a server, SavePlayer and a process abort.
	/// Also owns the per-tick safety net for the frame stack.
	/// </summary>
	public sealed class KillMeGuard : ModSystem
	{
		public static long Blocked;
		public override void Load() => On_Player.KillMe += Detour;
		public override void Unload() { try { On_Player.KillMe -= Detour; } catch { } }
		public override void OnWorldUnload() => SoldierOwner.Reset();
		public override void PreUpdateNPCs() { if (SoldierOwner.Depth > 0) SoldierOwner.Reset(); }

		private static void Detour(On_Player.orig_KillMe orig, Player self, Terraria.DataStructures.PlayerDeathReason reason, double dmg, int hitDirection, bool pvp)
		{
			if (SoldierOwner.IsDummy(self)) { Blocked++; self.statLife = Math.Max(1, self.statLife); self.dead = false; return; }
			orig(self, reason, dmg, hitDirection, pvp);
		}
	}
}
