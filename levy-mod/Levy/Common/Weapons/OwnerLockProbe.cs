using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Terraria;
using Terraria.ModLoader;
using Levy.Content.NPCs;

namespace Levy.Common.Weapons
{
	/// <summary>
	/// Finds modded projectiles that PIN THEIR OWNER'S ITEM-USE STATE and never let go. Soldier shots are owned by the commander (for kill credit), so a projectile whose AI keeps
	/// writing <c>owner.heldProj</c> / <c>owner.itemAnimation</c> / <c>owner.itemTime</c> holds the commander "mid-swing" for as long as it lives, and the commander cannot attack.
	/// Held weapons normally release the owner because they die the moment the owner stops holding the use button (they read <c>controlUseItem</c> / <c>channel</c>); a projectile that
	/// pins the owner WITHOUT that release (Mechanica's Serpent's Spear: out, pause, return, one per owner, no input check) jams the commander for good once soldiers fire it.
	/// The probe reads the compiled IL of the projectile's own AI hooks (and the same-class helpers they call), so it keys on the behaviour, never on an item name or id.
	/// Vanilla projectiles are never inspected: their locking styles (held projectile, thick laser, harpoon, hook, whip, yoyo, flail) are rejected by aiStyle in <see cref="WeaponClassifier"/>.
	/// </summary>
	internal static class OwnerLockProbe
	{
		private static readonly Dictionary<int, bool> _cache = new();
		private static FieldInfo _heldProj, _itemAnimation, _itemTime, _controlUseItem, _channel;
		private static Dictionary<short, OpCode> _opcodes;
		/// <summary>While a cursor probe runs: every method name called and every Terraria.Main field read by the scanned IL.</summary>
		private static HashSet<string> _sink;

		/// <summary>True when the projectile type's modded AI pins the owner's item-use state without releasing it when the owner stops using an item.</summary>
		public static bool LocksOwner(int projType)
		{
			if (_cache.TryGetValue(projType, out bool cached)) return cached;
			bool result = false;
			try { result = Probe(projType); }
			catch (Exception e) { ModContent.GetInstance<Levy>()?.Logger.Warn($"Levy: owner-lock probe failed for projectile {projType}: {e.Message}"); }
			_cache[projType] = result;
			return result;
		}

		private static bool Probe(int projType)
		{
			Projectile sample = WeaponClassifier.ProjectileSample(projType);
			ModProjectile mod = sample?.ModProjectile;
			if (mod == null) return false;
			EnsureReflection();

			var visited = new HashSet<MethodBase>();
			var found = new HashSet<FieldInfo>();
			for (Type t = mod.GetType(); t != null && t != typeof(ModProjectile) && t != typeof(object); t = t.BaseType)
			{
				const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
				foreach (string hook in new[] { "PreAI", "AI", "PostAI" })
				{
					MethodInfo m = t.GetMethod(hook, flags, null, Type.EmptyTypes, null);
					if (m != null) Scan(m, 0, visited, found);
				}
			}

			bool pins = found.Contains(_heldProj) || found.Contains(_itemAnimation) || found.Contains(_itemTime);
			bool releases = found.Contains(_controlUseItem) || found.Contains(_channel);
			return pins && !releases;
		}

		/// <summary>True when the modded projectile class chain overrides <c>OnKill</c>: a charge-and-release holdout fires its shot when it ends (Coral Spout, Calamity's charge staffs).</summary>
		public static bool FiresOnKill(int projType)
		{
			ModProjectile mod = WeaponClassifier.ProjectileSample(projType)?.ModProjectile;
			if (mod == null) return false;
			for (Type t = mod.GetType(); t != null && t != typeof(ModProjectile) && t != typeof(object); t = t.BaseType)
				if (t.GetMethod("OnKill", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly, null, new[] { typeof(int) }, null) != null) return true;
			return false;
		}

