using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;
using Levy.Common.Config;
using Levy.Common.Globals;
using Levy.Common.Targeting;
using Levy.Common.Util;
using Levy.Content.NPCs;

namespace Levy.Common.UI
{
	/// <summary>
	/// A companion mod's machine that should carry an ally tag (Mechanization's turrets and combat drones implement it). Levy reads only this, so it never depends on the mod.
	/// The machine must also be an <see cref="ITargetBody"/> (the seam Levy already sees companion machines through); the tag shows while <see cref="ITargetBody.IsAlive"/>.
	/// </summary>
	public interface IMachineNameTag
	{
		/// <summary>True for a drone, false for a turret.</summary>
		bool TagIsDrone { get; }
		/// <summary>The item type of the mounted weapon, or 0 when it fires its plain round.</summary>
		int TagWeaponType { get; }
	}

	/// <summary>
	/// ALLY TAGS: a small line of text over every friendly combatant that is the army's but is not a levy (levies have their own <see cref="Nameplates"/>):
	///  * a levy's minion, sentry, pet or gear helper (tagged to him through <see cref="SoldierProjectileTag.SoldierIndex"/>): "Belle's Baby Slime";
	///  * an officer's conjured creature (<see cref="ConjuredGlobalNPC"/>) or summoned unit (<see cref="SummonedUnitNPC"/>): "Zoologist's Antlion", by the officer's role;
	///  * a companion mod's machine (<see cref="IMachineNameTag"/>): "Turret", "Drone: Minishark".
	/// The commander's own minions carry no tag. Tags follow the nameplate setting (plus <see cref="LevyConfig.AllyNameTags"/>) and the War Horn's Show names; above <see cref="LevyConstants.NameplateHideAbove"/>
	/// on screen only the hovered one is drawn. One pass per frame over the NPC and projectile arrays, on-screen only; the text of each slot is cached and rebuilt only when what it names changes, so a
	/// steady frame allocates nothing.
	/// </summary>
	public class AllyTags : ModSystem
	{
		private enum Kind : byte { None, Conjured, Unit, Machine, Minion }

		private struct Slot { public Kind K; public int Type, A, B; public string S, Text; }

		private static readonly Slot[] _npc = new Slot[Main.maxNPCs + 1], _proj = new Slot[Main.maxProjectiles + 1];

		/// <summary>One tag to draw this frame: where its entity's top is (world), its hitbox, the text and how far above the top it sits.</summary>
		internal struct Entry { public Vector2 Top; public Rectangle Box; public string Text; public float Lift; }

		private static readonly Entry[] _entries = new Entry[Main.maxNPCs + Main.maxProjectiles + 2];
		private static int _count;

		private static readonly Color TagColor = new(175, 215, 175);

		/// <summary>Projectile types that are a minion's real body although they take no slot (the slot sits on an invisible counter), and the invisible counters themselves.</summary>
		private static readonly HashSet<int> SlotlessBodies = new() { ProjectileID.StormTigerTier1, ProjectileID.StormTigerTier2, ProjectileID.StormTigerTier3, ProjectileID.AbigailMinion };
		private static readonly HashSet<int> Counters = new() { ProjectileID.StormTigerGem, ProjectileID.AbigailCounter };

		public override void OnWorldUnload() { System.Array.Clear(_npc); System.Array.Clear(_proj); _count = 0; }

		// ------------------------------------------------------------------ settings

		/// <summary>True when ally tags are shown at all; <paramref name="forced"/> = the War Horn's Show names is on (every tag, even in a crowd).</summary>
		public static bool Enabled(out bool forced)
		{
			forced = UISystem.NameplatesForced;
			var cfg = LevyConfig.Get;
			if (cfg == null || !cfg.AllyNameTags) return false;
			if (forced) return true;
			return cfg.Nameplates && !global::Levy.Common.Perf.PerfAnalyzer.EffectsReduced;
		}

		// ------------------------------------------------------------------ the text (also what the self-checks read)

