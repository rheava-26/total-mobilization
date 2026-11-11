using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common;
using Levy.Common.Systems;
using Levy.Content.NPCs;

namespace Levy.Content.Buffs
{
	/// <summary>
	/// Fizzy (Fizz Pop, Fizz Tank): low gravity (a quarter, vanilla's floor at the top of the Space layer), a double jump, six times the knockback, and no knockback immunity (it becomes a little damage reduction).
	/// A real player gets the effect from the buff itself (<see cref="FizzyPlayer"/>); a levy gets the gravity and knockback through <see cref="ProvisionsBody"/>, the double jump through <see cref="DrinkBuffs.Apply"/>
	/// (its dummy never runs UpdateBuffs) and the hit side through SoldierNPC.Fizz.cs.
	/// </summary>
	public class FizzyBuff : ModBuff
	{
		public override string LocalizationCategory => "Provisions";
		public override string Texture => "Terraria/Images/Buff_" + BuffID.Featherfall;

		public override void SetStaticDefaults()
		{
			Main.debuff[Type] = false; Main.pvpBuff[Type] = false;
			Main.buffNoSave[Type] = true;
		}

		public override void ModifyBuffText(ref string buffName, ref string tip, ref int rare) => tip = string.Format(tip, (int)Math.Round(LevyConstants.FizzImmunityDr * 100f));

		public override void Update(Player player, ref int buffIndex) => ProvisionRules.ApplyFizzy(player);
	}

	/// <summary>
	/// Lava Shot (Iceberg Shot, Iceberg Tray; ice-cold vodka, so numb it ignores lava, and Tipsy like the other spirits): immunity to lava and to the Burning it sets, and a body that swims through lava the way it swims through water.
	/// A real player gets Obsidian Skin's flags from the buff; a levy gets the flags from <see cref="ProvisionsBody"/> and the swim from SoldierNPC.Movement.
	/// </summary>
	public class LavaShotBuff : ModBuff
	{
		public override string LocalizationCategory => "Provisions";
		public override string Texture => "Terraria/Images/Buff_" + BuffID.ObsidianSkin;

		public override void SetStaticDefaults()
		{
			Main.debuff[Type] = false; Main.pvpBuff[Type] = false;
			Main.buffNoSave[Type] = true;
		}

		public override void ModifyBuffText(ref string buffName, ref string tip, ref int rare) => tip = string.Format(tip, (int)Math.Round((LevyConstants.DrunkAimErrorMult - 1f) * 100f));

		public override void Update(Player player, ref int buffIndex)
		{
			player.lavaImmune = true; player.fireWalk = true; player.buffImmune[BuffID.OnFire] = true;
		}
	}

	/// <summary>Lava immunity and Fizzy gravity and knockback for a levy's body, from the buffs it carries (set each tick before its AI runs; the soldier files only read NPC.lavaImmune, NPC.gravity and NPC.knockBackResist).</summary>
	public class ProvisionsBody : GlobalNPC
	{
		public override bool InstancePerEntity => true;
		private bool _lava, _kb, _fireWasImmune;
		/// <summary>The body carries Fizzy this tick (refreshed in PreAI and on a hit).</summary>
		public bool Fizzy;

		public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => lateInstantiation && entity.ModNPC is SoldierNPC;

		public override bool PreAI(NPC npc)
		{
			Refresh(npc);
			return true;
		}

		/// <summary>Brings lava immunity (with Burning immunity), knockback resistance and gravity in line with the buffs the body carries now.</summary>
		public void Refresh(NPC npc)
		{
			bool lava = npc.HasBuff(ModContent.BuffType<LavaShotBuff>());
			bool fizz = npc.HasBuff(ModContent.BuffType<FizzyBuff>());
			Fizzy = fizz;
			npc.lavaImmune = lava;
			if (lava)
			{
				if (!_lava) { _lava = true; _fireWasImmune = npc.buffImmune[BuffID.OnFire]; }
				npc.buffImmune[BuffID.OnFire] = true;
				int fire = npc.FindBuffIndex(BuffID.OnFire);
				if (fire >= 0) npc.DelBuff(fire);
			}
			else if (_lava) { _lava = false; npc.buffImmune[BuffID.OnFire] = _fireWasImmune; }

			if (fizz) { if (!_kb && npc.knockBackResist <= 0f) { _kb = true; npc.knockBackResist = LevyConstants.FizzKnockbackResist; } }
			else if (_kb) { _kb = false; npc.knockBackResist = 0f; }

			// low gravity: vanilla resets NPC.GravityMultiplier every tick before the AI and reads it after, so it is multiplied here each tick (NPC.gravity, which the movement code reads for its jump speed, its fall prediction and its hook arcs, follows it)
			if (fizz) npc.GravityMultiplier *= LevyConstants.FizzGravityMult;
		}
	}

	/// <summary>The same Fizzy rules on a real player (a Fizz Can drunk by the commander): knockback immunity is turned off and paid back as damage reduction, and every hit carries six times the knockback.</summary>
	public class FizzyPlayer : ModPlayer
	{
		private bool _converted;

		public override void PostUpdateEquips()
		{
			_converted = false;
			if (!Player.HasBuff<FizzyBuff>()) return;
			if (Player.noKnockback) { Player.noKnockback = false; _converted = true; }
		}

		public override void ModifyHurt(ref Player.HurtModifiers modifiers)
		{
			if (!Player.HasBuff<FizzyBuff>()) return;
			modifiers.Knockback *= LevyConstants.FizzKnockbackMult;
			if (_converted) modifiers.FinalDamage *= 1f - LevyConstants.FizzImmunityDr;
		}
	}

