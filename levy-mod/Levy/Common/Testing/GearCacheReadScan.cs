using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Terraria;
using Terraria.ModLoader;

namespace Levy.Common.Testing
{
	/// <summary>
	/// GEAR CACHE READ SCAN (lab, LEVY_GEARCACHE_ILSCAN=1): which code of the gear build reads a given field. The build's entry points (the game's ResetEffects, UpdateEquips, ApplyEquipFunctional, the prefix and armor
	/// benefits, UpdateArmorSets, UpdateBuffs; every mod's ModPlayer ResetEffects / UpdateEquips / PreUpdateBuffs / PostUpdateBuffs; every GlobalItem and ModItem UpdateEquip / UpdateAccessory / UpdateArmorSet /
	/// UpdateVanitySet; every ModBuff and GlobalBuff Update) and what they call, three calls deep, are read as IL; every load of a field (ldfld, ldflda, ldsfld) is recorded with the method that does it.
	/// The answer for a field the levy's tail or Levy itself changes between ticks says whether the cache may leave it out of its comparison. Measurement only.
	/// </summary>
	internal static class GearCacheReadScan
	{
		internal static readonly bool On = Environment.GetEnvironmentVariable("LEVY_GEARCACHE_ILSCAN") == "1";
		private static bool _done;
		private static readonly Dictionary<short, OpCode> Ops = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static).Select(f => (OpCode)f.GetValue(null)).ToDictionary(o => o.Value);

		internal static void Run(Action<string> log)
		{
			if (!On || _done) return; _done = true;
			var watch = (Environment.GetEnvironmentVariable("LEVY_GEARCACHE_ILSCAN_FIELDS") ?? "Player.itemRotation,Player.compositeFrontArm,Player.compositeBackArm,Player.heldProj,Player.itemTime,Player.itemAnimation,Player.inventory,Player.lifeRegen,Player.wingTime,Player.stealth,Player.stealthTimer,Player.controlLeft,Player.controlRight,Player.controlJump,Player.direction,Player.miscCounter,Player.statMana,Player.statLife,Player.ownedProjectileCounts,Player.lifeRegenTime,Player.lifeRegenCount,Player.velocity,Player.position,CalamityPlayer.cooldowns,CalamityPlayer.rage,CalamityPlayer.adrenaline,CalamityPlayer.rogueStealth,CalamityPlayer.TimeHoldingMelee,CalamityPlayer.stealthStrikeThisFrame,Player.rocketDelay2,Player.numMinions,Player.endurance")
				.Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
			var entries = new List<MethodBase>();
			var pt = typeof(Player);
			foreach (var n in new[] { "ResetEffects", "UpdateEquips", "ApplyEquipFunctional", "GrantPrefixBenefits", "GrantArmorBenefits", "UpdateArmorSets", "UpdateBuffs", "ApplyEquipVanity" })
				entries.AddRange(pt.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Where(m => m.Name == n && m.DeclaringType == pt));
			var hooks = new HashSet<string> { "ResetEffects", "UpdateEquips", "PreUpdateBuffs", "PostUpdateBuffs", "UpdateEquip", "UpdateAccessory", "UpdateArmorSet", "UpdateVanitySet", "Update" };
			foreach (Mod mod in ModLoader.Mods)
			{
				if (mod.Code == null) continue;
				Type[] types; try { types = mod.Code.GetTypes(); } catch (ReflectionTypeLoadException e) { types = e.Types.Where(x => x != null).ToArray(); }
				foreach (var t in types)
				{
					if (t.IsAbstract) continue;
					bool mp = typeof(ModPlayer).IsAssignableFrom(t), gi = typeof(GlobalItem).IsAssignableFrom(t), mi = typeof(ModItem).IsAssignableFrom(t), mb = typeof(ModBuff).IsAssignableFrom(t), gb = typeof(GlobalBuff).IsAssignableFrom(t);
					if (!(mp || gi || mi || mb || gb)) continue;
					foreach (var m in t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
					{
						if (!hooks.Contains(m.Name)) continue;
						if (m.Name == "Update" && !(mb || gb)) continue;
						if (mp && !(m.Name is "ResetEffects" or "UpdateEquips" or "PreUpdateBuffs" or "PostUpdateBuffs")) continue;
						entries.Add(m);
					}
				}
			}
			var readers = new Dictionary<string, HashSet<string>>(); foreach (var w in watch) readers[w] = new HashSet<string>();
			var seen = new HashSet<MethodBase>(); int scanned = 0;
			var queue = new Queue<(MethodBase m, int depth)>(entries.Select(e => (e, 0)));
			while (queue.Count > 0)
			{
				var (m, depth) = queue.Dequeue();
				if (!seen.Add(m)) continue;
				byte[] il; try { il = m.GetMethodBody()?.GetILAsByteArray(); } catch { continue; }
				if (il == null) continue;
				scanned++;
				Type[] ta = m.DeclaringType?.IsGenericType == true ? m.DeclaringType.GetGenericArguments() : null, ma = m.IsGenericMethod ? m.GetGenericArguments() : null;
				int i = 0;
				while (i < il.Length)
				{
					short v = il[i++]; if (v == 0xFE) v = (short)(0xFE00 | il[i++]);
					if (!Ops.TryGetValue(v, out var op)) break;
					int size = op.OperandType switch { OperandType.InlineNone => 0, OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1, OperandType.InlineVar => 2, OperandType.InlineI8 or OperandType.InlineR => 8, OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(il, i), _ => 4 };
					if (op == OpCodes.Ldfld || op == OpCodes.Ldflda || op == OpCodes.Ldsfld)
					{
						try { var f = m.Module.ResolveField(BitConverter.ToInt32(il, i), ta, ma); string k = f.DeclaringType?.Name + "." + f.Name; if (readers.TryGetValue(k, out var set) && set.Count < 12) set.Add(m.DeclaringType?.Name + "." + m.Name); } catch { }
					}
					else if ((op == OpCodes.Call || op == OpCodes.Callvirt) && depth < 3)
					{
						try
						{
							var c = m.Module.ResolveMethod(BitConverter.ToInt32(il, i), ta, ma);
							var asm = c.DeclaringType?.Assembly;
							if (asm != null && asm != typeof(object).Assembly && !(c.DeclaringType.Namespace ?? "").StartsWith("System") && !(c.DeclaringType.Namespace ?? "").StartsWith("Microsoft"))
								queue.Enqueue((c, depth + 1));
						}
						catch { }
					}
					i += size;
				}
			}
			log($"HARNESS-GCH: read scan: {entries.Count} build entry points, {scanned} methods read (3 calls deep)");
			foreach (var kv in readers.OrderBy(k => k.Key)) log($"HARNESS-GCH: read scan: {kv.Key} read by {kv.Value.Count}{(kv.Value.Count >= 12 ? "+" : "")}: {string.Join(", ", kv.Value)}");
		}
	}
}
