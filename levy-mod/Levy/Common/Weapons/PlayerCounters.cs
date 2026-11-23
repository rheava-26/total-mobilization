using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Terraria;
using Terraria.ModLoader;
using Levy.Content.NPCs;

namespace Levy.Common.Weapons
{
	/// <summary>
	/// A WEAPON'S COUNTER ON THE PLAYER, KEPT PER LEVY. Some weapons keep their state on the player rather than on the item or the shot: Calamity's shark guns (Megalodon, Seadragon, Voidragon, Archerfish) count the bullets that hit on
	/// <c>CalamityPlayer.sharkGunDamageScaling</c> (every hit of a bullet the gun fired adds one, in the ModPlayer's on-hit hook of the bullet's OWNER) and the gun's holdout reads it back when it fires its rocket or its fish (damage times
	/// the count) and sets it to zero on the reload. A levy's holdout is held by his own player (the dummy, in the held chain) but his bullets are owned by the commander's slot, like every soldier shot: the hits counted up on HER player and
	/// his holdout read his own, which stayed at 0 (then 1): his Megalodon rocket did a thirtieth of a player's, and his hits raised her own gun's count.
	/// <para/>The fix is the one <see cref="ArmyHitCooldown"/> makes for the hit cooldown: for the length of every pass of a levy's shot (its update, hit and kill, <see cref="OwnerPuppet"/>) and of his use window, each counter listed in
	/// <c>PlayerCounters.tsv</c> on the owner's ModPlayer holds the LEVY's value; whatever the pass did to it is the levy's, and the commander's own value is put back in the same finally. The rows are data: mod, ModPlayer class, field.
	/// Lab A/B: env LEVY_PLAYERCOUNTERS=0.
	/// </summary>
	public sealed class PlayerCounters : ModSystem
	{
		public static readonly bool Disabled = Environment.GetEnvironmentVariable("LEVY_PLAYERCOUNTERS") == "0";
		public const string TablePath = "Common/Weapons/PlayerCounters.tsv";

		private sealed class Row { public ModPlayer Template; public Action<ModPlayer, ModPlayer> Swap; public string Name; }
		private static readonly List<Row> _rows = new();
		private static Player _owner; private static Player _dummy;
		/// <summary>Lab counters: passes run with a levy's counters; rows resolved.</summary>
		public static long Swaps;
		public static int Rows => _rows.Count;
		public static string RowNames => string.Join(", ", _rows.ConvertAll(r => r.Name));

		public override void PostSetupContent()
		{
			_rows.Clear();
			if (Disabled) return;
			string text;
			try { byte[] b = Mod.GetFileBytes(TablePath); text = b == null ? "" : Encoding.UTF8.GetString(b); }
			catch (Exception e) { Mod.Logger.Warn("Levy: " + TablePath + " not read: " + e.Message); return; }
			foreach (string raw in text.Split('\n'))
			{
				string line = raw.TrimEnd('\r');
				if (line.Length == 0 || line[0] == '#') continue;
				string[] c = line.Split('\t');
				if (c.Length < 3 || !ModLoader.TryGetMod(c[0], out Mod mod)) continue;
				try
				{
					Type t = mod.Code?.GetType(c[1]);
					if (t == null || !typeof(ModPlayer).IsAssignableFrom(t)) { Mod.Logger.Warn($"Levy: player counter {c[1]} is not a ModPlayer of {c[0]}"); continue; }
					FieldInfo f = t.GetField(c[2], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					if (f == null) { Mod.Logger.Warn($"Levy: player counter {c[1]}.{c[2]} not found"); continue; }
					ModPlayer template = null;
					foreach (ModPlayer mp in ModContent.GetContent<ModPlayer>()) if (mp.GetType() == t) { template = mp; break; }
					if (template == null) continue;
					_rows.Add(new Row { Template = template, Swap = CompileSwap(t, f), Name = c[1].Substring(c[1].LastIndexOf('.') + 1) + "." + c[2] });
				}
				catch (Exception e) { Mod.Logger.Warn($"Levy: player counter {line} not resolved: {e.Message}"); }
			}
		}

		public override void Unload() { _rows.Clear(); _owner = null; _dummy = null; }

		/// <summary>(a, b) => swap a.f and b.f, typed (no boxing).</summary>
		private static Action<ModPlayer, ModPlayer> CompileSwap(Type t, FieldInfo f)
		{
			ParameterExpression a = Expression.Parameter(typeof(ModPlayer), "a"), b = Expression.Parameter(typeof(ModPlayer), "b"), tmp = Expression.Variable(f.FieldType, "tmp");
			MemberExpression fa = Expression.Field(Expression.Convert(a, t), f), fb = Expression.Field(Expression.Convert(b, t), f);
			BlockExpression body = Expression.Block(new[] { tmp }, Expression.Assign(tmp, fa), Expression.Assign(fa, fb), Expression.Assign(fb, tmp));
			return Expression.Lambda<Action<ModPlayer, ModPlayer>>(body, a, b).Compile();
		}

		/// <summary>
		/// Opens a levy's counters on <paramref name="owner"/> (the slot his shots are owned by): returns true when it swapped, and the caller then calls <see cref="Close"/> in its finally. Nested passes (a hit inside an update) find the
		/// counters already open and change nothing.
		/// </summary>
		internal static bool Open(Player owner, SoldierNPC body)
		{
			if (_rows.Count == 0 || _owner != null || owner == null || body == null) return false;
			Player d = body.Dummy;
			if (d == null || ReferenceEquals(d, owner)) return false;
			try { SwapAll(owner, d); }
			catch (Exception) { return false; }
			_owner = owner; _dummy = d; Swaps++;
			return true;
		}

		internal static void Close()
		{
			Player o = _owner, d = _dummy;
			_owner = null; _dummy = null;
			if (o == null || d == null) return;
			try { SwapAll(o, d); } catch (Exception) { }
		}

		private static void SwapAll(Player a, Player b)
		{
			for (int i = 0; i < _rows.Count; i++)
			{
				Row r = _rows[i];
				r.Swap(a.GetModPlayer(r.Template), b.GetModPlayer(r.Template));
			}
		}
	}
}
