using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Levy.Common.Config;
using Levy.Common.Globals;
using Levy.Common.Targeting;
using Levy.Content.NPCs;

namespace Levy.Common.Boss
{
	/// <summary>
	/// "Bosses adapt to armies" (LevyConfig.BossesAdaptToArmies, default off) for Calamity's Exo Mechs. Design and measurements: levy-mod/lab/bossai/DESIGN.md.
	/// Calamity writes the Exo Mechs for a handful of players. Three places in its code already know how to fight several targets, or read the boss's target from a part, and the army swap
	/// (EnemyTargeting: a levy's stand-in in Main.player[255] for ONE NPC's AI call) never reaches them:
	///  * ARMS. Ares's four arms (AresLaserCannon, AresPlasmaFlamethrower, AresTeslaCannon, AresGaussNuke) aim at <c>Main.player[Main.npc[draedonExoMechPrime].target]</c>. With Ares locked on a levy that index is 255, and in an
	///    ARM's AI call slot 255 holds vanilla's empty player at the world's top-left corner (an arm is a realLife follower: it has no lock and no swap of its own). Here each arm reads a levy instead: the Laser Cannon
	///    the levy Ares is fighting (Calamity's design: the arms converge on Ares's target); the three area weapons (plasma fireball that bursts into bolts, chained tesla orbs, the Gauss Nuke and its 1350 px blast)
	///    the densest knot of soldiers within reach, held for <see cref="ArmHoldTicks"/> so the barrel turns and the telegraph stays readable.
	///  * VENTS. Thanatos's segments open and fire at up to three players within 2400 px (<c>foreach (Player plr in Main.ActivePlayers)</c>, ThanatosBody1/Body2/Tail): Calamity's own multiplayer attack. The scan only sees
	///    real players, so against an army it fires at the commander or at nobody (the whole underground laser barrage). On a segment's opening ticks the nearest soldiers stand in free player slots for its AI call,
	///    after any real player (players are scanned first, in index order), so the segment fires its normal, predictive and opposite lasers at them.
	///  * HOMING. Apollo's rockets and the Gauss Nuke home on <c>Player.FindClosest</c>: real players only, so they curve toward the commander wherever she stands. For their AI call the nearest soldier stands in a free
	///    player slot when it is nearer than every player, so they home on the nearest target as they would in multiplayer.
	/// Every part acts only while the mech it belongs to is locked on the army (a beaten boss, Bosses target soldiers on): a first kill stays solo, and a fight with no army is untouched (each hook is a pass-through).
	/// Stand-ins are placed only for the one AI call and always put back (the slot's own Player object, the dummy's whoAmI). Lab switches: LEVY_BOSSADAPT=1/0 forces the feature on/off, LEVY_BOSSADAPT_PARTS=arms,vents,homing picks parts.
	/// </summary>
	public class ArmyAwareExo : ModSystem
	{
		/// <summary>How long an area arm keeps the knot it picked (ticks).</summary>
		public const int ArmHoldTicks = 90;
		/// <summary>An area arm looks for soldiers within this of itself (px), and counts the neighbours of a candidate within <see cref="KnotRadiusPx"/>.</summary>
		public const float ArmReachPx = 1800f, KnotRadiusPx = 320f;
		/// <summary>Thanatos's own scan radius and target count (ThanatosBody1: maxDistance 2400, maxTargets 3).</summary>
		public const float VentReachPx = 2400f; public const int VentTargets = 3;

		private static readonly string ForceEnv = Environment.GetEnvironmentVariable("LEVY_BOSSADAPT");
		private static readonly string PartsEnv = Environment.GetEnvironmentVariable("LEVY_BOSSADAPT_PARTS");
		internal static bool ArmsPart = PartOn("arms"), VentsPart = PartOn("vents"), HomingPart = PartOn("homing");
		private static bool PartOn(string p) => string.IsNullOrEmpty(PartsEnv) || Array.IndexOf(PartsEnv.Split(','), p) >= 0;
		/// <summary>World check / lab: overrides the config (null = follow it).</summary>
		internal static bool? Force = ForceEnv == "1" ? true : ForceEnv == "0" ? false : null;

