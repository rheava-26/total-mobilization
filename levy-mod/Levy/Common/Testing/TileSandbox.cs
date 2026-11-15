using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Kind = Levy.Common.Weapons.WorldAlter.Kind;

namespace Levy.Common.Testing
{
	/// <summary>
	/// A throwaway patch of sky to fire things into: a block field (dirt, grass, stone, wood, platforms, cobweb, torches, glass, sand, ice, dungeon brick, water, lava, jungle grass; walls behind) built in the sky of a TEST world (tML keeps all tile data in
	/// one shared store, so a second tile map is not possible; the field and its pristine copy are two patches of the same world), with the NPC, item and projectile arrays swapped out for the length of one call, so nothing is hit or littered.
	/// Test worlds only: <see cref="Run"/> refuses to run on anything but a dedicated test server booted for the self-checks. Every test is
	/// fire -> step every projectile the shot made (children included) until all are gone -> compare the field tile by tile with its pristine copy. The measured answer to "does this alter the world".
	/// <para/>A shot is made exactly the way a levy's weapon use makes it: the game's own <c>Player.ItemCheck_Shoot</c> on a scratch player standing in slot 255 with Main.myPlayer = 255 (the owner-gated
	/// explosion and tile code runs; the real player is never the owner) in single-player mode (tile edits happen, nothing is sent). What was measured is whether the TILES changed: type, wall, liquid, slope, frame, paint, wire,
	/// actuator. A kill that only drops dust or damages NPCs changes nothing here and counts as safe.
	/// </summary>
	public static class TileSandbox
	{
		public const int W = 224, H = 110, FloorY = 60;
		/// <summary>World tile column / row of the field's top-left corner, and of its pristine copy's.</summary>
		private const int FieldX = 60, TemplateX = 1500, Top = 20;
		private static long _goldSum; public static int Repairs;
		private static int _ox = FieldX;
		private static NPC[] _npcs; private static Item[] _items; private static Projectile[] _projs; private static Player _player;
		private static bool _built;
		public static long Runs; public static double Ms;

		private struct Saved
		{
			public NPC[] Npc; public Item[] Item; public Projectile[] Proj; public Player P255; public int MyPlayer, NetMode; public bool Ded; public int NumLiquid;
		}

		public static Player Scratch => _player;

		private static Saved Swap()
		{
			if (!_built) Build();
			var s = new Saved { Npc = Main.npc, Item = Main.item, Proj = Main.projectile, P255 = Main.player[255], MyPlayer = Main.myPlayer, NetMode = Main.netMode, Ded = Main.dedServ, NumLiquid = Liquid.numLiquid };
			Main.npc = _npcs; Main.item = _items; Main.projectile = _projs;
			Main.player[255] = _player; Main.myPlayer = 255; // the server stays a server: no lighting, dust or sound is touched; the owner-gated tile code runs because the owner IS Main.myPlayer
			return s;
		}

		private static void Restore(Saved s)
		{
			Main.npc = s.Npc; Main.item = s.Item; Main.projectile = s.Proj; Main.player[255] = s.P255; Main.myPlayer = s.MyPlayer; Main.netMode = s.NetMode;
			Liquid.numLiquid = s.NumLiquid; Main.dedServ = s.Ded;
		}

		private static void Build()
		{
			_built = true;
			_npcs = new NPC[Main.maxNPCs + 1]; for (int i = 0; i < _npcs.Length; i++) { _npcs[i] = new NPC(); _npcs[i].whoAmI = i; }
			_items = new Item[Main.maxItems + 1]; for (int i = 0; i < _items.Length; i++) _items[i] = new Item();
			_projs = new Projectile[Main.maxProjectiles + 1]; for (int i = 0; i < _projs.Length; i++) { _projs[i] = new Projectile(); _projs[i].whoAmI = i; }
			_player = new Player(); _player.whoAmI = 255; _player.active = true; global::Levy.Common.Owner.SoldierOwner.RegisterDummy(_player);
			_ox = TemplateX;
			try { Layout(); } finally { _ox = FieldX; }
			_goldSum = TemplateSum();
		}

