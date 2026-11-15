using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Boss;
using Levy.Common.Compat.Redemption;
using Levy.Common.Globals;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Targeting;
using Levy.Common.Weapons;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// REDEMPTION part of the boss-bugs check (LEVY_BOSSBUGS_PART=redemption): runs only when the Redemption mod is loaded. Logged as "[BossBugs]" lines. Only code identifiers of that mod are named, never its display text.
	///  * CLASSIFIER: the eight vanilla thrown weapons the mod also made ammo stay thrown weapons; the compiled-code scan catches a block rewritten straight through <c>Tile.TileType</c>; the table knows the mod's altering items.
	///  * TARGETING: every modded type that carries UsesNewTargetting takes the flyer slot, not the native 300+ index (its AI may index Main.player[NPC.target]).
	///  * HOLD FIRE: a boss in a state where the player makes a choice is no target, takes no army shot and is not drawn off the commander; the same boss out of that state is.
	///  * WORM BOSS: a real fight with a modded worm boss that reads <c>HasValidTarget</c>, once it is beaten and bosses target soldiers: it stays in the fight and its AI reads a valid target every call.
	///  * RITUALIST: what a levy does with the custom-class weapons (alt-fire toggle, channel use, the spirit gauge on the owner).
	/// </summary>
	public partial class BossBugsWorldCheck
	{
		private static int RedNpc(Mod red, string name) => red.TryFind(name, out ModNPC n) ? n.Type : -1;
		private static string RedName(int npcType) => NPCLoader.GetNPC(npcType) is ModNPC mn ? mn.Mod.Name + "/" + mn.Name : "#" + npcType;

		private static void RedemptionPart()
		{
			if (!ModLoader.TryGetMod("Redemption", out Mod red)) { Note("Redemption is not loaded: the Redemption part is skipped"); return; }
			Arena(FloorY, Half);
			string only = Environment.GetEnvironmentVariable("LEVY_REDE_PART") ?? "all";
			if (Part(only, "classify")) { try { RedClassifier(red); } finally { Cleanup(); } }
			if (Part(only, "target")) { try { RedTargeting(red); } finally { Cleanup(); } }
			if (Part(only, "hold")) { try { RedHoldFire(red); } finally { Cleanup(); RedUnDown(red); } }
			if (Part(only, "worm")) { try { RedBossFight(red, "Gigapora", "worm boss"); } finally { Cleanup(); RedUnDown(red); } }
			if (Part(only, "ks3")) { try { RedBossFight(red, "KS3", "native-target boss", true); } finally { Cleanup(); RedUnDown(red); } }
			if (Part(only, "rit")) { try { RedRitualist(red); } finally { Cleanup(); } }
		}

		private static readonly List<int> _redDowned = new();
		private static void RedDown(int type) { BossDownedSystem.SetDownedForTest(type, true); _redDowned.Add(type); }
		private static void RedUnDown(Mod red) { foreach (int t in _redDowned) BossDownedSystem.SetDownedForTest(t, false); _redDowned.Clear(); }

		// ---------------------------------------------------------------------------------------------------- classifier
		private static void RedClassifier(Mod red)
		{
			string[] thrown = { "Grenade", "Beenade", "StickyGrenade", "BouncyGrenade", "PartyGirlGrenade", "ThrowingKnife", "PoisonedKnife", "BoneDagger" };
			int asAmmo = 0, wrong = 0; var bad = new List<string>();
			foreach (string n in thrown)
			{
				if (!ItemID.Search.TryGetId(n, out int id)) continue;
				Item it = new Item(); it.SetDefaults(id);
				if (it.ammo > 0 && it.notAmmo) asAmmo++;
				WeaponProfile pr = WeaponClassifier.GetProfile(it);
				if (pr == null || pr.Family != WeaponFamily.Thrown || WeaponClassifier.IsAmmo(it)) { wrong++; bad.Add(n + "=" + pr?.Family); }
			}
			Note($"the mod made {asAmmo} of the {thrown.Length} vanilla thrown weapons ammo (marked notAmmo); classified wrongly: {wrong} [{string.Join(",", bad)}]");
			Check($"the thrown weapons stay thrown weapons whatever another mod made them ({thrown.Length - wrong} of {thrown.Length})", wrong == 0);
			Check("the mod's ammo marker is on at least one of them (the case this part exists for)", asAmmo > 0);

			// the scan: a block type rewritten straight through the tile's type reference
			int boom = red.TryFind("PlutoniumBoom", out ModProjectile mp) ? mp.Type : -1;
			if (boom > 0)
			{
				bool scan = WorldAlter.ScanProjectileOnly(boom) != WorldAlter.Kind.None;
				Check("the compiled-code scan catches a projectile that rewrites block types through the tile's type reference", scan);
			}
			else Note("PlutoniumBoom not found: scan check skipped");
			// the scan alone over every projectile of the mod the table lists as altering (its recall), and every one it flags that the table does not (its over-flags)
			var altering = WorldAlter.TableKeys('P').Where(k => k.StartsWith("Redemption/")).ToList();
			int caught = 0, total = 0; var missed = new List<string>();
			foreach (string k in altering)
			{
				if (!red.TryFind(k.Substring("Redemption/".Length), out ModProjectile p)) continue;
				total++;
				if (WorldAlter.ScanProjectileOnly(p.Type) != WorldAlter.Kind.None) caught++; else missed.Add(k.Substring(11));
			}
			Note($"compiled-code scan alone, the mod's {total} projectiles the table lists as altering: caught {caught}; missed [{string.Join(",", missed)}]");
			int over = 0; var overList = new List<string>();
			foreach (ModProjectile p in red.GetContent<ModProjectile>())
			{
				string key = "Redemption/" + p.Name;
				if (WorldAlter.TableKeys('P').Contains(key)) continue;
				if (WorldAlter.ScanProjectileOnly(p.Type) != WorldAlter.Kind.None) { over++; if (overList.Count < 12) overList.Add(p.Name); }
			}
			Note($"projectiles of the mod the scan flags that the table does not list: {over} [{string.Join(",", overList)}]");
			// the table's items
			int alterMiss = 0;
			foreach (string k in WorldAlter.TableKeys('W').Concat(WorldAlter.TableKeys('A')).Where(k => k.StartsWith("Redemption/")))
			{
				int t = WorldAlter.ItemFromKey(k); if (t <= 0) continue;
				Item it = new Item(); it.SetDefaults(t);
				if (!WorldAlter.Alters(it)) alterMiss++;
			}
			Check("every item of the mod the table lists as altering is flagged", alterMiss == 0);
		}

		// ---------------------------------------------------------------------------------------------------- targeting
		private static void RedTargeting(Mod red)
		{
			int total = 0, wrong = 0; var rows = new List<string>();
			for (int type = NPCID.Count; type < NPCLoader.NPCCount; type++)
			{
				if (type >= NPCID.Sets.UsesNewTargetting.Length || !NPCID.Sets.UsesNewTargetting[type]) continue;
				ModNPC mn = NPCLoader.GetNPC(type); if (mn == null || mn.Mod != red) continue;
				NPC n = new NPC(); n.SetDefaults(type);
				total++;
				bool unproven = EnemyTargeting.ModdedNativeUnproven(type), native = EnemyTargeting.UsesNativeTargeting(n), flyer = EnemyTargeting.UsesFlyerSlot(n);
				if (!(unproven && !native && flyer)) wrong++;
				rows.Add($"{RedName(type)}: unproven={unproven} native={native} flyerSlot={flyer}");
			}
			foreach (string r in rows) Note("audit " + r);
			Check($"every UsesNewTargetting type of the mod takes the flyer slot, not the native 300+ index ({total} types, {wrong} wrong)", total >= 2 && wrong == 0);
			// every hostile type of the mod whose compiled AI reads target validity (HasValidTarget, HasPlayerTarget, GetTargetData): they take the flyer slot too
			var readers = new List<string>(); int hostileTypes = 0;
			for (int type = NPCID.Count; type < NPCLoader.NPCCount; type++)
			{
				ModNPC mn = NPCLoader.GetNPC(type); if (mn == null || mn.Mod != red) continue;
				NPC n = new NPC(); n.SetDefaults(type); if (n.friendly || n.townNPC) continue;
				hostileTypes++;
				if (TargetReadScan.Reads(n)) readers.Add(mn.Name);
			}
			Note($"hostile types of the mod: {hostileTypes}; {readers.Count} read target validity in their compiled AI and take the flyer slot: [{string.Join(",", readers)}]");
			Check("the rule finds some (the worm boss is one of them)", readers.Contains("Gigapora"));
			int boss = RedNpc(red, "KS3"), clone = RedNpc(red, "KS3_Clone");
			Check("the two boss types that index Main.player[NPC.target] are among them", boss > 0 && clone > 0 && EnemyTargeting.ModdedNativeUnproven(boss) && EnemyTargeting.ModdedNativeUnproven(clone));
		}

		// ---------------------------------------------------------------------------------------------------- hold fire
		private static void RedHoldFire(Mod red)
		{
			foreach (string w in RedemptionCompat.Wired) Note("wired: " + w);
			Check("the hold-fire states of both choice bosses were found", RedemptionCompat.Wired.Count >= 2 && RedemptionCompat.Wired.All(w => w.Contains("holds in states")));
			var cases = new (string name, string cls, string[] holdStates, string freeState)[]
			{
				("the death-state boss", "Erhan", new[] { "Death" }, "Fallen"),
				("the spare-countdown boss", "KS3", new[] { "SpareCountdown", "Spared" }, "GunAttacks"),
			};
			SetBossesTarget(true);
			PlaceCommander(-150);
			var levies = Levies(4, 3f);
			foreach (var (name, cls, holdStates, freeState) in cases)
			{
				int type = RedNpc(red, cls); if (type <= 0) { Check(name + " exists", false); continue; }
				RedDown(type);
				Type tp = red.Code.GetType(cls == "Erhan" ? "Redemption.NPCs.Bosses.Erhan.Erhan" : "Redemption.NPCs.Bosses.KSIII.KS3");
				Type en = tp?.GetNestedType("ActionState");
				if (en == null || !Enum.IsDefined(en, freeState)) { Check(name + ": its states can be named", false); continue; }
				// the NPC is made but its AI never runs (no world step while it stands here): this part reads the rule, not the boss's cutscene
				NPC n = new NPC(); n.SetDefaults(type); n.active = true; n.whoAmI = Main.maxNPCs - 1; n.position = new Vector2((Cx + 4) * 16f, (_floorY - 6) * 16f);
				n.lifeMax = 2000; n.life = 10; n.target = 0; n.dontTakeDamage = false; n.friendly = false; n.townNPC = false;
				foreach (string hs in holdStates)
				{
					n.ai[0] = Convert.ToInt32(Enum.Parse(en, hs));
					bool hostile = global::Levy.Common.Util.LevyUtil.IsHostileTarget(n), allow = EnemyTargeting.IsAllowlisted(n), holds = HoldFire.Holds(n);
					Check($"{name} in its {hs} state: held ({holds}), no levy target ({!hostile}), not drawn off the commander ({!allow})", holds && !hostile && !allow);
					// an army shot passes through; the commander's own does not
					var army = ArmyShot(n, true); var cmd = ArmyShot(n, false);
					bool? vArmy = NPCLoader.CanBeHitByProjectile(n, army), vCmd = NPCLoader.CanBeHitByProjectile(n, cmd);
					Check($"{name} in its {hs} state: an army shot is refused ({vArmy}), the commander's own is not ({vCmd})", vArmy == false && vCmd != false);
					army.active = false; cmd.active = false;
				}
				n.ai[0] = Convert.ToInt32(Enum.Parse(en, freeState));
				{
					bool hostile = global::Levy.Common.Util.LevyUtil.IsHostileTarget(n), holds = HoldFire.Holds(n);
					Check($"{name} in its {freeState} state (control): not held ({!holds}) and a levy target ({hostile})", !holds && hostile);
					var army = ArmyShot(n, true); bool? v = NPCLoader.CanBeHitByProjectile(n, army);
					Check($"{name} in its {freeState} state (control): an army shot is not refused ({v})", v != false);
					army.active = false;
				}
			}
		}

		/// <summary>A friendly projectile the army owns (tagged as a soldier's shot, owner a real player slot) or, when false, one the commander fired.</summary>
		private static Projectile ArmyShot(NPC at, bool army)
		{
			int keep = Main.myPlayer; Main.myPlayer = 0;
			int pi = Projectile.NewProjectile(new EntitySource_WorldEvent("BossBugsCheck"), at.Center, Vector2.Zero, ProjectileID.Bullet, 10, 0f, 0);
			Main.myPlayer = keep;
			Projectile p = Main.projectile[pi]; p.friendly = true; p.hostile = false;
			if (p.TryGetGlobalProjectile(out SoldierProjectileTag tag)) { tag.SoldierIndex = army ? 0 : -1; }
			return p;
		}

		// ---------------------------------------------------------------------------------------------------- worm boss
		private static void RedBossFight(Mod red, string cls, string label, bool deadCommander = false)
		{
			int type = RedNpc(red, cls); string L(string t) => t.Replace("worm boss", label).Replace("The worm boss", char.ToUpper(label[0]) + label.Substring(1)); void Chk(string n, bool ok) => Check(L(n), ok);
			if (type <= 0) { Check(label + " exists", false); return; }
			RedDown(type); SetBossesTarget(true);
			PlaceCommander(-120);
			var levies = Levies(8, 3f);
			foreach (var l in levies) { l.NPC.lifeMax = l.NPC.life = 900000; l.Record.CurrentHp = 900000; }
			Chk("eight levies spawn", levies.Count == 8);
			Step(2);
			var thrown = new Dictionary<string, int>(); int aiCalls = 0, lockedAi = 0, invalidWhileLocked = 0, badIndex = 0;
			On_NPC.hook_UpdateNPC det = (On_NPC.orig_UpdateNPC orig, NPC self, int i) =>
			{
				try { orig(self, i); }
				catch (Exception e) { if (_watched.Contains(self.type)) { string k = e.GetType().Name + " " + (e.StackTrace ?? "").Split('\n')[0].Trim(); thrown[k] = thrown.GetValueOrDefault(k) + 1; } throw; }
			};
			On_NPC.UpdateNPC += det;
			try
			{
				_watched.Add(type);
				EnemyTargeting.AiProbe = n =>
				{
					if (n.type != type) return;
					aiCalls++;
					if (!Locked(n)) return;
					lockedAi++;
					if (!n.HasValidTarget) invalidWhileLocked++;
					if (n.target < 0 || n.target >= Main.maxPlayers) badIndex++;
				};
				NPC boss = SpawnNpc(type, new Vector2((Cx + 40) * 16f, (_floorY - 30) * 16f));
				Chk("the worm boss spawns", boss != null && boss.type == type);
				if (boss == null) return;
				Chk("the worm boss takes the flyer slot (its AI asks whether it has a target)", EnemyTargeting.UsesFlyerSlot(boss) && !EnemyTargeting.UsesNativeTargeting(boss));
				boss.timeLeft = 300; // the despawn clock runs from here
				int alive = 0, lockedTicks = 0, minTimeLeft = int.MaxValue; bool gone = false;
				const int N = 900;
				_perTick = () =>
				{
					if (!boss.active || boss.type != type) { gone = true; return; }
					if (deadCommander) Main.player[0].dead = true; // the real player is down: this boss falls back to its own search and reads Main.player[NPC.target]
					alive++; if (Locked(boss)) lockedTicks++;
					minTimeLeft = Math.Min(minTimeLeft, boss.timeLeft);
					// stand-in for the world checks' absent players: nothing re-arms the despawn clock but the boss's own AI and vanilla's
				};
				Step(N);
				_perTick = null;
				long lost = levies.Sum(l => l.NPC.active ? (long)(l.NPC.lifeMax - l.NPC.life) : l.NPC.lifeMax); Note($"{label}: levies lost {lost} life, alive {alive} of {N} ticks (gone={gone}), locked on a levy {lockedTicks} ticks, AI calls {aiCalls}, locked {lockedAi}, locked with no valid target {invalidWhileLocked}, out-of-range target index {badIndex}, lowest timeLeft {(minTimeLeft == int.MaxValue ? -1 : minTimeLeft)}, AI errors [{string.Join("; ", thrown.Select(kv => kv.Value + "x " + kv.Key))}]");
				Chk("the worm boss's AI throws nothing against a levy-locked target", thrown.Count == 0);
				Chk("the worm boss stays in the fight (it did not despawn)", !gone && boss.active && alive >= N - 5);
				Chk("the worm boss locks onto a levy", lockedTicks >= 200);
				Chk($"its AI reads a valid target every call while locked on a levy ({invalidWhileLocked} of {lockedAi} did not)", lockedAi > 100 && invalidWhileLocked == 0);
				Chk("its despawn clock never ran down while it fought the army", minTimeLeft > 120);
				boss.active = false;
			}
			finally { On_NPC.UpdateNPC -= det; }
		}

		// ---------------------------------------------------------------------------------------------------- ritualist
		private sealed class WeaponRun { public bool Held; public string Family; public int Shots, Hits; public long Dmg; public int Leftover; public int Exceptions; public string Types = ""; public int AltToggles; }

		private static void RedRitualist(Mod red)
		{
			var cls = red.Code.GetType("Redemption.DamageClasses.RitualistClass");
			DamageClass dc = cls == null ? null : ModContent.GetContent<DamageClass>().FirstOrDefault(d => d.GetType() == cls);
			if (dc == null) { Note("the custom damage class was not found: ritualist part skipped"); return; }
			var items = new List<int>();
			for (int t = ItemID.Count; t < ItemLoader.ItemCount; t++)
			{
				if (!ContentSamples.ItemsByType.TryGetValue(t, out Item s) || s == null || s.IsAir || s.ModItem?.Mod != red) continue;
				if (s.damage > 0 && s.DamageType == dc) items.Add(t);
			}
			Note($"{items.Count} items of the custom damage class");
			Check("the custom damage class has weapons", items.Count > 0);
			int stuck = 0, held = 0, dealt = 0;
			foreach (int t in items)
			{
				Item probe = new Item(); probe.SetDefaults(t);
				var r = RunWeapon(probe, 420);
				string name = RedItem(t);
				Note($"ritual weapon {name}: family {r.Family}, held by a levy {r.Held}, shots {r.Shots}, hits {r.Hits}, damage {r.Dmg}, projectiles left after the levy is gone {r.Leftover}, throws {r.Exceptions}, shot types [{r.Types}], alt-fire {(probe.ModItem != null && probe.ModItem.AltFunctionUse(Main.player[0]) ? "yes" : "no")}");
				if (r.Held) held++;
				if (r.Dmg > 0) dealt++;
				if (r.Leftover > 0 || r.Exceptions > 0) stuck++;
			}
			Note($"ritual weapons: {held} of {items.Count} can be held by a levy, {dealt} dealt damage, {stuck} left projectiles behind or threw");
			Check("no custom-class weapon leaves a projectile stuck in the world or throws when a levy holds it", stuck == 0);
		}

		private static string RedItem(int type) { ModItem mi = ItemLoader.GetItem(type); return mi == null ? "#" + type : mi.Mod.Name + "/" + mi.Name; }

		private static WeaponRun RunWeapon(Item weapon, int ticks)
		{
			var run = new WeaponRun();
			Cleanup(); Arena(FloorY, Half); PlaceCommander(0);
			Item w = new Item(); w.SetDefaults(weapon.type);
			WeaponProfile pr = WeaponClassifier.GetProfile(w); run.Family = pr?.Family.ToString() ?? "none";
			var rec = new SoldierRecord
			{
				Id = RosterSystem.Instance.NextId++, Kind = SoldierKind.Regular, Grade = Grade.Levy, Temperament = Temperament.Sellsword, Male = true, Name = "Pvt. BossBugs",
				CommanderName = Commander, CommanderIndex = 0, LifeCrystals = LevyConstants.MaxLifeCrystals, ManaCrystals = 9, HomeBarracksId = -1, StoodDown = false,
			};
			rec.CurrentHp = rec.MaxHp; rec.Mana = rec.MaxMana;
			if (!rec.Gear.CanHold(SoldierGear.SlotWeapon, w)) { run.Held = false; return run; }
			run.Held = true;
			rec.Gear.Weapon = w; rec.Gear.SetLocked(SoldierGear.SlotWeapon, true); rec.LoadoutLocked = true; rec.Order = SoldierOrder.Follow;
			RosterSystem.Instance.Regulars.Add(rec);
			Vector2 at = new Vector2(Cx * 16f + 8f, _floorY * 16f);
			int body = RosterSystem.Instance.SpawnBody(rec, at);
			if (body < 0 || !rec.HasBody) return run;
			Main.npc[body].Bottom = at;
			var sn = Main.npc[body].ModNPC as SoldierNPC;
			// the enemy: pinned, a long way from dying
			Vector2 tat = new Vector2(Cx * 16f + 110f, _floorY * 16f);
			int ti = NPC.NewNPC(new EntitySource_WorldEvent("BossBugsCheck"), (int)tat.X, (int)tat.Y, NPCID.GreenSlime);
			NPC target = Main.npc[ti];
			target.lifeMax = 1_000_000; target.life = 1_000_000; target.defense = 0; target.damage = 0; target.knockBackResist = 0f; target.width = 64; target.height = 96;
			target.noGravity = true; target.noTileCollide = true; target.aiStyle = -1; target.friendly = false; target.value = 0f; target.Bottom = tat;
			var ht = target.GetGlobalNPC<HarnessTarget>(); ht.IsTarget = true; ht.Pin = target.position;
			var seen = new HashSet<int>(); int shots = 0; var shotTypes = new SortedSet<string>();
			On_NPC.hook_UpdateNPC det = (On_NPC.orig_UpdateNPC orig, NPC self, int i) => { try { orig(self, i); } catch (Exception) { run.Exceptions++; throw; } };
			On_NPC.UpdateNPC += det;
			try
			{
				for (int k = 0; k < ticks; k++)
				{
					KeepCommander(); _perTick?.Invoke();
					WorldStep.Update();
					for (int i = 0; i < Main.maxProjectiles; i++)
					{
						Projectile p = Main.projectile[i];
						if (p.active && p.TryGetGlobalProjectile(out SoldierProjectileTag tag) && (tag.IsSoldierShot || tag.Cast || tag.DrivenChild) && seen.Add(p.identity * 100000 + p.type + (int)(Main.GameUpdateCount % 997) * 7)) { shots++; shotTypes.Add(p.ModProjectile != null ? p.ModProjectile.Name : ProjectileID.Search.GetName(p.type)); }
					}
				}
				run.Shots = shots; run.Types = string.Join(",", shotTypes);
				run.Dmg = 1_000_000 - target.life; run.Hits = run.Dmg > 0 ? 1 : 0;
				// the levy is gone: what it threw must go with it
				RosterSystem.Instance.Regulars.RemoveAll(r => r.CommanderName == Commander);
				Main.npc[body].active = false;
				Step(4);
				for (int i = 0; i < Main.maxProjectiles; i++) { Projectile p = Main.projectile[i]; if (p.active && p.friendly && !p.hostile && p.type >= ProjectileID.Count) run.Leftover++; }
			}
			finally { On_NPC.UpdateNPC -= det; target.active = false; for (int i = 0; i < Main.maxProjectiles; i++) Main.projectile[i].active = false; }
			return run;
		}
	}
}
