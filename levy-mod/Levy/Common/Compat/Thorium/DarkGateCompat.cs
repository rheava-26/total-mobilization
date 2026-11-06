using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;
using ThoriumMod.Buffs;
using ThoriumMod.Items.Donate;
using ThoriumMod.Projectiles;
using Levy.Common.Globals;
using Levy.Common.Util;
using Levy.Common.Weapons;
using Levy.Content.NPCs;

namespace Levy.Common.Compat.Thorium
{
	/// <summary>What a soldier does with Thorium's Dark Gate this tick.</summary>
	public enum GateAction { None, Use, Place }

	/// <summary>Everything the Dark Gate decision reads, so the rule can be checked without a world.</summary>
	public struct GateSituation
	{
		public bool CommanderHasChaos, CommanderHoldsGate, CommanderNearSourceGate;
		/// <summary>Tiles from the soldier to the nearest gate that has a live partner (999 = none).</summary>
		public float SoldierToGate;
		public bool OwnCooldownUp, ToughNearby, PlayerOwnsGate, OtherSoldierGates;
		public float SelfHp, SoldierToCommander, PartnerToCommander;
		public bool HoldsGateItem, HasMana, PlaceCooldownUp, HaveGateOut, WoundedTargetFound, LineOfSight;
	}

	/// <summary>
	/// DARK GATE (Thorium, Donate/DarkGate): left-click places a linked pair of gateways, one at the caster and one at the cursor (50 mana, needs a clear line, and it deletes the owner's earlier pair);
	/// right-clicking with the cursor near a gateway teleports to the partner and heals, then <c>ChaosState</c> (12 s) blocks the next use. It has no AltFunctionUse: Thorium's player code does it every frame.
	/// Soldiers follow the rules in tools/altfire/patterns.md section 6 (PORTAL_USE):
	/// USE: any soldier within 3 tiles of a gateway whose own cooldown is up steps through to retreat and heal below 50% HP, or to regroup when it is 40+ tiles from the commander and the partner
	/// is 25+ tiles closer. Never while a tough enemy is within 12 tiles (it does not leave a fight to teleport).
	/// YIELD: nothing happens while the commander has ChaosState, is within 6 tiles of the gateway the soldier would enter, or holds a Dark Gate (the player is operating the pair).
	/// PLACE: only a soldier holding the item, only when no gate is out that the commander or another soldier owns (placing kills the owner's earlier pair, and soldier shots share the commander's owner
	/// slot), to bring a wounded commander or squadmate a retreat: gate A at the soldier, gate B within 6 tiles of the wounded one, with a clear line.
	/// </summary>
	[JITWhenModsEnabled("ThoriumMod")]
	public static class DarkGateCompat
	{
		public const float UseRangeTiles = 3f, YieldNearTiles = 6f, ToughRangeTiles = 12f, FarFromCommanderTiles = 40f, RegroupGainTiles = 25f, RetreatHp = 0.5f;
		public const float PlaceMinGapTiles = 10f, PlaceMaxTiles = 45f, PlaceWoundedBelow = 0.6f, PlaceBTiles = 6f;
		public const int PlaceCooldownTicks = 720;

		internal static bool IsGate(Item item) => item != null && !item.IsAir && item.ModItem is DarkGate;

		/// <summary>The pure rule: use, place or nothing.</summary>
		public static GateAction Decide(in GateSituation g)
		{
			if (g.CommanderHasChaos || g.CommanderHoldsGate) return GateAction.None;
			if (g.SoldierToGate <= UseRangeTiles)
			{
				if (g.CommanderNearSourceGate || !g.OwnCooldownUp || g.ToughNearby) return GateAction.None;
				bool retreat = g.SelfHp < RetreatHp && g.PartnerToCommander < g.SoldierToCommander;
				bool regroup = g.SoldierToCommander > FarFromCommanderTiles && g.PartnerToCommander <= g.SoldierToCommander - RegroupGainTiles;
				return retreat || regroup ? GateAction.Use : GateAction.None;
			}
			if (g.HoldsGateItem && g.HasMana && g.PlaceCooldownUp && !g.PlayerOwnsGate && !g.OtherSoldierGates && g.WoundedTargetFound && g.LineOfSight && !g.ToughNearby) return GateAction.Place;
			return GateAction.None;
		}

