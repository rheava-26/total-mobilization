using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Compat.Calamity;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Weapons;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	public partial class IdentityWorldCheck
	{
		// ---------------------------------------------------------------------------------------------- ROGUE ARMY SWEEP: stealth strikes, a levy against a player who plays them well
		// LEVY_ARMYSWEEP_ROGUE=1 (with LEVY_ARMYSWEEP_ALONE=1): each rogue weapon is fired for LEVY_ARMYSWEEP_TICKS (default 1200) ticks at the boss-flagged dummy 12 tiles away, alone ("solo") and with three pinned
		// small enemies around it ("crowd": a strike that ricochets, pierces or splits has something to hit). The levy and the stand-in player (slot 255, vanilla ItemCheck, its armor, accessories and Calamity's
		// stealth step run every tick as Player.Update runs them) wear the same rogue set (LEVY_ARMYSWEEP_ROGUE_SET=Head,Body,Legs; default Statigel) and accessory (LEVY_ARMYSWEEP_ROGUE_ACC; default none).
		// The stand-in plays two ways: "wait" holds the button until the bar is full and throws the strike (a held-then-thrown grenade, the Doomsday Device, is wound up on a full bar and let go the tick its charge
		// is full), "spam" throws at the weapon's own rate. Recorded: damage on every pinned enemy, throws, strikes (projectiles Calamity flagged), strike hits and ricochets (a strike's second and later hits).
		// LEVY_ARMYSWEEP_ONLY=Name;Name picks the weapons (default ArmyRogueList), LEVY_ARMYSWEEP_ROGUE_FILL=0 skips the bar-fill parity probe.
		private static readonly string[] ArmyRogueList = { "Doomsday Device", "Stellar Knife", "The Syringe", "Regulus Riot", "Shinobi Blade", "Cobalt Kunai", "Ballistic Poison Bomb" };
		private static readonly List<NPC> _rogueAdds = new();
		private static readonly List<Vector2> _rogueAddsAt = new();
		private static int _rogueEqErr;

		private sealed class RogueRes
		{
			public string Who = "", Notes = ""; public long Dmg; public int Hits, Throws, Strikes, Flagged, Moving, Partial, NearWaits, StrikeHits, Ricochets, MaxChain, Ran, BarFullTicks, Holds, HoldAnim, HoldTicks, StrikeProjs; public float BarMax; public string Err;
			public double PerMin(int n) => n * 3600.0 / Math.Max(1, Ran);
			public double Dps => Dmg * 60.0 / Math.Max(1, Ran);
			public override string ToString() => $"{Who}: dps {Dps:0}, throws {Throws}, strikes {Strikes} ({PerMin(Strikes):0.0}/min), strike hits {StrikeHits}, ricochets {Ricochets} (longest chain {MaxChain}), flagged shots {Flagged}, hits {Hits}, moving {Moving} ticks, bar {BarMax:0.##} full {BarFullTicks}/{Ran} ticks, policy holds {Holds}{(HoldTicks > 0 ? $", grenade held {HoldTicks} ticks ({HoldAnim} with the use animation running)" : "")}{(Notes.Length > 0 ? " [" + Notes + "]" : "")}{(Err != null ? ", THREW " + Err : "")}";
		}

		/// <summary>Pins the rogue sweep's extra enemies in place at full life (called with the sweep target's pin, before the NPCs update).</summary>
		private static void PinRogueAdds()
		{
			for (int i = 0; i < _rogueAdds.Count; i++)
			{
				NPC n = _rogueAdds[i]; if (n == null || !n.active) continue;
				n.timeLeft = 9999; n.Bottom = _rogueAddsAt[i]; n.velocity = Vector2.Zero; n.life = n.lifeMax; n.dontTakeDamage = false;
			}
		}

		private static void ClearRogueAdds() { foreach (NPC n in _rogueAdds) if (n != null) n.active = false; _rogueAdds.Clear(); _rogueAddsAt.Clear(); ArmySweepTally.Extra.Clear(); }

		/// <summary>Three small pinned enemies around the sweep target: 5 and 10 tiles beyond it and 5 beyond, 8 up.</summary>
		private static void SpawnRogueAdds(int n)
		{
			ClearRogueAdds();
			Vector2[] offs = { new Vector2(5 * 16f, 0f), new Vector2(10 * 16f, 0f), new Vector2(5 * 16f, -8 * 16f), new Vector2(-4 * 16f, -10 * 16f) };
			for (int i = 0; i < n && i < offs.Length; i++)
			{
				Vector2 at = _sweepTargetAt + offs[i];
				int ti = NPC.NewNPC(new EntitySource_WorldEvent("IdentityWorldCheck"), (int)at.X, (int)at.Y, NPCID.GreenSlime);
				if (ti < 0 || ti >= Main.maxNPCs) continue;
				NPC t = Main.npc[ti];
				t.lifeMax = 1_000_000; t.life = t.lifeMax; t.defense = 0; t.damage = 0; t.knockBackResist = 0f; t.width = 32; t.height = 32; t.boss = false;
				t.noGravity = true; t.noTileCollide = true; t.aiStyle = -1; t.friendly = false; t.value = 0f; t.Bottom = at;
				var ht = t.GetGlobalNPC<HarnessTarget>(); ht.IsTarget = true; ht.Pin = t.position;
				_rogueAdds.Add(t); _rogueAddsAt.Add(at); ArmySweepTally.Extra.Add(ti);
			}
		}

		private static int RogueItem(string name) => ModContent.TryFind("CalamityMod", name, out ModItem mi) ? mi.Type : 0;
		private static int[] RogueSet()
		{
			string[] s = (Environment.GetEnvironmentVariable("LEVY_ARMYSWEEP_ROGUE_SET") ?? "StatigelHeadRogue,StatigelArmor,StatigelGreaves").Split(',');
			return s.Length == 3 ? s.Select(RogueItem).ToArray() : new[] { 0, 0, 0 };
		}
		private static int RogueAcc() { string a = Environment.GetEnvironmentVariable("LEVY_ARMYSWEEP_ROGUE_ACC"); return string.IsNullOrEmpty(a) || a == "none" ? 0 : RogueItem(a); }

		/// <summary>The stand-in's own per-tick build, in Player.Update's order: ResetEffects, the equips and set bonus, the mods' PostUpdateEquips and PostUpdateMiscEffects (Calamity's stealth step). ItemCheck follows.</summary>
		private static void RogueStandInUpdate(Player p)
		{
			int saved = Main.myPlayer; Main.myPlayer = 255;
			try { p.ResetEffects(); p.UpdateEquips(255); p.UpdateArmorSets(255); PlayerLoader.PostUpdateEquips(p); PlayerLoader.PostUpdateMiscEffects(p); }
			catch (Exception e) { var ie = e is TargetInvocationException && e.InnerException != null ? e.InnerException : e; if (_rogueEqErr++ < 3) Note($"rogue sweep: the stand-in's update threw {ie.GetType().Name}: {ie.Message} @ {(ie.StackTrace ?? "").Split('\n')[0].Trim()}"); }
			finally { Main.myPlayer = saved; }
		}

		private static FieldInfo _ddFlung, _ddFull;
		/// <summary>The stand-in's grenade still in its hand (not flung), and whether its charge is full.</summary>
		private static Projectile GrenadeInHand(int owner, int type, out bool full)
		{
			full = false;
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile q = Main.projectile[i];
				if (!q.active || q.owner != owner || q.type != type || q.ModProjectile == null) continue;
				Type t = q.ModProjectile.GetType();
				_ddFlung ??= t.GetField("flung", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				_ddFull ??= t.GetField("hasReachedFullCharge", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (_ddFlung == null || _ddFull == null || (bool)_ddFlung.GetValue(q.ModProjectile)) continue;
				full = (bool)_ddFull.GetValue(q.ModProjectile);
				return q;
			}
			return null;
		}

		private static RogueRes ArmyRogueOne(int weaponType, string who, int adds, int ticks, float startBar = 0f)
		{
			var res = new RogueRes { Who = who };
			bool player = who != "levy", spam = who == "spam";
			ClearSoldiers(); ClearRogueAdds();
			_cmdAt = new Vector2((Cx - 60) * 16f, (FloorY - 3) * 16f); _cmdHolds = false; ReleaseCommanderButtons();
			Vector2 at = new Vector2(Cx * 16f + 8f, FloorY * 16f);
			Item w = new Item(); w.SetDefaults(weaponType);
			bool grenade = w.channel && w.noUseGraphic && w.useStyle == ItemUseStyleID.Swing;
			SpawnOriginTarget(at); _sweepTarget.boss = true; SpawnRogueAdds(adds);
			int[] set = RogueSet(); int acc = RogueAcc();
			Player scratch = null, saved255 = null; int body = -1; SoldierRecord rec = null; SoldierNPC soldier = null;
			ArmySweepTally.Target = _sweepTarget.whoAmI; ArmySweepTally.Rogue = true;
			int holds0 = StealthPolicy.Holds, near0 = StealthPolicy.NearFull, fin0 = StealthPolicy.Finishes, full0 = RogueGrenade.FullThrows, gw0 = RogueGrenade.GraceWaits;
			try
			{
				if (player)
				{
					saved255 = Main.player[255]; scratch = new Player { whoAmI = 255, name = "ArmyScratch" }; scratch.ResetEffects();
					Main.player[255] = scratch; _my = 255; if (Main.GameViewMatrix == null) Main.GameViewMatrix = new Terraria.Graphics.SpriteViewMatrix(null);
					ArmyPlayerSetup(scratch, w, 0, at);
					for (int i = 0; i < 3; i++) { Item a = new Item(); if (set[i] > 0) a.SetDefaults(set[i]); scratch.armor[i] = a; }
					if (acc > 0) { Item a = new Item(); a.SetDefaults(acc); scratch.armor[3] = a; }
				}
				else
				{
					rec = MakeOriginRecord(w, 0); rec.Order = SoldierOrder.Hold;
					for (int i = 0; i < 3; i++) { Item a = new Item(); if (set[i] > 0) a.SetDefaults(set[i]); if (i == 0) rec.Gear.Head = a; else if (i == 1) rec.Gear.Body = a; else rec.Gear.Legs = a; }
					rec.Gear.SetLocked(SoldierGear.SlotHead, true); rec.Gear.SetLocked(SoldierGear.SlotBody, true); rec.Gear.SetLocked(SoldierGear.SlotLegs, true);
					if (acc > 0) { Item a = new Item(); a.SetDefaults(acc); rec.Gear.Accessories[0] = a; rec.Gear.SetLocked(SoldierGear.SlotAcc0, true); }
					RosterSystem.Instance.Regulars.Add(rec);
					body = RosterSystem.Instance.SpawnBody(rec, at);
					if (body < 0 || !rec.HasBody) throw new Exception("levy did not spawn");
					Main.npc[body].Bottom = at; rec.HoldPos = Main.npc[body].Center; soldier = Main.npc[body].ModNPC as SoldierNPC;
				}
				Main.rand = new Terraria.Utilities.UnifiedRandom(777);
				KeepCommander(); PinSweepTarget(); PinRogueAdds(); CursorOnCommander();
				for (int k = 0; k < 20; k++) { if (player) RogueStandInUpdate(scratch); Step(1); } // the levy's gear frame and the stand-in's build settle
				for (int i = 0; i < Main.maxProjectiles; i++) Main.projectile[i].active = false;
				Player actorP = player ? scratch : soldier?.Dummy;
				float[] bar0 = HarnessCalamityProbe.Stealth(actorP);
				HarnessCalamityProbe.SetStealth(actorP, bar0 != null ? Math.Clamp(startBar, 0f, 1f) * bar0[1] : 0f); // both start on the same bar (empty unless asked)
				ArmySweepTally.Hits = 0; ArmySweepTally.Damage = 0; ArmySweepTally.ByProj = new(); ArmySweepTally.StrikeHits = 0; ArmySweepTally.StrikeHitsByProj = new(); ArmySweepTally.On = true;
				var was = new int[Main.maxProjectiles]; var flagged = new bool[Main.maxProjectiles]; var flung = new bool[Main.maxProjectiles]; // per slot: the type there last tick (0 = empty), the strike flag and the grenade's thrown state
				void EndChain(int slot) { if (ArmySweepTally.StrikeHitsByProj.Remove(slot, out int n)) { res.StrikeProjs++; res.Ricochets += Math.Max(0, n - 1); res.MaxChain = Math.Max(res.MaxChain, n); } flagged[slot] = false; flung[slot] = false; }
				int prevAnim = 0; uint prevUse = 0; bool prevReady = false; float prevBar = 0f;
				for (int k = 0; k < ticks; k++)
				{
					if (player)
					{
						RogueStandInUpdate(scratch);
						bool ready = HarnessCalamityProbe.StealthStrikeAvailable(scratch);
						int press;
						if (grenade)
						{
							Projectile g = GrenadeInHand(255, w.shoot, out bool full);
							if (g != null) { press = full ? 0 : 1; res.HoldTicks++; if (scratch.itemAnimation > 0) res.HoldAnim++; }
							else press = spam || ready ? 1 : 0;
						}
						else press = spam ? -1 : ready ? 1 : 0;
						ArmyPlayerUse(scratch, w, at, _sweepTarget.Center, k, press);
						if (!grenade && scratch.itemAnimation > prevAnim) res.Throws++; prevAnim = scratch.itemAnimation;
						scratch.numMinions = 0; scratch.slotsMinions = 0f; Array.Clear(scratch.ownedProjectileCounts, 0, scratch.ownedProjectileCounts.Length);
						Main.screenPosition = scratch.Center - new Vector2(Main.screenWidth / 2f, Main.screenHeight / 2f); Main.mouseX = (int)(_sweepTarget.Center.X - Main.screenPosition.X); Main.mouseY = (int)(_sweepTarget.Center.Y - Main.screenPosition.Y);
						Step(1);
					}
					else
					{
						rec.Mana = rec.MaxMana; OriginStep();
						if (!Main.npc[body].active || Main.npc[body].ModNPC is not SoldierNPC) { res.Err = "levy gone"; break; }
						if (grenade && soldier.Dummy != null && GrenadeInHand(soldier.Dummy.whoAmI, w.shoot, out _) != null) res.HoldTicks++;
						if (!grenade && soldier.LastUseTick != prevUse) { if (prevUse != 0) res.Throws++; prevUse = soldier.LastUseTick; }
						if (Math.Abs(Main.npc[body].velocity.X) > 0.1f || Math.Abs(Main.npc[body].velocity.Y) > 0.1f) res.Moving++;
					}
					ClearNaturalHostiles(); ArmyReadDamage(new ArmyRes());
					res.Ran++;
					float[] bar = HarnessCalamityProbe.Stealth(actorP);
					if (bar != null) { res.BarMax = bar[1]; if (bar[1] > 0f && bar[0] >= bar[1] - 1e-4f) res.BarFullTicks++; if (prevReady && bar[0] < prevBar - 0.5f * bar[1]) res.Strikes++; prevBar = bar[0]; } // a strike: a strike-ready bar spent
					prevReady = HarnessCalamityProbe.StealthStrikeAvailable(actorP);
					for (int i = 0; i < Main.maxProjectiles; i++)
					{
						Projectile p = Main.projectile[i];
						int now = p.active && !p.hostile ? p.type : 0;
						if (was[i] != 0 && now != was[i]) EndChain(i); // the projectile that was here is gone (or another took its slot)
						was[i] = now; if (now == 0) continue;
						if (!flagged[i] && HarnessCalamityProbe.IsStealthStrike(p, out bool sub) && !sub) { flagged[i] = true; res.Flagged++; }
						if (grenade && p.type == w.shoot && !flung[i] && _ddFlung != null && (bool)_ddFlung.GetValue(p.ModProjectile))
						{
							flung[i] = true; res.Throws++; if (!(bool)_ddFull.GetValue(p.ModProjectile)) res.Partial++;
							if (ArmyDebug) Note($"  rogue dbg {who} t={k} throw: strike {flagged[i]} full charge {(bool)_ddFull.GetValue(p.ModProjectile)} bar {(bar != null ? bar[0] : -1):0.###}/{(bar != null ? bar[1] : -1):0.###}");
						}
					}
				}
				res.Dmg = ArmySweepTally.Damage; res.Hits = ArmySweepTally.Hits; res.StrikeHits = ArmySweepTally.StrikeHits;
				for (int i = 0; i < Main.maxProjectiles; i++) EndChain(i);
				res.Holds = StealthPolicy.Holds - holds0; res.NearWaits = StealthPolicy.NearFull - near0; res.Notes = $"near-full waits {StealthPolicy.NearFull - near0}, finishes {StealthPolicy.Finishes - fin0}, grenade full-charge throws {RogueGrenade.FullThrows - full0}, grace ticks {RogueGrenade.GraceWaits - gw0}";
			}
			catch (Exception e) { res.Err = e.GetType().Name + ": " + e.Message + " @ " + string.Join(" < ", (e.StackTrace ?? "").Split('\n').Take(3).Select(l => l.Trim())); }
			finally
			{
				ArmySweepTally.On = false; ArmySweepTally.Rogue = false;
				if (player) { _my = 0; if (scratch != null && ReferenceEquals(Main.player[255], scratch)) Main.player[255] = saved255; global::Levy.Common.Targeting.EnemyTargeting.ResetState(); }
				ClearSoldiers(); ClearRogueAdds(); ReleaseCommanderButtons(); _cmdAt = OriginCmdAt();
			}
			return res;
		}

		/// <summary>
		/// Ticks for an emptied bar to fill: the stand-in (standing, or moving at 4 px a tick) and a levy (standing at its post, or walking to a post 60 tiles away), in the same armor and accessory.
		/// Returns {stand-in standing, stand-in moving, levy standing, levy moving, ticks the levy moved in its standing run, in its walking run}; -1 when the bar never filled within 700 ticks.
		/// </summary>
		private static int[] RogueFill(int acc)
		{
			var r = new[] { -1, -1, -1, -1, 0, 0 };
			int[] set = RogueSet(); if (set.Any(x => x <= 0)) return r;
			ClearSoldiers(); ClearRogueAdds(); if (_sweepTarget != null) { _sweepTarget.active = false; _sweepTarget = null; }
			_cmdAt = new Vector2((Cx - 60) * 16f, (FloorY - 3) * 16f);
			Vector2 at = new Vector2((Cx - 40) * 16f + 8f, FloorY * 16f);
			// the stand-in
			Player saved255 = Main.player[255]; Player scratch = new Player { whoAmI = 255, name = "ArmyScratch" }; scratch.ResetEffects(); Main.player[255] = scratch;
			try
			{
				ArmyPlayerSetup(scratch, new Item(), 0, at);
				for (int i = 0; i < 3; i++) { Item a = new Item(); a.SetDefaults(set[i]); scratch.armor[i] = a; }
				if (acc > 0) { Item a = new Item(); a.SetDefaults(acc); scratch.armor[3] = a; }
				for (int m = 0; m < 2; m++)
				{
					for (int k = 0; k < 5; k++) RogueStandInUpdate(scratch);
					HarnessCalamityProbe.SetStealth(scratch, 0f);
					for (int k = 1; k <= 700; k++)
					{
						scratch.velocity = m == 1 ? new Vector2(4f, 0f) : Vector2.Zero; RogueStandInUpdate(scratch);
						float[] b = HarnessCalamityProbe.Stealth(scratch); if (b != null && b[1] > 0f && b[0] >= b[1] - 1e-4f) { r[m] = k; break; }
					}
				}
			}
			finally { if (ReferenceEquals(Main.player[255], scratch)) Main.player[255] = saved255; }
			// the levy
			for (int m = 0; m < 2; m++)
			{
				ClearSoldiers();
				Item w = new Item(); w.SetDefaults(RogueItem("WulfrumKnife"));
				SoldierRecord rec = MakeOriginRecord(w, 0); rec.Order = SoldierOrder.Hold; rec.HoldFire = true; // no throw empties the bar while it is timed
				Item h = new Item(); h.SetDefaults(set[0]); Item bo = new Item(); bo.SetDefaults(set[1]); Item l = new Item(); l.SetDefaults(set[2]);
				rec.Gear.Head = h; rec.Gear.Body = bo; rec.Gear.Legs = l; rec.Gear.SetLocked(SoldierGear.SlotHead, true); rec.Gear.SetLocked(SoldierGear.SlotBody, true); rec.Gear.SetLocked(SoldierGear.SlotLegs, true);
				if (acc > 0) { Item a = new Item(); a.SetDefaults(acc); rec.Gear.Accessories[0] = a; rec.Gear.SetLocked(SoldierGear.SlotAcc0, true); }
				RosterSystem.Instance.Regulars.Add(rec);
				int body = RosterSystem.Instance.SpawnBody(rec, at); if (body < 0) continue;
				Main.npc[body].Bottom = at; rec.HoldPos = Main.npc[body].Center;
				SoldierNPC s = Main.npc[body].ModNPC as SoldierNPC;
				for (int k = 0; k < 20; k++) { Step(1); ClearNaturalHostiles(); }
				if (m == 1) rec.HoldPos = Main.npc[body].Center + new Vector2(60 * 16f, 0f); // walk 60 tiles to the new post
				HarnessCalamityProbe.SetStealth(s.Dummy, 0f);
				int moving = 0;
				for (int k = 1; k <= 700; k++)
				{
					Step(1); ClearNaturalHostiles();
					if (s.Dummy.velocity.Length() >= 0.1f) moving++; // Calamity's test (Player.StandingStill(0.1f)) on the levy's player
					float[] b = HarnessCalamityProbe.Stealth(s.Dummy);
					if (ArmyDebug && (k <= 4 || k % 60 == 0)) Note($"  rogue fill dbg {(m == 1 ? "walk" : "stand")} t={k} bar {(b != null ? b[0] : -1):0.###}/{(b != null ? b[1] : -1):0.###} npcVel {Main.npc[body].velocity.X:0.##},{Main.npc[body].velocity.Y:0.##} dummyVel {s.Dummy.velocity.X:0.##},{s.Dummy.velocity.Y:0.##} anim {s.Dummy.itemAnimation} upkeep {s.UpkeepActive} frame {s.LastFrameKind} state {s.State} tail {GearUpkeep.TailRuns}"); if (b != null && b[1] > 0f && b[0] >= b[1] - 1e-4f) { r[2 + m] = k; break; }
				}
				r[4 + m] = moving;
			}
			ClearSoldiers();
			return r;
		}

		/// <summary>
		/// The ship's rogue checks (Calamity half, with the army sweep): a levy's stealth bar fills as a player's does (standing, walking, with a stealth accessory); a levy with a full bar throws a real stealth strike;
		/// a levy with a nearly full bar waits for it (on a weapon whose policy is to throw at its own rate) and strikes; a Doomsday Device levy lets every grenade go on a full charge and its strikes ricochet.
		/// </summary>
		private static void ArmyRogueChecks()
		{
			if (!HarnessCalamityProbe.Available) { Note("rogue checks: Calamity's stealth fields are not readable"); return; }
			int dd = RogueItem("DoomsdayDevice"), sk = RogueItem("StellarKnife"), em = RogueItem("EclipseMirror");
			if (dd <= 0 || sk <= 0 || RogueSet().Any(x => x <= 0)) { Check($"rogue checks: the kit exists (Doomsday {dd}, Stellar Knife {sk}, set {string.Join("/", RogueSet())})", false); return; }
			foreach (int acc in em > 0 ? new[] { 0, em } : new[] { 0 })
			{
				int[] f = RogueFill(acc);
				// the levy's fill, tick by tick, at the stand-in's measured rate for what the levy did that tick (stood or moved): a bar that fills as a player's sums to one
				double Sum(int fill, int moved) => fill <= 0 || f[0] <= 0 || f[1] <= 0 ? -1 : (fill - moved) / (double)f[0] + moved / (double)f[1];
				double s0 = Sum(f[2], f[4]), s1 = Sum(f[3], f[5]);
				Check($"rogue: a levy's stealth bar fills as a player's does{(acc > 0 ? " with the " + ContentSamples.ItemsByType[acc].Name : "")} (player: standing {f[0]} ticks, moving {f[1]}; levy at its post {f[2]} ticks with {f[4]} moving, walking {f[3]} with {f[5]} moving: {s0:0.00} and {s1:0.00} of a bar at the player's rates)", s0 >= 0.93 && s0 <= 1.07 && s1 >= 0.93 && s1 <= 1.07);
			}
			RogueHoldProbe(sk);
			var a = ArmyRogueOne(sk, "levy", 0, 90, 1f);
			Check($"rogue: a levy with a full stealth bar throws a real stealth strike (Stellar Knife: strikes {a.Strikes}, Calamity-flagged shots {a.Flagged}){(a.Err != null ? " THREW " + a.Err : "")}", a.Err == null && a.Strikes >= 1 && a.Flagged >= 1);
			var c = ArmyRogueOne(dd, "levy", 3, 900);
			Check($"rogue: a Doomsday Device levy winds up, lets go on a full charge, and its stealth strikes ricochet (throws {c.Throws}, partial-charge throws {c.Partial}, strikes {c.Strikes}, strike hits {c.StrikeHits}, ricochets {c.Ricochets}, longest chain {c.MaxChain}; {c.Notes}){(c.Err != null ? " THREW " + c.Err : "")}", c.Err == null && c.Throws >= 6 && c.Partial == 0 && c.Strikes >= 3 && c.Ricochets >= 6);
		}

		/// <summary>The stealth policy's own answers for a levy holding a weapon thrown at its own rate (Stellar Knife), with its bar set: nearly full waits, half full throws, full throws (a strike), nearly full on a dying target throws.</summary>
		private static void RogueHoldProbe(int weapon)
		{
			ClearSoldiers(); ClearRogueAdds();
			Vector2 at = new Vector2(Cx * 16f + 8f, FloorY * 16f);
			SpawnOriginTarget(at); _sweepTarget.boss = true;
			Item w = new Item(); w.SetDefaults(weapon);
			int[] set = RogueSet();
			SoldierRecord rec = MakeOriginRecord(w, 0); rec.Order = SoldierOrder.Hold;
			Item h = new Item(); h.SetDefaults(set[0]); Item bo = new Item(); bo.SetDefaults(set[1]); Item l = new Item(); l.SetDefaults(set[2]);
			rec.Gear.Head = h; rec.Gear.Body = bo; rec.Gear.Legs = l; rec.Gear.SetLocked(SoldierGear.SlotHead, true); rec.Gear.SetLocked(SoldierGear.SlotBody, true); rec.Gear.SetLocked(SoldierGear.SlotLegs, true);
			RosterSystem.Instance.Regulars.Add(rec);
			int body = RosterSystem.Instance.SpawnBody(rec, at);
			try
			{
				if (body < 0) { Check("rogue policy probe: the levy spawns", false); return; }
				Main.npc[body].Bottom = at; rec.HoldPos = Main.npc[body].Center;
				SoldierNPC s = Main.npc[body].ModNPC as SoldierNPC;
				for (int k = 0; k < 20; k++) { Step(1); ClearNaturalHostiles(); }
				object cp = LevyRippers.Of(s.Dummy); float max = LevyRippers.StealthMax(cp);
				bool Ask(float frac, int life = -1)
				{
					s.StealthHoldRun = 0; s.StealthSaveRun = 0; HarnessCalamityProbe.SetStealth(s.Dummy, frac * max);
					int life0 = _sweepTarget.life; if (life > 0) _sweepTarget.life = life;
					try { return StealthPolicy.Hold(s, w, _sweepTarget); } finally { _sweepTarget.life = life0; }
				}
				bool near = Ask(0.97f), half = Ask(0.5f), full = Ask(1f), dying = Ask(0.97f, 1);
				Check($"rogue policy: a Stellar Knife levy (thrown at its own rate) waits for a nearly full bar ({near}), throws on a half bar ({!half}) and on a full one, a strike ({!full}), and does not wait to strike a target about to die ({!dying}) (bar {max:0.##})", max > 0f && near && !half && !full && !dying);
			}
			finally { ClearSoldiers(); if (_sweepTarget != null) _sweepTarget.active = false; _sweepTarget = null; }
		}

		private static void ArmyRogueSweep()
		{
			if (!HarnessCalamityProbe.Available) { Note("rogue sweep: Calamity's stealth fields are not readable"); return; }
			string only = Environment.GetEnvironmentVariable("LEVY_ARMYSWEEP_ONLY");
			string[] names = !string.IsNullOrEmpty(only) ? only.Replace('_', ' ').Split(';', StringSplitOptions.RemoveEmptyEntries) : ArmyRogueList;
			int ticks = int.TryParse(Environment.GetEnvironmentVariable("LEVY_ARMYSWEEP_TICKS"), out int tk) ? tk : 1200;
			var weapons = ArmyResolve(names).Where(x => ContentSamples.ItemsByType[x.type].DamageType?.Name?.Contains("Rogue") ?? false).ToList();
			var sw = Stopwatch.StartNew();
			SetupOriginArena(); _spNet = false;
			On_AchievementsHelper_Handle(true);
			try
			{
				KeepCommander(); Step(2);
				if (Environment.GetEnvironmentVariable("LEVY_ARMYSWEEP_ROGUE_FILL") != "0")
				{
					foreach (int acc in new[] { 0, RogueItem("EclipseMirror") })
					{
						int[] f = RogueFill(acc);
						Note($"rogue fill ({(acc > 0 ? ContentSamples.ItemsByType[acc].Name : "no accessory")}): stand-in standing {f[0]} ticks, moving {f[1]}; levy standing {f[2]} ({f[4]} moving), walking {f[3]} ({f[5]} moving)");
					}
				}
				Note("rogue sweep table (weapon | scene | actor | dps | throws | strikes | strikes/min | strike hits | ricochets | longest chain | bar full ticks | policy holds | moving ticks):");
				foreach (var (type, label, name) in weapons)
				{
					foreach (int adds in new[] { 0, 3 })
					{
						string scene = adds == 0 ? "solo" : "crowd";
						var rows = new[] { ArmyRogueOne(type, "levy", adds, ticks), ArmyRogueOne(type, "wait", adds, ticks), ArmyRogueOne(type, "spam", adds, ticks) };
						double best = Math.Max(1, Math.Max(rows[1].Dmg, rows[2].Dmg));
						Note($"rogue sweep {label} {scene}: levy / best player {rows[0].Dmg / best:0.00} (wait {rows[1].Dmg / best:0.00} spam {rows[2].Dmg / best:0.00} of best) || {rows[0]} || {rows[1]} || {rows[2]}");
						foreach (var r in rows) Note($"rogue row {label} | {scene} | {r.Who} | {r.Dps:0} | {r.Throws} | {r.Strikes} | {r.PerMin(r.Strikes):0.0} | {r.StrikeHits} | {r.Ricochets} | {r.MaxChain} | {r.BarFullTicks} | {r.Holds} | {r.Moving}{(r.Err != null ? " | THREW " + r.Err : "")}");
					}
				}
			}
			finally { _sweepTarget = null; On_AchievementsHelper_Handle(false); ClearRogueAdds(); }
			Note($"rogue sweep: {weapons.Count} weapons, {ticks} ticks a run, {sw.ElapsedMilliseconds / 1000} s");
		}

		private static void On_AchievementsHelper_Handle(bool on)
		{
			if (on) Terraria.GameContent.Achievements.On_AchievementsHelper.HandleSpecialEvent += NoAchievements;
			else Terraria.GameContent.Achievements.On_AchievementsHelper.HandleSpecialEvent -= NoAchievements;
		}
	}
}
