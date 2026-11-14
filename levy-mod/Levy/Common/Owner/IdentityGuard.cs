using System;
using System.Collections.Generic;
using System.Reflection;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using Terraria;
using Terraria.ModLoader;
using Levy.Common.Weapons;

namespace Levy.Common.Owner
{
	/// <summary>
	/// THE IDENTITY GUARD: no other mod's ModPlayer ever sees a levy's dummy Player as "the local player".
	/// <para/>Why it exists. A levy's dummy has to be "local" for vanilla and for item code: <c>whoAmI == Main.myPlayer</c> blocks (accessory casts, set bonuses, owner-gated spawns) only run for the local
	/// player, and Main.myPlayer is the one switch that opens them. Levy therefore lends Main.myPlayer to the dummy for the length of a gear frame or a weapon swap (<see cref="SoldierOwner"/>, SoldierNPC.GearFrame,
	/// HeldWeaponSwap, SoldierMinions). But every ModPlayer of every other mod is dispatched for the dummy inside that window (PlayerLoader.ResetEffects, UpdateEquips, PostUpdateMiscEffects ...), and a mod
	/// that reads <c>Main.player[Main.myPlayer]</c> / <c>Main.LocalPlayer</c> or runs UI code under <c>Player.whoAmI == Main.myPlayer</c> then acts on the dummy as if it were the human at the keyboard.
	/// Proved with AlchemistNPC Lite: its ModPlayer.ResetEffects hides the shop-chooser side menus whenever <c>Main.player[Main.myPlayer].talkNPC == -1</c>, so every levy's gear frame (SoldierNPC.AI ->
	/// RefreshStats -> GearFrame -> Player.ResetEffects -> that hook) closed the menu in the tick it opened; LuiAFK's ResetUIs did the same through <c>whoAmI == Main.myPlayer</c>.
	/// <para/>What it does. At the ONE choke point every ModPlayer hook passes through (the static dispatch methods of <see cref="PlayerLoader"/>), when the Player being dispatched is a Levy dummy that is currently
	/// the local player, Main.myPlayer is put back to the REAL local player (or, where there is none: a dedicated server, to a slot that is nobody's) for the length of the dispatch, and restored on the way out. To the
	/// ModPlayer the dummy then looks exactly like any other non-local player (a remote client on a server): <c>whoAmI != Main.myPlayer</c>, and <c>Main.LocalPlayer</c> is the commander with their own talk, chest
	/// and sign state. Nothing else changes: the equip pass, ModItem and ModBuff hooks, set bonuses and weapon use still run with the dummy local, so everything gear does for a levy keeps working.
	/// <para/>Which hooks. The per-tick update family (<see cref="StateHooks"/>): the hooks a gear frame, a stat refresh or an owner frame dispatch. The item-use family (CanUseItem, ModifyShootStats, Shoot, ...) is
	/// deliberately left alone: those run inside a use window, where the dummy IS the player using an item, and the real-player side of that window is protected by <see cref="UseGuard"/>.
	/// Env LEVY_IDENTITY_GUARD=0 switches the guard off (the unfixed behaviour, for A/B in the self-checks).
	/// </summary>
	public sealed class IdentityGuard : ModSystem
	{
		/// <summary>Lab A/B switch.</summary>
		public static readonly bool Disabled = Environment.GetEnvironmentVariable("LEVY_IDENTITY_GUARD") == "0";