		// ---- gates in the world (one scan per tick) ----
		private static uint _scanTick = uint.MaxValue;
		private static readonly List<Projectile> _gates = new();
		private static List<Projectile> Gates()
		{
			if (_scanTick == Main.GameUpdateCount) return _gates;
			_scanTick = Main.GameUpdateCount; _gates.Clear();
			int t = ModContent.ProjectileType<DarkGatePro>();
			for (int i = 0; i < Main.maxProjectiles; i++) { Projectile p = Main.projectile[i]; if (p.active && p.type == t) _gates.Add(p); }
			return _gates;
		}

		private static Projectile PartnerOf(Projectile g)
		{
			if (g.ModProjectile is not DarkGatePro m) return null;
			Vector2 dest = m.Destination; Projectile best = null; float bd = 48f * 48f;
			foreach (Projectile o in Gates())
			{
				if (o.whoAmI == g.whoAmI) continue;
				float d = Vector2.DistanceSquared(o.Center, dest);
				if (d < bd) { bd = d; best = o; }
			}
			return best;
		}

		private static bool CommanderHoldsGate(Player c) { try { return c.HeldItem != null && c.HeldItem.ModItem is DarkGate; } catch (Exception) { return false; } }

		/// <summary>Per soldier, per tick: use a gate the soldier stands at, or (for the holder) place a pair. Runs for every soldier while Thorium is loaded; a tick with no gates and no gate item costs nothing.</summary>
		public static void Drive(SoldierNPC s, SoldierThoriumGlobalNPC st)
		{
			Item held = WeaponUse.HeldItem(s);
			bool holds = IsGate(held);
			var gates = Gates();
			if (gates.Count == 0 && !holds) return;
			uint now = Main.GameUpdateCount;
			Player c = CompatAllies.CommanderOf(s);
			if (c == null) return;

			var g = new GateSituation
			{
				CommanderHasChaos = c.HasBuff(ModContent.BuffType<ChaosState>()),
				CommanderHoldsGate = CommanderHoldsGate(c),
				SoldierToGate = 999f, OwnCooldownUp = now >= st.GateUseReady, SelfHp = s.NPC.lifeMax > 0 ? (float)s.NPC.life / s.NPC.lifeMax : 1f,
				SoldierToCommander = Vector2.Distance(s.NPC.Center, c.Center) / 16f,
				HoldsGateItem = holds, PlaceCooldownUp = now >= st.GatePlaceReady,
			};
			Projectile src = null, partner = null;
			foreach (Projectile p in gates)
			{
				if (!p.TryGetGlobalProjectile(out SoldierProjectileTag t) || t.SoldierIndex < 0) g.PlayerOwnsGate = true;
				else if (t.SoldierIndex != s.NPC.whoAmI) g.OtherSoldierGates = true;
				else g.HaveGateOut = true;
				float d = Vector2.Distance(s.NPC.Center, p.Center) / 16f;
				if (d < g.SoldierToGate) { Projectile pr = PartnerOf(p); if (pr != null) { g.SoldierToGate = d; src = p; partner = pr; } }
			}
			if (src != null)
			{
				g.CommanderNearSourceGate = Vector2.Distance(c.Center, src.Center) / 16f <= YieldNearTiles;
				g.PartnerToCommander = Vector2.Distance(partner.Center, c.Center) / 16f;
			}
			if (g.SoldierToGate <= UseRangeTiles || holds) g.ToughNearby = ToughWithin(s, ToughRangeTiles);

			Vector2 b = default;
			if (holds && g.SoldierToGate > UseRangeTiles)
			{
				g.HasMana = s.Record.Mana >= Math.Max(0, held.mana);
				if (g.HasMana && g.PlaceCooldownUp && !g.PlayerOwnsGate && !g.OtherSoldierGates) g.WoundedTargetFound = FindPlacement(s, c, out b, out g.LineOfSight);
			}

			switch (Decide(g))
			{
				case GateAction.Use: Use(s, st, src, partner); break;
				case GateAction.Place: Place(s, st, held, b); break;
			}
		}

		private static bool ToughWithin(SoldierNPC s, float tiles)
		{
			float r2 = tiles * 16f; r2 *= r2;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC e = Main.npc[i];
				if (e.active && (e.boss || e.lifeMax >= 600) && LevyUtil.IsHostileTarget(e) && Vector2.DistanceSquared(e.Center, s.NPC.Center) <= r2) return true;
			}
			return false;
		}

