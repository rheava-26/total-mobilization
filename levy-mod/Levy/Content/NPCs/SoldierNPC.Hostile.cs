using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common;
using Levy.Common.Globals;
using Levy.Common.Soldiers;
using Levy.Common.Targeting;
using Levy.Common.Util;

namespace Levy.Common.Targeting
{
	/// <summary>
	/// A body a hostile soldier may attack that is not a levy: the stand-in NPC a companion mod parks on a player (Warbands' PlayerStandIn). The hostile brain reads targets as NPC indices, so a player is
	/// attacked through an NPC that follows it. Implemented by that companion NPC.
	/// </summary>
	public interface IHostileSideTarget
	{
		/// <summary>False while the player it stands for is dead, a ghost or not in the world.</summary>
		bool IsAlive { get; }
		/// <summary>The hostile soldier's contact attack (a blade swing) landed on this target.</summary>
		void HostileMeleeHit(NPC attacker, int damage, int dir);
	}

	/// <summary>
	/// The side of a hostile soldier (Warbands). While <see cref="Active"/> (set by <c>SoldierNPC.AI</c> for a body whose <c>Hostile</c> is true) the "who is a legal target" questions every part of the Levy brain
	/// asks (<c>LevyUtil.IsHostileTarget</c>, <c>SoldierTargeting.IsNeverTarget</c>) answer for the OTHER side: the player's levies, officers, summoned units, drones and turrets, and the players' stand-ins.
	/// Nothing else in Levy changes its answer.
	/// </summary>
	public static class HostileSide
	{
		/// <summary>True for the length of one hostile soldier's AI call. Server / single player only; NPC AI is single threaded.</summary>
		public static bool Active;

		/// <summary>A live body of the player's side (what a hostile soldier fights).</summary>
		public static bool IsEnemy(NPC n)
		{
			if (n == null || !n.active || n.life <= 0) return false;
			ModNPC m = n.ModNPC;
			if (m == null) return false;
			if (m is global::Levy.Content.NPCs.SoldierNPC s) return !s.Hostile && s.Record != null && s.IsAlive;
			if (m is IHostileSideTarget t) return t.IsAlive;
			if (m is ITargetBody b) return b.IsAlive; // summoned units, Mechanization turrets and drones
			return false;
		}
	}
}

namespace Levy.Content.NPCs
{
	/// <summary>
	/// HOSTILE SOLDIER SEAM (Warbands phase 2, see warbands-mod/SEAMS.md). A subclass registered by a companion mod returns true from <see cref="Hostile"/> and the whole soldier body (brain, movement with hooks, wings and
	/// dodging, weapon use, drawing) runs unchanged on the enemy side: it carries a synthetic <see cref="SoldierRecord"/> that is never in the roster, has no commander, barracks or armory, attacks the player's side
	/// (<see cref="HostileSide"/>), fires hostile-owned shots, takes damage as an ordinary enemy and keeps its own life and defense. All the policy (what a band wants) lives in the subclass through <see cref="HostileThink"/>.
	/// </summary>
	public partial class SoldierNPC
	{
		/// <summary>The side flag. False for every Levy body. A hostile subclass overrides it with true.</summary>
		public virtual bool Hostile => false;

		/// <summary>True while this hostile body must not be targeted by the player's side (a neutral band that has not been provoked). Levies, minions and turrets leave it alone.</summary>
		public virtual bool HostilePassive => false;
		/// <summary>The vanity (social armor) a hostile body is drawn in, or null for its real gear. Client draw path only.</summary>
		public virtual global::Levy.Common.Vanity.VanityLook HostileVanity() => null;
		/// <summary>Colour of the name and life bar of a hostile body.</summary>
		public virtual Color HostilePlateColor => HostilePlateRed;

