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
using Levy.Common.Soldiers;
using Levy.Common.Targeting;
using Levy.Common.Util;
using Levy.Content.NPCs;

namespace Levy.Common.Weapons
{
	/// <summary>The biome flags an attunement is chosen from (the fields of the player Calamity's right-click channel reads).</summary>
	internal struct BiomeZones
	{
		public bool Jungle, Snow, Sky, Corrupt, Crimson, Desert, Hell, Hallow, Astral;
		public override string ToString() => string.Join("+", new[] { Jungle ? "jungle" : null, Snow ? "snow" : null, Sky ? "sky" : null, Corrupt ? "corrupt" : null, Crimson ? "crimson" : null, Desert ? "desert" : null, Hell ? "hell" : null, Hallow ? "hallow" : null, Astral ? "astral" : null }.Where(x => x != null).DefaultIfEmpty("none"));
	}

	/// <summary>One Biome Blade item as the soldier sees it: the fields and methods of its own code, found once per item type.</summary>
	internal sealed class BiomeSpec
	{
		public string Name, Kind; public int ItemType;
		public Type ItemT, AttT, IdEnum;
		public FieldInfo Main, Secondary, UseTimer, OnHitProc, MeatHook, IdF, RegistryF;
		public MethodInfo Apply, Passive;
		public PropertyInfo DmgMult;
		public string[] Names;
		public Array Registry => (Array)RegistryF?.GetValue(null);
	}

	/// <summary>
	/// CALAMITY'S BIOME BLADES (Broken Biome Blade, Biome Blade, Omega Biome Blade, Four Seasons Galaxia). Each carries one or two ATTUNEMENTS (<c>mainAttunement</c> / <c>secondaryAttunement</c> on the item); the attunement decides
	/// the item's shot (<c>ApplyStats</c> writes shoot / channel / use style onto the item) and its Shoot hook spawns that attunement's real blade projectile, anchored to its owner. A player picks the attunement by right-clicking
	/// on arrival: a 120-tick channel whose holdout reads the player's biome flags (<c>BiomeBladeHoldout.Attune</c> and its three siblings, copied by <see cref="Choose"/>), keeping the previous attunement as the secondary
	/// (Omega's secondary is a passive that fires on its hits). The soldier does the same from his dummy's zone flags (the biome the commander stands in, <see cref="BiomeMirror"/>), whenever he has no blade out:
	///  * attunes by the item's own rule and fields, applies the attunement's stats to the item, and uses the item's own Shoot hook with the dummy as the player (<see cref="HeldWeapons.ShootHooks"/>), so the attunement's combo and
	///    lunge fields live on the soldier's own item;
	///  * drives the blade by the attunement's shape (<see cref="LevyConstants.BiomePlans"/>): a swing that ends on its own is never held; a spin / shred / flail is held while the target is in reach; a charge is held and let go
	///    (the throw, the lunge) once it does something;
	///  * anchors the blade to the soldier like every held chain (<see cref="HeldWeapons.RunAi"/>), keeps the dummy's item animation running for the blades that read it, and binds the few children that read their owner's button or hand
	///    (Aries constellation, the meat hook, the Sanguine and Polaris wheels) to the chain; every other child is an ordinary soldier shot;
	///  * resolves damage under the real player standing at the soldier with the soldier's blade in the real player's hand (<see cref="DamageScope"/>), so on-hit procs land on the soldier's item and lifesteal is the soldier's own;
	///  * runs Omega's secondary passive (the item's own <c>PassiveEffect</c>) from the soldier.
	/// A move a blade gives its owner (the lunges, the pogo bounce, the dash) is the soldier's: see <see cref="Impulse"/>.
	/// </summary>
	internal static class BiomeBlades
	{
		// ---------------------------------------------------------------------------------------------
		// Counters (the lab reads them from the log; see Report)
		// ---------------------------------------------------------------------------------------------
		public static int Dashes, Attunes, Uses, Releases, ChildShots, Passives, BoundChildren;
		public static readonly Dictionary<string, int> UsesBy = new();
		public static readonly Dictionary<string, int> AttunesTo = new();
		private static uint _lastReport, _lastUse;
		internal static readonly bool ReportOn = Environment.GetEnvironmentVariable("LEVY_BB_STATS") == "1";
		/// <summary>Measurement seam (env LEVY_BB_FORCE=Name): every biome blade that can hold this attunement holds it, whatever the biome, so a lab can read one attunement at a time.</summary>
		internal static string ForcedName; // lab token bb=
		internal static string Forced => ForcedName ?? Environment.GetEnvironmentVariable("LEVY_BB_FORCE");

		internal static void Reset() { Dashes = Attunes = Uses = Releases = ChildShots = Passives = BoundChildren = 0; UsesBy.Clear(); AttunesTo.Clear(); _lastReport = 0; _st.Clear(); }

		internal static void Report(bool force = false)
		{
			if (!ReportOn || Uses == 0) return;
			uint now = Main.GameUpdateCount;
			bool ended = now - _lastUse >= 300u;
			if (!force && !ended && now - _lastReport < 600u) return;
			_lastReport = now;
			string by = string.Join(",", UsesBy.OrderBy(k => k.Key).Select(k => k.Key + ":" + k.Value));
			string to = string.Join(",", AttunesTo.OrderBy(k => k.Key).Select(k => k.Key + ":" + k.Value));
			ModContent.GetInstance<Levy>()?.Logger.Info($"{(ended || force ? "BB-FIGHT" : "BB")}: uses={Uses} attunes={Attunes} releases={Releases} childShots={ChildShots} boundChildren={BoundChildren} passives={Passives} dashes={Dashes} by=[{by}] attunedTo=[{to}]");
			if (ended || force) Reset();
		}

