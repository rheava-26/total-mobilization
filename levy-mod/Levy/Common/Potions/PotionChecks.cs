using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Content.Items.Potions;

namespace Levy.Common.Potions
{
	/// <summary>Boot self-checks of the potions (pure math and item flags; never throws). Not covered: boss aggro, the chooser UI, the Seeker draw, levy fall-back in a live fight.</summary>
	public class PotionChecks : ModSystem
	{
		private static void SpectatorTargetingChecks(Action<bool, string> Check)
		{
			Player saved = Main.player[0];
			SpectatorPlayer.InvalidateCache();
			var boss = new NPC(); boss.SetDefaults(NPCID.EyeofCthulhu); boss.active = true;
			bool before = global::Levy.Common.Targeting.EnemyTargeting.BossAggroUnlocked(boss);
			try
			{
				var p = new Player { active = true };
				p.AddBuff(ModContent.BuffType<global::Levy.Content.Buffs.SpectatorBuff>(), 600);
				Main.player[0] = p;
				SpectatorPlayer.InvalidateCache();
				Check(SpectatorPlayer.IsSpectator(p) && SpectatorPlayer.AnyActive, "spectator: detected as a spectator");
				Check(!global::Levy.Common.Targeting.EnemyTargeting.BossAggroUnlocked(boss), "spectator: no boss aggro unlock for a boss not downed in this world (the first kill is solo)");
				Check(global::Levy.Common.Targeting.EnemyTargeting.IsUndownedBoss(boss) && !global::Levy.Common.Targeting.EnemyTargeting.HidesSpectatorFrom(boss), "spectator: an undowned boss is flagged and the spectator is not hidden from it");
				var pillar = new NPC(); pillar.SetDefaults(NPCID.LunarTowerSolar); pillar.active = true;
				Check(!global::Levy.Common.Targeting.EnemyTargeting.IsUndownedBoss(pillar), "spectator: Lunar Pillars are not bosses for the first-kill rule");
				Check(!global::Levy.Common.Targeting.EnemyTargeting.IsUndownedBoss(new NPC()), "spectator: an empty NPC is not an undowned boss");
				global::Levy.Common.Boss.BossDownedSystem.SetDownedForTest(NPCID.EyeofCthulhu, true);
				try
				{
					Check(global::Levy.Common.Targeting.EnemyTargeting.BossAggroUnlocked(boss), "spectator: a downed boss unlocks levy targeting while a commander spectates, whatever the config");
					Check(!global::Levy.Common.Targeting.EnemyTargeting.IsUndownedBoss(boss) && global::Levy.Common.Targeting.EnemyTargeting.HidesSpectatorFrom(boss), "spectator: a downed boss is not flagged and a spectator is hidden from it");
				}
				finally { global::Levy.Common.Boss.BossDownedSystem.SetDownedForTest(NPCID.EyeofCthulhu, false); }
				var z = new NPC(); z.SetDefaults(NPCID.Zombie); z.Center = p.Center; z.target = 0;
				SpectatorPlayer.HideAll();
				Check(p.dead && p.ghost, "spectator: hidden from the candidate search (dead+ghost, which vanilla's search skips)");
				SpectatorPlayer.HideAll(); SpectatorPlayer.ReleaseAll();
				Check(p.dead, "spectator: hide is re-entrant (inner release keeps it hidden)");
				SpectatorPlayer.ReleaseAll();
				Check(!p.dead && !p.ghost, "spectator: released after the search");
				Check(p.TryGetModPlayer(out SpectatorPlayer spp) && !spp.CanHitNPC(z) && spp.CanHitNPCWithItem(new Item(ItemID.CopperShortsword), z) == false, "spectator: deals no damage by dash, contact or item");
				z.target = 0;
				Check(global::Levy.Common.Targeting.EnemyTargeting.TargetsSpectator(z), "spectator: an enemy whose target is a spectator is flagged for an immediate re-search");
			}
			finally { Main.player[0] = saved; SpectatorPlayer.InvalidateCache(); }
			Check(global::Levy.Common.Targeting.EnemyTargeting.BossAggroUnlocked(boss) == before, "spectator: boss aggro override ends with the buff");
		}

