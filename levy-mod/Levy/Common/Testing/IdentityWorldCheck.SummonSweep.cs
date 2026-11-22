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
using Levy.Common.Weapons;
using Levy.Content.NPCs;
using Levy.Content.Projectiles;

namespace Levy.Common.Testing
{
	public partial class IdentityWorldCheck
	{
		// ---------------------------------------------------------------------------------------------- SUMMON SWEEP: summon staves in a summoner levy's hands, the commander 64 tiles away
		// Logged as "[SummonSweep]" lines with its own summary. One levy in summoner gear (6 minion slots, the levy cap) holds its ground 12 tiles from a line of four pinned enemies; the stand-in commander stands 64 tiles LEFT
		// of it with nothing in her hands. For each staff, over the window: how many of its minions / towers are out and the slots they take against his capacity, where they are (near him, or near her), the damage dealt, the
		// casts, what of it is born at her or untagged, and whether anything lands on HER: minions or towers she owns, her minion count, buffs she did not have. Special mechanics are logged by projectile name (a minion
		// whose slots grow with repeated casts, a ray a full flock channels). With LEVY_SUMMONSWEEP_PARITY=1 (on for the full sweep) the commander uses the same staff herself from the levy's spot in the same gear (the
		// DPS a player gets), clicking until her slots are full and, for a staff whose row says so (RECAST), holding the button at full slots.
		// Ship: the staves fixed in the summonsweep pass plus a vanilla / Calamity sample; asserted: summons at the levy, damage, nothing at the commander, the Saros Possession channels its ray at full slots.
		// Env: LEVY_SUMMONSWEEP=0 skips, =1 sweeps every post-Moon Lord Calamity summon weapon the armory issues and a post-Plantera sample (notes; parity on), LEVY_SUMMONSWEEP_ONLY=Name;Name, LEVY_SUMMONSWEEP_ALONE=1 runs only
		// this, LEVY_SUMMONSWEEP_TICKS=N (window, default 900), LEVY_SUMMONSWEEP_PARITY=1, LEVY_SUMMONSWEEP_DEBUG=1.

		private static int _ssPass, _ssFail;
		private static void SCheck(string name, bool ok) { if (ok) _ssPass++; else _ssFail++; _mod.Logger.Info($"[SummonSweep] {(ok ? "PASS" : "FAIL")} {name}"); }
		private static void SNote(string s) => _mod.Logger.Info("[SummonSweep] OBSERVED " + s);
		private static readonly bool SumDebug = Environment.GetEnvironmentVariable("LEVY_SUMMONSWEEP_DEBUG") == "1";

		private sealed class SumStat
		{
			public string Label, Mode, Err = ""; public long Damage; public int Ticks, Casts, Helpers, Sentries, Stacks, Recasts, HoldTicks; public float UsageMax = -1f, UsageEnd = -1f; public int Capacity, MaxMinions, MaxAlive, MinionTicks, NearLevyTicks, AtCmdTicks, CmdMinions, CmdOwned, CmdBuffs, LevyFrom;
			public float MaxSlots, MaxSingleSlots, CmdSlots; public string CmdBuffNames = "";
			/// <summary>Births by projectile name: total, born within 8 tiles of the levy, born at the commander, untagged; and the ticks each name was alive.</summary>
			public Dictionary<string, (int n, int atLevy, int atCmd, int untagged)> Born = new();
			public Dictionary<string, int> AliveTicks = new();
			public float Dps => Damage * 60f / Math.Max(1, Ticks);
			public int AtCmdBorn => Born.Values.Sum(v => v.atCmd);
			public int Untagged => Born.Values.Sum(v => v.untagged);
			public int BornOf(string name) => Born.TryGetValue(name, out var v) ? v.n : 0;
			public int AliveOf(string name) => AliveTicks.TryGetValue(name, out int v) ? v : 0;
			public override string ToString() => $"{Label} [{Mode}] ticks={Ticks} dmg={Damage} dps={Dps:0} casts={Casts} stacked={Stacks} recasts={Recasts} held={HoldTicks} helpers={Helpers} sentries={Sentries}{(UsageMax >= 0f ? $" rayUsage(max/end)={UsageMax:0}/{UsageEnd:0}" : "")} capacity={Capacity} maxMinions={MaxMinions} alive(max)={MaxAlive} slots(max)={MaxSlots:0.##} biggest={MaxSingleSlots:0.##} minionTicks={MinionTicks} nearLevy={NearLevyTicks} atCmd={AtCmdTicks} | commander: minions {CmdOwned} numMinions {CmdMinions} slots {CmdSlots:0.##} buffs+ {CmdBuffs}{(CmdBuffNames.Length > 0 ? " (" + CmdBuffNames + ")" : "")} | born {Born.Values.Sum(v => v.n)} atCmd {AtCmdBorn} untagged {Untagged} [{string.Join(", ", Born.OrderByDescending(kv => kv.Value.n).Take(8).Select(kv => kv.Key + " " + kv.Value.n + "/" + kv.Value.atLevy + (kv.Value.atCmd > 0 ? " CMD" + kv.Value.atCmd : "") + (kv.Value.untagged > 0 ? " untagged" + kv.Value.untagged : "")))}] alive [{string.Join(", ", AliveTicks.OrderByDescending(kv => kv.Value).Take(5).Select(kv => kv.Key + " " + kv.Value))}]{(Err.Length > 0 ? " ERR " + Err : "")}";
		}

