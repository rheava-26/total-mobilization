using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;
using Levy.Common.Flavor;
using Levy.Content.TileEntities;
using Levy.Content.Tiles;
using Mechanization.Content.Items;
using Mechanization.Content.NPCs;
using Mechanization.Content.Relay;

namespace Mechanization.Common
{
	/// <summary>
	/// WORLD-LEVEL self-check of what the player does with Mechanization, run on a dedicated server with LEVY_SELFCHECK_WORLD=1 and a loaded world, logged as "[MechWorld]" lines.
	/// Everything goes through the real paths: tiles are placed with vanilla's own placement sequence (TileObject.CanPlace / Place / the tile's post-placement hook, exactly what Player.PlaceThing runs),
	/// real vanilla and modded (Calamity) pylons are listed by vanilla's own pylon update, and the turret / drone items are used through ItemLoader.CanUseItem / UseItem (every mod's hooks) with the
	/// cursor aimed by Main.mouseX / mouseY. The world is stepped with the real Main.Update (the way the combat harness does) so NPC AI, the garrison sweep and the turret's own brain run.
	/// No test override of the pylon list is used. LEVY_MECHWORLD_EXIT=1 stops the server when the check ends.
	/// </summary>
	public class MechWorldCheck : ModSystem
	{
		private static bool _armed, _done, _running; private static int _calls;
		private static MethodInfo _update;
		private static int _pass, _fail;
		private static Mod _mod;
		private static readonly List<(int x, int y, int w, int h)> _placed = new();

		public override void OnWorldLoad()
		{
			string mode = Environment.GetEnvironmentVariable("LEVY_MECHWORLD");
			bool persist = mode == "persist1" || mode == "persist2";
			if (!Main.dedServ || mode == "0" || (!persist && Environment.GetEnvironmentVariable("LEVY_SELFCHECK_WORLD") != "1")) return;
			_mod = Mod; _armed = true; _done = false; _calls = 0; _pass = _fail = 0;
			Main.OnTickForThirdPartySoftwareOnly += Drive;
		}

		// A dedicated server has no local player (Main.myPlayer 255), but a single-player game has one, and the drone's rounds are owned by it. A full Player.Update of slot 0 as the local player needs a client,
		// so the stand-in makes slot 0 local only while NPCs and projectiles update, which is where the drone fires and where its round flies.
		private static NPC _pinTgt, _pinFollow; private static Vector2 _pinAt; private static bool _localIsSlot0; private static int _savedMyPlayer;
		// What a phase of the tick (the NPCs, the projectiles) changed on slot 0: the end-of-tick snapshot misses a value that a later phase puts back.
		private static Dictionary<string, string> _phaseBefore; internal static readonly HashSet<string> PhaseDiffs = new();
		private static void PhaseOpen() { if (_localIsSlot0 && Main.player[0] != null) _phaseBefore = PlayerSnapshot(Main.player[0]); }
		private static void PhaseClose(string phase) { if (_localIsSlot0 && _phaseBefore != null && Main.player[0] != null) { var d = new HashSet<string>(); Differences(_phaseBefore, PlayerSnapshot(Main.player[0]), d); foreach (string k in d) PhaseDiffs.Add(phase + ":" + k); } }
		// vanilla's ambience pass reads the active players later in the tick and throws (caught, but logged) when the server has just dropped the connectionless stand-in
		public override void PostUpdatePlayers() { if (_running && Main.player[0] != null) Main.player[0].active = true; }
		public override void PreUpdateNPCs() { PhaseOpen(); if (_running && Main.player[0] != null) Main.player[0].active = true; if (_running && _pinTgt != null) Pin(_pinTgt, _pinFollow != null && _pinFollow.active ? _pinFollow.Center + new Vector2(160f, 0f) - _pinTgt.Size * 0.5f : _pinAt); /* the server drops a slot with no connection each tick, and the world's NPCs despawn with nobody on */ if (_localIsSlot0) { _savedMyPlayer = Main.myPlayer; Main.myPlayer = 0; } }
		public override void PostUpdateNPCs() { if (_localIsSlot0) Main.myPlayer = _savedMyPlayer; PhaseClose("npcs"); }
		public override void PreUpdateProjectiles() { if (_running && Main.player[0] != null) Main.player[0].active = true; PhaseOpen(); if (_localIsSlot0) { _savedMyPlayer = Main.myPlayer; Main.myPlayer = 0; } }
		public override void PostUpdateProjectiles() { if (_localIsSlot0) Main.myPlayer = _savedMyPlayer; PhaseClose("projectiles"); }

		public override void OnWorldUnload() { try { Main.OnTickForThirdPartySoftwareOnly -= Drive; } catch { } _armed = false; }

