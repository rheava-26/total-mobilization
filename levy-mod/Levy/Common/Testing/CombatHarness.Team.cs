using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Team;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// TEAM ITEMS SCENARIO (LEVY_HARNESS_SCENARIO=team). For every team effect Levy patches, a short world run in which the REAL production code does the work (the pulse, the hooks, the buff stat pass) and the scenario only sets
	/// the scene and reads the result: a commander (the harness stand-in player, equipped through the real UpdateEquips) and two Champion levies, then "the commander's item reaches the levies" and "a levy's item reaches the commander and the
	/// other levy". One HARNESS-TEAM line per case (PASS / FAIL / SKIP with the numbers) and a summary, also appended to LEVY_HARNESS_OUT.team.txt. Calamity cases SKIP when CalamityMod is not loaded.
	/// A headless server has no local player, so the harness declares the stand-in local (TeamItems.HarnessLocal) and plays the owner's client for the reactions only that client runs.
	/// </summary>
	public partial class CombatHarness
	{
		private sealed class TmCase { public string Id; public bool Cal, Tho; public Action Setup, Mid; public int Ticks = 80, MidTick = 25; public Func<string> Eval; }
		private readonly List<TmCase> _tmCases = new();
		private int _tmI = -1, _tmTick, _tmPass, _tmFail, _tmSkip;
		private bool _tmStarted;
		private SoldierNPC _tmA, _tmB;
		private string _tmInfo = "";
		private bool _tmEquipCmd;
		private int _tmTarget = -1;
		private readonly List<string> _tmLines = new();

		private void TmLog(string s)
		{
			Mod.Logger.Info(s);
			_tmLines.Add(s);
			try { System.IO.File.AppendAllText(OutPath + ".team.txt", s + "\n"); } catch (Exception) { }
		}

		private static int TmBuff(string full) => TeamItems.ModBuffType(full);
		private static int TmItem(string full) => ModContent.TryFind(full, out ModItem m) ? m.Type : 0;
		private static int TmProj(string full) => ModContent.TryFind(full, out ModProjectile m) ? m.Type : 0;
		private static Item TmItemOf(int type) { var it = new Item(); it.SetDefaults(type); return it; }

		private bool BeginTeam()
		{
			try { MonoModHooks.Add(typeof(Netplay).GetMethod("UpdateConnectedClients", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic), new HookUpdateConnectedClients(TeamStandinDetour)); _spHooked = true; } catch (Exception e) { Mod.Logger.Warn("HARNESS-TEAM: could not hook UpdateConnectedClients: " + e.Message); }
			TeamItems.HarnessLocal = true;
			TeamItems.HarnessPrePulse = TmPrePulse;
			bool cal = ModLoader.HasMod("CalamityMod");
			TmBuild();
			_tmI = 0; _tmStarted = false;
			TmLog($"HARNESS-TEAM: begin; calamity={cal}; registry: {TeamItems.BuildReport}; hooks: spectre={TeamSpectre.Installed} nebula={TeamNebula.Installed} info={TeamInfo.Installed}; cases={_tmCases.Count}");
			return true;
		}

		/// <summary>The headless server's client bookkeeping (on its own thread) flips the client-less stand-in player inactive at random moments; for the team scenario it does not run at all, so the stand-in stays a live player for the whole tick like a connected one.</summary>
		private static void TeamStandinDetour(OrigUpdateConnectedClients orig) { if (!TeamItems.HarnessLocal) orig(); else { Main.player[0].active = true; } }

		// ---------------------------------------------------------------- scene helpers
		private Player TmCmd => Main.player[0];

		private void TmPin()
		{
			Player p = TmCmd;
			p.active = true; p.dead = false; p.name = CommanderName;
			p.team = 0;
			p.statLifeMax = p.statLifeMax2 = Math.Max(p.statLifeMax, 500);
			if (p.statLife < 1) p.statLife = 1;
			p.position = new Vector2(_cx * 16f - p.width / 2f, FloorY * 16f - p.height); p.velocity = Vector2.Zero;
			TmEquipPass();
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || n.ModNPC is SoldierNPC || n.townNPC || n.friendly) continue;
				if (n.TryGetGlobalNPC(out HarnessTarget ht) && ht.IsTarget) continue;
				n.active = false;
			}
			for (int s = 0; s < 2; s++) { SoldierNPC l = s == 0 ? _tmA : _tmB; if (l != null && l.NPC.active) { l.NPC.velocity.X = 0f; if (l.Record != null) l.Record.CommanderIndex = 0; } } // the roster re-derives the index from the players online, and the stand-in blinks
		}

		/// <summary>The per-tick equip pass the commander's own Player.Update runs: reset, buffs, equips, post-equips (flags such as hasPaladinShield and Calamity's trinketOfChi are set here). Run again right before each pulse, as the real update precedes it in a tick.</summary>
		private void TmEquipPass()
		{
			if (!_tmEquipCmd || _tmRunning == false) return;
			Player p = TmCmd;
			p.ResetEffects();
			p.UpdateBuffs(0);
			p.UpdateEquips(0);
			PlayerLoader.PostUpdateEquips(p);
		}
		private bool _tmRunning = true;

		/// <summary>Right before a pulse: the stand-in is flipped inactive by the headless server's client bookkeeping between the harness's tick and the world's, so make it a live player again, as a connected one always is, and run its equip pass.</summary>
		private void TmPrePulse()
		{
			if (!_tmRunning) return;
			Player p = TmCmd;
			p.active = true; p.dead = false; p.name = CommanderName;
			TmEquipPass();
		}

		private SoldierNPC TmLevy(string name, float dxTiles, Action<SoldierGear> gear = null)
		{
			var rec = MakeRecord(name);
			rec.Grade = Grade.Champion; rec.LoadoutLocked = true; rec.Order = SoldierOrder.Follow; rec.HoldFire = true;
			gear?.Invoke(rec.Gear);
			RosterSystem.Instance.Regulars.Add(rec);
			Vector2 at = new Vector2((_cx + dxTiles) * 16f, FloorY * 16f);
			int idx = RosterSystem.Instance.SpawnBody(rec, at);
			if (idx < 0 || !rec.HasBody) throw new Exception("SpawnBody failed for " + name);
			NPC n = Main.npc[idx]; n.Bottom = at; n.velocity = Vector2.Zero;
			var s = (SoldierNPC)n.ModNPC;
			rec.CommanderIndex = 0;
			s.RefreshStats();
			s.NPC.lifeMax = rec.MaxHp; s.NPC.life = rec.MaxHp; rec.CurrentHp = rec.MaxHp; // the body pins lifeMax to the record on its first tick; start from the pinned value so a fresh levy is not 'wounded'
			return s;
		}

		private void TmScene(Action<SoldierGear> gearA = null, Action<SoldierGear> gearB = null, float dxA = 4f, float dxB = 7f, bool equipCmd = true)
		{
			ClearAll(); SpBuildFloor("run");
			TmCmd.active = true; TmCmd.dead = false; TmCmd.name = CommanderName; // before the levies spawn: they look their commander up by name
			TmCmd.statLife = TmCmd.statLifeMax = TmCmd.statLifeMax2 = 500;
			for (int i = 0; i < Player.MaxBuffs; i++) { TmCmd.buffType[i] = 0; TmCmd.buffTime[i] = 0; }
			for (int i = 3; i < 10; i++) TmCmd.armor[i] = new Item();
			TmCmd.lifeSteal = 0f; TmCmd.immune = false; TmCmd.immuneTime = 0;
			_tmEquipCmd = equipCmd;
			_tmA = TmLevy("Pvt. Team A", dxA, gearA);
			_tmB = TmLevy("Pvt. Team B", dxB, gearB);
			TeamItems.HarnessLocal = true;
			TmPin();
		}

		private static void TmAcc(SoldierGear g, int slot, int itemType) { g.Accessories[slot] = TmItemOf(itemType); }
		private static bool TmHas(SoldierNPC s, int buff) => buff > 0 && s.NPC.HasBuff(buff);
		private static readonly bool TmTrace = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LEVY_TEAM_TRACE"));
		private string TmCmdBuffs()
		{
			var l = new List<string>();
			for (int q = 0; q < Player.MaxBuffs; q++) if (TmCmd.buffType[q] > 0) l.Add(TmCmd.buffType[q] + ":" + TmCmd.buffTime[q]);
			return string.Join(",", l);
		}
		private static string TmFlag(string field, Player p) { var f = TeamItems.ModFlag("CalamityMod", "CalamityMod.CalPlayer.CalamityPlayer", field); return f == null ? "?" : f(p).ToString(); }

		// ---------------------------------------------------------------- the cases
		private void TmBuild()
		{
			int pal = ItemID.PaladinsShield;
			// 1 Paladin's Shield: the commander wears it
			_tmCases.Add(new TmCase { Id = "paladin: commander's shield -> levy (buff, 0.75 hit, third back to the commander)", Ticks = 70,
				Setup = () => { TmScene(); TmCmd.armor[3] = TmItemOf(pal); },
				Eval = () =>
				{
					if (!TmHas(_tmA, BuffID.PaladinsShield)) return "levy A has no Paladin's Shield buff (cmd hasPaladinShield=" + TmCmd.hasPaladinShield + ")";
					float scale = _tmA.IncomingSetScale();
					if (Math.Abs(scale - 0.75f) > 0.001f) return "levy hit scale " + scale + ", wanted 0.75 (cmd shield=" + TmCmd.hasPaladinShield + " immune=" + TmCmd.immune + " life=" + TmCmd.statLife + "/" + TmCmd.statLifeMax2 + " active=" + TmCmd.active + " dist=" + Vector2.Distance(TmCmd.Center, _tmA.NPC.Center) + " commanderOf=" + (global::Levy.Common.Compat.Thorium.CompatAllies.CommanderOf(_tmA) != null) + ")";
					int before = TmCmd.statLife;
					typeof(SoldierNPC).GetMethod("SetsOnHurt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(_tmA, new object[] { 90, -1 });
					int lost = before - TmCmd.statLife;
					_tmInfo = $"buff ok, hit scale {scale}, commander {before}->{TmCmd.statLife} (a hit of 90 shares 30)";
					return lost >= 1 && lost <= 31 ? null : "commander did not take the shared third (lost " + lost + "; immune=" + TmCmd.immune + " immuneTime=" + TmCmd.immuneTime + " dead=" + TmCmd.dead + " active=" + TmCmd.active + " shares=" + TeamPlayer.PaladinShares + ")";
				} });
			// 2 Paladin's Shield: a levy wears it
			_tmCases.Add(new TmCase { Id = "paladin: levy's shield -> commander and levy (buff, 0.75 hit, third taken by the levy)", Ticks = 70,
				Setup = () => TmScene(g => TmAcc(g, 0, pal)),
				Eval = () =>
				{
					if (!_tmA.WearsPaladinShield) return "levy A does not read as wearing the shield";
					if (!TmCmd.HasBuff(BuffID.PaladinsShield)) return "commander has no Paladin's Shield buff";
					if (!TmHas(_tmB, BuffID.PaladinsShield)) return "levy B has no Paladin's Shield buff";
					TmCmd.immune = false; TmCmd.immuneTime = 0;
					TmCmd.ResetEffects(); TmCmd.UpdateBuffs(0);
					int la0 = _tmA.NPC.life, c0 = TmCmd.statLife, cuts0 = TeamPlayer.PaladinCuts;
					int saveMy = Main.myPlayer; Main.myPlayer = 0;
					double dealt;
					try { dealt = TmCmd.Hurt(PlayerDeathReason.LegacyEmpty(), 100, 0); } finally { Main.myPlayer = saveMy; }
					int la1 = _tmA.NPC.life;
					_tmInfo = $"commander hit for {dealt:0} (100 raw), commander {c0}->{TmCmd.statLife}, shield levy {la0}->{la1}; cuts={TeamPlayer.PaladinCuts} shares={TeamPlayer.PaladinShares}";
					if (TeamPlayer.PaladinCuts == cuts0 || dealt >= 90) return "commander took " + dealt + " (cut hook ran: " + (TeamPlayer.PaladinCuts != cuts0) + "), wanted about 75";
					return la1 < la0 ? null : "the shield levy took no share";
				} });
			// 2b Paladin's Shield with 0 / 1 / 3 holders: who takes the third? Victim A (levy, dx 4) is hit for 100 raw, then the commander (dx 0) is hit for 100 raw. Holders stand at dx 6, 9, 12.
			foreach (int nHold in new[] { 0, 1, 3 })
			{
				int n = nHold;
				var holders = new List<SoldierNPC>();
				_tmCases.Add(new TmCase { Id = $"paladin split: {n} holder(s), levy victim and commander victim", Ticks = 70,
					Setup = () =>
					{
						TmScene(); holders.Clear();
						for (int h = 0; h < n; h++)
						{
							var hs = TmLevy("Pvt. Shield " + (h + 1), 6f + 3f * h, g => TmAcc(g, 0, pal));
							hs.Record.Order = SoldierOrder.Hold;
							holders.Add(hs);
						}
					},
					Eval = () =>
					{
						var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
						var on = typeof(SoldierNPC).GetMethod("SetsOnHurt", bf);
						// levy victim: the real code path (the scale, then the share after the hit lands)
						int[] h0 = holders.Select(h => h.NPC.life).ToArray(); int a0 = _tmA.NPC.life;
						float scale = _tmA.IncomingSetScale();
						int taken = (int)(100 * scale);
						on.Invoke(_tmA, new object[] { taken, -1 });
						string lv = string.Join("/", holders.Select((h, i) => h0[i] - h.NPC.life));
						int sharers = holders.Where((h, i) => h0[i] - h.NPC.life > 0).Count();
						// commander victim (a holder that just paid is in immune frames and is skipped, as in vanilla; clear them so each stage is measured clean)
						foreach (var h in holders) h.ImmuneTime = 0;
						TmCmd.immune = false; TmCmd.immuneTime = 0; TmCmd.ResetEffects(); TmCmd.UpdateBuffs(0);
						int[] h1 = holders.Select(h => h.NPC.life).ToArray(); int c0 = TmCmd.statLife;
						int saveMy = Main.myPlayer; Main.myPlayer = 0; double dealt;
						try { dealt = TmCmd.Hurt(PlayerDeathReason.LegacyEmpty(), 100, 0); } finally { Main.myPlayer = saveMy; }
						string cv = string.Join("/", holders.Select((h, i) => h1[i] - h.NPC.life));
						int csharers = holders.Where((h, i) => h1[i] - h.NPC.life > 0).Count();
						// burst: a second hit on the commander right away, the payer still in immune frames
						TmCmd.immune = false; TmCmd.immuneTime = 0; TmCmd.ResetEffects(); TmCmd.UpdateBuffs(0);
						int[] h2 = holders.Select(h => h.NPC.life).ToArray(); double dealt2;
						Main.myPlayer = 0;
						try { dealt2 = TmCmd.Hurt(PlayerDeathReason.LegacyEmpty(), 100, 0); } finally { Main.myPlayer = saveMy; }
						string bv = string.Join("/", holders.Select((h, i) => h2[i] - h.NPC.life));
						_tmInfo = $"LEVY victim: scale {scale:0.00}, victim takes {taken} of 100, holders lose [{lv}] ({sharers} paid); COMMANDER victim: takes {dealt:0} of 100, holders lose [{cv}] ({csharers} paid); BURST 2nd commander hit: takes {dealt2:0}, holders lose [{bv}]";
						if (n == 0) return Math.Abs(scale - 1f) < 0.001f && dealt >= 99 && sharers == 0 && csharers == 0 ? null : "with no holder nothing should be cut or shared";
						if (Math.Abs(scale - 0.75f) > 0.001f) return "levy victim not cut to 0.75";
						if (dealt > 80) return "commander victim not cut";
						return sharers == 1 && csharers == 1 ? null : $"expected exactly one holder to pay each time, got {sharers} / {csharers}";
					} });
			}
			// 3 Calamity: Rampart of Deities both ways
			int rampart = TmItem("CalamityMod/RampartofDeities");
			_tmCases.Add(new TmCase { Id = "calamity rampart of deities: commander -> levy shield buff", Cal = true, Ticks = 70,
				Setup = () => { TmScene(); TmCmd.armor[3] = TmItemOf(rampart); },
				Eval = () => { _tmInfo = "cmd hasPaladinShield=" + TmCmd.hasPaladinShield; return TmHas(_tmA, BuffID.PaladinsShield) ? null : "levy A has no Paladin's Shield buff"; } });
			_tmCases.Add(new TmCase { Id = "calamity rampart of deities: levy -> commander shield buff", Cal = true, Ticks = 70,
				Setup = () => TmScene(g => TmAcc(g, 0, rampart)),
				Eval = () => { _tmInfo = "levy hasPaladinShield=" + _tmA.Dummy.hasPaladinShield; return TmCmd.HasBuff(BuffID.PaladinsShield) && TmHas(_tmB, BuffID.PaladinsShield) ? null : "commander " + TmCmd.HasBuff(BuffID.PaladinsShield) + " / levy B " + TmHas(_tmB, BuffID.PaladinsShield); } });
			// 4 Calamity: Trinket of Chi and Affliction
			int chiItem = TmItem("CalamityMod/TrinketofChi"), chiBuff = TmBuff("CalamityMod/ChiRegenBuff");
			_tmCases.Add(new TmCase { Id = "calamity trinket of chi: commander -> levy (buff, regen units)", Cal = true, Ticks = 90,
				Setup = () => { TmScene(); TmCmd.armor[3] = TmItemOf(chiItem); },
				Eval = () =>
				{
					if (!TmHas(_tmA, chiBuff)) return "levy A has no ChiRegenBuff";
					int units = _tmA.BuffLifeRegenUnits;
					_tmInfo = $"levy chiRegen flag={TmFlag("chiRegen", _tmA.Dummy)} regen units={units} (Calamity RegenBoost 1)";
					return units >= 1 ? null : "levy regen units " + units;
				} });
			_tmCases.Add(new TmCase { Id = "calamity trinket of chi: levy -> commander and levy", Cal = true, Ticks = 90,
				Setup = () => TmScene(g => TmAcc(g, 0, chiItem)),
				Eval = () => { _tmInfo = "levy trinketOfChi=" + TmFlag("trinketOfChi", _tmA.Dummy) + " cmd chiRegen=" + TmFlag("chiRegen", TmCmd); return TmCmd.HasBuff(chiBuff) && TmHas(_tmB, chiBuff) ? null : "commander " + TmCmd.HasBuff(chiBuff) + " / levy B " + TmHas(_tmB, chiBuff); } });
			int affItem = TmItem("CalamityMod/Affliction"), affBuff = TmBuff("CalamityMod/Afflicted");
			_tmCases.Add(new TmCase { Id = "calamity affliction: commander -> levy (buff, regen)", Cal = true, Ticks = 90,
				Setup = () => { TmScene(); TmCmd.armor[3] = TmItemOf(affItem); },
				Eval = () => { _tmInfo = $"afflicted flag={TmFlag("afflicted", _tmA.Dummy)} regen units={_tmA.BuffLifeRegenUnits}"; return TmHas(_tmA, affBuff) && _tmA.BuffLifeRegenUnits >= 1 ? null : "buff " + TmHas(_tmA, affBuff) + " units " + _tmA.BuffLifeRegenUnits; } });
			_tmCases.Add(new TmCase { Id = "calamity affliction: levy -> commander and levy", Cal = true, Ticks = 90,
				Setup = () => TmScene(g => TmAcc(g, 0, affItem)),
				Eval = () => TmCmd.HasBuff(affBuff) && TmHas(_tmB, affBuff) ? null : "commander " + TmCmd.HasBuff(affBuff) + " / levy B " + TmHas(_tmB, affBuff) });
			// 5 Calamity healing auras (projectiles)
			foreach (var (proj, buff, cleanse, far) in new[] { ("AbsorberAura", "AbsorberRegen", true, 350f), ("PinkJellyAura", "PinkJellyRegen", false, 190f), ("GreenJellyAura", "GreenJellyRegen", false, 270f), ("BlueJellyAura", null, true, 190f) })
			{
				string pn = proj, bn = buff; bool cl = cleanse; float farPx = far;
				_tmCases.Add(new TmCase { Id = "calamity " + pn + ": levies in the circle", Cal = true, Ticks = 40,
					Setup = () =>
					{
						TmScene(null, null, 2f, 2f + (farPx + 40f) / 16f);
						_tmA.NPC.AddBuff(BuffID.OnFire, 600);
						int pt = TmProj("CalamityMod/" + pn);
						int i = Projectile.NewProjectile(new EntitySource_WorldEvent("LevyHarness"), _tmA.NPC.Center, Vector2.Zero, pt, 0, 0f, 0);
						Main.projectile[i].timeLeft = 400;
					},
					Eval = () =>
					{
						int bt = bn == null ? 0 : TmBuff("CalamityMod/" + bn);
						if (bt > 0 && !TmHas(_tmA, bt)) return "levy A in the circle has no " + bn;
						if (bt > 0 && TmHas(_tmB, bt)) return "levy B outside the circle has " + bn;
						if (cl && _tmA.NPC.HasBuff(BuffID.OnFire)) return "levy A still on fire (not cleansed)";
						_tmInfo = $"A buff={(bt > 0 ? TmHas(_tmA, bt).ToString() : "-")} B outside={(bt > 0 ? TmHas(_tmB, bt).ToString() : "-")} OnFire cleansed={!_tmA.NPC.HasBuff(BuffID.OnFire)} regen units A={_tmA.BuffLifeRegenUnits}";
						return null;
					} });
			}
			_tmCases.Add(new TmCase { Id = "calamity DeathValleyDuster / Primordial Earth / Ancient: sand and earth wind buffs", Cal = true, Ticks = 40,
				Setup = () =>
				{
					TmScene();
					foreach (string pn in new[] { "DeathValleyDusterProjectile", "PrimordialEarthProjectile", "PrimordialAncientProjectile" })
					{
						int pt = TmProj("CalamityMod/" + pn);
						int i = Projectile.NewProjectile(new EntitySource_WorldEvent("LevyHarness"), _tmA.NPC.Center, Vector2.Zero, pt, 0, 0f, 0);
						Projectile p = Main.projectile[i]; p.ai[2] = 1f; p.timeLeft = 400; p.width = p.height = 200;
					}
				},
				Eval = () =>
				{
					bool sw = TmHas(_tmA, TmBuff("CalamityMod/SandsWindBuff")), ae = TmHas(_tmA, TmBuff("CalamityMod/AeolianEarthBuff"));
					_tmInfo = $"SandsWind={sw} AeolianEarth={ae}";
					return sw && ae ? null : "missing buff";
				} });
			// 6 Calamity: Mycoroot, Blazing Star, Corpus Avertor
			_tmCases.Add(new TmCase { Id = "calamity mycoroot: throw buffs the levies within 800 px (Mushy)", Cal = true, Ticks = 20,
				Setup = () =>
				{
					TmScene();
					Item m = TmItemOf(TmItem("CalamityMod/Mycoroot"));
					ModContent.GetInstance<TeamAuraItem>().Shoot(m, TmCmd, null, TmCmd.Center, Vector2.UnitX, ProjectileID.WoodenArrowFriendly, 10, 1f);
				},
				Eval = () => TmHas(_tmA, TmBuff("CalamityMod/Mushy")) ? null : "levy A has no Mushy" });
			_tmCases.Add(new TmCase { Id = "calamity blazing star heart: heals a wounded levy it reaches", Cal = true, Ticks = 30,
				Setup = () =>
				{
					TmScene(); _tmA.NPC.life = 100; _tmInfo = "";
					int i = Projectile.NewProjectile(new EntitySource_WorldEvent("LevyHarness"), _tmA.NPC.Center, Vector2.Zero, TmProj("CalamityMod/BlazingStarHeal"), 0, 0f, 0);
					Main.projectile[i].timeLeft = 150; Main.projectile[i].scale = 1f;
				},
				Eval = () => { _tmInfo = "levy A 100 -> " + _tmA.NPC.life; return _tmA.NPC.life > 100 ? null : "no heal"; } });
			_tmCases.Add(new TmCase { Id = "calamity corpus avertor stealth strike: avertor bonus to the levies", Cal = true, Ticks = 20,
				Setup = () =>
				{
					TmScene();
					int i = Projectile.NewProjectile(new EntitySource_WorldEvent("LevyHarness"), TmCmd.Center, Vector2.Zero, TmProj("CalamityMod/CorpusAvertorStealth"), 0, 0f, 0);
					Projectile p = Main.projectile[i]; p.ai[1] = 0f;
					var tgt = new NPC(); tgt.SetDefaults(NPCID.GreenSlime);
					p.GetGlobalProjectile<TeamAuraProjectile>().OnHitNPC(p, tgt, default, 10);
				},
				Eval = () => TmHas(_tmA, TmBuff("CalamityMod/AvertorBonus")) ? null : "levy A has no AvertorBonus" });
			// 7 Spectre
			_tmCases.Add(new TmCase { Id = "spectre: the commander's heal goes to a levy who has lost more life than he has", Ticks = 120,
				Setup = () =>
				{
					TmScene(null, null, 6f, 9f, false);
					_tmA.NPC.life = 150; TmCmd.statLife = TmCmd.statLifeMax2; TmCmd.lifeSteal = 200f;
					int i = Projectile.NewProjectile(new EntitySource_WorldEvent("LevyHarness"), TmCmd.Center, Vector2.Zero, ProjectileID.MagicMissile, 10, 0f, 0);
					Projectile p = Main.projectile[i]; p.DamageType = DamageClass.Magic; p.numHits = 0;
					TeamSpectre.RedirectedBolts = 0;
					TmCmd.active = true;
					p.ghostHeal(600, _tmA.NPC.Center - new Vector2(60f, 0f), null);
					_tmInfo = "";
				},
				Eval = () => { int heals = 0; for (int q = 0; q < Main.maxProjectiles; q++) if (Main.projectile[q].active && Main.projectile[q].ModProjectile is global::Levy.Common.Compat.Thorium.SoldierHeal) heals++; _tmInfo = $"live heal bolts={heals}; levy A 150 -> {_tmA.NPC.life} (max {_tmA.NPC.lifeMax}); bolts redirected={TeamSpectre.RedirectedBolts}; lifeSteal pool 200 -> {TmCmd.lifeSteal:0.0}"; return TeamSpectre.RedirectedBolts == 1 && _tmA.NPC.life > 150 ? null : "no heal reached the levy"; } });
			_tmCases.Add(new TmCase { Id = "spectre: a player more wounded than every levy keeps the vanilla pick", Ticks = 30,
				Setup = () =>
				{
					TmScene(null, null, 6f, 9f, false);
					_tmA.NPC.life = _tmA.NPC.lifeMax - 20; TmCmd.statLife = 100; TmCmd.lifeSteal = 200f;
					int i = Projectile.NewProjectile(new EntitySource_WorldEvent("LevyHarness"), TmCmd.Center, Vector2.Zero, ProjectileID.MagicMissile, 10, 0f, 0);
					Projectile p = Main.projectile[i]; p.DamageType = DamageClass.Magic; p.numHits = 0;
					TeamSpectre.RedirectedBolts = 0;
					TmCmd.active = true;
					p.ghostHeal(600, TmCmd.Center, null);
				},
				Eval = () => { _tmInfo = "redirected=" + TeamSpectre.RedirectedBolts; return TeamSpectre.RedirectedBolts == 0 ? null : "the hook stole the heal from a more wounded player"; } });
			int hood = ItemID.SpectreHood, robe = ItemID.SpectreRobe, pants = ItemID.SpectrePants;
			_tmCases.Add(new TmCase { Id = "spectre set on a levy: the heal reaches the wounded commander", Ticks = 150,
				Setup = () =>
				{
					TmScene(g => { g.Head = TmItemOf(hood); g.Body = TmItemOf(robe); g.Legs = TmItemOf(pants); }, null, 4f, 7f, false);
					_tmA.RefreshStats();
					TmCmd.statLife = 150;
					_tmTarget = SpSpawnTarget((_cx + 6) * 16f, 40, 40, 0, NPCID.GreenSlime);
					TeamSpectre.LevyBolts = 0; TeamSpectre.LevyDelivered = 0;
				},
				Mid = () => TmHoodHit(),
				Eval = () => { _tmInfo = $"wearer hood heal flag={_tmA.Dummy.ghostHeal}; commander 150 -> {TmCmd.statLife}; bolts={TeamSpectre.LevyBolts} delivered={TeamSpectre.LevyDelivered}"; return TmCmd.statLife > 150 ? null : "commander not healed"; } });
			_tmCases.Add(new TmCase { Id = "spectre set on a levy: the heal reaches a wounded squadmate", Ticks = 150,
				Setup = () =>
				{
					TmScene(g => { g.Head = TmItemOf(hood); g.Body = TmItemOf(robe); g.Legs = TmItemOf(pants); }, null, 4f, 7f, false);
					_tmA.RefreshStats();
					_tmB.NPC.life = 120;
					_tmTarget = SpSpawnTarget((_cx + 6) * 16f, 40, 40, 0, NPCID.GreenSlime);
					TeamSpectre.LevyBolts = 0; TeamSpectre.LevyDelivered = 0;
				},
				Mid = () => TmHoodHit(),
				Eval = () => { _tmInfo = $"squadmate 120 -> {_tmB.NPC.life}; bolts={TeamSpectre.LevyBolts} delivered={TeamSpectre.LevyDelivered}"; return _tmB.NPC.life > 120 ? null : "squadmate not healed"; } });
			// 8 Nebula
			_tmCases.Add(new TmCase { Id = "nebula: a booster the commander takes reaches the levies within 700 px", Ticks = 20,
				Setup = () =>
				{
					TmScene(null, null, 4f, 60f, false);
					int save = Main.myPlayer; Main.myPlayer = 0;
					try { TmCmd.NebulaLevelup(BuffID.NebulaUpLife1); } finally { Main.myPlayer = save; }
				},
				Eval = () => { _tmInfo = $"near levy life booster level={_tmA.NebulaLevelOf(0)}; far levy (60 tiles)={_tmB.NebulaLevelOf(0)}; shared={TeamNebula.Shared}"; return _tmA.NebulaLevelOf(0) == 1 && _tmB.NebulaLevelOf(0) == 0 ? null : "near/far wrong"; } });
			_tmCases.Add(new TmCase { Id = "nebula set on a levy: his booster reaches the commander and the squadmate", Ticks = 20,
				Setup = () =>
				{
					TmScene(g => { g.Head = TmItemOf(ItemID.NebulaHelmet); g.Body = TmItemOf(ItemID.NebulaBreastplate); g.Legs = TmItemOf(ItemID.NebulaLeggings); }, null, 4f, 7f, false);
					_tmA.RefreshStats(); TmCmd.active = true;
					typeof(SoldierNPC).GetMethod("NebulaBoosterArrived", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(_tmA, new object[] { 1 });
				},
				Eval = () => { _tmInfo = $"wearer setNebula={_tmA.Dummy.setNebula} own damage level={_tmA.NebulaLevelOf(1)}; commander damage booster={TmCmd.HasBuff(BuffID.NebulaUpDmg1)} (cmd buffs {TmCmdBuffs()}); squadmate level={_tmB.NebulaLevelOf(1)}"; return _tmA.NebulaLevelOf(1) == 1 && TmCmd.HasBuff(BuffID.NebulaUpDmg1) && _tmB.NebulaLevelOf(1) == 1 ? null : "booster not shared"; } });
			// 8b Thorium: Aura Defense staff and Dream Weaver's Mask set
			_tmCases.Add(new TmCase { Id = "thorium aura defense: levies in the circle get the damage reduction", Tho = true, Ticks = 60,
				Setup = () =>
				{
					TmScene(null, null, 2f, 40f);
					int i = Projectile.NewProjectile(new EntitySource_WorldEvent("LevyHarness"), _tmA.NPC.Center, Vector2.Zero, TmProj("ThoriumMod/AuraDefense"), 0, 0f, 0);
					Main.projectile[i].timeLeft = 400;
				},
				Eval = () =>
				{
					bool a = TmHas(_tmA, TmBuff("ThoriumMod/AuraDamageReduction")), b = TmHas(_tmB, TmBuff("ThoriumMod/AuraDamageReduction"));
					_tmInfo = $"A buff={a} endurance={_tmA.Endurance:0.00} (Thorium 12%); B outside={b}";
					return a && !b && _tmA.Endurance >= 0.119f ? null : "aura did not land";
				} });
			_tmCases.Add(new TmCase { Id = "thorium dream weaver's mask set: the levies get distorted time", Tho = true, Ticks = 40,
				Setup = () =>
				{
					TmScene();
					Projectile.NewProjectile(new EntitySource_WorldEvent("LevyHarness"), TmCmd.Center, Vector2.Zero, TmProj("ThoriumMod/DreamWeaversMaskEffectPro"), 0, 0f, 0);
				},
				Eval = () =>
				{
					bool a = TmHas(_tmA, TmBuff("ThoriumMod/DistortedTimeBoost"));
					_tmInfo = $"A buff={a} move speed x{_tmA.MoveSpeedMult:0.00} (Thorium +0.2)";
					return a && _tmA.MoveSpeedMult >= 1.19f ? null : "no distorted time";
				} });
			// 9 Info accessories
			_tmCases.Add(new TmCase { Id = "info accessories: a levy's Compass shows on the commander's readout", Ticks = 5,
				Setup = () => TmScene(g => TmAcc(g, 0, ItemID.Compass), null, 4f, 7f, false),
				Eval = () =>
				{
					TmCmd.accCompass = 0;
					TeamInfo.Gather(TmCmd);
					foreach (Item it in TeamInfoItems()) TmCmd.RefreshInfoAccsFromItemType(it);
					_tmInfo = "commander accCompass=" + TmCmd.accCompass;
					return TmCmd.accCompass == 1 ? null : "compass not shared";
				} });
			TmBuild2();
		}

		private static Func<Player, bool> TmThFlag(string field) => TeamItems.ModFlag("ThoriumMod", "ThoriumMod.ThoriumPlayer", field);

		/// <summary>Second batch: the effects added after the first survey pass (Calamity's Whitewater and Relic of Convergence; Thorium's aura projectiles, scythes, bard accessories, Ring of Unity, life recovery).</summary>
		private void TmBuild2()
		{
			// Calamity: Whitewater's vortex heals the owner's levies (1 HP / 20 ticks, 600 ticks for an ally)
			_tmCases.Add(new TmCase { Id = "calamity whitewater vortex: levies inside it heal over time", Cal = true, Ticks = 150,
				Setup = () =>
				{
					TmScene(null, null, 2f, 40f, false);
					_tmA.NPC.life = 100; _tmB.NPC.life = 100;
					TeamItems.WhitewaterStarts = 0;
					int i = Projectile.NewProjectile(new EntitySource_WorldEvent("LevyHarness"), _tmA.NPC.Center, Vector2.Zero, TmProj("CalamityMod/WhitewaterAura"), 0, 0f, 0);
					Main.projectile[i].timeLeft = 400;
				},
				Eval = () =>
				{
					int gain = _tmA.NPC.life - _tmB.NPC.life; // both levies regenerate naturally; the vortex adds 1 HP / 20 ticks (7 over 150 ticks) to the one inside it
					_tmInfo = $"A 100 -> {_tmA.NPC.life}; B outside 100 -> {_tmB.NPC.life}; vortex heal = {gain} over 150 ticks (1 HP / 20 ticks = 7); starts={TeamItems.WhitewaterStarts}";
					return gain >= 5 && gain <= 9 && TeamItems.WhitewaterStarts == 1 ? null : "heal over time wrong";
				} });
			// Calamity: Relic of Convergence heals the owner's levies within 138 px of the cursor point. The crystal's own AI needs a held channel and a live client (it shatters at once on a headless server), so the case builds the crystal,
			// puts it at the shatter tick with the cursor point on levy A and runs the production PostAI hook on it.
			_tmCases.Add(new TmCase { Id = "calamity relic of convergence: levies at the cursor are healed 1.5 x (PostAI hook driven directly)", Cal = true, Ticks = 4, MidTick = 1,
				Setup = () =>
				{
					TmScene(null, null, 3f, 40f, false);
					_tmA.NPC.life = 100; _tmB.NPC.life = 100;
					TeamItems.RelicHeals = 0;
				},
				Mid = () =>
				{
					int i = Projectile.NewProjectile(new EntitySource_WorldEvent("LevyHarness"), TmCmd.Center, Vector2.Zero, TmProj("CalamityMod/RelicOfConvergenceCrystal"), 0, 0f, 0);
					Projectile r = Main.projectile[i];
					r.ModProjectile.GetType().GetField("mousePos").SetValue(r.ModProjectile, _tmA.NPC.Center + new Vector2(40f, 0f));
					r.timeLeft = 5;
					r.GetGlobalProjectile<TeamAuraProjectile>().PostAI(r);
					r.Kill();
				},
				Eval = () =>
				{
					_tmInfo = $"A 100 -> {_tmA.NPC.life} (Relic heal 50 x 1.5 = 75, plus a little natural regeneration); B far 100 -> {_tmB.NPC.life}; heals={TeamItems.RelicHeals}";
					return _tmA.NPC.life >= 175 && _tmA.NPC.life <= 178 && _tmB.NPC.life <= 102 && TeamItems.RelicHeals == 1 ? null : "relic heal wrong";
				} });

			// Thorium: aura projectiles (continuous) - each one spawned on levy A only; B is 40 tiles away
			var auras = new (string proj, string buff)[]
			{
				("DragonZone", "DragonHeartWandBuff"), ("Nullzone", "NullZoneBuff"), ("RaPro2", "QuickRecovery"), ("SerpentsBubbleWandPro", "SerpentsBubbleWandBuff"), ("CurePro", "Cured"),
				("ZunpetPro3", "Liberated"), ("DreamCatcherPro", "DreamCatcherBuff"), ("ShroomBooster", "BloomBoost"), ("AirStrikeShieldPro", "AirStrikeShieldBuff"),
			};
			foreach (var (proj, buff) in auras)
			{
				string pj = proj, bf = buff;
				_tmCases.Add(new TmCase { Id = $"thorium {pj}: the levy inside its circle gets {bf}", Tho = true, Ticks = 40,
					Setup = () =>
					{
						TmScene(null, null, 1f, 40f, false);
						int i = Projectile.NewProjectile(new EntitySource_WorldEvent("LevyHarness"), _tmA.NPC.Center, Vector2.Zero, TmProj("ThoriumMod/" + pj), 0, 0f, 0);
						if (i >= 0 && i < Main.maxProjectiles) { Main.projectile[i].timeLeft = 300; Main.projectile[i].netUpdate = true; }
					},
					Eval = () =>
					{
						bool a = TmHas(_tmA, TmBuff("ThoriumMod/" + bf)), b = TmHas(_tmB, TmBuff("ThoriumMod/" + bf));
						_tmInfo = $"A buff={a} B outside={b}";
						return a && !b ? null : "aura did not land";
					} });
			}
			// Thorium: scythes buff the allies within 400 px of a hit (the owner skipped); a levy's swing includes the commander
			foreach (var (proj, buff) in new[] { ("FallingTwilightPro", "TwilightBoost"), ("BloodHarvestPro", "BloodBoost") })
			{
				string pj = proj, bf = buff;
				_tmCases.Add(new TmCase { Id = $"thorium {pj}: a hit buffs the levies within 400 px (and the commander when a levy swings)", Tho = true, Ticks = 20, MidTick = 5,
					Setup = () =>
					{
						TmScene(null, null, 4f, 60f, false);
						_tmTarget = SpSpawnTarget((_cx + 8) * 16f, 40, 40, 0, NPCID.GreenSlime);
					},
					Mid = () =>
					{
						int i = Projectile.NewProjectile(new EntitySource_WorldEvent("LevyHarness"), TmCmd.Center, Vector2.Zero, TmProj("ThoriumMod/" + pj), 0, 0f, 0);
						Projectile p = Main.projectile[i];
						p.GetGlobalProjectile<TeamAuraProjectile>().OnHitNPC(p, Main.npc[_tmTarget], default, 10);
						_tmInfo = "commander swing: A=" + TmHas(_tmA, TmBuff("ThoriumMod/" + bf)) + " B far=" + TmHas(_tmB, TmBuff("ThoriumMod/" + bf)) + " commander=" + TmCmd.HasBuff(TmBuff("ThoriumMod/" + bf)) + "; ";
						p.Kill();
						var j = Projectile.NewProjectile(new EntitySource_WorldEvent("LevyHarness"), TmCmd.Center, Vector2.Zero, TmProj("ThoriumMod/" + pj), 0, 0f, 0);
						Projectile q = Main.projectile[j];
						q.GetGlobalProjectile<global::Levy.Common.Globals.SoldierProjectileTag>().SoldierIndex = _tmB.NPC.whoAmI; // fired by the far levy B
						q.Center = _tmB.NPC.Center - new Vector2(50f, 0f);
						TmCmd.Center = _tmB.NPC.Center - new Vector2(100f, 0f); // the commander walks up to B
						q.GetGlobalProjectile<TeamAuraProjectile>().OnHitNPC(q, Main.npc[_tmTarget], default, 10);
					},
					Eval = () =>
					{
						bool cmd = TmCmd.HasBuff(TmBuff("ThoriumMod/" + bf));
						_tmInfo += "levy B's swing: commander=" + cmd;
						return TmHas(_tmA, TmBuff("ThoriumMod/" + bf)) && !TmHas(_tmB, TmBuff("ThoriumMod/" + bf)) && cmd ? null : "scythe buff wrong";
					} });
			}
			// Thorium: bard accessories shared by nearby bards: a levy's Subwoofer / Conductor's Baton reaches the commander
			_tmCases.Add(new TmCase { Id = "thorium bard accessories: a levy's Subwoofer and Conductor's Baton show up on the commander", Tho = true, Ticks = 10,
				Setup = () =>
				{
					int sub = TmItem("ThoriumMod/Subwoofer"), bat = TmItem("ThoriumMod/ConductorsBaton");
					TmScene(g => { TmAcc(g, 0, sub); TmAcc(g, 1, bat); }, null, 4f, 40f, true);
					_tmA.RefreshStats();
				},
				Eval = () =>
				{
					bool wearerSub = TmThFlag("accSubwoofer")(_tmA.Dummy), wearerBat = TmThFlag("accConductorStickBard")(_tmA.Dummy);
					TmEquipPass();
					bool cmdSub = TmThFlag("accSubwoofer")(TmCmd), cmdBat = TmThFlag("accConductorStickAlly")(TmCmd);
					_tmInfo = $"wearer subwoofer={wearerSub} baton={wearerBat}; commander subwoofer={cmdSub} baton-ally={cmdBat}; shared={TeamThoriumShared()}";
					return wearerSub && wearerBat && cmdSub && cmdBat ? null : "not shared";
				} });
			// Thorium: Ring of Unity counts the levies
			_tmCases.Add(new TmCase { Id = "thorium ring of unity: the commander counts his levy wearing one", Tho = true, Ticks = 10,
				Setup = () =>
				{
					int ring = TmItem("ThoriumMod/RingofUnity");
					TmScene(g => TmAcc(g, 0, ring), null, 4f, 40f, true);
					TmCmd.armor[3] = TmItemOf(ring);
					_tmA.RefreshStats();
				},
				Eval = () =>
				{
					TmEquipPass();
					float flat = TmCmd.GetDamage(DamageClass.Generic).Flat;
					TmCmd.armor[3] = new Item();
					_tmInfo = $"commander flat damage={flat} (ring 1 + 1 per levy wearer within 400 px = 2)";
					return flat >= 1.99f ? null : "levy not counted";
				} });
			// Thorium: life recovery of a buff becomes the levy's regeneration
			_tmCases.Add(new TmCase { Id = "thorium life recovery: Cured gives the levy regeneration", Tho = true, Ticks = 30,
				Setup = () =>
				{
					TmScene(null, null, 4f, 40f, false);
					TeamItems.GrantLevy(_tmA, TmBuff("ThoriumMod/Cured"), 600);
				},
				Eval = () =>
				{
					_tmInfo = $"A regen units from buffs={_tmA.BuffLifeRegenUnits} (Cured lifeRecovery 2 = 4 units)";
					return _tmA.BuffLifeRegenUnits >= 4 && _tmB.BuffLifeRegenUnits < 4 ? null : "no regeneration";
				} });
		}

		private static string TeamThoriumShared() => ModLoader.HasMod("ThoriumMod") ? TeamThoriumSharedImpl() : "-";
		private static string TeamThoriumSharedImpl() => TeamThorium.SharedFlags.ToString();

		/// <summary>The hood wearer lands a 400-damage magic hit on the pinned target: the production SpectreHealOnHit runs.</summary>
		private void TmHoodHit()
		{
			int i = Projectile.NewProjectile(new EntitySource_WorldEvent("LevyHarness"), Main.npc[_tmTarget].Center, Vector2.Zero, ProjectileID.MagicMissile, 10, 0f, 0);
			Projectile p = Main.projectile[i]; p.DamageType = DamageClass.Magic; p.friendly = true; p.hostile = false; p.numHits = 0;
			_tmA.SpectreHealOnHit(p, Main.npc[_tmTarget], 400);
			p.active = false;
		}

		private static string TmDump(SoldierNPC s)
		{
			if (s == null || !s.NPC.active) return "gone";
			var l = new List<string>();
			for (int i = 0; i < NPC.maxBuffs; i++) if (s.NPC.buffType[i] > 0) l.Add(s.NPC.buffType[i] + ":" + s.NPC.buffTime[i]);
			return "[" + string.Join(",", l) + "] immune43=" + s.NPC.buffImmune[BuffID.PaladinsShield];
		}

		private static IEnumerable<Item> TeamInfoItems()
		{
			var f = typeof(TeamInfo).GetField("_items", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
			return (IEnumerable<Item>)f.GetValue(null);
		}

		// ---------------------------------------------------------------- driver
		private void StepTeam()
		{
			if (_tmI >= _tmCases.Count)
			{
				TmLog($"HARNESS-TEAM: SUMMARY pass={_tmPass} fail={_tmFail} skip={_tmSkip} of {_tmCases.Count}");
				TeamItems.HarnessLocal = false; TeamItems.HarnessPrePulse = null; _tmRunning = false;
				Finish("team scenario complete");
				return;
			}
			TmCase c = _tmCases[_tmI];
			if (!_tmStarted)
			{
				if (c.Cal && !ModLoader.HasMod("CalamityMod")) { TmLog($"HARNESS-TEAM: SKIP  {c.Id} (CalamityMod not loaded)"); _tmSkip++; _tmI++; return; }
				if (c.Tho && !ModLoader.HasMod("ThoriumMod")) { TmLog($"HARNESS-TEAM: SKIP  {c.Id} (ThoriumMod not loaded)"); _tmSkip++; _tmI++; return; }
				try { TmCmd.active = true; _tmInfo = ""; c.Setup(); }
				catch (Exception e) { TmLog($"HARNESS-TEAM: FAIL  {c.Id}: setup threw {e.GetType().Name}: {e.Message}"); _tmFail++; _tmI++; _tmStarted = false; return; }
				_tmStarted = true; _tmTick = 0;
				return;
			}
			TmPin();
			if (TmTrace && _tmTick % 4 == 0) TmLog($"HARNESS-TEAM-TRACE: {c.Id.Substring(0, Math.Min(24, c.Id.Length))} t={_tmTick} gc={Main.GameUpdateCount} A={TmDump(_tmA)} B={TmDump(_tmB)} cmd={TmCmdBuffs()} grantCalls={TeamItems.GrantCalls} pulseGcs[{TeamItems.PulseGcLog}] pulse[{TeamItems.LastPulse}]");
			if (_tmTick == c.MidTick && c.Mid != null)
			{
				try { TmCmd.active = true; c.Mid(); } catch (Exception e) { TmLog($"HARNESS-TEAM: note  {c.Id}: mid step threw {e.GetType().Name}: {e.Message}"); }
			}
			if (++_tmTick < c.Ticks) return;
			string verdict;
			TmCmd.active = true; TmCmd.dead = false; // the headless server's client bookkeeping flips the stand-in inactive between ticks; a connected player's slot stays live
			try { verdict = c.Eval(); }
			catch (Exception e) { verdict = "eval threw " + e.GetType().Name + ": " + e.Message; }
			if (verdict != null) _tmInfo += $" [diag: pulses={TeamItems.Pulses} grants={TeamItems.TotalGrants} levies(cmd)={TeamItems.Levies(TmCmd).Count} A buffs={TmDump(_tmA)} B buffs={TmDump(_tmB)}]";
			if (verdict == null) { _tmPass++; TmLog($"HARNESS-TEAM: PASS  {c.Id}  | {_tmInfo}"); }
			else { _tmFail++; TmLog($"HARNESS-TEAM: FAIL  {c.Id}: {verdict}  | {_tmInfo}"); }
			_tmI++; _tmStarted = false;
		}
	}
}
