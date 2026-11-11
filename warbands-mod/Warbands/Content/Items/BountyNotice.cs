using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Levy.Common.Systems;
using Warbands.Common;

namespace Warbands.Content.Items
{
	/// <summary>
	/// The bounty notice a beaten band leaves behind. It remembers which band it is for and how much of the band was beaten. Using it at a Levy Barracks or next to a town NPC turns it in and pays the band's bounty.
	/// </summary>
	public class BountyNotice : ModItem
	{
		public byte Band = 0, SharePct = 100;
		public const float TurnInTiles = 32f;
		protected override bool CloneNewInstances => true;

		public override void SetStaticDefaults() { Item.ResearchUnlockCount = 1; }

		public override void SetDefaults()
		{
			Item.width = 32; Item.height = 32; Item.maxStack = 99; Item.consumable = true;
			Item.useStyle = ItemUseStyleID.HoldUp; Item.useTime = Item.useAnimation = 30; Item.UseSound = SoundID.Item35;
			Item.rare = ItemRarityID.Blue; Item.value = 0;
		}

		/// <summary>Points the notice at a band type and the share of that band that was beaten (percent).</summary>
		public void SetBand(int typeIdx, int sharePct)
		{
			Band = (byte)Math.Clamp(typeIdx, 0, 255); SharePct = (byte)Math.Clamp(sharePct, 1, 100);
			Refresh();
		}

		private BandRoster Roster => BandTypes.FromIndex(Band);

		private void Refresh()
		{
			var r = Roster; if (r == null) return;
			Item.SetNameOverride($"{r.Name} Bounty Notice");
			Item.rare = r.Stage <= 1 ? ItemRarityID.Blue : r.Stage <= 2 ? ItemRarityID.Green : r.Stage <= 3 ? ItemRarityID.LightRed : r.Stage <= 4 ? ItemRarityID.Pink : r.Stage <= 5 ? ItemRarityID.Lime : ItemRarityID.Yellow;
			Item.value = Item.sellPrice(copper: (int)(r.BountyCopper * 0.1f * SharePct / 100f));
		}

		public override bool CanStack(Item source) => source.ModItem is BountyNotice o && o.Band == Band && o.SharePct == SharePct;
		public override bool CanStackInWorld(Item source) => CanStack(source);

		public override void SaveData(TagCompound tag) { tag["band"] = (int)Band; tag["share"] = (int)SharePct; }
		public override void LoadData(TagCompound tag) { Band = (byte)tag.GetInt("band"); SharePct = (byte)Math.Max(1, tag.GetInt("share")); Refresh(); }
		public override void NetSend(BinaryWriter writer) { writer.Write(Band); writer.Write(SharePct); }
		public override void NetReceive(BinaryReader reader) { Band = reader.ReadByte(); SharePct = reader.ReadByte(); Refresh(); }

		public override void ModifyTooltips(List<TooltipLine> tooltips)
		{
			var r = Roster; if (r == null) return;
			string beaten = SharePct >= 100 ? "The whole band was beaten" : $"{SharePct}% of the band was beaten";
			tooltips.Add(new TooltipLine(Mod, "Bounty", beaten));
			tooltips.Add(new TooltipLine(Mod, "TurnIn", "Turn in at a Barracks or town NPC to claim the bounty"));
		}

		/// <summary>A Levy Barracks or a town NPC is close enough to take the notice.</summary>
		public static bool CanTurnInHere(Player p)
		{
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (n.active && n.townNPC && n.friendly && Vector2Dist(n, p) < TurnInTiles) return true;
			}
			try { if (SettlementSystem.Instance?.NearestBarracks(p.Center, TurnInTiles * 16f) != null) return true; } catch { }
			return false;
		}
		private static float Vector2Dist(NPC n, Player p) => Microsoft.Xna.Framework.Vector2.Distance(n.Center, p.Center) / 16f;

		private static uint _hintAt;
		public override bool CanUseItem(Player player)
		{
			if (CanTurnInHere(player)) return true;
			if (player.whoAmI == Main.myPlayer && Main.GameUpdateCount - _hintAt > 90) { _hintAt = Main.GameUpdateCount; Main.NewText("Take it to a Barracks or a town NPC to turn it in.", 230, 190, 90); }
			return false;
		}

		public override bool? UseItem(Player player)
		{
			if (player.whoAmI != Main.myPlayer) return true;
			var r = Roster; if (r == null) return true;
			BandBounty.Pay(player, r, SharePct / 100f);
			return true;
		}
	}

	/// <summary>The payout of a bounty notice: the type's items (min..max each, scaled by the share beaten) and coins, handed straight to the player.</summary>
	public static class BandBounty
	{
		public static void Pay(Player p, BandRoster type, float share)
		{
			var src = p.GetSource_Misc("Warbands:Bounty");
			foreach (var (item, min, max) in type.Bounty)
			{
				int t = BandData.ItemType(item); if (t <= 0) continue;
				int stack = Math.Max(1, (int)Math.Round(Main.rand.Next(min, max + 1) * share));
				p.QuickSpawnItem(src, t, stack);
			}
			int copper = (int)(type.BountyCopper * share);
			int plat = copper / 1000000; copper -= plat * 1000000; int gold = copper / 10000; copper -= gold * 10000; int silver = copper / 100; copper -= silver * 100;
			if (plat > 0) p.QuickSpawnItem(src, ItemID.PlatinumCoin, plat);
			if (gold > 0) p.QuickSpawnItem(src, ItemID.GoldCoin, gold);
			if (silver > 0) p.QuickSpawnItem(src, ItemID.SilverCoin, silver);
			if (copper > 0) p.QuickSpawnItem(src, ItemID.CopperCoin, copper);
			Main.NewText($"Bounty claimed: the {type.Name}.", 230, 190, 90);
		}
	}
}
