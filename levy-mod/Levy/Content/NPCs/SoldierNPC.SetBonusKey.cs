using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common;
using Levy.Common.Util;
using Levy.Common.Weapons;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// THE ARMOR SET BONUS KEY. A player triggers an activated set bonus with one input: double-tap DOWN (<c>Player.KeyDoubleTap</c>), which fires <c>PlayerLoader.ArmorSetBonusActivated</c> (every ModPlayer: Calamity's
	/// Brimflame frenzy, Tarragon cloak, Bloodflare souls, Omega Blue madness, Demonshade enrage, Plague Reaper blackout, Forbidden Circlet storm, Prismatic lasers, Desert Prowler smoke ...) and flips the vanilla
	/// bonuses (Vortex stealth, the Forbidden storm). A levy never pressed it, so those bonuses sat unused. <see cref="TickSetBonusKey"/> decides WHEN a competent player would press it and
	/// <see cref="PressSetBonusKey"/> presses it on the soldier's dummy through the very same method, in the same frame the upkeep and the hurt reaction use (slot 255, projectiles caught and adopted).
	/// Also here: the per-tick stealth stepping of the Vortex and Shroomite sets (vanilla advances it in the set pass, which runs once a second for a levy).
	/// </summary>
	public partial class SoldierNPC
	{
		private int _keyCd, _keyCalmTicks;
		/// <summary>Self-check / harness counters: presses, and presses per kind.</summary>
		public static int KeyPresses; public static readonly Dictionary<string, int> KeyPressKinds = new();
		public int KeyPressesOwn;

		private const int KeyRepressTicks = 120, KeyEngageTiles = 60, KeyForbiddenTiles = 30, KeyCalmTicks = 240;

		/// <summary>The enemy the soldier is fighting now, or null.</summary>
		private NPC KeyTarget()
		{
			if (State != SoldierState.Engage || TargetIndex < 0 || TargetIndex >= Main.maxNPCs) return null;
			NPC t = Main.npc[TargetIndex];
			return t.active && !t.friendly && t.life > 0 && NPC.Distance(t.Center) <= KeyEngageTiles * 16f ? t : null;
		}

		/// <summary>Called every combat tick on the authority. Never throws.</summary>
		private void TickSetBonusKey()
		{
			if (LevyUtil.IsClient || Dummy == null || Record == null || !IsAlive) return;
			try
			{
				TickStealthSets();
				TickCalamityMeters();
				if (_keyCd > 0) { _keyCd--; return; }
				Player d = Dummy;
				bool vortex = d.setVortex, forbidden = d.setForbidden;
				string calKind = CalamityKeyWanted(d, out bool calWants);
				if (!vortex && !forbidden && !calWants) return;
				NPC t = KeyTarget();
				if (t == null)
				{
					// out of the fight: a Vortex wearer drops stealth again so he can run
					if (vortex && d.vortexStealthActive && ++_keyCalmTicks > KeyCalmTicks) { _keyCalmTicks = 0; PressSetBonusKey(NPC.Center + new Vector2(NPC.direction * 64f, 0f), "vortex-off"); }
					return;
				}
				_keyCalmTicks = 0;
				if (calWants) { PressSetBonusKey(t.Center, calKind); return; }
				if (forbidden)
				{
					if (NPC.Distance(t.Center) > KeyForbiddenTiles * 16f || Record.Mana < 20 || OwnStormAlive()) return;
					PressSetBonusKey(t.Center, "forbidden-storm");
					return;
				}
				if (vortex && !d.vortexStealthActive) PressSetBonusKey(t.Center, "vortex-on");
			}
			catch (Exception e)
			{
				if (_accessoryProcWarned.Add(-ItemID.VortexHelmet)) Levy.Instance?.Logger.Warn("Levy: set bonus key failed on a soldier: " + e.Message);
			}
		}

		private bool OwnStormAlive()
		{
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile p = Main.projectile[i];
				if (p.active && p.type == ProjectileID.SandnadoFriendly && p.TryGetGlobalProjectile(out global::Levy.Common.Globals.SoldierProjectileTag tag) && tag.SoldierIndex == NPC.whoAmI) return true;
			}
			return false;
		}

		/// <summary>
		/// Presses the set bonus key for this soldier: <c>Player.KeyDoubleTap</c> on the dummy with the cursor on <paramref name="aim"/> (the Forbidden storm lands there). Returns true if it ran.
		/// </summary>
		internal bool PressSetBonusKey(Vector2 aim, string kind)
		{
			if (LevyUtil.IsClient || Dummy == null || !IsAlive || _inGearHurt) return false;
			Player d = Dummy;
			int savedWho = d.whoAmI; Vector2 savedCenter = d.Center;
			object thorium = null;
			FrameSlot fs = default;
			var prev = GearUpkeep.Body; GearUpkeep.Body = this;
			int spawnStart = GearUpkeep.Spawned.Count;
			Vector2 sc = Main.screenPosition; int mx = Main.mouseX, my = Main.mouseY;
			bool ran = false;
			try
			{
				d.Center = NPC.Center;
				d.statMana = Record != null ? Math.Clamp(Record.Mana, 0, Math.Max(0, Record.MaxMana)) : d.statMana;
				int mana0 = d.statMana;
				fs = EnterFrameSlot();
				thorium = OwnerPuppet.SwapThorium(d, true);
				Main.screenPosition = Vector2.Zero; Main.mouseX = (int)aim.X; Main.mouseY = (int)aim.Y;
				d.KeyDoubleTap(Main.ReversedUpDownArmorSetBonuses ? 1 : 0);
				ran = true;
				if (Record != null && d.statMana != mana0) Record.Mana = Math.Clamp(Record.Mana + (d.statMana - mana0), 0, Record.MaxMana);
			}
			catch (Exception e)
			{
				GearUpkeep.HookFailures++;
				if (GearUpkeep.WarnOnce(e)) Levy.Instance?.Logger.Warn("Levy: the set bonus key threw on a soldier (once per kind): " + e);
			}
			finally
			{
				Main.screenPosition = sc; Main.mouseX = mx; Main.mouseY = my;
				ExitFrameSlot(fs);
				d.whoAmI = savedWho; d.Center = savedCenter;
				OwnerPuppet.RestoreThorium(thorium);
				GearUpkeep.Body = prev;
				AdoptUpkeepSpawns(spawnStart, false);
			}
			_keyCd = KeyRepressTicks;
			if (ran)
			{
				KeyPresses++; KeyPressesOwn++;
				KeyPressKinds[kind] = KeyPressKinds.TryGetValue(kind, out int n) ? n + 1 : 1;
				if (SetBonusLog.First("key:" + kind)) SetBonusLog.Write("Set bonus key pressed by soldier '" + SoldierName + "': " + kind + ".");
			}
			return ran;
		}

		// ------------------------------------------------------------------------------------------------------------
		// Stealth (Vortex, Shroomite): the per-tick step of Player.UpdateArmorSets
		// ------------------------------------------------------------------------------------------------------------
		private void TickStealthSets()
		{
			Player d = Dummy;
			if (d.setVortex)
			{
				if (d.vortexStealthActive) d.stealth = Math.Max(0f, d.stealth - 0.04f);
				else d.stealth = Math.Min(1f, d.stealth + 0.04f);
			}
			else if (d.shroomiteStealth)
			{
				if (ItemAnimation > 0) d.stealthTimer = 5;
				bool still = Math.Abs(NPC.velocity.X) < 0.1f && Math.Abs(NPC.velocity.Y) < 0.1f;
				if (still)
				{
					if (d.stealthTimer == 0 && d.stealth > 0f) d.stealth = Math.Max(0f, d.stealth - 0.015f);
				}
				else d.stealth = Math.Min(1f, d.stealth + (Math.Abs(NPC.velocity.X) + Math.Abs(NPC.velocity.Y)) * 0.0075f);
				if (d.stealthTimer > 0) d.stealthTimer--;
			}
		}

		// ------------------------------------------------------------------------------------------------------------
		// Calamity: which set bonus is ready
		// ------------------------------------------------------------------------------------------------------------
		private static bool _calKeyInit;
		private static readonly Dictionary<string, Func<Player, bool>> CalFlags = new();
		private static FieldInfo _calCooldownsField; private static ModPlayer _calPlayerBase;
		private static readonly Dictionary<string, string> CalCooldownIds = new();

		private static void CalKeyInit()
		{
			_calKeyInit = true;
			if (!ModLoader.TryGetMod("CalamityMod", out Mod cal) || cal.Code == null) return;
			foreach (string f in new[] { "brimflameSet", "brimflameFrenzy", "tarraMelee", "tarragonCloak", "bloodflareRanged", "omegaBlueSet", "demonshadeSet", "plagueReaper", "forbiddenCirclet", "prismaticSet" })
			{
				var fn = Common.Team.TeamItems.ModFlag("CalamityMod", "CalamityMod.CalPlayer.CalamityPlayer", f);
				if (fn != null) CalFlags[f] = fn;
			}
			Type cp = cal.Code.GetType("CalamityMod.CalPlayer.CalamityPlayer");
			_calCooldownsField = cp?.GetField("cooldowns", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			_calPlayerBase = ModContent.GetContent<ModPlayer>().FirstOrDefault(x => x.GetType() == cp);
			foreach (string c in new[] { "BrimflameFrenzy", "TarragonCloak", "BloodflareRangedSet", "OmegaBlue", "PlagueBlackout", "PrismaticLaser", "SandsmokeBomb" })
			{
				Type t = cal.Code.GetType("CalamityMod.Cooldowns." + c);
				object id = t?.GetField("ID", BindingFlags.Static | BindingFlags.Public)?.GetValue(null) ?? t?.GetProperty("ID", BindingFlags.Static | BindingFlags.Public)?.GetValue(null);
				if (id is string s) CalCooldownIds[c] = s;
			}
		}

		private static bool CalFlag(Player d, string f) => CalFlags.TryGetValue(f, out var fn) && fn(d);

		private static bool CalOnCooldown(Player d, string cooldown)
		{
			try
			{
				if (_calCooldownsField == null || _calPlayerBase == null || !CalCooldownIds.TryGetValue(cooldown, out string id)) return false;
				var dict = _calCooldownsField.GetValue(d.GetModPlayer(_calPlayerBase)) as System.Collections.IDictionary;
				return dict != null && dict.Contains(id);
			}
			catch (Exception) { return false; }
		}

		private bool WearsHead(string item) => Record?.Gear.Head != null && !Record.Gear.Head.IsAir && ModContent.TryFind(item, out ModItem m) && Record.Gear.Head.type == m.Type;

		/// <summary>The Calamity set bonus a levy would press now (ready, not on cooldown, not already running), or null.</summary>
		private string CalamityKeyWanted(Player d, out bool wants)
		{
			wants = false;
			if (!_calKeyInit) CalKeyInit();
			if (CalFlags.Count == 0) return null;
			string k = null;
			if (CalFlag(d, "brimflameSet") && !CalFlag(d, "brimflameFrenzy") && !CalOnCooldown(d, "BrimflameFrenzy")) k = "brimflame";
			else if (CalFlag(d, "tarraMelee") && !CalFlag(d, "tarragonCloak") && !CalOnCooldown(d, "TarragonCloak")) k = "tarragon";
			else if (CalFlag(d, "bloodflareRanged") && !CalOnCooldown(d, "BloodflareRangedSet")) k = "bloodflare";
			else if (CalFlag(d, "omegaBlueSet") && !CalOnCooldown(d, "OmegaBlue")) k = "omegablue";
			else if (CalFlag(d, "demonshadeSet") && !d.HasBuff(CalBuff("Enraged"))) k = "demonshade";
			else if (CalFlag(d, "plagueReaper") && !CalOnCooldown(d, "PlagueBlackout")) k = "plaguereaper";
			else if (CalFlag(d, "forbiddenCirclet") && d.ownedProjectileCounts[CalProj("CircletTornado")] < 2 && Record != null && Record.Mana >= 40) k = "circlet";
			else if (CalFlag(d, "prismaticSet") && !CalOnCooldown(d, "PrismaticLaser")) k = "prismatic";
			else if (WearsHead("CalamityMod/DesertProwlerHat") && Record.Gear.Body != null && Record.Gear.Legs != null && !CalOnCooldown(d, "SandsmokeBomb")) k = "desertprowler";
			wants = k != null;
			return k;
		}

		private static int CalBuff(string n) => ModContent.TryFind("CalamityMod/" + n, out ModBuff b) ? b.Type : -1;
		private static int CalProj(string n) => ModContent.TryFind("CalamityMod/" + n, out ModProjectile p) ? p.Type : 0;

		// ------------------------------------------------------------------------------------------------------------
		// Owner-bound spawn effects: Calamity's Desert Prowler "Lights Out" reads the shooter's smoke in a GlobalProjectile.OnSpawn keyed to Main.player[projectile.owner], which for a soldier's shot is the commander.
		// Run once a shot is tagged (WeaponUse.TagSoldierProjectile), with the dummy standing in the owner slot, it gives the levy's shot the +200% crit, and the smoke ends with that shot as it does for a player.
		// ------------------------------------------------------------------------------------------------------------
		private static bool _lightsInit; private static GlobalProjectile _lightsGp; private static MethodInfo _lightsOnSpawn, _lightsShrouded, _lightsGetGlobal; private static ModPlayer _lightsPlayerBase;

		internal void ApplyOwnerBoundSpawnEffects(Projectile p)
		{
			if (LevyUtil.IsClient || Dummy == null || !UpkeepActive) return;
			try
			{
				if (!_lightsInit)
				{
					_lightsInit = true;
					_lightsGp = ModContent.GetContent<GlobalProjectile>().FirstOrDefault(x => x.GetType().FullName == "CalamityMod.Items.Armor.DesertProwler.DesertProwlerProjectile");
					Type hat = ModLoader.TryGetMod("CalamityMod", out Mod cal) ? cal.Code?.GetType("CalamityMod.Items.Armor.DesertProwler.DesertProwlerHat") : null;
					_lightsShrouded = hat?.GetMethod("ShroudedInSmoke");
					_lightsOnSpawn = _lightsGp?.GetType().GetMethod("OnSpawn");
					_lightsGetGlobal = typeof(Projectile).GetMethods().FirstOrDefault(m => m.Name == "GetGlobalProjectile" && m.IsGenericMethod && m.GetParameters().Length >= 1 && m.GetParameters()[0].ParameterType.IsGenericParameter)?.MakeGenericMethod(typeof(GlobalProjectile));
					_lightsPlayerBase = ModContent.GetContent<ModPlayer>().FirstOrDefault(x => x.GetType().FullName == "CalamityMod.Items.Armor.DesertProwler.DesertProwlerPlayer");
				}
				if (_lightsGp == null || _lightsShrouded == null || _lightsOnSpawn == null || _lightsGetGlobal == null || _lightsPlayerBase == null) return;
				if (!_lightsGp.AppliesToEntity(p, true)) return;
				object[] a = { Dummy, null };
				if (!(bool)_lightsShrouded.Invoke(null, a)) return;
				Player saved = Main.player[DummySlot]; int owner = p.owner, savedWho = Dummy.whoAmI;
				try
				{
					Main.player[DummySlot] = Dummy; Dummy.whoAmI = DummySlot; p.owner = DummySlot;
					object[] ga = _lightsGetGlobal.GetParameters().Length == 2 ? new object[] { _lightsGp, false } : new object[] { _lightsGp };
					object inst = _lightsGetGlobal.Invoke(p, ga);
					_lightsOnSpawn.Invoke(inst, new object[] { p, null });
					// the player's PostUpdate acts on the stopSmokeBomb flag Lights Out raised: the smoke ends with the shot
					ModPlayer mp = Dummy.GetModPlayer(_lightsPlayerBase);
					mp.GetType().GetMethod("PostUpdate")?.Invoke(mp, null);
				}
				finally { p.owner = owner; Main.player[DummySlot] = saved; Dummy.whoAmI = savedWho; }
			}
			catch (Exception e)
			{
				if (_accessoryProcWarned.Add(-ItemID.DesertTorch)) Levy.Instance?.Logger.Warn("Levy: owner-bound spawn effect failed on a soldier: " + (e.InnerException ?? e).Message);
			}
		}

		// ------------------------------------------------------------------------------------------------------------
		// Calamity Rage and Adrenaline: the keybind's body is "when the meter is full and the mode is off, add the mode buff" (CalamityPlayer.ProcessTriggers). A levy does it when the fight is on.
		// ------------------------------------------------------------------------------------------------------------
		private static bool _meterInit; private static Func<Player, object> _rage, _rageMax, _rageOn, _adr, _adrMax, _adrOn, _adrEnabled, _rageEnabled, _adrDur; private static int _rageBuff = -1, _adrBuff = -1;
		public static int MeterActivations;

		private static Func<Player, object> CalMember(Type t, ModPlayer inst, string name)
		{
			FieldInfo f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (f != null) return p => f.GetValue(p.GetModPlayer(inst));
			PropertyInfo pr = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			return pr == null ? null : p => pr.GetValue(p.GetModPlayer(inst));
		}

		private void TickCalamityMeters()
		{
			if (!_meterInit)
			{
				_meterInit = true;
				if (ModLoader.TryGetMod("CalamityMod", out Mod cal) && cal.Code != null)
				{
					Type cp = cal.Code.GetType("CalamityMod.CalPlayer.CalamityPlayer");
					ModPlayer inst = ModContent.GetContent<ModPlayer>().FirstOrDefault(x => x.GetType() == cp);
					if (cp != null && inst != null)
					{
						_rage = CalMember(cp, inst, "rage"); _rageMax = CalMember(cp, inst, "rageMax"); _rageOn = CalMember(cp, inst, "rageModeActive"); _rageEnabled = CalMember(cp, inst, "RageEnabled");
						_adr = CalMember(cp, inst, "adrenaline"); _adrMax = CalMember(cp, inst, "adrenalineMax"); _adrOn = CalMember(cp, inst, "adrenalineModeActive"); _adrEnabled = CalMember(cp, inst, "AdrenalineEnabled"); _adrDur = CalMember(cp, inst, "AdrenalineDuration");
						_rageBuff = ModContent.TryFind("CalamityMod/RageMode", out ModBuff rb) ? rb.Type : -1;
						_adrBuff = ModContent.TryFind("CalamityMod/AdrenalineMode", out ModBuff ab) ? ab.Type : -1;
					}
				}
			}
			if (_rage == null || _rageBuff < 0 || KeyTarget() == null) return;
			Player d = Dummy;
			if ((bool)_rageEnabled(d) && !(bool)_rageOn(d) && (float)_rage(d) >= (float)_rageMax(d) && (float)_rageMax(d) > 0f)
			{
				d.AddBuff(_rageBuff, 2); MeterActivations++;
				if (SetBonusLog.First("key:rage")) SetBonusLog.Write("Rage Mode started by soldier '" + SoldierName + "'.");
			}
			if (_adrBuff >= 0 && _adrEnabled != null && (bool)_adrEnabled(d) && !(bool)_adrOn(d) && (float)_adr(d) >= (float)_adrMax(d) && (float)_adrMax(d) > 0f)
			{
				d.AddBuff(_adrBuff, (int)_adrDur(d)); MeterActivations++;
				if (SetBonusLog.First("key:adrenaline")) SetBonusLog.Write("Adrenaline Mode started by soldier '" + SoldierName + "'.");
			}
		}
	}
}
