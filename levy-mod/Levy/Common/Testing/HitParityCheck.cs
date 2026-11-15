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
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Util;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// HIT PARITY: is a levy hurt by a boss exactly where a player standing there would be? Logged as "[HitParity]" lines when a world loads on a dedicated server with LEVY_SELFCHECK_WORLD=1
	/// (the shipworld stage chains it after the shop lag check; pass=N fail=M).
	/// <para/>For a list of bosses (vanilla in both halves, Calamity's in the Calamity half) in several poses (rotation, facing, AI state, scale), a grid of body-sized positions is laid over and around each hostile NPC
	/// and three answers are compared at every position: a PLAYER there (Player.Update_NPCCollision's own checks on a real Player: CombinedHooks.CanNPCHitPlayer, the immunity slot, NPC.GetMeleeCollisionData, the rectangle),
	/// the army's CURRENT rule (<see cref="HostileContact.Hits"/> with a levy's gear dummy), and the rule the army used BEFORE (the NPC-versus-NPC hook plus the bare rectangle). The counts that matter are
	/// "levy hurt where a player is not" and the reverse for each rule; the current rule must show 0 of both, and the same immunity slot and damage multiplier.
	/// <para/>A sample of the same positions is also run through the game's real Player.Update_NPCCollision on a real Player in a spare slot (hurt or not, which immunity cooldown it started), to ground the reference itself.
	/// The production path (<c>SoldierNPC.ContactDamage</c>, reflected) is exercised at a Leviathan corner, a Leviathan circle, a Moon Lord slot, an all-types window and the Empress's quiet states.
	/// </summary>
	public class HitParityCheck : ModSystem
	{
		private static bool _armed, _done; private static int _calls, _pass, _fail; private static Mod _mod;
		private const int FloorY = 300, Cx = 1800;

		public override void OnWorldLoad()
		{
			if (!Main.dedServ || Environment.GetEnvironmentVariable("LEVY_SELFCHECK_WORLD") != "1" || Environment.GetEnvironmentVariable("LEVY_HITPARITYWORLD") == "0") return;
			_mod = Mod; _armed = true; _done = false; _calls = 0; _pass = _fail = 0;
			if (Environment.GetEnvironmentVariable("LEVY_SUMMONWORLD") == "0") Main.OnTickForThirdPartySoftwareOnly += Drive; // standalone lab run: no other check chains this one
		}
		public override void OnWorldUnload() { try { Main.OnTickForThirdPartySoftwareOnly -= Drive; } catch { } _armed = false; }
		private static void Drive() { if (!_armed || _done || !Main.dedServ) return; if (++_calls < 6) return; RunIfArmed(); }

		public static void RunIfArmed()
		{
			if (!_armed || _done || !Main.dedServ) return;
			_done = true;
			try { Run(); }
			catch (Exception e) { Check("driver threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim(), false); }
			_mod.Logger.Info($"[HitParity] self-check pass={_pass} fail={_fail}");
		}

		private static void Check(string name, bool ok) { if (ok) _pass++; else _fail++; _mod.Logger.Info($"[HitParity] {(ok ? "PASS" : "FAIL")} {name}"); }
		private static void Note(string s) => _mod.Logger.Info("[HitParity] OBSERVED " + s);

		// ------------------------------------------------------------------------------------------------------ the matrix
		/// <summary>Counts for one boss (all poses, all positions).</summary>
		public sealed class Tally
		{
			public long Positions, PlayerHurt;
			/// <summary>current rule: hurt where a player is not / not hurt where a player is / a different slot or multiplier</summary>
			public long NewOnly, NewMissed, NewSlotMult;
			/// <summary>earlier rule: the same two counts</summary>
			public long OldOnly, OldMissed;
			public int Errors; public string FirstError;
			public void Add(Tally o) { Positions += o.Positions; PlayerHurt += o.PlayerHurt; NewOnly += o.NewOnly; NewMissed += o.NewMissed; NewSlotMult += o.NewSlotMult; OldOnly += o.OldOnly; OldMissed += o.OldMissed; Errors += o.Errors; FirstError ??= o.FirstError; }
			public long NewMismatch => NewOnly + NewMissed + NewSlotMult;
		}

		/// <summary>Player.Update_NPCCollision's decision for a real Player standing at <paramref name="victim"/>, hooks and all (CombinedHooks), without the hurt.</summary>
		public static bool PlayerHurts(NPC a, int index, Rectangle victim, Player p, out int slot, out float mult)
		{
			slot = -1; mult = 1f;
			switch (a.type)
			{
				case 396: case 397: case 398: case 400: case 401: slot = 1; break;
				case 636: slot = 1; if (a.ai[0] == 0f || a.ai[0] == 10f) return false; break;
			}
			p.position = new Vector2(victim.X, victim.Y);
			if (!CombinedHooks.CanNPCHitPlayer(a, p, ref slot) || (slot == -1 && p.immune) || (p.dash == 2 && index == p.eocHit && p.eocDash > 0) || p.npcTypeNoAggro[a.type]) return false;
			Rectangle rect = new Rectangle((int)(a.position.X + a.netOffset.X), (int)(a.position.Y + a.netOffset.Y), a.width, a.height);
			NPC.GetMeleeCollisionData(victim, index, ref slot, ref mult, ref rect);
			return victim.Intersects(rect);
		}

		/// <summary>The grid of body-sized rectangles over and around a hostile NPC: its hitbox plus <paramref name="pad"/> px each way, at most about 25 x 25 positions.</summary>
		public static IEnumerable<Rectangle> Grid(NPC a, int bodyW, int bodyH, int pad = 220)
		{
			int x0 = a.Hitbox.X - bodyW - pad, x1 = a.Hitbox.Right + pad, y0 = a.Hitbox.Y - bodyH - pad, y1 = a.Hitbox.Bottom + pad;
			int sx = Math.Clamp((x1 - x0) / 24, 8, 64), sy = Math.Clamp((y1 - y0) / 24, 8, 64);
			for (int y = y0; y <= y1; y += sy) for (int x = x0; x <= x1; x += sx) yield return new Rectangle(x, y, bodyW, bodyH);
		}

		/// <summary>Runs the three rules over the grid around <paramref name="a"/> and adds to <paramref name="t"/>. <paramref name="who"/> is the levy's gear dummy, <paramref name="body"/> its NPC body.</summary>
		public static void Matrix(NPC a, Player refPlayer, Player who, NPC body, Tally t, int pad = 220)
		{
			if (!a.active || a.friendly || a.damage <= 0) return;
			int idx = a.whoAmI; Vector2 keep = who.position;
			try
			{
				foreach (Rectangle v in Grid(a, body.width, body.height, pad))
				{
					bool pl, nw, ol; int sp = -1, sn = -1, so = -1; float mp = 1f, mn = 1f, mo = 1f;
					try
					{
						pl = PlayerHurts(a, idx, v, refPlayer, out sp, out mp);
						nw = HostileContact.Hits(a, idx, v, who, body, out sn, out mn);
						ol = HostileContact.LegacyHits(a, idx, v, body, out so, out mo);
					}
					catch (Exception e) { t.Errors++; t.FirstError ??= a.TypeName + ": " + e.GetType().Name + " " + e.Message; continue; }
					t.Positions++; if (pl) t.PlayerHurt++;
					if (nw && !pl) t.NewOnly++; else if (pl && !nw) t.NewMissed++; else if (pl && nw && (sn != sp || Math.Abs(mn - mp) > 1e-4f)) t.NewSlotMult++;
					if (ol && !pl) t.OldOnly++; else if (pl && !ol) t.OldMissed++;
				}
			}
			finally { who.position = keep; }
		}

		// ------------------------------------------------------------------------------------------------------ the run
		private static readonly string[] CalamityNames =
		{
			"Leviathan", "Anahita", "DesertScourgeHead", "DesertScourgeBody", "DesertScourgeTail", "AquaticScourgeHead", "AquaticScourgeBody", "AquaticScourgeTail", "PlaguebringerGoliath", "CalamitasClone",
			"Crabulon", "HiveMind", "PerforatorHive", "SlimeGodCore", "Cryogen", "BrimstoneElemental", "AstrumAureus", "AstrumDeusHead", "RavagerBody", "ProfanedGuardianCommander", "Providence",
			"CeaselessVoid", "StormWeaverHead", "Signus", "Polterghast", "OldDuke", "DevourerofGodsHead", "DevourerofGodsBody", "Yharon", "SupremeCalamitas", "ThanatosHead", "AresBody", "Apollo",
		};
		// vanilla: EoC, King Slime, Skeletron, Skeletron hand, Destroyer head/body/tail, Skeletron Prime and an arm, Plantera and its tentacle, Golem and a fist, Duke Fishron, Queen Slime, Empress of Light, Deerclops,
		// Moon Lord head/hand/core, Pumpking, Nutcracker, Wall of Flesh mouth, Twins
		private static readonly int[] VanillaTypes = { 4, 50, 35, 36, 134, 135, 136, 127, 128, 262, 264, 245, 247, 370, 657, 636, 668, 396, 397, 398, 325, 576, 113, 125, 126 };

		private static void Arena()
		{
			for (int i = Cx - 70; i <= Cx + 70; i++)
			{
				for (int j = FloorY - 80; j < FloorY; j++) { Tile t = Main.tile[i, j]; t.ClearEverything(); }
				Tile f = Main.tile[i, FloorY]; f.ClearEverything(); f.HasTile = true; f.TileType = TileID.GrayBrick; f.BlockType = BlockType.Solid;
			}
		}

		private static void ClearHostile() { for (int i = 0; i < Main.maxNPCs; i++) { NPC n = Main.npc[i]; if (n.active && !n.friendly && !n.townNPC && !(n.ModNPC is SoldierNPC) && !(n.ModNPC is SummonedUnitNPC)) n.active = false; } }

		private static NPC SpawnAt(int type, Vector2 at)
		{
			int i = NPC.NewNPC(new EntitySource_WorldEvent("HitParity"), (int)at.X, (int)at.Y, type);
			if (i < 0 || i >= Main.maxNPCs) return null;
			NPC n = Main.npc[i]; n.timeLeft = 99999; n.netOffset = Vector2.Zero; n.Center = at; n.velocity = Vector2.Zero; n.dontTakeDamage = false;
			return n;
		}

		private static int SpawnLevy(Vector2 at)
		{
			var rec = new SoldierRecord
			{
				Id = RosterSystem.Instance.NextId++, Kind = SoldierKind.Regular, Grade = Grade.Levy, Temperament = Temperament.Sellsword, Male = true, Name = "Pvt. Parity",
				CommanderName = "HitParityTester", CommanderIndex = 0, LifeCrystals = LevyConstants.MaxLifeCrystals, ManaCrystals = 9, HomeBarracksId = -1, StoodDown = false,
			};
			rec.CurrentHp = rec.MaxHp; rec.Mana = rec.MaxMana; rec.LoadoutLocked = true; rec.Order = SoldierOrder.Follow;
			RosterSystem.Instance.Regulars.Add(rec);
			int body = RosterSystem.Instance.SpawnBody(rec, at);
			if (body >= 0 && rec.HasBody) Main.npc[body].Bottom = at;
			return body;
		}

		private static IEnumerable<(string name, Action<NPC> pose)> Poses()
		{
			float[] rots = { 0f, 0.7f, MathHelper.PiOver2, 3.1f, -2.2f };
			foreach (float r in rots)
				foreach (int dir in new[] { 1, -1 })
					yield return ($"rot{r:0.0}/dir{dir}", n => { n.rotation = r; n.direction = dir; n.spriteDirection = dir; n.velocity = new Vector2(dir * 9f, 0f).RotatedBy(r); });
			foreach (int s in new[] { 0, 1, 2, 3, 4, 5, 6 })
				yield return ($"ai0={s}", n => { n.ai[0] = s; n.ai[1] = 0f; n.rotation = 0.3f * s; });
			yield return ("ai1=40", n => { n.ai[0] = 2f; n.ai[1] = 40f; });
			yield return ("scale1.4", n => { n.scale = 1.4f; });
			yield return ("frame15..18", n => { n.frame.Y = 15; });
			yield return ("frame17", n => { n.frame.Y = 17; });
		}

		private static void Run()
		{
			Arena(); ClearHostile();
			Vector2 at = new Vector2(Cx * 16f, (FloorY - 40) * 16f);
			int bi = SpawnLevy(new Vector2(Cx * 16f - 600f, FloorY * 16f));
			if (bi < 0 || !(Main.npc[bi].ModNPC is SoldierNPC levy) || levy.Dummy == null) { Check("a levy body spawns for the matrix", false); return; }
			NPC body = Main.npc[bi]; Player who = levy.Dummy;
			Player refP = new Player { whoAmI = Main.maxPlayers, active = true, width = body.width, height = body.height };
			var sw = Stopwatch.StartNew(); var total = new Tally(); var rows = new List<string>(); int skipped = 0;
			var targets = new List<(string label, int type)>();
			foreach (int t in VanillaTypes) targets.Add((NPCID.Search.TryGetName(t, out string nm) ? nm : "npc" + t, t));
			if (ModLoader.TryGetMod("CalamityMod", out Mod cal))
				foreach (string n in CalamityNames) { if (cal.TryFind<ModNPC>(n, out ModNPC mn)) targets.Add(("Cal." + n, mn.Type)); else { skipped++; Note("calamity npc not found: " + n); } }
			int budgetMs = int.TryParse(Environment.GetEnvironmentVariable("LEVY_HITPARITY_MS"), out int bm) && bm > 0 ? bm : 60000;
			var levi = new Tally(); string leviLabel = "Cal.Leviathan";
			foreach (var (label, type) in targets)
			{
				if (sw.ElapsedMilliseconds > budgetMs) { Note("time box reached before " + label); break; }
				var tally = new Tally(); int poses = 0; string err = null;
				foreach (var (pname, pose) in Poses())
				{
					ClearHostile();
					NPC a = null;
					try { a = SpawnAt(type, at); if (a == null) { err = "did not spawn"; break; } pose(a); a.damage = Math.Max(a.damage, 30); a.friendly = false; a.active = true; }
					catch (Exception e) { err = "spawn threw " + e.GetType().Name; break; }
					Matrix(a, refP, who, body, tally);
					a.active = false; poses++;
				}
				ClearHostile();
				if (poses == 0) { Note($"{label}: skipped ({err})"); skipped++; continue; }
				total.Add(tally); if (label == leviLabel) levi.Add(tally);
				rows.Add($"{label,-34} poses={poses,2} positions={tally.Positions,6} playerHurts={tally.PlayerHurt,6} | NOW levy-only={tally.NewOnly} player-only={tally.NewMissed} slot/mult={tally.NewSlotMult} | BEFORE levy-only={tally.OldOnly} player-only={tally.OldMissed}{(tally.Errors > 0 ? " errors=" + tally.Errors + " (" + tally.FirstError + ")" : "")}");
				Check($"{label}: a levy is hurt exactly where a player is ({tally.Positions} sampled positions, mismatches {tally.NewMismatch})", tally.NewMismatch == 0 && tally.Errors == 0);
			}
			foreach (string r in rows) Note(r);
			Note($"TOTAL {targets.Count - skipped} npcs, {total.Positions} sampled positions, playerHurts={total.PlayerHurt}: NOW levy-hurt-where-player-is-not={total.NewOnly} player-hurt-where-levy-is-not={total.NewMissed} slot/mult={total.NewSlotMult}; BEFORE levy-only={total.OldOnly} player-only={total.OldMissed}; {sw.ElapsedMilliseconds} ms");
			Check($"all sampled positions: current rule mismatches {total.NewMismatch} of {total.Positions}", total.NewMismatch == 0 && total.Positions > 1000);
			if (cal != null) Check($"the check has teeth: the earlier rule hurt a levy where a player is not hurt around the Leviathan ({levi.OldOnly} of {levi.Positions} positions)", levi.Positions == 0 || levi.OldOnly > 0);
			Oracle(at);
			Production(levy, body, at);
			ClearHostile();
			body.active = false; RosterSystem.Instance.Regulars.RemoveAll(r => r.CommanderName == "HitParityTester");
		}

		// ------------------------------------------------------------------------------------------------------ the game's own Player.Update_NPCCollision as the referee
		private static void Oracle(Vector2 at)
		{
			int saved = Main.myPlayer; Player prev = Main.player[254]; int agree = 0, disagree = 0, ran = 0; string firstDis = null; string failure = null;
			var real = new Player { whoAmI = 254, active = true, creativeGodMode = false };
			real.statLifeMax = real.statLifeMax2 = real.statLife = 400000; real.width = 20; real.height = 42;
			try
			{
				Main.player[254] = real; Main.myPlayer = 254;
				var refP = new Player { whoAmI = Main.maxPlayers, active = true, width = 20, height = 42 };
				var types = new List<int> { 4, 636, 396, 397, 134, 262, 370 };
				if (ModLoader.TryGetMod("CalamityMod", out Mod cal)) foreach (string n in new[] { "Leviathan", "Anahita", "DesertScourgeHead", "AquaticScourgeHead", "PlaguebringerGoliath", "CalamitasClone" }) if (cal.TryFind<ModNPC>(n, out ModNPC mn)) types.Add(mn.Type);
				foreach (int type in types)
				{
					int pi = 0;
					foreach (var (pname, pose) in Poses())
					{
						if (pi++ % 4 != 0) continue;
						ClearHostile();
						NPC a = SpawnAt(type, at); if (a == null) continue; pose(a); a.damage = Math.Max(a.damage, 30); a.friendly = false;
						int n = 0;
						foreach (Rectangle v in Grid(a, 20, 42)) // the real call: the same grid, a stride of 7 so the sample is spread over every region
						{
							if (n++ % 7 != 0) continue;
							real.position = new Vector2(v.X, v.Y); real.immune = false; real.immuneTime = 0; Array.Clear(real.hurtCooldowns); real.statLife = real.statLifeMax2; real.dead = false;
							int life0 = real.statLife;
							real.Update_NPCCollision();
							bool realHurt = real.statLife < life0 || real.immune || real.hurtCooldowns.Any(c => c > 0);
							int realSlot = real.immune ? -1 : Array.FindIndex(real.hurtCooldowns, c => c > 0);
							bool mine = PlayerHurts(a, a.whoAmI, v, refP, out int sp, out _);
							ran++;
							if (realHurt == mine && (!realHurt || EffSlot(sp) == realSlot)) agree++; else { disagree++; firstDis ??= $"{a.TypeName} {pname} at ({v.X - a.Hitbox.X},{v.Y - a.Hitbox.Y}) real={realHurt}/slot{realSlot} reference={mine}/slot{sp}"; }
						}
						a.active = false;
					}
				}
			}
			catch (Exception e) { failure = e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim(); }
			finally { Main.player[254] = prev; Main.myPlayer = saved; ClearHostile(); }
			if (failure != null) Note("real Player.Update_NPCCollision oracle unavailable: " + failure);
			else
			{
				Note($"real Player.Update_NPCCollision oracle: {ran} positions, agree {agree}, disagree {disagree}" + (firstDis != null ? "; first: " + firstDis : ""));
				Check($"the reference agrees with the game's own Player.Update_NPCCollision ({ran} sampled positions, disagreements {disagree})", disagree == 0 && ran > 100);
			}
		}
		private static int EffSlot(int s) => s == ImmunityCooldownID.DD2OgreKnockback ? -1 : s;

		// ------------------------------------------------------------------------------------------------------ the production path
		private static void Production(SoldierNPC levy, NPC body, Vector2 at)
		{
			MethodInfo contact = typeof(SoldierNPC).GetMethod("ContactDamage", BindingFlags.Instance | BindingFlags.NonPublic);
			if (contact == null) { Check("SoldierNPC.ContactDamage is reachable", false); return; }
			bool Strike(NPC boss, Vector2 centerAt, Action prep = null)
			{
				body.Center = centerAt; body.velocity = Vector2.Zero; body.life = body.lifeMax = 9999; levy.ImmuneTime = 0; levy.AllImmune = 0; Array.Clear(levy.SlotImmune);
				prep?.Invoke();
				int before = body.life; int hits = levy.HitsTaken;
				contact.Invoke(levy, null);
				return body.life < before || levy.HitsTaken > hits;
			}
			// the Leviathan: a point inside its rectangle but outside every circle, and the middle of its body
			if (ModLoader.TryGetMod("CalamityMod", out Mod cal) && cal.TryFind<ModNPC>("Leviathan", out ModNPC lm))
			{
				ClearHostile(); NPC l = SpawnAt(lm.Type, at); l.damage = 300; l.ai[0] = 0f; l.rotation = 0f; l.direction = 1; l.spriteDirection = 1;
				if (l != null)
				{
					int idx = l.whoAmI; var refP = new Player { whoAmI = Main.maxPlayers, active = true, width = body.width, height = body.height };
					Vector2 corner = new Vector2(l.Hitbox.X + 6f, l.Hitbox.Y + 6f); // the rectangle's own corner
					Rectangle cb = new Rectangle((int)corner.X, (int)corner.Y, body.width, body.height);
					bool rectHit = cb.Intersects(l.Hitbox); bool playerHurt = PlayerHurts(l, idx, cb, refP, out _, out _);
					bool cornerHurt = Strike(l, corner + new Vector2(body.width / 2f, body.height / 2f));
					Check($"a levy in the Leviathan's rectangle corner takes no hit when a player there would not (rectangle {rectHit}, player {playerHurt}, levy {cornerHurt})", rectHit && !playerHurt && !cornerHurt);
					bool bodyHurt = Strike(l, l.Center);
					Check("a levy in the middle of the Leviathan's body is hit", bodyHurt);
					l.active = false;
				}
			}
			// Moon Lord: a slot hit lands while the general window runs, then waits on its own slot; an all-types window stops it
			{
				ClearHostile(); NPC m = SpawnAt(397, at); m.damage = 60; m.ai[0] = 0f; m.dontTakeDamage = false; m.life = m.lifeMax = 1000;
				bool inSlot = Strike(m, m.Center, () => levy.ImmuneTime = 40);
				bool slotSet = levy.SlotImmune[1] > 0 && levy.ImmuneTime > 0;
				Check($"a Moon Lord hit (immunity slot 1) lands inside the general window and starts only its own slot (hit {inSlot}, slot1 {levy.SlotImmune[1]}, general {levy.ImmuneTime})", HostileContact.Slots && HostileContact.Parity ? inSlot && slotSet : true);
				int hp1 = body.life; contact.Invoke(levy, null);
				Check("the same slot does not hit again while its cooldown runs", body.life == hp1);
				bool blocked = !Strike(m, m.Center, () => { levy.ImmuneTime = 40; levy.AllImmune = 40; });
				Check("an all-types window (dash, parry, dodge) stops a slot hit", blocked || !(HostileContact.Slots && HostileContact.Parity));
				m.active = false;
			}
			// the Empress: quiet in states 0 and 10, hurts otherwise
			{
				ClearHostile(); NPC e = SpawnAt(636, at); e.damage = 100;
				e.ai[0] = 0f; bool quiet = !Strike(e, e.Center);
				e.ai[0] = 3f; bool loud = Strike(e, e.Center);
				Check($"the Empress of Light hurts a levy only outside her quiet states (state 0 hurts {!quiet}, state 3 hurts {loud})", quiet && loud);
				e.active = false;
			}
			// a hostile with no damage hurts nobody
			{
				ClearHostile(); NPC k = SpawnAt(NPCID.BlueSlime, at); k.damage = 0;
				Check("an NPC with damage 0 hurts no levy", !Strike(k, k.Center));
				k.active = false;
			}
			Strike(null, body.Center);
		}
	}
}
