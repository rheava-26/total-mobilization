using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Globals;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Weapons;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// LIFESTEAL POOL SCENARIO (LEVY_HARNESS_SCENARIO=lifesteal). Measurement only. For each army size N (LEVY_LS_COUNTS, default "1,4,10"): N levies, each with its own pinned target, the stand-in commander's
	/// lifesteal pool set to a known value, and Main.myPlayer = 0 for the whole run (single player: the commander is the local player, as Projectile.vampireHeal and Calamity's lifesteal expect).
	///   direct mode (default): every LEVY_LS_PERIOD ticks (10) each levy's shot hits its target and the hit calls vanilla's own Projectile.vampireHeal(LEVY_LS_DMG, ...) inside the puppeted pass, as vanilla's
	///     lifesteal weapons do. Each levy's life is pinned to 40% every tick so no heal is wasted at full health.
	///   item mode (LEVY_LS_ITEM=Mod/Name): every levy holds that real weapon and fights its target (Thorium / Calamity lifesteal weapons).
	/// Reported per N: the pool spent (sum over every vampireHeal / ghostHeal call), the life routed to levies (OwnerPuppet.RoutedLife), calls, the levies' pools at the end, and the COMMANDER's pool: how many
	/// ticks it fell and by how much, and where it ended. LEVY_LIFESTEAL_SHARED=1 switches the per-levy pool off (the 1.7.4 behaviour: one pool, the commander's) for the "before" run.
	/// </summary>
	public partial class CombatHarness
	{
		internal static bool LsDirect; internal static int LsHitDamage = 200;
		private int[] _lsCounts; private int _lsCase = -1, _lsTick, _lsPeriod = 10, _lsTicks = 900; private string _lsItem = "";
		private readonly List<SoldierNPC> _lsBodies = new(); private readonly List<int> _lsTargets = new();
		internal static int LsCalls, LsHeals, LsSpentMilli;
		private int _lsRoutedStart; private int _lsCmdFalls; private float _lsCmdFallSum, _lsCmdPrev, _lsCmdMin, _lsCmdMax;
		private int _lsHealStart, _lsOpensStart, _lsShotTicks, _lsUnpOwner, _lsUnpBody; private string _lsWpn = ""; private bool _lsHooked;

		private bool BeginLifeSteal()
		{
			string c = Environment.GetEnvironmentVariable("LEVY_LS_COUNTS") ?? "1,4,10";
			var list = new List<int>();
			foreach (var s in c.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) if (int.TryParse(s, out int n) && n >= 1 && n <= 60) list.Add(n);
			_lsCounts = list.ToArray();
			if (int.TryParse(Environment.GetEnvironmentVariable("LEVY_LS_TICKS"), out int t) && t >= 60) _lsTicks = t;
			if (int.TryParse(Environment.GetEnvironmentVariable("LEVY_LS_PERIOD"), out int p) && p >= 1) _lsPeriod = p;
			if (int.TryParse(Environment.GetEnvironmentVariable("LEVY_LS_DMG"), out int d) && d >= 20) LsHitDamage = d;
			_lsItem = Environment.GetEnvironmentVariable("LEVY_LS_ITEM") ?? "";
			LsDirect = _lsItem == "";
			_lsCase = 0; _lsTick = -1; _lsSavedMy = int.MinValue;
			if (!_lsHooked) { On_Projectile.vampireHeal += LsVampireDetour; On_Projectile.ghostHeal += LsGhostDetour; _lsHooked = true; }
			Mod.Logger.Info($"HARNESS-LS: lifesteal scenario counts=[{string.Join(",", _lsCounts)}] ticks={_lsTicks} mode={(LsDirect ? "direct vampireHeal dmg" + LsHitDamage + " every " + _lsPeriod : "item " + _lsItem)} shared={LifeStealPool.SharedForTest} expert={Main.expertMode}");
			return _lsCounts.Length > 0;
		}

		private static void LsVampireDetour(On_Projectile.orig_vampireHeal orig, Projectile self, int dmg, Vector2 pos, Entity victim)
		{
			LsCalls++;
			float before = Main.player[Main.myPlayer].lifeSteal;
			orig(self, dmg, pos, victim);
			float spent = before - Main.player[Main.myPlayer].lifeSteal;
			if (spent > 0.001f) { LsHeals++; LsSpentMilli += (int)(spent * 1000f); }
		}

		private static void LsGhostDetour(On_Projectile.orig_ghostHeal orig, Projectile self, int dmg, Vector2 pos, Entity victim)
		{
			LsCalls++;
			float before = Main.player[Main.myPlayer].lifeSteal;
			orig(self, dmg, pos, victim);
			float spent = before - Main.player[Main.myPlayer].lifeSteal;
			if (spent > 0.001f) { LsHeals++; LsSpentMilli += (int)(spent * 1000f); }
		}

		private int SpawnLsTarget(float centerX)
		{
			Vector2 pos = new Vector2(centerX, FloorY * 16f);
			int i = NPC.NewNPC(new EntitySource_WorldEvent("LevyHarness"), (int)pos.X, (int)pos.Y, NPCID.GreenSlime);
			if (i < 0 || i >= Main.maxNPCs) throw new Exception("target NewNPC failed");
			NPC n = Main.npc[i];
			var ht = n.GetGlobalNPC<HarnessTarget>();
			ht.IsTarget = true;
			n.lifeMax = 1_000_000; n.life = 1_000_000; n.defense = 0; n.damage = 0; n.knockBackResist = 0f;
			n.width = 64; n.height = 96; n.noGravity = true; n.noTileCollide = true; n.aiStyle = -1; n.friendly = false; n.dontTakeDamage = false; n.value = 0f;
			n.velocity = Vector2.Zero; n.Bottom = pos; ht.Pin = n.position; n.netUpdate = true;
			return i;
		}

		private void StepLifeSteal()
		{
			if (_lsCase >= _lsCounts.Length) { SetKeepAlive(false); if (_lsSavedMy != int.MinValue) Main.myPlayer = _lsSavedMy; Finish("lifesteal scenario complete"); return; }
			Player cmd = Main.player[0];
			if (_lsTick < 0)
			{
				ClearAll(); PlaceCommander(false);
				_lsSavedMy = _lsSavedMy == int.MinValue ? Main.myPlayer : _lsSavedMy;
				Main.myPlayer = 0;
				cmd.lifeSteal = 80f;
				SetKeepAlive(true);
				int n = _lsCounts[_lsCase];
				_lsBodies.Clear(); _lsTargets.Clear();
				for (int i = 0; i < n; i++)
				{
					var rec = MakeRecord("Pvt. Leech" + i);
					if (_lsItem != "")
					{
						Item w = LsMakeItem(_lsItem);
						if (w != null)
						{
							Item held = w.Clone();
							if (rec.Gear.CanHold(SoldierGear.SlotWeapon, held)) { rec.Gear.Weapon = held; rec.Gear.SetLocked(SoldierGear.SlotWeapon, true); }
							if (w.useAmmo > 0) { Item ammo = FindAmmo(w, ""); if (ammo != null && rec.Gear.CanHold(SoldierGear.SlotAmmo, ammo)) { rec.Gear.Ammo = ammo; rec.Gear.SetLocked(SoldierGear.SlotAmmo, true); } }
						}
					}
					rec.LoadoutLocked = true; rec.Order = LsDirect ? SoldierOrder.Hold : SoldierOrder.Follow; rec.HoldFire = LsDirect;
					rec.CurrentHp = Math.Max(1, (int)(rec.MaxHp * 0.4f)); rec.Mana = rec.MaxMana;
					RosterSystem.Instance.Regulars.Add(rec);
					float x = _cx * 16f + 8f + (i - n / 2) * 6 * 16f; // spread so each levy has his own corner of the range
					Vector2 at = new Vector2(x, FloorY * 16f);
					int idx = RosterSystem.Instance.SpawnBody(rec, at);
					if (idx < 0 || !rec.HasBody) { Mod.Logger.Warn("HARNESS-LS: SpawnBody failed"); continue; }
					var body = (SoldierNPC)Main.npc[idx].ModNPC; body.NPC.Bottom = at; if (LsDirect) body.ReceiveOrder(SoldierOrder.Hold);
					_lsBodies.Add(body);
					_lsTargets.Add(SpawnLsTarget(x + (LsDirect ? 5f : 2.5f) * 16f));
				}
				LsCalls = LsHeals = LsSpentMilli = 0;
				_lsRoutedStart = OwnerPuppet.RoutedLife; _lsHealStart = OwnerPuppet.RoutedHeals; _lsOpensStart = OwnerPuppet.PoolOpens;
				_lsUnpOwner = OwnerPuppet.UnpuppetedOwner; _lsUnpBody = OwnerPuppet.UnpuppetedBody; _lsShotTicks = 0; _lsWpn = _lsBodies.Count > 0 && _lsBodies[0].Record != null && _lsBodies[0].Record.Gear.HasWeapon ? _lsBodies[0].Record.Gear.Weapon.Name + "/" + WeaponClassifier.GetProfile(_lsBodies[0].Record.Gear.Weapon).Family : "none";
				_lsCmdFalls = 0; _lsCmdFallSum = 0f; _lsCmdPrev = cmd.lifeSteal; _lsCmdMin = _lsCmdMax = cmd.lifeSteal;
				_lsTick = 0;
				return;
			}
			// ---- per tick ----
			cmd.active = true; cmd.dead = false; cmd.name = CommanderName; cmd.statLifeMax = cmd.statLifeMax2 = 500; if (cmd.statLife < 1) cmd.statLife = 500;
			cmd.position = new Vector2(_cx * 16f - 4 * 16f, FloorY * 16f - cmd.height);
			if (cmd.lifeSteal < _lsCmdPrev - 0.001f) { _lsCmdFalls++; _lsCmdFallSum += _lsCmdPrev - cmd.lifeSteal; if (_lsCmdFalls <= 3) Mod.Logger.Info($"HARNESS-LS: commander pool fell {_lsCmdPrev:F1}->{cmd.lifeSteal:F1} at tick {_lsTick} depth={OwnerPuppet.DebugDepth}"); }
			_lsCmdPrev = cmd.lifeSteal; _lsCmdMin = Math.Min(_lsCmdMin, cmd.lifeSteal); _lsCmdMax = Math.Max(_lsCmdMax, cmd.lifeSteal);
			for (int pi = 0; pi < Main.maxProjectiles; pi++) { Projectile q = Main.projectile[pi]; if (q.active && q.TryGetGlobalProjectile(out SoldierProjectileTag qt) && qt.IsSoldierShot) _lsShotTicks++; }
			for (int k = 0; k < _lsBodies.Count; k++)
			{
				var b = _lsBodies[k];
				if (b == null || !b.NPC.active || b.Record == null) continue;
				int want = Math.Max(1, (int)(b.NPC.lifeMax * 0.4f));
				if (b.NPC.life > want) { b.NPC.life = want; b.Record.CurrentHp = want; }
				NPC t = Main.npc[_lsTargets[k]];
				if (t.active) { for (int q = 0; q < t.immune.Length; q++) t.immune[q] = 0; t.life = t.lifeMax; }
				if (LsDirect && t.active && _lsTick % _lsPeriod == (k % _lsPeriod))
				{
					Projectile pr = WeaponUse.Spawn(b, t.Center, Vector2.Zero, ProjectileID.Bullet, 5, 0f, DamageClass.Ranged);
					if (pr != null) { pr.penetrate = 1; pr.tileCollide = false; pr.timeLeft = 30; }
				}
			}
			if (++_lsTick < _lsTicks) return;
			// ---- case done ----
			int n2 = _lsBodies.Count; float poolSum = 0, poolMin = float.MaxValue; int used = 0;
			foreach (var b in _lsBodies) if (!float.IsNaN(b.LifeStealPool)) { used++; poolSum += b.LifeStealPool; poolMin = Math.Min(poolMin, b.LifeStealPool); }
			int routed = OwnerPuppet.RoutedLife - _lsRoutedStart;
			Mod.Logger.Info(string.Format(CultureInfo.InvariantCulture,
				"HARNESS-LS-RESULT: N={0} ticks={1} mode={2} shared={3} vampireHealCalls={4} poolSpendingCalls={5} poolSpent={6:F1} lifeRoutedToLevies={7} routedHeals={8} levyPoolsUsed={9}/{10} levyPoolAvgEnd={11:F1} levyPoolMinEnd={12:F1} cmdPoolFalls={13} cmdPoolFallSum={14:F1} cmdPoolEnd={15:F1} cmdPoolMin={16:F1} cmdPoolMax={17:F1} poolOpens={18} weapon={19} soldierShotTicks={20} unpuppetedOwnerInactive={21} unpuppetedNoBody={22}",
				n2, _lsTicks, LsDirect ? "direct" : _lsItem, LifeStealPool.SharedForTest, LsCalls, LsHeals, LsSpentMilli / 1000f, routed, OwnerPuppet.RoutedHeals - _lsHealStart, used, n2, used > 0 ? poolSum / used : float.NaN, used > 0 ? poolMin : float.NaN,
				_lsCmdFalls, _lsCmdFallSum, cmd.lifeSteal, _lsCmdMin, _lsCmdMax, OwnerPuppet.PoolOpens - _lsOpensStart, _lsWpn, _lsShotTicks, OwnerPuppet.UnpuppetedOwner - _lsUnpOwner, OwnerPuppet.UnpuppetedBody - _lsUnpBody));
			_lsCase++; _lsTick = -1;
		}

		private string LsLive() { int n = 0; foreach (var b in _lsBodies) if (b != null && b.NPC.active && global::Levy.Common.Targeting.EnemyTargeting.LiveBody(b.NPC.whoAmI) != null) n++; return n.ToString(); }
		private static int _lsSavedMy = int.MinValue;

		private static Item LsMakeItem(string spec)
		{
			int type = 0; int slash = spec.IndexOf('/');
			string mod = slash > 0 ? spec.Substring(0, slash) : "Vanilla", nm = slash > 0 ? spec.Substring(slash + 1) : spec;
			if (mod == "Vanilla") { var f = typeof(ItemID).GetField(nm); if (f != null) type = Convert.ToInt32(f.GetValue(null)); }
			else if (ModContent.TryFind<ModItem>(mod, nm, out var mi)) type = mi.Type;
			if (type <= 0) return null;
			var it = new Item(); it.SetDefaults(type); return it;
		}
	}

	/// <summary>Direct mode of the lifesteal scenario: a levy's bullet hits its target and the hit asks vanilla for a lifesteal bolt, as vanilla's own lifesteal weapons do.</summary>
	public class HarnessLifeStealGlobal : GlobalProjectile
	{
		public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (!CombatHarness.Enabled || !CombatHarness.LsDirect || projectile.type != ProjectileID.Bullet) return;
			if (!projectile.TryGetGlobalProjectile(out SoldierProjectileTag t) || !t.IsSoldierShot) return;
			projectile.vampireHeal(CombatHarness.LsHitDamage, target.Center, target);
		}
	}
}
