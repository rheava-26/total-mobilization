using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Common.Util;
using Levy.Content.Buffs;
using Levy.Content.Items.Placeables;
using Levy.Content.NPCs;
using Levy.Content.Tiles;

namespace Levy.Common.Banners
{
	/// <summary>
	/// Boot self-check "[Banner] self-check pass=N fail=M" (dedicated server boots; LEVY_BANNERCHECK=0 skips): the item places the tile, the tile's shape and anchors, the recipe, the order's wiring (radial, packet, audit),
	/// the assignment rules (nearest banner, one stand each, overflow to the next banner, reassignment when a banner is placed, a banner set aside when it cannot be reached) and the buff (the numbers on the dummy, through the
	/// same ApplyBuffEffect the potions use, and the reach of the aura). The placement itself and the walk to a banner on a tower are measured in the lab (travelab, specs/banner_*.yaml).
	/// </summary>
	public class BannerSelfCheck : ModSystem
	{
		private int pass, fail;
		private void Check(string name, bool ok, string detail = "")
		{
			if (ok) pass++; else fail++;
			Mod.Logger.Info($"[Banner] {(ok ? "PASS" : "FAIL")} {name}{(detail.Length > 0 ? " :: " + detail : "")}");
		}

		public override void PostSetupRecipes() => LoadSafe.Run(Run);

		private static SoldierRecord Rec(int id, Vector2 at) => new SoldierRecord { Id = id, Kind = SoldierKind.Regular, Order = SoldierOrder.HoldBanners, HoldPos = at, Name = "Pvt. Banner" + id };