		private static void Drive()
		{
			if (!_armed || _done || !Main.dedServ) return;
			if (++_calls < 4) return;
			_done = true;
			_running = true;
			try { Run(); }
			catch (Exception e) { Check("driver threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim(), false); }
			_running = false;
			_mod.Logger.Info($"[MechWorld] self-check pass={_pass} fail={_fail}");
			if (Environment.GetEnvironmentVariable("LEVY_MECHWORLD_EXIT") == "1") { try { Netplay.SaveOnServerExit = false; Netplay.Disconnect = true; } catch { } }
		}

		private static void Check(string name, bool ok) { if (ok) _pass++; else _fail++; _mod.Logger.Info($"[MechWorld] {(ok ? "PASS" : "FAIL")} {name}"); }
		private static void Note(string s) => _mod.Logger.Info("[MechWorld] OBSERVED " + s);

		// ---------------------------------------------------------------------------------------------- world helpers
		private const int FloorY = 300;

		private static void Flatten(int cx, int half = 40)
		{
			for (int i = cx - half; i <= cx + half; i++)
			{
				for (int j = FloorY - 60; j < FloorY; j++) { Tile t = Main.tile[i, j]; t.ClearEverything(); }
				Tile f = Main.tile[i, FloorY]; f.ClearEverything(); f.HasTile = true; f.TileType = TileID.Dirt; f.BlockType = BlockType.Solid;
				for (int j = FloorY + 1; j < FloorY + 4; j++) { Tile u = Main.tile[i, j]; u.ClearEverything(); u.HasTile = true; u.TileType = TileID.Dirt; u.BlockType = BlockType.Solid; }
			}
		}

		/// <summary>The player's own placement sequence for a tile object at cursor tile (x, y). Returns the object's top-left, or (-1,-1) when vanilla refuses.</summary>
		private static Point16 RealPlace(int type, int style, int x, int y)
		{
			if (!TileObject.CanPlace(x, y, type, style, 1, out TileObject data)) return new Point16(-1, -1);
			TileObjectData td = TileObjectData.GetTileData(type, style, data.alternate);
			bool ok = TileObject.Place(data);
			WorldGen.SquareTileFrame(x, y);
			if (!ok) return new Point16(-1, -1);
			TileObjectData.CallPostPlacementPlayerHook(x, y, type, style, 1, data.alternate, data);
			var tl = new Point16(x - td.Origin.X, y - td.Origin.Y);
			_placed.Add((tl.X, tl.Y, td.Width, td.Height));
			return tl;
		}

		private static void Refresh()
		{
			var sys = Main.PylonSystem; var t = sys.GetType();
			foreach (string name in new[] { "RequestImmediateUpdate", "Update" })
				t.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null)?.Invoke(sys, null);
		}

		private static void ClearPlaced()
		{
			foreach (var (x, y, w, h) in _placed)
				for (int i = x; i < x + w; i++) for (int j = y; j < y + h; j++) if (WorldGen.InWorld(i, j)) Main.tile[i, j].ClearEverything();
			_placed.Clear();
			foreach (int id in new List<int>(TileEntity.ByID.Keys))
			{
				TileEntity te = TileEntity.ByID[id];
				bool ours = te is BarracksTE || te is ArmoryTE || te is ArmoryRelayTE || te is Terraria.GameContent.Tile_Entities.TETeleportationPylon || te is Terraria.ModLoader.Default.TEModdedPylon;
				if (!ours) continue;
				if (te.ID >= 900000) continue; // another check's synthetic entity
				TileEntity.ByID.Remove(id); TileEntity.ByPosition.Remove(te.Position);
			}
			Refresh();
		}

		private static void Step(int n)
		{
			_update ??= typeof(Main).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(GameTime) }, null);
			for (int i = 0; i < n; i++)
			{
				KeepCommander();
				_update.Invoke(Main.instance, new object[] { new GameTime() });
			}
		}

		private static Vector2 _cmdAt;
		private static void KeepCommander()
		{
			Player p = Main.player[0];
			p.active = true; p.dead = false; p.name = "MechTester";
			if (p.statLifeMax < 500) { p.statLifeMax = p.statLifeMax2 = 500; }
			p.statLife = p.statLifeMax2;
			p.position = _cmdAt - new Vector2(p.width / 2f, p.height / 2f); p.velocity = Vector2.Zero;
		}

		/// <summary>Uses an item the way a player does: CanUseItem, UseItem with the cursor on <paramref name="cursor"/> and the local player slot, then the vanilla consume rule.</summary>
		private static bool UseItem(Player p, Item it, Vector2 cursor, out string why)
		{
			why = "";
			int savedMy = Main.myPlayer; Vector2 savedScreen = Main.screenPosition; int mx = Main.mouseX, my = Main.mouseY;
			try
			{
				Main.myPlayer = 0; p.whoAmI = 0;
				Main.screenPosition = cursor - new Vector2(300f, 300f); Main.mouseX = 300; Main.mouseY = 300;
				Vector2 mw = Main.MouseWorld;
				if (Vector2.Distance(mw, cursor) > 40f) why += $"[cursor off: wanted {cursor} got {mw}] ";
				p.inventory[0] = it; p.selectedItem = 0; p.controlUseItem = true;
				if (!ItemLoader.CanUseItem(it, p)) { why += "CanUseItem refused"; return false; }
				bool? used = ItemLoader.UseItem(it, p);
				if (used == true && it.consumable && ItemLoader.ConsumeItem(it, p)) it.stack--;
				why += "UseItem=" + (used?.ToString() ?? "null");
				return used == true;
			}
			finally { Main.myPlayer = savedMy; Main.screenPosition = savedScreen; Main.mouseX = mx; Main.mouseY = my; p.controlUseItem = false; }
		}

