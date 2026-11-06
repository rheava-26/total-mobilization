using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common;
using Levy.Common.Globals;
using Levy.Common.Officers;
using Levy.Common.Systems;
using Levy.Common.Util;
using Levy.Content.Buffs;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// OFFICER LAB (LEVY_HARNESS_SCENARIO=officer, LEVY_HARNESS_OFFICER=ArmsDealer|Painter|Guide|...). Commissions a real town NPC as an officer in the test world and drives the PRODUCTION code
	/// (OfficerSystem avatar, SoldierNPC.Officer.cs, Officer_ParkTownNpc, the orders): it looks at the draw state vanilla would render (the parked town NPC's ai[0..2] and frame inputs), and walks the
	/// orders (follow, hold, follow again, hold fire, fire at will). One "OFFLAB:" line per check, PASS / FAIL, then a summary. The render itself cannot be seen headless; the draw STATE is the proof.
	/// </summary>
	/// <summary>Keeps the lab's commander present at every phase of the tick (the dedicated server clears an unconnected player slot), so the officer system sees a commander online.</summary>
	public class OfficerLabPin : ModSystem
	{
		private delegate void OrigVoid();
		public override void Load()
		{
			if (!Main.dedServ || !string.Equals(Environment.GetEnvironmentVariable("LEVY_HARNESS_SCENARIO"), "officer", StringComparison.OrdinalIgnoreCase)) return;
			// The server's connection pass (Netplay.UpdateConnectedClients) switches off every player slot with no connected client, every tick. The lab's stand-in commander has no client,
			// so the pass is skipped while the lab runs: the officer system must see a commander online to field avatars.
			var m = typeof(Netplay).GetMethod("UpdateConnectedClients", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
			if (m != null) MonoModHooks.Add(m, (OrigVoid orig) => { if (CombatHarness.OlPinTiles == null) orig(); });
		}
		public override void PreUpdateWorld() => CombatHarness.OlApplyPin();
		public override void PostUpdatePlayers() => CombatHarness.OlApplyPin();
		public override void PreUpdateNPCs() => CombatHarness.OlApplyPin();
		public override void PostUpdateNPCs() => CombatHarness.OlApplyPin();
	}

	public partial class CombatHarness
	{
		private enum Ol { Spawn, Fight, Follow, Hold, HoldAway, FollowBack, HoldFire, FireAtWill, Done }
		private Ol _ol; private int _olTick, _olPass, _olFail, _olTownIdx = -1, _olTargetIdx = -1, _olShots, _olPoseTicks, _olMaxPoseTick;
		private string _olType = "ArmsDealer"; private int _olTownType;
		private Vector2 _olPost; private int _olLastProjCount;
		private int _olSoldierIdx = -1; private int _olDamage; private float _olAi2Min = 9f, _olAi2Max = -9f; private bool _olSawPaint; private int _olDefBefore = -1, _olDefPainted = -1;

		private void OlSay(string s) { Mod.Logger.Info("OFFLAB: " + s); Console.WriteLine("OFFLAB: " + s); }
		private void OlCheck(bool ok, string what) { if (ok) _olPass++; else _olFail++; OlSay((ok ? "PASS " : "FAIL ") + what); }

		private bool BeginOfficer()
		{
			_olType = Environment.GetEnvironmentVariable("LEVY_HARNESS_OFFICER") ?? "ArmsDealer";
			_olTownType = NPCID.Search.ContainsName(_olType) ? NPCID.Search.GetId(_olType) : -1;
			if (_olTownType < 0 || !OfficerRegistry.IsOfficerType(_olTownType)) { OlSay("FAIL unknown officer '" + _olType + "'"); return false; }
			_ol = Ol.Spawn; _olTick = -1;
			return true;
		}

		internal static float? OlPinTiles; internal static string OlPinName; internal static int OlPinCx, OlPinFloor;
		internal static void OlApplyPin()
		{
			if (OlPinTiles == null) return;
			Player p = Main.player[0];
			p.active = true; p.dead = false; p.name = OlPinName; p.statLifeMax = p.statLifeMax2 = 500; p.statLife = 500;
			p.position = new Vector2(OlPinCx * 16f + OlPinTiles.Value * 16f - p.width / 2f, OlPinFloor * 16f - p.height); p.velocity = Vector2.Zero;
		}

		private void OlPinCommander(float tilesFromCentre)
		{
			OlPinTiles = tilesFromCentre; OlPinName = CommanderName; OlPinCx = _cx; OlPinFloor = FloorY; OlApplyPin();
			Player p = Main.player[0];
			p.active = true; p.dead = false; p.name = CommanderName; p.statLifeMax = p.statLifeMax2 = 500; p.statLife = 500;
			p.position = new Vector2(_cx * 16f + tilesFromCentre * 16f - p.width / 2f, FloorY * 16f - p.height); p.velocity = Vector2.Zero;
			if (!Main.player[0].active) OlSay("pin readback: active is FALSE right after being set");
		}

		private NPC OlAvatarNpc()
		{
			for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && Main.npc[i].ModNPC is SoldierNPC s && s.OfficerMode && s.OfficerTownType == _olTownType) return Main.npc[i];
			return null;
		}

		private void OlSpawnTarget(float tilesRight)
		{
			if (_olTargetIdx >= 0 && Main.npc[_olTargetIdx].active) Main.npc[_olTargetIdx].active = false;
			float x = _cx * 16f + tilesRight * 16f;
			int i = NPC.NewNPC(new EntitySource_WorldEvent("LevyLab"), (int)x, (int)(FloorY * 16f - 40f), NPCID.GreenSlime);
			if (i < 0 || i >= Main.maxNPCs) { OlSay("FAIL target NewNPC"); return; }
			NPC n = Main.npc[i];
			var ht = n.GetGlobalNPC<HarnessTarget>(); ht.IsTarget = true;
			n.lifeMax = n.life = 10_000_000; n.defense = 0; n.damage = 0; n.knockBackResist = 0f; n.friendly = false; n.value = 0f; n.velocity = Vector2.Zero;
			n.Bottom = new Vector2(x, FloorY * 16f);
			ht.Pin = n.position; _olTargetIdx = i;
		}

		private void OlClearTarget() { if (_olTargetIdx >= 0 && Main.npc[_olTargetIdx].active) Main.npc[_olTargetIdx].active = false; _olTargetIdx = -1; }

		private int OlProjectiles()
		{
			int c = 0;
			for (int i = 0; i < Main.maxProjectiles; i++) { Projectile p = Main.projectile[i]; if (p.active && p.friendly && p.npcProj) c++; }
			return c;
		}

		private void StepOfficer()
		{
			if (_ol == Ol.Done) { OlPinTiles = null; Finish($"officer scenario complete pass={_olPass} fail={_olFail}"); return; }
			Player cmd = Main.player[0];
			_olTick++;
			NPC av = OlAvatarNpc();
			SoldierNPC avatar = av?.ModNPC as SoldierNPC;
			NPC town = _olTownIdx >= 0 && Main.npc[_olTownIdx].active ? Main.npc[_olTownIdx] : null;
			var reg = OfficerRegistry.Get(_olTownType);

			switch (_ol)
			{
				case Ol.Spawn:
				{
					OlPinCommander(0);
					if (_olTick == 0)
					{
						ClearAll();
						int i = NPC.NewNPC(new EntitySource_WorldEvent("LevyLab"), (int)(_cx * 16f - 80f), (int)(FloorY * 16f - 40f), _olTownType);
						if (i < 0 || i >= Main.maxNPCs) { OlSay("FAIL town NPC spawn"); _ol = Ol.Done; return; }
						_olTownIdx = i;
						NPC t = Main.npc[i];
						var g = OfficerSystem.Data(t);
						g.Enrolled = true; g.Commander = CommanderName; g.Slot = 0; g.DaysUnpaid = 0; g.ResetRuntime();
						t.netAlways = true; t.netUpdate = true;
						typeof(OfficerSystem).GetField("_stamp", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(OfficerSystem.Instance, uint.MaxValue);
						OlSay($"commissioned {_olType} (npc {t.type}) AttackType={NPCID.Sets.AttackType[t.type]} AttackTime={NPCID.Sets.AttackTime[t.type]} heldWeapon={(reg.HeldWeapon != null ? reg.HeldWeapon.Item.ToString() : "none")}");
					}
					if (_olTick == 60)
					{
						{ NPC tn = _olTownIdx >= 0 ? Main.npc[_olTownIdx] : null; var gg = tn != null ? OfficerSystem.Data(tn) : null;
						  OlSay($"debug: avatarLife={av?.life} avatarPos={(av != null ? av.Center / 16f : default)} townActive={tn?.active} townNPC={tn?.townNPC} type={tn?.type} enrolled={gg?.Enrolled} cmd='{gg?.Commander}' findPlayer={LevyUtil.FindPlayerByName(gg?.Commander)} suspended={(gg != null && OfficerSystem.IsSuspended(gg))} registry={OfficerRegistry.IsOfficerType(tn?.type ?? 0)} p0active={Main.player[0].active} p0name={Main.player[0].name} maxPlayers={Main.maxPlayers} netMode={Main.netMode} avatars={OfficerSystem.Instance.Enrolled.Count}"); }
						OlCheck(avatar != null, "an avatar is fielded for the commissioned town NPC");
						if (avatar == null) { _ol = Ol.Done; return; }
						OlCheck(avatar.Record != null && avatar.Record.Order == SoldierOrder.Follow, "the new officer starts under the Follow order");
						_ol = Ol.Fight; _olTick = -1;
					}
					return;
				}
				case Ol.Fight:
				{
					OlPinCommander(0);
					if (avatar == null || town == null) { OlCheck(false, $"avatar / town NPC vanished during the fight (tick {_olTick} avatar={(avatar != null)} town={(town != null)} enrolled={(town != null && OfficerSystem.Data(town)?.Enrolled == true)} townPos={(town != null ? town.Center / 16f : default)} floor={FloorY} cx={_cx})"); _ol = Ol.Done; return; }
					if (_olTick == 0)
					{
						avatar.NPC.Bottom = new Vector2(_cx * 16f - 60f, FloorY * 16f); avatar.NPC.velocity = Vector2.Zero;
						OlSpawnTarget(9f);
						if (_olTargetIdx >= 0) _olDefBefore = Main.npc[_olTargetIdx].defense;
						if (_olType == "Guide" || _olType == "Demolitionist")
						{
							var rec = MakeRecord("Pvt. Lab"); rec.LoadoutLocked = true; rec.Order = SoldierOrder.Follow;
							var w = new Item(); w.SetDefaults(ItemID.Handgun); rec.Gear.Weapon = w;
							var am = new Item(); am.SetDefaults(ItemID.MusketBall); am.stack = 999; rec.Gear.Ammo = am;
							RosterSystem.Instance.Regulars.Add(rec);
							_olSoldierIdx = RosterSystem.Instance.SpawnBody(rec, new Vector2(_cx * 16f - 56f * 16f, FloorY * 16f));
						}
					}
					int at = NPCID.Sets.AttackType[town.type];
					if (town.ai[0] >= 10f) { _olPoseTicks++; _olAi2Min = Math.Min(_olAi2Min, town.ai[2]); _olAi2Max = Math.Max(_olAi2Max, town.ai[2]); }
					int proj = OlProjectiles();
					if (proj > _olLastProjCount) _olShots += proj - _olLastProjCount;
					_olLastProjCount = proj;
					if (_olTargetIdx >= 0 && Main.npc[_olTargetIdx].active) _olDamage = Math.Max(_olDamage, 10_000_000 - Main.npc[_olTargetIdx].life);
					if (_olTargetIdx >= 0 && Main.npc[_olTargetIdx].active && PaintGlobalNPC.IsPainted(Main.npc[_olTargetIdx])) _olSawPaint = true;
					if (_olSawPaint && _olDefPainted < 0 && _olTargetIdx >= 0)
					{
						// the same intake the strike uses: ModifyIncomingHit lowers Defense.Flat for a painted enemy
						var mods = Main.npc[_olTargetIdx].GetIncomingStrikeModifiers(DamageClass.Generic, 1);
						_olDefPainted = (int)mods.Defense.Flat;
					}
					if (_olTick == 360)
					{
						float expectAi0 = at == 0 ? 10f : at == 1 ? 12f : at == 2 ? 14f : 15f;
						OlSay($"fight: shots={_olShots} poseTicks={_olPoseTicks} ai2 range=[{_olAi2Min:0.00},{_olAi2Max:0.00}] avatarX={(avatar.NPC.Center.X - _cx * 16f) / 16f:0.0}t");
						bool armed = reg.Weapon != null || reg.HeldWeapon != null;
						if (!armed) OlSay("this officer carries no weapon of his own (kit only): the fight checks are skipped");
						if (armed) OlCheck(_olShots > 0 || _olDamage > 0, $"the officer fired and hit the target (projectiles counted {_olShots}, damage dealt {_olDamage})");
						if (armed) OlCheck(_olPoseTicks > 0, $"the parked town NPC was driven into the stock attack pose (ai[0]={expectAi0}) for {_olPoseTicks} ticks, so vanilla draws the weapon");
						if (at == 1) OlCheck(Math.Abs(_olAi2Max) <= 0.5001f && Math.Abs(_olAi2Min) <= 0.5001f, "gun aim (ai[2]) stays inside vanilla's -0.5..0.5 shooting range");
						int held = reg.HeldWeapon?.Item ?? 0;
						OlSay($"draw state: vanilla gun draw={(at == 1 && expectAi0 == 12f)}; OfficerGlobalNPC.DrawTownAttackGun swaps in item {held} ({(held > 0 ? Lang.GetItemNameValue(held) : "stock gun")})");
						OlCheck(town.dontTakeDamage && town.noGravity, "the town NPC is parked (puppeted), the avatar is the body");
						if (_olType == "Painter")
						{
							OlCheck(_olSawPaint, "Painter: the paintball painted the enemy (Paint-Soaked buff present)");
							OlCheck(_olDefPainted <= -(int)LevyConstants.Officer.PaintDefenseLoss + 0, $"Painter: painted enemy takes hits at Defense.Flat {_olDefPainted} (Ichor-style defense loss {LevyConstants.Officer.PaintDefenseLoss})");
							OlCheck(Math.Abs(PaintGlobalNPC.DamageMult(Main.npc[_olTargetIdx]) - (1f - LevyConstants.Officer.PaintDamageCut)) < 1e-4f, "Painter: painted enemy deals " + (int)(LevyConstants.Officer.PaintDamageCut * 100) + "% less damage");
						}
						if (_olType == "Guide" && _olSoldierIdx >= 0 && Main.npc[_olSoldierIdx].ModNPC is SoldierNPC vs)
						{
							vs.NPC.Center = avatar.NPC.Center + new Vector2(-30f, 0f); vs.State = SoldierState.Engage; vs.AttackCooldown = 15;
							int called = GuideKit.CallVolley(avatar.NPC.Center);
							OlCheck(called >= 1 && vs.AttackCooldown == 0, $"Guide: the called volley brought a nearly-ready soldier's shot forward at once (called {called}, cooldown now {vs.AttackCooldown})");
							vs.AttackCooldown = 200;
							OlCheck(GuideKit.CallVolley(avatar.NPC.Center) == 0 && vs.AttackCooldown == 200, "Guide: a soldier whose weapon is far from ready is left alone (a volley only pulls shots forward)");
						}
						if (_olType == "Demolitionist" && _olSoldierIdx >= 0 && Main.npc[_olSoldierIdx].ModNPC is SoldierNPC ds && _olTargetIdx >= 0)
						{
							ds.State = SoldierState.Engage; ds.TargetIndex = _olTargetIdx; ds.NPC.Center = avatar.NPC.Center + new Vector2(-30f, 0f);
							int thrown = DemolitionistKit.ArmSappers(avatar.NPC.Center);
							bool tagged = false;
							for (int k = 0; k < Main.maxProjectiles; k++) { Projectile pr = Main.projectile[k]; if (pr.active && pr.type == ProjectileID.Grenade && pr.TryGetGlobalProjectile(out SoldierProjectileTag tg) && tg.SourceNpcIndex == _olSoldierIdx) tagged = true; }
							OlCheck(thrown >= 1 && tagged, $"Demolitionist: a fighting soldier near him lobbed one of his grenades at its target (thrown {thrown}, grenade credited to the soldier {tagged})");
						}
						{ int missing = 0; for (int x = _cx - 40; x <= _cx + 40; x++) if (!Main.tile[x, FloorY].HasTile) missing++; OlSay($"floor row tiles missing after the fight: {missing} of 81"); }
						OlClearTarget();
						// ---- Follow: send the commander far; the officer must come
						_ol = Ol.Follow; _olTick = -1;
					}
					return;
				}
				case Ol.Follow:
				{
					OlPinCommander(18);
					if (avatar == null) { _ol = Ol.Done; return; }
					if (_olTick == 900)
					{
						float d = Math.Abs(avatar.NPC.Center.X - cmd.Center.X) / 16f;
						OlSay($"debug follow: state={avatar.State} order={avatar.Record.Order} hasGoal={avatar.HasMoveGoal} goal={(avatar.HasMoveGoal ? avatar.MoveGoal / 16f : default)} pos={avatar.NPC.Bottom / 16f} vel={avatar.NPC.velocity} cmd={cmd.Center / 16f}");
						if (d >= 14f) for (int ry = FloorY - 5; ry <= FloorY + 1; ry++) { var sb = new System.Text.StringBuilder(); for (int x = _cx + 14; x <= _cx + 32; x++) sb.Append(Main.tile[x, ry].HasTile ? (Main.tileSolid[Main.tile[x, ry].TileType] ? '#' : 'o') : '.'); OlSay($"tiles row {ry - FloorY,2}: {sb}"); }
						OlCheck(d < 14f, $"Follow: the officer closed up on the commander 18 tiles away (now {d:0.0} tiles)");
						_ol = Ol.Hold; _olTick = -1;
					}
					return;
				}
				case Ol.Hold:
				{
					OlPinCommander(18);
					if (avatar == null) { _ol = Ol.Done; return; }
					if (_olTick == 0)
					{
						RosterSystem.Instance.IssueOrder(cmd, SoldierOrder.Hold, atBarracks: false);
						_olPost = avatar.NPC.Bottom;
						OlCheck(avatar.Record.Order == SoldierOrder.Hold, "Hold: the order reached the officer's avatar");
					}
					if (_olTick == 20) { _ol = Ol.HoldAway; _olTick = -1; }
					return;
				}
				case Ol.HoldAway:
				{
					OlPinCommander(-18);
					if (avatar == null) { _ol = Ol.Done; return; }
					if (_olTick == 400)
					{
						float fromPost = Vector2.Distance(avatar.NPC.Bottom, _olPost) / 16f, fromCmd = Math.Abs(avatar.NPC.Center.X - cmd.Center.X) / 16f;
						OlCheck(fromPost < 6f && fromCmd > 25f, $"Hold: the commander walked 36 tiles away and the officer stayed at his post ({fromPost:0.0} tiles from it, {fromCmd:0.0} from the commander)");
						RosterSystem.Instance.IssueOrder(cmd, SoldierOrder.Follow, atBarracks: true);
						_ol = Ol.FollowBack; _olTick = -1;
					}
					return;
				}
				case Ol.FollowBack:
				{
					OlPinCommander(-18);
					if (avatar == null) { _ol = Ol.Done; return; }
					if (_olTick == 900)
					{
						float fromCmd = Math.Abs(avatar.NPC.Center.X - cmd.Center.X) / 16f;
						OlCheck(fromCmd < 14f, $"Follow again: released from Hold he rejoined the commander ({fromCmd:0.0} tiles)");
						OlSpawnTarget(-18f + 9f);
						_olLastProjCount = OlProjectiles(); _olShots = 0;
						RosterSystem.Instance.ToggleHoldFire(cmd, atBarracks: true);
						OlCheck(avatar.Record.HoldFire, "Hold Fire: the order reached the officer's avatar");
						_ol = Ol.HoldFire; _olTick = -1;
					}
					return;
				}
				case Ol.HoldFire:
				{
					OlPinCommander(-18);
					if (avatar == null) { _ol = Ol.Done; return; }
					int proj = OlProjectiles(); if (proj > _olLastProjCount) _olShots += proj - _olLastProjCount; _olLastProjCount = proj;
					if (_olTick == 240)
					{
						OlCheck(_olShots == 0 && (town == null || town.ai[0] == 0f), $"Hold Fire: an enemy 9 tiles away drew no fire ({_olShots} shots) and the weapon is down (ai[0]={town?.ai[0]})");
						RosterSystem.Instance.ToggleHoldFire(cmd, atBarracks: true);
						_olShots = 0;
						_ol = Ol.FireAtWill; _olTick = -1;
					}
					return;
				}
				case Ol.FireAtWill:
				{
					OlPinCommander(-18);
					int proj = OlProjectiles(); if (proj > _olLastProjCount) _olShots += proj - _olLastProjCount; _olLastProjCount = proj;
					if (_olTick == 240)
					{
						if (reg.Weapon != null || reg.HeldWeapon != null) OlCheck(_olShots > 0, $"Fire at will: the officer fires again ({_olShots} shots)");
						OlClearTarget();
						OlSay($"SUMMARY {_olType}: pass={_olPass} fail={_olFail}");
						_ol = Ol.Done;
					}
					return;
				}
			}
		}
	}
}
