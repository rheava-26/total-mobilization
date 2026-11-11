using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Globals;
using Levy.Common.Util;

namespace Levy.Common.Weapons
{
	/// <summary>
	/// THE PROJECTILE/NPC PAIR LOOP. Terraria's Projectile.Damage walks every NPC slot for every damaging projectile, every tick, and for each live enemy asks the mods (CombinedHooks.CanHitNPCWithProj: every
	/// GlobalProjectile's CanHitNPC, every GlobalNPC's CanBeHitByProjectile) before it tests the hitboxes. An army of 36 levies with their minions keeps 200 to 300 projectiles in the air and the Destroyer is 80 NPCs
	/// long: 35 000 pairs a tick, a third of the whole tick, of which about nine are hits. Nearly all of them are a shot on one side of the arena asked about a segment on the other.
	///
	/// An IL edit of Projectile.Damage (<see cref="PairCullSystem"/>) puts one test at the top of that NPC loop, right after vanilla's own "is the slot alive" check and before anything else (the immunity flags, the
	/// mod hooks, the netOffset shift, the hitbox test): for an ARMY shot whose hit test is provably the plain overlap of its damage hitbox with the NPC's rectangle (<see cref="Classify"/>), an NPC whose rectangle is more than
	/// a margin away from that hitbox (<see cref="Check"/>) is skipped with the same "continue" vanilla uses for a dead slot. Nothing the skipped pair would have done can happen: a hook can only veto the pair or force it
	/// into the hitbox test (which still decides), and the hitbox test is false; the loop has no side effect before the hooks. The test reads the very rectangle vanilla tests (the local that holds Damage_GetHitbox's result,
	/// which already includes every mod's ModifyDamageHitbox), so only the collision SHAPE needs to be known: <see cref="Classify"/> refuses (margin 0, never culled) a projectile with a vanilla type or AI style whose
	/// Colliding is not the rectangle overlap (the complete list read from Projectile.Colliding / Damage_GetHitbox: lasers, beams, whips, spears, flails, cones, trails...), a modded projectile that overrides
	/// Colliding, and any projectile a loaded mod's GlobalProjectile.Colliding applies to. Hostile projectiles and non-army projectiles are never touched.
	///
	/// LEVY_PAIRCULL=0 turns it off (A/B). LEVY_PAIRCHK=1 (lab) culls nothing and instead asks the real hit test of EVERY classified pair, vanilla's own Colliding(hitbox, npc rectangle), and counts every pair the cull
	/// would have skipped that nevertheless collides (<see cref="Violations"/>, must stay 0) and how far beyond the plain rectangle any hit ever landed (<see cref="MaxHitExcessPx"/>, 0 expected).
	/// </summary>
	public static class PairCull
	{
		/// <summary>On unless LEVY_PAIRCULL=0 (or LEVY_OPT=0). PAIRCHK=1 forces the hook on so it can audit.</summary>
		public static readonly bool Requested = LevyProf.Opt && (Environment.GetEnvironmentVariable("LEVY_PAIRCULL") != "0" || Environment.GetEnvironmentVariable("LEVY_PAIRCHK") == "1");
		internal static readonly bool Audit = Environment.GetEnvironmentVariable("LEVY_PAIRCHK") == "1";
		/// <summary>True once the Projectile.Damage edit is in (the cull is live, or auditing).</summary>
		public static bool Enabled;
		/// <summary>Slack beyond the damage hitbox, px. A plain hit test reaches exactly the hitbox; the slack covers integer truncation of the rectangles and the 8 px the game adds around one NPC type. (The NPC's own netOffset is added exactly, per pair.)</summary>
		public const float MarginPx = 32f;

		/// <summary>Vanilla projectile TYPES whose Colliding / Damage_GetHitbox is not the plain overlap of the damage hitbox (read from the game's code: every type named in Projectile.Colliding and Damage_GetHitbox).</summary>
		private static readonly int[] SpecialTypes = { 85, 101, 188, 455, 461, 464, 466, 537, 580, 598, 607, 611, 614, 623, 632, 636, 642, 661, 684, 686, 687, 697, 699, 707, 711, 756, 758, 802, 842, 871, 872, 877, 878, 879, 919, 923, 927, 932, 933, 938, 939, 940, 941, 942, 943, 944, 945, 961, 963, 967, 973, 974, 985 };
		/// <summary>AI styles whose Colliding is special in the game's code (flail spin, cone, extension hitbox, jellyfish-style).</summary>
		private static readonly int[] SpecialStyles = { 15, 19, 137, 190 };

