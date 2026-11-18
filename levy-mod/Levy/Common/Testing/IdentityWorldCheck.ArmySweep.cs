using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.Achievements;
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
		public static Dictionary<string, (int hits, long dmg)> ByProj = new();
		public override void HitEffect(NPC npc, NPC.HitInfo hit) { if (On && npc.whoAmI == Target) { Hits++; Damage += hit.Damage; } }
		public override void OnHitByProjectile(NPC npc, Projectile projectile, NPC.HitInfo hit, int damageDone)
		{
			if (!On || npc.whoAmI != Target) return;
			string n = projectile.ModProjectile?.Name ?? ProjectileID.Search.GetName(projectile.type); ByProj.TryGetValue(n, out var e); ByProj[n] = (e.hits + 1, e.dmg + damageDone);
		}
	}

	public partial class IdentityWorldCheck
	{
		// ---------------------------------------------------------------------------------------------- ARMY SWEEP: every weapon the army carries, one levy against one player
		// For each weapon one levy fires it at a boss-flagged pinned dummy 12 tiles away for LEVY_ARMYSWEEP_TICKS (default 600) ticks, and a stand-in real player (slot 255, vanilla ItemCheck, the same weapon and ammo,
		// the same dummy) does the same. The levy runs twice: with the commander idle 60 tiles away (A) and with the commander holding her own use button (channel + controlUseItem) on a held weapon (B).
		// Recorded: uses a second, projectiles made, how far from the actor and from the commander they first appear, strikes and damage on the dummy, the most minions / sentries alive. Classification:
		//   OK     the levy's damage is at least 0.6 of the player's and nothing came from the commander;   WEAK  it is below 0.6;   BROKEN  the player does damage and the levy none, a shot comes out of the commander, or it threw.
		// Env: LEVY_ARMYSWEEP=1 every listed weapon (endgame: the post-Moon Lord tiers, see ArmyEndgame; unset: the representative subset the ship world runs, 0: skipped), LEVY_ARMYSWEEP_ONLY=Name;Name, LEVY_ARMYSWEEP_TICKS=N, LEVY_ARMYSWEEP_SHARD=i/n, LEVY_ARMYSWEEP_ALONE=1 (this check alone).
		private static readonly string[] ArmyList =
		{
			"Terra Blade", "Astral Scythe", "Ark of the Ancients", "Apathanull", "Star Shower", "Morning Star", "Influx Waver", "Wingman", "Vernal Bolter", "Dark Harvest", "Hellfire Flamberge", "Virulence", "Starspawn Helix Staff",
			"The Eye of Cthulhu", "Christmas Tree Sword", "Galvanizing Glaive", "Havoc's Breath", "Vulcanite Lance", "Malevolence", "Botanic Piercer", "Greentide", "Death Sickle", "Hellion Flower Spear", "The Microwave",
			"Nullification Pistol", "Sniper Rifle", "Pulse Turret Remote", "Rubico Prime", "Dreadmine Staff", "Desert Tiger Staff", "Doomsday Device", "Frostbrand", "Defective Sphere", "Regulus Riot", "Heavenfallen Stardisk",
			"Deadly Sphere Staff", "Brimlash", "Staff of Earth", "Fuel Cell Bundle", "Xenopopper",
			"Cosmolight", "Radiant Star", "Star of Destruction", "Shard of Antumbra", "Cranium Smasher", "Plaguenade", "The Syringe", "System Bane", "Infected Remote", "Wulfrum Controller", "Samsara Slicer", "Subduction Slicer", "Stellar Knife", "Phantasmal Ruin",
		};
		/// <summary>The sample the ship world runs (every other weapon is behind LEVY_ARMYSWEEP=1).</summary>
		private static readonly string[] ArmyShip = { "Terra Blade", "Vernal Bolter", "Sniper Rifle", "Stellar Knife", "Morning Star", "Infested Clawmerang", "Wingman", "Doomsday Device", "Nucleosynthesis" };
		/// <summary>
		/// Weapons whose own Shoot reads the player use cycle (itemAnimation, ItemUsesThisAnimation): the projectiles each use makes, in the order a player's use makes them. In levy hands each must make the
		/// same shots in the same order and about the same number as the stand-in player, and never the item's placeholder <c>shoot</c> (Nucleosynthesis's is a wooden arrow).
		/// Found by reading every Calamity weapon's Shoot / ModifyShootStats for itemAnimation, itemAnimationMax, itemTime, ItemUsesThisAnimation and useLimitPerAnimation: these two READ them (the rest only write itemTime).
		/// </summary>
		private static readonly Dictionary<string, string[]> ArmyUseCycle = new()
		{
			{ "nucleosynthesis", new[] { "SolarElementalBeam", "NebulaElementalBeam", "VortexElementalBeam", "StardustElementalBeam" } }, // itemAnimation 16, 12, 8, 4 of a 16-tick animation; any other tick: no shot
			{ "cosmickunai", new[] { "CosmicKunaiProj" } }, // four kunai a use (useLimitPerAnimation); ItemUsesThisAnimation == 4 closes a stealth salvo
		};
		/// <summary>Weapons whose levy fix belongs to another builder: measured and listed, never failed here.</summary>
		private static readonly Dictionary<string, string> ArmyCoveredElsewhere = new()
		{
			{ "starshower", "deusmind (a sky-falling weapon: the levy's shot never appears)" },
		};
		/// <summary>Weapons this headless run cannot measure for a reason of the harness, not the levy: listed, never failed.</summary>
		private static readonly Dictionary<string, string> ArmyNotMeasurable = new()
		{
			{ "pulseturretremote", "its turret loads its own texture on the firing tick, which a server with no graphics device cannot do (both the levy's and the stand-in's turret stand and never fire)" },
			{ "cosmolight", "a utility item, not a weapon" },
		};

		private static readonly bool ArmyDebug = Environment.GetEnvironmentVariable("LEVY_ARMYSWEEP_DEBUG") == "1";
		private static string ArmyKey(string s) => new string(s.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

		private sealed class ArmyRes
		{
			public string Who = ""; public int Uses, Projs, AtCmd, NearCmdTicks, MaxLinger, Engage, Ran, MaxMinions, MaxSentries, Hits; public long Dmg; public double SpawnActor, SpawnCmd; public string End = ""; public string Err;
			public List<string> Seq = new(); // the first projectiles seen, by type, in the order they appeared
			public Dictionary<string, int> Types = new(); public Dictionary<string, (int hits, long dmg)> ByProj = new(); public Dictionary<string, int> States = new(); public int DummyMinions, DummyTurrets; public float MinX = 1e9f, MaxX = -1e9f;
			public double UsesPerSec => Uses * 60.0 / Math.Max(1, Ran);
			public double AvgActor => Projs == 0 ? -1 : SpawnActor / Projs / 16.0; public double AvgCmd => Projs == 0 ? -1 : SpawnCmd / Projs / 16.0;
			public bool FromCommander => AtCmd > 0 || MaxLinger >= 12; // first seen at her, or ONE shot lingering around her (a shot that flies past her, as a piercing beam fired 45 tiles her way does, is geometry)
			public override string ToString() => $"{Who}: uses/s {UsesPerSec:0.00}, projs {Projs}, spawn {AvgActor:0.0} tiles from the actor / {AvgCmd:0.0} from the commander, hits {Hits}, damage {Dmg}, minions {MaxMinions} (slots {DummyMinions}), sentries {MaxSentries} (slots {DummyTurrets}), engaged {Engage}/{Ran} (states {string.Join(",", States.Select(kv => kv.Key + ":" + kv.Value))}; x {MinX:0.0}..{MaxX:0.0} tiles from start){(Err != null ? ", THREW " + Err : "")} [{string.Join(",", Types.OrderByDescending(kv => kv.Value).Take(4).Select(kv => kv.Key + "x" + kv.Value))}] hit by [{string.Join(",", ByProj.OrderByDescending(kv => kv.Value.dmg).Take(4).Select(kv => kv.Key + ":" + kv.Value.hits + "h/" + kv.Value.dmg))}]";
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
				if (!any)
				{
					Item util = null; for (int i = 1; i < ItemLoader.ItemCount && util == null; i++) if (ContentSamples.ItemsByType.TryGetValue(i, out Item u) && u != null && !u.IsAir && ArmyKey(u.Name) == k) util = u;
					Note("army sweep: " + (util != null ? $"'{n}' is not a weapon (damage {util.damage}, use style {util.useStyle}, channel {util.channel}: a utility item, nothing to compare)" : "no item named '" + n + "' in this load"));
				}
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
			bool grenade = it.channel && it.noUseGraphic && it.useStyle == ItemUseStyleID.Swing; // a held-then-thrown grenade (Doomsday Device) is charged with the button down and thrown on the release
			bool press = grenade ? k % 60 < 40 : (it.autoReuse || it.channel || k % 2 == 0);
			if ((it.sentry || it.buffType > 0 && it.shoot > 0 && (it.DamageType?.CountsAsClass(DamageClass.Summon) ?? false)) && p.ownedProjectileCounts.Length > 0) { int mine = 0; for (int i = 0; i < Main.maxProjectiles; i++) { Projectile q = Main.projectile[i]; if (q.active && q.owner == 255 && (it.sentry ? q.sentry : q.minion)) mine++; } if (mine > 0 && it.sentry) press = false; } // a player does not recast a sentry that is standing
			Main.screenPosition = p.Center - new Vector2(Main.screenWidth / 2f, Main.screenHeight / 2f); Main.mouseX = (int)(aim.X - Main.screenPosition.X); Main.mouseY = (int)(aim.Y - Main.screenPosition.Y);

			p.controlUseItem = press; p.controlUseTile = false; p.channel = press && it.channel; p.releaseUseItem = true;
			try { PlayerInput.Triggers.Current.MouseLeft = press; } catch { }
			Main.mouseLeft = press; Main.mouseLeftRelease = !press;
			p.direction = aim.X >= p.Center.X ? 1 : -1;
			try { object cp = global::Levy.Common.Weapons.AltPolicies.CalamityPlayerOf(p); if (cp != null) global::Levy.Common.Weapons.AltPolicies.SetCalAimPublic(cp, aim - p.MountedCenter, (aim - p.MountedCenter).ToRotation()); } catch (Exception) { }
			int saved = Main.myPlayer;
			try
			{
				Main.myPlayer = 255;
				if (_itemCheck == null)
					foreach (var m in typeof(Player).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
						if (m.Name == "ItemCheck") { var ps = m.GetParameters(); if (ps.Length == 0) { _itemCheck = m; _itemCheckTakesInt = false; break; } if (ps.Length == 1 && ps[0].ParameterType == typeof(int)) { _itemCheck = m; _itemCheckTakesInt = true; } }
				_itemCheck.Invoke(p, _itemCheckTakesInt ? new object[] { 255 } : null);
			}
			catch (Exception e) { var ie = e is TargetInvocationException && e.InnerException != null ? e.InnerException : e; if (!(ie.StackTrace ?? "").Contains("ItemCheck_PlayInstruments")) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ie).Throw(); }
			finally { Main.myPlayer = saved; }
		}

		/// <summary>Reads the damage the dummy took since the last read and tops it up (the pin is applied at the start of the NPC phase, which would otherwise erase a melee hit made before the tick).</summary>
		private static void ArmyReadDamage(ArmyRes res)
		{
			if (_sweepTarget == null || !_sweepTarget.active) return;
			int d = _sweepTarget.lifeMax - _sweepTarget.life; if (d > 0) { res.Dmg += d; _sweepTarget.life = _sweepTarget.lifeMax; }
		}

		private static ArmyRes ArmyOne(int weaponType, int ammoType, int ticks, bool player, bool cmdHolds, int minionCap = 1, int turretCap = 1)
		{
			var res = new ArmyRes { Who = player ? "player" : (cmdHolds ? "levy, commander holding her button" : "levy, commander idle 60 tiles away") };
			ClearSoldiers();
			_cmdAt = new Vector2((Cx - 60) * 16f, (FloorY - 3) * 16f);
			_cmdHolds = cmdHolds && !player; ReleaseCommanderButtons();
			Vector2 at = new Vector2(Cx * 16f + 8f, FloorY * 16f);
			Item w = new Item(); w.SetDefaults(weaponType);
			bool close = !w.noMelee || w.shoot <= 0 || ItemID.Sets.Yoyo[w.type] || ItemID.Sets.Spears[w.type]; // a blade or spear is used next to the enemy (5 tiles), a shooter from 12 tiles
			SpawnOriginTarget(close ? at - new Vector2(7 * 16f, 0f) : at); _sweepTarget.boss = true;
			Player scratch = null, saved255 = null; int body = -1; SoldierRecord rec = null;
			ArmySweepTally.Target = _sweepTarget.whoAmI; ArmySweepTally.On = true; ArmySweepTally.ByProj = new();
			try
			{
				if (player)
				{
					saved255 = Main.player[255]; scratch = new Player { whoAmI = 255, name = "ArmyScratch" }; scratch.ResetEffects();
					Main.player[255] = scratch; _my = 255; if (Main.GameViewMatrix == null) Main.GameViewMatrix = new Terraria.Graphics.SpriteViewMatrix(null);
					ArmyPlayerSetup(scratch, w, ammoType, ArmyPlayerAt(close, at)); scratch.maxMinions = Math.Max(1, minionCap); scratch.maxTurrets = Math.Max(1, turretCap);
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
				var live = new Dictionary<int, int>(); var linger = new Dictionary<int, (Vector2 at, int ticks)>(); uint prevUse = 0; int prevAnim = 0; ArmySweepTally.Hits = 0; ArmySweepTally.Damage = 0; ArmySweepTally.ByProj = new(); ArmyReadDamage(new ArmyRes());
				for (int k = 0; k < ticks; k++)
				{
					Vector2 actor;
					if (player)
					{
						ArmyPlayerUse(scratch, w, ArmyPlayerAt(close, at), _sweepTarget.Center, k);
						if (scratch.itemAnimation > prevAnim) res.Uses++; prevAnim = scratch.itemAnimation;
						scratch.numMinions = 0; scratch.slotsMinions = 0f; Array.Clear(scratch.ownedProjectileCounts, 0, scratch.ownedProjectileCounts.Length); // a player's own per-tick reset: the projectile pass below counts them again
						ArmyReadDamage(res);
						Main.screenPosition = scratch.Center - new Vector2(Main.screenWidth / 2f, Main.screenHeight / 2f); Main.mouseX = (int)(_sweepTarget.Center.X - Main.screenPosition.X); Main.mouseY = (int)(_sweepTarget.Center.Y - Main.screenPosition.Y);
						Step(1); actor = scratch.Center;
					}
					else { if (!_armyNoManaRefill || k == 0) rec.Mana = rec.MaxMana; OriginStep(); actor = Main.npc[body].Center; }
					ClearNaturalHostiles(); ArmyReadDamage(res);
					if (!player)
					{
						if (!Main.npc[body].active || Main.npc[body].ModNPC is not SoldierNPC soldier) { res.End = "levyGone"; break; }
						if (soldier.LastUseTick != prevUse) { if (prevUse != 0) res.Uses++; prevUse = soldier.LastUseTick; }
						if (soldier.State == SoldierState.Engage) res.Engage++;
						if (soldier.Dummy != null) { res.DummyMinions = soldier.Dummy.maxMinions; res.DummyTurrets = soldier.Dummy.maxTurrets; }
						res.States[soldier.State.ToString()] = res.States.TryGetValue(soldier.State.ToString(), out int sc) ? sc + 1 : 1; float dx = (Main.npc[body].Center.X - at.X) / 16f; res.MinX = Math.Min(res.MinX, dx); res.MaxX = Math.Max(res.MaxX, dx);
						actor = Main.npc[body].Center;
					}
					res.Ran++;
					Vector2 cmd = Main.player[0].Center; int minions = 0, sentries = 0;
					for (int i = 0; i < Main.maxProjectiles; i++)
					{
						Projectile p = Main.projectile[i];
						if (!p.active || p.hostile || ((p.type == ProjectileID.FallingStarSpawner || p.type == ProjectileID.FallingStar) && p.type != w.shoot)) { live.Remove(i); linger.Remove(i); continue; } // the night sky's own falling stars are not anyone's shot
						if (p.minion || p.minionSlots > 0f) minions++;
						if (p.sentry) sentries++;
						float da = Vector2.Distance(p.Center, actor), dc = Vector2.Distance(p.Center, cmd);
						if (live.TryGetValue(i, out int idn) && idn != p.identity) { live.Remove(i); linger.Remove(i); }
						if (!live.ContainsKey(i))
						{
							live[i] = p.identity; res.Projs++; res.SpawnActor += da; res.SpawnCmd += dc;
							string nm = p.ModProjectile?.Name ?? ProjectileID.Search.GetName(p.type); res.Types[nm] = res.Types.TryGetValue(nm, out int c0) ? c0 + 1 : 1; if (res.Seq.Count < 200) res.Seq.Add(nm);
							if (!player && dc < NearTiles * 16f && dc < da) res.AtCmd++;
							if (ArmyDebug && res.Projs <= 5) Note($"  armydbg first sight {(player ? "player" : "levy")} t={k} {nm} owner={p.owner} pos=({p.Center.X:0.0},{p.Center.Y:0.0}) actor=({actor.X:0.0},{actor.Y:0.0}) vel=({p.velocity.X:0.0},{p.velocity.Y:0.0}) dmg={p.damage} ai=({p.ai[0]:0.0},{p.ai[1]:0.0}) tagged={(p.TryGetGlobalProjectile(out SoldierProjectileTag tg0) && tg0.SoldierIndex >= 0)}");
						}
						if (!player && dc < NearTiles * 16f && dc < da) { res.NearCmdTicks++; if (!linger.TryGetValue(i, out var lg)) lg = (p.Center, 0); if (Vector2.Distance(p.Center, lg.at) < 48f) { lg.ticks++; res.MaxLinger = Math.Max(res.MaxLinger, lg.ticks); } linger[i] = lg; if (ArmyDebug && res.NearCmdTicks <= 6) Note($"  armydbg near commander t={k} {p.ModProjectile?.Name ?? p.type.ToString()} owner={p.owner} age={p.timeLeft} pos-levy=({(p.Center.X - actor.X) / 16f:0.0},{(p.Center.Y - actor.Y) / 16f:0.0}) pos-cmd=({(p.Center.X - cmd.X) / 16f:0.0},{(p.Center.Y - cmd.Y) / 16f:0.0}) vel=({p.velocity.X:0.0},{p.velocity.Y:0.0}) ai=({p.ai[0]:0.0},{p.ai[1]:0.0})"); }
						if (ArmyDebug && k % 100 == 50 && (p.minion || p.sentry || p.minionSlots > 0f)) Note($"  armydbg {(player ? "player" : "levy")} t={k} {p.ModProjectile?.Name ?? p.type.ToString()} owner={p.owner} dmg={p.damage} friendly={p.friendly} pos-target=({(p.Center.X - _sweepTarget.Center.X) / 16f:0.0},{(p.Center.Y - _sweepTarget.Center.Y) / 16f:0.0}) vel=({p.velocity.X:0.0},{p.velocity.Y:0.0}) ai=({p.ai[0]:0.0},{p.ai[1]:0.0}) localAI=({p.localAI[0]:0.0},{p.localAI[1]:0.0}) tl={p.timeLeft}");
					}
					res.MaxMinions = Math.Max(res.MaxMinions, minions); res.MaxSentries = Math.Max(res.MaxSentries, sentries);
				}
				res.Hits = ArmySweepTally.Hits; res.Dmg = Math.Max(res.Dmg, ArmySweepTally.Damage); res.ByProj = new Dictionary<string, (int, long)>(ArmySweepTally.ByProj);
			}
			catch (Exception e) { res.Err = e.GetType().Name + ": " + e.Message + " @ " + string.Join(" < ", (e.StackTrace ?? "").Split('\n').Take(3).Select(l => l.Trim())); }
			finally
			{
				ArmySweepTally.On = false;
				if (player) { _my = 0; if (scratch != null && ReferenceEquals(Main.player[255], scratch)) Main.player[255] = saved255; global::Levy.Common.Targeting.EnemyTargeting.ResetState(); }
				ClearSoldiers(); _cmdHolds = false; ReleaseCommanderButtons(); _cmdAt = OriginCmdAt();
			}
			return res;
		}

		private static void NoAchievements(On_AchievementsHelper.orig_HandleSpecialEvent orig, Player player, int eventID) { }

		private sealed class ArmyRow { public string Label, Name, Cls, Fam, Status, Probe = "", Cause = "", ShootName = ""; public bool Summons; public ArmyRes Player, A, B; public double RatioA, RatioB; }

		private static string ArmyProjName(int type) => type <= 0 ? "" : type >= ProjectileID.Count ? (ProjectileLoader.GetProjectile(type)?.Name ?? type.ToString()) : ProjectileID.Search.GetName(type);

		/// <summary>A summon (minion staff or sentry): its shot is the minion it keeps up, which a levy and the stand-in may each have made before the counted ticks, so the placeholder rule does not judge it.</summary>
		private static bool ArmySummons(Item s) => s.sentry || (s.DamageType?.CountsAsClass(DamageClass.Summon) ?? false) || (s.shoot > 0 && ContentSamples.ProjectilesByType.TryGetValue(s.shoot, out Projectile sp) && (sp.minion || sp.sentry || sp.minionSlots > 0f));

		/// <summary>
		/// The levy fired the item's own <c>shoot</c> while the stand-in player, holding the same weapon on the same dummy, never did: that is Levy firing the field in place of the weapon's real shot (a modded
		/// weapon's Shoot that picks the projectile itself and returns false leaves <c>shoot</c> as a placeholder; Nucleosynthesis's is a wooden arrow). Never allowed, whatever the damage says.
		/// </summary>
		private static bool ArmyPlaceholder(ArmyRow r, ArmyRes levy) =>
			!r.Summons && !string.IsNullOrEmpty(r.ShootName) && r.Player.Projs > 0 && !r.Player.Types.ContainsKey(r.ShootName) && levy.Types.TryGetValue(r.ShootName, out int n) && n > 0;

		/// <summary>The use-cycle weapons' proof: the shots of the item's own use, in the order they appeared, the first <paramref name="k"/>.</summary>
		private static List<string> ArmyUseSeq(ArmyRes res, string[] own, string shootName, int k) => res.Seq.Where(t => own.Contains(t) || t == shootName).Take(k).ToList();

		private static void ArmyUseCycleCheck(ArmyRow r, string[] given = null)
		{
			string[] own = given; if (own == null && !ArmyUseCycle.TryGetValue(ArmyKey(r.Name), out own)) return;
			const int K = 8;
			var p = ArmyUseSeq(r.Player, own, r.ShootName, K); var a = ArmyUseSeq(r.A, own, r.ShootName, K); var b = ArmyUseSeq(r.B, own, r.ShootName, K);
			int Count(ArmyRes x) => own.Sum(t => x.Types.TryGetValue(t, out int c) ? c : 0);
			int pc = Count(r.Player), ac = Count(r.A), bc = Count(r.B);
			int Ph(ArmyRes x) => !string.IsNullOrEmpty(r.ShootName) && !own.Contains(r.ShootName) && x.Types.TryGetValue(r.ShootName, out int c) ? c : 0;
			bool order = p.Count >= Math.Min(K, own.Length) && a.SequenceEqual(p) && b.SequenceEqual(p);
			bool count = pc > 0 && ac >= 0.8 * pc && bc >= 0.8 * pc;
			bool noPlaceholder = Ph(r.A) == 0 && Ph(r.B) == 0;
			Check($"army use cycle: {r.Label} makes its own shots in a player's order and number in levy hands (order player [{string.Join(",", p)}] / levy idle [{string.Join(",", a)}] / commander holding [{string.Join(",", b)}]; " +
				$"shots in {r.Player.Ran} ticks player {pc} / levy {ac} / {bc}; placeholder {r.ShootName} levy {Ph(r.A)} / {Ph(r.B)}, player {Ph(r.Player)}; damage ratio {r.RatioA:0.00} / {r.RatioB:0.00})", order && count && noPlaceholder);
		}

		/// <summary>Moon Lord's own weapon drops (vanilla's, and the one Calamity adds).</summary>
		private static readonly string[] ArmyMoonLordDrops = { "Meowmere", "Star Wrath", "Terrarian", "Celebration Mk2", "S.D.M.G.", "Last Prism", "Lunar Flare", "Lunar Portal Staff", "Rainbow Crystal Staff", "Utensil Poker" };

		/// <summary>
		/// LEVY_ARMYSWEEP=endgame: every Calamity weapon of the post-Moon Lord tiers up to the early post-Moon Lord bosses (Calamity's rarities: vanilla Purple, then Turquoise, then Pure Green), plus Moon Lord's own drops.
		/// Picked from the loaded items, so the list follows the mod's own tiers.
		/// </summary>
		private static List<(int type, string label, string name)> ArmyEndgame()
		{
			var rares = new HashSet<int> { ItemRarityID.Purple };
			foreach (string r in new[] { "Turquoise", "PureGreen" }) if (ModContent.TryFind<ModRarity>("CalamityMod", r, out ModRarity mr)) rares.Add(mr.Type);
			var res = ArmyResolve(ArmyMoonLordDrops);
			for (int i = ItemID.Count; i < ItemLoader.ItemCount; i++)
			{
				if (!ContentSamples.ItemsByType.TryGetValue(i, out Item s) || s == null || s.IsAir || s.ModItem?.Mod.Name != "CalamityMod" || !rares.Contains(s.rare)) continue;
				if (s.damage <= 0 || s.accessory || s.ammo > 0 || s.pick > 0 || s.axe > 0 || s.hammer > 0 || s.headSlot >= 0 || s.bodySlot >= 0 || s.legSlot >= 0 || s.consumable && s.shoot <= 0) continue;
				if (res.Any(w => w.type == i)) continue;
				res.Add((i, "CalamityMod:" + s.ModItem.Name, s.Name));
			}
			Note($"army sweep endgame: {res.Count} weapons (rarities {string.Join(",", rares)} and Moon Lord's drops)");
			return res;
		}

		private static void ArmySweep()
		{
			string mode = Environment.GetEnvironmentVariable("LEVY_ARMYSWEEP");
			if (mode == "0" || !ModLoader.HasMod("CalamityMod")) return;
			string only = Environment.GetEnvironmentVariable("LEVY_ARMYSWEEP_ONLY");
			string[] names = !string.IsNullOrEmpty(only) ? only.Replace('_', ' ').Split(';', StringSplitOptions.RemoveEmptyEntries) : mode == "1" ? ArmyList : ArmyShip;
			bool endgame = mode == "endgame" && string.IsNullOrEmpty(only);
			bool reportOnly = (mode == "1" || endgame) && string.IsNullOrEmpty(only);
			int ticks = int.TryParse(Environment.GetEnvironmentVariable("LEVY_ARMYSWEEP_TICKS"), out int tk) ? tk : 600;
			var weapons = endgame ? ArmyEndgame() : ArmyResolve(names);
			string shard = Environment.GetEnvironmentVariable("LEVY_ARMYSWEEP_SHARD");
			if (!string.IsNullOrEmpty(shard) && shard.Split('/') is { Length: 2 } sh && int.TryParse(sh[0], out int si) && int.TryParse(sh[1], out int sn) && sn > 1) weapons = weapons.Where((w, k) => k % sn == si).ToList();
			if (weapons.Count == 0) return;
			var ammoByKind = AmmoByKind(); var rows = new List<ArmyRow>(); var sw = Stopwatch.StartNew();
			SetupOriginArena(); _spNet = false;
			On_AchievementsHelper.HandleSpecialEvent += NoAchievements; // a yoyo throw tells the achievement system, which a headless server has not built
			try
			{
				KeepCommander(); Step(2);
				foreach (var (type, label, name) in weapons)
				{
					Item sample = ContentSamples.ItemsByType[type];
					int ammo = sample.useAmmo > 0 && ammoByKind.TryGetValue(sample.useAmmo, out int at) ? at : 0;
					var row = new ArmyRow { Label = label, Name = name, Cls = (sample.DamageType?.Name ?? "?").Replace("DamageClass", ""), ShootName = ArmyProjName(sample.shoot), Summons = ArmySummons(sample) };
					try { row.Probe = sample.shoot > 0 ? $"shot reads cursor {OwnerLockProbe.ReadsCursor(sample.shoot)}, reads buttons {OwnerLockProbe.ReadsUseButtons(sample.shoot)}, locks owner {OwnerLockProbe.LocksOwner(sample.shoot)}" : "no shot"; } catch (Exception) { row.Probe = "probe threw"; }
					try { Item fi = new Item(); fi.SetDefaults(type); WeaponProfile fp = WeaponClassifier.GetProfile(fi); row.Fam = fp.Family + (fp.Supported ? "" : "(unsupported)") + (ModShotKinds.Of(fi) != ModShotKind.None ? "/" + ModShotKinds.Of(fi) : ""); } catch (Exception) { row.Fam = "?"; }
					row.A = ArmyOne(type, ammo, ticks, false, false);
					row.B = ArmyOne(type, ammo, ticks, false, true);
					row.Player = ArmyOne(type, ammo, ticks, true, false, Math.Max(row.A.DummyMinions, row.B.DummyMinions), Math.Max(row.A.DummyTurrets, row.B.DummyTurrets)); // the stand-in gets the minion and sentry slots the levy's own player has
					ArmyClassify(row); rows.Add(row);
					Note($"army sweep {row.Status} {row.Label} [{row.Cls}, {row.Fam}; {row.Probe}] ratio idle {row.RatioA:0.00} / commander holding {row.RatioB:0.00} {row.Cause} || {row.Player} || {row.A} || {row.B}");
					Note($"army seq {row.Label}: player [{string.Join(",", row.Player.Seq.Take(12))}] || levy idle [{string.Join(",", row.A.Seq.Take(12))}] || commander holding [{string.Join(",", row.B.Seq.Take(12))}]");
				}
			}
			finally { _sweepTarget = null; On_AchievementsHelper.HandleSpecialEvent -= NoAchievements; }
			sw.Stop();
			Note($"army sweep: {rows.Count} weapons, {ticks} ticks per run, {sw.ElapsedMilliseconds / 1000} s: OK {rows.Count(r => r.Status == "OK")}, WEAK {rows.Count(r => r.Status == "WEAK")}, BROKEN {rows.Count(r => r.Status == "BROKEN")}, NOREF {rows.Count(r => r.Status == "NOREF")}");
			Note("army sweep table (name | status | worst ratio | player dmg | levy dmg idle | levy dmg commander holding | cause):");
			foreach (var r in rows) Note($"army row {r.Label} | {r.Status} | {Math.Min(r.RatioA, r.RatioB):0.00} | {r.Player.Dmg} | {r.A.Dmg} | {r.B.Dmg} | {r.Cause}{(ArmyCoveredElsewhere.TryGetValue(ArmyKey(r.Name), out string c1) ? " | COVERED BY " + c1 : ArmyNotMeasurable.TryGetValue(ArmyKey(r.Name), out string c2) ? " | NOT MEASURABLE: " + c2 : "")}");
			foreach (var r in rows)
			{
				if (ArmyCoveredElsewhere.TryGetValue(ArmyKey(r.Name), out string cov)) { Note($"army sweep: {r.Label} is covered by {cov} ({r.Status}, ratio {Math.Min(r.RatioA, r.RatioB):0.00})"); continue; }
				if (ArmyNotMeasurable.TryGetValue(ArmyKey(r.Name), out string nm2)) { Note($"army sweep: {r.Label} is not measurable here: {nm2}"); continue; }
				if (r.Status == "NOREF" || reportOnly) continue; // the full sweep reports; the ship subset (and a named re-check) fails
				ArmyUseCycleCheck(r);
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
			if (ArmyPlaceholder(r, r.A) || ArmyPlaceholder(r, r.B)) causes.Add($"fires the item's placeholder shot {r.ShootName}, which the player never fires");
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
