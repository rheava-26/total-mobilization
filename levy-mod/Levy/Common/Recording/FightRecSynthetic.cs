using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Content.NPCs;

namespace Levy.Common.Recording
{
	/// <summary>
	/// Headless test of the fight recorder (inert unless LEVY_FIGHTREC_TEST is set; output folder LEVY_FIGHTREC_OUT). A dedicated server has no local player, so Main.player[0] stands in for the commander and is
	/// moved by script (walk, jump, extra jump flag, dash, hook, item use, hit). A boss spawns above it, three levies stand next to it. The recorder runs on that player, the boss is then removed, and the process exits
	/// once the file is written. Pure data check of the file path: it does not claim the movement is realistic.
	/// </summary>
	public class FightRecSynthetic : ModSystem
	{
		private static readonly bool Enabled = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LEVY_FIGHTREC_TEST"));
		private int _t = -1; private int _boss = -1; private const int Floor = 120, RunTicks = 900;

		public override void OnWorldLoad() { if (Enabled) _t = 0; }

		// A dedicated server with no client does not advance the world by itself; pump Main.Update from the third-party tick hook (the combat harness does the same).
		private static bool _inDrive; private static System.Reflection.MethodInfo _update;
		public override void Load() { if (Enabled && Main.dedServ) Main.OnTickForThirdPartySoftwareOnly += Drive; }
		public override void Unload() { Main.OnTickForThirdPartySoftwareOnly -= Drive; }
		private static void Drive()
		{
			if (_inDrive || !Main.dedServ || Main.netMode != NetmodeID.Server) return;
			_inDrive = true;
			try
			{
				_update ??= typeof(Main).GetMethod("Update", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null, new[] { typeof(GameTime) }, null);
				for (int i = 0; i < 20; i++) _update.Invoke(Main.instance, new object[] { new GameTime() });
			}
			catch (Exception e) { Levy.Instance?.Logger.Warn("[FightRecTest] world update threw: " + (e is System.Reflection.TargetInvocationException t && t.InnerException != null ? t.InnerException : e)); }
			finally { _inDrive = false; }
		}

		public override void PostUpdateEverything()
		{
			if (!Enabled || _t < 0) return;
			try { Step(); } catch (Exception e) { Mod.Logger.Error("[FightRecTest] FAILED " + e); Environment.Exit(3); }
		}

