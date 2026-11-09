using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace Levy.Common.Movement
{
	/// <summary>
	/// How much movement an accessory gives a soldier: extra jumps, wings, rocket flight, sprint speed, jump height, dash, wall climbing. Read generically as the delta of <see cref="MovementAbilities.FromDummy"/> on a dummy wearing it vs bare; cached per item type. The armory uses it to give every levy one mobility accessory first.
	/// </summary>
	public static class AccessoryMobility
	{
		private static readonly Dictionary<int, (int jumps, bool wings, float value)> Cache = new();
		private static Player _dummy;

		/// <summary>Extra jumps the item grants and whether it is a pair of wings.</summary>
		public static (int jumps, bool wings) Read(Item it) { var r = ReadAll(it); return (r.jumps, r.wings); }

		static (MovementAbilities ab, int rocketTier, int rocketTime, float jumpQ) Snap(Player p, Item worn)
		{
			p.ResetEffects();
			p.maxRunSpeed = 3f; p.accRunSpeed = 3f; p.runAcceleration = 0.08f; p.runSlowdown = 0.2f; // ResetEffects leaves the sprint fields from the last item worn
			for (int i = 0; i < p.armor.Length; i++) p.armor[i] = new Item();
			if (worn != null) p.armor[3] = worn;
			p.UpdateEquips(250);
			var ab = MovementAbilities.FromDummy(p);
			var r = (ab, p.rocketBoots, p.rocketTimeMax, JumpQuality(p));
			p.armor[3] = new Item();
			p.ResetEffects();
			return r;
		}

		/// <summary>
		/// Tie-break among single-jump bottles, added to the flat per-jump value (always under 0.5): Eve's order is Fart in a Bottle, Sandstorm, Blizzard, Tsunami, then Cloud, which is not the order of their raw jump
		/// durations (Sandstorm's is longer than Fart's), so the vanilla five are ranked by an explicit table. Any other jump (mods) counts its real height multiplier, a little under the table's best.
		/// </summary>
		static float JumpQuality(Player p)
		{
			float q = 0f;
			var ordered = ExtraJumpLoader.OrderedJumps;
			if (ordered == null) return 0f;
			for (int i = 0; i < ordered.Count; i++) {
				ExtraJump j = ordered[i];
				if (j == null || !p.GetJumpState(j).Enabled) continue;
				q += j switch {
					FartInAJarJump => 0.40f, SandstormInABottleJump => 0.30f, BlizzardInABottleJump => 0.20f, TsunamiInABottleJump => 0.10f, CloudInABottleJump => 0f,
					_ => Math.Clamp(JumpDuration(j, p) * 0.1f, 0f, 0.35f),
				};
			}
			return q;
		}

		static float JumpDuration(ExtraJump j, Player p) { try { return j.GetDurationMultiplier(p); } catch (Exception) { return 1f; } }

		internal static string Dbg(Item it) { var b = Snap(_dummy ??= new Player(), null); var c = new Item(); c.SetDefaults(it.type); var w = Snap(_dummy, c); return $"run {b.ab.MaxRunSpeed}->{w.ab.MaxRunSpeed} jump {b.ab.JumpTiles}->{w.ab.JumpTiles} rocket {b.rocketTier}/{b.rocketTime}->{w.rocketTier}/{w.rocketTime} dash {w.ab.HasDash} nofall {w.ab.NoFallDamage}"; }

		/// <summary>The wall-climb strength (Player.spikedBoots) a dummy gets wearing the item: 0 none, 1 weak, 2 strong.</summary>
		internal static int Spikes(Item it) { var c = new Item(); c.SetDefaults(it.type); return Snap(_dummy ??= new Player(), c).ab.SpikedBoots; }

		static (int jumps, bool wings, float value) ReadAll(Item it)
		{
			if (it == null || it.IsAir || !it.accessory || it.vanity) return (0, false, 0f);
			if (Cache.TryGetValue(it.type, out var hit)) return hit;
			var res = (0, it.wingSlot > 0, it.wingSlot > 0 ? LevyConstants.Armory.MobilityWingsValue : 0f);
			try {
				_dummy ??= new Player();
				var bare = Snap(_dummy, null);
				var c = new Item(); c.SetDefaults(it.type);
				var w = Snap(_dummy, c);
				int jumps = Math.Max(0, w.ab.ExtraJumps - bare.ab.ExtraJumps);
				bool wings = it.wingSlot > 0 || (w.ab.HasWings && !bare.ab.HasWings);
				float v;
				if (wings) v = LevyConstants.Armory.MobilityWingsValue;
				else {
					v = LevyConstants.Armory.MobilityJumpValue * Math.Min(jumps, LevyConstants.Armory.MobilityMaxJumpsCounted) + Math.Min(Math.Max(0f, w.jumpQ - bare.jumpQ), 0.45f);
					// rocket flight: tier 1 (Rocket Boots) = 1, each tier up (Spectre, Lightning, Terraspark) another full point
					if (w.rocketTime > bare.rocketTime || w.rocketTier > bare.rocketTier) v += 1f + Math.Min(Math.Max(w.rocketTier - 1, 0), 2);
					// top run speed gain (sprint), px/tick
					float dRun = w.ab.MaxRunSpeed - bare.ab.MaxRunSpeed; if (dRun > 0f) v += Math.Min(dRun, 4f) * 0.2f;
					// jump height (Frog Leg, balloons, Amphibian Boots)
					float dJump = w.ab.JumpTiles - bare.ab.JumpTiles; if (dJump > 0f) v += Math.Min(dJump, 6f) * 0.25f;
					if (w.ab.HasDash && !bare.ab.HasDash) v += 0.3f;
					// wall climb gear (Player.spikedBoots: Shoe Spikes, Climbing Claws, Tiger Climbing Gear, Master Ninja Gear, mods): a levy with it climbs any wall on a long trip
					if (w.ab.SpikedBoots > bare.ab.SpikedBoots) v += LevyConstants.Armory.MobilityClimbValue + (w.ab.SpikedBoots >= 2 ? LevyConstants.Armory.MobilityClimbHoldBonus : 0f);
					if (v > 0f && ((w.ab.NoFallDamage && !bare.ab.NoFallDamage) || w.ab.ExtraFall > bare.ab.ExtraFall)) v += 0.1f;
					v = Math.Min(v, LevyConstants.Armory.MobilityWingsValue - 0.1f);
				}
				res = (jumps, wings, v);
			}
			catch (Exception) { try { _dummy?.ResetEffects(); } catch (Exception) { } }
			Cache[it.type] = res;
			return res;
		}

		/// <summary>0 for nothing. Wings (4) outrank flight-plus-sprint boots (Spectre and up) and jump bundles (3), which outrank Rocket Boots, Frog Leg and a single bottle (about 1), which outrank sprint-only boots.</summary>
		public static float Value(Item it) => ReadAll(it).value;

		public static bool Is(Item it) => Value(it) > 0f;

		/// <summary>The best mobility value among the worn accessories (0 if none); empty slots and skipIndex are ignored.</summary>
		public static float Best(Item[] worn, int skipIndex = -1)
		{
			if (worn == null) return 0f;
			float b = 0f;
			for (int i = 0; i < worn.Length; i++) { if (i == skipIndex) continue; var w = worn[i]; if (w == null || w.IsAir) continue; b = Math.Max(b, Value(w)); }
			return b;
		}
	}
}

