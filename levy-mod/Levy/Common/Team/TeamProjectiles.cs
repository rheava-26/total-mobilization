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
			"CalamityMod/WhitewaterAura", "CalamityMod/RelicOfConvergenceCrystal",
			"ThoriumMod/AuraDefense", "ThoriumMod/DreamWeaversMaskEffectPro",
			"ThoriumMod/DragonZone", "ThoriumMod/Nullzone", "ThoriumMod/RaPro2", "ThoriumMod/SerpentsBubbleWandPro", "ThoriumMod/CurePro", "ThoriumMod/ZunpetPro3", "ThoriumMod/DreamCatcherPro",
			"ThoriumMod/ShroomBooster", "ThoriumMod/AirStrikeShieldPro",
			"ThoriumMod/FallingTwilightPro", "ThoriumMod/TrueFallingTwilightPro", "ThoriumMod/BloodHarvestPro", "ThoriumMod/TrueBloodHarvestPro",
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
			string name = proj.ModProjectile?.Name;
			if (name == "BlazingStarHeal") Star(proj, owner);
			else if (name == "WhitewaterAura") Whitewater(proj, owner);
			else if (name == "RelicOfConvergenceCrystal") RelicOfConvergence(proj, owner);
		}

		private static System.Reflection.FieldInfo _wwScale, _rocMouse, _rocHeal;
		private static Func<Player, bool> _profanedRelic;
		/// <summary>Whitewater's vortex (stealth strike): every player inside 200 x areaScale px starts a heal of 1 HP every 20 ticks, 300 ticks for the thrower and 600 for any ally, unless one is already running. The owner's levies are those allies.</summary>
		private static void Whitewater(Projectile proj, Player owner)
		{
			_wwScale ??= proj.ModProjectile.GetType().GetField("areaScale", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
			float scale = _wwScale != null ? (float)_wwScale.GetValue(proj.ModProjectile) : 1f;
			foreach (SoldierNPC s in TeamItems.Levies(owner))
				if (Vector2.Distance(proj.Center, s.NPC.Center) < 200f * scale && TeamItems.StartHot(s, 1, 600, 20, 1)) TeamItems.WhitewaterStarts++;
		}

		/// <summary>Relic of Convergence: when the crystal shatters (timeLeft 5) every teammate within 138 px of the cursor point is healed for 1.5 x the relic's heal (x1.25 with the Profaned Soul relic buff). The owner's levies are those teammates.</summary>
		private static void RelicOfConvergence(Projectile proj, Player owner)
		{
			if (proj.timeLeft != 5) return;
			_rocMouse ??= proj.ModProjectile.GetType().GetField("mousePos");
			if (_rocHeal == null && ModLoader.TryGetMod("CalamityMod", out Mod cal) && cal.Code != null) _rocHeal = cal.Code.GetType("CalamityMod.Items.Tools.RelicOfConvergence")?.GetField("HealValue");
			_profanedRelic ??= TeamItems.ModFlag("CalamityMod", "CalamityMod.CalPlayer.CalamityPlayer", "profanedSoulRelicBuff");
			if (_rocMouse == null || _rocHeal == null) return;
			Vector2 at = (Vector2)_rocMouse.GetValue(proj.ModProjectile);
			int basis = (int)_rocHeal.GetValue(null);
			int heal = (int)(basis * 1.5f * (_profanedRelic != null && _profanedRelic(owner) ? 1.25f : 1f));
			foreach (SoldierNPC s in TeamItems.Levies(owner))
				if (Vector2.DistanceSquared(s.NPC.Center, at) < 138f * 138f && SoldierHeal.Restore(s.NPC, heal) > 0) TeamItems.RelicHeals++;
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

		/// <summary>An on-hit ally buff (Thorium's scythes): every ally within the radius of the projectile gets the buff, except the shooter. A scythe the commander swings leaves him out (Thorium skips the owner); one a levy swings is owned by the commander, who is then an ally too.</summary>
		private static void HitAura(Projectile proj, NPC target, TeamItems.HitAura ha)
		{
			if (LevyUtil.IsClient || target == null || target.friendly || proj.owner < 0 || proj.owner >= Main.maxPlayers) return;
			Player owner = Main.player[proj.owner];
			if (owner == null || !owner.active) return;
			var tag = proj.GetGlobalProjectile<global::Levy.Common.Globals.SoldierProjectileTag>();
			int shooter = tag != null && tag.IsSoldierShot ? tag.SoldierIndex : -1;
			foreach (SoldierNPC s in TeamItems.Levies(owner))
			{
				if (s.NPC.whoAmI == shooter || Vector2.DistanceSquared(proj.Center, s.NPC.Center) >= ha.Radius * ha.Radius) continue;
				if (TeamItems.GrantLevy(s, ha.Buff, ha.Ticks)) TeamItems.HitAuraGrants++;
			}
			if (shooter >= 0 && Vector2.DistanceSquared(proj.Center, owner.Center) < ha.Radius * ha.Radius && TeamItems.GrantPlayer(owner, ha.Buff, ha.Ticks)) TeamItems.HitAuraGrants++;
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
			if (TeamItems.HitAuras.TryGetValue(proj.type, out TeamItems.HitAura ha)) { HitAura(proj, target, ha); return; }
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