		/// <summary>The tag an NPC carries, or null when it is not one of the army's tagged allies.</summary>
		public static string For(NPC npc)
		{
			if (npc == null || !npc.active || npc.whoAmI < 0 || npc.whoAmI > Main.maxNPCs) return null;
			ref Slot s = ref _npc[npc.whoAmI];
			if (npc.ModNPC is SummonedUnitNPC unit)
			{
				int role = OfficerRole(unit.OwnerWho);
				if (role <= 0) return null;
				string nm = string.IsNullOrEmpty(npc.GivenName) ? npc.TypeName : npc.GivenName;
				if (s.K != Kind.Unit || s.Type != npc.type || s.A != role || !ReferenceEquals(s.S, nm)) s = new Slot { K = Kind.Unit, Type = npc.type, A = role, S = nm, Text = LevyUtil.T("AllyTags.Of", Lang.GetNPCNameValue(role), nm) };
				return s.Text;
			}
			if (npc.ModNPC is IMachineNameTag m)
			{
				if (npc.ModNPC is not ITargetBody tb || !tb.IsAlive) return null;
				int drone = m.TagIsDrone ? 1 : 0, w = m.TagWeaponType;
				if (w < 0 || w >= ItemLoader.ItemCount) w = 0;
				if (s.K != Kind.Machine || s.Type != npc.type || s.A != drone || s.B != w)
				{
					string word = LevyUtil.T(drone == 1 ? "AllyTags.Drone" : "AllyTags.Turret");
					s = new Slot { K = Kind.Machine, Type = npc.type, A = drone, B = w, Text = w > 0 ? LevyUtil.T("AllyTags.Armed", word, Lang.GetItemNameValue(w)) : word };
				}
				return s.Text;
			}
			if (npc.TryGetGlobalNPC(out ConjuredGlobalNPC c) && c.Active && !c.IsChild)
			{
				int role = OfficerRole(c.OwnerWho);
				if (role <= 0) return null;
				if (s.K != Kind.Conjured || s.Type != npc.type || s.A != role) s = new Slot { K = Kind.Conjured, Type = npc.type, A = role, Text = LevyUtil.T("AllyTags.Of", Lang.GetNPCNameValue(role), npc.TypeName) };
				return s.Text;
			}
			return null;
		}

		/// <summary>The tag a projectile carries (a levy's minion, sentry, pet or gear helper), or null.</summary>
		public static string For(Projectile p)
		{
			if (p == null || !p.active || p.whoAmI < 0 || p.whoAmI > Main.maxProjectiles) return null;
			bool pet = p.type < Main.projPet.Length && Main.projPet[p.type];
			if (!p.minion && !p.sentry && !pet) return null;
			if (!p.TryGetGlobalProjectile(out SoldierProjectileTag t) || t.SoldierIndex < 0 || t.SoldierIndex >= Main.maxNPCs || t.HostileShot) return null;
			if (Counters.Contains(p.type)) return null;
			if (p.minion && !p.sentry && !pet && !t.GearHelper && p.minionSlots <= 0f && !SlotlessBodies.Contains(p.type)) return null; // a minion's segment or a shot its AI made: the body that holds the slot carries the tag
			NPC body = Main.npc[t.SoldierIndex];
			if (!body.active || body.ModNPC is not SoldierNPC sn || sn.Hostile || sn.Record == null || string.IsNullOrEmpty(sn.Record.Name)) return null;
			string name = sn.Record.Name;
			ref Slot s = ref _proj[p.whoAmI];
			if (s.K != Kind.Minion || s.Type != p.type || s.A != t.SoldierIndex || !ReferenceEquals(s.S, name)) s = new Slot { K = Kind.Minion, Type = p.type, A = t.SoldierIndex, S = name, Text = LevyUtil.T("AllyTags.Of", name, Lang.GetProjectileName(p.type).Value) };
			return s.Text;
		}

