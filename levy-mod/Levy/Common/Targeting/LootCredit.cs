using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Boss;
using Levy.Common.Dialogue;
using Levy.Common.Globals;
using Levy.Common.Util;
using Levy.Content.NPCs;

namespace Levy.Common.Targeting
{
	/// <summary>
	/// Marks the commander as having interacted so vanilla loot rules credit soldier kills. OWNED BY THE TARGETING AGENT.
	/// Soldier projectiles are npcProj (Projectile.Damage: flag2 = !npcProj &amp;&amp; !trap gates every owner on-hit effect, and
	/// StrikeNPC is called with noPlayerInteraction = !flag2), so vanilla never credits them: NPC.NPCLoot only counts banners/bestiary
	/// when GetWereThereAnyInteractions() and names Main.player[lastInteraction] in the banner announcement. NPC.PlayerInteraction(idx)
	/// sets playerInteraction[idx], lastInteraction = idx (idx &lt; 255) and mirrors onto realLife roots and linked segments.
	/// </summary>
	public static class LootCredit
	{
		/// <summary>
		/// Player index that receives loot credit for a soldier's hits: the commander, else the local player in single player, else -1.
		/// CONTRACT-REQUEST: SoldierNPC.CreditPlayer (core) carries this at integration; computed locally until then.
		/// Never use SoldierNPC.ProjectileOwner for credit: it is Main.myPlayer (255 on a dedicated server) so the server processes the hits.
		/// </summary>
		public static int CreditPlayerOf(SoldierNPC shooter)
		{
			if (shooter == null) return -1;
			int c = shooter.CommanderIndex;
			if (c >= 0 && c < Main.maxPlayers && Main.player[c].active) return c;
			int me = Weapons.SoldierMinions.TrueMyPlayer; // not Main.myPlayer: a real minion's single-player Update has it faked to 255
			if (Main.netMode == NetmodeID.SinglePlayer && me >= 0 && me < Main.maxPlayers) return me;
			return -1;
		}

		/// <summary>
		/// Credit the shooter's commander with this enemy (banner progress, bestiary, "defeated by" line, instanced drops).
		/// Call it BEFORE the strike: NPC.StrikeNPC runs checkDead() → NPCLoot synchronously, so a credit applied afterwards misses the killing blow.
		/// </summary>
		public static void Credit(NPC target, SoldierNPC shooter)
		{
			if (target == null) return;
			int idx = CreditPlayerOf(shooter);
			if (idx < 0 || idx >= Main.maxPlayers) return;
			target.PlayerInteraction(idx);
		}

		/// <summary>
		/// Everything that happens after a soldier damaged an enemy, from any weapon family (projectile hits arrive through
		/// LootCreditGlobalProjectile; melee code calls Credit before SimpleStrikeNPC and this after it). Server / single player only.
		/// </summary>
		public static void OnSoldierHit(NPC target, SoldierNPC body, int damageDone)
		{
			if (target == null || body == null || body.Record == null || LevyUtil.IsClient) return;
			Credit(target, body); // harmless repeat; keeps melee callers that only call this correct for non-lethal hits
			BossRule.OnSoldierDamagedNpc(target, body, damageDone);
			Venom.VenomSystem.OnArmyHit(target, body.NPC.Center); // the Witch Doctor's venom: a share of every soldier's hits (a no-op with no Witch Doctor fielded)
			LevyNpcData.Of(target).LastHitBySoldier = body.NPC.whoAmI;
			NPC root = LevyUtil.RootNpc(target);
			if (root.life <= 0 || target.life <= 0)
			{
				body.Record.Kills++;
				DialogueSystem.Bark(body, DialogueTrigger.KILL);
			}
		}
	}

	/// <summary>Soldier shots: never hit what a soldier would never choose; book-keep every hit. Hooks fire on the machine whose index owns the projectile (the server for npcProj shots owned by Main.myPlayer).</summary>
	public class LootCreditGlobalProjectile : GlobalProjectile
	{
		public override bool? CanHitNPC(Projectile projectile, NPC target)
		{
			var tag = projectile.GetGlobalProjectile<SoldierProjectileTag>();
			if (!tag.IsSoldierShot) return null;
			// Friendly projectiles already skip friendly NPCs (Projectile.Damage flag9 = !npc.friendly); this adds the never-target list:
			// disguised mimics, cultist set pieces, bound NPCs, Plantera's hooks, the Dungeon Guardian... never start a fight we did not choose.
			// (the one exception: a critter inside the commander's Focus Attack radius)
			if (SoldierTargeting.IsNeverTarget(target) && !SoldierTargeting.FocusAllowsCritterShot(tag.SoldierIndex, target)) return false;
			// a soldier told to hold fire keeps its real minions beside it but they deal nothing
			if (Weapons.SoldierMinions.SuppressedByHoldFire(projectile, tag.SoldierIndex)) return false;
			return null;
		}

		// Runs before NPC.StrikeNPC (which drops loot synchronously on a kill): the commander must already be on the interaction list here.
		public override void ModifyHitNPC(Projectile projectile, NPC target, ref NPC.HitModifiers modifiers)
		{
			var tag = projectile.GetGlobalProjectile<SoldierProjectileTag>();
			if (!tag.IsSoldierShot || LevyUtil.IsClient) return;
			SoldierNPC body = EnemyTargeting.LiveBody(tag.SoldierIndex);
			if (body != null) LootCredit.Credit(target, body);
		}

		public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
		{
			var tag = projectile.GetGlobalProjectile<SoldierProjectileTag>();
			if (!tag.IsSoldierShot) return;
			SoldierNPC body = EnemyTargeting.LiveBody(tag.SoldierIndex);
			if (body == null) return;
			LootCredit.OnSoldierHit(target, body, damageDone);
		}
	}

	/// <summary>Attacker tracking for hostile projectiles hitting soldier bodies, and attacker-table hygiene on death.</summary>
	public class LootCreditGlobalNPC : GlobalNPC
	{
		public override void OnHitByProjectile(NPC npc, Projectile projectile, NPC.HitInfo hit, int damageDone)
		{
			if (npc.ModNPC is not SoldierNPC || !projectile.hostile) return;
			var tag = projectile.GetGlobalProjectile<SoldierProjectileTag>();
			if (tag.SourceNpcIndex >= 0) SoldierTargeting.NoteAttacker(tag.SourceNpcIndex);
		}

		// Kills are counted on the hit that killed (OnSoldierHit); here only the attacker memory of the slot is dropped.
		public override void OnKill(NPC npc) => SoldierTargeting.ForgetAttacker(npc.whoAmI);
	}
}