		/// <summary>The summoner kit both the levy and the parity commander wear: 6 minion slots (measured: the bare 1, Stardust Helmet, Spooky Breastplate and Stardust Leggings), no set bonus. 6 is a levy's own cap.</summary>
		private static readonly short[] SumArmor = { ItemID.StardustHelmet, ItemID.SpookyBreastplate, ItemID.StardustLeggings };
		

		/// <summary>The four pinned enemies of the dummy line: the main one 12 tiles right of the levy, then 3, 6 and 9 tiles past it.</summary>
		private static void SumLine(Vector2 levyAt)
		{
			SpawnOriginTarget(levyAt); ClearRogueAdds();
			for (int c = 1; c <= 3; c++)
			{
				Vector2 pos = new Vector2(_sweepTargetAt.X + 3 * 16f * c, FloorY * 16f);
				int ai = NPC.NewNPC(new EntitySource_WorldEvent("IdentityWorldCheck"), (int)pos.X, (int)pos.Y, NPCID.GreenSlime);
				if (ai < 0 || ai >= Main.maxNPCs) continue;
				NPC a = Main.npc[ai];
				a.lifeMax = 20000; a.life = 20000; a.defense = 0; a.damage = 0; a.knockBackResist = 0f; a.noGravity = true; a.noTileCollide = true; a.aiStyle = -1; a.value = 0f; a.Bottom = pos;
				_rogueAdds.Add(a); _rogueAddsAt.Add(pos);
			}
		}

		private static Item SumItem(int type) { Item i = new Item(); i.SetDefaults(type); return i; }

		/// <summary>The commander's use of a staff in the parity run: clicks (20 of every 30 ticks) until her flock fills its slots, then lets go; for a RECAST staff she lets go for a moment and then clicks and holds (a player channels it at full slots).</summary>
		private static bool SumPress(Item w, int tick, int fullSince, bool clickAgain)
		{
			if (fullSince >= 0) return AltPolicies.IsRecast(w) && clickAgain;
			return tick % 30 < 20;
		}

