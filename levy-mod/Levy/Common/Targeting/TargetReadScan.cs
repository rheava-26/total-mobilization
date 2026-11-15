using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Terraria;
using Terraria.ModLoader;
using Levy.Common.Weapons;

namespace Levy.Common.Targeting
{
	/// <summary>
	/// Does a modded NPC's compiled AI ask whether it HAS a target (<c>NPC.HasValidTarget</c>, <c>HasPlayerTarget</c>, <c>GetTargetData</c>)? Vanilla answers "no" for target 255, which is the slot the soldier swap puts the army's stand-in in, so such an AI
	/// reads "no target" for the whole fight: a worm head that refreshes its despawn clock only while it has a target, a helper that falls back to the player at slot 254 (an empty slot at the world's origin) to aim at. They take the flyer slot instead
	/// (<see cref="EnemyTargeting.UsesFlyerSlot"/>): a real player index (254) that holds the stand-in, which reads as a valid player.
	/// The answer is read once per type from the compiled code of the NPC's class chain and the methods it calls in its own mod (three levels), the same walk the world-alter scan makes. Mods whose boss AIs the swap has been fought against already
	/// (<see cref="LevyConstants.Targeting.SlotProvenMods"/>) keep the route they have.
	/// </summary>
	public static class TargetReadScan
	{
		/// <summary>LEVY_TARGETVALID=0 keeps every modded NPC off this rule (A/B switch for the lab and the world check).</summary>
		internal static readonly bool On = Environment.GetEnvironmentVariable("LEVY_TARGETVALID") != "0";
		private static readonly Dictionary<int, bool> _cache = new();
		private static readonly HashSet<string> Names = new() { "get_HasValidTarget", "get_HasPlayerTarget", "get_HasNPCTarget", "GetTargetData" };

		/// <summary>True when this modded NPC's AI reads target validity and its mod has no proven slot-255 route.</summary>
		public static bool Reads(NPC npc)
		{
			if (!On || npc == null || npc.type < Terraria.ID.NPCID.Count || npc.ModNPC == null) return false;
			int type = npc.type;
			lock (_cache)
			{
				if (_cache.TryGetValue(type, out bool v)) return v;
				bool r = false;
				try { r = !LevyConstants.Targeting.SlotProvenMods.Contains(npc.ModNPC.Mod.Name) && ScanReads(npc.ModNPC.GetType()); }
				catch (Exception) { r = false; }
				_cache[type] = r;
				return r;
			}
		}

		/// <summary>Forgets what was read (world check: the scan is repeated from nothing).</summary>
		internal static void Forget() { lock (_cache) _cache.Clear(); }

		/// <summary>Compiled-code verdict on one NPC class, ignoring the proven-mod list (world check).</summary>
		internal static bool ScanOnly(Type t) { try { return ScanReads(t); } catch (Exception) { return false; } }

		private static bool ScanReads(Type start)
		{
			WorldAlter.Meta meta = WorldAlter.MetaOf(start.Assembly);
			if (meta == null) return false;
			var seen = new HashSet<MethodBase>(); var queue = new Queue<(MethodBase, int)>();
			const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
			for (Type t = start; t != null && t != typeof(object) && t != typeof(ModNPC) && t.Assembly != typeof(Main).Assembly && t.Assembly != typeof(ModNPC).Assembly; t = t.BaseType)
				foreach (MethodBase m in t.GetMethods(flags)) if (seen.Add(m)) queue.Enqueue((m, 0));
			int budget = 700;
			while (queue.Count > 0 && budget-- > 0)
			{
				var (m, depth) = queue.Dequeue();
				var callees = new List<MethodBase>();
				if (Reads(m, meta, callees)) return true;
				if (depth < 3) foreach (MethodBase c in callees) if (seen.Add(c)) queue.Enqueue((c, depth + 1));
			}
			return false;
		}

		private static bool Reads(MethodBase m, WorldAlter.Meta meta, List<MethodBase> callees)
		{
			byte[] il; try { il = m.GetMethodBody()?.GetILAsByteArray(); } catch (Exception) { return false; }
			if (il == null) return false;
			int i = 0;
			while (i < il.Length)
			{
				short code = il[i++]; if (code == 0xFE && i < il.Length) code = (short)(0xFE00 | il[i++]);
				if (!WorldAlter._ops.TryGetValue(code, out var op)) return false;
				int size = op.OperandType switch
				{
					System.Reflection.Emit.OperandType.InlineNone => 0,
					System.Reflection.Emit.OperandType.ShortInlineBrTarget or System.Reflection.Emit.OperandType.ShortInlineI or System.Reflection.Emit.OperandType.ShortInlineVar => 1,
					System.Reflection.Emit.OperandType.InlineVar => 2,
					System.Reflection.Emit.OperandType.InlineI8 or System.Reflection.Emit.OperandType.InlineR => 8,
					System.Reflection.Emit.OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(il, i),
					_ => 4,
				};
				if (op.OperandType == System.Reflection.Emit.OperandType.InlineMethod && i + 4 <= il.Length && (op.Value == System.Reflection.Emit.OpCodes.Call.Value || op.Value == System.Reflection.Emit.OpCodes.Callvirt.Value))
				{
					int token = BitConverter.ToInt32(il, i);
					try
					{
						var n = WorldAlter.Name(meta.Reader, MetadataTokens.EntityHandle(token));
						if (n != null)
						{
							var (ns, type, name, def) = n.Value;
							if (ns == "Terraria" && type == "NPC" && Names.Contains(name)) return true;
							if (!def.IsNil && callees.Count < 64)
							{
								var d = meta.Reader.GetMethodDefinition(def);
								if (d.GetGenericParameters().Count == 0 && meta.Reader.GetTypeDefinition(d.GetDeclaringType()).GetGenericParameters().Count == 0)
								{
									MethodBase t = m.Module.ResolveMethod(token);
									if (t != null) callees.Add(t);
								}
							}
						}
					}
					catch (Exception) { }
				}
				i += size;
			}
			return false;
		}
	}
}
