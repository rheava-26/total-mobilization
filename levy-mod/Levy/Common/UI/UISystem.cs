using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.UI;
using Levy.Common.Net;
using Levy.Common.Soldiers;
using Levy.Common.Util;
using Levy.Content.TileEntities;

namespace Levy.Common.UI
{
	/// <summary>Interface layers, panels, radial, keybinds, HUD. OWNED BY THE UI AGENT.</summary>
	public class UISystem : ModSystem
	{
		public static UISystem Instance => ModContent.GetInstance<UISystem>();
		/// <summary>Soldiers drawn on screen this frame (nameplates auto-hide above the threshold). Reset at the start of every UpdateUI.</summary>
		public static int SoldiersOnScreen;
		/// <summary>Last completed frame's count; the nameplate decision reads this one.</summary>
		public static int SoldiersOnScreenLastFrame;
		/// <summary>Army strip visibility (toggled with the ArmyStrip keybind; not saved).</summary>
		public static bool ArmyStripVisible;
		/// <summary>Force soldier nameplates to show even in a crowd (toggled from the War Horn radial; not saved).</summary>
		public static bool NameplatesForced;

		/// <summary>True while the next left-click in the world designates the squad's Focus Attack (a hostile locks it, anything else marks a spot); set from the radial, read by <see cref="UIPlayer"/>.</summary>
		public static bool FocusDesignating { get; private set; }
		/// <summary>Client mirror: the local commander has a focus standing (locked NPC or attack position). Optimistic; the server is authoritative. Drives the radial's Clear-focus label in multiplayer.</summary>
		public static bool FocusActive;
		/// <summary>GameUpdateCount when designate mode began (clicks inside the grace window are ignored).</summary>
		public static uint FocusDesignateStartTick { get; private set; }

		public static ModKeybind FollowKey, HoldKey, ReturnKey, HoldFireKey, MobilizeKey, RallyKey, ArmyStripKey;

		public static BarracksTE CurrentBarracks { get; private set; }
		public static ArmoryTE CurrentArmory { get; private set; }
		/// <summary>Id of the soldier whose loadout panel is open, or -1.</summary>
		public static int CurrentLoadoutId { get; private set; } = -1;

		private UserInterface _barracksUI, _armoryUI, _radialUI, _hudUI, _manualUI, _loadoutUI, _officersUI;
		private BarracksPanel _barracks;
		private ArmoryPanel _armory;
		private SoldierLoadoutPanel _loadout;
		private OfficerPanel _officers;
		private ManualPanel _manual;
		private OrdersRadial _radial;
		private HudStrip _hud;
		private GameTime _lastGameTime;

		public override void Load()
		{
			FollowKey = KeybindLoader.RegisterKeybind(Mod, "FollowMe", "Y");
			HoldKey = KeybindLoader.RegisterKeybind(Mod, "HoldHere", "U");
			ReturnKey = KeybindLoader.RegisterKeybind(Mod, "ReturnToBarracks", "I");
			HoldFireKey = KeybindLoader.RegisterKeybind(Mod, "HoldFire", "O");
			MobilizeKey = KeybindLoader.RegisterKeybind(Mod, "Mobilize", "P");
			RallyKey = KeybindLoader.RegisterKeybind(Mod, "Rally", "K");
			ArmyStripKey = KeybindLoader.RegisterKeybind(Mod, "ArmyStrip", "N");

			LevyPackets.Register(LevyPacket.ArmorySlotSet, HandleArmorySlotSet);
			LevyPackets.Register(LevyPacket.ArmorySort, HandleArmorySort);
		}

		public override void PostSetupContent()
		{
			// Panels bake their static labels from localization in OnInitialize (via Activate), so build them after localization
			// is fully loaded; doing this in Load would stamp raw keys like "Mods.Levy.UI2.Roster" into the buttons.
			if (Main.dedServ) return;
			_barracksUI = new UserInterface(); _armoryUI = new UserInterface(); _radialUI = new UserInterface(); _hudUI = new UserInterface(); _manualUI = new UserInterface(); _loadoutUI = new UserInterface(); _officersUI = new UserInterface();
			_barracks = new BarracksPanel(); _armory = new ArmoryPanel(); _radial = new OrdersRadial(); _hud = new HudStrip(); _manual = new ManualPanel(); _loadout = new SoldierLoadoutPanel(); _officers = new OfficerPanel();
			_barracks.Activate(); _armory.Activate(); _radial.Activate(); _hud.Activate(); _manual.Activate(); _loadout.Activate(); _officers.Activate();
		}