		/// <summary>One staff for <paramref name="ticks"/> ticks: in a levy's hands (<paramref name="commander"/> false, she stands 64 tiles away) or in the commander's own at the levy's spot (the parity run).</summary>
		private static SumStat SumRun(int weaponType, string label, int ticks, bool commander)
		{
			var st = new SumStat { Label = label, Mode = commander ? "commander alone" : "1 levy" };
			Vector2 at = new Vector2(Cx * 16f + 8f, FloorY * 16f);
			_cmdAt = commander ? new Vector2(at.X, at.Y - 24f) : HoldCmdAt();
			ClearSoldiers(); ClearRogueAdds(); _cmdHolds = false; ReleaseCommanderButtons();
			Player cmd = Main.player[0];
			Item[] savedArmor = new Item[cmd.armor.Length]; for (int i = 0; i < cmd.armor.Length; i++) savedArmor[i] = cmd.armor[i];
			for (int i = 0; i < Player.MaxBuffs && i < cmd.buffType.Length; i++) { cmd.buffType[i] = 0; cmd.buffTime[i] = 0; }
			cmd.inventory[0] = new Item(); cmd.heldProj = -1; cmd.itemAnimation = 0; cmd.itemTime = 0;
			SumLine(at);
			int body = -1; SoldierNPC sn = null; Item w = SumItem(weaponType);
			if (commander)
			{
				for (int i = 0; i < SumArmor.Length; i++) cmd.armor[i] = SumItem(SumArmor[i]);
				// a minion that fires its owner's gun asks her for bullets (Cosmic Viper Engine, Tactical Plague Engine): a levy's minion is given a stack the same way (SoldierMinions.GiveMinionAmmo)
				Item am = SumItem(ItemID.MusketBall); am.stack = am.maxStack; cmd.inventory[54] = am;
				_cmdManaFloor = Math.Max(200, w.mana);
			}
			else
			{
				SoldierRecord rec = MakeOriginRecord(w, 0); rec.Name = "Pvt. Summoner";
				rec.Gear.Set(SoldierGear.SlotHead, SumItem(SumArmor[0])); rec.Gear.Set(SoldierGear.SlotBody, SumItem(SumArmor[1])); rec.Gear.Set(SoldierGear.SlotLegs, SumItem(SumArmor[2]));
				foreach (int sl in new[] { SoldierGear.SlotHead, SoldierGear.SlotBody, SoldierGear.SlotLegs }) rec.Gear.SetLocked(sl, true);
				rec.Order = SoldierOrder.Hold; rec.HoldPos = at;
				RosterSystem.Instance.Regulars.Add(rec);
				body = RosterSystem.Instance.SpawnBody(rec, at);
				if (body < 0 || !rec.HasBody) throw new Exception("levy did not spawn");
				Main.npc[body].Bottom = at; sn = Main.npc[body].ModNPC as SoldierNPC;
				// a stack of gel on the floor beside him, as a battlefield has: a minion that fetches its owner's items (the Void Eater Marionette) must still fight
				int gi = Item.NewItem(new EntitySource_WorldEvent("IdentityWorldCheck"), (int)at.X - 5 * 16, (int)at.Y - 24, 16, 16, ItemID.Gel, 20, noGrabDelay: true);
				if (gi >= 0 && gi < Main.maxItems) Main.item[gi].noGrabDelay = 0;
			}
			var buffs0 = new HashSet<int>(); for (int i = 0; i < cmd.buffType.Length; i++) if (cmd.buffType[i] > 0 && cmd.buffTime[i] > 0) buffs0.Add(cmd.buffType[i]);
			int c0 = SoldierMinions.CastTally, h0 = SoldierMinions.HelperTally, s0 = SoldierMinions.SentryTally, k0 = SoldierMinions.StackTally, r0 = SoldierMinions.RecastTally;
			bool saros = label.EndsWith(":SarosPossession");
			Main.rand = new Terraria.Utilities.UnifiedRandom(5151);
			KeepCommander(); PinSweepTarget(); PinRogueAdds(); CursorOnCommander(); Step(4);
			var lastSerial = new long[Main.maxProjectiles]; var seen = new HashSet<(int, long)>(); var buffNames = new HashSet<string>(); int dbg = 0, fullSince = -1; bool again = false;
			HoldBirthProbe.At.Clear(); HoldBirthProbe.On = true;
			if (SumDebug && !commander) { _killLog = 0; On_Projectile.Kill += KillSpy; }
			try
			{
				for (int k = 0; k < ticks; k++)
				{
					foreach (var r in RosterSystem.Instance.Regulars) r.Mana = r.MaxMana;
					if (commander)
					{
						float own = 0f; for (int i = 0; i < Main.maxProjectiles; i++) { Projectile q = Main.projectile[i]; if (q.active && q.owner == 0 && q.minion) own += q.minionSlots; } // her minions' slots (a server never resets her slotsMinions)
						bool full = cmd.maxMinions > 0 && own > 0.01f && own + SoldierNPC.SlotsPerMinion(w) > cmd.maxMinions + 0.01f;
						if (full && fullSince < 0) fullSince = k;
						if (fullSince >= 0 && !again && k - fullSince >= 3 && cmd.itemAnimation == 0) again = true; // the button let go until the last use is over, then clicked and held
						// her cursor is on the main target, the plain one and Calamity's own (a staff casts at it, the Saros ray turns to it): a server never updates Calamity's for her
						Vector2 aim = _sweepTarget != null ? _sweepTarget.Center : cmd.Center; _parityAim = aim;
						try { object cp = AltPolicies.CalamityPlayerOf(cmd); if (cp != null) AltPolicies.SetCalAimPublic(cp, aim - cmd.MountedCenter); } catch (Exception) { }
						if (SumDebug && fullSince >= 0 && k - fullSince < 8) { bool cu = false; try { cu = ItemLoader.CanUseItem(w, cmd); } catch (Exception) { } SNote($"  dbg {label} [commander] t={k} full-tick press={SumPress(w, k, fullSince, again)} canUse={cu} anim={cmd.itemAnimation} release={cmd.releaseUseItem} channel={cmd.channel} held={cmd.HeldItem?.Name} sel={cmd.selectedItem} noItems={cmd.noItems} cc={cmd.CCed}"); }
						if (SumDebug && k % 45 == 0) SNote($"  dbg {label} [commander] t={k} ownSlots={own} full={full} since={fullSince} press={SumPress(w, k, fullSince, again)} anim={cmd.itemAnimation} time={cmd.itemTime} release={cmd.releaseUseItem} counts(shoot)={(w.shoot < cmd.ownedProjectileCounts.Length ? cmd.ownedProjectileCounts[w.shoot] : -1)} max={cmd.maxMinions}");
						try { MegaCommanderUse(w, SumPress(w, k, fullSince, again)); } catch (Exception e) { st.Err = "cmd use " + (e is TargetInvocationException && e.InnerException != null ? e.InnerException.GetType().Name + ": " + e.InnerException.Message : e.GetType().Name); }
						_cmdHolds = cmd.controlUseItem && w.channel;
						st.MaxMinions = Math.Max(st.MaxMinions, cmd.maxMinions);
					}
					else CursorOnCommander();
					Step(1); ClearNaturalHostiles();
					if (_sweepTarget != null && _sweepTarget.active) { int d = _sweepTarget.lifeMax - _sweepTarget.life; if (d > 0) st.Damage += d; }
					foreach (NPC a in _rogueAdds) if (a != null && a.active) { int d = a.lifeMax - a.life; if (d > 0) st.Damage += d; }
					st.Ticks++;
					if (sn != null && sn.NPC.active) { st.Capacity = Math.Max(st.Capacity, sn.MinionCapacity(w)); st.MaxMinions = Math.Max(st.MaxMinions, sn.Dummy?.maxMinions ?? 0); if (Main.GameUpdateCount < sn.RecastHoldUntil) st.HoldTicks++; }
					if (saros) { float u = AltPolicies.ReadMeter(commander ? cmd : sn?.Dummy, "sarosEclipseBeamUsage"); if (!float.IsNaN(u)) { st.UsageMax = Math.Max(st.UsageMax, u); st.UsageEnd = u; } }
					Vector2 cmdC = cmd.Center, levyC = sn != null && sn.NPC.active ? sn.NPC.Center : at;
					int alive = 0, owned = 0; float slots = 0f;
					for (int i = 0; i < Main.maxProjectiles; i++)
					{
						Projectile p = Main.projectile[i];
						if (!p.active || p.hostile) continue;
						bool tagged = p.TryGetGlobalProjectile(out SoldierProjectileTag tag) && tag.SoldierIndex >= 0 && tag.SoldierIndex == body;
						if (p.type == ProjectileID.FallingStarSpawner || p.type == ProjectileID.FallingStar) continue; // the world's own falling stars
						string pn = p.ModProjectile?.Name ?? ProjectileID.Search.GetName(p.type);
						if (!commander && p.owner == 0 && p.minion && !p.sentry) owned++; // a levy's tower is owned by her slot by design (parked out of her tower cap); a minion is never
						bool mine = commander ? p.owner == 0 && !(p.TryGetGlobalProjectile(out SoldierProjectileTag ct) && ct.SoldierIndex >= 0) : tagged;
						if (mine)
						{
							st.AliveTicks.TryGetValue(pn, out int at0); st.AliveTicks[pn] = at0 + 1;
							if (SumDebug && !commander && k % 90 == 0 && p.minion) SNote($"  dbg {label} t={k} {pn} ai=({p.ai[0]:0.##},{p.ai[1]:0.##},{p.ai[2]:0.##}) slots={p.minionSlots} dmg={p.damage} orig={p.originalDamage} fromTarget={((p.Center - (_sweepTarget?.Center ?? levyC)) / 16f).ToString()} fromLevy={(p.Center - levyC).Length() / 16f:0.0}t vel={p.velocity.Length():0.0}");
							if (p.minion || p.sentry)
							{
								alive++; slots += p.minionSlots; st.MaxSingleSlots = Math.Max(st.MaxSingleSlots, p.minionSlots); st.MinionTicks++;
								float dl = Vector2.Distance(p.Center, levyC), dc = Vector2.Distance(p.Center, cmdC);
								if (!commander && dc < 10 * 16f && dc < dl) st.AtCmdTicks++;
								if (dl <= 20 * 16f || Vector2.Distance(p.Center, _sweepTarget?.Center ?? levyC) <= 20 * 16f) st.NearLevyTicks++;
							}
						}
						// a birth since the last look (see HoldRun)
						bool probed = HoldBirthProbe.At.TryGetValue(i, out var ba) && ba.type == p.type;
						if (probed ? ba.serial == lastSerial[i] : !seen.Add((i, ((long)p.identity << 20) | (uint)p.type))) continue;
						if (probed) lastSerial[i] = ba.serial;
						Vector2 born = probed ? ba.at : p.Center;
						if (commander && !mine) continue;
						if (!commander && !tagged && p.owner != 0 && p.owner != SoldierNPC.DummySlot) continue;
						float bl = Vector2.Distance(born, levyC), bc = Vector2.Distance(born, cmdC);
						bool atCmd = !commander && bc < 10 * 16f && bc < bl, atLevy = bl <= HoldNearLevyTiles * 16f;
						st.Born.TryGetValue(pn, out var bt); st.Born[pn] = (bt.n + 1, bt.atLevy + (atLevy ? 1 : 0), bt.atCmd + (atCmd ? 1 : 0), bt.untagged + (commander || tagged ? 0 : 1));
						if (SumDebug && dbg++ < 40) SNote($"  dbg {label} [{st.Mode}] t={k} new {pn} owner={p.owner} tagged={tagged} minion={p.minion} sentry={p.sentry} slots={p.minionSlots} fromLevy={bl / 16f:0.0}t fromCmd={bc / 16f:0.0}t ai=({p.ai[0]:0.##},{p.ai[1]:0.##},{p.ai[2]:0.##}) dmg={p.damage}");
					}
					st.MaxAlive = Math.Max(st.MaxAlive, alive); st.MaxSlots = Math.Max(st.MaxSlots, slots);
					if (!commander)
					{
						st.CmdOwned = Math.Max(st.CmdOwned, owned); st.CmdMinions = Math.Max(st.CmdMinions, cmd.numMinions); st.CmdSlots = Math.Max(st.CmdSlots, cmd.slotsMinions);
						int added = 0;
						for (int i = 0; i < cmd.buffType.Length; i++) if (cmd.buffType[i] > 0 && cmd.buffTime[i] > 0 && !buffs0.Contains(cmd.buffType[i])) { added++; buffNames.Add(BuffLoader.GetBuff(cmd.buffType[i])?.Name ?? Lang.GetBuffName(cmd.buffType[i])); }
						st.CmdBuffs = Math.Max(st.CmdBuffs, added);
					}
				}
			}
			finally
			{
				HoldBirthProbe.On = false; HoldBirthProbe.At.Clear(); _cmdHolds = false; _parityAim = null;
				if (SumDebug && !commander) On_Projectile.Kill -= KillSpy;
				try { MegaCommanderUse(w, false); } catch (Exception) { }
				Main.mouseLeft = false; Main.mouseLeftRelease = true;
				for (int i = 0; i < cmd.armor.Length; i++) cmd.armor[i] = savedArmor[i];
				cmd.inventory[54] = new Item(); _cmdManaFloor = 0;
				for (int i = 0; i < cmd.buffType.Length; i++) { cmd.buffType[i] = 0; cmd.buffTime[i] = 0; }
			}
			st.CmdBuffNames = string.Join(",", buffNames);
			st.Casts = SoldierMinions.CastTally - c0; st.Helpers = SoldierMinions.HelperTally - h0; st.Sentries = SoldierMinions.SentryTally - s0; st.Stacks = SoldierMinions.StackTally - k0; st.Recasts = SoldierMinions.RecastTally - r0;
			for (int i = 0; i < Main.maxItems; i++) if (Main.item[i].active && Main.item[i].type == ItemID.Gel) Main.item[i].active = false;
			ClearSoldiers(); ClearRogueAdds(); ReleaseCommanderButtons(); cmd.inventory[0] = new Item(); cmd.heldProj = -1; cmd.itemAnimation = 0; cmd.itemTime = 0; cmd.numMinions = 0; cmd.slotsMinions = 0f;
			return st;
		}