		/// <summary>A checksum of the pristine copy. A shot that reaches it (a world-wide spray, a digging bolt) would make every later test lie, so it is rebuilt when the sum moves.</summary>
		private static long TemplateSum()
		{
			long sum = 0;
			for (int x = 0; x <= W; x++) for (int y = 0; y <= H; y++) { Tile t = Main.tile[TemplateX + x, Top + y]; sum += (t.HasTile ? t.TileType + 1 : 0) * 31L + t.WallType * 7L + t.LiquidAmount * 3L + (x * 131L + y) % 7; }
			return sum;
		}

		private static void Put(int x, int y, int type, int wall = 0)
		{
			Tile t = Main.tile[_ox + x, Top + y]; t.ClearEverything();
			if (type >= 0) { t.HasTile = true; t.TileType = (ushort)type; t.BlockType = BlockType.Solid; }
			if (wall > 0) t.WallType = (ushort)wall;
		}

		/// <summary>The block field. Floor top at <see cref="FloorY"/>, zones side by side so one shot lands in one material.</summary>
		private static void Layout()
		{
			for (int x = 0; x <= W; x++) for (int y = 0; y <= H; y++) Main.tile[_ox + x, Top + y].ClearEverything();
			void Zone(int x0, int x1, int top, int body, int cap = -1, int wall = 0)
			{
				for (int x = x0; x < x1; x++)
				{
					for (int y = top; y < H; y++) Put(x, y, y == top && cap >= 0 ? cap : body, wall);
					if (wall > 0) for (int y = top - 12; y < top; y++) { Tile t = Main.tile[_ox + x, Top + y]; t.WallType = (ushort)wall; }
				}
			}
			Zone(4, 24, FloorY, TileID.Dirt, TileID.Grass, WallID.DirtUnsafe);
			Zone(24, 44, FloorY, TileID.Stone, -1, WallID.Stone);
			Zone(44, 64, FloorY, TileID.WoodBlock, -1, WallID.Wood);
			Zone(64, 84, FloorY, TileID.Sand, -1, WallID.Sandstone);
			Zone(84, 104, FloorY, TileID.GrayBrick, -1, WallID.GrayBrick);
			Zone(104, 124, FloorY, TileID.Mud, TileID.JungleGrass, WallID.MudUnsafe);
			Zone(124, 144, FloorY, TileID.IceBlock, -1, WallID.IceUnsafe);
			Zone(144, 176, FloorY, TileID.Dirt, TileID.Grass);
			// the three kinds of spreading biome, for the sprays and waters that convert them
			Zone(176, 190, FloorY, TileID.Ebonstone, TileID.CorruptGrass, WallID.EbonstoneUnsafe);
			Zone(190, 204, FloorY, TileID.Crimstone, TileID.CrimsonGrass, WallID.CrimstoneUnsafe);
			Zone(204, 218, FloorY, TileID.Pearlstone, TileID.HallowedGrass, WallID.PearlstoneBrickUnsafe);
			// fragile things standing on the floor and hanging above it
			for (int x = 46; x < 62; x += 2) Put(x, FloorY - 1, TileID.Platforms);
			for (int x = 48; x < 62; x++) for (int y = FloorY - 6; y < FloorY - 4; y++) Put(x, y, TileID.Cobweb);
			for (int x = 86; x < 102; x += 2) Put(x, FloorY - 1, TileID.Torches);
			for (int x = 66; x < 82; x += 3) for (int y = FloorY - 3; y < FloorY; y++) Put(x, y, TileID.Glass);
			for (int x = 26; x < 42; x += 3) Put(x, FloorY - 1, TileID.Pots);
			for (int x = 6; x < 22; x += 2) Put(x, FloorY - 1, TileID.Plants);
			for (int x = 126; x < 142; x += 4) for (int y = FloorY - 4; y < FloorY; y++) Put(x, y, TileID.BreakableIce);
			// liquid: a pool of water and a pool of lava sunk into the last zone
			for (int x = 150; x < 160; x++) for (int y = FloorY; y < FloorY + 4; y++) { Tile t = Main.tile[_ox + x, Top + y]; t.ClearEverything(); t.LiquidType = LiquidID.Water; t.LiquidAmount = 255; }
			for (int x = 162; x < 172; x++) for (int y = FloorY; y < FloorY + 4; y++) { Tile t = Main.tile[_ox + x, Top + y]; t.ClearEverything(); t.LiquidType = LiquidID.Lava; t.LiquidAmount = 255; }
			for (int y = H - 6; y <= H; y++) for (int x = 0; x <= W; x++) Put(x, y, TileID.Stone);
		}

