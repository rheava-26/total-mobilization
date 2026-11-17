using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Globals;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Targeting;
using Levy.Common.Util;
using Levy.Common.Weapons;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	public partial class IdentityWorldCheck
	{
		// ---------------------------------------------------------------------------------------------- DEAD HOOKS
		// On_Projectile.AI is a one-line stub the JIT inlines into Projectile.Update, so a detour on it is never called (observed: 0 calls over thousands of updates while On_Projectile.VanillaAI saw every one). Every Levy wrapper
		// that sat on it ran against nothing. This check (1) drives each of those wrappers' scenario and requires the wrapper to have ACTED (HookProbe), (2) measures what each one does, and (3) measures the Calamity plain-shot
		// holdouts that read their fire rate from Owner.HeldItem (the commander's) against a player holding the same weapon: with the fix the levy's own weapon is what the shot's owner holds while its AI runs.
		// Env: LEVY_DEADHOOKS=0 skips, LEVY_DEADHOOKS_ALONE=1 runs only this, LEVY_DEADHOOKS_TICKS=N (holdout run length, default 300), LEVY_DEADHOOKS_ONLY=Name;Name (holdouts), LEVY_DEADHOOKS_SCAN=1 prints the IL scan.

		private static readonly Dictionary<string, long> DhRows = new();
		/// <summary>Commander mana the levies' shots paid during the last <see cref="DhDamage"/> run (the pool is refilled to 200 every tick, so this is what the run drew from HER pool).</summary>
		private static long _dhMana;

		/// <summary>Damage one weapon does to the pinned enemy over <paramref name="ticks"/>: <paramref name="levies"/> levies carry it, the commander holds <paramref name="heldType"/> (0 = empty hand) without using it, or (playerUses) uses the weapon herself with no levy.</summary>
		private static long DhDamage(int weaponType, int ammoType, int levies, int ticks, int heldType, bool playerUses)
		{
			_cmdAt = OriginCmdAt(); ClearSoldiers(); _cmdHolds = false; ReleaseCommanderButtons();
			Player cmd = Main.player[0]; cmd.inventory[0] = new Item(); cmd.heldProj = -1; cmd.itemAnimation = 0; cmd.itemTime = 0; cmd.selectedItem = 0;
			Vector2 at = new Vector2(Cx * 16f + 8f, FloorY * 16f);
			SpawnOriginTarget(at);
			for (int k = 0; k < levies; k++)
			{
				Item w = new Item(); w.SetDefaults(weaponType);
				SoldierRecord rec = MakeOriginRecord(w, ammoType); rec.Name = "Pvt. Dead" + k;
				RosterSystem.Instance.Regulars.Add(rec);
				Vector2 pos = at + new Vector2(-34f * k, 0f);
				int b = RosterSystem.Instance.SpawnBody(rec, pos);
				if (b < 0 || !rec.HasBody) throw new Exception("levy " + k + " did not spawn");
				Main.npc[b].Bottom = pos;
			}
			Item mine = null;
			if (playerUses) { mine = new Item(); mine.SetDefaults(weaponType); if (ammoType > 0) { Item am = new Item(); am.SetDefaults(ammoType); am.stack = 999; cmd.inventory[54] = am; for (int s = 50; s < 54; s++) cmd.inventory[s] = new Item(); } }
			Main.rand = new Terraria.Utilities.UnifiedRandom(777);
			KeepCommander(); PinSweepTarget(); CursorOnCommander(); Step(10);
			long dmg = 0; _dhMana = 0;
			for (int k = 0; k < ticks; k++)
			{
				foreach (var r in RosterSystem.Instance.Regulars) r.Mana = r.MaxMana;
				cmd.statManaMax2 = 200; cmd.statMana = 200; // a player's pool, kept full: the shots are compared at the weapon's own rate, not at what an empty pool allows
				if (playerUses) { try { MegaCommanderUse(mine, true); } catch (Exception) { } }
				else
				{
					CursorOnCommander();
					if (heldType > 0 && (cmd.inventory[0] == null || cmd.inventory[0].type != heldType)) { Item h = new Item(); h.SetDefaults(heldType); cmd.inventory[0] = h; cmd.selectedItem = 0; }
				}
				Step(1); ClearNaturalHostiles();
				if (cmd.statMana < 200) _dhMana += 200 - cmd.statMana;
				if (_sweepTarget != null && _sweepTarget.active) { int d = _sweepTarget.lifeMax - _sweepTarget.life; if (d > 0) dmg += d; }
			}
			if (playerUses) { try { MegaCommanderUse(mine, false); } catch (Exception) { } }
			ClearSoldiers(); ReleaseCommanderButtons(); cmd.inventory[0] = new Item(); cmd.inventory[54] = new Item(); cmd.heldProj = -1; cmd.itemAnimation = 0; cmd.itemTime = 0;
			return dmg;
		}

		/// <summary>Items (any loaded mod) whose shot is a projectile whose own class chain reads Player.HeldItem and the held item's useAnimation / useTime (IL scan, not names): the "fire rate comes from the owner's held item" holdouts.</summary>
		private static List<(int itemType, string label, bool readsRate)> DhHeldReaders()
		{
			var res = new List<(int, string, bool)>();
			MethodInfo getHeld = typeof(Player).GetProperty("HeldItem")?.GetGetMethod();
			FieldInfo fAnim = typeof(Item).GetField("useAnimation"), fTime = typeof(Item).GetField("useTime");
			if (getHeld == null) return res;
			var cache = new Dictionary<Type, (bool held, bool rate)>();
			(bool held, bool rate) Scan(Type t)
			{
				if (cache.TryGetValue(t, out var c)) return c;
				bool held = false, rate = false;
				for (Type b = t; b != null && b != typeof(ModProjectile) && b != typeof(object); b = b.BaseType)
					foreach (MethodInfo m in b.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
					{
						byte[] il; try { il = m.GetMethodBody()?.GetILAsByteArray(); } catch (Exception) { continue; }
						if (il == null) continue;
						bool mHeld = false, mRate = false;
						for (int i = 0; i + 4 < il.Length; i++)
						{
							byte op = il[i];
							if (op != 0x28 && op != 0x6F && op != 0x7B) continue;
							int tok = BitConverter.ToInt32(il, i + 1);
							int table = (tok >> 24) & 0xFF; if (table != 0x0A && table != 0x06 && table != 0x04 && table != 0x2B) continue;
							try
							{
								if (op == 0x7B) { FieldInfo f = m.Module.ResolveField(tok, b.IsGenericType ? b.GetGenericArguments() : null, null); if (f == fAnim || f == fTime) mRate = true; }
								else { MethodBase mb = m.Module.ResolveMethod(tok, b.IsGenericType ? b.GetGenericArguments() : null, null); if (mb == getHeld) mHeld = true; }
							}
							catch (Exception) { }
						}
						if (mHeld) held = true; if (mHeld && mRate) rate = true;
					}
				return cache[t] = (held, rate);
			}
			for (int i = ItemID.Count; i < ItemLoader.ItemCount; i++)
			{
				if (!ContentSamples.ItemsByType.TryGetValue(i, out Item s) || s == null || s.IsAir || s.shoot <= 0 || s.damage <= 0 || s.ModItem == null) continue;
				Projectile sp = WeaponClassifier.ProjectileSample(s.shoot); if (sp?.ModProjectile == null) continue;
				var (held, rate) = Scan(sp.ModProjectile.GetType());
				if (held) res.Add((i, s.ModItem.Mod.Name + ":" + s.ModItem.Name, rate));
			}
			return res;
		}

		private static void DeadHooksCheck()
		{
			if (Environment.GetEnvironmentVariable("LEVY_DEADHOOKS") == "0") return;
			int ticks = int.TryParse(Environment.GetEnvironmentVariable("LEVY_DEADHOOKS_TICKS"), out int tk) ? tk : 300;
			string only = Environment.GetEnvironmentVariable("LEVY_DEADHOOKS_ONLY");
			SetupOriginArena();
			var ammoByKind = AmmoByKind();
			try
			{
				KeepCommander(); Step(2);
				// ---- 1. the wrappers that sat on On_Projectile.AI: do they fire, and what do they do ----
				Note("deadhooks: hooks installed: " + HookProbe.Snapshot());
				int[] start = HookProbe.Snap();
				DhDamage(ItemID.Zenith, 0, 3, 150, 0, false);
				DhDamage(ItemID.LaserMachinegun, ammoByKind.TryGetValue(AmmoID.Bullet, out int bul) ? bul : 0, 3, 150, 0, false);
				int[] d1 = HookProbe.Delta(start);
				Note("deadhooks: three levies each with the Zenith and the Laser Machinegun, 150 ticks each: " + HookProbe.Describe(d1));
				Check($"deadhooks: the held-weapon wrapper is entered through a hook that fires (Projectile.AI {HookProbe.Part(d1, "HeldAi")} / VanillaAI {HookProbe.Part(d1, "HeldVanilla")} / loader {HookProbe.Part(d1, "HeldLoader")} entries)", HookProbe.Part(d1, "HeldAi") + HookProbe.Part(d1, "HeldVanilla") > 0);
				Check($"deadhooks: the Zenith swords were anchored to their levy by the wrapper ({HookProbe.Part(d1, "MiscActed")} wrapped AI calls)", HookProbe.Part(d1, "MiscActed") > 0);
				if (HookProbe.SoundWatch)
					Check($"deadhooks: the army sound throttle's projectile update hook fires ({HookProbe.Part(d1, "SoundUpdate")} updates) and a levy shot's AI runs classified as army ({HookProbe.Part(d1, "SoundInAi")} AI calls classified, {HookProbe.Part(d1, "SoundOutAi")} not)", HookProbe.Part(d1, "SoundUpdate") > 0 && HookProbe.Part(d1, "SoundInAi") > 0 && HookProbe.Part(d1, "SoundOutAi") == 0);
				else Note("deadhooks: the army sound throttle is not installed on this machine (client only); its projectile hook is checked where LEVY_SELFCHECK_WORLD installs it");
				ZenithPhase();
				if (ModLoader.TryGetMod("CalamityMod", out Mod calMod)) AimPhase(calMod, ammoByKind);

				FlyerPhase(out int[] d2, out string flyNote);
				Note("deadhooks: flyer-slot bolts: " + flyNote);
				Check($"deadhooks: a hostile projectile aimed at the flyer slot is swapped to a levy for its AI ({HookProbe.Part(d2, "FlyerActed")} swapped calls)", HookProbe.Part(d2, "FlyerActed") > 0);

				// ---- 2. holdouts that read the owner's held item ----
				var list = new List<(int type, string label, bool rate)>();
				foreach (var h in DhHeldReaders()) list.Add(h);
				if (Environment.GetEnvironmentVariable("LEVY_DEADHOOKS_SCAN") == "1" || true)
					Note($"deadhooks: IL scan: {list.Count} items shoot a projectile whose class reads Player.HeldItem, {list.Count(l => l.rate)} also read useAnimation/useTime: " + string.Join(", ", list.Where(l => l.rate).Select(l => l.label)));
				var pick = new List<(int type, string label)>();
				foreach (string nm in new[] { "IonBlaster", "PhalanxSurge", "ApoctosisArray" })
					foreach (var l in list) if (l.label == "CalamityMod:" + nm) pick.Add((l.type, l.label));
				foreach (string nm in new[] { "Vulcan", "WildfireBloom", "Nidhogg", "HolofibreImmolator", "Photoviscerator" })
					foreach (var l in list) if (l.label == "CalamityMod:" + nm && !pick.Any(p => p.type == l.type)) pick.Add((l.type, l.label));
				// every other ranged / magic weapon that reads its fire rate from the owner's held item and is shot plain (a held chain runs against the dummy, which already holds the item)
				foreach (var l in list.Where(l => l.rate))
				{
					Item s0 = ContentSamples.ItemsByType[l.type];
					if (!pick.Any(p => p.type == l.type) && ModShotKinds.Of(s0) == ModShotKind.None && (s0.CountsAsClass(DamageClass.Ranged) || s0.CountsAsClass(DamageClass.Magic)) && s0.mana <= 40) pick.Add((l.type, l.label));
				}
				if (!string.IsNullOrEmpty(only)) { string[] names = only.Split(';', StringSplitOptions.RemoveEmptyEntries); pick = pick.Where(p => names.Any(n => p.label.EndsWith(":" + n))).ToList(); }
				foreach (var (type, label) in pick)
				{
					try
					{
						Item sample = ContentSamples.ItemsByType[type];
						int ammo = sample.useAmmo > 0 && ammoByKind.TryGetValue(sample.useAmmo, out int a) ? a : 0;
						string routed = ModShotKinds.Of(sample).ToString();
						long player = DhDamage(type, ammo, 0, ticks, 0, true);
						long empty = DhDamage(type, ammo, 1, ticks, 0, false); if (!string.IsNullOrEmpty(HookProbe.ViewLast)) { Note("deadhooks: view debug (empty) " + HookProbe.ViewLast); HookProbe.ViewLast = ""; }
						long sword = DhDamage(type, ammo, 1, ticks, ItemID.CopperShortsword, false); if (!string.IsNullOrEmpty(HookProbe.ViewLast)) { Note("deadhooks: view debug (sword) " + HookProbe.ViewLast); HookProbe.ViewLast = ""; }
						long slow = DhDamage(type, ammo, 1, ticks, ItemID.Zenith, false); if (!string.IsNullOrEmpty(HookProbe.ViewLast)) { Note("deadhooks: view debug (zenith) " + HookProbe.ViewLast); HookProbe.ViewLast = ""; }
						long same = DhDamage(type, ammo, 1, ticks, type, false);
						Note($"deadhooks: holdout {label} (shot kind {routed}, useAnimation {sample.useAnimation}) damage over {ticks} ticks: player using it {player}; ONE levy with the commander's hand empty {empty}, holding a Copper Shortsword {sword}, holding a Zenith {slow}, holding the same weapon {same}");
						DhRows[label] = empty;
						if (!string.IsNullOrEmpty(HookProbe.ViewLast)) { Note("deadhooks: view debug " + HookProbe.ViewLast); HookProbe.ViewLast = ""; }
						long lo = Math.Min(Math.Min(empty, sword), Math.Min(slow, same)), hi = Math.Max(Math.Max(empty, sword), Math.Max(slow, same));
						// the rate is the weapon's own: what the commander holds does not move it (a small band for the sim's chaos), and a levy does not out-fire the player who holds the same weapon
						if (label == "CalamityMod:IonBlaster" || label == "CalamityMod:PhalanxSurge" || label == "CalamityMod:ApoctosisArray")
						{
							// the held chain gives the weapon's own rate: the levy does what a player holding it does (band for the sim's chaos), whatever the commander holds, and never the hundred-fold of an empty hand
							Check($"deadhooks: {label} fires at its own rate whatever the commander holds (levy damage {empty} empty-handed, {sword} sword, {slow} Zenith, {same} same weapon; player {player})", hi <= Math.Max(1L, lo) * 1.6 + 50 && hi <= Math.Max(1L, player) * 2 + 100 && lo > 0);
						}
					}
					catch (Exception e) { Note($"deadhooks: holdout {label} threw {e.GetType().Name}: {e.Message} @ {(e.StackTrace ?? "").Split('\n')[0].Trim()}"); }
				}
				int[] dAll = HookProbe.Delta(start);
				if (list.Count > 0 && pick.Count > 0)
					Check($"deadhooks: a soldier shot whose AI reads the owner's hand runs with its levy's weapon there ({HookProbe.Part(dAll, "ViewActed")} calls over {pick.Count} weapons, {HookProbe.Part(dAll, "ViewMismatch")} where Player.HeldItem was not the levy's item)", HookProbe.Part(dAll, "ViewActed") > 0 && HookProbe.Part(dAll, "ViewMismatch") == 0);
				Note("deadhooks: totals: " + HookProbe.Snapshot());
			}
			finally { _spNet = false; _sweepTarget = null; ClearSoldiers(); ReleaseCommanderButtons(); }
		}

		/// <summary>
		/// The aim a levy's shot reads. (1) Calamity's Auger and Holofibre Immolator holdouts and the Doomsday Device turn "owner to cursor" into a direction: a held levy's cursor must be a valid point away from the levy, so none of its projectiles
		/// ever has a NaN position, velocity or rotation. (2) The Conference Call drops its sky bullets over the cursor's world X (Main.mouseX): the commander's cursor is parked 150 tiles to the left of the levy, and every bullet must still
		/// come from over the levy and its target, never over her cursor.
		/// </summary>
		private static void AimPhase(Mod cal, Dictionary<int, int> ammoByKind)
		{
			int bullet = ammoByKind.TryGetValue(AmmoID.Bullet, out int bu) ? bu : 0;
			foreach (string nm in new[] { "Auger", "HolofibreImmolator", "DoomsdayDevice", "ConferenceCall" })
			{
				if (!cal.TryFind(nm, out ModItem mi)) continue;
				try
				{
					_cmdAt = OriginCmdAt(); ClearSoldiers(); ReleaseCommanderButtons();
					Vector2 at = new Vector2(Cx * 16f + 8f, FloorY * 16f);
					SpawnOriginTarget(at);
					Item w = new Item(); w.SetDefaults(mi.Type);
					int ammo = w.useAmmo > 0 && ammoByKind.TryGetValue(w.useAmmo, out int a) ? a : (nm == "ConferenceCall" ? bullet : 0);
					SoldierRecord rec = MakeOriginRecord(w, ammo); rec.Name = "Pvt. Aim"; RosterSystem.Instance.Regulars.Add(rec);
					int b = RosterSystem.Instance.SpawnBody(rec, at); if (b < 0 || !rec.HasBody) { Note($"deadhooks: aim: {nm} levy did not spawn"); continue; }
					Main.npc[b].Bottom = at; Main.rand = new Terraria.Utilities.UnifiedRandom(777);
					KeepCommander(); PinSweepTarget(); Step(10);
					int nan = 0, seenN = 0, nearLevy = 0, nearCursor = 0; var seen = new HashSet<(int, int)>(); float cursorX = at.X - 150f * 16f;
					for (int k = 0; k < 240; k++)
					{
						foreach (var r in RosterSystem.Instance.Regulars) r.Mana = r.MaxMana;
						Main.player[0].statManaMax2 = 200; Main.player[0].statMana = 200;
						Main.screenPosition = Vector2.Zero; Main.mouseX = (int)cursorX; Main.mouseY = (int)(FloorY * 16f - 200f); // the commander's cursor, far to the left of the fight
						Step(1); ClearNaturalHostiles();
						for (int i = 0; i < Main.maxProjectiles; i++)
						{
							Projectile p = Main.projectile[i];
							if (!p.active || !p.TryGetGlobalProjectile(out SoldierProjectileTag tag) || tag.SoldierIndex != b) continue;
							if (!float.IsFinite(p.position.X) || !float.IsFinite(p.position.Y) || !float.IsFinite(p.velocity.X) || !float.IsFinite(p.velocity.Y) || !float.IsFinite(p.rotation)) nan++;
							if (nm == "ConferenceCall" && seen.Add((i, p.identity)))
							{
								seenN++; float dx = Math.Abs(p.Center.X - at.X) / 16f, dc = Math.Abs(p.Center.X - cursorX) / 16f;
								if (dx < 40f) nearLevy++; if (dc < 40f) nearCursor++;
							}
						}
					}
					ClearSoldiers();
					Note($"deadhooks: aim: {nm}, one levy, the commander's cursor 150 tiles to the left, 240 ticks: {nan} projectile ticks with a NaN position, velocity or rotation" + (nm == "ConferenceCall" ? $"; {seenN} bullets first seen, {nearLevy} within 40 tiles (horizontally) of the levy, {nearCursor} within 40 tiles of her cursor" : ""));
					Check($"deadhooks: {nm} in levy hands has no NaN projectile ({nan} ticks)", nan == 0);
					if (nm == "ConferenceCall" && seenN > 0) Check($"deadhooks: ConferenceCall's bullets come from over its levy and target, not over the commander's cursor ({nearLevy} of {seenN} within 40 tiles of the levy, {nearCursor} within 40 tiles of her cursor)", nearCursor == 0 && nearLevy * 10 >= seenN * 9);
				}
				catch (Exception e) { Note($"deadhooks: aim: {nm} threw {e.GetType().Name}: {e.Message}"); }
			}
		}

		/// <summary>One levy with the Zenith, the commander 45 tiles to the left: where do the swords orbit, and what do they do to the enemy.</summary>
		private static void ZenithPhase()
		{
			_cmdAt = OriginCmdAt(); ClearSoldiers(); ReleaseCommanderButtons();
			Vector2 at = new Vector2(Cx * 16f + 8f, FloorY * 16f);
			SpawnOriginTarget(at);
			Item w = new Item(); w.SetDefaults(ItemID.Zenith);
			SoldierRecord rec = MakeOriginRecord(w, 0); rec.Name = "Pvt. Zenith"; RosterSystem.Instance.Regulars.Add(rec);
			int b = RosterSystem.Instance.SpawnBody(rec, at); if (b < 0 || !rec.HasBody) { Note("deadhooks: zenith levy did not spawn"); return; }
			Main.npc[b].Bottom = at; Main.rand = new Terraria.Utilities.UnifiedRandom(777);
			KeepCommander(); PinSweepTarget(); CursorOnCommander(); Step(10);
			long dmg = 0; int orbit = 0, nearCmd = 0; float maxFromLevy = 0f;
			for (int k = 0; k < 300; k++)
			{
				foreach (var r in RosterSystem.Instance.Regulars) r.Mana = r.MaxMana;
				CursorOnCommander(); Step(1); ClearNaturalHostiles();
				if (_sweepTarget != null && _sweepTarget.active) { int d = _sweepTarget.lifeMax - _sweepTarget.life; if (d > 0) dmg += d; }
				Vector2 cmdC = Main.player[0].Center, levyC = Main.npc[b].Center;
				for (int i = 0; i < Main.maxProjectiles; i++)
				{
					Projectile p = Main.projectile[i];
					if (!p.active || p.type != ProjectileID.FinalFractal || !p.TryGetGlobalProjectile(out SoldierProjectileTag tag) || tag.SoldierIndex != b) continue;
					orbit++; float dl = Vector2.Distance(p.Center, levyC), dc = Vector2.Distance(p.Center, cmdC); maxFromLevy = Math.Max(maxFromLevy, dl); if (dc < dl) nearCmd++;
				}
			}
			ClearSoldiers();
			Note($"deadhooks: Zenith, one levy, commander 45 tiles away, 300 ticks: sword ticks {orbit}, nearer the commander than the levy {nearCmd}, farthest from the levy {maxFromLevy / 16f:0.0} tiles, damage to the enemy {dmg}{(ProjAiHooks.Off ? " [revived hooks OFF]" : "")}");
			Check($"deadhooks: the Zenith's swords orbit their levy, not the commander ({nearCmd} of {orbit} sword ticks nearer her)", orbit == 0 || nearCmd == 0);
		}

		/// <summary>A hostile projectile whose ai[0] is the flyer slot (what an Empress of Light bolt carries) is fired near a levy; it must be run with that levy in slot 254, so Calamity's bolt steers at the levy and not at the world origin.</summary>
		private static void FlyerPhase(out int[] delta, out string note)
		{
			int[] start = HookProbe.Snap();
			note = "";
			ClearSoldiers(); _cmdAt = OriginCmdAt();
			Vector2 at = new Vector2(Cx * 16f + 8f, FloorY * 16f);
			if (_sweepTarget != null) _sweepTarget.active = false; _sweepTarget = null;
			Item w = new Item(); w.SetDefaults(ItemID.CopperShortsword);
			SoldierRecord rec = MakeOriginRecord(w, 0); rec.Name = "Pvt. Flyer"; RosterSystem.Instance.Regulars.Add(rec);
			int b = RosterSystem.Instance.SpawnBody(rec, at);
			if (b < 0 || !rec.HasBody) { delta = HookProbe.Delta(start); note = "levy did not spawn"; return; }
			Main.npc[b].Bottom = at;
			KeepCommander(); Step(5);
			int type = ProjectileID.HallowBossRainbowStreak;
			Vector2 from = at + new Vector2(-30f * 16f, -24f * 16f);
			int pi = Projectile.NewProjectile(new Terraria.DataStructures.EntitySource_Misc("DeadHooks"), from, new Vector2(3f, 0f), type, 10, 0f, Main.myPlayer, EnemyTargeting.FlyerSlot, 0f);
			if (pi < 0 || pi >= Main.maxProjectiles) { delta = HookProbe.Delta(start); note = "bolt did not spawn"; return; }
			Projectile bolt = Main.projectile[pi]; bolt.hostile = true; bolt.friendly = false; bolt.ai[0] = EnemyTargeting.FlyerSlot;
			float d0 = Vector2.Distance(from, Main.npc[b].Center), dMin = d0; float cosEnd = 0f; int ticksAlive = 0;
			for (int k = 0; k < 120 && bolt.active; k++)
			{
				KeepCommander(); Step(1); bolt.timeLeft = Math.Max(bolt.timeLeft, 100);
				if (!bolt.active) break; ticksAlive++;
				float d = Vector2.Distance(bolt.Center, Main.npc[b].Center); if (d < dMin) dMin = d;
				if (bolt.velocity.LengthSquared() > 0.01f) cosEnd = Vector2.Dot(Vector2.Normalize(bolt.velocity), Vector2.Normalize(Main.npc[b].Center - bolt.Center));
			}
			float toOrigin = Vector2.Distance(bolt.Center, Vector2.Zero);
			note = $"{ProjectileID.Search.GetName(type)} ai0=254 from {d0 / 16f:0.0} tiles: closest approach to the levy {dMin / 16f:0.0} tiles, cos(velocity, direction to levy) {cosEnd:0.00} after {ticksAlive} ticks, speed {bolt.velocity.Length():0.0}";
			if (bolt.active) bolt.active = false;
			ClearSoldiers();
			delta = HookProbe.Delta(start);
		}
	}
}