		/// <summary>The staves fixed in the summonsweep pass (asserted on the Calamity half): one minion that grows with repeated casts (Saros Possession and its ray, Sirius, the Void Eater Marionette), a ring of shots that counts for its
		/// owner (Void Concentration Staff), a cast of two half-slot pieces (Pole Warper), a staff that sizes its slime from its holder's free slots (Abandoned Slime Staff), staves that ask for a number of slots and were never issued
		/// (Cosmilamp, Kingof Constellations, Temporal Umbrella, Mutated Truffle, Lilies of Finality, Dragonblood Disgorger), and a tower whose hive sends out minions (Hive Pod).</summary>
		private static readonly string[] SumFixedCal = { "SarosPossession", "Sirius", "VoidEaterMarionette", "VoidConcentrationStaff", "PoleWarper", "AbandonedSlimeStaff", "Cosmilamp", "KingofConstellationsTenryu", "TemporalUmbrella", "MutatedTruffle", "LiliesOfFinality", "DragonbloodDisgorger", "HivePod" };
		/// <summary>Staves whose Shoot powers up the minion already out: one minion, grown to the levy's slots.</summary>
		private static readonly string[] SumStackers = { "SarosPossession", "Sirius", "VoidEaterMarionette" };
		/// <summary>Staves whose own CanUseItem asks for minion slots: issued now (the armory's use gate probe has slots).</summary>
		private static readonly string[] SumSlotGated = { "Cosmilamp", "KingofConstellationsTenryu", "TemporalUmbrella", "MutatedTruffle", "LiliesOfFinality", "DragonbloodDisgorger", "Endogenesis", "CosmicImmaterializer" };
		/// <summary>Ship sample: post-Plantera and post-Moon Lord staves covering the ways a modded summon works (a flock, a minion that fires its owner's gun).</summary>
		private static readonly string[] SumSampleCal = { "TacticalPlagueEngine", "EtherealSubjugator", "CorvidHarbringerStaff" };
		private static readonly short[] SumSampleVanilla = { ItemID.RavenStaff, ItemID.XenoStaff, ItemID.StardustDragonStaff, ItemID.EmpressBlade, ItemID.MoonlordTurretStaff };