		private static readonly string[] Fields = { "type", "wall", "liquid", "frame", "slope", "paint", "wire" };

		private static void Snap(int fromX, int toX)
		{
			for (int x = 0; x <= W; x++) for (int y = 0; y <= H; y++) Main.tile[toX + x, Top + y].CopyFrom(Main.tile[fromX + x, Top + y]);
		}

		/// <summary>The tiles that differ between the field and the pristine copy, as a kind set (and a count).</summary>
		private static Kind Diff(out int changed, HashSet<int> types)
		{
			Kind k = Kind.None; changed = 0;
			for (int x = 0; x <= W; x++)
				for (int y = 0; y <= H; y++)
				{
					Tile a = Main.tile[FieldX + x, Top + y], b = Main.tile[TemplateX + x, Top + y];
					bool any = false;
					if (a.HasTile != b.HasTile) { if (a.HasTile) k |= Kind.Place; else if (b.TileType < Main.tileCut.Length && Main.tileCut[b.TileType]) k |= Kind.Soft; else k |= Kind.Tiles; any = true; types.Add(b.HasTile ? b.TileType : a.TileType); }
					else if (a.HasTile && a.TileType != b.TileType) { k |= Kind.Convert; any = true; }
					if (a.WallType != b.WallType) { k |= a.WallType == 0 ? Kind.Walls : Kind.Convert; any = true; }
					if (a.LiquidAmount != b.LiquidAmount || a.LiquidType != b.LiquidType) { k |= Kind.Liquid; any = true; }
					if (a.HasTile && b.HasTile && (a.TileColor != b.TileColor) || a.WallColor != b.WallColor) { k |= Kind.Paint; any = true; }
					if (a.HasTile && b.HasTile && (a.Slope != b.Slope || a.IsHalfBlock != b.IsHalfBlock || a.IsActuated != b.IsActuated || a.HasActuator != b.HasActuator || a.RedWire != b.RedWire || a.BlueWire != b.BlueWire || a.GreenWire != b.GreenWire || a.YellowWire != b.YellowWire)) { k |= Kind.Other; any = true; }
					if (any) changed++;
				}
			return k;
		}

		public sealed class Result { public Kind Kind; public int Changed; public SortedSet<int> Made = new(); public HashSet<int> Types = new(); public string Error; public int Ticks; }

		private static MethodInfo _shoot;

