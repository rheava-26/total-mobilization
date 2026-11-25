using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Targeting;
using Levy.Common.Weapons;

namespace Levy.Common.Compat.Redemption
{
	/// <summary>
	/// Compatibility for the Redemption mod. Runs only when it is loaded, and reads it through reflection (no reference, no hard dependency): every lookup that fails is logged and that part is simply off.
	///
	/// HOLD FIRE ON A CHOICE. Two of its bosses end in a moment where the PLAYER decides: strike the beaten boss, or wait. One enters a "death" state in which it can be hit for one damage at a time and ends itself if left alone;
	/// the other stops at one life point in a "spare countdown" state, asks the player a question on screen and waits for the answer. Neither is the army's call. For exactly those states the army holds fire on the boss
	/// (<see cref="HoldFire"/>): no levy targets it, no army shot hurts it, and it is not drawn off the commander. The states are read from the boss's own AI state (its ai[0], an enum), found by name, so a renamed state turns the hold off
	/// instead of misfiring.
	/// </summary>
	public class RedemptionCompat : ModSystem
	{
		/// <summary>The type and the AI-state values that hold fire, per boss: resolved at load, empty when the mod is absent or changed.</summary>
		private static readonly Dictionary<int, HashSet<int>> _holdStates = new();
		public static bool Loaded { get; private set; }
		/// <summary>One line per hold that was wired (or why not), for the world check.</summary>
		public static readonly List<string> Wired = new();

		public static bool HoldsFor(int type, int state) => _holdStates.TryGetValue(type, out var s) && s.Contains(state);

		public override void PostSetupContent()
		{
			if (!ModLoader.TryGetMod("Redemption", out Mod red)) return;
			Loaded = true;
			if (Environment.GetEnvironmentVariable("LEVY_REDECOMPAT") == "0") { Mod.Logger.Info("Levy Redemption compat: disabled by LEVY_REDECOMPAT=0 (test switch)"); return; }
			if (Environment.GetEnvironmentVariable("LEVY_LAB_REDE_ELEMENTOFF") == "1") LabElementsOff(red);
			WireDamageClass(red);
			try
			{
				// the first boss: the death state is the open choice
				Wire(red, "Redemption.NPCs.Bosses.Erhan.Erhan", new[] { "Death" });
				// the second: the spare countdown asks the question, "Spared" is the answer being played out
				Wire(red, "Redemption.NPCs.Bosses.KSIII.KS3", new[] { "SpareCountdown", "Spared" });
				if (_holdStates.Count > 0) HoldFire.Register(Provider);
			}
			catch (Exception e) { Mod.Logger.Warn("Levy Redemption compat: could not wire the hold-fire states (" + e.GetType().Name + ": " + e.Message + ")"); }
		}

		public override void Unload() { _holdStates.Clear(); Wired.Clear(); Loaded = false; if (_ritHook != null) { WeaponClassifier.ExtraCombatClasses.Remove(_ritHook); _ritHook = null; } _ritClass = null; }

		private static DamageClass _ritClass; private static Func<Item, bool> _ritHook;
		/// <summary>The mod's own damage class (a dagger class that counts as none of the five combat classes) is declared issuable: levies carry and use its weapons. Found by type name; a renamed class leaves it benched.</summary>
		private void WireDamageClass(Mod red)
		{
			try
			{
				Type t = red.Code?.GetType("Redemption.DamageClasses.RitualistClass");
				_ritClass = t == null ? null : ModContent.GetContent<DamageClass>().FirstOrDefault(d => d.GetType() == t);
				if (_ritClass == null) { Wired.Add("custom damage class not found"); return; }
				if (Environment.GetEnvironmentVariable("LEVY_REDECLASS") == "0") { Wired.Add("custom damage class: off (LEVY_REDECLASS=0)"); return; }
				_ritHook = it => it.ModItem != null && it.ModItem.Mod == red && it.DamageType == _ritClass;
				WeaponClassifier.ExtraCombatClasses.Add(_ritHook);
				Wired.Add("custom damage class -> issuable");
			}
			catch (Exception e) { Mod.Logger.Warn("Levy Redemption compat: could not wire the custom damage class (" + e.GetType().Name + ": " + e.Message + ")"); }
		}

		/// <summary>True for an item of the mod's own damage class.</summary>
		public static bool IsCustomClass(Item it) => _ritClass != null && it != null && it.DamageType == _ritClass;

		/// <summary>Lab switch (LEVY_LAB_REDE_ELEMENTOFF=1): turns the mod's own element damage scaling off through its server config, to tell its effect on a measurement from Levy's. Never on in play.</summary>
		private void LabElementsOff(Mod red)
		{
			try
			{
				Type cfg = red.Code.GetType("Redemption.RedeConfigServer");
				object inst = cfg?.GetField("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) ?? cfg?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
				FieldInfo f = cfg?.GetField("ElementDisable"); if (inst == null || f == null) { Mod.Logger.Info("Levy Redemption compat (lab): element switch not found"); return; }
				f.SetValue(inst, true); Mod.Logger.Info("Levy Redemption compat (lab): the mod's element damage scaling is OFF");
			}
			catch (Exception e) { Mod.Logger.Info("Levy Redemption compat (lab): element switch failed " + e.Message); }
		}

		private void Wire(Mod red, string typeName, string[] stateNames)
		{
			Type t = red.Code?.GetType(typeName);
			if (t == null) { Wired.Add(typeName + ": type not found"); Mod.Logger.Info("Levy Redemption compat: " + typeName + " not found, no hold"); return; }
			Type en = t.GetNestedType("ActionState", BindingFlags.Public | BindingFlags.NonPublic);
			if (en == null || !en.IsEnum) { Wired.Add(typeName + ": state enum not found"); return; }
			var vals = new HashSet<int>();
			foreach (string n in stateNames)
				if (Enum.IsDefined(en, n)) vals.Add(Convert.ToInt32(Enum.Parse(en, n)));
			if (vals.Count != stateNames.Length) { Wired.Add(typeName + ": a state name changed"); Mod.Logger.Info("Levy Redemption compat: " + typeName + " state names changed, no hold"); return; }
			int type = -1;
			foreach (var kv in red.GetContent<ModNPC>()) if (kv.GetType() == t) { type = kv.Type; break; }
			if (type < 0) { Wired.Add(typeName + ": not a registered NPC"); return; }
			_holdStates[type] = vals;
			Wired.Add(typeName + " -> type " + type + " holds in states [" + string.Join(",", vals) + "]");
		}

		private static bool Provider(NPC npc) => npc.type >= NPCID.Count && _holdStates.TryGetValue(npc.type, out var s) && s.Contains((int)npc.ai[0]);
	}
}
