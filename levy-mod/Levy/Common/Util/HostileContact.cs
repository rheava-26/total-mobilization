using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Levy.Common.Util
{
	/// <summary>
	/// Does a hostile NPC's touch hurt a body standing at <c>victim</c>? Vanilla gives NPCs no contact damage against other NPCs, so the army's bodies
	/// (soldiers, summoned units, conjured creatures) apply it themselves; this is the one place that decides WHERE. It runs the same checks
	/// Player.Update_NPCCollision runs for a player, in the same order, so a levy is hit exactly where a player standing there would be:
	/// <list type="number">
	/// <item>the special immunity slot vanilla assigns before any hook (Moon Lord's parts 396 to 401 and the Empress 636 use slot 1; the Empress cannot hurt while ai[0] is 0 or 10);</item>
	/// <item><see cref="NPCLoader.CanHitPlayer"/> with a Player standing in for the body (ModNPC.CanHitPlayer and GlobalNPC.CanHitPlayer: Calamity's circle hitboxes on the Leviathan's mouth, body and tail, Aquatic Scourge's, Desert Scourge's and every boss that refuses a contact by state), which may also change the immunity slot;</item>
	/// <item>the stand-in's <c>npcTypeNoAggro</c> and Shield of Cthulhu dash exemptions;</item>
	/// <item><see cref="NPC.GetMeleeCollisionData"/>: <see cref="NPCLoader.ModifyCollisionData"/> and vanilla's special rectangles, then a rectangle intersection with the adjusted hitbox.</item>
	/// </list>
	/// The earlier check (NPCLoader.CanHitNPC plus the bare rectangle) is the NPC-versus-NPC hook, not the one a boss writes its hitbox rules in, so a body took the whole of a boss's rectangle,
	/// corners included. <c>LEVY_CONTACTPARITY=0</c> restores that check (A/B in the boss lab). <c>LEVY_CONTACTSLOTS=0</c> folds every immunity slot into the body's single window.
	/// <para/>NOT run: PlayerLoader.CanBeHitByNPC (a ModPlayer's own veto reads real-player state the stand-in does not carry; Calamity defines none).
	/// </summary>
	public static class HostileContact
	{
		public static readonly bool Parity = Environment.GetEnvironmentVariable("LEVY_CONTACTPARITY") != "0";
		public static readonly bool Slots = Environment.GetEnvironmentVariable("LEVY_CONTACTSLOTS") != "0";
		/// <summary>Player.hurtCooldowns.Length: the number of immunity slots besides the general one.</summary>
		public const int SlotCount = 5;

		private static Player _stand;
		private static bool _warned;
		/// <summary>Boss-lab telemetry (read by the harness): contacts the rectangle alone would have allowed that a player standing there would not take, and the reverse. Counted only while <see cref="Counting"/>.</summary>
		public static long Vetoed, Added;
		/// <summary>Harness telemetry on (SoldierNPC.HitDbg is set): count rectangle-only decisions against the player-rule ones.</summary>
		public static bool Counting => Content.NPCs.SoldierNPC.HitDbg != null;

		/// <summary>A neutral player body for NPCs that have no gear dummy of their own (summoned units, conjured creatures). Never in Main.player[].</summary>
		public static Player Stand
		{
			get
			{
				if (_stand != null) return _stand;
				var p = new Player { whoAmI = Main.maxPlayers, active = true, width = 20, height = 42, difficulty = 0 };
				p.dead = false; p.ghost = false; p.direction = 1;
				return _stand = p;
			}
		}

		/// <summary>The effective immunity slot for a hit: -1 is the general window (slot 2 is the general window in vanilla's Player.Hurt, and every slot folds into it with slots off).</summary>
		public static int EffectiveSlot(int slot) => Slots && slot >= 0 && slot < SlotCount && slot != ImmunityCooldownID.DD2OgreKnockback ? slot : -1;

		/// <summary>The rectangle-only decision the army used before (kept for the A/B switch and for the parity check's "before" column).</summary>
		public static bool LegacyHits(NPC a, int index, Rectangle victim, NPC victimNpc, out int slot, out float mult)
		{
			slot = -1; mult = 1f;
			if (a.type == NPCID.HallowBoss && (a.ai[0] == 0f || a.ai[0] == 10f)) return false;
			if (victimNpc != null && !NPCLoader.CanHitNPC(a, victimNpc)) return false;
			Rectangle rect = a.Hitbox;
			NPC.GetMeleeCollisionData(victim, index, ref slot, ref mult, ref rect);
			return victim.Intersects(rect);
		}

		/// <summary>
		/// Would <paramref name="a"/> (Main.npc[<paramref name="index"/>]) hurt a body whose hitbox is <paramref name="victim"/>? <paramref name="who"/> is the Player the NPC's hooks are handed
		/// (the levy's gear dummy, or <see cref="Stand"/>); it is moved to <paramref name="victim"/>. <paramref name="slot"/> is the immunity slot to use (-1 general), <paramref name="mult"/> the damage multiplier.
		/// </summary>
		public static bool Hits(NPC a, int index, Rectangle victim, Player who, NPC victimNpc, out int slot, out float mult)
		{
			if (!Parity || who == null) return LegacyHits(a, index, victim, victimNpc, out slot, out mult);
			slot = -1; mult = 1f;
			bool legacy = false;
			if (Counting) legacy = LegacyHits(a, index, victim, victimNpc, out _, out _);
			bool hit = PlayerRules(a, index, victim, who, out slot, out mult);
			if (Counting && hit != legacy)
			{
				if (legacy) { Vetoed++; Content.NPCs.SoldierNPC.HitDbgAdd("veto:" + a.TypeName, 0); }
				else { Added++; Content.NPCs.SoldierNPC.HitDbgAdd("extra:" + a.TypeName, 0); }
			}
			return hit;
		}

		/// <summary>Harness switch (LEVY_PROJPARITY=0): a hostile projectile hits a body wherever its hitbox touches, as before.</summary>
		public static readonly bool ProjParity = Environment.GetEnvironmentVariable("LEVY_PROJPARITY") != "0";
		/// <summary>Telemetry: hostile projectile hits on a body that the projectile's own player rule refused (a player standing there would not have been hit).</summary>
		public static long ProjVetoed;
		private static bool _projWarned;

		/// <summary>
		/// Would hostile projectile <paramref name="p"/> be allowed to hurt a PLAYER standing at <paramref name="victim"/>'s hitbox? Vanilla asks <see cref="ProjectileLoader.CanHitPlayer"/> before it hurts a player, and a
		/// mod writes its real hit shape there (Calamity's growing blasts hit a player only inside 0.4 of their hitbox, as a circle: Ares's Gauss explosion; many boss shots are circles), while a body (an NPC) was hit by the
		/// whole rectangle. Asked only once the rectangles touch (the cheap test first). True when the projectile has no such rule or it throws.
		/// </summary>
		public static bool ProjectileAllows(Projectile p, NPC victim, Player who)
		{
			if (!ProjParity || p == null || victim == null || who == null) return true;
			if (!p.Hitbox.Intersects(victim.Hitbox)) return true; // the rectangle decides alone where they do not even touch (a beam's line is its own Colliding)
			try
			{
				if (who.width != victim.width) who.width = victim.width;
				if (who.height != victim.height) who.height = victim.height;
				who.position = victim.position;
				if (ProjectileLoader.CanHitPlayer(p, who)) return true;
				ProjVetoed++; return false;
			}
			catch (Exception e)
			{
				if (!_projWarned) { _projWarned = true; Levy.Instance?.Logger.Warn($"[HostileContact] projectile {p.Name} threw {e.GetType().Name} in its player hit rule ({e.Message}); its hitbox decides for it"); }
				return true;
			}
		}

		/// <summary>Player.Update_NPCCollision's decision for <paramref name="who"/> standing at <paramref name="victim"/>, with the player's own hurt and immunity bookkeeping left out.</summary>
		public static bool PlayerRules(NPC a, int index, Rectangle victim, Player who, out int slot, out float mult)
		{
			slot = -1; mult = 1f;
			try
			{
				switch (a.type)
				{
					case NPCID.MoonLordHead: case NPCID.MoonLordHand: case NPCID.MoonLordCore: case NPCID.MoonLordFreeEye: case NPCID.MoonLordLeechBlob:
						slot = 1; break;
					case NPCID.HallowBoss:
						slot = 1;
						if (a.ai[0] == 0f || a.ai[0] == 10f) return false;
						break;
				}
				if (who.width != victim.Width) who.width = victim.Width;
				if (who.height != victim.Height) who.height = victim.Height;
				who.position = new Vector2(victim.X, victim.Y);
				if (!NPCLoader.CanHitPlayer(a, who, ref slot)) return false;
				if ((who.npcTypeNoAggro != null && a.type < who.npcTypeNoAggro.Length && who.npcTypeNoAggro[a.type]) || (who.dash == 2 && index == who.eocHit && who.eocDash > 0)) return false;
				Rectangle rect = new Rectangle((int)(a.position.X + a.netOffset.X), (int)(a.position.Y + a.netOffset.Y), a.width, a.height);
				NPC.GetMeleeCollisionData(victim, index, ref slot, ref mult, ref rect);
				return victim.Intersects(rect);
			}
			catch (Exception e)
			{
				if (!_warned) { _warned = true; Levy.Instance?.Logger.Warn($"[HostileContact] {a.TypeName} threw {e.GetType().Name} in its player hit rules ({e.Message}); the bare rectangle decides for it"); }
				return LegacyHits(a, index, victim, null, out slot, out mult);
			}
		}
	}
}
