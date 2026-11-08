using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Globals;
using Levy.Common.Targeting;
using Levy.Common.Util;
using Levy.Content.NPCs;

namespace Levy.Common.Weapons
{
	/// <summary>What a projectile of a soldier's Ark is: its swung blade (or the thrown scissors), its parry stab, or a child bound to the blade (the Cosmos constellation).</summary>
	public enum ArkRole : byte { None = 0, Blade = 1, Parry = 2, Child = 3 }

	/// <summary>One Ark as the soldier sees it: the projectile classes its own Shoot hook spawns, the item's charge and combo fields, and its reach numbers. Built once per item type from the item's own code.</summary>
	internal sealed class ArkSpec
	{
		public string Name;
		public Type Swung, Parry, Blast, Constellation;
		public int SwungId, ParryId, BlastId, ConstellationId;
		public FieldInfo Charge, Combo;
		public LevyConstants.ArkNumbers N;
		public bool HasThrow => N.ThrowMax > 0;
	}

	/// <summary>
	/// CALAMITY'S ARKS (Fractured Ark, True Ark of the Ancients, Ark of the Elements, Ark of the Cosmos). The item's shot is a placeholder: its Shoot hook spawns a swung blade anchored to its owner's body (left button)
	/// or a parry holdout (right button). A parry that strikes an enemy empowers the Ark's next swings (the item's own <c>Charge</c>: bigger swings, thrown projectiles); the Elements and Cosmos also combo into a thrown pair
	/// of scissors that snaps shut when the button is let go, and the Cosmos spends its charge on the Big Rip. All of it is the real item code: the soldier
	///  * swings through the item's own Shoot hook (<see cref="HeldWeapons.ShootHooks"/>, dummy as the player), keeping the item's combo and charge on the soldier's own item;
	///  * parries by spawning the parry holdout the hook would (the hook refuses while ANY owner-255 parry is out, which would let one soldier's parry block every other soldier's);
	///  * anchors the blade and the parry to the soldier like every held chain (<see cref="HeldWeapons.RunAi"/>), holds the use button while the scissors fly and lets go in the snap window;
	///  * resolves damage under the real player (<see cref="HeldWeapons.RunDamage"/>), standing the real player at the soldier for the call because the blade's hit line starts at its owner's centre (<see cref="DamageScope"/>).
	/// A parry that connects runs Calamity's own on-hit code (<c>Owner.HeldItem.ModItem.Charge</c>), which reads the owner's held item: the scope hands the soldier's Ark to the real player's hand for that call, so the charge lands on
	/// the soldier's item, and puts back everything the parry's immunity window changed on the real player.
	/// </summary>
	internal static class ArkSwings
	{
		// ---------------------------------------------------------------------------------------------
		// Counters (the lab reads them from the log; see Report)
		// ---------------------------------------------------------------------------------------------
		public static int Swings, ChargedSwings, Parries, ParryHits, Throws, Snaps, Blasts, ChildShots, ChargedChildShots;
		private static uint _lastReport;
		internal static readonly bool ReportOn = Environment.GetEnvironmentVariable("LEVY_ARK_STATS") == "1";

		internal static void Reset() { Swings = ChargedSwings = Parries = ParryHits = Throws = Snaps = Blasts = ChildShots = ChargedChildShots = 0; _lastReport = 0; _live.Clear(); }

		private static uint _lastUse;
		/// <summary>Measurement seam (env LEVY_ARK_NOPARRY=1): the soldiers never parry or blast, so a lab can read the plain swing's damage against the parried one.</summary>
		/// <summary>Measurement seam (env LEVY_ARK_BLASTALL=1): the Cosmos throws its Big Rip at any range, so a lab can see the blast fire and what it does.</summary>
		internal static readonly bool BlastAll = Environment.GetEnvironmentVariable("LEVY_ARK_BLASTALL") == "1";
		internal static readonly bool NoParry = Environment.GetEnvironmentVariable("LEVY_ARK_NOPARRY") == "1"; // measurement seam: the soldiers never parry or blast

