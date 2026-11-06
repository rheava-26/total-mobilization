using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Util;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// BANNER SCENARIO (LEVY_HARNESS_SCENARIO=banner). Measurement only. For each of (gun, sword, contact) x (no banner, the enemy's banner, a different enemy's banner):
	/// the commander stands 80 tiles from the soldier and the enemy (outside any single screen of the fight) with a banner hung over his head, and the mean damage per hit
	/// the soldier deals to the pinned enemy ("gun", "sword") or takes from it ("contact") is logged as one HARNESS-BANNER line. LEVY_BANNER_MOD=<ModName> uses that mod's first
	/// ModNPC with a Banner and BannerItem as the enemy instead of a Green Slime. LEVY_BANNER_EXPERT=1 turns Expert on for the run.
	/// </summary>
	public partial class CombatHarness
	{
		private sealed class BanTrial { public string Kind, Variant; }
		private readonly List<BanTrial> _banTrials = new();
		private int _banI = -1, _banTick, _banEnemyType, _banBannerId, _banOtherId;
		private bool _banStarted;
		private SoldierRecord _banRec; private int _banBody, _banTarget;
		private const int BanWarm = 45, BanCmdOffset = 80; // BanWarm: ticks before counting (the commander banner scan refreshes every 30 ticks)
		private static readonly int BanTicks = int.TryParse(Environment.GetEnvironmentVariable("LEVY_BANNER_TICKS"), out int bt) ? bt : 1500;

		/// <summary>Logs and also appends to LEVY_HARNESS_OUT.banner.txt (the shared tML server.log is rotated by every concurrent run).</summary>
		private void BanLog(string s)
		{
			Mod.Logger.Info(s);
			try { System.IO.File.AppendAllText(OutPath + ".banner.txt", s + "\n"); } catch (Exception) { }
		}

		private bool BeginBanner()
		{
			int type = NPCID.GreenSlime;
			string mod = Environment.GetEnvironmentVariable("LEVY_BANNER_MOD");
			if (!string.IsNullOrEmpty(mod))
			{
				ModNPC m = ModContent.GetContent<ModNPC>().FirstOrDefault(x => x.Mod.Name == mod && x.Banner > 0 && x.BannerItem > 0 && ItemID.Sets.BannerStrength[x.BannerItem].Enabled);
				if (m != null) type = m.Type; else Mod.Logger.Warn("HARNESS-BANNER: no bannered ModNPC in " + mod + "; using Green Slime");
			}
			try { MonoModHooks.Add(typeof(Netplay).GetMethod("UpdateConnectedClients", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic), new HookUpdateConnectedClients(KeepStandinDetour)); _spHooked = true; } catch (Exception e) { Mod.Logger.Warn("HARNESS-BANNER: could not hook UpdateConnectedClients: " + e.Message); }
			_banEnemyType = type;
			var probe = new NPC(); probe.SetDefaults(type);
			_banBannerId = Item.NPCtoBanner(probe.BannerID());
			var other = new NPC(); other.SetDefaults(NPCID.Zombie);
			_banOtherId = Item.NPCtoBanner(other.BannerID());
			if (_banOtherId == _banBannerId) { other.SetDefaults(NPCID.DemonEye); _banOtherId = Item.NPCtoBanner(other.BannerID()); }
			if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LEVY_BANNER_EXPERT"))) Main.GameMode = GameModeID.Expert;
			foreach (string k in new[] { "gun", "sword", "contact" })
				foreach (string v in new[] { "none", "own", "other" })
					_banTrials.Add(new BanTrial { Kind = k, Variant = v });
			_banI = 0; _banStarted = false;
			BanLog($"HARNESS-BANNER: enemy={Lang.GetNPCNameValue(type)} (type {type}) bannerId={_banBannerId} other bannerId={_banOtherId} expert={Main.expertMode}");
			return true;
		}

		/// <summary>Hangs the banner for <paramref name="bannerId"/> at the given tile (a ceiling tile is built above it), exactly as a player placing the banner item would.</summary>
		private bool BanHang(int bannerId, int x, int y)
		{
			int item = Item.BannerToItem(bannerId);
			Item it = ContentSamples.ItemsByType[item];
			for (int dx = -1; dx <= 1; dx++) { Tile c = Main.tile[x + dx, y - 1]; c.HasTile = true; c.TileType = TileID.GrayBrick; c.BlockType = BlockType.Solid; }
			bool ok = WorldGen.PlaceObject(x, y, it.createTile, true, it.placeStyle);
			ModTile mt = TileLoader.GetTile(it.createTile);
			BanLog($"HARNESS-BANNER: tile class={(mt == null ? "vanilla" : mt.GetType().FullName + " : " + mt.GetType().BaseType?.Name)} BannerItemToNPC({item})={NPCLoader.BannerItemToNPC(item)} strengthEnabled={ItemID.Sets.BannerStrength[item].Enabled}");
			Tile t = Main.tile[x, y];
			BanLog($"HARNESS-BANNER: hang banner id {bannerId} (item {item} {it.Name}) tile {it.createTile} style {it.placeStyle} at {x},{y}: placed={ok} hasTile={t.HasTile} type={t.TileType} frame={t.TileFrameX},{t.TileFrameY}");
			return ok;
		}

		private void StepBanner()
		{
			if (_banI >= _banTrials.Count) { Finish("banner scenario complete"); return; }
			BanTrial tr = _banTrials[_banI];
			int enemyX = _cx + 20, soldierX = enemyX - (tr.Kind == "gun" ? 12 : 3), cmdX = enemyX - BanCmdOffset;
			if (!_banStarted)
			{
				ClearAll(); SpBuildFloor("run");
				for (int x = cmdX - 4; x <= cmdX + 4; x++) for (int y = FloorY - 12; y < FloorY; y++) { Tile c = Main.tile[x, y]; c.HasTile = false; c.TileType = 0; }
				if (tr.Variant != "none") BanHang(tr.Variant == "own" ? _banBannerId : _banOtherId, cmdX, FloorY - 8);
				HarnessBannerProbe.Reset();
				var rec = MakeRecord("Pvt. Banner");
				var gear = rec.Gear;
				var w = new Item(); w.SetDefaults(tr.Kind == "gun" ? ItemID.Handgun : tr.Kind == "sword" ? ItemID.IronBroadsword : ItemID.Handgun);
				if (gear.CanHold(SoldierGear.SlotWeapon, w)) { gear.Weapon = w; gear.SetLocked(SoldierGear.SlotWeapon, true); }
				Item ammo = FindAmmo(w, ""); if (ammo != null && gear.CanHold(SoldierGear.SlotAmmo, ammo)) { gear.Ammo = ammo; gear.SetLocked(SoldierGear.SlotAmmo, true); }
				rec.LoadoutLocked = true; rec.Order = SoldierOrder.FreeRein; rec.CurrentHp = rec.MaxHp; rec.Mana = rec.MaxMana;
				RosterSystem.Instance.Regulars.Add(rec); _banRec = rec;
				BanPin(cmdX);
				Vector2 at = new Vector2(soldierX * 16f, FloorY * 16f);
				_banBody = RosterSystem.Instance.SpawnBody(rec, at);
				if (_banBody < 0 || !rec.HasBody) { Mod.Logger.Warn("HARNESS-BANNER: SpawnBody failed"); _banI++; _banStarted = false; return; }
				Main.npc[_banBody].Bottom = at; Main.npc[_banBody].velocity = Vector2.Zero;
				_banTarget = SpSpawnTarget(enemyX * 16f, 64, 96, tr.Kind == "contact" ? 40 : 0, _banEnemyType);
				if (tr.Kind == "contact") Main.npc[_banTarget].Center = new Vector2(soldierX * 16f, FloorY * 16f - 48f);
				if (tr.Kind == "contact") Main.npc[_banTarget].GetGlobalNPC<HarnessTarget>().Pin = Main.npc[_banTarget].position;
				_banStarted = true; _banTick = 0;
				return;
			}
			BanPin(cmdX);
			_banTick++;
			HarnessBannerProbe.Counting = _banTick > BanWarm;
			NPC s = Main.npc[_banBody];
			if (s.active && s.ModNPC is SoldierNPC)
			{
				s.life = s.lifeMax; // the soldier is topped up each tick: only the per-hit number matters
				if (tr.Kind != "contact") { }
			}
			NPC e = Main.npc[_banTarget];
			if (e.active) e.life = e.lifeMax;
			if (tr.Kind == "contact" && e.active && s.active) { e.Center = s.Center; e.GetGlobalNPC<HarnessTarget>().Pin = e.position; } // the enemy rides on the soldier
			if (_banTick < BanTicks) return;
			bool contact = tr.Kind == "contact";
			int n = contact ? HarnessBannerProbe.TakenN : HarnessBannerProbe.DealtN;
			long sum = contact ? HarnessBannerProbe.TakenSum : HarnessBannerProbe.DealtSum;
			BanLog($"HARNESS-BANNER: {tr.Kind,-7} banner={tr.Variant,-5} hits={n,3} mean={(n > 0 ? (double)sum / n : 0):0.00} min={HarnessBannerProbe.Min} max={HarnessBannerProbe.Max} commanderHasBanner(own)={BannerBuffs.Has(0, _banBannerId)} (other)={BannerBuffs.Has(0, _banOtherId)}");
			_banI++; _banStarted = false;
		}

		private void BanPin(int cmdX)
		{
			Player p = Main.player[0];
			p.active = true; p.dead = false; p.name = CommanderName;
			p.statLifeMax = p.statLifeMax2 = 500; p.statLife = 500; p.statManaMax = p.statManaMax2 = 200; p.statMana = 200;
			p.position = new Vector2(cmdX * 16f - p.width / 2f, FloorY * 16f - p.height); p.velocity = Vector2.Zero;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || n.ModNPC is SoldierNPC || n.townNPC || n.friendly) continue;
				if (n.TryGetGlobalNPC(out HarnessTarget ht) && ht.IsTarget) continue;
				n.active = false;
			}
		}
	}

	/// <summary>Records the damage of every strike that lands on the harness enemy (dealt) or on a soldier (taken) while the banner scenario runs.</summary>
	public class HarnessBannerProbe : GlobalNPC
	{
		public static long DealtSum, TakenSum; public static int DealtN, TakenN, Min, Max; public static bool Counting;
		public static void Reset() { DealtSum = TakenSum = 0; DealtN = TakenN = 0; Min = int.MaxValue; Max = 0; Counting = false; }
		public override void HitEffect(NPC npc, NPC.HitInfo hit)
		{
			if (!Counting || !CombatHarness.Enabled || !string.Equals(CombatHarness.Scenario, "banner", StringComparison.OrdinalIgnoreCase)) return;
			if (npc.ModNPC is SoldierNPC) { TakenSum += hit.Damage; TakenN++; Min = Math.Min(Min, hit.Damage); Max = Math.Max(Max, hit.Damage); }
			else if (npc.TryGetGlobalNPC(out HarnessTarget ht) && ht.IsTarget) { DealtSum += hit.Damage; DealtN++; Min = Math.Min(Min, hit.Damage); Max = Math.Max(Max, hit.Damage); }
		}
	}
}