		/// <summary>Lab counters.</summary>
		public static long Audited, Violations, Eligible, Ineligible, Checked; public static float MaxHitExcessPx;
		/// <summary>Lab: why army projectile types were left alone (type -> reason), for the profile dump.</summary>
		public static readonly Dictionary<int, string> WhyNot = new();
		public static readonly Dictionary<int, int> InelPairs = new();
		private static List<GlobalProjectile> _collidingGlobals;
		private static readonly Dictionary<Type, bool> _overrides = new();

		/// <summary>Called once per Projectile.Damage (installed by <see cref="PairCullSystem"/>), after the damage hitbox <paramref name="val"/> is known. True for an army shot whose hit test is the plain rectangle overlap: then the four
		/// bounds are val's rectangle grown by <see cref="MarginPx"/>, and <see cref="Far"/> may skip NPCs entirely outside them.</summary>
		public static bool Begin(Projectile p, Rectangle val, out float loX, out float hiX, out float loY, out float hiY)
		{
			loX = hiX = loY = hiY = 0f;
			if (!p.friendly || p.hostile || p.damage <= 0) return false;
			if (!p.TryGetGlobalProjectile(out SoldierProjectileTag t)) return false;
			if (t.SoldierIndex < 0 && t.CommanderIndex < 0 && !t.DroneShot) return false;
			float m = t.CullMargin(p);
			if (m == 0f)
			{
				if (LevyProf.On) { InelPairs.TryGetValue(p.type, out int n); InelPairs[p.type] = n + 1; }
				return false;
			}
			loX = val.X - m; hiX = val.X + val.Width + m; loY = val.Y - m; hiY = val.Y + val.Height + m;
			return true;
		}

		/// <summary>Decides, for one projectile (cached on its tag until its type or AI style changes), whether its hit test is the plain rectangle overlap. Returns the margin to add to its hitbox, or 0.</summary>
		internal static float Classify(Projectile p)
		{
			string why = null;
			int t = p.type;
			if (t <= 0) why = "type";
			else if (Array.IndexOf(SpecialStyles, p.aiStyle) >= 0) why = "aiStyle" + p.aiStyle;
			else if (ProjectileID.Sets.IsAWhip[t]) why = "whip";
			else if (t < ProjectileID.Count && Array.IndexOf(SpecialTypes, t) >= 0) why = "vanilla shape";
			else if (t >= ProjectileID.Count && (p.ModProjectile == null || Overrides(p.ModProjectile.GetType(), "Colliding"))) why = "ModProjectile.Colliding";
			else if (GlobalCollides(p)) why = "GlobalProjectile.Colliding";
			if (why != null)
			{
				Ineligible++;
				if (LevyProf.On) WhyNot[t] = why;
				return 0f;
			}
			Eligible++;
			return MarginPx;
		}

		/// <summary>True when the NPC's rectangle (with its netOffset, which vanilla adds to the position before it tests the hitboxes) lies entirely outside the grown hitbox: the pair cannot collide and is skipped before any hook is dispatched.</summary>
		[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
		public static bool Far(NPC n, float loX, float hiX, float loY, float hiY)
		{
			float l = n.position.X + n.netOffset.X, t = n.position.Y + n.netOffset.Y;
			if (l + n.width < loX | l > hiX | t + n.height < loY | t > hiY)
			{
				if (LevyProf.On) LevyProf.PairsCulled++;
				return true;
			}
			return false;
		}

		/// <summary>Audit stand-in for <see cref="Far"/> (LEVY_PAIRCHK=1): culls nothing; asks vanilla's real hit test of the pair and counts the pairs <see cref="Far"/> would have skipped that collide anyway.</summary>
		public static bool AuditPair(Projectile p, Rectangle val, NPC n, float loX, float hiX, float loY, float hiY)
		{
			Checked++;
			bool far = Far0(n, loX, hiX, loY, hiY);
			bool hit = false;
			try { hit = p.Colliding(val, new Rectangle((int)(n.position.X + n.netOffset.X), (int)(n.position.Y + n.netOffset.Y), n.width, n.height)); } catch (Exception) { }
			if (hit)
			{
				float l = n.position.X + n.netOffset.X, t = n.position.Y + n.netOffset.Y;
				float gap = Math.Max(Math.Max(l - (val.X + val.Width), val.X - (l + n.width)), Math.Max(t - (val.Y + val.Height), val.Y - (t + n.height)));
				if (gap > MaxHitExcessPx) MaxHitExcessPx = gap;
			}
			if (far) { Audited++; if (hit) Violations++; }
			return false;
		}
		private static bool Far0(NPC n, float loX, float hiX, float loY, float hiY)
		{
			float l = n.position.X + n.netOffset.X, t = n.position.Y + n.netOffset.Y;
			return l + n.width < loX | l > hiX | t + n.height < loY | t > hiY;
		}

		/// <summary>Does <paramref name="type"/> (or a base class below ModProjectile / GlobalProjectile) declare its own <paramref name="method"/>?</summary>
		private static bool Overrides(Type type, string method)
		{
			if (_overrides.TryGetValue(type, out bool known) && method == "Colliding") return known;
			bool r = false;
			for (Type t = type; t != null && t != typeof(ModProjectile) && t != typeof(GlobalProjectile) && t != typeof(object); t = t.BaseType)
				if (t.GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly) != null) { r = true; break; }
			if (method == "Colliding") _overrides[type] = r;
			return r;
		}