		/// <summary>Lab line: the counters of one fight (no Ark use for 300 ticks ends it) or, while it runs, a running total every 600 ticks.</summary>
		internal static void Report(bool force = false)
		{
			if (!ReportOn || (Swings + Parries == 0)) return;
			uint now = Main.GameUpdateCount;
			bool ended = now - _lastUse >= 300u;
			if (!force && !ended && now - _lastReport < 600u) return;
			_lastReport = now;
			ModContent.GetInstance<Levy>()?.Logger.Info($"{(ended || force ? "ARKS-FIGHT" : "ARKS")}: swings={Swings} chargedSwings={ChargedSwings} parries={Parries} parryHits={ParryHits} throws={Throws} snaps={Snaps} blasts={Blasts} childShots={ChildShots} chargedChildShots={ChargedChildShots}");
			if (ended || force) { Swings = ChargedSwings = Parries = ParryHits = Throws = Snaps = Blasts = ChildShots = ChargedChildShots = 0; }
		}

		// ---------------------------------------------------------------------------------------------
		// Recognition
		// ---------------------------------------------------------------------------------------------
		private const string SwungSuffix = "SwungBlade", ParrySuffix = "ParryHoldout";
		private static readonly Dictionary<int, ArkSpec> _specs = new();

		/// <summary>True when the item's own hooks name a swung blade and the matching parry holdout, and the item carries a float <c>Charge</c> the parry fills.</summary>
		internal static bool IsArk(Item item) => Build(item) != null;

		/// <summary>True when the item is classified as an Ark (the cached shot kind).</summary>
		internal static bool IsArkItem(Item item) => item != null && ModShotKinds.Of(item) == ModShotKind.ArkSwing;

		internal static ArkSpec SpecOf(Item item) => item == null || item.IsAir ? null : Build(item);