namespace Levy.Common.Movement
{
	/// <summary>
	/// What an accessory does for the wearer's survival, read the way <see cref="AccessoryMobility"/> reads movement: a dummy wears it and the delta against bare is measured (life regeneration, damage reduction,
	/// defense beyond the item's own, extra life). Cached per item type. This is the "effect value" part of the accessory score, so a small accessory that really does something is not bumped by a rarer one that does not.
	/// </summary>
	public static class AccessoryEffect
	{
		private static readonly Dictionary<int, float> Cache = new();
		private static Player _dummy;

		static (float regen, float end, int def, int life) Snap(Player p, Item worn)
		{
			p.ResetEffects();
			for (int i = 0; i < p.armor.Length; i++) p.armor[i] = new Item();
			if (worn != null) p.armor[3] = worn;
			p.UpdateEquips(250);
			var r = ((float)p.lifeRegen, p.endurance, p.statDefense, p.statLifeMax2);
			p.armor[3] = new Item();
			p.ResetEffects();
			return r;
		}

		/// <summary>Score points for survival effects: regeneration, damage reduction, bonus defense and life. Zero for an item that has none, and capped.</summary>
		public static float Value(Item it)
		{
			if (it == null || it.IsAir || !it.accessory || it.vanity) return 0f;
			if (Cache.TryGetValue(it.type, out float hit)) return hit;
			float v = 0f;
			try {
				_dummy ??= new Player();
				var bare = Snap(_dummy, null);
				var c = new Item(); c.SetDefaults(it.type);
				var w = Snap(_dummy, c);
				v = LevyConstants.AccessoryRegenWeight * Math.Max(0f, w.regen - bare.regen)
					+ LevyConstants.AccessoryEnduranceWeight * Math.Max(0f, w.end - bare.end)
					+ LevyConstants.AccessoryDefenseWeight * Math.Max(0, w.def - bare.def - c.defense)
					+ LevyConstants.AccessoryLifeWeight * Math.Max(0, w.life - bare.life);
				v = Math.Min(v, LevyConstants.AccessoryEffectCap);
			}
			catch (Exception) { try { _dummy?.ResetEffects(); } catch (Exception) { } }
			Cache[it.type] = v;
			return v;
		}
	}
}
