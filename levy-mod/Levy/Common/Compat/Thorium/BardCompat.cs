using System;
using Terraria;
using Terraria.ModLoader;
using ThoriumMod;
using ThoriumMod.Items;
using ThoriumMod.Items.BardItems;
using Levy.Common.Config;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Content.NPCs;

namespace Levy.Common.Compat.Thorium
{
	/// <summary>
	/// Thorium Bard on a Levy soldier. Levy fires the instrument's real <c>Shoot</c> on the soldier's dummy Player, but never runs the item-use
	/// pipeline (<c>CanUseItem</c> / <c>UseAnimation</c>), so the Inspiration economy has to be supplied here:
	/// <list type="bullet">
	/// <item>the pool lives on the dummy's <see cref="ThoriumPlayer"/> (<c>bardResource</c>, <c>bardResourceMax</c>);</item>
	/// <item>its size is the soldier's OWN permanent maximum (10 + the Inspiration Fragments / Shards / Crystals he has absorbed from his home Armory, kept on his record, cap 40);</item>
	/// <item>it refills on Thorium's own regeneration wheel, re-run here each tick because the dummy never gets <c>Player.Update</c>;</item>
	/// <item>each shot is gated on and charged against the weapon's real inspiration cost (<see cref="TryPlay"/>).</item>
	/// </list>
	/// The instrument's empowerments (its team buffs) are applied to the squad and the commander by <see cref="EmpowerCompat"/> each time the soldier plays.
	/// </summary>
	[JITWhenModsEnabled("ThoriumMod")]
	public static class BardCompat
	{
		public static bool IsBardWeapon(Item item) => item != null && !item.IsAir && item.ModItem is BardItem;

		/// <summary>Inspiration cost of one play of this instrument for this soldier (Thorium's own formula, run on the dummy).</summary>
		public static int CostOf(Item item, Player dummy)
		{
			if (item.ModItem is not BardItem bi) return 0;
			try { return bi.GetInspirationCost(dummy); }
			catch (Exception) { return Math.Max(0, bi.InspirationCost); }
		}

		/// <summary>PreFireGate arm: true (and the cost is paid, with Thorium's post-spend regen penalty) when the pool covers one play; false = the soldier holds.</summary>
		public static bool TryPlay(SoldierNPC s, Item item)
		{
			Player d = s?.Dummy;
			if (d == null) return true;
			try
			{
				ThoriumPlayer tp = d.GetModPlayer<ThoriumPlayer>();
				int cost = CostOf(item, d);
				if (cost <= 0) { PlayToSquad(s, item); return true; }
				if (tp.bardResource < cost) return false;
				BardItem.ConsumeInspiration(d, cost);
				PlayToSquad(s, item);
				return true;
			}
			catch (Exception) { return true; } // never let a Thorium surprise stop a soldier from fighting
		}

		/// <summary>The note has sounded: give the squad (and the commander) the instrument's empowerments.</summary>
		private static void PlayToSquad(SoldierNPC s, Item item)
		{
			try
			{
				if (item.ModItem is BardItem bi && s.NPC.TryGetGlobalNPC(out SoldierThoriumGlobalNPC st)) EmpowerCompat.Play(s, bi, st);
			}
			catch (Exception) { /* empowerments are a bonus: never let them stop the music */ }
		}

		/// <summary>Key of the soldier's own Inspiration maximum in <c>SoldierRecord.Ext</c> (absent = Thorium's base 10).</summary>
		public const string MaxKey = "thorium.inspirationMax";
		public const int BaseMax = 10, CapMax = 40;
		/// <summary>Ticks between a bard's checks of the Armory for an Inspiration item.</summary>
		private const int FeedIntervalTicks = 180;

		/// <summary>The soldier's permanent Inspiration maximum, 10..40.</summary>
		public static int StoredMax(SoldierRecord rec) =>
			rec != null && rec.Ext != null && rec.Ext.TryGetValue(MaxKey, out int v) ? Math.Clamp(v, BaseMax, CapMax) : BaseMax;