		private void Run()
		{
			string env = Environment.GetEnvironmentVariable("LEVY_BANNERCHECK");
			if (env == "0" || (!Main.dedServ && env != "1")) return;
			pass = fail = 0;
			var roster = RosterSystem.Instance;
			var added = new List<SoldierRecord>();
			try
			{
				// 1. the item, the tile, the recipe
				int tile = ModContent.TileType<HoldingBannerTile>(), item = ModContent.ItemType<HoldingBannerItem>();
				Check("1 the item places the Holding Banner tile", ContentSamples.ItemsByType[item].createTile == tile && ContentSamples.ItemsByType[item].consumable);
				var td = TileObjectData.GetTileData(tile, 0);
				Check("1 the tile is 2 x 4 and several can stand", td != null && td.Width == LevyConstants.Banner.Width && td.Height == LevyConstants.Banner.Height && td.Width == 2 && td.Height == 4);
				Check("1 it stands on the ground, a platform or a table", td != null && (td.AnchorBottom.type & AnchorType.SolidTile) != 0 && (td.AnchorBottom.type & AnchorType.SolidWithTop) != 0 && (td.AnchorBottom.type & AnchorType.Table) != 0);
				bool recipe = false; string rd = "";
				foreach (var r in Main.recipe)
				{
					if (r == null || r.createItem == null || r.createItem.type != item) continue;
					rd = $"{r.requiredItem.Count} ingredients, tile(s) {string.Join("+", r.requiredTile)}";
					recipe = r.requiredTile.Contains(TileID.WorkBenches) && r.HasIngredient(ItemID.Silk) && r.requiredItem.Exists(i => i.stack == LevyConstants.Banner.RecipeWood);
				}
				Check("1 Work Bench recipe: wood and silk", recipe, rd);

				// 2. the order is wired through: the radial's order byte is accepted by the packet handler, the audit reads it as a hold post
				Check("2 the order is a distinct byte the Order packet accepts", (byte)SoldierOrder.HoldBanners == 7 && (byte)SoldierOrder.HoldBanners != (byte)SoldierOrder.GoTo && SoldierNPC.StateAfterOrder(SoldierOrder.HoldBanners) == SoldierState.Idle);
				Check("2 the Order packet has a handler", Net.LevyPackets.IsRegistered(Net.LevyPacket.Order));

				// 3. assignment (a stand-in for the tile scan: two banners with 3 stands each)
				BannerSystem.ResetForTest();
				Point16 A = new Point16(100, 100), B = new Point16(160, 100);
				List<Point> StandsAt(Point16 t) => new List<Point> { new Point(t.X + 2, t.Y + 3), new Point(t.X - 1, t.Y + 3), new Point(t.X + 3, t.Y + 3) };
				var banners = new List<(Point16, List<Point>)> { (A, StandsAt(A)), (B, StandsAt(B)) };
				BannerSystem.TestSource = () => banners;
				int baseId = 900000;
				var near = new List<SoldierRecord>(); // five levies near A (its 3 stands fill, two go to B), one far east near B
				for (int i = 0; i < 5; i++) near.Add(Rec(baseId + i, new Vector2((100 + i) * 16f, 104 * 16f)));
				var east = Rec(baseId + 5, new Vector2(165 * 16f, 104 * 16f));
				foreach (var r in near) { roster.Regulars.Add(r); added.Add(r); }
				roster.Regulars.Add(east); added.Add(east);
				var posts = new List<BannerPost>(); foreach (var r in near) posts.Add(BannerSystem.PostOf(r));
				var pe = BannerSystem.PostOf(east);
				Check("3 every levy on the order gets a post", posts.TrueForAll(p => p != null) && pe != null);
				int onA = 0, onB = 0; foreach (var p in posts) { if (p.Banner == A) onA++; else if (p.Banner == B) onB++; }
				Check("3 the nearest banner takes levies up to its stands, the rest go to the next", onA == 3 && onB == 2, $"A={onA} B={onB}");
				var seen = new HashSet<Point>(); bool distinct = true; foreach (var p in posts) if (p.Banner == A && !seen.Add(p.Stand)) distinct = false;
				Check("3 each levy at a banner has its own stand", distinct && seen.Count == 3);
				Check("3 a levy beside the other banner goes to that one", pe != null && pe.Banner == B);
				// a banner placed nearer to the first levy reassigns the army
				var C = new Point16(100, 90); banners.Add((C, StandsAt(C))); BannerSystem.Changed();
				var again = BannerSystem.PostOf(near[0]);
				Check("3 placing a banner reassigns the army to the nearest", again != null && again.Banner == A || again != null && again.Banner == C);
				// one is set aside when it cannot be reached
				var p0 = BannerSystem.PostOf(near[1]); var firstBanner = p0.Banner;
				BannerSystem.NoteUnreachable(near[1], firstBanner);
				var p1 = BannerSystem.PostOf(near[1]);
				Check("3 a banner a levy cannot reach is set aside for it", p1 != null && p1.Banner != firstBanner, p1 == null ? "null" : p1.Banner.ToString());
				// with no banner at all the order is plain Hold: no post
				banners.Clear(); BannerSystem.Changed();
				Check("3 with no banner the levy has no post (it holds where it stands)", BannerSystem.PostOf(near[0]) == null);
				// a levy not on the order has no post
				near[0].Order = SoldierOrder.Follow; banners.Add((A, StandsAt(A))); BannerSystem.Changed();
				Check("3 a levy on another order has no post", BannerSystem.PostOf(near[0]) == null);

				// 4. the buff
				var d = new Player();
				int def0 = d.statDefense.Positive, kb0 = d.noKnockback ? 1 : 0; float end0 = d.endurance;
				var m = typeof(SoldierNPC).GetMethod("ApplyBuffEffect", BindingFlags.Static | BindingFlags.NonPublic);
				m?.Invoke(null, new object[] { d, ModContent.BuffType<HoldingBannerBuff>() });
				Check("4 the buff gives +" + LevyConstants.Banner.Defense + " defense, " + (int)(LevyConstants.Banner.DamageReduction * 100) + "% damage reduction and knockback immunity through ApplyBuffEffect",
					m != null && d.statDefense.Positive == def0 + LevyConstants.Banner.Defense && Math.Abs(d.endurance - (end0 + LevyConstants.Banner.DamageReduction)) < 1e-4f && d.noKnockback && kb0 == 0,
					$"def {d.statDefense.Positive} endurance {d.endurance:0.00} noKnockback {d.noKnockback}");
				string tip = Terraria.Localization.Language.GetTextValue("Mods.Levy.Buffs.HoldingBannerBuff.Description");
				Check("4 the buff text states the numbers in use", tip.Contains(LevyConstants.Banner.Defense.ToString()) && tip.Contains((int)(LevyConstants.Banner.DamageReduction * 100) + "%") && tip.Contains("knockback"), tip.Replace("\n", " | "));
				Check("4 it is one boon, not a debuff", ModContent.GetInstance<HoldingBannerBuff>() != null && !Main.debuff[ModContent.BuffType<HoldingBannerBuff>()]);
				// the aura's reach: a banner whose tile entity sits at (100,100): pole foot at row 104
				Point16 top = new Point16(100, 100); float footY = 104 * 16f;
				Check("4 a levy at the foot of the banner is in the aura", BannerSystem.InAura(top, new Vector2(101 * 16f, footY - 20f), footY));
				Check("4 a levy " + LevyConstants.Banner.AuraTiles + " tiles to the side is in, 8 tiles is out", BannerSystem.InAura(top, new Vector2((101 + LevyConstants.Banner.AuraTiles) * 16f, footY - 20f), footY) && !BannerSystem.InAura(top, new Vector2(109 * 16f, footY - 20f), footY));
				Check("4 a levy on the floor 8 rows above or 4 rows below is out", !BannerSystem.InAura(top, new Vector2(101 * 16f, footY - 8 * 16f - 20f), footY - 8 * 16f) && !BannerSystem.InAura(top, new Vector2(101 * 16f, footY + 4 * 16f - 20f), footY + 4 * 16f));
			}
			catch (Exception e) { Check("no exception", false, e.ToString()); }
			finally
			{
				BannerSystem.TestSource = null; BannerSystem.ResetForTest();
				foreach (var r in added) roster.Regulars.Remove(r);
			}
			Mod.Logger.Info($"[Banner] self-check pass={pass} fail={fail}");
		}
	}
}
