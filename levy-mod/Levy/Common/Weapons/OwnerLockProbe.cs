using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Terraria;
using Terraria.ModLoader;

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
							}
							else
							{
								MethodBase callee = module.ResolveMethod(token, typeArgs, null);
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
