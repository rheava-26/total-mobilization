using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;
using Terraria.WorldBuilding;
using Levy.Common.Globals;
using Levy.Common.Targeting;
using Levy.Content.NPCs;

namespace Levy.Common.Util
{
	/// <summary>
	/// MONSTER BANNERS FOR THE ARMY. Vanilla gives a player +damage dealt to, and -damage taken from, an enemy type while that type's banner hangs inside the
	/// player's buff-scan box (SceneMetrics.NPCBannerBuff, rebuilt from the tiles around the player; Player.ApplyBannerOffenseBuff / ApplyBannerDefenseBuff read it).
	/// Vanilla only reads it for the local player's own non-npcProj hits, and a dedicated server never builds it, so a levy (whose shots are npcProj) gets none of it.
	///
	/// WHOSE BANNERS: the COMMANDER's. The buff belongs to the player standing near the banner; a levy is the commander's army, so it fights under the commander's
	/// standard wherever it is, as the player would. That is also one scan per commander, not per soldier, run lazily (only when a hit asks) and at most twice a second.
	/// The scan is the same box vanilla uses (Main.buffScanAreaWidth x Height around the player centre) and the same rules (vanilla tile 91 frame maths, any ModBannerTile through
	/// NPCLoader.BannerItemToNPC, the banner item's BannerStrength.Enabled), so modded banners and modded enemies need no per-mod code. It does not read Main.SceneMetrics,
	/// which exists only on the local client.
	///
	/// The multipliers are vanilla's own (ItemID.Sets.BannerStrength: normal / expert damage dealt and received), keyed by the same Item.NPCtoBanner(npc.BannerID()).
	/// </summary>
	public static class BannerBuffs
	{
		private const int RescanTicks = 30;
		private static readonly bool[][] Buff = new bool[Main.maxPlayers][];
		private static readonly uint[] Stamp = new uint[Main.maxPlayers];

		/// <summary>Banner id of an enemy NPC (vanilla's own mapping), or 0 when it has none.</summary>
		public static int BannerOf(NPC npc) => npc == null ? 0 : Item.NPCtoBanner(npc.BannerID());

		/// <summary>True when the commander currently stands in range of a (strength-enabled) banner for <paramref name="bannerId"/>.</summary>
		public static bool Has(int commander, int bannerId)
		{
			if (bannerId <= 0 || commander < 0 || commander >= Main.maxPlayers) return false;
			Player p = Main.player[commander];
			if (!p.active) return false;
			bool[] set = Buff[commander];
			if (set == null || set.Length < NPCLoader.NPCCount || Stamp[commander] == 0 || Main.GameUpdateCount - Stamp[commander] >= RescanTicks)
			{
				set = Scan(p, set);
				Buff[commander] = set;
				Stamp[commander] = Math.Max(1u, Main.GameUpdateCount);
			}
			return bannerId < set.Length && set[bannerId];
		}

		/// <summary>Damage multiplier for the army's hit on <paramref name="target"/> (1 = no banner).</summary>
		public static float Offense(int commander, NPC target) => Offense(commander, BannerOf(target));

		public static float Offense(int commander, int bannerId)
		{
			if (!Has(commander, bannerId)) return 1f;
			ItemID.BannerEffect e = Effect(bannerId);
			return Main.expertMode ? e.ExpertDamageDealt : e.NormalDamageDealt;
		}

		/// <summary>Damage multiplier for a hit on the army by an enemy of <paramref name="bannerId"/> (1 = no banner).</summary>
		public static float Defense(int commander, int bannerId)
		{
			if (!Has(commander, bannerId)) return 1f;
			ItemID.BannerEffect e = Effect(bannerId);
			return Main.expertMode ? e.ExpertDamageReceived : e.NormalDamageReceived;
		}

		public static float Defense(int commander, NPC attacker) => Defense(commander, BannerOf(attacker));

		/// <summary>The enemy projectile's banner: vanilla's own tag when it carries one, else the NPC it came from (the tag records that for modded and indirect shots too).</summary>
		public static int BannerOf(Projectile projectile)
		{
			if (projectile.bannerIdToRespondTo > 0) return projectile.bannerIdToRespondTo;
			if (!projectile.TryGetGlobalProjectile(out SoldierProjectileTag tag)) return 0;
			int idx = tag.SourceNpcIndex;
			if (idx >= 0 && idx < Main.maxNPCs && Main.npc[idx].active && (tag.SourceNpcType <= 0 || Main.npc[idx].type == tag.SourceNpcType)) return BannerOf(Main.npc[idx]);
			return tag.SourceNpcType > 0 ? Item.NPCtoBanner(tag.SourceNpcType) : 0;
		}

		/// <summary>The commander whose standard an army projectile fights under, or -1 for a shot that is not the army's.</summary>
		public static int CommanderOf(Projectile projectile)
		{
			if (projectile == null || projectile.hostile || !projectile.TryGetGlobalProjectile(out SoldierProjectileTag tag)) return -1;
			if (tag.CommanderIndex >= 0) return tag.CommanderIndex;
			SoldierNPC body = EnemyTargeting.LiveBody(tag.SoldierIndex);
			if (body != null) return body.CommanderIndex;
			int src = tag.SourceNpcIndex; // an officer avatar's bolt carries only the source-NPC tag
			if (src >= 0 && src < Main.maxNPCs && Main.npc[src].active)
			{
				if (Main.npc[src].ModNPC is SoldierNPC s) return s.CommanderIndex;
				if (Main.npc[src].ModNPC is SummonedUnitNPC u) return u.CommanderIndex;
			}
			return -1;
		}