		/// <summary>The PlayerLoader dispatch methods that run in a levy's per-tick update (see the class summary). Everything else in PlayerLoader (use, shoot, hit, hurt, draw, fishing, shop ...) is untouched.</summary>
		public static readonly HashSet<string> StateHooks = new()
		{
			"ResetEffects", "ResetInfoAccessories", "RefreshInfoAccessoriesFromTeamPlayers", "ResetMaxStatsToVanilla", "ModifyMaxStats", "UpdateDead", "UpdateBadLifeRegen", "UpdateLifeRegen",
			"NaturalLifeRegen", "UpdateAutopause", "PreUpdate", "SetControls", "PreUpdateBuffs", "PostUpdateBuffs", "UpdateEquips", "PostUpdateEquips", "UpdateVisibleAccessories",
			"UpdateVisibleVanityAccessories", "UpdateDyes", "PostUpdateMiscEffects", "PostUpdateRunSpeeds", "PreUpdateMovement", "PostUpdate", "FrameEffects", "ModifyScreenPosition", "ModifyZoom",
			"ArmorSetBonusActivated", "ArmorSetBonusHeld", "OnRespawn", "SyncPlayer", "CopyClientState", "SendClientChanges",
		};

		/// <summary>Dispatches (ModPlayer hook calls) the guard has made non-local for a dummy: total, and per hook name.</summary>
		public static long Guarded;
		public static readonly Dictionary<string, long> GuardedBy = new();
		/// <summary>Every PlayerLoader method that was wrapped (for the self-check), and the number of state hooks among them.</summary>
		public static int Wrapped, WrappedState;

		private static int[] _saved = new int[64]; private static int _depth;
		private const int None = int.MinValue;

		public override void Load()
		{
			if (Disabled) return;
			foreach (MethodInfo m in typeof(PlayerLoader).GetMethods(BindingFlags.Public | BindingFlags.Static))
			{
				if (!StateHooks.Contains(m.Name)) continue;
				ParameterInfo[] ps = m.GetParameters();
				if (ps.Length == 0 || ps[0].ParameterType != typeof(Player)) continue;
				string name = m.Name;
				MonoModHooks.Modify(m, il => Wrap(il, name));
				Wrapped++; WrappedState++;
			}
		}

		private static void Wrap(ILContext il, string name)
		{
			var c = new ILCursor(il);
			c.Index = 0;
			c.Emit(OpCodes.Ldarg_0);
			c.EmitDelegate<Action<Player>>(Enter);
			while (c.TryGotoNext(MoveType.Before, i => i.MatchRet()))
			{
				c.EmitDelegate<Action>(Exit);
				c.Index++;
			}
		}

		/// <summary>True when <paramref name="p"/> is a levy's dummy standing in as the machine's local player.</summary>
		public static bool IsDummyLocal(Player p) => p != null && p.whoAmI == Main.myPlayer && SoldierOwner.IsDummy(p);

		/// <summary>The slot a ModPlayer of a dummy sees as Main.myPlayer: the real local player's, or on a machine with none (a dedicated server, where the dummy's slot IS Main.myPlayer) a slot nobody holds.</summary>
		private static int PresentedLocal(Player dummy)
		{
			int real = SoldierMinions.TrueMyPlayer;
			if (real != dummy.whoAmI && real >= 0 && real < Main.maxPlayers) return real;
			int s = Main.maxPlayers - 1; // 254: a parked null player; never the dummy's slot (255)
			return s == dummy.whoAmI ? Main.maxPlayers - 2 : s;
		}

		private static void Enter(Player p)
		{
			if (_depth >= _saved.Length) Array.Resize(ref _saved, _saved.Length * 2);
			if (Disabled || p == null || p.whoAmI != Main.myPlayer || !SoldierOwner.IsDummy(p)) { _saved[_depth++] = None; return; }
			_saved[_depth++] = Main.myPlayer;
			Main.myPlayer = PresentedLocal(p);
			Guarded++;
		}

		private static void Exit()
		{
			if (_depth <= 0) return;
			int s = _saved[--_depth];
			if (s != None) Main.myPlayer = s;
		}

		/// <summary>Called by every frame when it closes: an exception inside a hook skipped its Exit, and the frame restores Main.myPlayer itself.</summary>
		internal static void Reset() { _depth = 0; }

		public override void PreUpdateNPCs() { _depth = 0; }
	}
}