		/// <summary>Ceiling for this body's defense (a hostile soldier's gear defense is held to the stage line, see Warbands BandStats). Applied after the stat pass.</summary>
		public int HostileDefenseCap = int.MaxValue;
		/// <summary>Multiplies the brain's standoff distance (below 1 closes in, above 1 keeps its distance). 1 = Levy's own.</summary>
		public float HostileStandoffMult = 1f;

		/// <summary>Per-shot damage of a hostile body: <paramref name="raw"/> is the projectile's damage as the weapon layer set it (the gear's real damage, with Levy's grade factor). The result is the projectile's damage; the player takes twice a hostile projectile's damage.</summary>
		public virtual int HostileShotDamage(int raw) => Math.Max(1, (int)Math.Round(raw / LevyConstants.HostileProjectileDamageMult));
		/// <summary>Damage of one blade swing that lands on a target (before its defense). <paramref name="raw"/> is the weapon layer's figure.</summary>
		public virtual int HostileMeleeDamage(int raw) => Math.Max(1, raw);

		/// <summary>Per-shot veto for a hostile body (a band's attack token, a warning that has not been broken). False = hold fire this tick.</summary>
		public virtual bool HostileMayFire(NPC target) => true;

		/// <summary>The brain's decision for a hostile body, every think. Default: engage the nearest of the player's side, else walk toward it. A subclass replaces it with its band tactics, using <see cref="HostileEngage"/>, <see cref="HostileGoTo"/> and <see cref="HostileStop"/>.</summary>
		public virtual void HostileThink(int now)
		{
			if (HostileEngage()) return;
			HostileAdvanceToNearest(HostileDefaultAggroTiles);
		}
		public const float HostileDefaultAggroTiles = 90f;

		/// <summary>The player's-side body this soldier is currently shooting at (null when none).</summary>
		public NPC HostileTarget => TargetIndex >= 0 && TargetIndex < Main.maxNPCs && Main.npc[TargetIndex].active ? Main.npc[TargetIndex] : null;

		/// <summary>Runs Levy's engage logic (target choice, standoff, perch and firing-spot search, melee goal). True when a target is held and the brain set the goal.</summary>
		public bool HostileEngage() => TryEngage((int)Main.GameUpdateCount);

		/// <summary>Walk to a point with the full movement kit (path, jump, hook, wings). With <paramref name="path"/> false the body steps straight at it.</summary>
		public void HostileGoTo(Vector2 pos, bool path = true)
		{
			if (State != SoldierState.Engage && State != SoldierState.Routed) SetState(SoldierState.SearchDestroy);
			_engPathOk = path;
			SetGoal(pos);
		}

		/// <summary>Drops the movement goal (stand and keep shooting).</summary>
		public void HostileStop() { _engPathOk = false; ClearGoal(); }

		/// <summary>Drop the held target (the next think picks again).</summary>
		public void HostileDropTarget() { TargetIndex = -1; _engMode = EngageMode.None; }

		/// <summary>Not fighting: label the state so movement paths.</summary>
		public void HostileIdleState() { if (State != SoldierState.Routed) SetState(SoldierState.SearchDestroy); }

		/// <summary>Fighting retreat: the Levy "Routed" body (backs away from the nearest threat and keeps shooting at its held target). Set and cleared by the subclass's morale.</summary>
		public void HostileSetRouted(bool routed)
		{
			if (routed && State != SoldierState.Routed) { State = SoldierState.Routed; StateTimer = 0; NPC.netUpdate = true; }
			else if (!routed && State == SoldierState.Routed) { State = SoldierState.SearchDestroy; StateTimer = 0; NPC.netUpdate = true; }
		}

