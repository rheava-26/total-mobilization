using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Globals;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Weapons;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>Counts the strikes the army sweep's target takes (every strike: projectile, melee, direct).</summary>
	public sealed class ArmySweepTally : GlobalNPC
	{
		public static bool On; public static int Target = -1; public static int Hits; public static long Damage;
		public override void HitEffect(NPC npc, NPC.HitInfo hit) { if (On && npc.whoAmI == Target) { Hits++; Damage += hit.Damage; } }
	}

	public partial class IdentityWorldCheck
	{
		// ---------------------------------------------------------------------------------------------- ARMY SWEEP: every weapon the army carries, one levy against one player
		// For each weapon one levy fires it at a boss-flagged pinned dummy 12 tiles away for LEVY_ARMYSWEEP_TICKS (default 600) ticks, and a stand-in real player (slot 255, vanilla ItemCheck, the same weapon and ammo,
		// the same dummy) does the same. The levy runs twice: with the commander idle 60 tiles away (A) and with the commander holding her own use button (channel + controlUseItem) on a held weapon (B).
		// Recorded: uses a second, projectiles made, how far from the actor and from the commander they first appear, strikes and damage on the dummy, the most minions / sentries alive. Classification:
		//   OK     the levy's damage is at least 0.6 of the player's and nothing came from the commander;   WEAK  it is below 0.6;   BROKEN  the player does damage and the levy none, a shot comes out of the commander, or it threw.
		// Env: LEVY_ARMYSWEEP=1 every listed weapon (unset: the representative subset the ship world runs, 0: skipped), LEVY_ARMYSWEEP_ONLY=Name;Name, LEVY_ARMYSWEEP_TICKS=N, LEVY_ARMYSWEEP_SHARD=i/n, LEVY_ARMYSWEEP_ALONE=1 (this check alone).
		private static readonly string[] ArmyList =
		{
			"Terra Blade", "Astral Scythe", "Ark of the Ancients", "Apathanull", "Star Shower", "Morning Star", "Influx Waver", "Wingman", "Vernal Bolter", "Dark Harvest", "Hellfire Flamberge", "Virulence", "Starspawn Helix Staff",
			"The Eye of Cthulhu", "Christmas Tree Sword", "Galvanizing Glaive", "Havoc's Breath", "Vulcanite Lance", "Malevolence", "Botanic Piercer", "Greentide", "Death Sickle", "Hellion Flower Spear", "The Microwave",
			"Nullification Pistol", "Sniper Rifle", "Pulse Turret Remote", "Rubico Prime", "Dreadmine Staff", "Desert Tiger Staff", "Doomsday Device", "Frostbrand", "Defective Sphere", "Regulus Riot", "Heavenfallen Stardisk",
			"Deadly Sphere Staff", "Brimlash", "Staff of Earth", "Fuel Cell Bundle", "Xenopopper",
			"Cosmolight", "Radiant Star", "Star of Destruction", "Shard of Antumbra", "Cranium Smasher", "Plaguenade", "The Syringe", "System Bane", "Infected Remote", "Wulfrum Controller", "Samsara Slicer", "Subduction Slicer", "Stellar Knife", "Phantasmal Ruin",
		};
		/// <summary>The sample the ship world runs (every other weapon is behind LEVY_ARMYSWEEP=1).</summary>
		private static readonly string[] ArmyShip = { "Terra Blade", "Wingman", "Vernal Bolter", "Sniper Rifle", "Stellar Knife", "Dreadmine Staff" };
		/// <summary>Weapons whose levy fix belongs to another builder: measured and listed, never failed here.</summary>
		private static readonly Dictionary<string, string> ArmyCoveredElsewhere = new() { { "starshower", "deusmind" } };

		private static string ArmyKey(string s) => new string(s.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

		private sealed class ArmyRes
		{
			public string Who = ""; public int Uses, Projs, AtCmd, NearCmdTicks, Engage, Ran, MaxMinions, MaxSentries, Hits; public long Dmg; public double SpawnActor, SpawnCmd; public string End = ""; public string Err;
			public Dictionary<string, int> Types = new(); public Dictionary<string, int> States = new(); public float MinX = 1e9f, MaxX = -1e9f;
			public double UsesPerSec => Uses * 60.0 / Math.Max(1, Ran);
			public double AvgActor => Projs == 0 ? -1 : SpawnActor / Projs / 16.0; public double AvgCmd => Projs == 0 ? -1 : SpawnCmd / Projs / 16.0;
			public bool FromCommander => AtCmd > 0 || NearCmdTicks >= 3;
			public override string ToString() => $"{Who}: uses/s {UsesPerSec:0.00}, projs {Projs}, spawn {AvgActor:0.0} tiles from the actor / {AvgCmd:0.0} from the commander, hits {Hits}, damage {Dmg}, minions {MaxMinions}, sentries {MaxSentries}, engaged {Engage}/{Ran} (states {string.Join(",", States.Select(kv => kv.Key + ":" + kv.Value))}; x {MinX:0.0}..{MaxX:0.0} tiles from start){(Err != null ? ", THREW " + Err : "")} [{string.Join(",", Types.OrderByDescending(kv => kv.Value).Take(4).Select(kv => kv.Key + "x" + kv.Value))}]";
		}

		/// <summary>Weapons by display name from every loaded mod (vanilla included).</summary>
		private static List<(int type, string label, string name)> ArmyResolve(string[] names)
		{
			var res = new List<(int, string, string)>();
			foreach (string n in names)
			{
				string k = ArmyKey(n); bool any = false;
				for (int i = 1; i < ItemLoader.ItemCount; i++)
				{
					if (!ContentSamples.ItemsByType.TryGetValue(i, out Item s) || s == null || s.IsAir || s.damage <= 0 || s.accessory || s.ammo > 0 || s.pick > 0 || s.axe > 0 || s.hammer > 0) continue;
					if (ArmyKey(s.Name) != k && !(s.ModItem != null && ArmyKey(s.ModItem.Name) == k)) continue;
					res.Add((i, (s.ModItem?.Mod.Name ?? "Terraria") + ":" + (s.ModItem?.Name ?? s.Name), s.Name)); any = true;
				}
				if (!any) Note("army sweep: no weapon named '" + n + "' in this load");
			}
			return res;
		}

		private static Vector2 ArmyPlayerAt(bool close, Vector2 at) => close ? new Vector2(_sweepTargetAt.X - 4 * 16f, FloorY * 16f) : at; // the player stands where the levy ends up for a melee weapon: next to the enemy

		private static void ArmyPlayerSetup(Player p, Item it, int ammoType, Vector2 at)
		{
			p.whoAmI = 255; p.active = true; p.dead = false; p.ghost = false; p.name = "ArmyScratch";
			p.position = at - new Vector2(p.width / 2f, p.height); p.velocity = Vector2.Zero;
			p.statLifeMax = p.statLifeMax2 = 500; p.statLife = 500; p.statManaMax = p.statManaMax2 = 200; p.statMana = 200; p.maxMinions = 10; p.maxTurrets = 3;
			p.inventory[0] = it.Clone(); p.selectedItem = 0; p.noItems = false;
			if (ammoType > 0) { Item am = new Item(); am.SetDefaults(ammoType); am.stack = Math.Min(999, am.maxStack); p.inventory[54] = am; }
		}

		/// <summary>One real-player tick of use: autoReuse and channel weapons hold the button, the rest press on alternate ticks (a release between presses, as a finger does).</summary>
		private static void ArmyPlayerUse(Player p, Item it, Vector2 at, Vector2 aim, int k)
		{
			p.position = at - new Vector2(p.width / 2f, p.height); p.velocity = Vector2.Zero; p.statMana = p.statManaMax2; p.statLife = p.statLifeMax2;
			bool press = it.autoReuse || it.channel || k % 2 == 0;
			Main.screenPosition = Vector2.Zero; Main.mouseX = (int)aim.X; Main.mouseY = (int)aim.Y;
			p.controlUseItem = press; p.controlUseTile = false; p.channel = press && it.channel; p.releaseUseItem = true;
			try { PlayerInput.Triggers.Current.MouseLeft = press; } catch { }
			Main.mouseLeft = press; Main.mouseLeftRelease = !press;
			p.direction = aim.X >= p.Center.X ? 1 : -1;
			try { object cp = global::Levy.Common.Weapons.AltPolicies.CalamityPlayerOf(p); if (cp != null) global::Levy.Common.Weapons.AltPolicies.SetCalAimPublic(cp, aim - p.MountedCenter); } catch (Exception) { }
			int saved = Main.myPlayer;
			try
			{
				Main.myPlayer = 255;
				if (_itemCheck == null)
					foreach (var m in typeof(Player).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
						if (m.Name == "ItemCheck") { var ps = m.GetParameters(); if (ps.Length == 0) { _itemCheck = m; _itemCheckTakesInt = false; break; } if (ps.Length == 1 && ps[0].ParameterType == typeof(int)) { _itemCheck = m; _itemCheckTakesInt = true; } }
				_itemCheck.Invoke(p, _itemCheckTakesInt ? new object[] { 255 } : null);
			}
			catch (Exception e) { var ie = e is TargetInvocationException && e.InnerException != null ? e.InnerException : e; if (!(ie.StackTrace ?? "").Contains("ItemCheck_PlayInstruments")) throw ie; }
			finally { Main.myPlayer = saved; }
		}

		/// <summary>Reads the damage the dummy took since the last read and tops it up (the pin is applied at the start of the NPC phase, which would otherwise erase a melee hit made before the tick).</summary>
		private static void ArmyReadDamage(ArmyRes res)
		{
			if (_sweepTarget == null || !_sweepTarget.active) return;
			int d = _sweepTarget.lifeMax - _sweepTarget.life; if (d > 0) { res.Dmg += d; _sweepTarget.life = _sweepTarget.lifeMax; }
		}

		private static ArmyRes ArmyOne(int weaponType, int ammoType, int ticks, bool player, bool cmdHolds)
		{
			var res = new ArmyRes { Who = player ? "player" : (cmdHolds ? "levy, commander holding her button" : "levy, commander idle 60 tiles away") };
			ClearSoldiers();
			_cmdAt = new Vector2((Cx - 60) * 16f, (FloorY - 3) * 16f);
			_cmdHolds = cmdHolds && !player; ReleaseCommanderButtons();
			Vector2 at = new Vector2(Cx * 16f + 8f, FloorY * 16f);
			Item w = new Item(); w.SetDefaults(weaponType);
			bool close = !w.noMelee || w.shoot <= 0; // a blade or spear is used next to the enemy (5 tiles), a shooter from 12 tiles
			SpawnOriginTarget(close ? at - new Vector2(7 * 16f, 0f) : at); _sweepTarget.boss = true;
			Player scratch = null, saved255 = null; int body = -1; SoldierRecord rec = null;
			ArmySweepTally.Target = _sweepTarget.whoAmI; ArmySweepTally.On = true;
			try
			{
				if (player)
				{
					saved255 = Main.player[255]; scratch = new Player { whoAmI = 255, name = "ArmyScratch" }; scratch.ResetEffects();
					Main.player[255] = scratch; _my = 255; if (Main.GameViewMatrix == null) Main.GameViewMatrix = new Terraria.Graphics.SpriteViewMatrix(null);
					ArmyPlayerSetup(scratch, w, ammoType, ArmyPlayerAt(close, at));
				}
				else
				{
					rec = MakeOriginRecord(w, ammoType); rec.Order = Enum.TryParse(Environment.GetEnvironmentVariable("LEVY_ARMYSWEEP_ORDER"), out SoldierOrder so) ? so : SoldierOrder.Hold; RosterSystem.Instance.Regulars.Add(rec); // Hold: a levy that stays where it is posted fights there, whatever the 60 tiles to her commander would do to a follower
					body = RosterSystem.Instance.SpawnBody(rec, at);
					if (body < 0 || !rec.HasBody) throw new Exception("levy did not spawn");
					Main.npc[body].Bottom = at; rec.HoldPos = Main.npc[body].Center;
				}
				Main.rand = new Terraria.Utilities.UnifiedRandom(777);
				KeepCommander(); PinSweepTarget(); CursorOnCommander(); Step(20);
				for (int i = 0; i < Main.maxProjectiles; i++) Main.projectile[i].active = false;
				var live = new Dictionary<int, int>(); uint prevUse = 0; int prevAnim = 0; ArmySweepTally.Hits = 0; ArmySweepTally.Damage = 0; ArmyReadDamage(new ArmyRes());
				for (int k = 0; k < ticks; k++)
				{
					Vector2 actor;
					if (player)
					{
						ArmyPlayerUse(scratch, w, ArmyPlayerAt(close, at), _sweepTarget.Center, k);
						if (scratch.itemAnimation > prevAnim) res.Uses++; prevAnim = scratch.itemAnimation;
						ArmyReadDamage(res);
						Main.screenPosition = Vector2.Zero; Main.mouseX = (int)_sweepTarget.Center.X; Main.mouseY = (int)_sweepTarget.Center.Y;
						Step(1); actor = scratch.Center;
					}
					else { rec.Mana = rec.MaxMana; OriginStep(); actor = Main.npc[body].Center; }
					ClearNaturalHostiles(); ArmyReadDamage(res);
					if (!player)
					{
						if (!Main.npc[body].active || Main.npc[body].ModNPC is not SoldierNPC soldier) { res.End = "levyGone"; break; }
						if (soldier.LastUseTick != prevUse) { if (prevUse != 0) res.Uses++; prevUse = soldier.LastUseTick; }
						if (soldier.State == SoldierState.Engage) res.Engage++;
						res.States[soldier.State.ToString()] = res.States.TryGetValue(soldier.State.ToString(), out int sc) ? sc + 1 : 1; float dx = (Main.npc[body].Center.X - at.X) / 16f; res.MinX = Math.Min(res.MinX, dx); res.MaxX = Math.Max(res.MaxX, dx);
						actor = Main.npc[body].Center;
					}
					res.Ran++;
					Vector2 cmd = Main.player[0].Center; int minions = 0, sentries = 0;
					for (int i = 0; i < Main.maxProjectiles; i++)
					{
						Projectile p = Main.projectile[i];
						if (!p.active || p.hostile) { live.Remove(i); continue; }
						if (p.minion || p.minionSlots > 0f) minions++;
						if (p.sentry) sentries++;
						float da = Vector2.Distance(p.Center, actor), dc = Vector2.Distance(p.Center, cmd);
						if (live.TryGetValue(i, out int idn) && idn != p.identity) live.Remove(i);
						if (!live.ContainsKey(i))
						{
							live[i] = p.identity; res.Projs++; res.SpawnActor += da; res.SpawnCmd += dc;
							string nm = p.ModProjectile?.Name ?? ProjectileID.Search.GetName(p.type); res.Types[nm] = res.Types.TryGetValue(nm, out int c0) ? c0 + 1 : 1;
							if (!player && dc < NearTiles * 16f && dc < da) res.AtCmd++;
						}
						if (!player && dc < NearTiles * 16f && dc < da) res.NearCmdTicks++;
					}
					res.MaxMinions = Math.Max(res.MaxMinions, minions); res.MaxSentries = Math.Max(res.MaxSentries, sentries);
				}
				res.Hits = ArmySweepTally.Hits; res.Dmg = Math.Max(res.Dmg, ArmySweepTally.Damage);
			}
			catch (Exception e) { res.Err = e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim(); }
			finally
			{
				ArmySweepTally.On = false;
				if (player) { _my = 0; if (scratch != null && ReferenceEquals(Main.player[255], scratch)) Main.player[255] = saved255; global::Levy.Common.Targeting.EnemyTargeting.ResetState(); }
				ClearSoldiers(); _cmdHolds = false; ReleaseCommanderButtons(); _cmdAt = OriginCmdAt();
			}
			return res;
		}

		private sealed class ArmyRow { public string Label, Name, Cls, Fam, Status, Cause = ""; public ArmyRes Player, A, B; public double RatioA, RatioB; }

		private static void ArmySweep()
		{
			string mode = Environment.GetEnvironmentVariable("LEVY_ARMYSWEEP");
			if (mode == "0" || !ModLoader.HasMod("CalamityMod")) return;
			string only = Environment.GetEnvironmentVariable("LEVY_ARMYSWEEP_ONLY");
			string[] names = !string.IsNullOrEmpty(only) ? only.Replace('_', ' ').Split(';', StringSplitOptions.RemoveEmptyEntries) : mode == "1" ? ArmyList : ArmyShip;
			bool reportOnly = mode == "1" && string.IsNullOrEmpty(only);
			int ticks = int.TryParse(Environment.GetEnvironmentVariable("LEVY_ARMYSWEEP_TICKS"), out int tk) ? tk : 600;
			var weapons = ArmyResolve(names);
			string shard = Environment.GetEnvironmentVariable("LEVY_ARMYSWEEP_SHARD");
			if (!string.IsNullOrEmpty(shard) && shard.Split('/') is { Length: 2 } sh && int.TryParse(sh[0], out int si) && int.TryParse(sh[1], out int sn) && sn > 1) weapons = weapons.Where((w, k) => k % sn == si).ToList();
			if (weapons.Count == 0) return;
			var ammoByKind = AmmoByKind(); var rows = new List<ArmyRow>(); var sw = Stopwatch.StartNew();
			SetupOriginArena(); _spNet = false;
			try
			{
				KeepCommander(); Step(2);
				foreach (var (type, label, name) in weapons)
				{
					Item sample = ContentSamples.ItemsByType[type];
					int ammo = sample.useAmmo > 0 && ammoByKind.TryGetValue(sample.useAmmo, out int at) ? at : 0;
					var row = new ArmyRow { Label = label, Name = name, Cls = (sample.DamageType?.Name ?? "?").Replace("DamageClass", "") };
					try { Item fi = new Item(); fi.SetDefaults(type); WeaponProfile fp = WeaponClassifier.GetProfile(fi); row.Fam = fp.Family + (fp.Supported ? "" : "(unsupported)") + (ModShotKinds.Of(fi) != ModShotKind.None ? "/" + ModShotKinds.Of(fi) : ""); } catch (Exception) { row.Fam = "?"; }
					row.Player = ArmyOne(type, ammo, ticks, true, false);
					row.A = ArmyOne(type, ammo, ticks, false, false);
					row.B = ArmyOne(type, ammo, ticks, false, true);
					ArmyClassify(row); rows.Add(row);
					Note($"army sweep {row.Status} {row.Label} [{row.Cls}, {row.Fam}] ratio idle {row.RatioA:0.00} / commander holding {row.RatioB:0.00} {row.Cause} || {row.Player} || {row.A} || {row.B}");
				}
			}
			finally { _sweepTarget = null; }
			sw.Stop();
			Note($"army sweep: {rows.Count} weapons, {ticks} ticks per run, {sw.ElapsedMilliseconds / 1000} s: OK {rows.Count(r => r.Status == "OK")}, WEAK {rows.Count(r => r.Status == "WEAK")}, BROKEN {rows.Count(r => r.Status == "BROKEN")}, NOREF {rows.Count(r => r.Status == "NOREF")}");
			Note("army sweep table (name | status | worst ratio | player dmg | levy dmg idle | levy dmg commander holding | cause):");
			foreach (var r in rows) Note($"army row {r.Label} | {r.Status} | {Math.Min(r.RatioA, r.RatioB):0.00} | {r.Player.Dmg} | {r.A.Dmg} | {r.B.Dmg} | {r.Cause}");
			foreach (var r in rows)
			{
				if (ArmyCoveredElsewhere.TryGetValue(ArmyKey(r.Name), out string cov)) { Note($"army sweep: {r.Label} is covered by {cov} ({r.Status})"); continue; }
				if (r.Status == "NOREF" || reportOnly) continue; // the full sweep reports; the ship subset (and a named re-check) fails
				Check($"army: {r.Label} works in levy hands ({r.Status}: ratio idle {r.RatioA:0.00}, commander holding {r.RatioB:0.00}; levy damage {r.A.Dmg}/{r.B.Dmg}, player {r.Player.Dmg}; from commander {r.A.FromCommander || r.B.FromCommander})", r.Status == "OK");
			}
		}

		private static void ArmyClassify(ArmyRow r)
		{
			double pd = Math.Max(1, r.Player.Dmg);
			r.RatioA = r.A.Dmg / pd; r.RatioB = r.B.Dmg / pd;
			bool pOk = r.Player.Err == null && r.Player.Dmg > 0;
			var causes = new List<string>();
			if (r.A.Err != null || r.B.Err != null) causes.Add("threw " + (r.A.Err ?? r.B.Err));
			if (r.A.FromCommander || r.B.FromCommander) causes.Add("fires from the commander");
			if (!pOk)
			{
				r.Status = causes.Count > 0 ? "BROKEN" : (r.A.Dmg > 0 || r.B.Dmg > 0 ? "OK" : "NOREF");
				causes.Add("reference player did no damage" + (r.Player.Err != null ? " (" + r.Player.Err + ")" : "")); r.Cause = string.Join("; ", causes); return;
			}
			double worst = Math.Min(r.RatioA, r.RatioB);
			if (causes.Count > 0 || r.A.Dmg == 0 || r.B.Dmg == 0) { r.Status = "BROKEN"; if (r.A.Dmg == 0) causes.Add("no damage idle"); if (r.B.Dmg == 0) causes.Add("no damage with the commander holding"); }
			else if (worst < 0.6) { r.Status = "WEAK"; causes.Add($"ratio {worst:0.00}"); }
			else r.Status = "OK";
			r.Cause = string.Join("; ", causes);
		}
	}
}
