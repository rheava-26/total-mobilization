using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Levy.Common;
using Levy.Common.Util;
using Levy.Common.Weapons;

namespace Levy.Content.NPCs
{
	/// <summary>
	/// ON-HIT GEAR FOR A LEVY'S SHOTS. Vanilla runs a player's mod hooks for a projectile hit (<c>PlayerLoader.OnHitNPCWithProj</c>, through <c>CombinedHooks.OnHitNPCWithProj</c>) only for the projectile's OWNER, and
	/// <c>Projectile.TryGetOwner</c> returns nobody for an <c>npcProj</c> shot. A levy's shot is an npcProj shot owned by the commander, so the hook never ran: not for the commander's gear (right) and not for the levy's
	/// own either (wrong). Every set bonus and accessory a mod keeps as a flag on its ModPlayer and reads in that hook (Calamity's Astral star rain, Tarragon / Bloodflare / Silva / Ataxia on-hit effects, the Bloodflare
	/// and Ataxia life steal, the Forbidden Circlet, the Gem Tech set, the debuff-damage multipliers a player copies onto what it hits; Thorium's and any other mod's) showed on a levy's ranged, magic, rogue and summon hits
	/// as nothing. A swing already ran it (<see cref="Levy.Common.Weapons.MeleeEffects"/>).
	/// <para/>Here the same hook is run for the levy's shot on the levy's own dummy, in the frame the hurt reaction uses (the dummy stands in a player slot and is the local player for the length of the call, so a hook that
	/// spawns something or heals only for <c>whoAmI == Main.myPlayer</c> does it), and what the hook spawns is adopted as the levy's own like every gear spawn. Only for a levy whose gear is modded (<see cref="UpkeepActive"/>:
	/// the soldiers that pay for the per-tick frame already); one hook call per hit, at most <see cref="HitsPerTick"/> a tick for one levy. Nothing the commander wears is involved.
	/// <c>LEVY_GEARONHIT=0</c> switches it off (A/B for the lab).
	/// </summary>
	public partial class SoldierNPC
	{
		internal static readonly bool GearOnHitOn = Environment.GetEnvironmentVariable("LEVY_GEARONHIT") != "0";
		/// <summary>The most hook calls one levy makes in one tick (a shotgun's pellets all land in the same tick; nothing a player's hand makes comes near it).</summary>
		internal const int HitsPerTick = 24;
		private bool _inGearHit; private uint _gearHitTick; private int _gearHitCount;
		/// <summary>Lab counters: hook calls run for a levy's projectile hits, and the ones the per-tick cap held back.</summary>
		internal static long GearHitCalls, GearHitCapped, GearHitErrors;

		/// <summary>The levy's shot <paramref name="p"/> just hit <paramref name="target"/>: the dummy's ModPlayer hooks for a projectile hit run, as for a player's own shot. Server / single player.</summary>
		internal void GearOnHitProj(Projectile p, NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (!GearOnHitOn || _inGearHit || LevyUtil.IsClient || !IsAlive || !UpkeepActive || p == null || target == null || !p.npcProj || (PlayerCounters.HasHitRows && PlayerCounters.CountsShot(p))) return; // a shot that is not npcProj already ran vanilla's own hook for its owner (a held chain's real owner)
			if (_gearHitTick != Main.GameUpdateCount) { _gearHitTick = Main.GameUpdateCount; _gearHitCount = 0; }
			if (++_gearHitCount > HitsPerTick) { GearHitCapped++; return; }
			_inGearHit = true;
			Player d = Dummy;
			int savedWho = d.whoAmI; Vector2 savedCenter = d.Center; int savedLife = d.statLife;
			object thorium = null;
			int savedOwner = p.owner; bool savedNpcProj = p.npcProj;
			FrameSlot fs = default;
			var prev = GearUpkeep.Body; GearUpkeep.Body = this;
			int spawnStart = GearUpkeep.Spawned.Count;
			try
			{
				d.Center = NPC.Center;
				fs = EnterFrameSlot();
				thorium = OwnerPuppet.SwapThorium(d, true);
				// For the length of the call the shot is a player's: owned by the dummy's slot (a hook that homes a life steal or spawns a helper at "the owner" finds the levy, never the commander) and not an npcProj shot
				// (Calamity wraps its whole on-hit body in !proj.npcProj).
				p.owner = DummySlot; p.npcProj = false;
				int life0 = NPC.life; d.statLife = life0;
				PlayerLoader.OnHitNPCWithProj(d, p, target, in hit, damageDone);
				GearHitCalls++;
				int heal = d.statLife - life0; // a life steal the hook made (Bloodflare, Ataxia): the body gets it
				if (heal > 0 && NPC.life > 0) { NPC.life = Math.Min(NPC.lifeMax, NPC.life + heal); NPC.netUpdate = true; if (Record != null) Record.CurrentHp = NPC.life; GearUpkeep.LifeWriteBacks++; }
			}
			catch (Exception e)
			{
				GearHitErrors++; GearUpkeep.HookFailures++;
				if (GearUpkeep.WarnOnce(e)) Levy.Instance?.Logger.Warn("Levy: a ModPlayer on-hit hook threw for a levy's shot (once per kind): " + e);
			}
			finally
			{
				p.owner = savedOwner; p.npcProj = savedNpcProj;
				ExitFrameSlot(fs);
				d.whoAmI = savedWho; d.Center = savedCenter; d.statLife = NPC.life > 0 ? NPC.life : savedLife; d.dead = false;
				OwnerPuppet.RestoreThorium(thorium);
				GearUpkeep.Body = prev;
				AdoptUpkeepSpawns(spawnStart, false);
				_inGearHit = false;
			}
		}
	}
}