		// ---------------------------------------------------------------------------------------------
		// Recognition: an item whose own code carries a main attunement (and the Attunement machinery)
		// ---------------------------------------------------------------------------------------------
		private static readonly Dictionary<int, BiomeSpec> _specs = new();
		private static readonly Dictionary<string, string> KindOf = new() { ["BrokenBiomeBlade"] = "broken", ["TrueBiomeBlade"] = "biome", ["OmegaBiomeBlade"] = "omega", ["FourSeasonsGalaxia"] = "galaxia" };
		private static readonly Dictionary<string, string[]> NamesOf = new()
		{
			["broken"] = new[] { "Default", "Hot", "Cold", "Evil" },
			["biome"] = new[] { "TrueDefault", "TrueHot", "TrueCold", "TrueTropical", "TrueEvil", "Holy" },
			["omega"] = new[] { "Whirlwind", "FlailBlade", "SuperPogo", "Shockwave" },
			["galaxia"] = new[] { "Phoenix", "Aries", "Polaris", "Andromeda" },
		};
		private const BindingFlags AnyInst = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

		internal static bool IsBiomeBlade(Item item) => SpecOf(item) != null;
		/// <summary>True when the item is classified as a Biome Blade (the cached shot kind).</summary>
		internal static bool IsBiomeItem(Item item) => item != null && ModShotKinds.Of(item) == ModShotKind.BiomeBlade;