		/// <summary>Every summon weapon of <paramref name="cal"/> a levy can carry as a staff or a tower whose rarity is post-Moon Lord (Purple and up, Calamity's own rarities), and an even spread of the post-Plantera ones (Lime to Red).</summary>
		private static List<(int type, string label, string tier)> SumSweepWeapons(Mod cal, string[] only)
		{
			var post = new List<(int, string, string)>(); var mid = new List<(int, string, string)>();
			var probe = new SoldierGear();
			for (int i = ItemID.Count; i < ItemLoader.ItemCount; i++)
			{
				if (!ContentSamples.ItemsByType.TryGetValue(i, out Item s) || s == null || s.IsAir || s.ModItem?.Mod != cal || s.damage <= 0 || s.shoot <= 0 || s.accessory) continue;
				if (!(s.CountsAsClass(DamageClass.Summon) || s.sentry)) continue;
				if (only != null && !only.Contains(s.ModItem.Name)) continue;
				Item it = new Item(); it.SetDefaults(i);
				if (!probe.CanHold(SoldierGear.SlotWeapon, it)) continue;
				WeaponFamily f = WeaponClassifier.GetProfile(it).Family;
				if (f != WeaponFamily.Summon && f != WeaponFamily.Sentry) continue;
				if (ModWeaponGates.Locked(it)) { SNote($"sweep: CalamityMod:{s.ModItem.Name} (rare {s.rare} {f}) is not issued to levies: the armory's use gate finds it locked (its own CanUseItem refuses a fresh player)"); continue; }
				bool postMl = s.rare >= ItemRarityID.Purple || s.rare >= ItemRarityID.Count;
				if (postMl || only != null) post.Add((i, "CalamityMod:" + s.ModItem.Name, "rare " + s.rare + " " + f));
				else if (s.rare >= ItemRarityID.Lime) mid.Add((i, "CalamityMod:" + s.ModItem.Name, "rare " + s.rare + " " + f));
			}
			int step = Math.Max(1, mid.Count / 10);
			return post.Concat(mid.Where((w, k) => k % step == 0)).ToList();
		}

