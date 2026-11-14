using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace Levy.Common.Testing
{
	/// <summary>
	/// Field-by-field snapshots of a Player and of every one of its ModPlayers (every mod's), by name: whatever a weapon's use, a projectile's hook or a mod's listener could write.
	/// The self-checks compare two snapshots and list the fields that moved.
	/// </summary>
	public static class PlayerProbe
	{
		private static readonly Dictionary<Type, List<FieldInfo>> _fieldCache = new();

		private static List<FieldInfo> StateFields(Type t)
		{
			if (_fieldCache.TryGetValue(t, out var l)) return l;
			l = new List<FieldInfo>();
			for (Type c = t; c != null && c != typeof(object); c = c.BaseType)
				foreach (FieldInfo f in c.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
				{
					Type ft = f.FieldType;
					if (ft.IsPrimitive || ft.IsEnum || ft == typeof(string) || ft == typeof(Vector2) || (ft.IsArray && ft.GetElementType().IsPrimitive)) l.Add(f);
				}
			_fieldCache[t] = l;
			return l;
		}

		public static Dictionary<string, string> Snapshot(Player p)
		{
			var d = new Dictionary<string, string>();
			void Take(object o, string tag) { foreach (FieldInfo f in StateFields(o.GetType())) { try { object v = f.GetValue(o); d[tag + "." + f.Name] = v is Array arr ? string.Join(",", arr.Cast<object>()) : Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture); } catch (Exception) { } } }
			Take(p, "Player");
			foreach (ModPlayer mp in p.ModPlayers) Take(mp, mp.Mod.Name + ":" + mp.Name);
			d["Player.velocity"] = p.velocity.ToString(); d["Player.compositeFrontArm"] = p.compositeFrontArm.enabled + "/" + p.compositeFrontArm.rotation;
			return d;
		}

		/// <summary>Names of the fields in <paramref name="now"/> that differ from <paramref name="before"/>, added to <paramref name="into"/>.</summary>
		public static void Differences(Dictionary<string, string> before, Dictionary<string, string> now, HashSet<string> into)
		{
			foreach (var kv in now) if (kv.Key != "Player.active" && kv.Key != "Player.nearbyActiveNPCs" && (!before.TryGetValue(kv.Key, out string b) || b != kv.Value)) into.Add(kv.Key);
		}
	}
}