		private static readonly Dictionary<int, bool> _cursorCache = new();
		/// <summary>True when the projectile's own modded AI reads the owner's cursor: <c>Main.MouseWorld</c> / <c>Main.mouseX</c>, Calamity's per-player <c>mouseWorld</c> or <c>ClampedMouseWorld</c>. A commander-owned
		/// copy would read the COMMANDER's cursor (wherever it last was), so such a shot flies off at nothing: the soldier puts the cursor on its target for the span of that projectile's AI.</summary>
		public static bool ReadsCursor(int projType)
		{
			if (_cursorCache.TryGetValue(projType, out bool cached)) return cached;
			bool result = false;
			try
			{
				ModProjectile mod = WeaponClassifier.ProjectileSample(projType)?.ModProjectile;
				if (mod != null)
				{
					EnsureReflection();
					var sink = new HashSet<string>(); _sink = sink;
					var visited = new HashSet<MethodBase>(); var found = new HashSet<FieldInfo>();
					try
					{
						for (Type t = mod.GetType(); t != null && t != typeof(ModProjectile) && t != typeof(object); t = t.BaseType)
						{
							const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
							foreach (MethodInfo m in t.GetMethods(flags))
								if (m.Name == "PreAI" || m.Name == "AI" || m.Name == "PostAI" || m.Name == "HoldoutAI" || m.Name == "OnKill" || m.Name == "PreKill" || m.Name == "OnSpawn" || m.Name == "OnHitNPC" || m.Name == "ModifyHitNPC")
									Scan(m, 0, visited, found);
						}
					}
					finally { _sink = null; }
					result = sink.Contains("get_MouseWorld") || sink.Contains("get_mouseWorld") || sink.Contains("ClampedMouseWorld") || sink.Contains("mouseX") || sink.Contains("mouseY") || sink.Contains("get_mouseWorldDeltaFromPlayer");
				}
			}
			catch (Exception e) { ModContent.GetInstance<Levy>()?.Logger.Warn($"Levy: cursor probe failed for projectile {projType}: {e.Message}"); }
			_cursorCache[projType] = result;
			return result;
		}

		private static readonly Dictionary<int, bool> _boundCache = new();
		/// <summary>
		/// True for a modded projectile a held modded weapon's holdout spawns whose own AI stays bound to that holder: it reads its owner's use button (Rancor's magic circle dies the moment the owner lets go) or finds its parent
		/// through the owner's projectile-UUID table (<c>Projectile.GetByUUID(owner, ...)</c>: Starmageddon's stars, Yharim's Crystal beams, Calamity's <c>FindProjectileByIdentity</c> lookups). Such a child cannot be handed to the
		/// commander like an ordinary shot (the button it reads is never down for the commander, and a UUID table lookup under another owner finds nothing): it stays in the holder's chain, owned by the dummy slot and run under
		/// the same dummy swap, button and cursor as the holdout itself.
		/// </summary>
		public static bool BoundToHolder(Projectile child)
		{
			if (child == null || child.ModProjectile == null || !HeldWeapons.IsChainOwner(child.owner)) return false;
			int t = child.type;
			if (_boundCache.TryGetValue(t, out bool cached)) return cached;
			bool result = false;
			try
			{
				EnsureReflection();
				var sink = new HashSet<string>(); _sink = sink;
				var visited = new HashSet<MethodBase>(); var found = new HashSet<FieldInfo>();
				try
				{
					for (Type ty = child.ModProjectile.GetType(); ty != null && ty != typeof(ModProjectile) && ty != typeof(object); ty = ty.BaseType)
					{
						const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
						foreach (string hook in new[] { "PreAI", "AI", "PostAI" })
						{
							MethodInfo m = ty.GetMethod(hook, flags, null, Type.EmptyTypes, null);
							if (m != null) Scan(m, 0, visited, found);
						}
					}
				}
				finally { _sink = null; }
				result = found.Contains(_channel) || found.Contains(_controlUseItem) || sink.Contains("GetByUUID") || sink.Contains("FindProjectileByIdentity") || sink.Contains("FindByIdentity");
			}
			catch (Exception e) { ModContent.GetInstance<Levy>()?.Logger.Warn($"Levy: holder-bound probe failed for projectile {t}: {e.Message}"); }
			_boundCache[t] = result;
			return result;
		}

