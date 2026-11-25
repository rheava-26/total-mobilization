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
		/// <summary>One line for the custom damage class wiring (kept apart from <see cref="Wired"/>, which lists the hold-fire bosses only).</summary>
		public static readonly List<string> WiredClass = new();

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

		public override void Unload() { _fedGauges.Clear(); _gaugeF = _gaugeCdF = _gaugeCdMaxF = _levelF = null; _getModPlayer = _gaugePostUpdate = null; _ritPlayerType = null; _ritPlayerBase = null; _holdStates.Clear(); Wired.Clear(); WiredClass.Clear(); Loaded = false; if (_ritHook != null) { WeaponClassifier.ExtraCombatClasses.Remove(_ritHook); _ritHook = null; } _ritClass = null; }

		private static DamageClass _ritClass; private static Func<Item, bool> _ritHook;
		/// <summary>The mod's own damage class (a dagger class that counts as none of the five combat classes) is declared issuable: levies carry and use its weapons. Found by type name; a renamed class leaves it benched.</summary>
		private void WireDamageClass(Mod red)
		{
			try
			{
				Type t = red.Code?.GetType("Redemption.DamageClasses.RitualistClass");
				_ritClass = t == null ? null : ModContent.GetContent<DamageClass>().FirstOrDefault(d => d.GetType() == t);
				if (_ritClass == null) { WiredClass.Add("custom damage class not found"); return; }
				if (Environment.GetEnvironmentVariable("LEVY_REDECLASS") == "0") { WiredClass.Add("custom damage class: off (LEVY_REDECLASS=0)"); return; }
				_ritHook = it => it.ModItem != null && it.ModItem.Mod == red && it.DamageType == _ritClass;
				WeaponClassifier.ExtraCombatClasses.Add(_ritHook);
				WiredClass.Add("custom damage class -> issuable");
			}
			catch (Exception e) { Mod.Logger.Warn("Levy Redemption compat: could not wire the custom damage class (" + e.GetType().Name + ": " + e.Message + ")"); }
		}

		// ---- the custom class's gauge. Its weapons fill a spirit gauge on their owner: a hit has a one in three chance to drop one or two pickups worth 5 each, which the player collects by touching them; the gauge rises to levels the
		// weapons read (an empowered mode). The drops are world items a levy never collects, and the hit hook that makes them runs on the commander (the shot's owner), so a levy's gauge would never leave zero. Each hit of a levy's
		// custom-class shot adds the gauge those pickups are worth on average (5 x 1/3 x 1.5) to the levy's own player.
		private static Type _ritPlayerType; private static ModPlayer _ritPlayerBase; private static System.Reflection.MethodInfo _getModPlayer; private static System.Reflection.FieldInfo _gaugeF, _gaugeCdF, _gaugeCdMaxF;
		internal const float GaugePerHit = 2.5f;

		private static bool WireGauge(Mod red)
		{
			if (_getModPlayer != null) return true;
			try
			{
				_ritPlayerType = red.Code?.GetType("Redemption.Globals.Players.RitualistPlayer");
				if (_ritPlayerType == null) return false;
				_ritPlayerBase = ModContent.GetContent<ModPlayer>().FirstOrDefault(m => m.GetType() == _ritPlayerType);
				_gaugeF = _ritPlayerType.GetField("SpiritGauge"); _gaugeCdF = _ritPlayerType.GetField("SpiritGaugeCD"); _gaugeCdMaxF = _ritPlayerType.GetField("SpiritGaugeCDMax");
				var gm = typeof(Player).GetMethods().FirstOrDefault(m => m.Name == "GetModPlayer" && m.IsGenericMethodDefinition && m.GetParameters().Length == 1);
				if (_ritPlayerBase == null || _gaugeF == null || _gaugeCdF == null || gm == null) return false;
				_getModPlayer = gm.MakeGenericMethod(_ritPlayerType);
				return true;
			}
			catch (Exception) { _getModPlayer = null; return false; }
		}

		/// <summary>A hit of a levy's custom-class shot: feeds the gauge on the levy's own player (called by <see cref="RedemptionRitualHits"/>).</summary>
		internal static void FeedGauge(Projectile proj)
		{
			try
			{
				if (_ritClass == null || proj.DamageType != _ritClass || !proj.TryGetGlobalProjectile(out global::Levy.Common.Globals.SoldierProjectileTag tag) || tag.SoldierIndex < 0 || tag.SoldierIndex >= Main.maxNPCs) return;
				if (!(Main.npc[tag.SoldierIndex].ModNPC is global::Levy.Content.NPCs.SoldierNPC sn) || sn.Dummy == null) return;
				if (!ModLoader.TryGetMod("Redemption", out Mod red) || !WireGauge(red)) return;
				object mp = _getModPlayer.Invoke(sn.Dummy, new object[] { _ritPlayerBase });
				if (mp == null) return;
				_gaugeCdF.SetValue(mp, _gaugeCdMaxF?.GetValue(mp) ?? 600f);
				_gaugeF.SetValue(mp, (float)_gaugeF.GetValue(mp) + GaugePerHit);
				_fedGauges[tag.SoldierIndex] = mp; // its per-tick step (PostUpdate) is never run on a levy's player: it is run here while the gauge is up
			}
			catch (Exception) { }
		}

		private static readonly Dictionary<int, object> _fedGauges = new(); private static readonly List<int> _fedDone = new(); private static System.Reflection.MethodInfo _gaugePostUpdate; private static System.Reflection.FieldInfo _levelF;

		/// <summary>The mod's own per-tick gauge step (decay, level up and down) for the levies whose gauge was fed: a player runs it every tick, a levy's player never does.</summary>
		public override void PostUpdateEverything()
		{
			if (_fedGauges.Count == 0) return;
			_gaugePostUpdate ??= _ritPlayerType?.GetMethod("PostUpdate", Type.EmptyTypes); _levelF ??= _ritPlayerType?.GetField("SpiritLevel");
			_fedDone.Clear();
			foreach (var kv in _fedGauges)
			{
				try
				{
					Terraria.ModLoader.ModPlayer mp = (Terraria.ModLoader.ModPlayer)kv.Value;
					if (!Main.npc[kv.Key].active) { _fedDone.Add(kv.Key); continue; }
					_gaugePostUpdate.Invoke(mp, null);
					if ((float)_gaugeF.GetValue(mp) <= 0f && (int)_levelF.GetValue(mp) <= 0) _fedDone.Add(kv.Key);
				}
				catch (Exception) { _fedDone.Add(kv.Key); }
			}
			foreach (int k in _fedDone) _fedGauges.Remove(k);
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
	/// <summary>Feeds a levy's gauge on a hit of its custom-class shot (see RedemptionCompat.FeedGauge). Applies only to the loaded mod's own projectiles.</summary>
	public class RedemptionRitualHits : GlobalProjectile
	{
		public override bool AppliesToEntity(Projectile entity, bool lateInstantiation) => entity.type >= Terraria.ID.ProjectileID.Count && ModLoader.HasMod("Redemption");
		public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone) => RedemptionCompat.FeedGauge(projectile);
	}
}