		private static ItemID.BannerEffect Effect(int bannerId)
		{
			int item = Item.BannerToItem(bannerId);
			return ItemID.Sets.BannerStrength.IndexInRange(item) ? ItemID.Sets.BannerStrength[item] : new ItemID.BannerEffect(1f);
		}

		/// <summary>SceneMetrics.ScanAndExportToMain's banner rule over the same box, without its side effects.</summary>
		private static bool[] Scan(Player p, bool[] set)
		{
			int n = NPCLoader.NPCCount;
			if (set == null || set.Length < n) set = new bool[n]; else Array.Clear(set, 0, set.Length);
			Point c = p.Center.ToTileCoordinates();
			var r = new Rectangle(c.X - Main.buffScanAreaWidth / 2, c.Y - Main.buffScanAreaHeight / 2, Main.buffScanAreaWidth, Main.buffScanAreaHeight);
			r = WorldUtils.ClampToWorld(r);
			SceneMetrics scratch = null, saved = null; bool swapped = false;
			try {
			for (int i = r.Left; i < r.Right; i++)
			{
				for (int j = r.Top; j < r.Bottom; j++)
				{
					Tile t = Main.tile[i, j];
					if (!t.HasTile) continue;
					int banner = -1;
					if (t.TileType == TileID.Banners)
					{
						if (t.TileFrameX < 396 && t.TileFrameY < 54) continue;
						banner = t.TileFrameX / 18 - 21;
						for (int fy = t.TileFrameY; fy >= 54; fy -= 54) banner += 90 + 21;
						int item = Item.BannerToItem(banner);
						if (!ItemID.Sets.BannerStrength.IndexInRange(item) || !ItemID.Sets.BannerStrength[item].Enabled) banner = -1;
					}
					else if (TileLoader.GetTile(t.TileType) is ModTile mt && OverridesNearby(mt))
					{
						// A modded banner announces itself the way vanilla's scan lets it: through its own NearbyEffects, which writes Main.SceneMetrics.NPCBannerBuff.
						// ModBannerTile does it by the book; some mods (Thorium) write the same array from their own override. The write goes to a scratch SceneMetrics, so the local player's is never touched.
						scratch ??= new SceneMetrics();
						if (!swapped) { saved = Main.SceneMetrics; Main.SceneMetrics = scratch; swapped = true; }
						try { mt.NearbyEffects(i, j, false); } catch (Exception) { }
					}
					if (banner > 0 && banner < set.Length) set[banner] = true;
				}
			}
			}
			finally { if (swapped) Main.SceneMetrics = saved; }
			if (scratch != null) for (int k = 0; k < set.Length && k < scratch.NPCBannerBuff.Length; k++) if (scratch.NPCBannerBuff[k]) set[k] = true;
			return set;
		}

		private static readonly System.Collections.Generic.Dictionary<int, bool> NearbyOverride = new();
		/// <summary>True when the modded tile's class overrides NearbyEffects (cached by tile type), the only way it can announce a banner.</summary>
		private static bool OverridesNearby(ModTile mt)
		{
			if (NearbyOverride.TryGetValue(mt.Type, out bool o)) return o;
			var m = mt.GetType().GetMethod("NearbyEffects", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public, null, new[] { typeof(int), typeof(int), typeof(bool) }, null);
			return NearbyOverride[mt.Type] = m != null && m.DeclaringType != typeof(ModTile);
		}
	}

	/// <summary>Offense: every hit the army's projectiles land on an enemy is scaled by the commander's banner for that enemy (see <see cref="BannerBuffs"/>); melee swings and summoned blows apply the same number where they strike. Defense: vanilla's own enemy-vs-friendly-NPC contact (NPC.BeHurtByOtherNPC) is scaled by the banner of the attacker; SoldierNPC's own contact and projectile paths apply it themselves.</summary>
	public class BannerGlobalNPC : GlobalNPC
	{
		public override void ModifyHitByProjectile(NPC npc, Projectile projectile, ref NPC.HitModifiers modifiers)
		{
			if (npc.friendly || npc.ModNPC is SoldierNPC) return;
			int cmd = BannerBuffs.CommanderOf(projectile);
			if (cmd < 0) return;
			if (!projectile.npcProj && !projectile.trap && Main.netMode != NetmodeID.Server) return; // vanilla's own Projectile.Damage applies the player's banner to this one
			float m = BannerBuffs.Offense(cmd, npc);
			if (m != 1f) modifiers.TargetDamageMultiplier *= m;
		}

		/// <summary>Vanilla's enemy-vs-friendly-NPC contact (NPC.BeHurtByOtherNPC) reaches here for a soldier or summoned unit: the commander's banner for the attacker cuts it.</summary>
		public override void ModifyHitNPC(NPC npc, NPC target, ref NPC.HitModifiers modifiers)
		{
			if (npc.friendly) return;
			int cmd = target.ModNPC is SoldierNPC s ? s.CommanderIndex : target.ModNPC is SummonedUnitNPC u ? u.CommanderIndex : -1;
			if (cmd < 0) return;
			float m = BannerBuffs.Defense(cmd, npc);
			if (m != 1f) modifiers.FinalDamage *= m;
		}
	}
}
