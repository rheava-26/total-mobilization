using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.Achievements;
using Terraria.ID;
using Terraria.ModLoader;

namespace Levy.Common.Testing
{
	/// <summary>
	/// The use-cycle probe's weapon: while <see cref="On"/>, the Amethyst Staff becomes a burst staff whose own Shoot picks its bolt from the player's itemAnimation and returns false, the way Calamity's
	/// Nucleosynthesis does (16-tick animation, a shot every 4, four a use, 14 ticks of reuse delay; itemAnimation 16, 12, 8, 4: topaz, sapphire, emerald, ruby bolt; any other value: nothing). Its
	/// <c>shoot</c> (the amethyst bolt) is then a placeholder that must never be fired. Off outside the check, so nothing in a real game changes.
	/// </summary>
	public sealed class UseCycleProbeItem : GlobalItem
	{
		public static bool On; public static int Mana = 10;
		public static readonly int[] Bolts = { ProjectileID.TopazBolt, ProjectileID.SapphireBolt, ProjectileID.EmeraldBolt, ProjectileID.RubyBolt };
		public override bool AppliesToEntity(Item entity, bool lateInstantiation) => entity.type == ItemID.AmethystStaff;
		public override void SetDefaults(Item item)
		{
			if (!On) return;
			item.useTime = 4; item.useAnimation = 16; item.reuseDelay = 14; item.useLimitPerAnimation = 4; item.autoReuse = true; item.mana = Mana;
		}
		public override bool Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
		{
			if (!On) return true;
			int k = -1;
			for (int i = 0; i < Bolts.Length; i++) if (player.itemAnimation == item.useAnimation - item.useTime * i) k = i;
			if (k < 0) return false;
			Projectile.NewProjectile(source, position, velocity, Bolts[k], damage, knockback, player.whoAmI);
			return false;
		}
	}

	public partial class IdentityWorldCheck
	{
		private static bool _armyNoManaRefill;

		/// <summary>
		/// THE USE-CYCLE CLASS CHECK, with no other mod needed (runs in the vanilla and the Calamity world). A staff whose Shoot keys off itemAnimation (<see cref="UseCycleProbeItem"/>) is fired by a levy and by the
		/// stand-in player: the levy must make the same four bolts in the same order and about as many, and never the item's placeholder shot. A second levy run with the mana pool NOT refilled and a cast that
		/// costs three quarters of the pool proves the burst is paid once per use, as for a player: its first four shots are the whole topaz-sapphire-emerald-ruby burst (paying per shot left it one bolt).
		/// </summary>
		private static void UseCycleProbe()
		{
			int ticks = 300;
			SetupOriginArena(); _spNet = false;
			On_AchievementsHelper.HandleSpecialEvent += NoAchievements;
			UseCycleProbeItem.On = true; UseCycleProbeItem.Mana = 10;
			string[] own = UseCycleProbeItem.Bolts.Select(ArmyProjName).ToArray();
			try
			{
				KeepCommander(); Step(2);
				var row = new ArmyRow { Label = "use-cycle probe (Amethyst Staff as a 4-shot itemAnimation-keyed burst)", Name = "use cycle probe", Cls = "Magic", ShootName = ArmyProjName(ProjectileID.AmethystBolt) };
				row.A = ArmyOne(ItemID.AmethystStaff, 0, ticks, false, false);
				row.B = ArmyOne(ItemID.AmethystStaff, 0, ticks, false, true);
				row.Player = ArmyOne(ItemID.AmethystStaff, 0, ticks, true, false);
				ArmyClassify(row);
				Note($"use-cycle probe {row.Status} ratio {row.RatioA:0.00} / {row.RatioB:0.00} {row.Cause} || {row.Player} || {row.A} || {row.B}");
				ArmyUseCycleCheck(row, own);
				Check($"use-cycle probe: no placeholder shot in levy hands ({row.ShootName} levy {(row.A.Types.TryGetValue(row.ShootName, out int pa) ? pa : 0)} / {(row.B.Types.TryGetValue(row.ShootName, out int pb) ? pb : 0)}) and the damage is a player's ({row.Status}, {row.RatioA:0.00} / {row.RatioB:0.00})", row.Status == "OK");

				// the mana: one cast costs 150 of the levy's 200, the pool is not topped up; a player pays once per use, so the whole burst comes out
				UseCycleProbeItem.Mana = 150; _armyNoManaRefill = true;
				ArmyRes m = ArmyOne(ItemID.AmethystStaff, 0, 120, false, false);
				var first = m.Seq.Where(t => own.Contains(t) || t == row.ShootName).Take(8).ToList();
				bool burst = Enumerable.Range(0, Math.Max(0, first.Count - own.Length + 1)).Any(i => first.Skip(i).Take(own.Length).SequenceEqual(own)); // paid per shot, 150 of 200 leaves one bolt: four in a row never happen
				Check($"use-cycle probe: a burst costs its mana once, not per shot (pool full once then not refilled, cast 150 of 200: shots [{string.Join(",", first)}])", burst);
			}
			catch (Exception e) { Check("use-cycle probe threw " + e.GetType().Name + ": " + e.Message, false); }
			finally { UseCycleProbeItem.On = false; UseCycleProbeItem.Mana = 10; _armyNoManaRefill = false; _sweepTarget = null; On_AchievementsHelper.HandleSpecialEvent -= NoAchievements; }
		}
	}
}