		/// <summary>
		/// Vanilla-potion parity. Reads the vanilla Ironskin potion from ContentSamples and requires our two potions to carry the same use animation, sound, stack, consume flag and every ItemID.Sets flag
		/// that both vanilla Ironskin and Spelunker share. Sprite size must equal the item's width and height.
		/// </summary>
		private static void ParityChecks(Action<bool, string> Check, Action<string> Info)
		{
			Item iron = ContentSamples.ItemsByType[ItemID.IronskinPotion];
			bool dump = Environment.GetEnvironmentVariable("LEVY_POTION_DUMP") == "1";
			if (dump)
				foreach (int vt in new[] { ItemID.IronskinPotion, ItemID.SpelunkerPotion, ItemID.RegenerationPotion, ItemID.SwiftnessPotion, ItemID.InvisibilityPotion, ItemID.NightOwlPotion, ItemID.HunterPotion, ItemID.GravitationPotion })
				{
					Item v = ContentSamples.ItemsByType[vt];
					Info($"vanilla {ItemID.Search.GetName(vt)}: useStyle={v.useStyle} useAnim={v.useAnimation} useTime={v.useTime} useTurn={v.useTurn} sound={v.UseSound?.SoundPath} consumable={v.consumable} maxStack={v.maxStack} {v.width}x{v.height} rare={v.rare} value={v.value} buffType={v.buffType} buffTime={v.buffTime} researchCount={v.ResearchUnlockCount}");
				}
			foreach (int type in new[] { ModContent.ItemType<SpectatorPotion>(), ModContent.ItemType<SeekerPotion>() })
			{
				Item it = ContentSamples.ItemsByType[type];
				string n = it.ModItem.Name;
				if (dump) Info($"ours {n}: useStyle={it.useStyle} useAnim={it.useAnimation} useTime={it.useTime} useTurn={it.useTurn} sound={it.UseSound?.SoundPath} consumable={it.consumable} maxStack={it.maxStack} {it.width}x{it.height} rare={it.rare} value={it.value} buffType={it.buffType} buffTime={it.buffTime} researchCount={it.ResearchUnlockCount}");
				Check(it.useStyle == iron.useStyle && it.useAnimation == iron.useAnimation && it.useTime == iron.useTime && it.useTurn == iron.useTurn, n + ": use style, animation, time and turn as vanilla potions");
				Check(it.UseSound.HasValue && iron.UseSound.HasValue && it.UseSound.Value == iron.UseSound.Value, n + ": drink sound as vanilla potions");
				Check(it.consumable && it.maxStack == iron.maxStack && it.buffType > 0 && it.buffTime > 0, n + ": consumable, stack " + iron.maxStack + ", buff and duration set");
				Check(it.ResearchUnlockCount == iron.ResearchUnlockCount, n + ": research count as vanilla potions");
				Check(it.rare == iron.rare && it.value == iron.value, n + ": rarity and value as the vanilla basic potions");
				Check(ItemID.Sets.DrinkParticleColors[type] != null && ItemID.Sets.DrinkParticleColors[type].Length > 0, n + ": drink particle colors set like vanilla potions");
				var tex = Terraria.GameContent.TextureAssets.Item[type];
				Check(tex == null || !tex.IsLoaded || (tex.Width() == it.width && tex.Height() == it.height), n + ": width and height match the sprite");
				Check(it.damage <= 0 && !it.accessory && !it.questItem, n + ": no weapon, accessory or quest flags");
				foreach (var f in typeof(ItemID.Sets).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
				{
					if (f.Name == "IsAMaterial" || f.GetValue(null) is not bool[] arr || arr.Length <= type) continue; // IsAMaterial is derived from recipes
					if (arr[ItemID.IronskinPotion] && arr[ItemID.SpelunkerPotion]) Check(arr[type], n + ": ItemID.Sets." + f.Name + " (set on vanilla potions)");
					else if (dump && (arr[ItemID.IronskinPotion] || arr[ItemID.SpelunkerPotion])) Info("set differs between Ironskin/Spelunker: " + f.Name);
				}
				if (dump)
					foreach (var f in typeof(ItemID.Sets).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
						if (f.GetValue(null) is Array a && a.Length > type && a.Length > ItemID.IronskinPotion && f.FieldType != typeof(bool[]) && !Equals(a.GetValue(ItemID.IronskinPotion), a.GetValue(type)) && a.GetValue(ItemID.IronskinPotion) != null)
							Info($"set {f.Name}: Ironskin={a.GetValue(ItemID.IronskinPotion)} ours={a.GetValue(type)}");
			}
		}

		public override void PostSetupRecipes() => global::Levy.Common.Util.LoadSafe.Run(PostSetupRecipesCore);

		private void PostSetupRecipesCore()
		{
			var failures = new List<string>(); int ok = 0;
			void Check(bool cond, string what) { if (cond) ok++; else failures.Add(what); }
			try
			{
				float edge = PotionConstants.TetherTiles * 16f;
				Vector2 v = new Vector2(5f, 0f), c = new Vector2(edge + 200f, 0f);
				SpectatorPlayer.Tether(Vector2.Zero, ref c, ref v, out Vector2 pull);
				Check(v.X <= 0.001f && pull.X < 0f, "tether: outward speed removed past the edge and a pull back toward the anchor");
				v = new Vector2(5f, 0f); c = new Vector2(100f, 0f);
				SpectatorPlayer.Tether(Vector2.Zero, ref c, ref v, out pull);
				Check(v.X == 5f && pull == Vector2.Zero, "tether: free movement near the anchor");
				Vector2 anc = Vector2.Zero;
				Check(!SpectatorPlayer.TeleportResponse(Vector2.Zero, new Vector2(5 * 16f, 0f), ref anc, out bool endA) && !endA && anc == Vector2.Zero, "spectator teleport: a normal step is not a teleport");
				Vector2 hop = new Vector2(30 * 16f, 0f);
				Check(SpectatorPlayer.TeleportResponse(Vector2.Zero, hop, ref anc, out bool endB) && !endB && anc == hop, "spectator teleport: a short hop moves the tether anchor to the landing spot");
				anc = Vector2.Zero;
				Check(SpectatorPlayer.TeleportResponse(Vector2.Zero, new Vector2(400 * 16f, 0f), ref anc, out bool endC) && endC && anc == Vector2.Zero, "spectator teleport: a long teleport ends the buff");
				Check(SpectatorPlayer.Blocked(new Item(ItemID.CopperShortsword)), "spectator: weapons blocked");
				Check(SpectatorPlayer.Blocked(new Item(ItemID.CopperPickaxe)), "spectator: pickaxes blocked");
				Check(SpectatorPlayer.Blocked(new Item(ItemID.StoneBlock)), "spectator: blocks blocked");
				Check(!SpectatorPlayer.Blocked(new Item(ItemID.LesserHealingPotion)), "spectator: potions allowed");
				Check(PotionConstants.AutonomousFallbackFrac < PotionConstants.AutonomousReengageFrac, "autonomy: re-engage threshold above fall-back threshold");
				Check(Array.IndexOf(Flavor.GuideSystem.Keys, "Autonomy") >= 0, "autonomy: guide key registered");
				Check(ModContent.ItemType<SeekerPotion>() > 0 && ModContent.ItemType<SpectatorPotion>() > 0, "potion items registered");
				try { ParityChecks(Check, m => Mod.Logger.Info("potion parity: " + m)); } catch (Exception e) { failures.Add("potion parity checks threw " + e.GetType().Name + ": " + e.Message); }
				try { SpectatorTargetingChecks(Check); } catch (Exception e) { failures.Add("spectator targeting checks threw " + e.GetType().Name + ": " + e.Message); }
			}
			catch (Exception e) { failures.Add("potion checks threw " + e.GetType().Name + ": " + e.Message); }
			if (failures.Count == 0) Mod.Logger.Info($"Levy potion self-check: {ok} checks OK");
			else { Mod.Logger.Warn($"Levy potion self-check: {failures.Count} FAILURE(S) ({ok} checks OK) !!!"); foreach (var f in failures) Mod.Logger.Warn("  potion: " + f); }
		}
	}
}
