using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Owner;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Weapons;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// WORLD-LEVEL identity self-check, logged as "[IdentityWorld]" lines when a world loads on a dedicated server with LEVY_SELFCHECK_WORLD=1 (the shipworld stage chains it after the summon world check).
	/// It stands slot 0 in as the single-player commander (Main.myPlayer = 0 while NPCs and projectiles update) and, for the length of those two phases, shows other mods the client they would see in a
	/// single-player game (Main.netMode = SinglePlayer), because that is where a levy's gear frame reaches mods whose "is this a client" test is netMode rather than dedServ.
	/// <para/>Check 1 (only when AlchemistNPCLite is loaded): the Brewer's "Shops" side menu is opened as a click does (ModNPC.OnChatButtonClicked, second button) while the commander is talking to the Brewer,
	/// then the world is stepped with no levies (control) and with levies (their gear frames run). The tick the menu closes, and the call stack that closed it, are logged.
	/// Env LEVY_IDENTITY_GUARD=0 turns the identity guard off, which is how the unfixed behaviour is reproduced.
	/// </summary>
	public class IdentityWorldCheck : ModSystem
	{
		private static bool _armed, _done, _running, _spNet; private static int _calls, _pass, _fail;
		private static Mod _mod; private static int _savedMy, _savedNet;
		private const int FloorY = 300, Cx = 1800;
		private static Vector2 _cmdAt;
		private static NPC _brewer; private static Vector2 _brewerAt;

		public override void OnWorldLoad()
		{
			if (!Main.dedServ || Environment.GetEnvironmentVariable("LEVY_SELFCHECK_WORLD") != "1" || Environment.GetEnvironmentVariable("LEVY_IDENTITYWORLD") == "0") return;
			_mod = Mod; _armed = true; _done = false; _calls = 0; _pass = _fail = 0;
			// standalone fallback: when no other world check chains this one, a plain driver does
			if (Environment.GetEnvironmentVariable("LEVY_SUMMONWORLD") == "0") Main.OnTickForThirdPartySoftwareOnly += Drive;
		}
		public override void OnWorldUnload() { try { Main.OnTickForThirdPartySoftwareOnly -= Drive; } catch { } _armed = false; }

		private static void Drive()
		{
			if (!_armed || _done || !Main.dedServ) return;
			if (++_calls < 6) return;
			if (ModLoader.HasMod("Mechanization") && Environment.GetEnvironmentVariable("LEVY_MECHWORLD") != "0") return;
			RunIfArmed();
		}

		// the server drops a slot with no connection every tick, and the NPCs despawn with nobody on: slot 0 is kept alive and made local only while NPCs and projectiles update
		public override void PostUpdatePlayers() { if (_running && Main.player[0] != null) Main.player[0].active = true; }
		public override void PreUpdateNPCs() { if (!_running) return; Main.player[0].active = true; Pin(); _savedMy = Main.myPlayer; Main.myPlayer = 0; _savedNet = Main.netMode; if (_spNet) Main.netMode = NetmodeID.SinglePlayer; }
		public override void PostUpdateNPCs() { if (!_running) return; Main.myPlayer = _savedMy; Main.netMode = _savedNet; }
		public override void PreUpdateProjectiles() { if (!_running) return; Main.player[0].active = true; _savedMy = Main.myPlayer; Main.myPlayer = 0; _savedNet = Main.netMode; if (_spNet) Main.netMode = NetmodeID.SinglePlayer; }
		public override void PostUpdateProjectiles() { if (!_running) return; Main.myPlayer = _savedMy; Main.netMode = _savedNet; }

		/// <summary>Runs now when this boot armed it (the summon world check calls this at its end, so the checks run one after another).</summary>
		public static void RunIfArmed()
		{
			if (!_armed || _done || !Main.dedServ) return;
			_done = true; _running = true;
			bool sp = global::Levy.Common.Util.LevyUtil.SpEmulation;
			try { global::Levy.Common.Util.LevyUtil.SpEmulation = true; Run(); }
			catch (Exception e) { Check("driver threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim(), false); }
			finally { _running = false; _spNet = false; global::Levy.Common.Util.LevyUtil.SpEmulation = sp; Main.netMode = NetmodeID.Server; }
			_mod.Logger.Info($"[IdentityWorld] self-check pass={_pass} fail={_fail}");
		}

		private static void Check(string name, bool ok) { if (ok) _pass++; else _fail++; _mod.Logger.Info($"[IdentityWorld] {(ok ? "PASS" : "FAIL")} {name}"); }
		private static void Note(string s) => _mod.Logger.Info("[IdentityWorld] OBSERVED " + s);

		private static void Step(int n)
		{
			for (int i = 0; i < n; i++) { KeepCommander(); RealPlayerHooks(); WorldStep.Update(); }
		}

		/// <summary>The commander's own per-tick ModPlayer reset, run the way a single-player client runs it (Main.myPlayer = the commander, a client netMode): what a real tick does to the menu for the real player.</summary>
		private static void RealPlayerHooks()
		{
			if (!_spNet) return;
			int my = Main.myPlayer, net = Main.netMode; Main.myPlayer = 0; Main.netMode = NetmodeID.SinglePlayer;
			try { PlayerLoader.ResetEffects(Main.player[0]); }
			finally { Main.myPlayer = my; Main.netMode = net; }
		}

		private static void KeepCommander()
		{
			Player p = Main.player[0];
			p.active = true; p.dead = false; p.name = "IdentityTester";
			if (p.statLifeMax < 500) p.statLifeMax = p.statLifeMax2 = 500;
			p.statLife = p.statLifeMax2;
			p.position = _cmdAt - new Vector2(p.width / 2f, p.height / 2f); p.velocity = Vector2.Zero;
			if (_brewer != null && _brewer.active) p.SetTalkNPC(_brewer.whoAmI, true); // the commander is in conversation with the Brewer
		}

		private static void Pin()
		{
			if (_brewer != null && _brewer.active) { _brewer.position = _brewerAt; _brewer.velocity = Vector2.Zero; _brewer.timeLeft = 9999; }
		}

		private static void Arena()
		{
			for (int i = Cx - 70; i <= Cx + 70; i++)
			{
				for (int j = FloorY - 80; j < FloorY; j++) { Tile t = Main.tile[i, j]; t.ClearEverything(); }
				Tile f = Main.tile[i, FloorY]; f.ClearEverything(); f.HasTile = true; f.TileType = TileID.GrayBrick; f.BlockType = BlockType.Solid;
				for (int j = FloorY + 1; j < FloorY + 4; j++) { Tile u = Main.tile[i, j]; u.ClearEverything(); u.HasTile = true; u.TileType = TileID.GrayBrick; u.BlockType = BlockType.Solid; }
			}
		}

		private static void ClearSoldiers()
		{
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (n.active && n.ModNPC is SoldierNPC sn) { if (sn.Record != null) sn.Record.Gear = new SoldierGear(); n.active = false; }
			}
			RosterSystem.Instance.Regulars.RemoveAll(r => r.CommanderName == "IdentityTester");
			for (int i = 0; i < Main.maxProjectiles; i++) Main.projectile[i].active = false;
		}

		/// <summary>A modded accessory a levy can wear: its gear needs the per-tick frame (any modded armor or accessory does).</summary>
		private static Item ModdedAccessory()
		{
			for (int i = ItemID.Count; i < ItemLoader.ItemCount; i++)
			{
				if (!ContentSamples.ItemsByType.TryGetValue(i, out Item s) || s == null || s.IsAir || !s.accessory || s.vanity) continue;
				Item it = new Item(); it.SetDefaults(i);
				if (new SoldierGear().CanHold(SoldierGear.SlotAcc0, it)) return it;
			}
			return null;
		}

		private static int SpawnLevy(string name, Item weapon, Item accessory, Vector2 at)
		{
			var rec = new SoldierRecord
			{
				Id = RosterSystem.Instance.NextId++, Kind = SoldierKind.Regular, Grade = Grade.Levy, Temperament = Temperament.Sellsword, Male = true, Name = name,
				CommanderName = "IdentityTester", CommanderIndex = 0, LifeCrystals = LevyConstants.MaxLifeCrystals, ManaCrystals = 9, HomeBarracksId = -1, StoodDown = false,
			};
			rec.CurrentHp = rec.MaxHp; rec.Mana = rec.MaxMana;
			if (weapon != null && rec.Gear.CanHold(SoldierGear.SlotWeapon, weapon)) { rec.Gear.Weapon = weapon; rec.Gear.SetLocked(SoldierGear.SlotWeapon, true); }
			if (accessory != null && rec.Gear.CanHold(SoldierGear.SlotAcc0, accessory)) { rec.Gear.Accessories[0] = accessory; rec.Gear.SetLocked(SoldierGear.SlotAcc0, true); }
			rec.LoadoutLocked = true; rec.Order = SoldierOrder.Follow;
			RosterSystem.Instance.Regulars.Add(rec);
			int body = RosterSystem.Instance.SpawnBody(rec, at);
			if (body >= 0 && rec.HasBody) Main.npc[body].Bottom = at;
			return body;
		}

		private static void Run()
		{
			Main.player[0] = Main.player[0] ?? new Player();
			Arena();
			_cmdAt = new Vector2(Cx * 16f, (FloorY - 3) * 16f);
			ClearSoldiers();
			AlchemistMenu();
			ClearSoldiers();
		}

		// ---------------------------------------------------------------------------------------------- AlchemistNPC Lite: the side menu of a shop NPC
		private sealed class AlchTrace { public int Calls, Closes; public string FirstClose; public string FirstCloseWho; public uint FirstCloseTick; }
		private static AlchTrace _alch;
		private delegate void OrigVoid(ModPlayer self);
		private static Func<bool> _menuVisible; private static Action<bool> _setMenu;

		private static void AlchemistMenu()
		{
			if (!ModLoader.TryGetMod("AlchemistNPCLite", out Mod alch)) return;
			try
			{
				Type uiT = alch.Code.GetType("AlchemistNPCLite.Interface.ShopChangeUI");
				Type plT = alch.Code.GetType("AlchemistNPCLite.AlchemistNPCLitePlayer");
				FieldInfo vis = uiT.GetField("visible", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				_menuVisible = () => (bool)vis.GetValue(null); _setMenu = v => vis.SetValue(null, v);
				if (!alch.TryFind("Brewer", out ModNPC brewerType)) { Check("AlchemistNPC: Brewer found", false); return; }
				_alch = new AlchTrace();
				MethodInfo reset = plT.GetMethod("ResetEffects", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				MonoModHooks.Add(reset, (Action<OrigVoid, ModPlayer>)((orig, self) =>
				{
					bool before = _menuVisible(); if (_alch != null) _alch.Calls++;
					orig(self);
					if (_alch != null && before && !_menuVisible())
					{
						_alch.Closes++;
						if (_alch.FirstClose == null)
						{
							_alch.FirstCloseTick = Main.GameUpdateCount;
							_alch.FirstCloseWho = $"Player.whoAmI={self.Player.whoAmI} IsLevyDummy={SoldierOwner.IsDummy(self.Player)} Main.myPlayer={Main.myPlayer} Main.player[myPlayer].talkNPC={Main.player[Main.myPlayer].talkNPC} Main.LocalPlayer is dummy={SoldierOwner.IsDummy(Main.LocalPlayer)} netMode={Main.netMode} dedServ={Main.dedServ}";
							_alch.FirstClose = string.Join("\n", new StackTrace(1, false).ToString().Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).Take(14).Select(l => "      " + l));
						}
					}
				}));
				_spNet = true;
				try
				{
					Item acc = ModdedAccessory();
					// brewer next to the commander
					_brewerAt = new Vector2(Cx * 16f + 40f, (FloorY - 3) * 16f);
					int bi = NPC.NewNPC(new EntitySource_WorldEvent("IdentityWorldCheck"), (int)_brewerAt.X, (int)_brewerAt.Y, brewerType.Type);
					if (bi < 0 || bi >= Main.maxNPCs) { Check("AlchemistNPC: Brewer spawns", false); return; }
					_brewer = Main.npc[bi]; _brewer.homeless = false; _brewer.townNPC = true; Pin();
					KeepCommander(); Step(2);

					// control: no levies in the world
					string shop = null;
					_brewer.ModNPC.OnChatButtonClicked(false, ref shop); // the second button: "Shops"
					bool opened = _menuVisible();
					int closedAt = -1;
					for (int t = 1; t <= 180; t++) { Step(1); if (!_menuVisible() && closedAt < 0) closedAt = t; }
					Check("AlchemistNPC side menu: the click opens it", opened);
					Check($"AlchemistNPC side menu: no levies, 180 ticks, stays open (closed at tick {closedAt}; Alchemist ResetEffects calls {_alch.Calls})", closedAt < 0);
					Note($"AlchemistNPC control: talkNPC of the commander = {Main.player[0].talkNPC} (brewer index {_brewer.whoAmI}); hook calls {_alch.Calls}, closes {_alch.Closes}");

					// levies in the world
					_alch.Calls = _alch.Closes = 0; _alch.FirstClose = null;
					Item wpn = new Item(); wpn.SetDefaults(ItemID.CopperShortsword);
					int made = 0;
					for (int k = 0; k < 6; k++)
					{
						Item w = new Item(); w.SetDefaults(ItemID.CopperShortsword);
						Item a = null; if (acc != null) { a = new Item(); a.SetDefaults(acc.type); }
						if (SpawnLevy("Pvt. Identity" + k, w, a, new Vector2(Cx * 16f - 60f - 24f * k, FloorY * 16f)) >= 0) made++;
					}
					Step(2);
					shop = null; _setMenu(false);
					_brewer.ModNPC.OnChatButtonClicked(false, ref shop);
					opened = _menuVisible();
					closedAt = -1;
					int ticks = 0;
					for (int t = 1; t <= 180; t++) { Step(1); ticks++; if (!_menuVisible() && closedAt < 0) closedAt = t; }
					Note($"AlchemistNPC with {made} levies (accessory {(acc == null ? "none" : acc.Name)}): menu opened by click={opened}, closed at tick {closedAt}, Alchemist ResetEffects calls {_alch.Calls}, closing calls {_alch.Closes}");
					if (_alch.FirstClose != null) Note($"AlchemistNPC closer: {_alch.FirstCloseWho}\n    stack:\n{_alch.FirstClose}");
					Check($"AlchemistNPC side menu: {made} levies alive, 180 ticks, stays open (closed at tick {closedAt})", made > 0 && closedAt < 0);
				}
				finally { _spNet = false; _alch = null; _setMenu?.Invoke(false); if (_brewer != null) _brewer.active = false; _brewer = null; Main.player[0].SetTalkNPC(-1, true); }
			}
			catch (Exception e) { Check("AlchemistNPC menu check threw " + e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n')[0].Trim(), false); }
		}
	}
}
