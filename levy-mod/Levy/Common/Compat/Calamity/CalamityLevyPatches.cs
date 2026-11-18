using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Owner;
using Levy.Content.NPCs;

namespace Levy.Common.Compat.Calamity
{
	/// <summary>
	/// CALAMITY PATCHES FOR LEVY DUMMIES. A levy is a puppet Player whose gear frame runs Calamity's per-player code every tick (SoldierNPC.GearUpkeep.cs). Three of Calamity's steps do nothing for a puppet that a player
	/// could see or feel, and one walks the whole projectile array for every player:
	/// <list type="bullet">
	/// <item><c>CalamityPlayer.AndroombaRightClick</c>: the local player's right-click on a friendly Androomba with a solution in hand (cursor icon, conversion). It walks every NPC. A dummy never right-clicks: skipped for a dummy.</item>
	/// <item><c>CalamityPlayer.AbyssEffects</c>: its first line, <c>Player.SetAbyssLightLevels()</c>, sets the abyss darkness and glow numbers only the lighting and the darkness shader read. Skipped for a dummy; the abyss's breath, defense and life loss below it still run.</item>
	/// <item><c>CalamityPlayer.UpdateDrawingParameters</c>: the shield visuals (Rover Drive, Lunic Corps, Profaned, Sponge) and their sync. A levy is drawn with its dummy, so this keeps running wherever the game draws; it is
	/// skipped for a dummy only on a dedicated server, where nothing is drawn.</item>
	/// <item><c>CalamityPlayer.ResetEffects</c> asks <c>Main.projectile.Any(...)</c> whether one of the player's Virid Vanguard blades is in its Zenith slash: 1,000 slots for every player every tick. Inside a levy's gear frame the
	/// same question is asked of the Virid Vanguard blades only (a list of their slots made once a tick, remade when a blade is spawned): the predicate requires an active blade, so the answer is the same.</item>
	/// </list>
	/// Every patch checks "is this a levy dummy" first and is otherwise transparent. Without Calamity nothing is patched; Calamity is found by name (no reference). LEVY_CALPATCH=0 turns them all off.
	/// </summary>
	public class CalamityLevyPatches : ModSystem
	{
		internal static bool On = Environment.GetEnvironmentVariable("LEVY_CALPATCH") != "0";
		/// <summary>Lab/check counters: calls skipped for a dummy, and Any answers served from the blade list.</summary>
		internal static long AndroombaSkips, LightSkips, DrawSkips, AnyServed, AnyFull, ListBuilds;
		internal static readonly List<string> Installed = new();
		/// <summary>Check / lab switch: the patches stay installed and answer as the original (the same boot compares both).</summary>
		internal static bool Bypass;
		/// <summary>Check: every patched projectile question is also asked of the whole array and the answers compared.</summary>
		internal static bool Verify; internal static long AnyMismatch;

		private static int _bladeType = -1;

		/// <summary>CalamityPlayer fields the skipped calls would have set for a dummy, read only by the lighting, the darkness shader and the shield drawing: a parity check leaves them out while the patches are in force.</summary>
		internal static bool LightingOnly(string field) => On && !Bypass && Installed.Count > 0 && (field == "abyssDarkness" || field == "abyssPlayerGlowMultiplier" || field == "abyssFlashlightWidthMultiplier" || field == "darknessIntensity");

		public override void Load()
		{
			if (!On || !ModLoader.TryGetMod("CalamityMod", out Mod cal) || cal.Code == null) return;
			Type cp = cal.Code.GetType("CalamityMod.CalPlayer.CalamityPlayer");
			if (cp == null) { Mod.Logger.Warn("Levy: Calamity patches: CalamityPlayer not found, none installed"); return; }
			const BindingFlags all = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
			TryPatch(cp.GetMethod("AndroombaRightClick", all, null, Type.EmptyTypes, null), "AndroombaRightClick", il => SkipForDummy(il, nameof(SkipAndroomba), "AndroombaRightClick"));
			TryPatch(cp.GetMethod("UpdateDrawingParameters", all, null, Type.EmptyTypes, null), "UpdateDrawingParameters", il => SkipForDummy(il, nameof(SkipDrawing), "UpdateDrawingParameters"));
			TryPatch(cp.GetMethod("AbyssEffects", all, null, Type.EmptyTypes, null), "AbyssEffects", PatchAbyss);
			TryPatch(cp.GetMethod("ResetEffects", all, null, Type.EmptyTypes, null), "ResetEffects", PatchResetAny);
		}

