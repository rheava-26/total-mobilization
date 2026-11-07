using System;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Config;
using Levy.Common.Owner;
using Levy.Content.NPCs;

namespace Levy.Common.Movement
{
	/// <summary>Tags a hook projectile cast by a soldier. The soldier's cast sets <see cref="SoldierHooks.Casting"/> for the length of QuickGrapple, so the projectile is tagged before the first sync packet would go out.</summary>
	public class HookTag : GlobalProjectile
	{
		public override bool InstancePerEntity => true;
		/// <summary>NPC index of the soldier that cast this hook, or -1.</summary>
		public int Soldier = -1;
		private static Player _drawBackup; private static int _drawSlot = -1;

		public override void OnSpawn(Projectile projectile, IEntitySource source)
		{
			var c = SoldierHooks.Casting;
			if (c == null || projectile.type < 0 || projectile.type >= Main.projHook.Length || !Main.projHook[projectile.type]) return;
			Soldier = c.NPC.whoAmI;
			projectile.npcProj = false;                       // a player's hook: it must behave as one
			c.HookNoteSpawn(projectile.whoAmI);
		}

		// Single player draws the real hook projectile, whose chain starts at Main.player[owner].MountedCenter. tML draws the chain in the extras pass BEFORE PreDraw, so the soldier's dummy
		// stands in the owner slot from PreDrawExtras on (PreDraw re-checks, PostDraw restores, and the frame-end systems below restore if another mod skipped the sprite).
		public override bool PreDrawExtras(Projectile projectile)
		{
			BeginDrawSwap(projectile);
			return true;
		}

		public override bool PreDraw(Projectile projectile, ref Color lightColor)
		{
			BeginDrawSwap(projectile);
			return true;
		}

		private void BeginDrawSwap(Projectile projectile)
		{
			if (Soldier < 0 || Main.dedServ || _drawBackup != null) return;
			NPC n = Main.npc[Soldier];
			if (!n.active || n.ModNPC is not SoldierNPC sn || sn.Dummy == null) return;
			Player d = sn.Dummy;
			d.position = n.position; d.velocity = n.velocity; d.direction = n.direction == 0 ? 1 : n.direction; d.active = true;
			_drawSlot = projectile.owner; _drawBackup = Main.player[_drawSlot]; Main.player[_drawSlot] = d;
		}

		public override void PostDraw(Projectile projectile, Color lightColor) => EndDrawSwap();

		internal static void EndDrawSwap()
		{
			if (_drawBackup == null) return;
			Main.player[_drawSlot] = _drawBackup; _drawBackup = null; _drawSlot = -1;
		}

		/// <summary>Self-check helper: after the extras-pass swap the owner slot's MountedCenter must sit on the soldier. Returns the distance in px (always restores).</summary>
		internal float SwapError(Projectile projectile)
		{
			if (Soldier < 0 || Main.dedServ) return 0f;
			BeginDrawSwap(projectile);
			float err = _drawBackup == null ? float.MaxValue : Vector2.Distance(Main.player[projectile.owner].MountedCenter, Main.npc[Soldier].Center);
			EndDrawSwap();
			return err;
		}
	}

	/// <summary>Puts the owner slot back if a hook's sprite draw was skipped by another mod after its chain was drawn.</summary>
	public class HookDrawRestore : ModSystem
	{
		public override void PostDrawInterface(Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch) => HookTag.EndDrawSwap();
		public override void PostUpdateEverything() => HookTag.EndDrawSwap();
	}

	/// <summary>
	/// The grappling-hook plumbing shared by every soldier: the hook projectile's Update runs inside the owner frame, the dedicated server never sends a soldier hook to clients, and the cost counters.
	/// </summary>
	public sealed class SoldierHooks : ModSystem
	{
		/// <summary>The soldier whose QuickGrapple is running (tags the hook it spawns).</summary>
		internal static SoldierNPC Casting;
		public static long TickTs, PlanTs, ProjTs; public static int TickCalls, Plans, PlansFound, ProjCalls, Casts, CastsOk, Latches, Releases, Landings, Aborts, Teleports, NetSuppressed, OrphanKills;
		private static uint _enabledTick = uint.MaxValue; private static bool _enabled = true;
		private static uint _budgetTick; private static int _budget;
		private static bool _hookedUpdate, _hookedSend;

		/// <summary>The config switch, read once a tick (the harness can override it).</summary>
		public static bool Enabled
		{
			get
			{
				if (_override >= 0) return _override == 1;
				if (_enabledTick == Main.GameUpdateCount) return _enabled;
				_enabledTick = Main.GameUpdateCount;
				try { _enabled = LevyConfig.Get.SoldiersUseHooks; } catch (Exception) { _enabled = true; }
				return _enabled;
			}
		}
		private static int _override = -1;
		/// <summary>Logs every plan (env LEVY_HOOKDBG=1).</summary>
		public static readonly bool Debug = Environment.GetEnvironmentVariable("LEVY_HOOKDBG") == "1";
		internal static void SetOverride(int v) { _override = v; }