		/// <summary>The nearest live body of the player's side within <paramref name="maxTiles"/>, or null.</summary>
		public NPC HostileNearestEnemy(float maxTiles, out float tiles)
		{
			NPC best = null; float bestSq = maxTiles * maxTiles * LevyConstants.TilePx * LevyConstants.TilePx;
			Vector2 c = NPC.Center;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || !HostileSide.IsEnemy(n)) continue;
				float d = Vector2.DistanceSquared(c, n.Center);
				if (d < bestSq) { bestSq = d; best = n; }
			}
			tiles = best == null ? float.MaxValue : (float)Math.Sqrt(bestSq) / LevyConstants.TilePx;
			return best;
		}

		/// <summary>Default idle behaviour: walk toward the nearest enemy within the aggro radius, else stand.</summary>
		public bool HostileAdvanceToNearest(float aggroTiles)
		{
			HostileIdleState();
			NPC n = HostileNearestEnemy(aggroTiles, out _);
			if (n == null) { HostileStop(); return false; }
			HostileGoTo(n.Bottom);
			return true;
		}

		/// <summary>Called by the brain in place of the order logic for a hostile body.</summary>
		private void HostileThinkBody(int now, int fleeIdx, float nearestHostileTiles)
		{
			_spawnGraceUntil = 0; // a band does not hold back after appearing
			if (Record != null) Record.Fear = 0f;
			HostileThink(now);
		}

		/// <summary>Build and bind the synthetic record: no roster, commander, barracks or armory. <paramref name="lifeMax"/> and <paramref name="defense"/> come from the caller's stat rules.</summary>
		public void HostileBind(SoldierRecord record, int lifeMax, int defenseCap)
		{
			record.CommanderName = ""; record.CommanderIndex = -1; record.Kind = SoldierKind.Regular; record.Order = SoldierOrder.Follow; record.HomeBarracksId = -1;
			Bind(record);
			NPC.lifeMax = Math.Max(1, lifeMax); NPC.life = NPC.lifeMax;
			HostileDefenseCap = defenseCap;
			Record.Mana = Record.MaxMana;
			_spawnGraceUntil = 0;
			State = SoldierState.SearchDestroy;
			NPC.friendly = false;
			StatsDirty = true;
		}

		/// <summary>Marks a projectile this hostile body fired: hostile-owned (hits the player and the player's levies, not the enemy side), damage per <see cref="HostileShotDamage"/>, and the tag a hit is credited through.</summary>
		internal void TagHostileShot(Projectile p, DamageClass dmgType)
		{
			p.owner = Common.Weapons.WeaponUse.OwnerFor(this);
			p.npcProj = true;
			p.noDropItem = true;
			p.friendly = false;
			p.hostile = true;
			p.damage = Math.Max(1, HostileShotDamage(p.damage));
			if (dmgType != null) p.DamageType = dmgType;
			if (p.TryGetGlobalProjectile<SoldierProjectileTag>(out var tag)) { tag.SourceNpcIndex = NPC.whoAmI; tag.SourceNpcType = NPC.type; tag.HostileShot = true; }
			p.netUpdate = true;
		}

		/// <summary>A blade swing from a hostile body: hits each live body of the player's side its swing box touches, once per swing.</summary>
		private void HostileSwing(Item item, Rectangle box)
		{
			int raw = Math.Max(1, (int)(WeaponDamage(item) * DamageMultiplier));
			int dmg = Math.Max(1, HostileMeleeDamage(raw));
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || _swingHits.Contains(i) || !HostileSide.IsEnemy(n)) continue;
				if (!box.Intersects(n.Hitbox)) continue;
				if (!n.noTileCollide && !Collision.CanHit(NPC.position, NPC.width, NPC.height, n.position, n.width, n.height)) continue;
				int dir = n.Center.X < NPC.Center.X ? -1 : 1;
				_swingHits.Add(i);
				if (n.ModNPC is SoldierNPC sn) sn.ReceiveHostileMelee(NPC, dmg, dir);
				else if (n.ModNPC is IHostileSideTarget t) t.HostileMeleeHit(NPC, dmg, dir);
				else n.SimpleStrikeNPC(dmg, dir, false, item.knockBack, null, false, 0f, true);
				LastMeleeHitTick = (int)Main.GameUpdateCount;
			}
		}

		/// <summary>A hostile soldier's blade lands on this levy: the same intake as an enemy's contact hit (defense formula, immune window, fear, barks).</summary>
		public int ReceiveHostileMelee(NPC attacker, int damage, int dir)
		{
			if (ImmuneTime > 0 || !IsAlive || LevyUtil.IsClient) return 0;
			if (TryShadowDodge()) return 0;
			LastAttackerIndex = attacker.whoAmI; LastAttackerType = attacker.type; LastAttackerName = attacker.GivenOrTypeName;
			int done;
			_contactStriking = true;
			PendingContactScale = IncomingSetScale();
			try { done = NPC.SimpleStrikeNPC(Math.Max(1, damage), dir, false, NPC.knockBackResist > 0f ? LevyConstants.FizzContactKnockback : 0f, null, false, 0f, true); }
			finally { _contactStriking = false; PendingContactScale = 1f; }
			AfterHit(done, attacker.whoAmI);
			return done;
		}

		/// <summary>Is this projectile one this body should step out of? A levy dodges hostile shots; a hostile body dodges the player's side's.</summary>
		private bool IsThreatShot(Projectile p) => Hostile ? p.friendly && !p.hostile : p.hostile;

		/// <summary>Name over the head of a hostile body; a subclass overrides it for a band tag.</summary>
		public virtual string HostilePlateTag => null;
		/// <summary>Colour of a hostile body's name and bar.</summary>
		public static readonly Color HostilePlateRed = new Color(238, 74, 62);

		/// <summary>The red plate of a hostile body: name (and band tag), a red life bar, the weapon it holds.</summary>
		internal void DrawHostilePlate(SpriteBatch sb, Vector2 screenPos)
		{
			if (Main.dedServ || Record == null) return;
			var font = FontAssets.MouseText.Value;
			Vector2 top = NPC.Top - screenPos;
			float scale = LevyConstants.UI.NameplateTextScale;
			float y = top.Y - LevyConstants.UI.NameplateOffsetY;
			string name = Record.Name ?? "";
			Color plate = HostilePlateColor;
			Utils.DrawBorderString(sb, name, new Vector2(top.X, y), plate, scale, 0.5f, 0.5f);
			string tag = HostilePlateTag;
			if (!string.IsNullOrEmpty(tag)) Utils.DrawBorderString(sb, tag, new Vector2(top.X, y - 14f * scale), new Color(190, 70, 60), scale * 0.8f, 0.5f, 0.5f);
			int w = LevyConstants.UI.NameplateHpBarWidth, h = LevyConstants.UI.NameplateHpBarHeight;
			float frac = MathHelper.Clamp(NPC.life / (float)Math.Max(1, NPC.lifeMax), 0f, 1f);
			var pixel = TextureAssets.MagicPixel.Value;
			int bx = (int)(top.X - w * 0.5f), by = (int)(y + 8f);
			sb.Draw(pixel, new Rectangle(bx - 1, by - 1, w + 2, h + 2), new Color(20, 6, 6, 230));
			sb.Draw(pixel, new Rectangle(bx, by, (int)(w * frac), h), new Color((int)(plate.R * 0.84f), (int)(plate.G * 0.5f), (int)(plate.B * 0.5f)));
			var weapon = Record.Gear?.Weapon;
			if (weapon != null && !weapon.IsAir)
				Utils.DrawBorderString(sb, weapon.Name, new Vector2(top.X, by + h + 6f), new Color(200, 150, 140), scale * LevyConstants.UI.NameplateWeaponScale, 0.5f, 0f);
			if (State == SoldierState.Routed) Utils.DrawBorderString(sb, "!!", new Vector2(bx + w + 6f, by + h * 0.5f), new Color(255, 214, 60), 1.1f, 0f, 0.5f);
		}
	}
}