		public override void PostSetupContent()
		{
			if (ModLoader.TryGetMod("CalamityMod", out Mod cal) && cal.TryFind<ModProjectile>("ViridVanguardBlade", out var b)) _bladeType = b.Type;
		}

		public override void Unload() { Installed.Clear(); _bladeType = -1; }

		private void TryPatch(MethodInfo m, string name, ILContext.Manipulator edit)
		{
			if (m == null || m.ReturnType != typeof(void)) { Mod.Logger.Warn("Levy: Calamity patch " + name + ": method not found or not void, skipped"); return; }
			try { MonoModHooks.Modify(m, edit); }
			catch (Exception e) { Mod.Logger.Warn("Levy: Calamity patch " + name + " failed, skipped: " + e.Message); }
		}

		// ------------------------------------------------------------------------------------------------------------
		// The dummy test
		// ------------------------------------------------------------------------------------------------------------
		/// <summary>The ModPlayer belongs to a levy's dummy (a puppet Player, never a person).</summary>
		private static bool IsDummy(ModPlayer mp) => !Bypass && mp != null && SoldierOwner.IsDummy(mp.Player);

		public static bool SkipAndroomba(ModPlayer mp) { if (!IsDummy(mp)) return false; AndroombaSkips++; return true; }
		public static bool SkipDrawing(ModPlayer mp) { if (Main.netMode != NetmodeID.Server || !IsDummy(mp)) return false; DrawSkips++; return true; }

		/// <summary>At the method's start: <c>if (Test(this)) return;</c></summary>
		private void SkipForDummy(ILContext il, string test, string name)
		{
			var c = new ILCursor(il);
			var go = c.DefineLabel();
			c.Emit(OpCodes.Ldarg_0);
			c.Emit(OpCodes.Call, typeof(CalamityLevyPatches).GetMethod(test, BindingFlags.Public | BindingFlags.Static));
			c.Emit(OpCodes.Brfalse_S, go);
			c.Emit(OpCodes.Ret);
			c.MarkLabel(go);
			lock (Installed) if (!Installed.Contains(name)) Installed.Add(name);
		}

		// ------------------------------------------------------------------------------------------------------------
		// AbyssEffects: the light levels call
		// ------------------------------------------------------------------------------------------------------------
		private void PatchAbyss(ILContext il)
		{
			var c = new ILCursor(il);
			if (!c.TryGotoNext(MoveType.Before, i => i.OpCode == OpCodes.Call && i.Operand is Mono.Cecil.MethodReference mr && mr.Name == "SetAbyssLightLevels" && mr.Parameters.Count == 1))
			{ Mod.Logger.Warn("Levy: Calamity patch AbyssEffects: SetAbyssLightLevels call not found, skipped"); return; }
			var target = (Mono.Cecil.MethodReference)c.Next.Operand;
			// the call takes the Player on the stack: replaced by ours, which calls the original unless the player is a dummy
			var orig = _lightOrig = ResolveLight(target);
			if (orig == null) { Mod.Logger.Warn("Levy: Calamity patch AbyssEffects: SetAbyssLightLevels not resolved, skipped"); return; }
			c.Next.Operand = il.Import(typeof(CalamityLevyPatches).GetMethod(nameof(LightLevels), BindingFlags.Public | BindingFlags.Static));
			lock (Installed) if (!Installed.Contains("AbyssEffects")) Installed.Add("AbyssEffects");
		}