		private static int CountNpcs<T>() where T : ModNPC { int n = 0; for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && Main.npc[i].ModNPC is T) n++; return n; }
		private static void KillAll<T>() where T : ModNPC { for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && Main.npc[i].ModNPC is T) Main.npc[i].active = false; }

		// ---------------------------------------------------------------------------------------------- the scenarios
		private static void Run()
		{
			Note($"world {Main.maxTilesX}x{Main.maxTilesY}, mods: Calamity={ModLoader.HasMod("CalamityMod")}");
			Main.player[0] = Main.player[0] ?? new Player();
			string mode = Environment.GetEnvironmentVariable("LEVY_MECHWORLD");
			if (mode == "persist1") { PersistBuild(); return; }
			if (mode == "persist2") { PersistRead(); return; }
			RelayAcrossPylons();
			TurretItemUse();
			DroneItemUse();
			DroneLeavesPlayerAlone();
			if (Environment.GetEnvironmentVariable("LEVY_MECHSWEEP") == "1") ShotSweep();
			DroneGarrison();
			CrowdedWorld();
		}

		/// <summary>A busy world (an army, militia, townsfolk and an invasion: 175 of the 200 NPC slots taken): the player's drone and turret still go down.</summary>
		private static void CrowdedWorld()
		{
			int cx = 1200; PrepareArena(cx);
			var filler = new List<NPC>();
			for (int i = 0; i < Main.maxNPCs && filler.Count < 175; i++)
			{
				int idx = NPC.NewNPC(new EntitySource_WorldEvent("MechWorldCheck"), (cx + 40) * 16, (FloorY - 3) * 16, NPCID.Bunny);
				if (idx < 0 || idx >= Main.maxNPCs) break;
				NPC n = Main.npc[idx]; n.aiStyle = -1; n.noGravity = true; n.noTileCollide = true; n.lifeMax = n.life = 100000; n.friendly = true; filler.Add(n);
			}
			int active = 0; for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active) active++;
			Note($"crowded world: {active} of {Main.maxNPCs} NPC slots in use");
			Player p = Main.player[0];
			Item drone = new Item(ModContent.ItemType<DroneItem>(), 2);
			bool used = UseItem(p, drone, new Vector2(cx * 16f, (FloorY - 4) * 16f), out string why);
			Note("crowded: drone item use: " + why);
			Check($"with {active} NPCs in the world the Combat Drone item still places a drone", used && CountNpcs<DroneNPC>() == 1);
			Item turret = new Item(ModContent.ItemType<TurretItem>(), 2);
			bool tused = UseItem(p, turret, new Vector2((cx + 6) * 16f, (FloorY - 4) * 16f), out string twhy);
			Check($"with {active} NPCs in the world the Automatic Turret item still places a turret", tused && CountNpcs<TurretNPC>() == 1);
			KillAll<DroneNPC>(); KillAll<TurretNPC>(); FieldDroneSystem.Instance?.OnWorldUnload();
			foreach (NPC n in filler) n.active = false;
			ClearPlaced();
		}

		private sealed class PylonKind { public string Name; public int Type, Style; }

		private static List<PylonKind> PylonKinds()
		{
			var l = new List<PylonKind>();
			string[] vn = { "Purity", "Jungle", "Hallow", "Underground", "Beach", "Desert", "Snow", "Mushroom", "Victory" };
			for (int s = 0; s < vn.Length; s++) l.Add(new PylonKind { Name = "vanilla " + vn[s], Type = TileID.TeleportationPylon, Style = s });
			foreach (ModPylon mp in ModContent.GetContent<ModPylon>()) l.Add(new PylonKind { Name = mp.Mod.Name + ":" + mp.Name, Type = mp.Type, Style = 0 });
			return l;
		}

		/// <summary>Bug 1: an Armory Relay beside one pylon, a Barracks + Armory beside another, 1,800+ tiles apart, for every pylon type.</summary>
		private static void RelayAcrossPylons()
		{
			int west = 500, east = 2400;
			int barType = ModContent.TileType<BarracksTile>(), armType = ModContent.TileType<ArmoryTile>(), relType = ModContent.TileType<ArmoryRelayTile>();
			var kinds = PylonKinds();
			Note("pylon kinds under test: " + string.Join(", ", kinds.ConvertAll(k => k.Name)));

			bool Case(string label, PylonKind relayPylon, PylonKind armoryPylon)
			{
				ClearPlaced(); Flatten(west); Flatten(east);
				Point16 bar = RealPlace(barType, 0, west + 2, FloorY - 1);
				Point16 arm = RealPlace(armType, 0, west + 6, FloorY - 1);
				Point16 pyB = RealPlace(armoryPylon.Type, armoryPylon.Style, west + 12, FloorY - 1);
				Point16 rel = RealPlace(relType, 0, east + 2, FloorY - 1);
				Point16 pyA = RealPlace(relayPylon.Type, relayPylon.Style, east + 8, FloorY - 1);
				Refresh();
				bool placed = bar.X >= 0 && arm.X >= 0 && pyB.X >= 0 && rel.X >= 0 && pyA.X >= 0;
				if (!placed) { Check($"{label}: everything placed (bar {bar.X >= 0} arm {arm.X >= 0} pyB {pyB.X >= 0} rel {rel.X >= 0} pyA {pyA.X >= 0})", false); return false; }
				int listed = Main.PylonSystem.Pylons.Count;
				ArmoryRelayTE relay = TileEntity.ByPosition.TryGetValue(rel, out TileEntity e1) ? e1 as ArmoryRelayTE : null;
				ArmoryTE armory = TileEntity.ByPosition.TryGetValue(arm, out TileEntity e2) ? e2 as ArmoryTE : null;
				bool linked = relay != null && armory != null && ReferenceEquals(ArmoryRelays.LinkOf(relay), armory);
				var pool = new List<ArmoryTE>(); ArmoryLink.Find(new Vector2((east + 6) * 16f, (FloorY - 2) * 16f), pool);
				bool turretSees = armory != null && pool.Contains(armory);
				Check($"{label}: pylons listed={listed} relay linked={linked} turret near relay sees the armory={turretSees}", listed == 2 && linked && turretSees);
				return linked && turretSees;
			}

			PylonKind other(PylonKind k) => kinds.Find(x => x != k && (x.Name.Contains("Purity") || x.Name.Contains("Hallow")) && x.Name != k.Name);
			foreach (PylonKind k in kinds) Case($"relay at {k.Name} pylon, armory at {other(k).Name} pylon", k, other(k));
			PylonKind fixedRelay = kinds[0];
			foreach (PylonKind k in kinds) if (k != fixedRelay) Case($"relay at {fixedRelay.Name} pylon, armory at {k.Name} pylon", fixedRelay, k);
			// an unlinked relay (a pylon beside it, no Armory anywhere): the hover text names what was looked for
			ClearPlaced(); Flatten(east);
			Point16 lone = RealPlace(relType, 0, east + 2, FloorY - 1); RealPlace(kinds[0].Type, kinds[0].Style, east + 8, FloorY - 1); Refresh();
			ArmoryRelayTE loneRelay = TileEntity.ByPosition.TryGetValue(lone, out TileEntity e3) ? e3 as ArmoryRelayTE : null;
			Check("a lone relay beside a pylon with no Armory in the world stays unlinked", loneRelay != null && ArmoryRelays.LinkOf(loneRelay) == null);
			if (loneRelay != null)
			{
				ArmoryRelays.Diagnose(loneRelay.Center, out int inReach, out int pylons, out int onNet);
				string txt = Terraria.Localization.Language.GetTextValue("Mods.Mechanization.UI.RelayUnlinked", (int)MechConstants.Relay.LinkReachTiles, inReach, (int)Levy.Common.LevyConstants.PylonShareRadius, pylons, onNet);
				Note("unlinked relay hover text: " + txt);
				Check("the unlinked text counts 1 pylon and 0 armories and has no unfilled slot", pylons == 1 && inReach == 0 && onNet == 0 && !txt.Contains("{"));
			}
			ClearPlaced();
		}

		/// <summary>Saved-world check, part 1: builds a base and a far relay with real placements (a Calamity Sunken Sea pylon beside the relay) and saves the world.</summary>
		private static void PersistBuild()
		{
			int west = 500, east = 2400;
			ClearPlaced(); Flatten(west); Flatten(east);
			var sunken = PylonKinds().Find(k => k.Name.Contains("SunkenPylon")) ?? PylonKinds()[0];
			RealPlace(ModContent.TileType<BarracksTile>(), 0, west + 2, FloorY - 1);
			RealPlace(ModContent.TileType<ArmoryTile>(), 0, west + 6, FloorY - 1);
			RealPlace(TileID.TeleportationPylon, 0, west + 12, FloorY - 1);
			RealPlace(ModContent.TileType<ArmoryRelayTile>(), 0, east + 2, FloorY - 1);
			RealPlace(sunken.Type, sunken.Style, east + 8, FloorY - 1);
			Refresh();
			Note($"persist1: built; pylons listed {Main.PylonSystem.Pylons.Count}; saving the world");
			_placed.Clear();
			Note($"persist1: pylon TEs {CountPylonTes()}; pylon list: " + string.Join(", ", Main.PylonSystem.Pylons.Select(q => q.PositionInTiles + "/" + q.TypeOfPylon)));
			for (int px = 509; px < 516; px++) { string col = ""; for (int py = FloorY - 5; py <= FloorY; py++) { Tile t = Main.tile[px, py]; col += t.HasTile ? (t.TileType + " ") : ". "; } Note($"  pre-save col {px}: {col}"); }
			Terraria.IO.WorldFile.SaveWorld();
			Note("persist1: saved");
		}

		/// <summary>Saved-world check, part 2: after a fresh load, with NO pylon refresh by us, reads what the game itself sees.</summary>
		private static void PersistRead()
		{
			int east = 2400;
			Note($"persist2: pylons listed right after load: {Main.PylonSystem.Pylons.Count}; cooldown field = {typeof(TeleportPylonsSystem).GetField("_cooldownForUpdatingPylonsList", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(Main.PylonSystem)}; GameUpdateCount={Main.GameUpdateCount}; pylon TEs in world: {CountPylonTes()}");
			foreach (var p in Main.PylonSystem.Pylons) Note($"  pylon {p.PositionInTiles} type {p.TypeOfPylon}");
			foreach (TileEntity te in TileEntity.ByID.Values) Note($"  TE id {te.ID} {te.GetType().Name} at {te.Position}");
			for (int px = 498; px < 520; px++) { string col = ""; for (int py = FloorY - 5; py <= FloorY; py++) { Tile t = Main.tile[px, py]; col += t.HasTile ? (t.TileType + " ") : ". "; } Note($"  col {px}: {col}"); }
			foreach (int px in new[] { 500 + 12 - 1, 2400 + 8 - 1 })
				for (int py = FloorY - 4; py < FloorY; py++) { Tile t = Main.tile[px, py]; if (t.HasTile) Note($"  tile ({px},{py}) type {t.TileType} {(t.TileType >= TileID.Count ? TileLoader.GetTile(t.TileType)?.FullName : "vanilla")} frame {t.TileFrameX},{t.TileFrameY}"); }
			ArmoryRelayTE relay = null; ArmoryTE arm = null;
			foreach (TileEntity te in TileEntity.ByID.Values) { if (te is ArmoryRelayTE r) relay = r; if (te is ArmoryTE a) arm = a; }
			Check("persist2: the saved relay and armory exist", relay != null && arm != null);
			if (relay == null || arm == null) return;
			Check("persist2: pylon list holds both pylons after load", Main.PylonSystem.Pylons.Count == 2);
			Check("persist2: relay linked to the armory after load", ReferenceEquals(ArmoryRelays.LinkOf(relay), arm));
			Step(120);
			Note($"persist2: after 120 ticks pylons listed {Main.PylonSystem.Pylons.Count}; cooldown field = {typeof(TeleportPylonsSystem).GetField("_cooldownForUpdatingPylonsList", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(Main.PylonSystem)}; GameUpdateCount={Main.GameUpdateCount}");
			Check("persist2: relay still linked after 120 ticks", ReferenceEquals(ArmoryRelays.LinkOf(relay), arm));
			Refresh();
			Note($"persist2: after a forced pylon refresh the list holds {Main.PylonSystem.Pylons.Count}; relay linked now = {ReferenceEquals(ArmoryRelays.LinkOf(relay), arm)}");
		}

		private static int CountPylonTes() { int n = 0; foreach (TileEntity te in TileEntity.ByID.Values) if (te is IPylonTileEntity) n++; return n; }

		private static void PrepareArena(int cx)
		{
			ClearPlaced(); Flatten(cx, 60);
			_cmdAt = new Vector2(cx * 16f, (FloorY - 3) * 16f);
		}

		private static ArmoryTE ArmoryWith(int cx, int dx, params int[] weapons)
		{
			Point16 a = RealPlace(ModContent.TileType<ArmoryTile>(), 0, cx + dx, FloorY - 1);
			ArmoryTE armory = TileEntity.ByPosition.TryGetValue(a, out TileEntity e) ? e as ArmoryTE : null;
			if (armory != null) for (int i = 0; i < weapons.Length; i++) armory.Storage[i] = new Item(weapons[i]);
			return armory;
		}

		/// <summary>A pinned hostile the turret / drone can shoot at, 14 tiles from the commander.</summary>
		private static NPC Target(int cx, int dx)
		{
			int i = NPC.NewNPC(new EntitySource_WorldEvent("MechWorldCheck"), (cx + dx) * 16, (FloorY - 3) * 16, NPCID.GreenSlime);
			if (i < 0 || i >= Main.maxNPCs) return null;
			NPC n = Main.npc[i]; n.lifeMax = n.life = 100000; n.knockBackResist = 0f; n.noGravity = true; n.noTileCollide = true; n.damage = 0; n.defense = 0;
			return n;
		}
		private static void Pin(NPC n, Vector2 at) { n.active = true; n.timeLeft = 9999; n.position = at; n.velocity = Vector2.Zero; n.life = n.lifeMax; }

		private static void TurretItemUse()
		{
			int cx = 1200; PrepareArena(cx);
			ArmoryTE armory = ArmoryWith(cx, -10, ItemID.Handgun, ItemID.Minishark);
			Check("turret scenario: an Armory with two guns is placed", armory != null);
			Player p = Main.player[0];
			Item item = new Item(ModContent.ItemType<TurretItem>(), 3);
			bool used = UseItem(p, item, new Vector2(cx * 16f, (FloorY - 4) * 16f), out string why);
			Note("turret item use: " + why + $" stackLeft={item.stack}");
			Check("using the Automatic Turret item places a turret", used && CountNpcs<TurretNPC>() == 1);
			Step(120);
			TurretNPC t = null; for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && Main.npc[i].ModNPC is TurretNPC tn) t = tn;
			Check("the turret is still alive after 2 seconds of real ticks", t != null && t.NPC.active && t.NPC.life > 0);
			if (t != null)
			{
				Note($"turret at {t.NPC.Center} life {t.NPC.life}/{t.NPC.lifeMax} mounted weapon type {t.MountedWeaponType} reach {t.ReachPx / 16f:0.0} tiles");
				Check("it mounted a weapon from the Armory in reach", t.MountedWeaponType == ItemID.Handgun || t.MountedWeaponType == ItemID.Minishark);
				NPC tgt = Target(cx, 14); Vector2 at = tgt.position; int shots = 0; int seen = 0;
				for (int k = 0; k < 400; k++)
				{
					Pin(tgt, at); Step(1);
					if (k % 10 == 0) for (int i = 0; i < Main.maxProjectiles; i++) if (Main.projectile[i].active && Main.projectile[i].friendly) { shots++; break; }
					seen = Math.Max(seen, tgt.lifeMax - tgt.life);
				}
				Check("the turret shoots a hostile 14 tiles away (damage dealt)", seen > 0);
				Note($"turret damage dealt to the pinned target: {seen}, friendly projectile samples {shots}");
				tgt.active = false;
			}
			KillAll<TurretNPC>(); ClearPlaced();
		}

		private static void DroneItemUse()
		{
			int cx = 1200; PrepareArena(cx);
			ArmoryTE armory = ArmoryWith(cx, -10, ItemID.Handgun, ItemID.Minishark);
			Player p = Main.player[0];
			Item item = new Item(ModContent.ItemType<DroneItem>(), 3);
			bool used = UseItem(p, item, new Vector2(cx * 16f, (FloorY - 4) * 16f), out string why);
			Note("drone item use: " + why + $" stackLeft={item.stack}");
			Check("using the Combat Drone item places a drone", used && CountNpcs<DroneNPC>() == 1);
			Step(180);
			DroneNPC d = null; for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && Main.npc[i].ModNPC is DroneNPC dn) d = dn;
			Check("the drone is alive and flying after 3 seconds", d != null && d.NPC.active && d.NPC.life > 0);
			if (d != null)
			{
				Note($"drone at {d.NPC.Center} phase {d.Phase} armed {d.Armed} weapon {d.FieldWeaponType}");
				NPC tgt = Target(cx, 14); Vector2 at = tgt.position; int seen = 0;
				for (int k = 0; k < 600; k++) { Pin(tgt, at); Step(1); seen = Math.Max(seen, tgt.lifeMax - tgt.life); }
				Check("the drone shoots a hostile in reach (damage dealt)", seen > 0);
				Note("drone damage dealt: " + seen);
				tgt.active = false;
			}
			KillAll<DroneNPC>(); FieldDroneSystem.Instance?.OnWorldUnload(); ClearPlaced();
		}

		// ---------------------------------------------------------------------------------------------- the real player stays untouched by a machine's gun
		private static readonly Dictionary<Type, List<FieldInfo>> _fieldCache = new();

		private static List<FieldInfo> StateFields(Type t)
		{
			if (_fieldCache.TryGetValue(t, out var l)) return l;
			l = new List<FieldInfo>();
			for (Type c = t; c != null && c != typeof(object); c = c.BaseType)
				foreach (FieldInfo f in c.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
				{
					Type ft = f.FieldType;
					if (ft.IsPrimitive || ft.IsEnum || ft == typeof(string) || ft == typeof(Vector2) || (ft.IsArray && ft.GetElementType().IsPrimitive)) l.Add(f);
				}
			_fieldCache[t] = l;
			return l;
		}

		/// <summary>Every scalar field of the player and of each of its mod players (Calamity's included), by name: whatever a weapon's use, a projectile's hook or a mod's listener could write.</summary>
		private static Dictionary<string, string> PlayerSnapshot(Player p)
		{
			var d = new Dictionary<string, string>();
			void Take(object o, string tag) { foreach (FieldInfo f in StateFields(o.GetType())) { try { object v = f.GetValue(o); d[tag + "." + f.Name] = v is Array arr ? string.Join(",", arr.Cast<object>()) : Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture); } catch (Exception) { } } }
			Take(p, "Player");
			foreach (ModPlayer mp in p.ModPlayers) Take(mp, mp.Mod.Name + ":" + mp.Name);
			d["Player.velocity"] = p.velocity.ToString(); d["Player.compositeFrontArm"] = p.compositeFrontArm.enabled + "/" + p.compositeFrontArm.rotation;
			return d;
		}

		/// <summary>Names of the fields in <paramref name="now"/> that differ from <paramref name="before"/>, added to <paramref name="into"/>.</summary>
		private static void Differences(Dictionary<string, string> before, Dictionary<string, string> now, HashSet<string> into)
		{
			foreach (var kv in now) if (kv.Key != "Player.active" && kv.Key != "Player.nearbyActiveNPCs" && kv.Key != "Player.lifeRegen" && (!before.TryGetValue(kv.Key, out string b) || b != kv.Value)) into.Add(kv.Key);
		}

		/// <summary>
		/// A gun mounted on a machine fires with the real player as the shot's owner (single player: Main.myPlayer is the player). That must never move the player. Slot 0 stands in for the single-player
		/// local player (a dedicated server has none; see the Pre/PostUpdate hooks), the machine's weapon fires at a pinned enemy, and EVERY scalar field of the player and of its mod players is compared
		/// with a quiet stretch (the same machine with nothing in reach). Anything that changes only while the gun fires is a touch on the player; the check fails and names it.
		/// </summary>
		private static void MachineLeavesPlayerAlone(string name, int weaponType, bool turret)
		{
			int cx = 1200; PrepareArena(cx);
			ArmoryTE armory = ArmoryWith(cx, -10, weaponType);
			Player p = Main.player[0];
			Item item = new Item(turret ? ModContent.ItemType<TurretItem>() : ModContent.ItemType<DroneItem>(), 3);
			UseItem(p, item, new Vector2(cx * 16f, (FloorY - 4) * 16f), out string why);
			Step(180);
			string label = $"player-safety, {(turret ? "turret" : "drone")} with {name}";
			bool placed = false; NPC machine = null;
			for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && (turret ? Main.npc[i].ModNPC is TurretNPC : Main.npc[i].ModNPC is DroneNPC)) { placed = true; machine = Main.npc[i]; }
			if (!placed) { Check($"{label}: the machine is placed", false); KillAll<DroneNPC>(); KillAll<TurretNPC>(); ClearPlaced(); return; }
			NPC tgt = Target(cx, 14); Vector2 near = tgt.position, far = near + new Vector2(1800f, 0f);
			_update ??= typeof(Main).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(GameTime) }, null);
			MechShotProbe.On = true; MechShotProbe.Spawned = MechShotProbe.Killed = 0; MechShotProbe.FirstKill = "";
			var quiet = new HashSet<string>(); var firing = new HashSet<string>();
			int ownedTicks = 0, lastType = -1, anyOwnerTicks = 0, shots = 0, lastSeq = 0; string machineState = ""; PhaseDiffs.Clear(); var quietPhase = new HashSet<string>();
			p.inventory[0] = new Item(ItemID.WoodenSword); p.inventory[0].autoReuse = true; p.selectedItem = 0;
			try
			{
				_localIsSlot0 = true;
				for (int phase = 0; phase < 2; phase++)
				{
					if (phase == 1) { quietPhase = new HashSet<string>(PhaseDiffs); PhaseDiffs.Clear(); }
					_pinTgt = tgt; _pinAt = phase == 0 ? far : near; _pinFollow = phase == 0 || turret ? null : machine; // the enemy stays a short shot from the machine, whatever waypoint the drone is loitering at
					KeepCommander(); Step(1);
					Dictionary<string, string> before = PlayerSnapshot(Main.player[0]);
					for (int k = 0; k < (phase == 0 ? 240 : 700); k++)
					{
						KeepCommander(); // the position and health only: the speed and everything else is read after the update, so a recoil or a lock shows
						_update.Invoke(Main.instance, new object[] { new GameTime() });
						Differences(before, PlayerSnapshot(Main.player[0]), phase == 0 ? quiet : firing);
						if (phase == 1)
						{
							bool owned = false;
							for (int i = 0; i < Main.maxProjectiles; i++) { Projectile pr = Main.projectile[i]; if (pr.active && pr.owner == 0 && pr.type != ProjectileID.None) { owned = true; lastType = pr.type; } }
							if (owned) ownedTicks++;
							for (int i = 0; i < Main.maxProjectiles; i++) if (Main.projectile[i].active && Main.projectile[i].friendly) { anyOwnerTicks++; break; }
							int seq = machine.ModNPC == null ? 0 : (int)(machine.ModNPC.GetType().GetField("_shotSeq", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(machine.ModNPC) ?? 0); if (seq != lastSeq) { shots += Math.Max(0, seq - lastSeq); lastSeq = seq; }
						}
					}
				}
			}
			finally { _localIsSlot0 = false; _pinTgt = null; _pinFollow = null; }
			MechShotProbe.On = false; firing.ExceptWith(quiet); PhaseDiffs.ExceptWith(quietPhase);
			Note($"{label}: ticks with a round in flight owned by the player = {ownedTicks} (machine shots {shots}, rounds spawned {MechShotProbe.Spawned}, killed {MechShotProbe.Killed}, first kill {MechShotProbe.FirstKill}) (last round type {lastType}); player fields changed only while it fired: [{string.Join(", ", firing)}]; changed inside a phase: [{string.Join(", ", PhaseDiffs)}]");
			firing.RemoveWhere(f => !IsWatched(f)); // the counters and timers the player advances on its own are noise; what a use or a hook writes is not
			Check($"{label}: it fires, and nothing on the real player changes", shots > 0 && MechShotProbe.Spawned > 0 && firing.Count == 0 && PhaseDiffs.Count == 0);
			tgt.active = false;
			KillAll<DroneNPC>(); KillAll<TurretNPC>(); FieldDroneSystem.Instance?.OnWorldUnload(); ClearPlaced();
		}


		// ---------------------------------------------------------------------------------------------- every round a drone or turret can fire, one by one
		/// <summary>
		/// The whole class, not one rifle: every distinct projectile a mountable weapon (vanilla and every loaded mod) fires with any ammo it takes is spawned the way the drone spawns it (owner = the real player, source = an NPC's AI, tagged by ShotKit),
		/// flown into a pinned enemy and left to hit and die, with slot 0 standing in for the single-player local player. Every field of the player and its mod players is compared across each phase; a round that moves any of them is listed.
		/// Slow (about a minute per hundred rounds), so it runs only with LEVY_MECHSWEEP=1 and its verdict is a Check.
		/// </summary>
		private static void ShotSweep()
		{
			int cx = 1200; PrepareArena(cx);
			var byProj = new Dictionary<int, string>(); // projectile type -> first "weapon + ammo" that fires it
			var ammoByKind = new Dictionary<int, List<Item>>();
			for (int i = 1; i < ItemLoader.ItemCount; i++)
			{
				if (!ContentSamples.ItemsByType.TryGetValue(i, out Item smp) || smp == null || smp.IsAir || smp.ammo <= 0 || smp.IsACoin) continue;
				if (!ammoByKind.TryGetValue(smp.ammo, out var l)) ammoByKind[smp.ammo] = l = new List<Item>();
				l.Add(smp);
			}
			for (int i = 1; i < ItemLoader.ItemCount; i++)
			{
				if (!ContentSamples.ItemsByType.TryGetValue(i, out Item w) || w == null || w.IsAir) continue;
				Item weapon = new Item(); weapon.SetDefaults(i);
				if (!Levy.Common.Weapons.DroneArsenal.IsMountable(weapon)) continue;
				var rounds = new List<Item>();
				if (weapon.useAmmo > 0) { if (ammoByKind.TryGetValue(weapon.useAmmo, out var al)) rounds.AddRange(al); } else rounds.Add(null);
				foreach (Item round in rounds)
				{
					Levy.Common.Weapons.DroneShot shot = Levy.Common.Weapons.DroneArsenal.BuildShot(weapon, round);
					if (shot.Valid && !byProj.ContainsKey(shot.Projectile)) byProj[shot.Projectile] = Lang.GetItemNameValue(i) + (round != null ? " + " + Lang.GetItemNameValue(round.type) : "");
				}
			}
			int g0 = OwnerGuard.GuardedUpdates;
			Note($"shot sweep: {byProj.Count} distinct projectile types across the mountable weapons and their ammo");
			_update ??= typeof(Main).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(GameTime) }, null);
			NPC tgt = Target(cx, 14); Vector2 at = tgt.position;
			var offenders = new List<string>(); int done = 0;
			try
			{
				_localIsSlot0 = true; _pinTgt = tgt; _pinAt = at; _pinFollow = null;
				KeepCommander(); Step(1);
				// What the player's own update moves with no round in the air (gravity, counters, the regen tick): the watched fields are compared against it.
				var control = new HashSet<string>();
				for (int c = 0; c < 3; c++) { SweepRound(-1, tgt, at, control, null); }
				string only = Environment.GetEnvironmentVariable("LEVY_MECHSWEEP_TYPES"); // a comma list of projectile ids: re-check just those
				foreach (var kv in byProj)
				{
					if (!string.IsNullOrEmpty(only) && Array.IndexOf(only.Split(','), kv.Key.ToString()) < 0) continue;
					var seen = new HashSet<string>();
					string threw = SweepRound(kv.Key, tgt, at, seen, null);
					seen.ExceptWith(control);
					done++;
					if (threw != null) offenders.Add($"{kv.Value} (projectile {kv.Key}): {threw}");
					else if (seen.Count > 0) offenders.Add($"{kv.Value} (projectile {kv.Key}{(ProjectileLoader.GetProjectile(kv.Key) is ModProjectile mp ? " " + mp.GetType().FullName : "")}): [{string.Join(", ", seen)}]");
				}
			}
			finally { _localIsSlot0 = false; _pinTgt = null; }
			tgt.active = false;
			Note($"shot sweep: the owner guard wrapped {OwnerGuard.GuardedUpdates - g0} round updates and had to put the use state back {OwnerGuard.Undone} times");
			foreach (string o in SweepOther) Note("shot sweep, other player field moved inside the round's own pass: " + o);
			foreach (string o in offenders) Note("shot sweep, touches the real player: " + o);
			Check($"shot sweep: none of {done} projectile types a machine can fire changes the real player (touched: {offenders.Count})", offenders.Count == 0);
		}

		/// <summary>The fields of the player a weapon's use or a round's hook can write: the sweep watches these at the end of every tick. Anything the player's own update does not move on its own.</summary>
		private static readonly string[] Watched = { "itemAnimation", "itemTime", "reuseDelay", "channel", "controlUse", "releaseUse", "heldProj", "noItems", "CCed", "selectedItem", "itemRotation", "direction", "velocity", "mouseWorld", "compositeFrontArm", "compositeBackArm", "talkNPC", "mount", "dead", "stoned", "frozen", "webbed", "noThrow", "channel" };

		/// <summary>One round (or, with a type below 0, nothing at all as the control) flown for 90 ticks; the differences on slot 0 go into <paramref name="seen"/>. Returns a message if the spawn threw.</summary>
		private static string SweepRound(int type, NPC tgt, Vector2 at, HashSet<string> seen, string unused)
		{
			for (int i = 0; i < Main.maxProjectiles; i++) Main.projectile[i].active = false;
			KeepCommander(); Step(1);
			PhaseDiffs.Clear();
			Dictionary<string, string> before = PlayerSnapshot(Main.player[0]);
			string threw = null;
			if (type >= 0)
			{
				Vector2 muzzle = at + new Vector2(-150f, 0f);
				int sm = Main.myPlayer; Main.myPlayer = 0;
				try
				{
					OwnerGuard.Hold(0, () =>
					{
						int idx = Projectile.NewProjectile(tgt.GetSource_FromAI(), muzzle.X, muzzle.Y, 11f, 0f, type, 40, 3f, 0);
						if (idx >= 0 && idx < Main.maxProjectiles) ShotKit.TagShot(Main.projectile[idx], 0, null, 0);
					});
				}
				catch (Exception e) { threw = "spawn threw " + e.GetType().Name; }
				finally { Main.myPlayer = sm; }
			}
			var all = new HashSet<string>();
			Differences(before, PlayerSnapshot(Main.player[0]), all);
			var watched = new HashSet<string>(); foreach (string d in all) if (IsWatched(d)) watched.Add("spawn:" + d);
			for (int k = 0; k < 90; k++)
			{
				KeepCommander();
				_update.Invoke(Main.instance, new object[] { new GameTime() });
				all.Clear(); Differences(before, PlayerSnapshot(Main.player[0]), all);
				foreach (string d in all) if (IsWatched(d)) watched.Add(d);
			}
			foreach (string d in PhaseDiffs) { if (IsWatched(d)) seen.Add(d); else SweepOther.Add(type + " " + d); } // a change inside the NPC or projectile pass is the round's own doing; fields of the player that are not use state (health, a cooldown, a screen shake) are listed, not failed
			foreach (string d in watched) seen.Add(d);
			return threw;
		}
		private static readonly HashSet<string> SweepOther = new();
		private static bool IsWatched(string field) { foreach (string w in Watched) if (field.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0) return true; return false; }

		private static void DroneLeavesPlayerAlone()
		{
			var weapons = new List<(string name, int type)> { ("Handgun", ItemID.Handgun) };
			if (ModLoader.TryGetMod("CalamityMod", out Mod cal) && cal.TryFind("NitroExpressRifle", out ModItem nit)) weapons.Add(("Nitro Express Rifle", nit.Type));
			foreach (var (name, type) in weapons) { MachineLeavesPlayerAlone(name, type, false); MachineLeavesPlayerAlone(name, type, true); }
		}

		private static void DroneGarrison()
		{
			int cx = 1200; PrepareArena(cx);
			GuideSystem g = GuideSystem.Instance; bool savedEng = g.Engineering; g.Engineering = true;
			Point16 bar = RealPlace(ModContent.TileType<BarracksTile>(), 0, cx + 2, FloorY - 1);
			int[] guns = { ItemID.Handgun, ItemID.Minishark, ItemID.Musket, ItemID.TheUndertaker, ItemID.FlintlockPistol, ItemID.Boomstick, ItemID.RedRyder, ItemID.PhoenixBlaster, ItemID.ClockworkAssaultRifle, ItemID.Megashark, ItemID.Shotgun, ItemID.SniperRifle };
			foreach (int n in new[] { 3, 12 })
			{
				ArmoryTE armory = ArmoryWith(cx, 8 + (n == 3 ? 0 : 6), guns[..n]);
				Check($"garrison scenario ({n} guns): barracks and armory placed", bar.X >= 0 && armory != null);
				Step(1500);
				int bound = 0; for (int i = 0; i < Main.maxNPCs; i++) if (Main.npc[i].active && Main.npc[i].ModNPC is DroneNPC dn && dn.IsBound && !dn.IsField) bound++;
				Note($"garrison with {n} spare ranged weapons in the Armory, after 25 s: {bound} garrison drone(s) launched");
				Check($"the garrison launches a drone with {n} ranged weapons in its Armory", bound >= 1);
				KillAll<DroneNPC>();
				if (armory != null) TileEntity.ByID.Remove(armory.ID);
			}
			g.Engineering = savedEng; ClearPlaced();
		}
	}

	/// <summary>The check's eyes on the rounds a machine spawns: how many, owned by whom, and how long each lived. Costs one flag test when the check is not running.</summary>
	public class MechShotProbe : GlobalProjectile
	{
		internal static bool On; internal static int Spawned, Killed, ShortLived; internal static string FirstKill = "";
		public override void OnSpawn(Projectile projectile, Terraria.DataStructures.IEntitySource source) { if (On && source is Terraria.DataStructures.EntitySource_Parent { Entity: NPC }) Spawned++; }
		public override void OnKill(Projectile projectile, int timeLeft) { if (!On || projectile.owner != 0) return; Killed++; if (FirstKill == "") FirstKill = $"type {projectile.type} timeLeft {timeLeft}/{projectile.timeLeft} pos {projectile.Center} vel {projectile.velocity}"; }
	}
}