		public override void Unload()
		{
			FollowKey = HoldKey = ReturnKey = HoldFireKey = MobilizeKey = RallyKey = ArmyStripKey = null;
			_barracksUI = _armoryUI = _radialUI = _hudUI = _manualUI = _loadoutUI = _officersUI = null;
			_barracks = null; _armory = null; _radial = null; _hud = null; _manual = null; _loadout = null; _officers = null;
			CurrentBarracks = null; CurrentArmory = null; CurrentLoadoutId = -1; ArmyStripVisible = false; NameplatesForced = false; FocusDesignating = false; FocusActive = false;
		}

		public override void OnWorldUnload() { FocusDesignating = false; FocusActive = false; if (!Main.dedServ) CloseAll(); }

		// ------------------------------------------------------------------ open / close (client)
		public virtual void OpenBarracks(BarracksTE te)
		{
			if (Main.dedServ || te == null || _barracksUI == null) return;
			CloseArmory(); CloseRadial(); CloseManual(); CloseSoldierLoadout(); CloseOfficers();
			CurrentBarracks = te;
			_barracks.Bind(te);
			_barracksUI.SetState(_barracks);
			Main.playerInventory = true; Main.recBigList = false;
			if (Main.LocalPlayer.talkNPC >= 0) Main.LocalPlayer.SetTalkNPC(-1);
			Main.npcChatText = "";
			SoundEngine.PlaySound(SoundID.MenuOpen);
		}

		public virtual void OpenArmory(ArmoryTE te)
		{
			if (Main.dedServ || te == null || _armoryUI == null) return;
			CloseBarracks(); CloseRadial(); CloseManual(); CloseSoldierLoadout(); CloseOfficers();
			CurrentArmory = te;
			_armory.Bind(te);
			_armoryUI.SetState(_armory);
			Main.playerInventory = true; Main.recBigList = false;
			if (Main.LocalPlayer.talkNPC >= 0) Main.LocalPlayer.SetTalkNPC(-1);
			Main.npcChatText = "";
			SoundEngine.PlaySound(SoundID.MenuOpen);
		}

		public virtual void OpenManual()
		{
			if (Main.dedServ || _manualUI == null) return;
			CloseBarracks(); CloseArmory(); CloseRadial(); CloseSoldierLoadout();
			_manual.Open();
			_manualUI.SetState(_manual);
			SoundEngine.PlaySound(SoundID.MenuOpen);
		}

		/// <summary>Opens the per-soldier loadout panel next to the Barracks panel (which stays open).</summary>
		public virtual void OpenSoldierLoadout(SoldierRecord rec)
		{
			if (Main.dedServ || rec == null || _loadoutUI == null) return;
			CloseArmory(); CloseRadial(); CloseManual(); CloseOfficers();
			CurrentLoadoutId = rec.Id;
			_loadout.Bind(rec.Id);
			_loadoutUI.SetState(_loadout);
			Main.playerInventory = true; Main.recBigList = false;
			SoundEngine.PlaySound(SoundID.MenuOpen);
		}

		/// <summary>Opens the officer menu next to the Barracks panel (which stays open). It shares the dock with the soldier loadout panel, so each closes the other.</summary>
		public virtual void OpenOfficers(BarracksTE te)
		{
			if (Main.dedServ || te == null || _officersUI == null) return;
			CloseArmory(); CloseRadial(); CloseManual(); CloseSoldierLoadout();
			_officers.Bind(te);
			_officersUI.SetState(_officers);
			Main.playerInventory = true; Main.recBigList = false;
			SoundEngine.PlaySound(SoundID.MenuOpen);
		}

		public bool OfficersOpen => _officersUI?.CurrentState != null;

		public void CloseOfficers()
		{
			if (_officersUI?.CurrentState == null) return;
			_officersUI.SetState(null);
			SoundEngine.PlaySound(SoundID.MenuClose);
		}

		public virtual void CloseAll() { CloseBarracks(); CloseArmory(); CloseRadial(); CloseManual(); CloseSoldierLoadout(); CloseOfficers(); }

		public void CloseSoldierLoadout()
		{
			if (_loadoutUI?.CurrentState == null) { CurrentLoadoutId = -1; return; }
			_loadoutUI.SetState(null); CurrentLoadoutId = -1;
			SoundEngine.PlaySound(SoundID.MenuClose);
		}