		private static readonly Dictionary<int, bool> _channelCache = new();
		/// <summary>True when the projectile's own modded AI reads its owner's use button (<c>Player.channel</c> / <c>controlUseItem</c>, or Calamity's <c>CantUseHoldout</c>): a held / charge-and-release
		/// "holdout" that only lives, charges and fires while its owner holds the button. A commander-owned copy never sees the button held, so it expires uncharged and shoots wherever the commander's stale cursor was.</summary>
		public static bool ReadsOwnerButton(int projType)
		{
			if (_channelCache.TryGetValue(projType, out bool cached)) return cached;
			bool result = false;
			try
			{
				ModProjectile mod = WeaponClassifier.ProjectileSample(projType)?.ModProjectile;
				if (mod != null)
				{
					EnsureReflection();
					var visited = new HashSet<MethodBase>(); var found = new HashSet<FieldInfo>();
					for (Type t = mod.GetType(); t != null && t != typeof(ModProjectile) && t != typeof(object); t = t.BaseType)
					{
						const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
						foreach (string hook in new[] { "PreAI", "AI", "PostAI" })
						{
							MethodInfo m = t.GetMethod(hook, flags, null, Type.EmptyTypes, null);
							if (m != null) Scan(m, 0, visited, found);
						}
					}
					result = found.Contains(_channel) || found.Contains(_controlUseItem);
				}
			}
			catch (Exception e) { ModContent.GetInstance<Levy>()?.Logger.Warn($"Levy: owner-button probe failed for projectile {projType}: {e.Message}"); }
			_channelCache[projType] = result;
			return result;
		}

		private static readonly Dictionary<int, bool> _tileCache = new();
		/// <summary>True when the projectile's own modded AI turns tile collision ON (<c>tileCollide = true</c>): its SetDefaults value is not the whole story (Calamity's Flare Bolt starts tile-free and collides once launched).</summary>
		public static bool SetsTileCollide(int projType)
		{
			if (_tileCache.TryGetValue(projType, out bool cached)) return cached;
			bool result = false;
			try
			{
				ModProjectile mod = WeaponClassifier.ProjectileSample(projType)?.ModProjectile;
				if (mod != null)
				{
					EnsureReflection();
					var visited = new HashSet<MethodBase>(); var found = new HashSet<FieldInfo>();
					for (Type t = mod.GetType(); t != null && t != typeof(ModProjectile) && t != typeof(object); t = t.BaseType)
					{
						const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
						foreach (MethodInfo m in t.GetMethods(flags))
							if (m.Name == "PreAI" || m.Name == "AI" || m.Name == "PostAI" || m.Name == "OnSpawn") Scan(m, 0, visited, found);
					}
					foreach (FieldInfo f in found) if (f.DeclaringType == typeof(Projectile) && f.Name == "tileCollide") result = true;
				}
			}
			catch (Exception e) { ModContent.GetInstance<Levy>()?.Logger.Warn($"Levy: tile-collide probe failed for projectile {projType}: {e.Message}"); }
			_tileCache[projType] = result;
			return result;
		}

		private static readonly Dictionary<int, List<Type>> _childCache = new();
		/// <summary>The modded projectile types this projectile's own AI / hit / kill hooks name (<c>ProjectileType&lt;T&gt;()</c>): what a holdout spawns by itself.</summary>
		public static List<Type> NamedChildTypes(int projType)
		{
			if (_childCache.TryGetValue(projType, out var cached)) return cached;
			var list = new List<Type>();
			try
			{
				ModProjectile mod = WeaponClassifier.ProjectileSample(projType)?.ModProjectile;
				if (mod != null)
				{
					EnsureReflection();
					const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
					for (Type t = mod.GetType(); t != null && t != typeof(ModProjectile) && t != typeof(object); t = t.BaseType)
						foreach (MethodInfo m in t.GetMethods(flags))
							if (m.Name == "PreAI" || m.Name == "AI" || m.Name == "PostAI" || m.Name == "HoldoutAI" || m.Name == "OnKill" || m.Name == "OnHitNPC" || m.Name == "OnSpawn") ScanProjectileTypes(m, list);
				}
			}
			catch (Exception e) { ModContent.GetInstance<Levy>()?.Logger.Warn($"Levy: child-shot probe failed for projectile {projType}: {e.Message}"); }
			_childCache[projType] = list;
			return list;
		}