		/// <summary>The setting is on (or forced) and the army targeting it builds on is on.</summary>
		internal static bool SettingOn => (Force ?? (LevyConfig.Get?.BossesAdaptToArmies == true)) && EnemyTargeting.Enabled;

		public static bool Installed { get; private set; }
		private static int _tAres = -1, _tLaser = -1, _tPlasma = -1, _tTesla = -1, _tGauss = -1, _tThanHead = -1, _tApollo = -1;
		private static GlobalNPC _calG; private static FieldInfo _newAI;

		// ---- telemetry (lab + world check) ----
		/// <summary>Arm AI calls with Ares on the army; of those, calls that read a levy through this feature, and calls that (feature off) read the empty slot.</summary>
		public static long ArmCalls, ArmLent, ArmReadEmpty;
		/// <summary>Segment AI calls on an opening tick with Thanatos on the army; stand-ins placed for them.</summary>
		public static long VentCalls, VentPlaced;
		/// <summary>Homing projectile AI calls; calls where a soldier stood in because it was nearer than every player.</summary>
		public static long HomingCalls, HomingPlaced;
		/// <summary>Lab probe: an arm's AI is about to run reading this point (the arm, what it reads in Main.player[Ares's target]).</summary>
		internal static Action<NPC, Player> ArmProbe;