		public void CloseManual()
		{
			if (_manualUI?.CurrentState == null) return;
			_manualUI.SetState(null);
			SoundEngine.PlaySound(SoundID.MenuClose);
		}

		public void CloseBarracks()
		{
			CloseSoldierLoadout(); // the loadout panel hangs off the roster
			CloseOfficers();       // so does the officer menu
			if (_barracksUI?.CurrentState == null) { CurrentBarracks = null; return; }
			_barracksUI.SetState(null); CurrentBarracks = null;
			SoundEngine.PlaySound(SoundID.MenuClose);
		}

		public void CloseArmory()
		{
			if (_armoryUI?.CurrentState == null) { CurrentArmory = null; return; }
			_armoryUI.SetState(null); CurrentArmory = null;
			SoundEngine.PlaySound(SoundID.MenuClose);
		}

		public virtual void ToggleRadial()
		{
			if (Main.dedServ || _radialUI == null) return;
			if (_radialUI.CurrentState != null) { CloseRadial(); return; }
			CloseBarracks(); CloseArmory(); CloseManual(); CloseSoldierLoadout();
			_radial.Open();
			_radialUI.SetState(_radial);
			SoundEngine.PlaySound(SoundID.MenuOpen);
		}

		public void CloseRadial()
		{
			if (_radialUI?.CurrentState == null) return;
			_radialUI.SetState(null);
			SoundEngine.PlaySound(SoundID.MenuClose);
		}

		/// <summary>Enters one-shot designate mode: the next left-click locks a hostile NPC as the focus target, or marks the spot under a critter or bare ground; Escape or right-click cancels.</summary>
		public void BeginFocusDesignate()
		{
			if (Main.dedServ) return;
			FocusDesignating = true;
			FocusDesignateStartTick = Main.GameUpdateCount;
			Main.NewText(LevyUtil.T("UI2.FocusDesignate"), 200, 200, 120);
		}

		public void EndFocusDesignate() => FocusDesignating = false;

		public bool RadialOpen => _radialUI?.CurrentState != null;

		public virtual void ToggleArmyStrip()
		{
			if (Main.dedServ) return;
			ArmyStripVisible = !ArmyStripVisible;
			SoundEngine.PlaySound(SoundID.MenuTick);
		}

		/// <summary>Force all soldier nameplates on/off regardless of the crowd threshold.</summary>
		public virtual void ToggleNameplates()
		{
			if (Main.dedServ) return;
			NameplatesForced = !NameplatesForced;
			SoundEngine.PlaySound(SoundID.MenuTick);
		}

		// ------------------------------------------------------------------ per tick
		public override void UpdateUI(GameTime gameTime)
		{
			SoldiersOnScreenLastFrame = SoldiersOnScreen;
			SoldiersOnScreen = 0;
			if (Main.dedServ || _barracksUI == null) return;
			_lastGameTime = gameTime;
			var player = Main.LocalPlayer;

			if (_barracksUI.CurrentState != null)
			{
				if (!PanelStillValid(CurrentBarracks, CurrentBarracks?.Center ?? Vector2.Zero, player)) CloseBarracks();
				else _barracksUI.Update(gameTime);
			}
			if (_loadoutUI.CurrentState != null)
			{
				if (CurrentBarracks == null || !Main.playerInventory || Main.gameMenu || player.dead) CloseSoldierLoadout();
				else _loadoutUI.Update(gameTime);
			}
			if (_officersUI.CurrentState != null)
			{
				if (CurrentBarracks == null || !Main.playerInventory || Main.gameMenu || player.dead) CloseOfficers();
				else _officersUI.Update(gameTime);
			}
			if (_armoryUI.CurrentState != null)
			{
				if (!PanelStillValid(CurrentArmory, CurrentArmory?.Center ?? Vector2.Zero, player)) CloseArmory();
				else { _armory?.RefreshIfStorageReplaced(); _armoryUI.Update(gameTime); }
			}
			if (_manualUI.CurrentState != null)
			{
				if (Main.gameMenu || player.dead) CloseManual();
				else _manualUI.Update(gameTime);
			}
			if (_radialUI.CurrentState != null)
			{
				if (Main.gameMenu || player.dead) CloseRadial();
				else _radialUI.Update(gameTime);
			}
			if (ArmyStripVisible && !Main.gameMenu) { _hudUI.SetState(_hud); _hudUI.Update(gameTime); }
			else if (_hudUI.CurrentState != null) _hudUI.SetState(null);
		}