		/// <summary>The modded projectile types a modded item's own code names (<c>ModContent.ProjectileType&lt;T&gt;()</c> inside Shoot / ModifyShootStats / UseItem / HoldItem / CanUseItem), in addition to <c>item.shoot</c>.
		/// An item whose Shoot hook spawns its real shot by type (Sahara Slicers: its blades) hides that shot from <c>item.shoot</c>.</summary>
		public static List<Type> NamedProjectileTypes(Item item)
		{
			var list = new List<Type>();
			try
			{
				ModItem mi = item?.ModItem;
				if (mi == null) return list;
				EnsureReflection();
				const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
				for (Type t = mi.GetType(); t != null && t != typeof(ModItem) && t != typeof(object); t = t.BaseType)
					foreach (string hook in new[] { "Shoot", "ModifyShootStats", "UseItem", "HoldItem", "CanUseItem" })
						foreach (MethodInfo m in t.GetMethods(flags))
							if (m.Name == hook) ScanProjectileTypes(m, list);
			}
			catch (Exception e) { ModContent.GetInstance<Levy>()?.Logger.Warn($"Levy: shot-type probe failed for {item?.Name}: {e.Message}"); }
			return list;
		}

		private static void ScanProjectileTypes(MethodBase method, List<Type> into)
		{
			byte[] il = method.GetMethodBody()?.GetILAsByteArray();
			if (il == null) return;
			Module module = method.Module;
			Type[] typeArgs = method.DeclaringType != null && method.DeclaringType.IsGenericType ? method.DeclaringType.GetGenericArguments() : null;
			int i = 0;
			while (i < il.Length)
			{
				short code = il[i++];
				if (code == 0xFE && i < il.Length) code = (short)(0xFE00 | il[i++]);
				if (!_opcodes.TryGetValue(code, out OpCode op)) return;
				int operandAt = i;
				switch (op.OperandType)
				{
					case OperandType.InlineNone: break;
					case OperandType.ShortInlineBrTarget:
					case OperandType.ShortInlineI:
					case OperandType.ShortInlineVar: i += 1; break;
					case OperandType.InlineVar: i += 2; break;
					case OperandType.InlineI8:
					case OperandType.InlineR: i += 8; break;
					case OperandType.InlineSwitch: i += 4 + 4 * BitConverter.ToInt32(il, i); break;
					case OperandType.InlineMethod:
						i += 4;
						try
						{
							MethodBase callee = module.ResolveMethod(BitConverter.ToInt32(il, operandAt), typeArgs, null);
							if (callee != null && callee.IsGenericMethod && callee.Name == "ProjectileType" && callee.GetGenericArguments().Length == 1)
							{
								Type pt = callee.GetGenericArguments()[0];
								if (typeof(ModProjectile).IsAssignableFrom(pt) && !into.Contains(pt)) into.Add(pt);
							}
						}
						catch (Exception) { }
						break;
					default: i += 4; break;
				}
			}
		}