		public override void PostSetupContent()
		{
			try
			{
				if (!ModLoader.TryGetMod("CalamityMod", out Mod cal)) return;
				_tAres = T("AresBody"); _tLaser = T("AresLaserCannon"); _tPlasma = T("AresPlasmaFlamethrower"); _tTesla = T("AresTeslaCannon"); _tGauss = T("AresGaussNuke");
				_tThanHead = T("ThanatosHead"); _tApollo = T("Apollo");
				if (ModContent.TryFind("CalamityMod/CalamityGlobalNPC", out GlobalNPC g)) { _calG = g; _newAI = g.GetType().GetField("newAI", BindingFlags.Public | BindingFlags.Instance); }
				int n = 0;
				foreach (string arm in new[] { "Ares.AresLaserCannon", "Ares.AresPlasmaFlamethrower", "Ares.AresTeslaCannon", "Ares.AresGaussNuke" }) n += HookNpc(cal, "CalamityMod.NPCs.ExoMechs." + arm, (Action<OrigNpcAI, ModNPC>)ArmAiDetour);
				foreach (string seg in new[] { "Thanatos.ThanatosBody1", "Thanatos.ThanatosBody2", "Thanatos.ThanatosTail" }) n += HookNpc(cal, "CalamityMod.NPCs.ExoMechs." + seg, (Action<OrigNpcAI, ModNPC>)VentAiDetour);
				foreach (string pr in new[] { "CalamityMod.Projectiles.Boss.ApolloRocket", "CalamityMod.Projectiles.Boss.AresGaussNukeProjectile" })
				{
					MethodInfo ai = cal.Code.GetType(pr)?.GetMethod("AI", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
					if (ai != null && ai.DeclaringType.FullName == pr) { MonoModHooks.Add(ai, (Action<OrigProjAI, ModProjectile>)HomingAiDetour); n++; }
				}
				Installed = n == 9;
				Mod.Logger.Info($"Levy army-aware Exo Mechs: {n}/9 hooks installed (arms, vents, homing){(Installed ? "" : " - INCOMPLETE")}");
			}
			catch (Exception e) { Mod.Logger.Warn("Levy army-aware Exo Mechs failed to install: " + e); }
		}

		private static int T(string name) => ModContent.TryFind("CalamityMod/" + name, out ModNPC m) ? m.Type : -1;

		private delegate void OrigNpcAI(ModNPC self);
		private delegate void OrigProjAI(ModProjectile self);

		private static int HookNpc(Mod cal, string type, Action<OrigNpcAI, ModNPC> hook)
		{
			MethodInfo ai = cal.Code.GetType(type)?.GetMethod("AI", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
			if (ai == null || ai.DeclaringType.FullName != type) return 0;
			MonoModHooks.Add(ai, hook);
			return 1;
		}

		// =====================================================================================================
		// Shared: is this mech on the army, and the live levy bodies
		// =====================================================================================================

		/// <summary>The levy body a boss NPC is locked on through the army swap (its target is the stand-in slot), or null.</summary>
		private static ITargetBody ArmyLock(NPC boss)
		{
			if (boss == null || !boss.active || boss.target != SoldierNPC.DummySlot) return null;
			ITargetBody b = EnemyTargeting.LiveTarget(LevyNpcData.Of(boss).SoldierTarget);
			return b != null && b.IsAlive && !EnemyTargeting.IsMachineBody(b.NPC) ? b : null;
		}

		private static bool Fieldable(ITargetBody b) => b != null && b.IsAlive && b.Dummy != null && !EnemyTargeting.IsMachineBody(b.NPC);

		// =====================================================================================================
		// ARMS
		// =====================================================================================================
		private static readonly int[] _armPick = new int[Main.maxNPCs];
		private static readonly uint[] _armPickAt = new uint[Main.maxNPCs];
		private static int _hl_ArmAiDetour_1;

		private static void ArmAiDetour(OrigNpcAI orig, ModNPC self)
		{
			_hl_ArmAiDetour_1++;
			NPC arm = self.NPC;
			int b = (int)arm.ai[2];
			NPC body = b >= 0 && b < Main.maxNPCs && Main.npc[b].active && Main.npc[b].type == _tAres ? Main.npc[b] : null;
			ITargetBody aresLock = body != null && !EnemyTargeting.IsSwapped ? ArmyLock(body) : null;
			if (aresLock == null) { orig(self); return; }
			ArmCalls++;
			ITargetBody pick = null;
			if (ArmsPart && SettingOn) pick = arm.type == _tLaser ? aresLock : KnotPick(arm, aresLock);
			if (pick == null)
			{
				Player seen = Main.player[body.target];
				if (seen == null || !seen.active) ArmReadEmpty++;
				ArmProbe?.Invoke(arm, seen);
				orig(self);
				return;
			}
			ArmLent++;
			EnemyTargeting.Swap(pick, SoldierNPC.DummySlot);
			try { ArmProbe?.Invoke(arm, Main.player[SoldierNPC.DummySlot]); orig(self); }
			finally { EnemyTargeting.Unswap(); }
		}

		/// <summary>The soldier at the heart of the densest knot within an area arm's reach, held for ArmHoldTicks; Ares's own levy when nobody is in reach.</summary>
		private static ITargetBody KnotPick(NPC arm, ITargetBody fallback)
		{
			int w = arm.whoAmI; uint now = Main.GameUpdateCount;
			if (_armPickAt[w] != 0 && now >= _armPickAt[w] && now - _armPickAt[w] < ArmHoldTicks)
			{
				ITargetBody held = EnemyTargeting.LiveTarget(_armPick[w]);
				if (Fieldable(held) && Vector2.DistanceSquared(held.NPC.Center, arm.Center) <= ArmReachPx * ArmReachPx) return held;
			}
			var bodies = EnemyTargeting.Bodies;
			ITargetBody best = null; int bestN = -1; float bestD = float.MaxValue;
			float reach2 = ArmReachPx * ArmReachPx, knot2 = KnotRadiusPx * KnotRadiusPx;
			for (int i = 0; i < bodies.Count; i++)
			{
				ITargetBody c = bodies[i];
				if (!Fieldable(c)) continue;
				Vector2 at = c.NPC.Center;
				float d = Vector2.DistanceSquared(at, arm.Center);
				if (d > reach2) continue;
				int nb = 0;
				for (int j = 0; j < bodies.Count; j++) { ITargetBody o = bodies[j]; if (j != i && Fieldable(o) && Vector2.DistanceSquared(o.NPC.Center, at) <= knot2) nb++; }
				if (nb > bestN || (nb == bestN && d < bestD)) { bestN = nb; bestD = d; best = c; }
			}
			best ??= fallback;
			_armPick[w] = best.NPC.whoAmI; _armPickAt[w] = Math.Max(1u, now);
			return best;
		}

		// =====================================================================================================
		// VENTS
		// =====================================================================================================
		private static int _hl_VentAiDetour_2;
		private static readonly ITargetBody[] _ventPick = new ITargetBody[VentTargets];
		private static readonly float[] _ventD = new float[VentTargets];

		private static void VentAiDetour(OrigNpcAI orig, ModNPC self)
		{
			_hl_VentAiDetour_2++;
			NPC seg = self.NPC;
			if (!VentsPart || !SettingOn || EnemyTargeting.IsSwapped || EnemyTargeting.Bodies.Count == 0 || !Opening(seg)) { orig(self); return; }
			int h = seg.realLife;
			NPC head = h >= 0 && h < Main.maxNPCs && Main.npc[h].active && Main.npc[h].type == _tThanHead ? Main.npc[h] : null;
			if (ArmyLock(head) == null) { orig(self); return; }
			VentCalls++;
			// Calamity takes the first VentTargets live players within VentReachPx in index order: real players first, then the soldiers placed below (nearest first, lowest free slot first).
			int players = 0;
			for (int i = 0; i < Main.maxPlayers; i++) { Player p = Main.player[i]; if (p != null && p.active && !p.dead && Vector2.Distance(p.Center, seg.Center) < VentReachPx) players++; }
			int want = VentTargets - players;
			if (want <= 0) { orig(self); return; }
			int got = NearestBodies(seg.Center, VentReachPx, want);
			if (got == 0) { orig(self); return; }
			int placed = Place(_ventPick, got);
			VentPlaced += placed;
			try { orig(self); }
			finally { Restore(); }
		}

		/// <summary>True on the ticks a segment is opening its vent (Calamity: newAI[0] counts up to the fire tick while newAI[1] is 0): the only ticks its laser scan can run.</summary>
		private static bool Opening(NPC seg)
		{
			if (_calG == null || _newAI == null) return true;
			try { return seg.TryGetGlobalNPC(_calG, out GlobalNPC g) && _newAI.GetValue(g) is float[] a && a.Length > 1 && a[1] == 0f && a[0] > 0f; }
			catch (Exception) { return true; }
		}

		/// <summary>Fills _ventPick with up to n live soldier bodies nearest to <paramref name="at"/> within reach, nearest first. Returns how many.</summary>
		private static int NearestBodies(Vector2 at, float reach, int n)
		{
			int k = 0; float r2 = reach * reach;
			var bodies = EnemyTargeting.Bodies;
			for (int i = 0; i < bodies.Count; i++)
			{
				ITargetBody c = bodies[i];
				if (!Fieldable(c)) continue;
				float d = Vector2.DistanceSquared(c.NPC.Center, at);
				if (d >= r2) continue;
				int pos = k < n ? k++ : n;
				if (pos == n) { if (d >= _ventD[n - 1]) continue; pos = n - 1; }
				while (pos > 0 && _ventD[pos - 1] > d) { _ventD[pos] = _ventD[pos - 1]; _ventPick[pos] = _ventPick[pos - 1]; pos--; }
				_ventD[pos] = d; _ventPick[pos] = c;
			}
			return k;
		}

		// =====================================================================================================
		// HOMING
		// =====================================================================================================
		private static int _hl_HomingAiDetour_3;
		private static readonly ITargetBody[] _one = new ITargetBody[1];

		private static void HomingAiDetour(OrigProjAI orig, ModProjectile self)
		{
			_hl_HomingAiDetour_3++;
			Projectile p = self.Projectile;
			if (!HomingPart || !SettingOn || EnemyTargeting.IsSwapped || EnemyTargeting.Bodies.Count == 0 || !p.active || !p.hostile || !ExoOnArmy()) { orig(self); return; }
			HomingCalls++;
			ITargetBody near = EnemyTargeting.NearestBody(p.Center, float.PositiveInfinity, out float dl);
			if (!Fieldable(near)) { orig(self); return; }
			for (int i = 0; i < Main.maxPlayers; i++)
			{
				Player pl = Main.player[i];
				if (pl != null && pl.active && !pl.dead && Vector2.Distance(pl.Center, p.Center) <= dl) { orig(self); return; } // a player is nearer: Calamity's own pick stands
			}
			_one[0] = near;
			if (Place(_one, 1) == 0) { orig(self); return; }
			HomingPlaced++;
			try { orig(self); }
			finally { Restore(); }
		}

		private static uint _exoAt = uint.MaxValue; private static bool _exoOn;
		/// <summary>Ares or Apollo (the two mechs these projectiles come from) is locked on the army this tick.</summary>
		private static bool ExoOnArmy()
		{
			if (_exoAt == Main.GameUpdateCount) return _exoOn;
			_exoAt = Main.GameUpdateCount; _exoOn = false;
			for (int i = 0; i < Main.maxNPCs && !_exoOn; i++) { NPC n = Main.npc[i]; if (n.active && (n.type == _tAres || n.type == _tApollo) && ArmyLock(n) != null) _exoOn = true; }
			return _exoOn;
		}

		// =====================================================================================================
		// Stand-ins in free player slots, for one AI call
		// =====================================================================================================
		private static readonly int[] _slot = new int[VentTargets];
		private static readonly Player[] _was = new Player[VentTargets];
		private static readonly Player[] _dummy = new Player[VentTargets];
		private static int _placed;

		/// <summary>Puts the dummies of the first <paramref name="n"/> bodies into free player slots (inactive, below 255), the first body in the lowest of them. Returns how many were placed.</summary>
		private static int Place(ITargetBody[] bodies, int n)
		{
			_placed = 0;
			int free = 0;
			for (int k = Main.maxPlayers - 1; k >= 1 && free < n; k--) { Player q = Main.player[k]; if (q == null || !q.active) _slot[free++] = k; }
			// lowest slot for the first body: reverse the found slots
			for (int i = 0; i < free; i++)
			{
				int k = _slot[free - 1 - i]; ITargetBody b = bodies[i];
				if (!Fieldable(b)) continue;
				bool dup = false; for (int j = 0; j < _placed; j++) if (ReferenceEquals(_dummy[j], b.Dummy)) dup = true;
				if (dup) continue;
				b.SyncDummyTransform();
				b.Dummy.whoAmI = k;
				_was[_placed] = Main.player[k]; _dummy[_placed] = b.Dummy; _slotUsed[_placed] = k;
				Main.player[k] = b.Dummy;
				_placed++;
			}
			return _placed;
		}
		private static readonly int[] _slotUsed = new int[VentTargets];

		private static void Restore()
		{
			for (int i = 0; i < _placed; i++)
			{
				int k = _slotUsed[i];
				if (ReferenceEquals(Main.player[k], _dummy[i])) Main.player[k] = _was[i] ?? new Player();
				if (_dummy[i] != null) _dummy[i].whoAmI = SoldierNPC.DummySlot;
				_was[i] = null; _dummy[i] = null;
			}
			_placed = 0;
		}

		/// <summary>Safety net: a stand-in must never survive its AI call (an exception skipped the finally only if the process is going down anyway; this is cheap).</summary>
		public override void PostUpdateNPCs() { if (_placed > 0) Restore(); }
		public override void PostUpdateProjectiles() { if (_placed > 0) Restore(); }

		internal static void ResetCounters() { ArmCalls = ArmLent = ArmReadEmpty = VentCalls = VentPlaced = HomingCalls = HomingPlaced = 0; Array.Clear(_armPickAt, 0, _armPickAt.Length); }
	}
}