		/// <summary>The town NPC type of the live, friendly officer avatar at <paramref name="ownerWho"/>, or 0.</summary>
		private static int OfficerRole(int ownerWho)
		{
			if (ownerWho < 0 || ownerWho >= Main.maxNPCs) return 0;
			NPC o = Main.npc[ownerWho];
			return o.active && o.ModNPC is SoldierNPC { OfficerMode: true } s && !s.Hostile && s.OfficerTownType > 0 ? s.OfficerTownType : 0;
		}

		// ------------------------------------------------------------------ the frame

		/// <summary>Fills the frame's list with every tagged ally whose box meets <paramref name="view"/> (world coordinates). Returns the count. Allocates nothing once the texts are cached.</summary>
		internal static int Collect(Rectangle view)
		{
			_count = 0;
			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC n = Main.npc[i];
				if (!n.active || !n.Hitbox.Intersects(view)) continue;
				string text = For(n);
				if (text == null) continue;
				float lift = n.ModNPC is IMachineNameTag ? LevyConstants.UI.AllyTagLiftMachine : LevyConstants.UI.AllyTagLiftUnit;
				_entries[_count++] = new Entry { Top = n.Top + new Vector2(0f, n.gfxOffY), Box = n.Hitbox, Text = text, Lift = lift };
			}
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile p = Main.projectile[i];
				if (!p.active || (!p.minion && !p.sentry && !(p.type < Main.projPet.Length && Main.projPet[p.type]))) continue;
				if (p.alpha >= 255 || !p.Hitbox.Intersects(view)) continue; // fully faded: nothing to name
				string text = For(p);
				if (text == null) continue;
				_entries[_count++] = new Entry { Top = p.Top + new Vector2(0f, p.gfxOffY), Box = p.Hitbox, Text = text, Lift = LevyConstants.UI.AllyTagLiftMinion };
			}
			return _count;
		}

		/// <summary>The tags collected by the last <see cref="Collect"/> (self-check use).</summary>
		internal static int CollectedCount => _count;
		internal static Entry Collected(int i) => _entries[i];

		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
		{
			int at = layers.FindIndex(l => l.Name == "Vanilla: Entity Health Bars");
			if (at < 0) at = layers.FindIndex(l => l.Name == "Vanilla: Mouse Text");
			if (at < 0) return;
			layers.Insert(at + 1, new LegacyGameInterfaceLayer("Levy: Ally Tags", DrawLayer, InterfaceScaleType.Game));
		}

		private static bool DrawLayer()
		{
			if (Main.dedServ || Main.gameMenu || Main.mapFullscreen || !Enabled(out bool forced)) return true;
			var view = new Rectangle((int)Main.screenPosition.X - 32, (int)Main.screenPosition.Y - 32, Main.screenWidth + 64, Main.screenHeight + 64);
			int n = Collect(view);
			if (n == 0) return true;
			var sb = Main.spriteBatch;
			float scale = LevyConstants.UI.NameplateTextScale * LevyConstants.UI.NameplateWeaponScale;
			if (!forced && n > LevyConstants.NameplateHideAbove)
			{
				// a crowd: only the ally under the cursor (the nearest one when several overlap)
				Point m = Main.MouseWorld.ToPoint(); int best = -1; float bestD = float.MaxValue;
				for (int i = 0; i < n; i++)
				{
					var b = _entries[i].Box; b.Inflate(6, 6);
					if (!b.Contains(m)) continue;
					float d = Vector2.DistanceSquared(b.Center.ToVector2(), Main.MouseWorld);
					if (d < bestD) { bestD = d; best = i; }
				}
				if (best >= 0) DrawOne(sb, in _entries[best], scale);
				return true;
			}
			for (int i = 0; i < n; i++) DrawOne(sb, in _entries[i], scale);
			return true;
		}

		private static void DrawOne(Microsoft.Xna.Framework.Graphics.SpriteBatch sb, in Entry e, float scale)
		{
			Vector2 at = e.Top - Main.screenPosition - new Vector2(0f, e.Lift);
			Utils.DrawBorderString(sb, e.Text, at, TagColor, scale, 0.5f, 1f);
		}
	}
}
