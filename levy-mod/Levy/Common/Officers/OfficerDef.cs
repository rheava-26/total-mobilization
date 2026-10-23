using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;

namespace Levy.Common.Officers
{
	/// <summary>
	/// How a kit wants the shared soldier movement to place the avatar. FRONTLINE marches at the follow distance and fights
	/// what comes in reach (the fighter default). SUPPORT hangs behind the army near a wounded soldier and keeps clear of enemies.
	/// </summary>
	public enum OfficerPosture : byte { Frontline, Support }

	/// <summary>
	/// A ranged attack in the vanilla town-NPC shape (the same numbers NPC.AI_007_TownEntities hard-codes per type): one projectile, thrown at the
	/// target after <see cref="Delay"/> ticks of the <see cref="WindUp"/> animation, then a cooldown. Add melee / magic shapes here later.
	/// </summary>
	public sealed class OfficerWeapon
	{
		/// <summary>Projectile type spawned (a friendly one; it is flagged as a town-NPC projectile).</summary>
		public int Projectile;
		/// <summary>Launch speed (px/tick) and the arc lift (px) a far shot is aimed above the target to meet gravity.</summary>
		public float Speed, GravityCorrection;
		/// <summary>Damage and knockback at the start of the game; the officer scales damage with the world's progress like a vanilla town NPC.</summary>
		public int Damage; public float Knockback;
		/// <summary>Tick of the wind-up animation the projectile leaves on, the animation's length (NPCID.Sets.AttackTime of the NPC) and the pause after (fixed + random extra).</summary>
		public int Delay, WindUp, Cooldown, RandomCooldown;
		/// <summary>How far (tiles) a hostile may be and still be shot at.</summary>
		public float RangeTiles;
	}

	/// <summary>
	/// What makes an officer an officer: the registry entry for one town NPC type. To add the next officer, build one of these (a weapon and/or a
	/// <see cref="OfficerKit"/>) and call <see cref="OfficerRegistry.Register"/> from <see cref="OfficerRegistry.RegisterAll"/>.
	/// </summary>
	public sealed class OfficerDef
	{
		/// <summary>The town NPC type (NPCID.* or a modded type) this entry commissions.</summary>
		public int NpcType;
		/// <summary>Localization key stem under Mods.Levy.Officers.Kit: "{Key}.Name" is the role line, "{Key}.Desc" the 1-4 sentence kit description shown in the menu.</summary>
		public string Key;
		/// <summary>Wage and commission-fee multiplier on top of <see cref="LevyConstants.Officer.WageMult"/> (1 = standard). Bigger kits cost more.</summary>
		public float CostMult = 1f;
		/// <summary>Taunt weight for enemy targeting (see <see cref="LevyConstants.Officer.NurseAggro"/>).</summary>
		public int Aggro;
		/// <summary>The officer's own attack, or null for a pure support officer that never shoots.</summary>
		public OfficerWeapon Weapon;
		/// <summary>The special ability, run on the server / single player every tick while the officer is active. Null = fights only.</summary>
		public OfficerKit Kit;
		/// <summary>How the avatar positions itself in the line (see <see cref="OfficerPosture"/>). Fighters march up; support hangs back.</summary>
		public OfficerPosture Posture = OfficerPosture.Frontline;
		/// <summary>The on-the-move chatter trigger this officer fires while marching (a role voice; the generic default is <see cref="DialogueTrigger.OFFICER_MARCH"/>).</summary>
		public DialogueTrigger MarchTrigger = DialogueTrigger.OFFICER_MARCH;
	}

