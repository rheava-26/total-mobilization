using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Compat.Thorium;
using Levy.Common.Util;
using Levy.Content.NPCs;

namespace Levy.Common.Team
{
	/// <summary>
	/// Projectile team effects, mirrored onto the casting commander's levies with the projectile's own numbers. Each of these projectiles loops <c>Main.player</c> (or the team) from its own AI, so a levy standing in the circle
	/// was invisible to it. After the projectile's AI runs, on the server / single player, every levy of the owner inside the same radius gets the same buff for the same time (<see cref="TeamItems.ProjAura"/>, Calamity's healing auras),
	/// the Absorber and the Blue Jelly also strip the levy's debuffs once (Calamity zeroes the time of every debuff), and Blazing Star hearts and Corpus Avertor's stealth strike do their ally part for a levy.
	/// A levy that fires such a weapon owns the same projectile (shots are owned by the commander), so the aura buffs the commander through Calamity's own loop and the other levies through this one: both directions.
	/// </summary>
	public sealed class TeamAuraProjectile : GlobalProjectile
	{
		public override bool InstancePerEntity => true;
		private HashSet<int> _done, _cleansed;

		/// <summary>The Calamity projectile classes this mirrors (matched by name so nothing depends on load order).</summary>
		private static readonly HashSet<string> Names = new()
		{
			"CalamityMod/AbsorberAura", "CalamityMod/PinkJellyAura", "CalamityMod/GreenJellyAura", "CalamityMod/BlueJellyAura", "CalamityMod/DeathValleyDusterProjectile", "CalamityMod/PrimordialEarthProjectile",
			"CalamityMod/PrimordialAncientProjectile", "CalamityMod/BlazingStarHeal", "CalamityMod/CorpusAvertorStealth",
			"ThoriumMod/AuraDefense", "ThoriumMod/DreamWeaversMaskEffectPro",
		};

		public override bool AppliesToEntity(Projectile entity, bool lateInstantiation) => entity.ModProjectile != null && Names.Contains(entity.ModProjectile.Mod.Name + "/" + entity.ModProjectile.Name);

		/// <summary>Thorium's Dream Weaver's Mask set bonus: when it fires, every other player gets Distorted Time (speed and attack speed) for 15 s. Its effect projectile marks the moment; the owner's levies are those other players.</summary>
		public override void OnSpawn(Projectile proj, Terraria.DataStructures.IEntitySource source)
		{
			if (LevyUtil.IsClient || proj.ModProjectile == null || proj.ModProjectile.Mod.Name != "ThoriumMod" || proj.ModProjectile.Name != "DreamWeaversMaskEffectPro" || proj.owner < 0 || proj.owner >= Main.maxPlayers) return;
			int buff = TeamItems.ModBuffType("ThoriumMod/DistortedTimeBoost");
			Player owner = Main.player[proj.owner];
			if (owner == null || buff <= 0) return;
			foreach (SoldierNPC s in TeamItems.Levies(owner)) { TeamItems.GrantLevy(s, buff, 900); TeamItems.DreamWeaverGrants++; }
		}

		public override void PostAI(Projectile proj)
		{
			if (LevyUtil.IsClient || proj.owner < 0 || proj.owner >= Main.maxPlayers) return;
			Player owner = Main.player[proj.owner];
			if (owner == null) return;
			if (TeamItems.ProjAuras.TryGetValue(proj.type, out TeamItems.ProjAura aura)) { Aura(proj, owner, aura); return; }
			if (proj.ModProjectile?.Name == "BlazingStarHeal") Star(proj, owner);
		}

		private void Aura(Projectile proj, Player owner, TeamItems.ProjAura aura)
		{
			if (aura.Active != null && !aura.Active(proj)) return;
			float r = aura.Radius(proj);
			if (r <= 0f) return;
			foreach (SoldierNPC s in TeamItems.Levies(owner))
			{
				if (Vector2.Distance(proj.Center, s.NPC.Center) >= r) continue;
				int id = s.NPC.whoAmI;
				if (aura.Once) { _done ??= new HashSet<int>(); if (!_done.Add(id)) continue; }
				if (aura.Buff > 0) TeamItems.GrantLevy(s, aura.Buff, aura.Ticks);
				if (aura.Cleanse) { _cleansed ??= new HashSet<int>(); if (_cleansed.Add(id)) Cleanse(s); }
			}
		}

		/// <summary>Calamity's Absorber / Blue Jelly cleanse: every debuff on the ally has its time set to nothing.</summary>
		public static int Cleanse(SoldierNPC s)
		{
			int n = 0;
			for (int i = 0; i < NPC.maxBuffs; i++)
			{
				int t = s.NPC.buffType[i];
				if (t > 0 && s.NPC.buffTime[i] > 2 && Main.debuff[t]) { s.NPC.DelBuff(i); i--; n++; }
			}
			return n;
		}

		/// <summary>Blazing Star hearts (Divine Providence): a heart that comes within 30 px of the owner's levy, after its first ten frames, heals him by (200 - timeLeft) / 10 clamped 1..10 and is spent, as for the nearest player.</summary>
		private static void Star(Projectile proj, Player owner)
		{
			if (proj.timeLeft > 190) return;
			foreach (SoldierNPC s in TeamItems.Levies(owner))
			{
				if (s.NPC.life >= s.NPC.lifeMax || Vector2.Distance(proj.Center, s.NPC.Center) >= 30f * proj.scale + s.NPC.width * 0.5f) continue;
				int heal = Utils.Clamp((200 - proj.timeLeft) / 10, 1, 10);
				SoldierHeal.Restore(s.NPC, heal);
				proj.Kill();
				return;
			}
		}

		public override void OnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone)
		{
			// Corpus Avertor's stealth strike: the first hit of the spinning dagger gives every teammate the Avertor bonus for 20 s (no reach).
			if (LevyUtil.IsClient || proj.ModProjectile?.Name != "CorpusAvertorStealth" || proj.numHits != 0 || proj.ai[1] != 0f || target.friendly || proj.owner < 0 || proj.owner >= Main.maxPlayers) return;
			Player owner = Main.player[proj.owner];
			int buff = TeamItems.ModBuffType("CalamityMod/AvertorBonus");
			if (owner == null || buff <= 0) return;
			foreach (SoldierNPC s in TeamItems.Levies(owner)) TeamItems.GrantLevy(s, buff, 1200);
			if (owner.active) TeamItems.GrantPlayer(owner, buff, 1200); // a levy's throw is owned by the commander: the stealth strike's team (the thrower's allies) includes him
		}
	}

	/// <summary>Items whose use buffs the allies around the user. Mycoroot's throw gives the user and every teammate within 800 px the Mushy buff for 15 s; the user's levies are teammates here.</summary>
	public sealed class TeamAuraItem : GlobalItem
	{
		private static int _mushy = -1;

		public override bool AppliesToEntity(Item entity, bool lateInstantiation) => entity.ModItem != null && entity.ModItem.Mod.Name == "CalamityMod" && entity.ModItem.Name == "Mycoroot";

		public override bool Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
		{
			try
			{
				if (_mushy < 0) _mushy = TeamItems.ModBuffType("CalamityMod/Mushy");
				if (_mushy > 0 && !LevyUtil.IsClient)
					foreach (SoldierNPC s in TeamItems.Levies(player))
						if (Vector2.Distance(player.Center, s.NPC.Center) <= 800f) TeamItems.GrantLevy(s, _mushy, 900);
			}
			catch (Exception e) { TeamItems.WarnOnce("Mycoroot team buff", e); }
			return true;
		}
	}
}