		/// <summary>The most wounded ally (commander or squadmate) worth a gate, and the point for the second gate near it (clear line from the soldier).</summary>
		private static bool FindPlacement(SoldierNPC s, Player c, out Vector2 b, out bool los)
		{
			b = default; los = false;
			Vector2 from = s.NPC.Center; float bestFrac = PlaceWoundedBelow; Vector2 at = default; bool found = false;
			if (c.statLifeMax2 > 0)
			{
				float f = (float)c.statLife / c.statLifeMax2, d = Vector2.Distance(from, c.Center) / 16f;
				if (f < bestFrac && d >= PlaceMinGapTiles && d <= PlaceMaxTiles) { bestFrac = f; at = c.Center; found = true; }
			}
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC a = Main.npc[i];
				if (!a.active || a.whoAmI == s.NPC.whoAmI || a.ModNPC is not SoldierNPC o || o.Record == null || a.lifeMax <= 0 || !CompatAllies.SameCommander(s, o)) continue;
				float f = (float)a.life / a.lifeMax, d = Vector2.Distance(from, a.Center) / 16f;
				if (f < bestFrac && d >= PlaceMinGapTiles && d <= PlaceMaxTiles) { bestFrac = f; at = a.Center; found = true; }
			}
			if (!found) return false;
			Vector2 toward = from - at; if (toward.LengthSquared() > 1f) toward.Normalize();
			foreach (float tiles in new[] { PlaceBTiles * 0.5f, 1f, 0f })
			{
				Vector2 cand = at + toward * tiles * 16f;
				if (Collision.CanHit(from, 1, 1, cand, 1, 1) && !Collision.SolidCollision(cand - new Vector2(8f, 8f), 16, 16)) { b = cand; los = true; return true; }
			}
			return true; // found someone, no clear line: LineOfSight stays false
		}

		private static void Use(SoldierNPC s, SoldierThoriumGlobalNPC st, Projectile src, Projectile partner)
		{
			if (src.ModProjectile is not DarkGatePro m) return;
			NPC n = s.NPC;
			Vector2 dest = partner.Center;
			n.Center = dest; n.velocity = Vector2.Zero; n.netUpdate = true;
			int heal = Math.Max(0, m.Heal);
			int before = n.life; n.life = Math.Min(n.lifeMax, n.life + heal);
			if (n.life > before) { n.HealEffect(n.life - before, true); s.Record.CurrentHp = n.life; }
			st.GateUseReady = Main.GameUpdateCount + (uint)Math.Max(60, m.Timer);
			UsedCount++;
			if (!Main.dedServ) for (int i = 0; i < 12; i++) Dust.NewDustPerfect(dest, 27, Main.rand.NextVector2Circular(3f, 3f), 255, default, 1.3f).noGravity = true;
		}

		private static void Place(SoldierNPC s, SoldierThoriumGlobalNPC st, Item item, Vector2 b)
		{
			Player d = s.Dummy; if (d == null) return;
			int t = ModContent.ProjectileType<DarkGatePro>();
			var before = new HashSet<int>();
			for (int i = 0; i < Main.maxProjectiles; i++) if (Main.projectile[i].active && Main.projectile[i].type == t) before.Add(i);
			// the soldier's own earlier pair goes (the item would delete it); nobody else's is out (checked), so nothing else is touched
			foreach (int i in before) Main.projectile[i].Kill();
			Vector2 savedCenter = d.Center; int savedDir = d.direction;
			try
			{
				d.Center = s.NPC.Center; d.direction = s.NPC.direction == 0 ? 1 : s.NPC.direction;
				using (new AltPolicies.AimScope(d, b)) DarkGatePro.Spawn(d);
			}
			finally { d.Center = savedCenter; d.direction = savedDir; }
			int tagged = 0;
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile p = Main.projectile[i];
				if (!p.active || p.type != t || before.Contains(i)) continue;
				if (p.TryGetGlobalProjectile(out SoldierProjectileTag tag)) { tag.SoldierIndex = s.NPC.whoAmI; tagged++; }
			}
			s.Record.Mana = Math.Max(0, s.Record.Mana - Math.Max(0, item.mana));
			st.GatePlaceReady = Main.GameUpdateCount + (uint)PlaceCooldownTicks;
			if (tagged == 2) PlacedCount++;
			if (item.UseSound.HasValue) SoundEngine.PlaySound(item.UseSound.Value, s.NPC.Center);
			if (s.ItemAnimation <= 0) { s.ItemAnimation = s.ItemAnimationMax = Math.Max(1, item.useAnimation); s.UsesThisAnimation = 0; }
			s.AimRotation = (b - s.NPC.Center).ToRotation();
			s.NPC.netUpdate = true;
		}

		/// <summary>Counters for the harness scenario.</summary>
		public static int UsedCount, PlacedCount;
	}
}
