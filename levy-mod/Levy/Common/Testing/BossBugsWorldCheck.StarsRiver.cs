using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Globals;
using Levy.Common.Recording;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Targeting;
using Levy.Common.Weapons;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// STARS ABOVE / STARLIGHT RIVER part of the boss-bugs check (LEVY_BOSSBUGS_PART=starsriver, or any run with either mod loaded). Logged as "[BossBugs]" lines. Only code identifiers of those mods are named (no display text).
	///  * GEAR: every accessory and every armour set of the two mods is worn by a levy for a second. Nothing it does may throw through Levy's gear frame, take the levy away, move him, make his stats nonsense, or touch the commander.
	///  * TABLE: every weapon measured unusable in levy hands (ModWeaponGates.UnusableByLevies) is a real item of its mod and is never issued.
	///  The swords, the modded sentry and the bosses are checked on the army sweep's arena (IdentityWorldCheck.StarsRiver.cs).
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
			if (Part(only, "table")) { try { SrTable(); } finally { Cleanup(); } }
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

		/// <summary>An armed levy, built as <see cref="RunWeapon"/> builds one: the weapon (and ammo) are on the record, locked, before the body spawns.</summary>
		private static SoldierNPC SrArmedLevy(Vector2 at, int weaponType, int ammoType, SoldierOrder order, int hp)
		{
			var rec = new SoldierRecord
			{
				Id = RosterSystem.Instance.NextId++, Kind = SoldierKind.Regular, Grade = Grade.Levy, Temperament = Temperament.Sellsword, Male = true, Name = "Pvt. BossBugs",
				CommanderName = Commander, CommanderIndex = 0, LifeCrystals = LevyConstants.MaxLifeCrystals, ManaCrystals = 9, HomeBarracksId = -1, StoodDown = false,
			};
			rec.CurrentHp = rec.MaxHp; rec.Mana = rec.MaxMana; rec.Order = order;
			Item w = new Item(); w.SetDefaults(weaponType); rec.Gear.Weapon = w; rec.Gear.SetLocked(SoldierGear.SlotWeapon, true);
			if (ammoType > 0) { Item am = new Item(); am.SetDefaults(ammoType); am.stack = Math.Min(999, am.maxStack); rec.Gear.Ammo = am; rec.Gear.SetLocked(SoldierGear.SlotAmmo, true); }
			rec.LoadoutLocked = true;
			RosterSystem.Instance.Regulars.Add(rec);
			int body = RosterSystem.Instance.SpawnBody(rec, at);
			if (body < 0 || !rec.HasBody) return null;
			Main.npc[body].Bottom = at;
			if (hp > 0) { Main.npc[body].lifeMax = Main.npc[body].life = hp; rec.CurrentHp = hp; }
			return Main.npc[body].ModNPC as SoldierNPC;
		}

		// ---------------------------------------------------------------------------------------------------- the never-issued table
		private static void SrTable()
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
		}
	}
}
