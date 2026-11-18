using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Recording;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Targeting;
using Levy.Common.Weapons;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>Counts the strikes a watched NPC takes (the boss part below: the commander is idle, so every strike comes from the army).</summary>
	public sealed class StarsRiverTally : GlobalNPC
	{
		public static readonly HashSet<int> Watch = new(); public static int Hits; public static long Damage;
		public override void HitEffect(NPC npc, NPC.HitInfo hit) { if (Watch.Count > 0 && Watch.Contains(npc.whoAmI)) { Hits++; Damage += hit.Damage; } }
	}

	/// <summary>
	/// STARS ABOVE / STARLIGHT RIVER part of the boss-bugs check (LEVY_BOSSBUGS_PART=starsriver, or any run with either mod loaded). Logged as "[BossBugs]" lines. Only code identifiers of those mods are named (no display text).
	///  * GEAR: every accessory and every armour set of the two mods is worn by a levy for a second. Nothing it does may throw through Levy's gear frame, take the levy away, move him, make his stats nonsense, or touch the commander.
	///  * BOSSES: every boss type of the two mods is spawned beside an armed army and stepped for a few seconds: does it stay, do the army's shots land, does the recorder see it as a boss, does its AI throw, does it carry levies away.
	///    Spawned on a flat arena, outside whatever place its own mod builds for it, so "does not stay" is an observation, not a failure; only what Levy controls is checked.
	/// Exceptions are counted from the first-chance stream with the frames of the two mods and Levy in them, so a hook another mod swallows is still seen.
	/// </summary>
	public partial class BossBugsWorldCheck
	{
		private static readonly string[] SrMods = { "StarsAbove", "StarlightRiver" };
		private static bool SrLoaded => SrMods.Any(ModLoader.HasMod);

		// ---- first-chance exceptions with a frame of the two mods or Levy in them
		private static Dictionary<string, int> _srExc; private static bool _srExcOn;
		private static void SrExcHandler(object sender, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e)
		{
			if (!_srExcOn || _srExc == null || _srExc.Count > 200) return;
			_srExcOn = false; // the stack walk below must not recurse into this handler
			try
			{
				string st = Environment.StackTrace;
				if (!st.Contains("StarsAbove.") && !st.Contains("StarlightRiver.") && !st.Contains("Levy.")) return;
				string top = st.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => (l.Contains("StarsAbove.") || l.Contains("StarlightRiver.") || l.Contains(" Levy.")) && !l.Contains("BossBugsWorldCheck")) ?? "?";
				string k = e.Exception.GetType().Name + " " + (top.Length > 160 ? top.Substring(0, 160) : top);
				_srExc[k] = _srExc.GetValueOrDefault(k) + 1;
			}
			catch (Exception) { }
			finally { _srExcOn = true; }
		}
		private static void SrExcStart() { _srExc = new Dictionary<string, int>(); _srExcOn = true; AppDomain.CurrentDomain.FirstChanceException += SrExcHandler; }
		private static Dictionary<string, int> SrExcStop() { _srExcOn = false; AppDomain.CurrentDomain.FirstChanceException -= SrExcHandler; var r = _srExc ?? new(); _srExc = null; return r; }
		private static string SrExcText(Dictionary<string, int> d) => string.Join("; ", d.OrderByDescending(kv => kv.Value).Take(4).Select(kv => kv.Value + "x " + kv.Key));

		/// <summary>One world step that survives a throw (counted by the first-chance stream): a boss that throws every tick must not end the check.</summary>
		private static int SrStep(int n)
		{
			int thrown = 0;
			for (int i = 0; i < n; i++) { try { Step(1); } catch (Exception) { thrown++; } }
			return thrown;
		}

		private static void StarsRiverPart()
		{
			if (!SrLoaded) { Note("neither StarsAbove nor StarlightRiver is loaded: the part is skipped"); return; }
			Arena(FloorY, Half);
			string only = Environment.GetEnvironmentVariable("LEVY_SR_PART") ?? "all";
			if (Part(only, "gear")) { try { SrGear(); } finally { Cleanup(); } }
			if (Part(only, "weapons")) { try { SrWeapons(); } finally { Cleanup(); } }
			if (Part(only, "boss")) { try { SrBosses(); } finally { Cleanup(); } }
		}

		private static string SrName(int itemType) { ModItem mi = ItemLoader.GetItem(itemType); return mi == null ? "#" + itemType : mi.Mod.Name + "/" + mi.Name; }

		// ---------------------------------------------------------------------------------------------------- gear
		private sealed class SrStat { public int Def, Life, LifeMax; public float End, Move; public Vector2 At; public bool Active; }
		private static SrStat SrRead(SoldierNPC s) => new SrStat { Def = s.Defense, End = s.Endurance, Move = s.MoveSpeedMult, Life = s.NPC.life, LifeMax = s.NPC.lifeMax, At = s.NPC.Center, Active = s.NPC.active && s.NPC.ModNPC is SoldierNPC };

		private static void SrGear()
		{
			int notWorn = 0; var acc = new List<int>(); var heads = new List<int>(); var bodies = new List<int>(); var legs = new List<int>();
			for (int t = ItemID.Count; t < ItemLoader.ItemCount; t++)
			{
				if (!ContentSamples.ItemsByType.TryGetValue(t, out Item s) || s == null || s.IsAir || s.ModItem == null || !SrMods.Contains(s.ModItem.Mod.Name)) continue;
				var probe = new SoldierGear(); Item it0 = new Item(); it0.SetDefaults(t);
				if (s.accessory) { if (probe.CanHold(SoldierGear.SlotAcc0, it0) || global::Levy.Common.Movement.MobilitySlots.Is(it0) && WeaponClassifier.IsUsefulAccessory(it0)) acc.Add(t); else notWorn++; }
				else if (s.headSlot > 0 && !s.vanity) heads.Add(t); else if (s.bodySlot > 0 && !s.vanity) bodies.Add(t); else if (s.legSlot > 0 && !s.vanity) legs.Add(t);
			}
			// armour sets: the head's own IsArmorSet decides (no names guessed)
			var sets = new List<(int h, int b, int l)>();
			foreach (int h in heads)
			{
				Item hi = new Item(); hi.SetDefaults(h);
				foreach (int b in bodies) { Item bi = new Item(); bi.SetDefaults(b); foreach (int l in legs) { Item li = new Item(); li.SetDefaults(l); bool set = false; try { set = (hi.ModItem?.IsArmorSet(hi, bi, li) ?? false) || (bi.ModItem?.IsArmorSet(hi, bi, li) ?? false) || (li.ModItem?.IsArmorSet(hi, bi, li) ?? false); } catch (Exception) { } if (set) sets.Add((h, b, l)); } }
			}
			Note($"gear: {notWorn} accessories a levy never wears (vanity, information, listed harmful) left out");
			if (ModLoader.TryGetMod("StarlightRiver", out Mod slr) && slr.TryFind("BloodlessAmulet", out ModItem amulet))
			{
				Item am = new Item(); am.SetDefaults(amulet.Type);
				Check("a levy never wears an accessory that sets its wearer's life to 0 (StarlightRiver BloodlessAmulet: refused by the accessory slot)", !new SoldierGear().CanHold(SoldierGear.SlotAcc0, am) && !WeaponClassifier.IsUsefulAccessory(am));
			}
			Note($"gear: {acc.Count} accessories, {heads.Count}/{bodies.Count}/{legs.Count} head/body/leg pieces, {sets.Count} armour sets");

			PlaceCommander(-60);
			Step(2);
			Player c = Main.player[0];
			int cmdLife0 = c.statLifeMax2, cmdDef0 = c.statDefense; float cmdMove0 = c.moveSpeed; Vector2 cmdAt0 = c.Center;
			var control = Levy(new Vector2(Cx * 16f, _floorY * 16f), hp: 0);
			if (control == null) { Check("a control levy spawns", false); return; }
			control.StatsDirty = true; Step(30); var baseStat = SrRead(control); control.NPC.active = false; Step(1);

			int thrownItems = 0, gone = 0, moved = 0, nonsense = 0, cmdTouched = 0; var bad = new List<string>();
			void Wear(string label, Action<SoldierGear> dress)
			{
				var s = Levy(new Vector2(Cx * 16f, _floorY * 16f), hp: 0);
				if (s == null) { bad.Add(label + ": no levy"); return; }
				dress(s.Record.Gear); s.StatsDirty = true;
				SrExcStart(); int thrown = SrStep(60); var ex = SrExcStop();
				var st = SrRead(s);
				var why = new List<string>();
				if (ex.Count > 0 || thrown > 0) { thrownItems++; why.Add("throws " + SrExcText(ex)); }
				if (!st.Active) { gone++; why.Add("levy gone"); }
				else
				{
					if (Vector2.Distance(st.At, baseStat.At) > 20 * 16f) { moved++; why.Add($"moved {Vector2.Distance(st.At, baseStat.At) / 16f:0} tiles"); }
					if (float.IsNaN(st.End) || float.IsNaN(st.Move) || st.Def < 0 || st.Def > 1000 || st.Move > 6f || st.LifeMax != baseStat.LifeMax) { nonsense++; why.Add($"stats def {st.Def} endurance {st.End:0.00} move {st.Move:0.00} lifeMax {st.LifeMax}/{baseStat.LifeMax}"); }
				}
				if (c.statLifeMax2 != cmdLife0 || c.statDefense != cmdDef0 || Math.Abs(c.moveSpeed - cmdMove0) > 0.001f || Vector2.Distance(c.Center, cmdAt0) > 2 * 16f) { cmdTouched++; why.Add($"commander changed (life {c.statLifeMax2}/{cmdLife0}, def {c.statDefense}/{cmdDef0}, move {c.moveSpeed:0.00}/{cmdMove0:0.00})"); }
				Note($"gear {label}: def {st.Def} (base {baseStat.Def}), endurance {st.End:0.00}, move {st.Move:0.00}{(why.Count > 0 ? " :: " + string.Join("; ", why) : "")}");
				if (why.Count > 0) bad.Add(label + " (" + string.Join("; ", why) + ")");
				if (s.NPC.active) { s.Record.Gear = new SoldierGear(); s.NPC.active = false; }
				RosterSystem.Instance.Regulars.RemoveAll(r => r == s.Record);
				for (int i = 0; i < Main.maxProjectiles; i++) Main.projectile[i].active = false;
				Step(1);
			}
			foreach (int a in acc) Wear("accessory " + SrName(a), g => { Item it = new Item(); it.SetDefaults(a); g.Accessories[0] = it; });
			foreach (var (h, b, l) in sets) Wear($"set {SrName(h)}+{SrName(b)}+{SrName(l)}", g => { g.Head = new Item(); g.Head.SetDefaults(h); g.Body = new Item(); g.Body.SetDefaults(b); g.Legs = new Item(); g.Legs.SetDefaults(l); });
			Note($"gear: {acc.Count + sets.Count} worn; throws {thrownItems}, levy gone {gone}, moved {moved}, nonsense stats {nonsense}, commander changed {cmdTouched}");
			foreach (string x in bad.Take(40)) Note("gear problem: " + x);
			Check($"no StarsAbove / StarlightRiver accessory or armour set takes a levy away ({gone})", gone == 0);
			Check($"no StarsAbove / StarlightRiver accessory or armour set gives a levy nonsense stats ({nonsense})", nonsense == 0);
			Check($"no StarsAbove / StarlightRiver accessory or armour set worn by a levy changes the commander ({cmdTouched})", cmdTouched == 0);
		}

		// ---------------------------------------------------------------------------------------------------- weapons fixed for levies
		/// <summary>One levy carrying <paramref name="itemType"/>, posted on Hold, against a pinned enemy <paramref name="tiles"/> to his right: damage dealt, and how far from him his towers stand.</summary>
		private static (long dmg, int hits, float farthestSentryTiles, int sentries) SrFight(int itemType, float tiles, int ticks)
		{
			Cleanup(); Arena(FloorY, Half); PlaceCommander(-60);
			var s = Levy(new Vector2(Cx * 16f, _floorY * 16f));
			if (s == null) return (-1, 0, -1f, 0);
			Item w = new Item(); w.SetDefaults(itemType); s.Record.Gear.Weapon = w; s.Record.Gear.SetLocked(SoldierGear.SlotWeapon, true); s.Record.LoadoutLocked = true; s.StatsDirty = true;
			Vector2 at = new Vector2((Cx + tiles) * 16f, _floorY * 16f);
			NPC t = SpawnNpc(NPCID.GreenSlime, at);
			if (t == null) return (-1, 0, -1f, 0);
			t.lifeMax = t.life = 1_000_000; t.defense = 0; t.damage = 0; t.knockBackResist = 0f; t.aiStyle = -1; t.noGravity = true; t.width = 64; t.height = 96; t.Bottom = at;
			int ti = t.whoAmI; StarsRiverTally.Watch.Clear(); StarsRiverTally.Watch.Add(ti); StarsRiverTally.Hits = 0; StarsRiverTally.Damage = 0;
			float far = -1f; int sentries = 0;
			_perTick = () =>
			{
				NPC n = Main.npc[ti]; if (n.active) { n.Bottom = at; n.velocity = Vector2.Zero; n.life = n.lifeMax; n.timeLeft = 999; }
				for (int i = 0; i < Main.maxProjectiles; i++) { Projectile q = Main.projectile[i]; if (q.active && q.sentry && q.TryGetGlobalProjectile(out global::Levy.Common.Globals.SoldierProjectileTag tg) && tg.SoldierIndex == s.NPC.whoAmI) { sentries = Math.Max(sentries, 1); far = Math.Max(far, Vector2.Distance(q.Center, s.NPC.Center) / 16f); } }
			};
			SrStep(ticks); _perTick = null;
			var r = (StarsRiverTally.Damage, StarsRiverTally.Hits, far, sentries);
			StarsRiverTally.Watch.Clear();
			return r;
		}

		private static void SrWeapons()
		{
			// weapons measured unusable in levy hands: never issued (ModWeaponGates.UnusableByLevies); every row names a real item of its mod, so a renamed item is noticed
			int rows = 0, found = 0, locked = 0; var missing = new List<string>();
			foreach (var (mod, name) in ModWeaponGates.UnusableRows())
			{
				if (!ModLoader.TryGetMod(mod, out Mod m)) continue;
				rows++;
				if (!m.TryFind(name, out ModItem mi)) { missing.Add(mod + "/" + name); continue; }
				found++;
				Item it = new Item(); it.SetDefaults(mi.Type);
				if (ModWeaponGates.Locked(it)) locked++; else missing.Add(mod + "/" + name + " (not locked)");
			}
			Note($"unusable-by-levies table: {rows} rows for loaded mods, {found} found, {locked} not issued [{string.Join(", ", missing)}]");
			Check($"every weapon measured unusable in levy hands is a real item and is never issued ({locked}/{rows})", rows > 0 && locked == rows);
			// swords whose real blow is a slash circling the wielder: fought at the slash's reach (WeaponClassifier.AnchoredSwingReach), they land
			if (ModLoader.TryGetMod("StarsAbove", out Mod sa))
			{
				foreach (string n in new[] { "BloodBlade", "BurningDesire", "Umbra", "SakuraVengeance" })
				{
					if (!sa.TryFind(n, out ModItem mi)) { Note("StarsAbove/" + n + " not found"); continue; }
					Item probe = new Item(); probe.SetDefaults(mi.Type);
					var prof = WeaponClassifier.GetProfile(probe);
					var (dmg, hits, _, _) = SrFight(mi.Type, 3f, 300);
					Note($"weapon StarsAbove/{n}: family {prof.Family}, reach {prof.RangeTiles} tiles, anchored swing {WeaponClassifier.IsAnchoredSwing(probe)}; levy on Hold 3 tiles from a pinned enemy: {hits} hits, {dmg} damage in 300 ticks");
					Check($"StarsAbove/{n} is fought at its slash's reach and lands in levy hands ({prof.RangeTiles} tiles, {dmg} damage)", WeaponClassifier.IsAnchoredSwing(probe) && prof.RangeTiles <= 6 && dmg > 0);
				}
			}
			// a modded sentry that places itself at its owner's cursor on spawn goes down beside its levy, not at the commander's cursor
			if (ModLoader.TryGetMod("StarlightRiver", out Mod slr) && slr.TryFind("HeartStatueSentryItem", out ModItem hs))
			{
				int before = SoldierNPC.ModdedSentrySpawns;
				var (dmg, hits, far, sentries) = SrFight(hs.Type, 6f, 240);
				Note($"weapon StarlightRiver/HeartStatueSentryItem: towers {sentries}, farthest {far:0.0} tiles from the levy, modded-sentry spawns {SoldierNPC.ModdedSentrySpawns - before}, hits {hits}, damage {dmg}");
				Check($"a modded sentry that places itself at its owner's cursor goes down beside its levy ({far:0.0} tiles)", sentries > 0 && far >= 0f && far <= 12f);
			}
		}

		// ---------------------------------------------------------------------------------------------------- bosses
		private static void SrBosses()
		{
			var bosses = new List<int>();
			for (int t = NPCID.Count; t < NPCLoader.NPCCount; t++)
			{
				if (NPCLoader.GetNPC(t) is not ModNPC mn || !SrMods.Contains(mn.Mod.Name)) continue;
				if (ContentSamples.NpcsByNetId.TryGetValue(t, out NPC s) && s != null && s.boss) bosses.Add(t);
			}
			string onlyB = Environment.GetEnvironmentVariable("LEVY_SR_BOSS");
			if (!string.IsNullOrEmpty(onlyB)) bosses = bosses.Where(t => onlyB.Split(',').Contains((NPCLoader.GetNPC(t) as ModNPC)?.Name)).ToList();
			bool? bts = global::Levy.Common.Config.LevyConfig.Get?.BossesTargetSoldiers;
			Note($"bosses: {bosses.Count} [{string.Join(",", bosses.Select(AuditName))}]; bosses target soldiers = {bts}");
			int ticks = int.TryParse(Environment.GetEnvironmentVariable("LEVY_SR_BOSS_TICKS"), out int tk) ? tk : 720;
			int spawned = 0, hit = 0, recognised = 0, threwLevy = 0, carried = 0; long controlHits = -1;
			// the control: a vanilla boss through the same harness, so "the army never hit it" can be read as the boss's doing and not the harness's
			foreach (int type in bosses.Prepend(NPCID.EyeofCthulhu))
			{
				bool isControl = type == NPCID.EyeofCthulhu;
				string name = isControl ? "control (vanilla Eye of Cthulhu)" : AuditName(type);
				Cleanup(); Arena(FloorY, Half);
				PlaceCommander(-90);
				var levies = Levies(8, 3f);
				for (int k = 0; k < levies.Count; k++)
				{
					var l = levies[k]; l.NPC.lifeMax = l.NPC.life = 900000; l.Record.CurrentHp = 900000;
					Item w = new Item(); w.SetDefaults(k % 2 == 0 ? ItemID.Megashark : ItemID.TerraBlade); l.Record.Gear.Weapon = w; l.Record.Gear.SetLocked(SoldierGear.SlotWeapon, true);
					if (w.useAmmo > 0) { Item am = new Item(); am.SetDefaults(ItemID.ChlorophyteBullet); am.stack = 999; l.Record.Gear.Ammo = am; l.Record.Gear.SetLocked(SoldierGear.SlotAmmo, true); }
					l.Record.LoadoutLocked = true; l.StatsDirty = true;
				}
				Step(2);
				var start = levies.Select(l => l.NPC.Center).ToArray();
				SrExcStart(); NPC boss = null; int thrown = 0;
				try { boss = SpawnNpc(type, new Vector2((Cx + 25) * 16f, (_floorY - 20) * 16f)); } catch (Exception e) { thrown++; Note($"boss {name}: spawn threw {e.GetType().Name}: {e.Message}"); }
				if (boss == null || boss.type != type) { var ex0 = SrExcStop(); Note($"boss {name}: did not spawn [{SrExcText(ex0)}]"); continue; }
				if (!isControl) spawned++;
				int bossIdx = boss.whoAmI; StarsRiverTally.Watch.Clear(); StarsRiverTally.Watch.Add(bossIdx); StarsRiverTally.Hits = 0; StarsRiverTally.Damage = 0;
				bool rec = FightRecorder.IsFightBoss(boss);
				int alive = 0, locked = 0, immune = 0, jumps = 0, maxAway = 0, levyGone = 0, engaged = 0, shots = 0; var seenShots = new HashSet<int>(); var last = start.ToArray(); bool gone = false; int goneAt = -1; Vector2 bossMin = boss.Center, bossMax = boss.Center;
				_perTick = () =>
				{
					NPC b = Main.npc[bossIdx];
					if (!b.active || b.type != type) { if (!gone) goneAt = alive; gone = true; }
					else { alive++; if (Locked(b)) locked++; if (b.dontTakeDamage || b.immortal) immune++; bossMin = Vector2.Min(bossMin, b.Center); bossMax = Vector2.Max(bossMax, b.Center); b.timeLeft = Math.Max(b.timeLeft, 300); }
					for (int k = 0; k < levies.Count; k++)
					{
						NPC n = levies[k].NPC;
						if (!n.active || n.ModNPC is not SoldierNPC sn) continue;
						if (Vector2.Distance(n.Center, last[k]) > 25 * 16f) jumps++;
						maxAway = Math.Max(maxAway, (int)(Vector2.Distance(n.Center, start[k]) / 16f));
						last[k] = n.Center;
						if (sn.TargetIndex == bossIdx || (sn.TargetIndex >= 0 && sn.TargetIndex < Main.maxNPCs && Main.npc[sn.TargetIndex].realLife == bossIdx)) engaged++;
					}
					for (int i = 0; i < Main.maxProjectiles; i++) { Projectile q = Main.projectile[i]; if (q.active && q.friendly && q.TryGetGlobalProjectile(out global::Levy.Common.Globals.SoldierProjectileTag tg) && tg.SoldierIndex >= 0 && seenShots.Add(q.identity)) shots++; }
				};
				thrown += SrStep(ticks);
				_perTick = null;
				var ex = SrExcStop();
				levyGone = levies.Count(l => !l.NPC.active || l.NPC.ModNPC is not SoldierNPC);
				bool levyThrew = ex.Keys.Any(k => k.Contains(" Levy.") || k.Contains("at Levy."));
				if (isControl) controlHits = StarsRiverTally.Hits;
				else
				{
					if (StarsRiverTally.Hits > 0) hit++;
					if (rec) recognised++;
					if (levyThrew) threwLevy++;
					if (jumps > 0 || levyGone > 0) carried++;
				}
				Note($"boss {name}: alive {alive}/{ticks} ticks{(gone ? $" (gone at {goneAt})" : "")}, army shots {shots}, army hits {StarsRiverTally.Hits} for {StarsRiverTally.Damage}, levy-target ticks {engaged}, locked on a levy {locked}, untouchable {immune}, recorder sees a boss {rec}, " +
					$"boss moved x {(bossMin.X - (Cx + 25) * 16f) / 16f:0}..{(bossMax.X - (Cx + 25) * 16f) / 16f:0} y {(bossMin.Y - (_floorY - 20) * 16f) / 16f:0}..{(bossMax.Y - (_floorY - 20) * 16f) / 16f:0} tiles, levy jumps >25 tiles {jumps}, levies farthest {maxAway} tiles, levies gone {levyGone}, steps thrown {thrown}, exceptions [{SrExcText(ex)}]");
				StarsRiverTally.Watch.Clear();
				for (int i = 0; i < Main.maxNPCs; i++) { NPC n = Main.npc[i]; if (n.active && (n.ModNPC is ModNPC m && SrMods.Contains(m.Mod.Name) || n.type == NPCID.EyeofCthulhu || n.type == NPCID.ServantofCthulhu)) n.active = false; }
			}
			Note($"bosses: {spawned}/{bosses.Count} spawned, the army hit {hit}, the recorder sees {recognised} as bosses, Levy code threw on {threwLevy}, levies carried off or lost on {carried}");
			Check($"the harness control: the army hits a vanilla boss on the same arena ({controlHits} hits)", controlHits > 0);
			Check($"every StarsAbove / StarlightRiver boss that spawns is seen as a boss by the fight recorder ({recognised}/{spawned})", recognised == spawned);
			Check($"no Levy code throws while the army fights a StarsAbove / StarlightRiver boss ({threwLevy})", threwLevy == 0);
		}
	}
}
