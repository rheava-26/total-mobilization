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

		/// <summary>The fields of the player a weapon's use or a round's hook can write (by name fragment): everything the player's own update does not move on its own.</summary>
		public static readonly string[] Watched = { "itemAnimation", "itemTime", "reuseDelay", "channel", "controlUse", "releaseUse", "heldProj", "noItems", "CCed", "selectedItem", "itemRotation", "direction", "velocity", "mouseWorld", "compositeFrontArm", "compositeBackArm", "talkNPC", "mount", "dead", "stoned", "frozen", "webbed", "noThrow", "itemLocation", "controlUp", "controlDown", "controlLeft", "controlRight", "controlJump" };
		public static bool IsWatched(string field) { foreach (string w in Watched) if (field.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0) return true; return false; }

		private static readonly Dictionary<Type, FieldInfo[]> _watchedFields = new();
		private static FieldInfo[] WatchedFields(Type t)
		{
			if (_watchedFields.TryGetValue(t, out var a)) return a;
			a = StateFields(t).Where(f => IsWatched(f.Name)).ToArray();
			_watchedFields[t] = a;
			return a;
		}

		/// <summary>Only the watched use-state fields (see <see cref="Watched"/>) of the player and of every ModPlayer: a few dozen reads, cheap enough for every phase of every tick.</summary>
		public static Dictionary<string, string> WatchedSnapshot(Player p)
		{
			var d = new Dictionary<string, string>();
			void Take(object o, string tag) { foreach (FieldInfo f in WatchedFields(o.GetType())) { try { object v = f.GetValue(o); d[tag + "." + f.Name] = v is Array arr ? string.Join(",", arr.Cast<object>()) : Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture); } catch (Exception) { } } }
			Take(p, "Player");
			foreach (ModPlayer mp in p.ModPlayers) Take(mp, mp.Mod.Name + ":" + mp.Name);
			d["Player.talkNPC"] = p.talkNPC.ToString(); d["Player.compositeFrontArm"] = p.compositeFrontArm.enabled + "/" + p.compositeFrontArm.rotation; d["Player.compositeBackArm"] = p.compositeBackArm.enabled + "/" + p.compositeBackArm.rotation;
			d["Player.velocity"] = p.velocity.ToString(); d["Player.itemLocation"] = p.itemLocation.ToString();
			return d;
		}

		/// <summary>Names of the fields in <paramref name="now"/> that differ from <paramref name="before"/>, added to <paramref name="into"/>.</summary>
		public static void Differences(Dictionary<string, string> before, Dictionary<string, string> now, HashSet<string> into)
		{
			foreach (var kv in now) if (kv.Key != "Player.active" && kv.Key != "Player.nearbyActiveNPCs" && (!before.TryGetValue(kv.Key, out string b) || b != kv.Value)) into.Add(kv.Key);
		}
	}
}
