using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Terraria;
using Terraria.ModLoader;
using Levy.Common.Globals;
using Levy.Common.Targeting;
using Levy.Content.NPCs;

namespace Levy.Common.Weapons
{
	/// <summary>
	/// A WEAPON'S COUNTER ON THE PLAYER, KEPT PER LEVY. Some weapons keep their state on the player rather than on the item or the shot: Calamity's shark guns (Megalodon, Seadragon, Voidragon, Archerfish) count the shots that hit on
	/// <c>CalamityPlayer.sharkGunDamageScaling</c> and the gun's holdout reads it back when it fires its rocket, its fish or its beam (damage times the count) and sets it to zero on the reload. A levy's holdout is held by his own player
	/// (the dummy, in the held chain) but his shots are owned by the commander's slot, like every soldier shot: the hits counted up on HER player and his holdout read his own, which stayed at 0 (then 1): his Megalodon rocket did a
	/// thirtieth of a player's, and his hits raised her own gun's count.
	/// <para/>Rows "C" (mod, ModPlayer class, field): the fix <see cref="ArmyHitCooldown"/> makes for the hit cooldown. For the length of every pass of a levy's shot (its update, hit and kill, <see cref="OwnerPuppet"/>) and of his use window,
	/// the field on the owner's ModPlayer holds the LEVY's value; whatever the pass did to it is the levy's, and the commander's own value is put back in the same finally.
	/// <para/>Rows "H" (mod, GlobalProjectile class, bool field, ModPlayer class, int field): the counts a player's OWN ModPlayer hit hook makes. A levy's shot is an NPC shot (npcProj, so the commander's on-hit gear never fires for it),
	/// which skips the owner's ModPlayer hit hooks: Calamity's count of a shark gun's bullets (CalamityPlayer.OnHitNPCWithProj: the bullet's sharkBullets flag, first hit only) never ran for a levy's bullet, and the Voidragon's beam had half a
	/// player's multiplier. For a levy's shot whose flag is set, its first hit adds one to that counter on the levy's own player.
	/// Lab A/B: env LEVY_PLAYERCOUNTERS=0.
	/// </summary>
	public sealed class PlayerCounters : ModSystem
	{
		public static readonly bool Disabled = Environment.GetEnvironmentVariable("LEVY_PLAYERCOUNTERS") == "0";
		public const string TablePath = "Common/Weapons/PlayerCounters.tsv";

		private sealed class Row { public ModPlayer Template; public Action<ModPlayer, ModPlayer> Swap; public string Name; }
		private sealed class HitRow { public GlobalProjectile Global; public Func<GlobalProjectile, bool> Flag; public ModPlayer Template; public Action<ModPlayer> Add; public string Name; }
		private static readonly List<Row> _rows = new();
		private static readonly List<HitRow> _hits = new();
		private static Player _owner; private static Player _dummy;
		/// <summary>Lab counters: passes run with a levy's counters; first hits of a flagged levy shot counted on his player.</summary>
		public static long Swaps, HitCounts;
		public static int Rows => _rows.Count + _hits.Count;
		public static string RowNames => string.Join(", ", _rows.ConvertAll(r => r.Name)) + " | " + string.Join(", ", _hits.ConvertAll(r => r.Name));
		internal static bool HasHitRows => _hits.Count > 0;

		public override void PostSetupContent()
		{
			_rows.Clear(); _hits.Clear();
			if (Disabled) return;
			string text;
			try { byte[] b = Mod.GetFileBytes(TablePath); text = b == null ? "" : Encoding.UTF8.GetString(b); }
			catch (Exception e) { Mod.Logger.Warn("Levy: " + TablePath + " not read: " + e.Message); return; }
			foreach (string raw in text.Split('\n'))
			{
				string line = raw.TrimEnd('\r');
				if (line.Length == 0 || line[0] == '#') continue;
				string[] c = line.Split('\t');
				try
				{
					if (c[0] == "C" && c.Length >= 4) AddCounter(c[1], c[2], c[3]);
					else if (c[0] == "H" && c.Length >= 6) AddHit(c[1], c[2], c[3], c[4], c[5]);
				}
				catch (Exception e) { Mod.Logger.Warn($"Levy: player counter row '{line}' not resolved: {e.Message}"); }
			}
		}

		public override void Unload() { _rows.Clear(); _hits.Clear(); _owner = null; _dummy = null; }

		private static T Template<T>(Type t) where T : class, ILoadable
		{
			foreach (T x in ModContent.GetContent<T>()) if (x.GetType() == t) return x;
			return null;
		}