		private static ArkSpec Build(Item item)
		{
			if (item == null || item.IsAir || item.ModItem == null || item.type < ItemID.Count) return null;
			if (_specs.TryGetValue(item.type, out ArkSpec cached)) return cached;
			ArkSpec spec = null;
			try
			{
				List<Type> named = OwnerLockProbe.NamedProjectileTypes(item);
				Type swung = named.FirstOrDefault(t => t.Name.EndsWith(SwungSuffix, StringComparison.Ordinal));
				Type parry = swung == null ? null : named.FirstOrDefault(t => t.Name == swung.Name.Substring(0, swung.Name.Length - SwungSuffix.Length) + ParrySuffix);
				FieldInfo charge = item.ModItem.GetType().GetField("Charge", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (swung != null && parry != null && charge != null && charge.FieldType == typeof(float))
				{
					string stem = swung.Name.Substring(0, swung.Name.Length - SwungSuffix.Length);
					spec = new ArkSpec
					{
						Name = item.ModItem.Name,
						Swung = swung, Parry = parry,
						Blast = swung.Assembly.GetType(swung.Namespace + "." + stem + "Blast"),
						Constellation = swung.Assembly.GetType(swung.Namespace + "." + stem + "Constellation"),
						Charge = charge,
						Combo = item.ModItem.GetType().GetField("Combo", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
					};
					spec.N = LevyConstants.ArkTable.TryGetValue(swung.Name, out var n) ? n : LevyConstants.ArkDefault;
					spec.SwungId = IdOf(swung); spec.ParryId = IdOf(parry);
					spec.BlastId = spec.Blast == null ? 0 : IdOf(spec.Blast); spec.ConstellationId = spec.Constellation == null ? 0 : IdOf(spec.Constellation);
					if (spec.SwungId <= 0 || spec.ParryId <= 0) spec = null;
				}
			}
			catch (Exception e) { ModContent.GetInstance<Levy>()?.Logger.Warn($"Levy: Ark probe failed for {item.Name}: {e.Message}"); spec = null; }
			_specs[item.type] = spec;
			return spec;
		}

		private static int IdOf(Type t)
		{
			foreach (ModProjectile mp in ModContent.GetContent<ModProjectile>())
				if (mp.GetType() == t) return mp.Type;
			return 0;
		}

		internal static void Unload() { _specs.Clear(); _live.Clear(); }

		/// <summary>Tiles a swing starts at (the profile's reach).</summary>
		internal static int BlowTiles(Item item) => (int)Math.Round((SpecOf(item)?.N ?? LevyConstants.ArkDefault).BlowTiles);

		/// <summary>Sustained single-target damage per second of one soldier swinging this Ark, parries included, before the soldier's own damage scale: the listed damage at its use rate, times the lab-measured share the parry adds.</summary>
		internal static float SustainedDps(Item item)
		{
			ArkSpec sp = SpecOf(item);
			float mult = sp != null && LevyConstants.ArkSustainedMult.TryGetValue(sp.Swung.Name, out float m) ? m : 1.5f;
			return Math.Max(0, item.damage) * 60f / Math.Max(1, item.useTime) * mult;
		}

		// ---------------------------------------------------------------------------------------------
		// The item's charge and combo
		// ---------------------------------------------------------------------------------------------
		internal static float ChargeOf(ArkSpec sp, Item item) { try { return item.ModItem != null && sp.Charge.DeclaringType.IsInstanceOfType(item.ModItem) ? (float)sp.Charge.GetValue(item.ModItem) : 0f; } catch (Exception) { return 0f; } }

		// ---------------------------------------------------------------------------------------------
		// Live projectiles of a soldier
		// ---------------------------------------------------------------------------------------------
		private static readonly Dictionary<int, int[]> _live = new(); // soldier -> projectile slot per role: blade, parry, blast

		private static Projectile LiveOf(SoldierNPC s, int slotKind, int type)
		{
			if (type <= 0 || !_live.TryGetValue(s.NPC.whoAmI, out int[] a)) return null;
			int pi = a[slotKind];
			if (pi < 0 || pi >= Main.maxProjectiles) return null;
			Projectile p = Main.projectile[pi];
			if (p.active && p.type == type && p.TryGetGlobalProjectile(out SoldierProjectileTag t) && t.SoldierIndex == s.NPC.whoAmI && p.TryGetGlobalProjectile(out HeldWeaponProjectile g) && ReferenceEquals(g.Record, s.Record)) return p;
			a[slotKind] = -1;
			return null;
		}

		private static void Remember(SoldierNPC s, int slotKind, Projectile p)
		{
			if (!_live.TryGetValue(s.NPC.whoAmI, out int[] a)) _live[s.NPC.whoAmI] = a = new[] { -1, -1, -1 };
			a[slotKind] = p.whoAmI;
		}

		// ---------------------------------------------------------------------------------------------
		// Combat: one use of the Ark
		// ---------------------------------------------------------------------------------------------
		/// <summary>Called by WeaponUse every tick the soldier is ready to use the Ark and has a target (reach is decided here).</summary>
		internal static bool TryFire(SoldierNPC s, Item item, WeaponProfile profile, NPC target)
		{
			ArkSpec sp = SpecOf(item);
			if (sp == null || s.Dummy == null) return WeaponUse.Dud(s, item);
			// Calamity's CanUseItem: no swing while the blade is out (the item's own test counts every owner-255 blade, so it is asked per soldier here)
			if (LiveOf(s, 0, sp.SwungId) != null) { s.AttackCooldown = 1; return false; }

			float dist = Aiming.TilesToHitbox(s.NPC.Center, target);
			Vector2 toTarget = target.Center - s.NPC.Center;
			if (toTarget == Vector2.Zero) toTarget = Vector2.UnitX * (s.NPC.direction == 0 ? 1 : s.NPC.direction);
			Vector2 dir = Vector2.Normalize(toTarget);
			float charge = ChargeOf(sp, item);
			int damage = Math.Max(1, (int)Math.Round(s.WeaponDamage(item) * s.DamageMultiplier));
			Vector2 vel = dir * Math.Max(1f, item.shootSpeed);
			bool parryOut = LiveOf(s, 1, sp.ParryId) != null;

			// the Big Rip: spent from out of the blade's reach on something worth it (a boss, a big target)
			bool tough = target.boss || target.lifeMax >= LevyConstants.ArkBlastToughLife;
			if (!NoParry && sp.BlastId > 0 && charge >= LevyConstants.ArkBlastMinCharge && tough && (BlastAll || dist > sp.N.BlowTiles) && dist <= sp.N.BlastTiles && LiveOf(s, 2, sp.BlastId) == null)
				return Use(s, item, sp, dir, vel, damage, alt: true, parry: false);
			// the parry: when the Ark has no charge left and no parry is out, with an enemy inside the stab
			if (!NoParry && charge <= 0f && !parryOut && dist <= sp.N.ParryTiles)
				return Use(s, item, sp, dir, vel, damage, alt: true, parry: true);
			if (dist <= sp.N.BlowTiles + 1f)
				return Use(s, item, sp, dir, vel, damage, alt: false, parry: false);
			return false;
		}

		private static bool Use(SoldierNPC s, Item item, ArkSpec sp, Vector2 dir, Vector2 vel, int damage, bool alt, bool parry)
		{
			_lastUse = Main.GameUpdateCount;
			float chargeBefore = ChargeOf(sp, item);
			Action<IEntitySource, Vector2> custom = null;
			if (parry)
				custom = (src, v) => Projectile.NewProjectile(src, s.Dummy.Center, v, sp.ParryId, damage, 0f, SoldierNPC.DummySlot, 0f, 0f);
			List<Projectile> made = HeldWeapons.ShootHooks(s, item, vel, damage, SoldierNPC.DummySlot, alt, custom, out bool threw);
			int blades = 0, children = 0;
			foreach (Projectile c in made)
			{
				if (c.type == sp.SwungId) { Anchor(s, item, sp, c, ArkRole.Blade); Remember(s, 0, c); blades++; if (c.ai[0] == 2f) Throws++; }
				else if (c.type == sp.ParryId) { Anchor(s, item, sp, c, ArkRole.Parry); Remember(s, 1, c); }
				else
				{
					if (sp.BlastId > 0 && c.type == sp.BlastId) { Remember(s, 2, c); Blasts++; }
					else { children++; ChildShots++; if (chargeBefore > 0f) ChargedChildShots++; }
					WeaponUse.TagSoldierProjectile(s, c, item.DamageType ?? DamageClass.Default);
				}
			}
			if (made.Count == 0) return WeaponUse.Dud(s, item);
			if (parry) Parries++;
			else if (blades > 0) { Swings++; if (chargeBefore > 0f) ChargedSwings++; }
			Report();
			WeaponUse.FinishUse(s, item, dir);
			if (parry && sp.BlastId > 0) s.AttackCooldown = Math.Max(1, (int)(Math.Max(1, item.useTime) / LevyConstants.ArkFastParryDivisor)); // the Cosmos parry is cast five times as fast
			return true;
		}

		private static void Anchor(SoldierNPC s, Item item, ArkSpec sp, Projectile p, ArkRole role)
		{
			HeldWeapons.JoinChain(s, p, item);
			if (!p.TryGetGlobalProjectile(out HeldWeaponProjectile g)) return;
			g.ModHolder = true; g.ReleaseOnKill = false; g.CycleRelease = false;
			g.Ark = role; g.ArkSpec = sp; g.ParryHit = false; g.ParryContact = false; g.HeldTicks = 0;
			p.direction = p.velocity.X < 0f ? -1 : 1;
		}

		/// <summary>A projectile the blade's AI made this tick: the Cosmos constellation stays bound to the blade (it reads its owner's centre and button), anything else is an ordinary soldier shot. True when handled here.</summary>
		internal static bool AdoptChild(SoldierNPC body, Projectile holder, Projectile c, Item item)
		{
			if (!holder.TryGetGlobalProjectile(out HeldWeaponProjectile hg) || hg.Ark != ArkRole.Blade || hg.ArkSpec == null) return false;
			if (hg.ArkSpec.ConstellationId > 0 && c.type == hg.ArkSpec.ConstellationId)
			{
				HeldWeapons.JoinChain(body, c, item);
				if (c.TryGetGlobalProjectile(out HeldWeaponProjectile cg)) { cg.ModHolder = true; cg.BoundChild = true; cg.Ark = ArkRole.Child; cg.ArkSpec = hg.ArkSpec; }
				return true;
			}
			if (c.type == hg.ArkSpec.ParryId) return false;
			ChildShots++;
			if (ChargeOf(hg.ArkSpec, item) > 0f || holder.ai[1] > 0f) ChargedChildShots++; // the blade's own Charge (ai[1]) is what it was thrown with
			return false;
		}

		/// <summary>
		/// Whether the use button is down for this Ark projectile. Only the thrown scissors (blade state 2 of the Elements and the Cosmos) read it: held while they fly out, let go once the snap window has opened so the snap lands inside it.
		/// Everything else is a swing or a stab and has no use for the button.
		/// </summary>
		internal static bool WantsButton(Projectile p, HeldWeaponProjectile g)
		{
			ArkSpec sp = g.ArkSpec;
			if (sp == null || !sp.HasThrow || g.Ark != ArkRole.Blade && g.Ark != ArkRole.Child) return false;
			if (g.Ark == ArkRole.Child) return true; // the constellation lasts while the scissors are held; its own lifetime ends it
			if (p.ai[0] == 3f && !g.SnapCounted) { g.SnapCounted = true; Snaps++; } // the scissors snapped shut (Calamity moves the blade to state 3)
			if (p.ai[0] != 2f) return false;
			float done = (sp.N.ThrowMax - p.timeLeft) / (float)sp.N.ThrowMax;
			return done < sp.N.SnapStart + LevyConstants.ArkSnapMargin;
		}

		// ---------------------------------------------------------------------------------------------
		// Damage: the real player stands where the soldier is
		// ---------------------------------------------------------------------------------------------
		/// <summary>
		/// Around one Projectile.Damage call of an Ark projectile (the damage resolves under the real player, see HeldWeapons.RunDamage). The blade's hit line starts at its owner's centre and the owner is the real player for the call, so
		/// the real player is stood at the soldier; a parry's on-hit reads the owner's HELD ITEM (it fills the Ark's Charge), so the soldier's Ark is put in the real player's hand. Both, and every field the parry's immunity window
		/// touches, are put back exactly. The soldier gets the window instead (against an enemy that can hurt by touch).
		/// </summary>
		internal struct DamageScope : IDisposable
		{
			private readonly Player _real; private readonly SoldierNPC _body; private readonly Projectile _p; private readonly HeldWeaponProjectile _g;
			private readonly Vector2 _pos; private readonly int _sel; private readonly Item _held;
			private readonly bool _immune, _noBlink; private readonly int _immuneTime; private readonly int[] _hurt;
			private readonly bool _parry, _on; private readonly float _ai1;

			public DamageScope(SoldierNPC body, Projectile p, HeldWeaponProjectile g)
			{
				_real = null; _body = body; _p = p; _g = g; _pos = default; _sel = 0; _held = null; _immune = _noBlink = false; _immuneTime = 0; _hurt = null; _parry = false; _on = false; _ai1 = 0f;
				int who = SoldierMinions.TrueMyPlayer;
				if (who < 0 || who >= Main.maxPlayers + 1 || Main.player[who] == null) return;
				_real = Main.player[who];
				_on = true;
				_parry = g.Ark == ArkRole.Parry;
				_ai1 = p.ai[1];
				_pos = _real.position;
				_real.Center = body.NPC.Center;
				if (_parry)
				{
					_immune = _real.immune; _noBlink = _real.immuneNoBlink; _immuneTime = _real.immuneTime; _hurt = (int[])_real.hurtCooldowns.Clone();
					_sel = _real.selectedItem;
					Item soldierItem = WeaponUse.HeldItem(body);
					if (_sel >= 0 && _sel < _real.inventory.Length && soldierItem != null && !soldierItem.IsAir)
					{
						_held = _real.inventory[_sel];
						_real.inventory[_sel] = soldierItem;
					}
				}
			}

			public void Dispose()
			{
				if (!_on) return;
				try
				{
					_real.position = _pos;
					if (_parry)
					{
						if (_held != null || (_sel >= 0 && _sel < _real.inventory.Length)) { if (_held != null) _real.inventory[_sel] = _held; }
						_real.immune = _immune; _real.immuneNoBlink = _noBlink; _real.immuneTime = _immuneTime;
						if (_hurt != null) Array.Copy(_hurt, _real.hurtCooldowns, Math.Min(_hurt.Length, _real.hurtCooldowns.Length));
						if (_g.ParryHit && _p.ai[1] > 0f && _ai1 <= 0f)
						{
							ParryHits++;
							// the parry's immunity window goes to the soldier, against an enemy that hurts by touch
							if (_g.ParryContact && _body.Dummy != null)
							{
								int frames = LevyConstants.ArkParryIFrames + (_body.Dummy.longInvince ? LevyConstants.ArkParryIFramesCross : 0);
								_body.ImmuneTime = Math.Max(_body.ImmuneTime, frames);
							}
						}
					}
				}
				catch (Exception) { }
			}
		}
	}

	/// <summary>Keeps the Ark counters out of stale worlds and writes the lab line (LEVY_ARK_STATS=1) a few times a minute.</summary>
	public class ArkSwingsSystem : ModSystem
	{
		public override void OnWorldLoad() => ArkSwings.Reset();
		public override void OnWorldUnload() { ArkSwings.Report(true); ArkSwings.Reset(); }
		public override void Unload() => ArkSwings.Unload();
		public override void PostUpdateEverything() => ArkSwings.Report();

		/// <summary>Boot self-check: every Ark the loaded mods have classifies as a supported held melee weapon with its blade and parry resolved. With LEVY_ARK_STATS=1 it also logs the ranking numbers (the lab reads them).</summary>
		public override void PostAddRecipes()
		{
			try
			{
				int arks = 0, bad = 0;
				for (int t = ItemID.Count; t < ItemLoader.ItemCount; t++)
				{
					Item it = ContentSamples.ItemsByType.TryGetValue(t, out var smp) ? smp : null;
					if (it == null || it.IsAir || it.ModItem == null || !ArkSwings.IsArkItem(it)) continue;
					arks++;
					WeaponProfile p = WeaponClassifier.GetProfile(it);
					ArkSpec sp = ArkSwings.SpecOf(it);
					bool ok = p.Family == WeaponFamily.HeldWeapon && p.HeldMelee && p.Supported && sp != null && sp.SwungId > 0 && sp.ParryId > 0 && p.RangeTiles >= 5;
					if (!ok) bad++;
					if (ArkSwings.ReportOn || !ok) Mod.Logger.Info($"ARKCHECK {it.ModItem.Mod.Name}/{it.ModItem.Name} family={p.Family} heldMelee={p.HeldMelee} reach={p.RangeTiles} swung={sp?.Swung.Name} parry={sp?.Parry.Name} blast={sp?.Blast?.Name} score={WeaponClassifier.Score(it):0.0} rank={WeaponRank.Of(it):0.0} {(ok ? "ok" : "FAIL")}");
				}
				if (ArkSwings.ReportOn)
					foreach (int id in new[] { ItemID.Excalibur, ItemID.TerraBlade, ItemID.Zenith, ItemID.NightsEdge, ItemID.TrueExcalibur, ItemID.TrueNightsEdge, ItemID.Muramasa, ItemID.BladeofGrass, ItemID.Meowmere })
					{
						var it = new Item(); it.SetDefaults(id);
						Mod.Logger.Info($"ARKCHECK vanilla/{it.Name} score={WeaponClassifier.Score(it):0.0} rank={WeaponRank.Of(it):0.0} dmg={it.damage} use={it.useTime}");
					}
				if (arks > 0) Mod.Logger.Info($"[ArkSwings] self-check arks={arks} fail={bad}");
			}
			catch (Exception e) { Mod.Logger.Warn("[ArkSwings] self-check threw: " + e.Message); }
		}
	}
}
