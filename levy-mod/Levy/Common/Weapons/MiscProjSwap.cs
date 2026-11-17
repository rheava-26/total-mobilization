using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Globals;
using Levy.Common.Targeting;
using Levy.Common.Util;
using Levy.Content.NPCs;

namespace Levy.Common.Weapons
{
	/// <summary>
	/// Owner-anchored vanilla projectiles that are NOT part of the held-weapon chain (see <see cref="HeldWeapons"/>), anchored to the soldier instead of the commander.
	///
	/// ZENITH SWORDS (ProjectileID.FinalFractal, aiStyle Zenith 182). Soldier shots are owned by the commander (<see cref="WeaponUse.OwnerFor"/>), and AI_182_FinalFractal centres the
	/// swirl on <c>Main.player[owner].MountedCenter</c>, so the swords would orbit the commander. For the length of that one AI call the soldier's dummy is put into Main.player[255]
	/// through <see cref="EnemyTargeting.Swap"/> (the swap enemies and held weapons use) and the projectile's owner is pointed at 255; both are undone in the same finally. Nothing else in
	/// the Zenith AI reads the machine's local player (no Main.myPlayer / mouse input), so the swap is all it needs. Hits are damaged by Projectile.Damage with the real owner, untouched.
	/// SLOT 255 DISCIPLINE: Swap in try, Unswap in finally, EnsureSlotRestored in the same finally, plus PreUpdateProjectiles/PostUpdateProjectiles.
	///
	/// LUNAR SENTRIES (Lunar Portal Staff's Moonlord Turret, Rainbow Crystal Staff's Rainbow Crystal; aiStyle 123). The tower picks its facing once, as <c>Main.player[owner].direction</c>
	/// when its own direction is still 0. Seeding the direction from the soldier before the first AI tick replaces that read, so no swap is needed.
	/// </summary>
	public static class MiscProjSwap
	{
		private const int Slot = SoldierNPC.DummySlot;

		/// <summary>The sword-orbit projectile whose AI is run against the soldier.</summary>
		private static bool IsAnchoredType(int type) => type == ProjectileID.FinalFractal;

		/// <summary>The Lunar sentry shots that face the way their owner faces (aiStyle 123, types 641 and 643).</summary>
		private static bool IsLunarSentry(int type) => type == ProjectileID.MoonlordTurret || type == ProjectileID.RainbowCrystal;

		/// <summary>The live soldier that fired this projectile, or null (not a soldier shot, soldier dead / gone, or its NPC slot reused).</summary>
		internal static SoldierNPC BodyOf(Projectile p)
		{
			if (!p.TryGetGlobalProjectile(out SoldierProjectileTag tag)) return null;
			int idx = tag.SoldierIndex;
			if (idx < 0 || idx >= Main.maxNPCs) return null;
			NPC n = Main.npc[idx];
			if (!n.active || n.ModNPC is not SoldierNPC s || s.Record == null || s.Dummy == null || !s.IsAlive) return null;
			return s;
		}

		internal static bool WantsAnchor(Projectile p) =>
			IsAnchoredType(p.type) && p.TryGetGlobalProjectile(out SoldierProjectileTag tag) && tag.SoldierIndex >= 0;

		internal static void RunAi(Action<Projectile> orig, Projectile p)
		{
			SoldierNPC body = BodyOf(p);
			if (body == null)
			{
				// the soldier is gone: swords left orbiting the commander would be wrong, so they end (the swirl lives under two seconds anyway)
				if (LevyUtil.IsServerOrSingle) p.Kill(); else orig(p);
				return;
			}
			int savedOwner = p.owner;
			bool swapped = false;
			try
			{
				EnemyTargeting.Swap(body); // dummy -> Main.player[255]
				swapped = true; global::Levy.Common.Util.HookProbe.MiscActed++;
				if (p.owner != Slot) p.owner = Slot; // on a dedicated server a soldier shot is already owned by 255
				orig(p);
			}
			finally
			{
				if (p.owner != savedOwner) p.owner = savedOwner;
				if (swapped) EnemyTargeting.Unswap();
				EnemyTargeting.EnsureSlotRestored(); // never leave a dummy in Main.player[255]
			}
		}