		private static MethodInfo _lightOrig;
		private static Action<Player> _light;
		private static MethodInfo ResolveLight(Mono.Cecil.MethodReference r)
		{
			if (!ModLoader.TryGetMod("CalamityMod", out Mod cal)) return null;
			var t = cal.Code.GetType(r.DeclaringType.FullName.Replace('/', '+'));
			return t?.GetMethod(r.Name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(Player) }, null);
		}

		public static void LightLevels(Player p)
		{
			if (!Bypass && SoldierOwner.IsDummy(p)) { LightSkips++; return; }
			(_light ??= (Action<Player>)Delegate.CreateDelegate(typeof(Action<Player>), _lightOrig))(p);
		}

		// ------------------------------------------------------------------------------------------------------------
		// ResetEffects: the Virid Vanguard blade question
		// ------------------------------------------------------------------------------------------------------------
		private void PatchResetAny(ILContext il)
		{
			var c = new ILCursor(il);
			int found = 0;
			while (c.TryGotoNext(MoveType.Before, i => i.OpCode == OpCodes.Call && i.Operand is Mono.Cecil.GenericInstanceMethod gm && gm.Name == "Any" && gm.DeclaringType.FullName == "System.Linq.Enumerable"
				&& gm.GenericArguments.Count == 1 && gm.GenericArguments[0].FullName == "Terraria.Projectile" && gm.Parameters.Count == 2))
			{
				c.Next.Operand = il.Import(typeof(CalamityLevyPatches).GetMethod(nameof(AnyProjectile), BindingFlags.Public | BindingFlags.Static));
				found++; c.Index++;
			}
			if (found != 1) Mod.Logger.Warn("Levy: Calamity patch ResetEffects: " + found + " projectile Any calls (expected 1)" + (found == 0 ? ", skipped" : ""));
			if (found > 0) lock (Installed) if (!Installed.Contains("ResetEffects.Any")) Installed.Add("ResetEffects.Any");
		}

		private static readonly List<int> _blades = new();
		private static uint _bladeTick = uint.MaxValue;
		/// <summary>A Virid Vanguard blade was spawned: the list is made again at the next question.</summary>
		internal static void NoteSpawn(Projectile p) { if (p.type == _bladeType && _bladeType >= 0) _bladeTick = uint.MaxValue; }

		/// <summary>
		/// <c>Enumerable.Any(Main.projectile, pred)</c> as Calamity's ResetEffects asks it. Inside a levy's gear frame: the predicate on the slots that held an active Virid Vanguard blade when the list was made (the predicate requires
		/// an active blade, so no other slot can answer yes; a blade spawned since remakes the list). Anywhere else: the original.
		/// </summary>
		public static bool AnyProjectile(IEnumerable<Projectile> src, Func<Projectile, bool> pred)
		{
			if (Bypass || _bladeType < 0 || GearUpkeep.Body == null || !ReferenceEquals(src, Main.projectile)) { AnyFull++; return Enumerable.Any(src, pred); }
			uint now = Main.GameUpdateCount;
			if (_bladeTick != now)
			{
				_bladeTick = now; _blades.Clear(); ListBuilds++;
				var arr = Main.projectile; int bt = _bladeType;
				for (int i = 0; i < arr.Length; i++) { var p = arr[i]; if (p != null && p.active && p.type == bt) _blades.Add(i); }
			}
			AnyServed++;
			bool r = false;
			for (int k = 0; k < _blades.Count; k++) if (pred(Main.projectile[_blades[k]])) { r = true; break; }
			if (Verify && r != Enumerable.Any(src, pred)) AnyMismatch++;
			return r;
		}
	}

	/// <summary>Remakes the blade list when a Virid Vanguard blade is spawned.</summary>
	public class CalamityLevyPatchSpawns : GlobalProjectile
	{
		public override bool IsLoadingEnabled(Mod mod) => CalamityLevyPatches.On && ModLoader.HasMod("CalamityMod");
		public override void OnSpawn(Projectile projectile, Terraria.DataStructures.IEntitySource source) => CalamityLevyPatches.NoteSpawn(projectile);
	}
}