		private static bool PanelStillValid(TileEntity te, Vector2 center, Player player)
		{
			if (te == null || !UIActions.IsLive(te)) return false;
			if (!Main.playerInventory || Main.gameMenu || player.dead) return false;
			return LevyUtil.WithinTiles(player.Center, center, LevyConstants.UI.PanelCloseDistanceTiles);
		}

		// ------------------------------------------------------------------ layers
		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
		{
			if (_barracksUI == null) return;
			var gt = _lastGameTime ?? new GameTime();

			int inv = layers.FindIndex(l => l.Name.Equals("Vanilla: Inventory"));
			if (inv != -1)
			{
				layers.Insert(inv + 1, new LegacyGameInterfaceLayer("Levy: Panels", () =>
				{
					if (_barracksUI?.CurrentState != null) _barracksUI.Draw(Main.spriteBatch, gt);
					if (_loadoutUI?.CurrentState != null) _loadoutUI.Draw(Main.spriteBatch, gt);
					if (_officersUI?.CurrentState != null) _officersUI.Draw(Main.spriteBatch, gt);
					if (_armoryUI?.CurrentState != null) _armoryUI.Draw(Main.spriteBatch, gt);
					if (_manualUI?.CurrentState != null) _manualUI.Draw(Main.spriteBatch, gt);
					return true;
				}, InterfaceScaleType.UI));
				layers.Insert(inv + 2, new LegacyGameInterfaceLayer("Levy: Orders Radial", () =>
				{
					if (_radialUI?.CurrentState != null) _radialUI.Draw(Main.spriteBatch, gt);
					if (FocusDesignating) HintTooltip.Queue(LevyUtil.T("UI2.FocusCursorHint")); // clean tooltip (no fake-item "Material" line)
					return true;
				}, InterfaceScaleType.UI));
			}
			int res = layers.FindIndex(l => l.Name.Equals("Vanilla: Resource Bars"));
			if (res != -1)
			{
				layers.Insert(res + 1, new LegacyGameInterfaceLayer("Levy: Army Strip", () =>
				{
					if (ArmyStripVisible && _hudUI?.CurrentState != null) _hudUI.Draw(Main.spriteBatch, gt);
					return true;
				}, InterfaceScaleType.UI));
			}
		}

		// ------------------------------------------------------------------ packets
		/// <summary>Server: a client asked for the armory storage to be sorted. Payload: int armoryId.</summary>
		private static void HandleArmorySort(BinaryReader reader, int fromWhoAmI)
		{
			int armoryId = reader.ReadInt32();
			if (Main.netMode != NetmodeID.Server) return;
			if (!TileEntity.ByID.TryGetValue(armoryId, out var te) || te is not ArmoryTE armory) return;
			if (fromWhoAmI < 0 || fromWhoAmI >= Main.maxPlayers || !Main.player[fromWhoAmI].active) return;
			if (!LevyUtil.WithinTiles(Main.player[fromWhoAmI].Center, armory.Center, LevyConstants.UI.ArmoryEditRangeTiles)) return;
			armory.SortStorage();
			armory.Sync();
		}

		/// <summary>Server: a client set one armory slot. Payload: int armoryId, byte region, short slot, Item (ItemIO, stack).</summary>
		private static void HandleArmorySlotSet(BinaryReader reader, int fromWhoAmI)
		{
			int armoryId = reader.ReadInt32();
			byte region = reader.ReadByte();
			short slot = reader.ReadInt16();
			Item item = ItemIO.Receive(reader, readStack: true);
			if (Main.netMode != NetmodeID.Server) return;
			if (!TileEntity.ByID.TryGetValue(armoryId, out var te) || te is not ArmoryTE armory) return;
			var arr = UIActions.ArmoryRegion(armory, region);
			if (arr == null || slot < 0 || slot >= arr.Length) return;
			if (fromWhoAmI < 0 || fromWhoAmI >= Main.maxPlayers || !Main.player[fromWhoAmI].active) return;
			if (!LevyUtil.WithinTiles(Main.player[fromWhoAmI].Center, armory.Center, LevyConstants.UI.ArmoryEditRangeTiles)) return;
			arr[slot] = item ?? new Item();
			NetMessage.SendData(MessageID.TileEntitySharing, -1, -1, null, armory.ID, armory.Position.X, armory.Position.Y);
		}
	}
}