		private void Step()
		{
			_t++;
			int cx = Main.maxTilesX / 2;
			Player p = Main.player[0];
			if (_t == 1)
			{
				for (int x = cx - 100; x <= cx + 100; x++)
				{
					for (int y = Floor - 40; y < Floor; y++) { Tile a = Main.tile[x, y]; a.HasTile = false; a.LiquidAmount = 0; }
					Tile top = Main.tile[x, Floor]; top.HasTile = true; top.TileType = TileID.GrayBrick; top.BlockType = BlockType.Solid;
					Tile u = Main.tile[x, Floor + 1]; u.HasTile = true; u.TileType = TileID.GrayBrick; u.BlockType = BlockType.Solid;
				}
				for (int x = cx - 8; x <= cx - 2; x++) { Tile pl = Main.tile[x, Floor - 8]; pl.HasTile = true; pl.TileType = TileID.Platforms; pl.BlockType = BlockType.Solid; }
				p.active = true; p.dead = false; p.name = "FightRecTester"; p.statLife = p.statLifeMax = p.statLifeMax2 = 500; p.statMana = 200;
				p.position = new Vector2(cx * 16f, Floor * 16f - p.height); p.velocity = Vector2.Zero;
				for (int i = 0; i < 3; i++)
				{
					var rec = new SoldierRecord { Id = RosterSystem.Instance.NextId++, Kind = SoldierKind.Regular, Grade = Grade.Levy, Temperament = Temperament.Sellsword, Male = true, Name = "Pvt. Rec" + i,
						CommanderName = p.name, CommanderIndex = 0, LifeCrystals = 20, ManaCrystals = 9, HomeBarracksId = -1, StoodDown = false };
					rec.CurrentHp = rec.MaxHp; rec.Mana = rec.MaxMana; rec.Order = SoldierOrder.Follow;
					RosterSystem.Instance.Regulars.Add(rec);
					Vector2 at = new Vector2((cx - 6 + i * 3) * 16f, Floor * 16f);
					int idx = RosterSystem.Instance.SpawnBody(rec, at);
					if (idx >= 0) { Main.npc[idx].Bottom = at; Main.npc[idx].velocity = Vector2.Zero; }
					else Mod.Logger.Warn("[FightRecTest] levy spawn failed");
				}
			}
			if (_t == 20)
			{
				FightRecorder.IgnoreInactive = true;
				FightRecorder.OutDirOverride = Environment.GetEnvironmentVariable("LEVY_FIGHTREC_OUT");
				_boss = NPC.NewNPC(new EntitySource_WorldEvent("FightRecTest"), cx * 16 + 400, (Floor - 25) * 16, NPCID.EyeofCthulhu);
				if (_boss >= 0) { NPC b = Main.npc[_boss]; b.aiStyle = -1; b.noGravity = true; b.noTileCollide = true; b.life = b.lifeMax = 2800; b.damage = 0; }
				string path = FightRecorder.Start(p, null, out string err);
				if (path == null) { Mod.Logger.Error("[FightRecTest] start failed: " + err); Environment.Exit(4); }
				Mod.Logger.Info("[FightRecTest] recording to " + path);
			}
			if (_t > 20 && _t <= 20 + RunTicks)
			{
				int k = _t - 20;
				// scripted commander: weave left and right, jump every 120 ticks, dash at 300 and 600, item use every 40, one hit at 450
				float ph = k / 90f;
				p.position.X = cx * 16f + (float)Math.Sin(ph) * 300f; p.velocity.X = (float)Math.Cos(ph) * 3.3f; p.direction = p.velocity.X >= 0 ? 1 : -1;
				p.controlLeft = p.velocity.X < 0; p.controlRight = p.velocity.X >= 0;
				bool air = k % 120 < 30; p.jump = k % 120 == 0 ? 15 : 0;
				p.velocity.Y = air ? -2f : 0f; p.position.Y = Floor * 16f - p.height - (air ? 40f : 0f);
				p.dashDelay = (k == 300 || k == 600) ? -1 : 0; p.dash = 2;
				p.itemAnimation = k % 40 < 10 ? 10 - k % 40 : 0; p.selectedItem = 0; p.inventory[0] = new Item(ItemID.Zenith);
				p.armor[3] = new Item(ItemID.CloudinaBottle); p.armor[4] = new Item(ItemID.EoCShield);
				p.statLife = 500; p.active = true; p.dead = false;
				if (_boss >= 0 && k < 700) // scripted boss: circles the player, "dashes" (a speed spike) every 150 ticks
				{
					NPC b = Main.npc[_boss]; b.active = true; b.timeLeft = 3600; b.life = b.lifeMax; b.ai[0] = k / 150; b.ai[1] = k % 150;
					float ang = k / 70f; bool dash = k % 150 < 20;
					Vector2 target = p.Center + new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang) * 0.5f - 0.6f) * 400f;
					Vector2 v = dash ? (p.Center - b.Center).SafeNormalize(Vector2.UnitX) * 18f : (target - b.Center) * 0.04f;
					b.velocity = v; b.position += v;
				}
				if (k == 450) FightRecorder.NoteHit(p, 42, _boss, 0, -1);
				if (_boss >= 0 && Main.npc[_boss].active && k >= 700) Main.npc[_boss].active = false; // the boss "dies": recorder stops by itself after its grace period
			}
			if (_t > 20 + RunTicks + 200)
			{
				if (FightRecorder.On) { Mod.Logger.Error("[FightRecTest] recorder did not auto-stop"); Environment.Exit(5); }
				Mod.Logger.Info("[FightRecTest] done: " + FightRecorder.Path);
				Environment.Exit(0);
			}
		}
	}
}