		/// <summary>True while the army has plan budget left this tick (the fan and the simulations are the costly part, so only a couple start per tick).</summary>
		internal static bool TakePlanBudget()
		{
			uint now = Main.GameUpdateCount;
			if (_budgetTick != now) { _budgetTick = now; _budget = LevyConstants.Hooks.PlansPerTick; }
			if (_budget <= 0) return false;
			_budget--; return true;
		}

		internal static void ResetCounters() { TickTs = PlanTs = ProjTs = 0; TickCalls = Plans = PlansFound = ProjCalls = Casts = CastsOk = Latches = Releases = Landings = Aborts = Teleports = NetSuppressed = OrphanKills = 0; }

		public override void Load()
		{
			On_Projectile.Update += UpdateDetour; _hookedUpdate = true;
			if (Main.dedServ) { On_NetMessage.SendData += SendDetour; _hookedSend = true; }
		}

		public override void Unload()
		{
			try { if (_hookedUpdate) On_Projectile.Update -= UpdateDetour; if (_hookedSend) On_NetMessage.SendData -= SendDetour; } catch { }
			_hookedUpdate = _hookedSend = false; Casting = null;
		}

		public override void OnWorldUnload() { Casting = null; HookInfo.ClearCache(); }

		// ---------------------------------------------------------------------------------------------
		// the hook projectile's Update, inside the owner frame
		// ---------------------------------------------------------------------------------------------
		private static void UpdateDetour(On_Projectile.orig_Update orig, Projectile self, int i)
		{
			if (!self.active || self.type >= Main.projHook.Length || !Main.projHook[self.type] || !self.TryGetGlobalProjectile(out HookTag tag) || tag.Soldier < 0) { orig(self, i); return; }
			RunHook(orig, self, i, tag);
		}

		private static void RunHook(On_Projectile.orig_Update orig, Projectile p, int i, HookTag tag)
		{
			long t0 = Stopwatch.GetTimestamp();
			NPC n = Main.npc[tag.Soldier];
			if (!n.active || n.ModNPC is not SoldierNPC sn || n.life <= 0 || !sn.HookOwns(p.whoAmI)) { if (Debug) Levy.Instance?.Logger.Info($"HOOKDBG orphan hook #{p.whoAmI} of npc {tag.Soldier}: npcActive={n.active} life={n.life} soldier={(n.ModNPC as SoldierNPC)?.HookState} owns={(n.ModNPC as SoldierNPC)?.HookOwns(p.whoAmI)} tick={Main.GameUpdateCount}"); p.active = false; p.timeLeft = 0; OrphanKills++; return; } // the soldier is gone, or is not hooking (a stray hook from a reload)
			using var f = SoldierOwner.Enter(sn, default);
			if (!f.Ok) return;                      // no slot or lock this tick: the hook simply waits (a frame that cannot open changes nothing)
			Player d = sn.Dummy;
			d.frozen = n.HasBuff(BuffID.Frozen); d.webbed = n.HasBuff(BuffID.Webbed); d.stoned = n.HasBuff(BuffID.Stoned); // frozen / webbed / stoned: the hook AI kills itself
			Vector2 prePos = d.position, preVel = d.velocity;
			try { orig(p, i); }
			finally
			{
				if (Vector2.DistanceSquared(d.position, prePos) > LevyConstants.Hooks.HookTeleportPx * LevyConstants.Hooks.HookTeleportPx) { sn.HookAdoptTeleport(d.position, d.velocity); Teleports++; } // Hook of Dissonance moves the player itself
				else if (d.velocity != preVel) n.velocity = d.velocity;                                                                                                                   // Thorium's Spring Hook moves the player in its own AI
				ProjTs += Stopwatch.GetTimestamp() - t0; ProjCalls++;
			}
		}

		// ---------------------------------------------------------------------------------------------
		// multiplayer: a soldier's hook is server-side only. Clients get the soldier through NPC sync and draw the chain from SendExtraAI.
		// ---------------------------------------------------------------------------------------------
		private static void SendDetour(On_NetMessage.orig_SendData orig, int msgType, int remoteClient, int ignoreClient, Terraria.Localization.NetworkText text, int number, float number2, float number3, float number4, int number5, int number6, int number7)
		{
			if (msgType == MessageID.PlayerControls) { if (OwnerSlots.IsPooled(number)) { NetSuppressed++; return; } }                                    // 13: the dummy's controls
			else if (msgType == MessageID.SyncProjectile) { if (number >= 0 && number < Main.maxProjectiles && Main.projectile[number].TryGetGlobalProjectile(out HookTag t) && t.Soldier >= 0) { NetSuppressed++; return; } } // 27: the hook itself
			else if (msgType == MessageID.KillProjectile) { if (OwnerSlots.IsPooled((int)number2)) { NetSuppressed++; return; } }                         // 29: its removal (owner in number2)
			else if (msgType == MessageID.TeleportEntity) { if (OwnerSlots.IsPooled((int)number2)) { NetSuppressed++; return; } }                         // 65: a hook teleport of the dummy
			orig(msgType, remoteClient, ignoreClient, text, number, number2, number3, number4, number5, number6, number7);
		}
	}
}