		internal static BiomeSpec SpecOf(Item item)
		{
			if (item == null || item.IsAir || item.ModItem == null || item.type < ItemID.Count) return null;
			if (_specs.TryGetValue(item.type, out BiomeSpec cached)) return cached;
			BiomeSpec sp = null;
			try
			{
				Type t = item.ModItem.GetType();
				if (item.ModItem.Mod.Name == "CalamityMod" && KindOf.TryGetValue(t.Name, out string kind))
				{
					FieldInfo main = t.GetField("mainAttunement", AnyInst);
					if (main != null && main.FieldType.Name == "Attunement")
					{
						Type att = main.FieldType;
						Type sys = att.Assembly.GetType("CalamityMod.Systems.AttunementSystem");
						Type idEnum = att.Assembly.GetType("CalamityMod.DataStructures.AttunementID");
						var s = new BiomeSpec
						{
							Name = t.Name, Kind = kind, ItemType = item.type, ItemT = t, AttT = att, IdEnum = idEnum, Main = main,
							Secondary = t.GetField("secondaryAttunement", AnyInst), UseTimer = t.GetField("UseTimer", AnyInst), OnHitProc = t.GetField("OnHitProc", AnyInst), MeatHook = t.GetField("MeatHook", AnyInst),
							IdF = att.GetField("id", AnyInst), RegistryF = sys?.GetField("Registry", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
							Apply = att.GetMethod("ApplyStats", AnyInst), Passive = att.GetMethod("PassiveEffect", AnyInst), DmgMult = att.GetProperty("DamageMultiplier", AnyInst),
							Names = NamesOf[kind],
						};
						if (s.IdF != null && s.RegistryF != null && s.Apply != null && s.DmgMult != null && s.IdEnum != null) sp = s;
					}
				}
			}
			catch (Exception e) { ModContent.GetInstance<Levy>()?.Logger.Warn($"Levy: Biome Blade probe failed for {item.Name}: {e.Message}"); sp = null; }
			_specs[item.type] = sp;
			return sp;
		}

		internal static void Unload() { _specs.Clear(); _st.Clear(); _astralProp = null; _astralTried = false; }

		/// <summary>Tiles the brain walks a soldier up to for this blade (what an attunement can do from there is the plan's business).</summary>
		internal static int ItemReach(Item item)
		{
			switch (SpecOf(item)?.Kind)
			{
				case "broken": return LevyConstants.BiomeReachBroken;
				case "biome": return LevyConstants.BiomeReachBiome;
				case "omega": return LevyConstants.BiomeReachOmega;
				default: return LevyConstants.BiomeReachGalaxia;
			}
		}

		// ---------------------------------------------------------------------------------------------
		// Attunement objects (by the AttunementID member name)
		// ---------------------------------------------------------------------------------------------
		private static object AttOf(BiomeSpec sp, string name)
		{
			try { Array reg = sp.Registry; if (reg == null || !Enum.IsDefined(sp.IdEnum, name)) return null; int idx = Convert.ToInt32(Enum.Parse(sp.IdEnum, name)); return idx >= 0 && idx < reg.Length ? reg.GetValue(idx) : null; }
			catch (Exception) { return null; }
		}

		private static string NameOf(BiomeSpec sp, object att)
		{
			if (att == null) return null;
			try { return Enum.GetName(sp.IdEnum, sp.IdF.GetValue(att)); } catch (Exception) { return null; }
		}

		internal static string MainName(BiomeSpec sp, Item item) => item?.ModItem == null ? null : NameOf(sp, sp.Main.GetValue(item.ModItem));
		internal static string SecondaryName(BiomeSpec sp, Item item) => item?.ModItem == null || sp.Secondary == null ? null : NameOf(sp, sp.Secondary.GetValue(item.ModItem));

		/// <summary>The attunement's damage multiplier over the item's listed damage (its own DamageMultiplier).</summary>
		internal static float DamageMultOf(BiomeSpec sp, string name)
		{
			try { object a = AttOf(sp, name); return a == null ? 1f : Convert.ToSingle(sp.DmgMult.GetValue(a)); } catch (Exception) { return 1f; }
		}

		internal static LevyConstants.BiomePlan PlanOf(string name) => name != null && LevyConstants.BiomePlans.TryGetValue(name, out var p) ? p : LevyConstants.BiomePlanDefault;

		// ---------------------------------------------------------------------------------------------
		// Which attunement: the item's own rule, from the biome flags
		// ---------------------------------------------------------------------------------------------
		private static bool _astralTried; private static PropertyInfo _astralProp;
		internal static BiomeZones ZonesOf(Player d)
		{
			var z = new BiomeZones { Jungle = d.ZoneJungle, Snow = d.ZoneSnow, Sky = d.ZoneSkyHeight, Corrupt = d.ZoneCorrupt, Crimson = d.ZoneCrimson, Desert = d.ZoneDesert, Hell = d.ZoneUnderworldHeight, Hallow = d.ZoneHallow };
			try
			{
				object cp = AltPolicies.CalamityPlayerOf(d);
				if (cp != null)
				{
					if (!_astralTried) { _astralTried = true; _astralProp = cp.GetType().GetProperty("ZoneAstral", AnyInst); }
					if (_astralProp != null) z.Astral = (bool)_astralProp.GetValue(cp);
				}
			}
			catch (Exception) { }
			return z;
		}

		/// <summary>
		/// The attunement an item of this family takes in this biome. Broken / Biome / Omega copy the item's own holdout (BrokenBiomeBladeHoldout / BiomeBladeHoldout / TrueBiomeBladeHoldout .Attune, later checks override earlier ones).
		/// The Galaxia has no biome rule (its right button cycles Phoenix, Aries, Polaris, Andromeda by hand): a soldier takes the one that suits the biome's character: fire, the cold north, the heavens, otherwise the ram.
		/// </summary>
		internal static string Choose(string kind, in BiomeZones z)
		{
			bool cold = z.Snow || z.Sky, evil = z.Corrupt || z.Crimson, hot = z.Desert || z.Hell, holy = z.Hallow || z.Astral;
			switch (kind)
			{
				case "broken":
					{ string a = "Default"; if (hot) a = "Hot"; if (cold) a = "Cold"; if (evil) a = "Evil"; return a; }
				case "biome":
					{ string a = "TrueDefault"; if (hot) a = "TrueHot"; if (cold) a = "TrueCold"; if (z.Jungle) a = "TrueTropical"; if (evil) a = "TrueEvil"; if (holy) a = "Holy"; return a; }
				case "omega":
					{ string a = "Shockwave"; if (z.Jungle || z.Snow || z.Sky) a = "FlailBlade"; if (hot || evil) a = "SuperPogo"; if (holy) a = "Whirlwind"; return a; }
				default:
					{ string a = "Aries"; if (hot) a = "Phoenix"; if (cold) a = "Polaris"; if (z.Jungle || evil) a = "Aries"; if (holy) a = "Andromeda"; return a; }
			}
		}

		/// <summary>Attunes the item to the soldier's biome the way the holdout does (the previous main becomes the secondary on a blade that has one) and writes the attunement's stats onto the item. True when the attunement changed.</summary>
		private static bool EnsureAttuned(SoldierNPC s, Item item, BiomeSpec sp, out string att)
		{
			object mod = item.ModItem;
			string want = Forced != null && sp.Names.Contains(Forced) ? Forced : Choose(sp.Kind, ZonesOf(s.Dummy));
			string cur = NameOf(sp, sp.Main.GetValue(mod));
			bool changed = cur != want;
			if (changed)
			{
				object wantObj = AttOf(sp, want);
				if (wantObj == null) { att = cur; return false; }
				if (sp.Secondary != null) { object curObj = sp.Main.GetValue(mod); sp.Secondary.SetValue(mod, curObj); } // the previous attunement stays as the secondary (null the first time)
				sp.Main.SetValue(mod, wantObj);
				Attunes++; AttunesTo[want] = AttunesTo.TryGetValue(want, out int n) ? n + 1 : 1;
			}
			object main = sp.Main.GetValue(mod);
			try { sp.Apply.Invoke(main, new object[] { item }); } catch (Exception) { }
			att = want;
			return changed;
		}

		// ---------------------------------------------------------------------------------------------
		// Per-soldier state
		// ---------------------------------------------------------------------------------------------
		internal sealed class St { public SoldierRecord Rec; public int Root = -1; public uint Start, AnimTick, DashUntil; public Vector2 PreVel, Dash; public string Att; public bool Button, Released; public int Hook = -1; }
		private static readonly Dictionary<int, St> _st = new();

		internal static St StOf(SoldierNPC s, bool create = true)
		{
			int idx = s.NPC.whoAmI;
			if (_st.TryGetValue(idx, out St st) && ReferenceEquals(st.Rec, s.Record)) return st;
			if (!create) return null;
			return _st[idx] = new St { Rec = s.Record };
		}

		private static bool IsBlade(Projectile p, SoldierNPC s, out HeldWeaponProjectile g)
		{
			g = null;
			return p.active && p.TryGetGlobalProjectile(out SoldierProjectileTag t) && t.SoldierIndex == s.NPC.whoAmI && p.TryGetGlobalProjectile(out g) && g.Biome == 1 && ReferenceEquals(g.Record, s.Record);
		}

		private static Projectile LiveRoot(SoldierNPC s, St st)
		{
			int pi = st.Root;
			if (pi < 0 || pi >= Main.maxProjectiles) return null;
			Projectile p = Main.projectile[pi];
			if (IsBlade(p, s, out _)) return p;
			st.Root = -1;
			return null;
		}

		private static NPC TargetOf(SoldierNPC s)
		{
			int ti = s.TargetIndex;
			if (ti < 0 || ti >= Main.maxNPCs) return null;
			NPC t = Main.npc[ti];
			return t.active && t.life > 0 ? t : null;
		}

		// ---------------------------------------------------------------------------------------------
		// Combat: one use of the blade
		// ---------------------------------------------------------------------------------------------
		/// <summary>Called by HeldWeapons.TryFire every tick the soldier is ready to use the blade and has a target.</summary>
		internal static bool TryFire(SoldierNPC s, Item item, WeaponProfile profile, NPC target)
		{
			BiomeSpec sp = SpecOf(item);
			if (sp == null || s.Dummy == null) return WeaponUse.Dud(s, item);
			St st = StOf(s);
			// Calamity's CanUseItem: no second use while the blade is out (the item's own test counts every owner-255 blade, so it is asked per soldier here)
			if (LiveRoot(s, st) != null) { s.AttackCooldown = 1; return false; }

			bool changed = EnsureAttuned(s, item, sp, out string att);
			var plan = PlanOf(att);
			if (changed) { s.AttackCooldown = LevyConstants.BiomeAttuneTicks; s.NPC.netUpdate = true; return false; } // the channel: a moment to take the new attunement
			float dist = Aiming.TilesToHitbox(s.NPC.Center, target);
			if (dist > Math.Max(profile.RangeTiles, plan.ReachTiles) + 1f) return false;

			Vector2 toTarget = target.Center - s.NPC.Center;
			if (toTarget == Vector2.Zero) toTarget = Vector2.UnitX * (s.NPC.direction == 0 ? 1 : s.NPC.direction);
			Vector2 dir = Vector2.Normalize(toTarget);
			int damage = Math.Max(1, (int)Math.Round(s.WeaponDamage(item) * s.DamageMultiplier));
			Vector2 vel = dir * Math.Max(1f, item.shootSpeed);
			List<Projectile> made = HeldWeapons.ShootHooks(s, item, vel, damage, SoldierNPC.DummySlot, false, null, out bool threw);
			Projectile root = null;
			foreach (Projectile c in made)
			{
				if (root == null && c.owner == SoldierNPC.DummySlot && c.type == item.shoot) { root = c; continue; }
				ChildShots++;
				WeaponUse.TagSoldierProjectile(s, c, item.DamageType ?? DamageClass.Default);
			}
			if (root == null) return WeaponUse.Dud(s, item);

			Anchor(s, item, root, 1);
			st.Root = root.whoAmI; st.Start = Main.GameUpdateCount; st.Att = att; st.Released = false; st.Button = plan.Mode != LevyConstants.BiomeMode.Self;
			Uses++; UsesBy[att] = UsesBy.TryGetValue(att, out int u) ? u + 1 : 1; _lastUse = Main.GameUpdateCount;
			Report();
			WeaponUse.FinishUse(s, item, dir);
			if (plan.Mode != LevyConstants.BiomeMode.Self) s.AttackCooldown = 1;
			return true;
		}

		/// <summary>Binds a projectile to the soldier's chain: the blade (role 1) or a child that reads its owner's button / hand (role 2).</summary>
		private static void Anchor(SoldierNPC s, Item item, Projectile p, byte role)
		{
			HeldWeapons.JoinChain(s, p, item);
			// Calamity gives its swings a STATIC immunity (per projectile type across every owner): every soldier's blade of that type would share one hit cooldown on a target. Each blade keeps its own, as a player's does.
			if (p.usesIDStaticNPCImmunity) { p.usesIDStaticNPCImmunity = false; p.usesLocalNPCImmunity = true; p.localNPCHitCooldown = Math.Max(1, p.idStaticNPCHitCooldown); }
			if (!p.TryGetGlobalProjectile(out HeldWeaponProjectile g)) return;
			g.ModHolder = true; g.ReleaseOnKill = false; g.CycleRelease = false; g.Biome = role; g.HeldTicks = 0;
			p.direction = p.velocity.X < 0f ? -1 : 1;
		}

		/// <summary>Children a blade's AI made: the ones that read their owner's button or hand stay bound to the chain; every other child is an ordinary soldier shot (false).</summary>
		private static readonly HashSet<string> Bound = new() { "AriesWrathConstellation", "ChainedMeatHook", "SanguineFuryWheel", "PolarisGazeStar" };

		internal static bool AdoptChild(SoldierNPC body, Projectile holder, Projectile c, Item item)
		{
			if (!holder.TryGetGlobalProjectile(out HeldWeaponProjectile hg) || hg.Biome == 0) return false;
			if (c.ModProjectile != null && Bound.Contains(c.ModProjectile.Name))
			{
				Anchor(body, item, c, 2);
				BoundChildren++;
				return true;
			}
			ChildShots++;
			return false;
		}

		// ---------------------------------------------------------------------------------------------
		// The button
		// ---------------------------------------------------------------------------------------------
		/// <summary>Whether the use button is down for this projectile: a swing never; a spin while the target is in reach; a charge until it has done what it is for.</summary>
		internal static bool WantsButton(SoldierNPC body, Projectile p, HeldWeaponProjectile g)
		{
			St st = StOf(body, false);
			if (st == null) return false;
			if (g.Biome != 1) return st.Button; // a bound child follows the blade's button
			if (st.Released) return false;
			var plan = PlanOf(st.Att);
			if (plan.Mode == LevyConstants.BiomeMode.Self) { st.Button = false; return false; }
			NPC t = TargetOf(body);
			bool want = false;
			if (t != null)
			{
				float dist = Aiming.TilesToHitbox(body.NPC.Center, t);
				bool inReach = dist <= plan.ReachTiles * LevyConstants.BiomeHoldReachMult;
				if (plan.Mode == LevyConstants.BiomeMode.Loop) want = inReach;
				else
				{
					float frac = plan.ChargeMax > 0f ? p.ai[plan.ChargeSlot] / plan.ChargeMax : 1f;
					bool big = t.boss || t.lifeMax >= LevyConstants.BiomeFullChargeLife;
					float need = big ? plan.IdealFrac : Math.Min(plan.IdealFrac, plan.MinFrac + 0.04f);
					want = inReach && frac < need;
				}
			}
			if (!want) { st.Released = true; Releases++; }
			st.Button = want;
			return want;
		}

		// ---------------------------------------------------------------------------------------------
		// The AI pass: the dummy's item animation, for the blades that read it
		// ---------------------------------------------------------------------------------------------
		/// <summary>Before a biome projectile's AI (dummy swapped in): a held use restarts the dummy's item animation when it has run out, as Player.ItemCheck would (the Pure Clarity swings run on it); the soldier keeps his pose.</summary>
		internal static void PreAi(SoldierNPC body, Projectile p, HeldWeaponProjectile g, bool button)
		{
			Player d = body.Dummy;
			St st0 = StOf(body, false);
			if (st0 != null) st0.PreVel = d.velocity;
			if (button && d.itemAnimation <= 0) d.itemAnimation = Math.Max(1, d.itemAnimationMax);
			if (button && body.ItemAnimation < LevyConstants.HeldPoseHoldTicks) body.ItemAnimation = body.ItemAnimationMax = LevyConstants.HeldPoseHoldTicks;
			if (g.Biome == 1) body.AttackCooldown = Math.Max(body.AttackCooldown, 1);
		}

		/// <summary>After the AI pass: one tick of the dummy's item animation runs down (once per tick however many extra updates the projectile has).</summary>
		internal static void PostAi(SoldierNPC body, Projectile p, HeldWeaponProjectile g)
		{
			Player d = body.Dummy;
			St st = StOf(body, false);
			uint now = Main.GameUpdateCount;
			if (st != null && st.AnimTick != now) { st.AnimTick = now; if (d.itemAnimation > 0) d.itemAnimation--; }
			if (st != null && Vector2.DistanceSquared(d.velocity, st.PreVel) > 1f) Impulse(body, st, d.velocity); // the blade moved its owner: a lunge, a dash, a pogo bounce
			if (!p.active && g.Biome == 1) d.itemAnimation = 0;
		}

		/// <summary>A blade moved its owner (<c>Owner.velocity = ...</c>: the lunge, the dash to the wheel, the charge's dash, the pogo and rapier bounces). The dummy is re-synced from the body every tick, so the move is the body's: held on him for this tick's motion (<see cref="BiomeDashNpc"/>).</summary>
		internal static void Impulse(SoldierNPC body, St st, Vector2 v)
		{
			if (v.HasNaNs()) return;
			st.Dash = new Vector2(MathHelper.Clamp(v.X, -LevyConstants.BiomeDashMaxSpeed, LevyConstants.BiomeDashMaxSpeed), MathHelper.Clamp(v.Y, -LevyConstants.BiomeDashMaxSpeed, LevyConstants.BiomeDashMaxSpeed));
			st.DashUntil = Main.GameUpdateCount + 1;
			Dashes++;
		}

		internal static bool TakeDash(SoldierNPC body, out Vector2 v)
		{
			v = default;
			St st = StOf(body, false);
			if (st == null || Main.GameUpdateCount > st.DashUntil) return false;
			v = st.Dash;
			return true;
		}

		// ---------------------------------------------------------------------------------------------
		// Damage: the real player stands where the soldier is, with the soldier's blade in hand
		// ---------------------------------------------------------------------------------------------
		/// <summary>
		/// Around one Projectile.Damage call of a biome projectile (the damage resolves under the real player, see HeldWeapons.RunDamage). A blade's hit line starts at its owner's centre and the owner is the real player for the call,
		/// so the real player is stood at the soldier; the on-hit code reads the owner's HELD ITEM (Omega's procs set <c>OnHitProc</c> on it, the Biome Blade's lunge counter, the evil lunge's bounce reads the item's fields), so the
		/// soldier's blade is put in the real player's hand; what the hit does to its owner (a bounce, an immunity window, lifesteal) is the soldier's, never the commander's: velocity, immunity and life are put back and the life gained is
		/// the soldier's. Children spawned inside the call are the soldier's (<see cref="OwnerPuppet.Context"/>).
		/// </summary>
		internal struct DamageScope : IDisposable
		{
			private readonly Player _real; private readonly SoldierNPC _body; private readonly bool _on;
			private readonly Vector2 _pos, _vel; private readonly int _dir, _sel, _life, _lifeMax, _fall, _immuneTime; private readonly float _steal;
			private readonly Item _held; private readonly Vector2 _start; private readonly bool _immune, _noBlink; private readonly int[] _hurt; private readonly SoldierNPC _ctx;

			public DamageScope(SoldierNPC body, Projectile p, HeldWeaponProjectile g)
			{
				_real = null; _body = body; _on = false; _pos = _vel = default; _dir = _sel = _life = _lifeMax = _fall = _immuneTime = 0; _steal = 0f; _held = null; _start = default; _immune = _noBlink = false; _hurt = null; _ctx = null;
				int who = SoldierMinions.TrueMyPlayer;
				if (who < 0 || who >= Main.maxPlayers || Main.player[who] == null || !Main.player[who].active) return;
				_real = Main.player[who];
				_on = true;
				_pos = _real.position; _vel = _real.velocity; _dir = _real.direction; _life = _real.statLife; _lifeMax = _real.statLifeMax2; _fall = _real.fallStart; _steal = _real.lifeSteal;
				_immune = _real.immune; _noBlink = _real.immuneNoBlink; _immuneTime = _real.immuneTime; _hurt = (int[])_real.hurtCooldowns.Clone();
				_sel = _real.selectedItem; _ctx = OwnerPuppet.Context;
				_real.Center = body.NPC.Center; _real.velocity = body.NPC.velocity; _start = _real.velocity; _real.direction = body.NPC.direction == 0 ? 1 : body.NPC.direction;
				_real.statLifeMax2 = Math.Max(1, body.NPC.lifeMax); _real.statLife = Math.Min(Math.Max(1, body.NPC.life), _real.statLifeMax2);
				_real.lifeSteal = LifeStealPool.Current(body);
				Item soldierItem = WeaponUse.HeldItem(body);
				if (_sel >= 0 && _sel < _real.inventory.Length && soldierItem != null && !soldierItem.IsAir) { _held = _real.inventory[_sel]; _real.inventory[_sel] = soldierItem; }
				OwnerPuppet.Context = body;
			}

			public void Dispose()
			{
				if (!_on) return;
				try
				{
					OwnerPuppet.Context = _ctx;
					int gained = _real.statLife - Math.Min(Math.Max(1, _body.NPC.life), Math.Max(1, _body.NPC.lifeMax));
					LifeStealPool.Store(_body, _real.lifeSteal);
					if (Vector2.DistanceSquared(_real.velocity, _start) > 1f) { St st = StOf(_body, false); if (st != null) Impulse(_body, st, _real.velocity); } // a hit bounced its owner (the evil lunge's rebound, the pogo)
					_real.position = _pos; _real.velocity = _vel; _real.direction = _dir; _real.statLife = _life; _real.statLifeMax2 = _lifeMax; _real.fallStart = _fall; _real.lifeSteal = _steal;
					_real.immune = _immune; _real.immuneNoBlink = _noBlink; _real.immuneTime = _immuneTime;
					if (_hurt != null) Array.Copy(_hurt, _real.hurtCooldowns, Math.Min(_hurt.Length, _real.hurtCooldowns.Length));
					if (_held != null && _sel >= 0 && _sel < _real.inventory.Length) _real.inventory[_sel] = _held;
					if (gained > 0 && _body.NPC.active && _body.NPC.life > 0 && _body.NPC.life < _body.NPC.lifeMax && Main.netMode != NetmodeID.MultiplayerClient) global::Levy.Common.Compat.Thorium.SoldierHeal.Restore(_body.NPC, gained);
				}
				catch (Exception) { }
			}
		}

		// ---------------------------------------------------------------------------------------------
		// Omega's secondary passive: the item's own PassiveEffect, from the soldier
		// ---------------------------------------------------------------------------------------------
		/// <summary>One tick of the item's own HoldItem passive section for a soldier whose Omega blade holds a secondary attunement: the use timer runs while a blade is out; a hit that proc'd, the shockwave timer and a missing meat hook call the secondary's PassiveEffect.</summary>
		private static void RunPassive(SoldierNPC s, St st)
		{
			Item item = WeaponUse.HeldItem(s);
			BiomeSpec sp = SpecOf(item);
			if (sp == null || sp.Passive == null || sp.Secondary == null || sp.UseTimer == null || sp.OnHitProc == null || item?.ModItem == null || s.Dummy == null) return;
			object sec = sp.Secondary.GetValue(item.ModItem);
			string secName = NameOf(sp, sec);
			if (sec == null) return;
			bool rootAlive = LiveRoot(s, st) != null;
			object mod = item.ModItem;
			if (rootAlive) sp.UseTimer.SetValue(mod, (int)sp.UseTimer.GetValue(mod) + 1);
			int timer = (int)sp.UseTimer.GetValue(mod);
			bool proc = (bool)sp.OnHitProc.GetValue(mod);
			Projectile hook = sp.MeatHook?.GetValue(mod) as Projectile;
			bool hookAlive = hook != null && hook.active && hook.TryGetGlobalProjectile(out HeldWeaponProjectile hg) && hg.Chain;
			bool needHook = secName == "FlailBlade" && !hookAlive && (rootAlive || proc);
			bool timed = secName == "Shockwave" && timer % 120 == 119;
			if (!proc && !timed && !needHook) return;
			if (!hookAlive && sp.MeatHook != null) sp.MeatHook.SetValue(mod, null);
			Player d = s.Dummy;
			int lifeBefore = d.statLife; float stealBefore = d.lifeSteal;
			d.lifeSteal = LifeStealPool.Current(s);
			var made = HeldWeapons.ShootHooks(s, item, Vector2.Zero, 0, SoldierNPC.DummySlot, false, (src, v) =>
			{
				if (needHook)
				{
					Type hookT = sp.ItemT.Assembly.GetType("CalamityMod.Projectiles.Melee.ChainedMeatHook");
					int hookType = hookT == null ? 0 : ModContent.GetContent<ModProjectile>().FirstOrDefault(m => m.GetType() == hookT)?.Type ?? 0;
					FieldInfo dmgF = sp.ItemT.GetField("FlailBladeAttunement_PassiveBaseDamage", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
					int hdmg = dmgF == null ? 100 : Convert.ToInt32(dmgF.GetValue(null));
					if (hookType > 0)
					{
						Projectile h = Projectile.NewProjectileDirect(src, d.Center, Vector2.Zero, hookType, hdmg, 0f, SoldierNPC.DummySlot);
						h.originalDamage = hdmg;
						sp.MeatHook.SetValue(mod, h);
					}
				}
				object hookNow = sp.MeatHook?.GetValue(mod);
				if (secName == "FlailBlade" && hookNow == null) return; // the secondary reads its hook: no hook, nothing to twirl
				object[] args = { d, src, timer, proc, hookNow };
				try { sp.Passive.Invoke(sec, args); } catch (Exception) { }
				sp.UseTimer.SetValue(mod, (int)args[2]);
				sp.OnHitProc.SetValue(mod, (bool)args[3]);
			}, out _);
			int gained = d.statLife - lifeBefore;
			d.statLife = lifeBefore; LifeStealPool.Store(s, d.lifeSteal); d.lifeSteal = stealBefore;
			if (gained > 0 && s.NPC.life < s.NPC.lifeMax && Main.netMode != NetmodeID.MultiplayerClient) global::Levy.Common.Compat.Thorium.SoldierHeal.Restore(s.NPC, gained);
			Passives++;
			foreach (Projectile c in made)
			{
				if (c.ModProjectile != null && Bound.Contains(c.ModProjectile.Name)) { Anchor(s, item, c, 2); BoundChildren++; st.Hook = c.whoAmI; }
				else { ChildShots++; WeaponUse.TagSoldierProjectile(s, c, item.DamageType ?? DamageClass.Default); }
			}
		}

		internal static void Tick()
		{
			if (_st.Count == 0) return;
			foreach (var kv in _st.ToArray())
			{
				int idx = kv.Key;
				if (idx < 0 || idx >= Main.maxNPCs) { _st.Remove(idx); continue; }
				NPC n = Main.npc[idx];
				if (!n.active || n.ModNPC is not SoldierNPC s || s.Record == null || !ReferenceEquals(s.Record, kv.Value.Rec) || !s.IsAlive) { _st.Remove(idx); continue; }
				try { RunPassive(s, kv.Value); } catch (Exception e) { ModContent.GetInstance<Levy>()?.Logger.Warn("Levy: biome blade passive failed: " + e.Message); }
			}
		}

		// ---------------------------------------------------------------------------------------------
		// Ranking
		// ---------------------------------------------------------------------------------------------
		/// <summary>Sustained single-target damage per second of one soldier using this blade, before the soldier's own damage scale: the listed damage x each attunement's multiplier at the use rate x the lab-measured share, averaged over the attunements the blade can hold (a soldier takes the one his biome gives).</summary>
		internal static float SustainedDps(Item item)
		{
			BiomeSpec sp = SpecOf(item);
			float rate = 60f / Math.Max(1, item.useTime);
			if (sp == null) return Math.Max(0, item.damage) * rate;
			float sum = 0f;
			foreach (string name in sp.Names)
			{
				float mult = DamageMultOf(sp, name);
				float lab = LevyConstants.BiomeSustainedMult.TryGetValue(name, out float m) ? m : 1f;
				sum += Math.Max(0, item.damage) * mult * rate * lab;
			}
			return sum / sp.Names.Length;
		}
	}

	/// <summary>Keeps the Biome Blade counters out of stale worlds, ticks the passives, and writes the lab line (LEVY_BB_STATS=1) a few times a minute.</summary>
	public class BiomeBladesSystem : ModSystem
	{
		public override void OnWorldLoad() => BiomeBlades.Reset();
		public override void OnWorldUnload() { BiomeBlades.Report(true); BiomeBlades.Reset(); }
		public override void Unload() => BiomeBlades.Unload();
		public override void PostUpdateEverything() { BiomeBlades.Tick(); BiomeBlades.Report(); }

		/// <summary>Boot self-check: the biome rule gives each flag set the attunement the item's own holdout gives it, and every Biome Blade the loaded mods have resolves with all its attunements, each with its real blade projectile and a plan.</summary>
		public override void PostAddRecipes()
		{
			try
			{
				int fail = 0, items = 0, cases = 0;
				// the rule, flag set by flag set (expected = the holdout's own order of overrides, read from BiomeBladeHoldout / TrueBiomeBladeHoldout / BrokenBiomeBladeHoldout)
				var rows = new (string kind, BiomeZones z, string want)[]
				{
					("broken", new BiomeZones(), "Default"), ("broken", new BiomeZones { Desert = true }, "Hot"), ("broken", new BiomeZones { Hell = true }, "Hot"), ("broken", new BiomeZones { Snow = true }, "Cold"), ("broken", new BiomeZones { Sky = true }, "Cold"),
					("broken", new BiomeZones { Corrupt = true }, "Evil"), ("broken", new BiomeZones { Crimson = true, Desert = true }, "Evil"), ("broken", new BiomeZones { Snow = true, Desert = true }, "Cold"), ("broken", new BiomeZones { Jungle = true }, "Default"), ("broken", new BiomeZones { Hallow = true }, "Default"),
					("biome", new BiomeZones(), "TrueDefault"), ("biome", new BiomeZones { Desert = true }, "TrueHot"), ("biome", new BiomeZones { Hell = true }, "TrueHot"), ("biome", new BiomeZones { Snow = true }, "TrueCold"), ("biome", new BiomeZones { Jungle = true }, "TrueTropical"),
					("biome", new BiomeZones { Jungle = true, Snow = true }, "TrueTropical"), ("biome", new BiomeZones { Corrupt = true, Jungle = true }, "TrueEvil"), ("biome", new BiomeZones { Hallow = true, Corrupt = true }, "Holy"), ("biome", new BiomeZones { Astral = true }, "Holy"),
					("omega", new BiomeZones(), "Shockwave"), ("omega", new BiomeZones { Jungle = true }, "FlailBlade"), ("omega", new BiomeZones { Snow = true }, "FlailBlade"), ("omega", new BiomeZones { Sky = true }, "FlailBlade"), ("omega", new BiomeZones { Desert = true }, "SuperPogo"),
					("omega", new BiomeZones { Hell = true }, "SuperPogo"), ("omega", new BiomeZones { Crimson = true, Snow = true }, "SuperPogo"), ("omega", new BiomeZones { Hallow = true, Desert = true }, "Whirlwind"), ("omega", new BiomeZones { Astral = true }, "Whirlwind"),
					("galaxia", new BiomeZones(), "Aries"), ("galaxia", new BiomeZones { Hell = true }, "Phoenix"), ("galaxia", new BiomeZones { Snow = true }, "Polaris"), ("galaxia", new BiomeZones { Hallow = true }, "Andromeda"), ("galaxia", new BiomeZones { Jungle = true }, "Aries"),
				};
				foreach (var r in rows)
				{
					cases++;
					string got = BiomeBlades.Choose(r.kind, r.z);
					if (got != r.want) { fail++; Mod.Logger.Info($"BBCHECK rule {r.kind} {r.z} -> {got} (want {r.want}) FAIL"); }
				}
				for (int t = ItemID.Count; t < ItemLoader.ItemCount; t++)
				{
					Item it = ContentSamples.ItemsByType.TryGetValue(t, out var smp) ? smp : null;
					BiomeSpec sp = it == null || it.IsAir ? null : BiomeBlades.SpecOf(it);
					if (sp == null) continue;
					items++;
					WeaponProfile p = WeaponClassifier.GetProfile(it);
					bool ok = p.Family == WeaponFamily.HeldWeapon && p.HeldMelee && p.Supported && p.RangeTiles >= 5;
					var probe = new Item(); probe.SetDefaults(t);
					foreach (string name in sp.Names)
					{
						object a = null; try { Array reg = sp.Registry; a = reg?.GetValue(Convert.ToInt32(Enum.Parse(sp.IdEnum, name))); } catch (Exception) { }
						bool good = a != null && LevyConstants.BiomePlans.ContainsKey(name);
						int shoot = 0;
						if (good)
						{
							// the attunement's own stats make the item shoot its blade: a real ModProjectile
							try { sp.Apply.Invoke(a, new object[] { probe }); shoot = probe.shoot; } catch (Exception) { }
							good = shoot > 0 && shoot >= ProjectileID.Count && ProjectileLoader.GetProjectile(shoot) != null;
						}
						if (!good) { ok = false; Mod.Logger.Info($"BBCHECK {sp.Name}/{name} attunement={(a != null)} shoot={shoot} FAIL"); }
						else if (BiomeBlades.ReportOn) Mod.Logger.Info($"BBCHECK {sp.Name}/{name} blade={ProjectileLoader.GetProjectile(shoot).GetType().Name} dmgMult={BiomeBlades.DamageMultOf(sp, name):0.00} plan={BiomeBlades.PlanOf(name).Mode} reach={BiomeBlades.PlanOf(name).ReachTiles}");
					}
					if (!ok) fail++;
					if (BiomeBlades.ReportOn || !ok) Mod.Logger.Info($"BBCHECK {sp.Name} family={p.Family} heldMelee={p.HeldMelee} reach={p.RangeTiles} attunements={sp.Names.Length} score={WeaponClassifier.Score(it):0.0} rank={WeaponRank.Of(it):0.0} {(ok ? "ok" : "FAIL")}");
				}
				if (ModLoader.HasMod("CalamityMod") && items < 4) { fail++; Mod.Logger.Info($"BBCHECK only {items} of the 4 Biome Blades resolved FAIL"); }
				Mod.Logger.Info($"[BiomeBlades] self-check blades={items} rules={cases} fail={fail}");
			}
			catch (Exception e) { Mod.Logger.Warn("[BiomeBlades] self-check threw: " + e.Message); }
		}
	}

	/// <summary>Holds a soldier's Biome Blade move (a lunge, a dash, a bounce) on his body for the tick: set right after his own AI, before the tick's motion, so his walking does not overwrite it.</summary>
	public class BiomeDashNpc : GlobalNPC
	{
		public override void PostAI(NPC npc)
		{
			if (npc.ModNPC is not SoldierNPC s || !LevyUtil.IsServerOrSingle) return;
			if (BiomeBlades.TakeDash(s, out Vector2 v)) { npc.velocity = v; npc.netUpdate = true; }
		}
	}
}