		private void AddCounter(string modName, string playerClass, string field)
		{
			if (!ModLoader.TryGetMod(modName, out Mod mod)) return;
			Type t = mod.Code?.GetType(playerClass);
			FieldInfo f = t?.GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			ModPlayer template = t != null ? Template<ModPlayer>(t) : null;
			if (f == null || template == null) { Mod.Logger.Warn($"Levy: player counter {playerClass}.{field} not found"); return; }
			ParameterExpression a = Expression.Parameter(typeof(ModPlayer), "a"), b = Expression.Parameter(typeof(ModPlayer), "b"), tmp = Expression.Variable(f.FieldType, "tmp");
			MemberExpression fa = Expression.Field(Expression.Convert(a, t), f), fb = Expression.Field(Expression.Convert(b, t), f);
			BlockExpression body = Expression.Block(new[] { tmp }, Expression.Assign(tmp, fa), Expression.Assign(fa, fb), Expression.Assign(fb, tmp));
			_rows.Add(new Row { Template = template, Swap = Expression.Lambda<Action<ModPlayer, ModPlayer>>(body, a, b).Compile(), Name = t.Name + "." + field });
		}

		private void AddHit(string modName, string globalClass, string flagField, string playerClass, string field)
		{
			if (!ModLoader.TryGetMod(modName, out Mod mod)) return;
			Type gt = mod.Code?.GetType(globalClass), pt = mod.Code?.GetType(playerClass);
			FieldInfo gf = gt?.GetField(flagField, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic), pf = pt?.GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			GlobalProjectile g = gt != null ? Template<GlobalProjectile>(gt) : null;
			ModPlayer m = pt != null ? Template<ModPlayer>(pt) : null;
			if (gf == null || pf == null || gf.FieldType != typeof(bool) || pf.FieldType != typeof(int) || g == null || m == null) { Mod.Logger.Warn($"Levy: player counter hit row {globalClass}.{flagField} -> {playerClass}.{field} not resolved"); return; }
			ParameterExpression a = Expression.Parameter(typeof(GlobalProjectile), "g"), b = Expression.Parameter(typeof(ModPlayer), "p");
			MemberExpression f = Expression.Field(Expression.Convert(b, pt), pf);
			_hits.Add(new HitRow
			{
				Global = g, Template = m, Name = gt.Name + "." + flagField + " -> " + pt.Name + "." + field,
				Flag = Expression.Lambda<Func<GlobalProjectile, bool>>(Expression.Field(Expression.Convert(a, gt), gf), a).Compile(),
				Add = Expression.Lambda<Action<ModPlayer>>(Expression.Assign(f, Expression.Increment(f)), b).Compile(),
			});
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

		/// <summary>True when one of the hit rows counts this shot (its flag is set): the counter is kept by <see cref="OnLevyHit"/>, so the dummy's own on-hit hook is not run for it (<c>SoldierNPC.GearOnHitProj</c>: it would count the same hit twice, and into the field the open pass holds for the owner).</summary>
		internal static bool CountsShot(Projectile p)
		{
			for (int i = 0; i < _hits.Count; i++)
			{
				try { if (p.TryGetGlobalProjectile(_hits[i].Global, out GlobalProjectile g) && _hits[i].Flag(g)) return true; } catch (Exception) { }
			}
			return false;
		}

		/// <summary>A levy's shot landed a hit (<see cref="PlayerCounterHits"/>): its first hit counts on his player for every hit row whose flag the shot carries.</summary>
		internal static void OnLevyHit(Projectile p, SoldierNPC body)
		{
			if (p.numHits != 0 || body?.Dummy == null) return;
			for (int i = 0; i < _hits.Count; i++)
			{
				HitRow r = _hits[i];
				try
				{
					if (!p.TryGetGlobalProjectile(r.Global, out GlobalProjectile g) || !r.Flag(g)) continue;
					// while his counters are open on the owner (inside his shot's own pass), the owner's field IS his: add there, or the close would hand it to the commander
					Player holder = ReferenceEquals(_dummy, body.Dummy) && _owner != null ? _owner : body.Dummy;
					r.Add(holder.GetModPlayer(r.Template)); HitCounts++;
				}
				catch (Exception) { }
			}
		}
	}

	/// <summary>The hit side of <see cref="PlayerCounters"/>: a levy's shot (tagged) that lands a hit is offered to the hit rows.</summary>
	public sealed class PlayerCounterHits : GlobalProjectile
	{
		public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (!PlayerCounters.HasHitRows || !projectile.npcProj || !projectile.TryGetGlobalProjectile(out SoldierProjectileTag t) || t.SoldierIndex < 0) return;
			SoldierNPC body = EnemyTargeting.LiveBody(t.SoldierIndex);
			if (body != null) PlayerCounters.OnLevyHit(projectile, body);
		}
	}
}
