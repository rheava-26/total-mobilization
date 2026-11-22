using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Config;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.UI;
using Levy.Common.Util;
using Levy.Common.Weapons;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// "[AllyTags]": the name tags over the army's allies (<see cref="AllyTags"/>), checked by the text each one resolves to (not pixels), in a real world (LEVY_SELFCHECK_WORLD=1, part of SummonWorldCheck's chain; LEVY_ALLYTAGS=0 skips it):
	/// a levy's minion and sentry made through the real cast tag ("Pvt. Belle's Baby Slime"), the commander's own minion (no tag), the levy himself (his nameplate, no tag), an officer's conjured creature by the officer's
	/// role ("Zoologist's Zombie", "Clothier's Skeleton"), Mechanization's turret and drone when that mod is loaded ("Turret", "Drone: Minishark"), the settings that show and hide them, and that a frame
	/// with every tag already resolved allocates nothing.
	/// </summary>
	public class AllyTagsWorldCheck : ModSystem
	{
		private static bool _armed, _done; private static int _pass, _fail; private static Mod _mod;
		private const string Commander = "TagTester";

		public override void OnWorldLoad()
		{
			if (!Main.dedServ || Environment.GetEnvironmentVariable("LEVY_SELFCHECK_WORLD") != "1" || Environment.GetEnvironmentVariable("LEVY_ALLYTAGS") == "0") return;
			_mod = Mod; _armed = true; _done = false; _pass = _fail = 0;
		}
		public override void OnWorldUnload() { _armed = false; }

		private static void Check(string name, bool ok) { if (ok) _pass++; else _fail++; _mod.Logger.Info($"[AllyTags] {(ok ? "PASS" : "FAIL")} {name}"); }
		private static void Note(string s) => _mod.Logger.Info("[AllyTags] OBSERVED " + s);

		/// <summary>Part of SummonWorldCheck's chain.</summary>
		public static void RunIfArmed()
		{
			if (!_armed || _done || !Main.dedServ) return;
			_done = true;
			var npcs = new List<NPC>(); var projs = new List<Projectile>(); var recs = new List<SoldierRecord>();
			bool sp = LevyUtil.SpEmulation; int my0 = Main.myPlayer;
			var cfg = LevyConfig.Get; bool np0 = cfg.Nameplates, at0 = cfg.AllyNameTags, forced0 = UISystem.NameplatesForced;
			try { LevyUtil.SpEmulation = true; Run(npcs, projs, recs); }
			catch (Exception e) { Check("driver threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim(), false); }
			finally
			{
				cfg.Nameplates = np0; cfg.AllyNameTags = at0; UISystem.NameplatesForced = forced0;
				SoldierMinions.PendingBody = null;
				foreach (var p in projs) p.active = false;
				foreach (var n in npcs) n.active = false;
				foreach (var r in recs) { try { if (r.HasBody) Main.npc[r.BodyIndex].active = false; RosterSystem.Instance.Regulars.Remove(r); } catch { } }
				LevyUtil.SpEmulation = sp; Main.myPlayer = my0;
			}
			_mod.Logger.Info($"[AllyTags] self-check pass={_pass} fail={_fail}");
		}

		private static SoldierNPC MakeLevy(Vector2 at, string name, List<SoldierRecord> recs)
		{
			var rec = new SoldierRecord { Id = RosterSystem.Instance.NextId++, Kind = SoldierKind.Regular, Grade = Grade.Levy, Temperament = Temperament.Sellsword, Male = false, Name = name, CommanderName = Commander, CommanderIndex = 0, HomeBarracksId = -1, StoodDown = false };
			rec.CurrentHp = rec.MaxHp; rec.LoadoutLocked = true; rec.Order = SoldierOrder.Hold;
			RosterSystem.Instance.Regulars.Add(rec); recs.Add(rec);
			int bi = RosterSystem.Instance.SpawnBody(rec, at);
			if (bi < 0 || !rec.HasBody) return null;
			Main.npc[bi].Bottom = at;
			return Main.npc[bi].ModNPC as SoldierNPC;
		}

		private static void Run(List<NPC> npcs, List<Projectile> projs, List<SoldierRecord> recs)
		{
			var cmd = Main.player[0] ?? (Main.player[0] = new Player());
			cmd.active = true; cmd.name = Commander;
			int sx = Main.spawnTileX, sy = Main.spawnTileY;
			Vector2 at = new Vector2(sx * 16f, (sy - 2) * 16f);
			cmd.Center = at + new Vector2(-48f, -24f);

			// ---- a levy's minion and sentry, made through the real cast tag (SoldierMinions.PendingBody, read by SoldierProjectileTag.OnSpawn)
			var belle = MakeLevy(at, "Pvt. Belle", recs);
			Check("a levy body stands in the world", belle != null);
			if (belle == null) return;
			Check("the levy himself carries no ally tag (he has his nameplate)", AllyTags.For(belle.NPC) == null);
			Projectile Cast(int type, int owner, SoldierNPC body)
			{
				SoldierMinions.PendingBody = body;
				int pi;
				try { pi = Projectile.NewProjectile(new EntitySource_Misc("Levy:AllyTagsCheck"), at + new Vector2(0f, -16f), Vector2.Zero, type, 10, 0f, owner); }
				finally { SoldierMinions.PendingBody = null; }
				if (pi < 0 || pi >= Main.maxProjectiles) return null;
				var p = Main.projectile[pi]; projs.Add(p); return p;
			}
			var slime = Cast(ProjectileID.BabySlime, SoldierNPC.DummySlot, belle);
			string st = slime == null ? null : AllyTags.For(slime);
			Note($"levy minion tag: \"{st}\"");
			Check($"a levy's minion is tagged with his name: \"{st}\" == \"Pvt. Belle's Baby Slime\"", st == "Pvt. Belle's Baby Slime");
			var sentry = Cast(ProjectileID.DD2BallistraTowerT1, SoldierNPC.DummySlot, belle);
			string sst = sentry == null ? null : AllyTags.For(sentry);
			Note($"levy sentry tag: \"{sst}\"");
			Check($"a levy's sentry is tagged with his name (\"{sst}\")", sst != null && sst.StartsWith("Pvt. Belle's ") && sst.Length > "Pvt. Belle's ".Length);
			var mine = Cast(ProjectileID.BabySlime, 0, null);
			Check($"the commander's own minion carries no tag (got \"{(mine == null ? "none spawned" : AllyTags.For(mine))}\")", mine != null && AllyTags.For(mine) == null);
			belle.Record.Name = "Pvt. Belle Renamed";
			Check($"a renamed levy's minion follows the new name (\"{(slime == null ? null : AllyTags.For(slime))}\")", slime != null && AllyTags.For(slime) == "Pvt. Belle Renamed's Baby Slime");
			belle.Record.Name = "Pvt. Belle";

			// ---- an officer's conjured creature, by the officer's role
			foreach (var (role, creature, roleName) in new[] { (NPCID.BestiaryGirl, NPCID.Zombie, "Zoologist"), (NPCID.Clothier, NPCID.Skeleton, "Clothier") })
			{
				var off = MakeLevy(at + new Vector2(64f, 0f), "Officer " + roleName, recs);
				if (off == null) { Check($"an officer body for the {roleName} stands in the world", false); continue; }
				off.OfficerMode = true; off.OfficerTownType = role;
				int ci = ConjuredGlobalNPC.Conjure(off.NPC, 0, creature);
				Check($"the {roleName} conjures a creature ({(ci < 0 ? ConjuredGlobalNPC.LastFail : "ok")})", ci >= 0);
				if (ci < 0) continue;
				npcs.Add(Main.npc[ci]);
				string want = roleName + "'s " + Lang.GetNPCNameValue(creature), got = AllyTags.For(Main.npc[ci]);
				Note($"conjured tag: \"{got}\"");
				Check($"a conjured creature is tagged by its officer's role: \"{got}\" == \"{want}\"", got == want);
				off.OfficerMode = false;
				Check("a creature whose officer is gone carries no tag", AllyTags.For(Main.npc[ci]) == null);
				off.OfficerMode = true;
			}

			// ---- Mechanization's machines (through IMachineNameTag; Levy does not depend on the mod)
			if (ModLoader.HasMod("Mechanization"))
			{
				foreach (var (name, word) in new[] { ("TurretNPC", "Turret"), ("DroneNPC", "Drone") })
				{
					if (!ModContent.TryFind<ModNPC>("Mechanization/" + name, out var mn)) { Check($"Mechanization has {name}", false); continue; }
					int ni = NPC.NewNPC(new EntitySource_Misc("Levy:AllyTagsCheck"), (int)at.X + 120, (int)at.Y, mn.Type);
					if (ni < 0 || ni >= Main.maxNPCs) { Check($"a {word} can be placed", false); continue; }
					NPC m = Main.npc[ni]; npcs.Add(m);
					if (word == "Drone") m.ai[3] = 1f; // bound (a drone shows only once bound)
					Check($"the {word} is seen through Levy's machine seam", m.ModNPC is IMachineNameTag && m.ModNPC is Common.Targeting.ITargetBody);
					string bare = AllyTags.For(m);
					Check($"an unarmed {word} is tagged \"{word}\" (got \"{bare}\")", bare == word);
					var f = m.ModNPC.GetType().GetField(word == "Drone" ? "WeaponType" : "_weaponType", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
					if (f == null) { Check($"the {word}'s weapon field is found (test seam)", false); continue; }
					f.SetValue(m.ModNPC, ItemID.Minishark);
					string armed = AllyTags.For(m);
					Note($"{word} tag: \"{armed}\"");
					Check($"an armed {word} is tagged with its weapon: \"{armed}\" == \"{word}: Minishark\"", armed == word + ": Minishark");
				}
			}
			else Note("Mechanization is not loaded: the machine tags are not checked");

			// ---- settings
			var cfg = LevyConfig.Get;
			cfg.Nameplates = true; cfg.AllyNameTags = true; UISystem.NameplatesForced = false;
			Check("tags show with nameplates and the ally tag setting on", AllyTags.Enabled(out _));
			cfg.AllyNameTags = false;
			Check("the ally tag setting off hides them", !AllyTags.Enabled(out _));
			UISystem.NameplatesForced = true;
			Check("the ally tag setting off hides them even with Show names", !AllyTags.Enabled(out _));
			cfg.AllyNameTags = true; cfg.Nameplates = false; UISystem.NameplatesForced = false;
			Check("nameplates off hides them", !AllyTags.Enabled(out _));
			UISystem.NameplatesForced = true;
			Check("Show names brings them back with nameplates off, and marks the frame forced", AllyTags.Enabled(out bool forced) && forced);
			cfg.Nameplates = true; UISystem.NameplatesForced = false;

			// ---- the frame: on-screen only, nothing allocated once the texts are cached
			var view = new Rectangle((int)at.X - 400, (int)at.Y - 300, 800, 600);
			int n1 = AllyTags.Collect(view);
			long a0 = GC.GetAllocatedBytesForCurrentThread();
			int n2 = AllyTags.Collect(view);
			long alloc = GC.GetAllocatedBytesForCurrentThread() - a0;
			Note($"frame: {n2} tags in view, {alloc} bytes allocated on a cached frame");
			Check($"the frame finds the tagged allies in view ({n2}, at least 2)", n2 >= 2 && n1 == n2);
			Check($"a frame with every tag cached allocates nothing ({alloc} bytes)", alloc == 0);
			var far = new Rectangle((int)at.X + 50_000, (int)at.Y, 800, 600);
			Check("allies off screen are not collected", AllyTags.Collect(far) == 0);
		}
	}
}