		/// <summary>Faces a freshly spawned Lunar sentry the way its soldier faces (called before the sentry's first AI tick; its own direction is 0 until then).</summary>
		internal static void SeedSentryDirection(Projectile p)
		{
			if (p.direction != 0 || !IsLunarSentry(p.type)) return;
			SoldierNPC body = BodyOf(p);
			if (body == null) return;
			p.direction = body.NPC.direction == 0 ? 1 : body.NPC.direction;
		}
	}

	/// <summary>Seeds the Lunar sentries' facing from the soldier before their first AI tick.</summary>
	public class MiscProjectile : GlobalProjectile
	{
		public override bool PreAI(Projectile projectile)
		{
			if (projectile.direction == 0) MiscProjSwap.SeedSentryDirection(projectile);
			if (global::Levy.Common.Util.HookProbe.SoundWatch && projectile.TryGetGlobalProjectile(out SoldierProjectileTag t) && t.SoldierIndex >= 0) { if (global::Levy.Common.Audio.ArmySoundThrottle.CurrentKind != 0) global::Levy.Common.Util.HookProbe.SoundInAi++; else global::Levy.Common.Util.HookProbe.SoundOutAi++; }
			return true;
		}
	}

	/// <summary>Installs the Projectile.AI wrapper for the Zenith swords and keeps slot 255 clean around the projectile phase.</summary>
	public class MiscProjSwapSystem : ModSystem
	{
		/// <summary>Above zero while the wrapper is inside orig on this thread: the three hook points (Projectile.AI, VanillaAI, ProjectileLoader.ProjectileAI) nest, and only the first one wraps (see <see cref="ProjAiHooks"/>).</summary>
		[ThreadStatic] private static int _depth;

		public override void Load()
		{
			On_Projectile.AI += AiDetour;
			On_Projectile.VanillaAI += VanillaAiDetour;
			ProjAiHooks.AddLoader(LoaderAiDetour, "the Zenith anchor");
		}

		public override void Unload()
		{
			On_Projectile.AI -= AiDetour;
			On_Projectile.VanillaAI -= VanillaAiDetour;
			EnemyTargeting.EnsureSlotRestored();
		}

		public override void PreUpdateProjectiles() => EnemyTargeting.EnsureSlotRestored();
		public override void PostUpdateProjectiles() => EnemyTargeting.EnsureSlotRestored();

		private static void AiDetour(On_Projectile.orig_AI orig, Projectile self) { global::Levy.Common.Util.HookProbe.MiscAi++; if (!Wants(self)) orig(self); else Go(pp => orig(pp), self); }
		private static void VanillaAiDetour(On_Projectile.orig_VanillaAI orig, Projectile self) { global::Levy.Common.Util.HookProbe.MiscVanilla++; if (!Wants(self)) orig(self); else Go(pp => orig(pp), self); }
		private static void LoaderAiDetour(Action<Projectile> orig, Projectile self) { global::Levy.Common.Util.HookProbe.MiscLoader++; if (!Wants(self)) orig(self); else Go(orig, self); }

		/// <summary>The cheap test every projectile passes: a Zenith sword of a soldier, not already inside the wrapper.</summary>
		private static bool Wants(Projectile self) => self.type == Terraria.ID.ProjectileID.FinalFractal && _depth == 0 && !ProjAiHooks.Off && self.active && MiscProjSwap.WantsAnchor(self);

		private static void Go(Action<Projectile> orig, Projectile self)
		{
			if (!Wants(self)) { orig(self); return; }
			_depth++;
			try { MiscProjSwap.RunAi(orig, self); }
			finally { _depth--; }
		}
	}
}