	/// <summary>
	/// An officer's kit: bonuses and abilities that run while he is mobilised. <see cref="Tick"/> is server / single player only (heal, buffs, call-downs);
	/// everything an ability changes must replicate through the normal NPC / projectile / tile-entity sync. State that has to survive between ticks lives on
	/// the <see cref="OfficerContext.State"/> object the kit creates in <see cref="CreateState"/>, never in statics (two commanders can field the same type in turn).
	/// </summary>
	public abstract class OfficerKit
	{
		/// <summary>Format arguments for the "Mods.Levy.Officers.Kit.{Key}.Desc" menu text, so the numbers shown are the live constants.</summary>
		public virtual object[] DescriptionArgs => Array.Empty<object>();
		/// <summary>A fresh per-officer state object (cooldown timers and the like), or null when the kit keeps none.</summary>
		public virtual object CreateState() => null;
		/// <summary>One server tick of the ability. Called after movement and the officer's own attack.</summary>
		public abstract void Tick(OfficerContext ctx);
		/// <summary>Called once when the officer is mobilised (server). Optional.</summary>
		public virtual void OnMobilized(OfficerContext ctx) { }
		/// <summary>Called once when the commission ends for any reason: dismissed, lapsed, killed (server). Optional.</summary>
		public virtual void OnDismissed(OfficerContext ctx) { }
		/// <summary>
		/// SUPPORT posture only: a world position the avatar should hover near (e.g. a wounded soldier it is tending), so the movement AI keeps it
		/// in reach of its work instead of glued to the commander. Return false for no preference. Server / single player.
		/// </summary>
		public virtual bool SupportAnchor(OfficerContext ctx, out Vector2 pos) { pos = default; return false; }
	}

	/// <summary>Everything a kit needs about the officer it runs for.</summary>
	public sealed class OfficerContext
	{
		public NPC Npc;
		public OfficerDef Def;
		public Player Commander;
		/// <summary>Per-officer scratch object created by <see cref="OfficerKit.CreateState"/>.</summary>
		public object State;
		/// <summary>The barracks that pays this officer (the one that commissioned him, else the nearest); null when none stands.</summary>
		public Content.TileEntities.BarracksTE Barracks;
		/// <summary>Command slot (0-based) the officer holds.</summary>
		public int Slot;
	}

	/// <summary>Which town NPC types can be commissioned, and as what. Fill it in <see cref="RegisterAll"/>.</summary>
	public static class OfficerRegistry
	{
		private static readonly Dictionary<int, OfficerDef> _byType = new();
		private static readonly List<OfficerDef> _all = new();

		/// <summary>Registered officers in registration order (the menu lists them in this order).</summary>
		public static IReadOnlyList<OfficerDef> All => _all;

		public static bool TryGet(int npcType, out OfficerDef def) => _byType.TryGetValue(npcType, out def);
		public static OfficerDef Get(int npcType) => _byType.TryGetValue(npcType, out var d) ? d : null;
		public static bool IsOfficerType(int npcType) => _byType.ContainsKey(npcType);

		public static void Register(OfficerDef def)
		{
			if (def == null) throw new ArgumentNullException(nameof(def));
			if (_byType.ContainsKey(def.NpcType)) throw new InvalidOperationException("Levy: officer type " + def.NpcType + " registered twice");
			_byType[def.NpcType] = def; _all.Add(def);
		}

		public static void Clear() { _byType.Clear(); _all.Clear(); }

		/// <summary>THE extension point: one line per officer. Called from OfficerSystem.Load (after content is set up for vanilla types; modded types may be looked up by name here too).</summary>
		public static void RegisterAll()
		{
			Register(NurseOfficer.Create());
			Register(GuideOfficer.Create());
			Register(AnglerOfficer.Create());
			Register(PirateOfficer.Create());           // summon officer: fields a flintlock boarding crew
			// Buff / debuff officers (rebuild)
			Register(PrincessOfficer.Create());         // army-wide morale aura (soldiers + officers)
			Register(DryadOfficer.Create());            // AoE shred mark + Thorns/Regeneration aura
			Register(GoblinTinkererOfficer.Create());   // gear-tune aura + thrown spiky balls
			Register(TaxCollectorOfficer.Create());     // AoE shred mark + coin bounty on a marked kill
			Register(DemolitionistOfficer.Create());    // frontline: lobbed grenades + a periodic heavy dynamite
			Register(ArmsDealerOfficer.Create());       // frontline: rapid rifle + an Archery aura
			Register(MerchantOfficer.Create());         // frontline: coin gun + a modest treasury trickle
			Register(PainterOfficer.Create());          // frontline: colorful paint barrage + an inspiration aura
		}
	}
}