		/// <summary>True when any loaded mod's GlobalProjectile overrides Colliding and applies to this projectile (or its applicability cannot be told).</summary>
		private static bool GlobalCollides(Projectile p)
		{
			if (_collidingGlobals == null)
			{
				_collidingGlobals = new List<GlobalProjectile>();
				try { foreach (GlobalProjectile g in ModContent.GetContent<GlobalProjectile>()) if (Overrides(g.GetType(), "Colliding")) _collidingGlobals.Add(g); }
				catch (Exception e) { Levy.Instance?.Logger.Warn("PairCull could not list the GlobalProjectiles: " + e.Message + " (every projectile is treated as having a custom hit test)"); _collidingGlobals.Add(null); }
				if (_collidingGlobals.Count > 0) Levy.Instance?.Logger.Info("PairCull: GlobalProjectiles that override Colliding: " + string.Join(", ", _collidingGlobals.ConvertAll(g => g == null ? "?" : g.Mod.Name + "/" + g.Name)));
			}
			foreach (GlobalProjectile g in _collidingGlobals)
			{
				if (g == null) return true;
				try { if (g.AppliesToEntity(p, true)) return true; } catch (Exception) { return true; }
			}
			return false;
		}

		/// <summary>Lab: the army projectile types left alone and why, with how often (for the profile dump).</summary>
		public static string WhyNotSummary(int top)
		{
			var l = new List<KeyValuePair<int, int>>(InelPairs);
			l.Sort((a, b) => b.Value.CompareTo(a.Value));
			var sb = new System.Text.StringBuilder();
			for (int i = 0; i < top && i < l.Count; i++) { WhyNot.TryGetValue(l[i].Key, out string w); sb.Append(i > 0 ? ", " : "").Append(l[i].Key < ProjectileID.Count ? "vanilla" + l[i].Key : (ProjectileLoader.GetProjectile(l[i].Key)?.FullName ?? l[i].Key.ToString())).Append(':').Append(w).Append('x').Append(l[i].Value); }
			return sb.ToString();
		}
	}