		/// <summary>Runs one test body inside the sandbox: <paramref name="fire"/> spawns the shot (the field is pristine), then every projectile is stepped to its end.</summary>
		public static Result Run(Action<Player> fire, int maxTicks = 420)
		{
			var res = new Result();
			if (!Main.dedServ || (Environment.GetEnvironmentVariable("LEVY_TILESWEEP") != "1" && Environment.GetEnvironmentVariable("LEVY_SELFCHECK_WORLD") != "1")) { res.Error = "the sandbox only runs on a test server"; return res; } // it overwrites patches of the loaded world
			long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
			Saved s = Swap(); AlterWatch.Sandbox = true;
			try
			{
				if (TemplateSum() != _goldSum) { _ox = TemplateX; try { Layout(); } finally { _ox = FieldX; } Repairs++; }
				Snap(TemplateX, FieldX);
				for (int i = 0; i < _projs.Length; i++) _projs[i].active = false;
				for (int i = 0; i < _items.Length; i++) _items[i].active = false;
				Player p = _player;
				p.active = true; p.dead = false; p.whoAmI = 255; p.position = new Vector2((FieldX + 60) * 16f, (Top + FloorY - 8) * 16f); p.velocity = Vector2.Zero; p.direction = 1; p.gravDir = 1f;
				p.statLifeMax = p.statLifeMax2 = p.statLife = 500; p.statManaMax = p.statManaMax2 = p.statMana = 400;
				// aim: down and ahead, so a shot lands in the zone in front of the shooter
				Main.mouseX = (int)(p.Center.X + 140 - Main.screenPosition.X); Main.mouseY = (int)(p.Center.Y + 230 - Main.screenPosition.Y);
				try { fire(p); }
				catch (Exception e) { res.Error = (e.InnerException ?? e).GetType().Name + ": " + (e.InnerException ?? e).Message.Split('\n')[0]; }
				int t;
				for (t = 0; t < maxTicks; t++)
				{
					bool any = false;
					for (int i = 0; i < _projs.Length; i++)
					{
						Projectile pr = _projs[i];
						if (!pr.active) continue;
						any = true; res.Made.Add(pr.type);
						try { pr.Update(i); } catch (Exception e) { res.Error ??= (e.InnerException ?? e).GetType().Name; pr.active = false; }
					}
					if (!any) break;
				}
				res.Ticks = t;
				for (int i = 0; i < _projs.Length; i++) { Projectile pr = _projs[i]; if (!pr.active) continue; res.Made.Add(pr.type); try { pr.Kill(); } catch (Exception) { } pr.active = false; } // a shot that outlives the window ends here, its own end-of-life code included
				for (int k = 0; k < 4; k++) for (int i = 0; i < _projs.Length; i++) { Projectile pr = _projs[i]; if (!pr.active) continue; res.Made.Add(pr.type); try { pr.Update(i); } catch (Exception) { pr.active = false; } } // children made by that last kill
				res.Kind = Diff(out res.Changed, res.Types);
			}
			finally { AlterWatch.Sandbox = false; for (int i = 0; i < _projs.Length; i++) _projs[i].active = false; Restore(s); Runs++; Ms += (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency; }
			return res;
		}

		/// <summary>The weapon fired through the game's own shot code, with <paramref name="ammo"/> (or none) in the ammo slot.</summary>
		public static Result Shoot(int weaponType, int ammoType, int maxTicks = 420)
		{
			_shoot ??= typeof(Player).GetMethod("ItemCheck_Shoot", BindingFlags.Instance | BindingFlags.NonPublic);
			return Run(p =>
			{
				Item weapon = new Item(); weapon.SetDefaults(weaponType); weapon.stack = 1; p.inventory[0] = weapon; p.selectedItem = 0;
				if (ammoType > 0) { Item am = new Item(); am.SetDefaults(ammoType); am.stack = Math.Max(1, am.maxStack); p.inventory[54] = am; } else p.inventory[54] = new Item();
				p.itemAnimation = p.itemAnimationMax = Math.Max(1, weapon.useAnimation); p.itemTime = 0; p.controlUseItem = true; p.channel = true;
				_shoot.Invoke(p, new object[] { 255, weapon, weapon.damage }); // the first argument is the owner slot: the scratch player's own
			}, maxTicks);
		}

		/// <summary>One projectile spawned in the air above the field, headed down and a little forward (the shape of a thrown or lobbed shot), with the given army flags.</summary>
		public static readonly int[] Zones = { 70, 183, 197, 211, 114 }; // over the sand and glass, the Corruption, the Crimson, the Hallow, the jungle mud: the places a spray or a bomb has something of its own to convert

		public static Result Spawn(int projType, bool npcProj, int maxTicks = 420, int column = 70)
		{
			return Run(p =>
			{
				Vector2 at = new Vector2((FieldX + column) * 16f, (Top + FloorY - 6) * 16f); Vector2 v = new Vector2(2f, 6f);
				int i = Projectile.NewProjectile(new EntitySource_Misc("TileSandbox"), at, v, projType, 60, 2f, 255);
				if (i >= 0 && i < _projs.Length) { _projs[i].npcProj = npcProj; if (npcProj) _projs[i].noDropItem = true; }
			}, maxTicks);
		}

		/// <summary>True when the sandbox itself works: a bomb dropped on the field must change it, a bullet must not.</summary>
		public static bool Calibrated(out string why)
		{
			var bomb = Spawn(ProjectileID.Bomb, false); var bullet = Spawn(ProjectileID.Bullet, false);
			why = $"Bomb -> {bomb.Kind} ({bomb.Changed} tiles, {bomb.Ticks} ticks, error {bomb.Error ?? "none"}); Bullet -> {bullet.Kind} ({bullet.Changed} tiles)";
			return global::Levy.Common.Weapons.WorldAlter.Hard(bomb.Kind) != Kind.None && global::Levy.Common.Weapons.WorldAlter.Hard(bullet.Kind) == Kind.None;
		}

		public static string KindText(Kind k) => k == Kind.None ? "-" : string.Join("+", Enum.GetValues(typeof(Kind)).Cast<Kind>().Where(f => f != Kind.None && k.HasFlag(f)));
	}
}
