using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Globals;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	public partial class IdentityWorldCheck
	{
		// ---------------------------------------------------------------------------------------------- AMMO WORLD: special ammo does in a levy's gun what it does in a player's
		// Logged as "[AmmoWorld]" lines with its own summary. For each special ammo (vanilla on the vanilla half, every Calamity bullet and arrow on the Calamity half) one gun or bow fires it for a window twice:
		//   LEVY:   one levy holds its ground 12 tiles from a pinned target, the stand-in commander (slot 0, the single-player owner: Main.myPlayer = 0 while projectiles update) stands 64 tiles to the LEFT with her cursor on herself;
		//   PLAYER: no levy, the commander herself fires the same gun and ammo from the levy's spot with her cursor on the target (her real ItemCheck).
		// Every friendly projectile is followed from the tick NewProjectile made it: where it was born (against the shooter and the commander), its parent, how long it lived, any jump in one tick far beyond its speed (a warp),
		// and every hit it lands (count, damage). The checks: damage per shot within 25% of the player's, nothing born at or warped to the commander, every child born near the shot that made it, no lifetime beyond the player's.
		// Env: LEVY_AMMOWORLD=0 skips, LEVY_AMMOWORLD_ALONE=1 runs only this, LEVY_AMMO_TICKS=N (window, default 600), LEVY_AMMO_ONLY=Name;Name (ammo item names), LEVY_AMMO_DEBUG=1 (per-projectile lines).

		private static int _awPass, _awFail;
		private static void ACheck(string name, bool ok) { if (ok) _awPass++; else _awFail++; _mod.Logger.Info($"[AmmoWorld] {(ok ? "PASS" : "FAIL")} {name}"); }
		private static void ANote(string s) => _mod.Logger.Info("[AmmoWorld] OBSERVED " + s);
		private static readonly bool AmmoDebug = Environment.GetEnvironmentVariable("LEVY_AMMO_DEBUG") == "1";
		private const int AmmoCmdTiles = 64, AmmoNearCmdTiles = 10;

		private sealed class AmmoType
		{
			public int Born, Children, AtCmd, Untagged, Died, Jumps, JumpsToCmd, Hits, NearCmdTicks; public long LifeSum, HitDamage; public int MaxLife; public float MaxJump, MaxChildFromParent, MinFromCmd = 1e9f, MaxBornFromShooter;
		}

		private sealed class AmmoStat
		{
			public string Label, Mode, Err = ""; public long Damage; public int Ticks, Shots; public int ShotType;
			public Dictionary<string, AmmoType> ByType = new();
			public int AtCmd => ByType.Values.Sum(v => v.AtCmd);
			public int JumpsToCmd => ByType.Values.Sum(v => v.JumpsToCmd);
			public int NearCmd => ByType.Values.Sum(v => v.NearCmdTicks);
			public int Untagged => ByType.Values.Sum(v => v.Untagged);
			public float MaxChildFromParent => ByType.Count == 0 ? 0f : ByType.Values.Max(v => v.MaxChildFromParent);
			public double PerShot => Shots > 0 ? Damage / (double)Shots : 0;
			public double Dps => Ticks > 0 ? Damage * 60.0 / Ticks : 0;
			public override string ToString() => $"{Label} [{Mode}] ticks={Ticks} shots={Shots} dmg={Damage} perShot={PerShot:0.0} dps={Dps:0} atCmd={AtCmd} jumpsToCmd={JumpsToCmd} nearCmdTicks={NearCmd} untagged={Untagged}{(Err.Length > 0 ? " ERR " + Err : "")} | "
				+ string.Join("; ", ByType.OrderByDescending(kv => kv.Value.Born).Take(6).Select(kv => $"{kv.Key} born={kv.Value.Born} (child {kv.Value.Children}, maxFromShooter {kv.Value.MaxBornFromShooter / 16f:0.0}t, maxFromParent {kv.Value.MaxChildFromParent / 16f:0.0}t) life avg={(kv.Value.Died > 0 ? kv.Value.LifeSum / (double)kv.Value.Died : 0):0.0} max={kv.Value.MaxLife} hits={kv.Value.Hits} hitDmg={kv.Value.HitDamage} jumps={kv.Value.Jumps} (max {kv.Value.MaxJump / 16f:0.0}t, to cmd {kv.Value.JumpsToCmd})"));
		}

		private sealed class AmmoLive { public int Type, Born; public long Serial; public Vector2 Last; public float LastSpeed; public string Name; }

		/// <summary>One gun + ammo for <paramref name="ticks"/> ticks, fired by one levy (<paramref name="player"/> false) or by the commander from the levy's spot.</summary>
		private static AmmoStat AmmoRun(int weaponType, int ammoType, string label, bool player, int ticks)
		{
			var st = new AmmoStat { Label = label, Mode = player ? "player" : "levy" };
			Item ammoSample = ContentSamples.ItemsByType[ammoType];
			st.ShotType = ammoSample.shoot;
			Vector2 at = new Vector2(Cx * 16f + 8f, FloorY * 16f);
			_cmdAt = player ? at - new Vector2(0f, 24f) : HoldCmdAt();
			ClearSoldiers(); _cmdHolds = false; ReleaseCommanderButtons(); ClearRogueAdds();
			Player cmd = Main.player[0]; cmd.inventory[0] = new Item(); cmd.heldProj = -1; cmd.itemAnimation = 0; cmd.itemTime = 0;
			for (int s = 50; s < 58; s++) cmd.inventory[s] = new Item();
			SpawnOriginTarget(at);
			_sweepVel = Vector2.Zero;
			int body = -1;
			if (!player)
			{
				Item w = new Item(); w.SetDefaults(weaponType);
				SoldierRecord rec = MakeOriginRecord(w, ammoType); rec.Name = "Pvt. Ammo";
				rec.Gear.Ammo.stack = rec.Gear.Ammo.maxStack;
				rec.Order = SoldierOrder.Hold; rec.HoldPos = at;
				RosterSystem.Instance.Regulars.Add(rec);
				body = RosterSystem.Instance.SpawnBody(rec, at);
				if (body < 0 || !rec.HasBody) throw new Exception("levy did not spawn");
				Main.npc[body].Bottom = at;
			}
			Item cw = null;
			if (player) { cw = new Item(); cw.SetDefaults(weaponType); Item am = new Item(); am.SetDefaults(ammoType); am.stack = am.maxStack; cmd.inventory[54] = am; }
			Main.rand = new Terraria.Utilities.UnifiedRandom(4242);
			KeepCommander(); PinSweepTarget(); CursorOnCommander(); Step(10);
			for (int i = 0; i < Main.maxProjectiles; i++) Main.projectile[i].active = false;
			var live = new Dictionary<int, AmmoLive>(); var lastSerial = new long[Main.maxProjectiles];
			var lastPos = new Dictionary<long, Vector2>();
			AmmoProbe.Reset(); AmmoProbe.On = true;
			int dbg = 0;
			try
			{
				for (int k = 0; k < ticks; k++)
				{
					if (player) { try { HoldCommanderUse(cw, true); } catch (Exception e) { st.Err = "cmd use " + (e is TargetInvocationException && e.InnerException != null ? e.InnerException.GetType().Name : e.GetType().Name); } }
					else CursorOnCommander();
					if (!player) foreach (var r in RosterSystem.Instance.Regulars) { if (r.Gear.Ammo != null && !r.Gear.Ammo.IsAir) r.Gear.Ammo.stack = r.Gear.Ammo.maxStack; }
					else if (cmd.inventory[54] != null && !cmd.inventory[54].IsAir) cmd.inventory[54].stack = cmd.inventory[54].maxStack;
					Step(1); ClearNaturalHostiles();
					if (_sweepTarget != null && _sweepTarget.active) { int d = _sweepTarget.lifeMax - _sweepTarget.life; if (d > 0) st.Damage += d; }
					st.Ticks++;
					Vector2 cmdC = Main.player[0].Center;
					Vector2 shooter = player ? cmdC : (body >= 0 && Main.npc[body].active ? Main.npc[body].Center : at);
					// deaths: a slot that went inactive or was refilled
					foreach (int slot in live.Keys.ToList())
					{
						Projectile q = Main.projectile[slot]; AmmoLive l = live[slot];
						bool same = q.active && q.type == l.Type && (!AmmoProbe.Births.TryGetValue(slot, out var bb) || bb.serial == l.Serial);
						if (same) continue;
						live.Remove(slot);
						if (st.ByType.TryGetValue(l.Name, out AmmoType t)) { t.Died++; int life = k - l.Born; t.LifeSum += life; t.MaxLife = Math.Max(t.MaxLife, life); }
					}
					for (int i = 0; i < Main.maxProjectiles; i++)
					{
						Projectile p = Main.projectile[i];
						if (!p.active || p.hostile || !p.friendly && p.damage <= 0) continue;
						bool probed = AmmoProbe.Births.TryGetValue(i, out var ba) && ba.type == p.type;
						string pn = p.ModProjectile?.Name ?? ProjectileID.Search.GetName(p.type);
						bool tagged = p.TryGetGlobalProjectile(out SoldierProjectileTag tag) && tag.SoldierIndex >= 0 && tag.SoldierIndex == body;
						if (probed && ba.serial != lastSerial[i])
						{
							lastSerial[i] = ba.serial;
							var born = ba;
							if (!st.ByType.TryGetValue(pn, out AmmoType t)) st.ByType[pn] = t = new AmmoType();
							t.Born++;
							float dsh = Vector2.Distance(ba.at, shooter), dc = Vector2.Distance(ba.at, cmdC);
							bool child = born.parentProj;
							if (child) { t.Children++; t.MaxChildFromParent = Math.Max(t.MaxChildFromParent, Vector2.Distance(ba.at, born.parentAt)); }
							else t.MaxBornFromShooter = Math.Max(t.MaxBornFromShooter, dsh);
							if (!player && dc < AmmoNearCmdTiles * 16f && dc < dsh) t.AtCmd++;
							if (!player && !tagged) t.Untagged++;
							t.MinFromCmd = Math.Min(t.MinFromCmd, dc);
							if (!child && p.type == st.ShotType) st.Shots++;
							live[i] = new AmmoLive { Type = p.type, Born = k, Serial = ba.serial, Last = p.Center, LastSpeed = p.velocity.Length() * (1 + p.extraUpdates), Name = pn };
							if (AmmoDebug && dbg++ < 60) ANote($"  dbg {label} [{st.Mode}] t={k} born {pn}{(child ? " child of " + born.parentName : "")} fromShooter={dsh / 16f:0.0}t fromCmd={dc / 16f:0.0}t dmg={p.damage} pen={p.penetrate} tl={p.timeLeft} upd={1 + p.extraUpdates} tagged={tagged}");
							continue;
						}
						if (!live.TryGetValue(i, out AmmoLive lv) || lv.Type != p.type) continue;
						AmmoType at2 = st.ByType[lv.Name];
						float moved = Vector2.Distance(p.Center, lv.Last), speed = Math.Max(lv.LastSpeed, p.velocity.Length() * (1 + p.extraUpdates));
						if (moved > speed * 2f + 64f)
						{
							at2.Jumps++; at2.MaxJump = Math.Max(at2.MaxJump, moved);
							float jc = Vector2.Distance(p.Center, cmdC);
							if (!player && jc < AmmoNearCmdTiles * 16f) at2.JumpsToCmd++;
							if (AmmoDebug && dbg++ < 60) ANote($"  dbg {label} [{st.Mode}] t={k} {pn} jumped {moved / 16f:0.0}t to {(p.Center.X - shooter.X) / 16f:0.0},{(p.Center.Y - shooter.Y) / 16f:0.0}t from the shooter, {jc / 16f:0.0}t from the commander, ai=({p.ai[0]:0.#},{p.ai[1]:0.#}) vel=({p.velocity.X:0.0},{p.velocity.Y:0.0}) dmg={p.damage} pen={p.penetrate}");
						}
						if (!player && Vector2.Distance(p.Center, cmdC) < AmmoNearCmdTiles * 16f) at2.NearCmdTicks++;
						lv.Last = p.Center; lv.LastSpeed = p.velocity.Length() * (1 + p.extraUpdates);
					}
				}
			}
			finally { AmmoProbe.On = false; _parityAim = null; _cmdHolds = false; }
			foreach (var kv in AmmoProbe.Hits) { string n = ProjName(kv.Key); if (!st.ByType.TryGetValue(n, out AmmoType t)) st.ByType[n] = t = new AmmoType(); t.Hits += kv.Value.n; t.HitDamage += kv.Value.dmg; }
			if (player) { try { MegaCommanderUse(cw, false); } catch (Exception) { } Main.mouseLeft = false; Main.mouseLeftRelease = true; }
			ClearSoldiers(); ClearRogueAdds(); ReleaseCommanderButtons(); cmd.inventory[0] = new Item(); cmd.inventory[54] = new Item(); cmd.heldProj = -1; cmd.itemAnimation = 0; cmd.itemTime = 0;
			return st;
		}

		private static string ProjName(int type) => type >= ProjectileID.Count ? (ProjectileLoader.GetProjectile(type)?.Name ?? type.ToString()) : ProjectileID.Search.GetName(type);

		/// <summary>The gun or bow each ammo is fired from: a plain one that does not convert the ammo (Megashark for bullets, Platinum Bow for arrows).</summary>
		private static int AmmoGunFor(Item ammo)
		{
			if (ammo.ammo == AmmoID.Bullet) return ItemID.Megashark;
			if (ammo.ammo == AmmoID.Arrow) return ItemID.PlatinumBow;
			return 0;
		}

		private static void AmmoWorldCheck()
		{
			if (Environment.GetEnvironmentVariable("LEVY_AMMOWORLD") == "0") return;
			_awPass = _awFail = 0;
			int ticks = int.TryParse(Environment.GetEnvironmentVariable("LEVY_AMMO_TICKS"), out int tk) ? tk : 600;
			var sw = System.Diagnostics.Stopwatch.StartNew();
			var list = new List<(int ammo, string label)>();
			foreach (short t in new short[] { ItemID.ChlorophyteBullet, ItemID.ExplodingBullet, ItemID.MoonlordBullet, ItemID.HolyArrow }) list.Add((t, "Terraria:" + ItemID.Search.GetName(t)));
			if (ModLoader.TryGetMod("CalamityMod", out Mod cal))
				for (int i = ItemID.Count; i < ItemLoader.ItemCount; i++)
					if (ContentSamples.ItemsByType.TryGetValue(i, out Item s) && s?.ModItem?.Mod == cal && (s.ammo == AmmoID.Bullet || s.ammo == AmmoID.Arrow) && s.shoot > 0) list.Add((i, "CalamityMod:" + s.ModItem.Name));
			string only = Environment.GetEnvironmentVariable("LEVY_AMMO_ONLY");
			if (!string.IsNullOrEmpty(only)) { var names = only.Split(';', StringSplitOptions.RemoveEmptyEntries); list = list.Where(a => names.Any(n => a.label.EndsWith(":" + n))).ToList(); }
			try
			{
				Arena(); _cmdAt = HoldCmdAt(); KeepCommander(); Step(2);
				foreach (var (ammo, label) in list)
				{
					try
					{
						Item am = ContentSamples.ItemsByType[ammo];
						int gun = AmmoGunFor(am);
						if (gun == 0) { ANote($"{label}: no gun for ammo kind {am.ammo}"); continue; }
						string l = label + " in " + ItemID.Search.GetName(gun);
						AmmoStat lv = AmmoRun(gun, ammo, l, false, ticks);
						AmmoStat pl = AmmoRun(gun, ammo, l, true, ticks);
						ANote(lv.ToString()); ANote(pl.ToString());
						double ratio = pl.PerShot > 0 ? lv.PerShot / pl.PerShot : 0;
						ANote($"{l}: levy/player damage per shot {ratio:0.00}, dps {lv.Dps:0} / {pl.Dps:0}");
						ACheck($"{l}: a levy's shot does what the player's does (damage per shot {lv.PerShot:0.0} vs {pl.PerShot:0.0}, ratio {ratio:0.00}, within 0.75 to 1.25)", lv.Shots > 0 && pl.Shots > 0 && ratio >= 0.75 && ratio <= 1.25);
						ACheck($"{l}: nothing of the levy's shot is born at, warped to or lingers at the commander {AmmoCmdTiles} tiles away (born {lv.AtCmd}, warped {lv.JumpsToCmd}, ticks near her {lv.NearCmd}, untagged {lv.Untagged})", lv.AtCmd == 0 && lv.JumpsToCmd == 0 && lv.NearCmd == 0 && lv.Untagged == 0);
						float pMaxLife = pl.ByType.Count == 0 ? 0 : pl.ByType.Values.Max(v => v.MaxLife), lMaxLife = lv.ByType.Count == 0 ? 0 : lv.ByType.Values.Max(v => v.MaxLife);
						ACheck($"{l}: no projectile of the levy's outlives the player's (longest {lMaxLife} vs {pMaxLife} ticks)", lMaxLife <= pMaxLife + 10);
					}
					catch (Exception e) { ACheck($"{label} threw {e.GetType().Name}: {e.Message} @ {(e.StackTrace ?? "").Split('\n')[0].Trim()}", false); }
				}
			}
			catch (Exception e) { ACheck("ammo world threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim(), false); }
			finally { _sweepTarget = null; ClearRogueAdds(); ClearSoldiers(); ReleaseCommanderButtons(); }
			ANote($"ammo world ran in {sw.ElapsedMilliseconds / 1000} s ({list.Count} ammo, {ticks} ticks each side)");
			_mod.Logger.Info($"[AmmoWorld] self-check pass={_awPass} fail={_awFail}");
		}
	}

	/// <summary>Lab only (the ammo world check): each projectile's parent at birth, and every hit a friendly projectile lands, while <see cref="On"/>.</summary>
	public class AmmoProbe : GlobalProjectile
	{
		internal static bool On;
		internal static readonly Dictionary<int, (int type, Vector2 at, long serial, bool parentProj, Vector2 parentAt, string parentName)> Births = new();
		private static long _serial;
		internal static readonly Dictionary<int, (int n, long dmg)> Hits = new();
		internal static void Reset() { Births.Clear(); Hits.Clear(); }
		public override void OnSpawn(Projectile projectile, IEntitySource source)
		{
			if (!On) return;
			bool pp = source is EntitySource_Parent ep && ep.Entity is Projectile;
			Projectile par = pp ? (Projectile)((EntitySource_Parent)source).Entity : null;
			Births[projectile.whoAmI] = (projectile.type, projectile.Center, ++_serial, pp, par != null ? par.Center : Vector2.Zero, par != null ? (par.ModProjectile?.Name ?? ProjectileID.Search.GetName(par.type)) : "");
		}
		public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (!On) return;
			Hits.TryGetValue(projectile.type, out var h); Hits[projectile.type] = (h.n + 1, h.dmg + damageDone);
		}
	}
}