	/// <summary>Installs the Projectile.Damage edit described on <see cref="PairCull"/>. If the game's code no longer has the shape the edit expects it stays off (and says so in the log).</summary>
	public class PairCullSystem : ModSystem
	{
		public override void Load()
		{
			if (!PairCull.Requested) { Mod.Logger.Info("PairCull: off (LEVY_PAIRCULL=0 or LEVY_OPT=0)"); return; }
			try
			{
				MethodInfo damage = typeof(Projectile).GetMethod("Damage", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
				if (damage == null) { Mod.Logger.Warn("PairCull: Projectile.Damage not found, cull stays off"); return; }
				MonoModHooks.Modify(damage, Manipulate);
			}
			catch (Exception e) { Mod.Logger.Warn("PairCull: could not install, cull stays off: " + e.Message); }
		}

		private void Manipulate(ILContext il)
		{
			try
			{
				MethodInfo getHitbox = typeof(Projectile).GetMethod("Damage_GetHitbox", BindingFlags.Instance | BindingFlags.NonPublic);
				MethodInfo hookCall = typeof(CombinedHooks).GetMethod("CanHitNPCWithProj", BindingFlags.Static | BindingFlags.Public);
				if (getHitbox == null || hookCall == null) { Mod.Logger.Warn("PairCull: Damage_GetHitbox / CanHitNPCWithProj not found, cull stays off"); return; }
				// 1. the damage hitbox local, and the spot right after it is stored
				var c = new ILCursor(il);
				if (!c.TryGotoNext(MoveType.After, i => i.MatchCall(getHitbox))) { Mod.Logger.Warn("PairCull: Damage_GetHitbox call not found, cull stays off"); return; }
				if (!c.Next.MatchStloc(out int valIdx)) { Mod.Logger.Warn("PairCull: hitbox not stored in a local, cull stays off"); return; }
				int afterHitbox = c.Index + 1;
				// 2. the NPC loop: the first CanHitNPCWithProj call, the vanilla "slot alive" check before it, the loop's continue label and the loop variable
				var d = new ILCursor(il);
				if (!d.TryGotoNext(MoveType.Before, i => i.MatchCall(hookCall))) { Mod.Logger.Warn("PairCull: CanHitNPCWithProj call not found, cull stays off"); return; }
				if (!d.TryGotoPrev(MoveType.After, i => i.MatchLdfld(typeof(Entity), "active"))) { Mod.Logger.Warn("PairCull: slot-alive check not found, cull stays off"); return; }
				if (!d.Next.MatchBrfalse(out ILLabel cont)) { Mod.Logger.Warn("PairCull: slot-alive branch has an unexpected shape, cull stays off"); return; }
				int loopVar = -1;
				Mono.Cecil.Cil.Instruction ldloc = d.Instrs[d.Index - 3];
				if (!ldloc.MatchLdloc(out loopVar) || !d.Instrs[d.Index - 4].MatchLdsfld(typeof(Main), "npc") || !d.Instrs[d.Index - 2].MatchLdelemRef()) { Mod.Logger.Warn("PairCull: loop variable not found, cull stays off"); return; }
				int branchIdx = d.Index; // the brfalse

				// edits (everything matched, nothing emitted so far)
				VariableDefinition Flt() { var v = new VariableDefinition(il.Import(typeof(float))); il.Body.Variables.Add(v); return v; }
				var act = new VariableDefinition(il.Import(typeof(bool))); il.Body.Variables.Add(act);
				VariableDefinition loX = Flt(), hiX = Flt(), loY = Flt(), hiY = Flt();
				var valVar = il.Body.Variables[valIdx];
				var loopVarDef = il.Body.Variables[loopVar];
				MethodInfo far = typeof(PairCull).GetMethod(nameof(PairCull.Far));
				MethodInfo audit = typeof(PairCull).GetMethod(nameof(PairCull.AuditPair));
				d.Index = branchIdx + 1;
				ILLabel skip = d.DefineLabel();
				d.Emit(OpCodes.Ldloc, act);
				d.Emit(OpCodes.Brfalse, skip);
				if (PairCull.Audit) { d.Emit(OpCodes.Ldarg_0); d.Emit(OpCodes.Ldloc, valVar); }
				d.Emit(OpCodes.Ldsfld, typeof(Main).GetField("npc"));
				d.Emit(OpCodes.Ldloc, loopVarDef);
				d.Emit(OpCodes.Ldelem_Ref);
				d.Emit(OpCodes.Ldloc, loX); d.Emit(OpCodes.Ldloc, hiX); d.Emit(OpCodes.Ldloc, loY); d.Emit(OpCodes.Ldloc, hiY);
				d.Emit(OpCodes.Call, PairCull.Audit ? audit : far);
				d.Emit(OpCodes.Brtrue, cont);
				d.MarkLabel(skip);
				// once per Damage call, right after the damage hitbox is stored (the locals start at false / 0 on every path)
				c.Index = afterHitbox;
				c.Emit(OpCodes.Ldarg_0);
				c.Emit(OpCodes.Ldloc, valVar);
				c.Emit(OpCodes.Ldloca, loX); c.Emit(OpCodes.Ldloca, hiX); c.Emit(OpCodes.Ldloca, loY); c.Emit(OpCodes.Ldloca, hiY);
				c.Emit(OpCodes.Call, typeof(PairCull).GetMethod(nameof(PairCull.Begin)));
				c.Emit(OpCodes.Stloc, act);
				PairCull.Enabled = true;
				Mod.Logger.Info("PairCull: Projectile.Damage edited" + (PairCull.Audit ? " (AUDIT: nothing is culled, every classified pair is also asked the real hit test)" : ""));
			}
			catch (Exception e) { Mod.Logger.Warn("PairCull: IL edit failed, cull stays off: " + e); }
		}
	}
}