	/// <summary>The Provisions rules: drink and meal ids, Fizzy's numbers, the Well Fed ladder, the nearest-first pick and the serving of a round.</summary>
	public static class ProvisionRules
	{
		/// <summary>Round ids on the wire (byte): 0 Ale, 1 Stout, 2 Rum (DrinkBuffs), then Fizz Tank, Magma Crucible and the four meals.</summary>
		public const int FizzId = 3, LavaId = 4, MealBase = 5, MealCount = 4, MaxServeId = MealBase + MealCount - 1;

		/// <summary>Fizzy's effect on a player or a levy's dummy: a Cloud in a Bottle jump, and a quarter of the gravity (a player; the levy's body carries its own, see <see cref="ProvisionsBody"/>).</summary>
		public static void ApplyFizzy(Player p)
		{
			p.GetJumpState(ExtraJump.CloudInABottle).Enable();
			p.gravity *= LevyConstants.FizzGravityMult;
		}

		public static int WellFedBuff(int tier) => tier <= 1 ? BuffID.WellFed : tier == 2 ? BuffID.WellFed2 : BuffID.WellFed3;

		/// <summary>The highest Well Fed tier the body carries (0 none).</summary>
		public static int HeldTier(NPC n)
		{
			for (int t = 3; t >= 1; t--) if (n.HasBuff(WellFedBuff(t))) return t;
			return 0;
		}

		/// <summary>Indices of the points within the drink radius of <paramref name="centre"/>, nearest first, at most <paramref name="max"/>.</summary>
		public static List<int> NearestInReach(IReadOnlyList<Vector2> at, Vector2 centre, int max)
		{
			var hit = new List<(float d, int i)>();
			for (int i = 0; i < at.Count; i++) if (DrinkBuffs.InReach(centre, at[i])) hit.Add((Vector2.DistanceSquared(centre, at[i]), i));
			hit.Sort((a, b) => a.d.CompareTo(b.d));
			var res = new List<int>(Math.Min(max, hit.Count));
			for (int k = 0; k < hit.Count && k < max; k++) res.Add(hit[k].i);
			return res;
		}

		private static List<SoldierNPC> Eligible(string commander)
		{
			var l = new List<SoldierNPC>();
			if (RosterSystem.Instance == null) return l;
			foreach (SoldierNPC s in RosterSystem.Instance.ActiveBodies())
			{
				if (s == null || !s.IsAlive || s.Record == null) continue;
				if (commander != null && s.IsRegular && s.Record.CommanderName != commander) continue;
				l.Add(s);
			}
			return l;
		}

		/// <summary>Serves a drink round (Fizz Tank, Magma Crucible) to every levy in reach of <paramref name="centre"/>. Returns how many.</summary>
		public static int ServeDrink(Vector2 centre, int id, string commander)
		{
			int type = id == FizzId ? ModContent.BuffType<FizzyBuff>() : ModContent.BuffType<LavaShotBuff>();
			int ticks = id == FizzId ? LevyConstants.DrinkDurationTicks : LevyConstants.LavaShotDurationTicks;
			var served = new List<SoldierNPC>();
			foreach (SoldierNPC s in Eligible(commander))
			{
				if (!DrinkBuffs.InReach(centre, s.NPC.Center)) continue;
				s.NPC.AddBuff(type, ticks);
				s.StatsDirty = true;
				served.Add(s);
			}
			DrinkCheers.Begin(served, commander);
			return served.Count;
		}

		/// <summary>Serves a meal to the nearest levies in reach (the meal's feed count), giving Well Fed of its tier and Morale. Returns how many were fed.</summary>
		public static int ServeMeal(Vector2 centre, int meal, string commander)
		{
			meal = Math.Clamp(meal, 0, MealCount - 1);
			var pool = Eligible(commander);
			var at = new List<Vector2>(pool.Count);
			foreach (var s in pool) at.Add(s.NPC.Center);
			var fed = new List<SoldierNPC>();
			foreach (int i in NearestInReach(at, centre, LevyConstants.MealFeeds[meal]))
			{
				Feed(pool[i], LevyConstants.MealWellFedTier[meal]);
				fed.Add(pool[i]);
			}
			DrinkCheers.Begin(fed, commander, meal: true);
			return fed.Count;
		}

		/// <summary>One levy eats: Well Fed of <paramref name="tier"/> unless it already holds a higher tier (a lower one is replaced), and Morale.</summary>
		public static void Feed(SoldierNPC s, int tier)
		{
			NPC n = s.NPC;
			int have = HeldTier(n);
			if (have <= tier)
			{
				if (have < tier) for (int t = 1; t < tier; t++) { int ix = n.FindBuffIndex(WellFedBuff(t)); if (ix >= 0) n.DelBuff(ix); }
				n.AddBuff(WellFedBuff(tier), LevyConstants.MealDurationTicks);
			}
			n.AddBuff(ModContent.BuffType<MoraleBuff>(), LevyConstants.MealMoraleTicks);
			s.StatsDirty = true;
		}

		/// <summary>Serves a round by its wire id (3 and up). Single player and the server call this.</summary>
		public static int Serve(Vector2 centre, int id, string commander)
		{
			if (id == FizzId || id == LavaId) return ServeDrink(centre, id, commander);
			if (id >= MealBase && id <= MaxServeId) return ServeMeal(centre, id - MealBase, commander);
			return 0;
		}

		/// <summary>True if a levy body is in reach of the player (used to refuse a round that would feed nobody).</summary>
		public static bool AnyLevyInReach(Vector2 centre)
		{
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (n.active && n.ModNPC is SoldierNPC && DrinkBuffs.InReach(centre, n.Center)) return true;
			}
			return false;
		}
	}
}