		private static void SummonSweepCheck()
		{
			string mode = Environment.GetEnvironmentVariable("LEVY_SUMMONSWEEP");
			if (mode == "0") return;
			_ssPass = _ssFail = 0;
			int ticks = int.TryParse(Environment.GetEnvironmentVariable("LEVY_SUMMONSWEEP_TICKS"), out int tk) ? tk : 900;
			string only = Environment.GetEnvironmentVariable("LEVY_SUMMONSWEEP_ONLY");
			string[] names = string.IsNullOrEmpty(only) ? null : only.Split(';', StringSplitOptions.RemoveEmptyEntries);
			bool full = mode == "1" || names != null, parity = full || Environment.GetEnvironmentVariable("LEVY_SUMMONSWEEP_PARITY") == "1";
			bool cal = ModLoader.TryGetMod("CalamityMod", out Mod calMod);
			var sw = System.Diagnostics.Stopwatch.StartNew();
			var runs = new List<(int type, string label, string tier, bool asserted)>();
			if (!full || names != null)
			{
				foreach (short t in SumSampleVanilla) if (names == null || names.Contains(ItemID.Search.GetName(t))) runs.Add((t, "Terraria:" + ItemID.Search.GetName(t), "sample", true));
				if (cal) foreach (string n in SumFixedCal.Concat(SumSampleCal)) if ((names == null || names.Contains(n)) && calMod.TryFind(n, out ModItem mi)) runs.Add((mi.Type, "CalamityMod:" + n, "sample", true));
			}
			if (full && cal) foreach (var w in SumSweepWeapons(calMod, names)) if (!runs.Any(r => r.type == w.type)) runs.Add((w.type, w.label, w.tier, false));
			try
			{
				Arena(); _cmdAt = HoldCmdAt(); KeepCommander(); Step(2);
				foreach (var (type, label, tier, asserted) in runs)
				{
					try
					{
						SumStat lv = SumRun(type, label, ticks, false);
						string par = "";
						if (parity)
						{
							SumStat pc = SumRun(type, label, ticks, true);
							par = $" || {pc} || levy/commander dps={(pc.Dps > 0 ? (lv.Dps / pc.Dps).ToString("0.00") : "n/a")}";
						}
						Item fi = SumItem(type); WeaponProfile fp = WeaponClassifier.GetProfile(fi);
						string route = fp.Family + (SoldierMinions.CanDrive(fi.shoot) ? "/real" : "/stand-in") + (SoldierMinions.UsesRealShoot(fi) ? "/shoot" : "") + (SoldierNPC.TransientStaff(fi) ? "/self-ending" : "") + (SoldierMinions.IsBadStaff(fi.shoot) ? "/BAD" : "") + (AltPolicies.HasRow(type) ? "/row=" + AltPolicies.For(type).Kind : "") + " slots/cast=" + SoldierNPC.SlotsPerMinion(fi).ToString("0.##") + " cap=" + (SummonCap.IsCapped(fi) ? SummonCap.MaxCasts(fi).ToString() : "none");
						SNote($"sweep: {lv} route={route} ({tier}){par}");
						if (!asserted) continue;
						bool sentry = fp.Family == WeaponFamily.Sentry;
						SCheck($"{label}: its {(sentry ? "towers" : "minions")} are out for the levy ({lv.MaxAlive} at most, {lv.MaxSlots:0.##} slots of his {lv.MaxMinions}, {lv.Casts + lv.Sentries} casts) and stay by him, none of them near the commander 64 tiles away ({lv.AtCmdTicks} minion-ticks near her, {lv.AtCmdBorn} born at her)",
							lv.MaxAlive > 0 && lv.AtCmdTicks == 0 && lv.AtCmdBorn == 0);
						SCheck($"{label}: hits the dummy line (damage {lv.Damage} in {lv.Ticks} ticks)", lv.Damage > 0);
						SCheck($"{label}: nothing lands on the commander (minions or towers she owns {lv.CmdOwned}, her minion count {lv.CmdMinions}, her slots {lv.CmdSlots:0.##}, buffs she did not have {lv.CmdBuffs}{(lv.CmdBuffNames.Length > 0 ? ": " + lv.CmdBuffNames : "")}, untagged births {lv.Untagged})",
							lv.CmdOwned == 0 && lv.CmdMinions == 0 && lv.CmdSlots < 0.01f && lv.CmdBuffs == 0 && lv.Untagged == 0);
						string nm = label.Substring(label.IndexOf(':') + 1);
						if (SumStackers.Contains(nm))
							SCheck($"{label}: repeated summons grow one minion to the levy's slots instead of casting new ones (biggest {lv.MaxSingleSlots:0.##} slots of {lv.Capacity}, alive at most {lv.MaxAlive}, {lv.Stacks} casts powered it up)", lv.MaxAlive == 1 && lv.MaxSingleSlots >= lv.Capacity - 0.01f && lv.Stacks > 0);
						if (nm == "VoidEaterMarionette")
							SCheck($"{label}: the worm fights with an item on the floor instead of fetching it to the levy forever ({lv.Dps:0} damage a second; it was 64 while it played fetch)", lv.Dps >= 1000f);
						if (nm == "SarosPossession")
							SCheck($"{label}: at full slots the levy channels the Totality Ray (rays born {lv.BornOf("SarosEclipseBeam")}, alive {lv.AliveOf("SarosEclipseBeam")} ticks of {lv.Ticks}, button held {lv.HoldTicks} ticks)", lv.BornOf("SarosEclipseBeam") > 0 && lv.AliveOf("SarosEclipseBeam") >= lv.Ticks / 4);
						if (nm == "PoleWarper" || nm == "AbandonedSlimeStaff")
							SCheck($"{label}: the levy's flock fills his slots ({lv.MaxSlots:0.##} of {lv.MaxMinions})", lv.MaxSlots >= lv.MaxMinions - 0.01f);
						if (nm == "VoidConcentrationStaff")
							SCheck($"{label}: its ring of shots stays one ring and the real minion keeps working (born {lv.Born.Values.Sum(v => v.n)} in {lv.Ticks} ticks, the staff on the stand-in: {SoldierMinions.IsBadStaff(fi.shoot)})", !SoldierMinions.IsBadStaff(fi.shoot) && lv.Born.Values.Sum(v => v.n) < 400);
						if (SumSlotGated.Contains(nm))
							SCheck($"{label}: issued to levies (its own CanUseItem asks for minion slots, which is the holder's gear, not a world lock)", !ModWeaponGates.Locked(fi));
					}
					catch (Exception e) { SCheck($"{label} threw {e.GetType().Name}: {e.Message} @ {(e.StackTrace ?? "").Split('\n')[0].Trim()}", false); }
				}
			}
			catch (Exception e) { SCheck("summon sweep threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim(), false); }
			finally { _sweepTarget = null; ClearRogueAdds(); ClearSoldiers(); ReleaseCommanderButtons(); }
			SNote($"summon sweep: {runs.Count} staves, {ticks} ticks each{(parity ? " (and the commander's own run)" : "")}, in {sw.ElapsedMilliseconds / 1000} s");
			_mod.Logger.Info($"[SummonSweep] self-check pass={_ssPass} fail={_ssFail}");
		}
	}
}