		/// <summary>The Thorium item that raises a maximum of <paramref name="max"/> by one (Fragment 10-19, Shard 20-29, Crystal 30-39), or 0 at the cap.</summary>
		private static int FeedItemFor(int max) => max < 20 ? ModContent.ItemType<InspirationFragment>() : max < 30 ? ModContent.ItemType<InspirationShard>() : max < CapMax ? ModContent.ItemType<InspirationCrystalNew>() : 0;

		/// <summary>
		/// Like the army's growth-crystal auto-feed: every few seconds a bard below the cap takes ONE Inspiration item that fits his tier from his home Armory's storage and
		/// raises his own maximum by one. No micromanagement; honours the AutoFeedCrystals setting. Server / single player.
		/// </summary>
		private static void FeedCrystals(SoldierNPC s, SoldierThoriumGlobalNPC state)
		{
			if (Main.GameUpdateCount < state.NextFeedTick) return;
			state.NextFeedTick = Main.GameUpdateCount + (uint)FeedIntervalTicks + (uint)(s.NPC.whoAmI % 30);
			var cfg = LevyConfig.Get;
			if (cfg == null || !cfg.AutoFeedCrystals || s.Record == null || s.Record.Reserve) return;
			int max = StoredMax(s.Record);
			int item = FeedItemFor(max);
			if (item <= 0) return;
			var home = s.Home;
			var armory = home == null ? null : SettlementSystem.Instance.LinkedArmory(home);
			if (armory == null || !armory.TryTakeOne(item)) return;
			s.Record.Ext[MaxKey] = max + 1;
			CombatText.NewText(s.NPC.Hitbox, new Microsoft.Xna.Framework.Color(165, 255, 185), 1);
			Terraria.Audio.SoundEngine.PlaySound(Terraria.ID.SoundID.Item9, s.NPC.Center);
			s.NPC.netUpdate = true;
		}

		/// <summary>Per tick (server / single player), for a soldier carrying an instrument: pool size from his own record, then Thorium's regen wheel.</summary>
		public static void Tick(SoldierNPC s, SoldierThoriumGlobalNPC state)
		{
			Player d = s.Dummy;
			if (d == null) return;
			ThoriumPlayer tp = d.GetModPlayer<ThoriumPlayer>();

			// Pool size: this soldier's OWN permanent maximum (10 + the Inspiration items he has absorbed, kept on his record), grown from the home Armory's storage.
			FeedCrystals(s, state);
			tp.bardResourceMax = StoredMax(s.Record);
			tp.bardResourceMax2 = Math.Clamp(Math.Max(tp.bardResourceMax2, tp.bardResourceMax), tp.bardResourceMax, Math.Min(60, tp.bardResourceMax + 20));

			// A soldier is handed a full pool the first time this runs; after that only the regen wheel (and the spend) move it.
			if (!state.BardPoolInit) { tp.bardResource = tp.bardResourceMax2; state.BardPoolInit = true; }
			if (tp.bardResource > tp.bardResourceMax2) tp.bardResource = tp.bardResourceMax2;

			// Thorium's regen wheel, verbatim (ThoriumPlayer: runs once per tick while the pool is below its max).
			if (tp.bardResource < tp.bardResourceMax2)
			{
				if (tp.inspirationRegenTimer < 8f)
				{
					tp.inspirationRegenTimer += (int)((1f + tp.inspirationRegenBase) * tp.inspirationRegenBonus);
				}
				else
				{
					if (tp.inspirationRegenBase < 5f) tp.inspirationRegenBase += 0.15f;
					tp.inspirationRegenTimer = 0f;
					tp.bardResource++;
				}
				if (tp.bardResource >= tp.bardResourceMax2) tp.inspirationRegenBase = 0f;
			}

			// What clients draw over his head: current / maximum Inspiration, re-synced when it moves (at most a few times a second).
			byte cur = (byte)Math.Clamp(tp.bardResource, 0, 255), max = (byte)Math.Clamp(tp.bardResourceMax2, 0, 255);
			if ((state.InspCur != cur || state.InspMax != max) && Main.GameUpdateCount - state.LastDisplayNet >= 15)
			{
				state.InspCur = cur; state.InspMax = max;
				state.LastDisplayNet = Main.GameUpdateCount;
				s.NPC.netUpdate = true;
			}
		}
	}
}