		private static void EnsureReflection()
		{
			if (_heldProj != null) return;
			const BindingFlags f = BindingFlags.Instance | BindingFlags.Public;
			_heldProj = typeof(Player).GetField("heldProj", f);
			_itemAnimation = typeof(Player).GetField("itemAnimation", f);
			_itemTime = typeof(Player).GetField("itemTime", f);
			_controlUseItem = typeof(Player).GetField("controlUseItem", f);
			_channel = typeof(Player).GetField("channel", f);
			_opcodes = new Dictionary<short, OpCode>();
			foreach (FieldInfo fi in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
				if (fi.GetValue(null) is OpCode oc) _opcodes[oc.Value] = oc;
		}

		/// <summary>Walks one method's IL: records every Player field it touches that matters, and follows calls into other methods of the same projectile class (depth-limited).</summary>
		private static void Scan(MethodBase method, int depth, HashSet<MethodBase> visited, HashSet<FieldInfo> found)
		{
			if (method == null || depth > 3 || !visited.Add(method)) return;
			byte[] il = method.GetMethodBody()?.GetILAsByteArray();
			if (il == null) return;
			Module module = method.Module;
			Type[] typeArgs = method.DeclaringType != null && method.DeclaringType.IsGenericType ? method.DeclaringType.GetGenericArguments() : null;
			int i = 0;
			OpCode prevOp = OpCodes.Nop;
			while (i < il.Length)
			{
				short code = il[i++];
				if (code == 0xFE && i < il.Length) code = (short)(0xFE00 | il[i++]);
				if (!_opcodes.TryGetValue(code, out OpCode op)) return;
				int operandAt = i;
				OpCode before = prevOp; prevOp = op;
				switch (op.OperandType)
				{
					case OperandType.InlineNone: break;
					case OperandType.ShortInlineBrTarget:
					case OperandType.ShortInlineI:
					case OperandType.ShortInlineVar: i += 1; break;
					case OperandType.InlineVar: i += 2; break;
					case OperandType.InlineI8:
					case OperandType.InlineR: i += 8; break;
					case OperandType.InlineSwitch: i += 4 + 4 * BitConverter.ToInt32(il, i); break;
					case OperandType.InlineField:
					case OperandType.InlineMethod:
						i += 4;
						int token = BitConverter.ToInt32(il, operandAt);
						try
						{
							if (op.OperandType == OperandType.InlineField)
							{
								FieldInfo fi = module.ResolveField(token, typeArgs, null);
								if (fi != null && fi.DeclaringType == typeof(Player) && (op == OpCodes.Stfld || op == OpCodes.Ldfld)) AddFieldUse(fi, op == OpCodes.Stfld, found);
								if (fi != null && fi.DeclaringType == typeof(Projectile) && op == OpCodes.Stfld && fi.Name == "tileCollide" && before == OpCodes.Ldc_I4_1) found.Add(fi); // the AI switches tile collision ON (Calamity's Flare Bolt does at launch)
								if (fi != null && _sink != null && fi.DeclaringType == typeof(Main)) _sink.Add(fi.Name);
							}
							else
							{
								MethodBase callee = module.ResolveMethod(token, typeArgs, null);
								if (callee != null) _sink?.Add(callee.Name);
								if (callee != null && callee.Name == "CantUseHoldout") found.Add(_channel); // Calamity's PlayerUtils.CantUseHoldout reads Player.channel for the projectile
								if (callee != null && callee.DeclaringType != null && callee.DeclaringType.IsAssignableFrom(method.DeclaringType)
									&& callee.DeclaringType != typeof(ModProjectile) && callee.DeclaringType != typeof(object))
									Scan(callee, depth + 1, visited, found);
							}
						}
						catch (Exception) { /* an unresolvable token is just not followed */ }
						break;
					default: i += 4; break; // InlineBrTarget, InlineI, InlineSig, InlineString, InlineTok, InlineType, ShortInlineR
				}
			}
		}

		/// <summary>Writes to the use-state fields count as pinning; reads of the use-button fields count as the release.</summary>
		private static void AddFieldUse(FieldInfo fi, bool isStore, HashSet<FieldInfo> found)
		{
			if (isStore) { if (fi == _heldProj || fi == _itemAnimation || fi == _itemTime) found.Add(fi); }
			else if (fi == _controlUseItem || fi == _channel) found.Add(fi);
		}
	}
}
