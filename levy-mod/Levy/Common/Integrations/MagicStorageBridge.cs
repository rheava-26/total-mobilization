using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Config;

namespace Levy.Common.Integrations
{
	/// <summary>
	/// Weak, reflection-only bridge to Magic Storage (no compile-time reference). Lets an armory draw issue gear from a Storage Heart's network.
	/// Every MS touch is guarded: any miss logs ONE warning and turns MS supply off for the session. When MS is absent, <see cref="Enabled"/> is a
	/// single cached-bool check and nothing else runs.
	/// Targeted members (MagicStorage 0.7.x, namespace MagicStorage.Components): TEStorageHeart.GetStoredItems() : IEnumerable&lt;Item&gt;,
	/// TEStorageHeart.Withdraw(Item, bool) / TryWithdraw(Item, bool, bool toInventory = false) : Item, TEStorageComponent.GetHeart(),
	/// TEStorageHeart.IsAlive. Withdraw signatures are matched defensively (Item, bool, then only optional bools).
	/// </summary>
	public static class MagicStorageBridge
	{
		private const string Ns = "MagicStorage.Components.";

		private static bool _probed, _available, _disabled;
		private static Type _heartType, _accessType, _remoteType, _compType;
		private static MethodInfo _getStored, _withdraw, _getHeart;
		private static PropertyInfo _isAlive;
		private static object[] _withdrawTail; // args after (Item, bool)

		/// <summary>True when Magic Storage is loaded, its API resolved, the config allows it, and we are on the server / single player.</summary>
		public static bool Enabled
		{
			get
			{
				if (_disabled) return false;
				if (!_probed) Probe();
				if (!_available) return false;
				if (Main.netMode == NetmodeID.MultiplayerClient) return false;
				try { return LevyConfig.Get.MagicStorageSupply; } catch (Exception) { return false; }
			}
		}

		private static void Fail(string why)
		{
			if (_disabled) return;
			_disabled = true; _available = false;
			try { Levy.Instance?.Logger.Warn("Levy: Magic Storage supply disabled for this session (" + why + ")."); } catch (Exception) { }
		}

		private static void Probe()
		{
			_probed = true;
			try
			{
				if (!ModLoader.TryGetMod("MagicStorage", out Mod ms) || ms == null) return; // not installed: silent and normal
				var asm = ms.GetType().Assembly;
				_heartType = asm.GetType(Ns + "TEStorageHeart");
				_accessType = asm.GetType(Ns + "TEStorageAccess");
				_remoteType = asm.GetType(Ns + "TERemoteAccess");
				_compType = asm.GetType(Ns + "TEStorageComponent");
				if (_heartType == null || _compType == null) { Fail("heart type not found"); return; }

				_getStored = _heartType.GetMethod("GetStoredItems", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
				if (_getStored == null || !typeof(IEnumerable<Item>).IsAssignableFrom(_getStored.ReturnType)) { Fail("GetStoredItems not found"); return; }

				_getHeart = _compType.GetMethod("GetHeart", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
				if (_getHeart == null) { Fail("GetHeart not found"); return; }
				_isAlive = _heartType.GetProperty("IsAlive", BindingFlags.Public | BindingFlags.Instance);

				// Withdraw: (Item lookFor, bool keepOneIfFavorite, [optional bools...]) -> Item. Prefer plain "Withdraw" (no netcode branch) over "TryWithdraw".
				MethodInfo best = null; int bestRank = int.MaxValue;
				foreach (var m in _heartType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
				{
					if ((m.Name != "Withdraw" && m.Name != "TryWithdraw") || m.ReturnType != typeof(Item)) continue;
					var ps = m.GetParameters();
					if (ps.Length < 2 || ps[0].ParameterType != typeof(Item) || ps[1].ParameterType != typeof(bool)) continue;
					bool ok = true;
					for (int i = 2; i < ps.Length; i++) if (ps[i].ParameterType != typeof(bool) || !ps[i].IsOptional) { ok = false; break; }
					if (!ok) continue;
					int rank = (m.Name == "Withdraw" ? 0 : 100) + ps.Length;
					if (rank < bestRank) { bestRank = rank; best = m; }
				}
				if (best == null) { Fail("no usable Withdraw signature"); return; }
				var bp = best.GetParameters();
				_withdrawTail = new object[bp.Length - 2];
				for (int i = 2; i < bp.Length; i++)
				{
					object d = bp[i].HasDefaultValue ? bp[i].DefaultValue : null;
					_withdrawTail[i - 2] = d is bool b && b;
				}
				_withdraw = best;
				_available = true;
			}
			catch (Exception e) { Fail("probe failed: " + e.Message); }
		}

		private static bool IsAir(Item it) => it == null || it.IsAir || it.stack <= 0;

		// ------------------------------------------------------------------ finding the heart

		private static readonly List<TileEntity> _comps = new();
		private static uint _compsStamp = uint.MaxValue;

		private static bool IsRelevant(Type t) =>
			(_heartType != null && _heartType.IsAssignableFrom(t))
			|| (_accessType != null && _accessType.IsAssignableFrom(t))
			|| (_remoteType != null && _remoteType.IsAssignableFrom(t));

		/// <summary>The live Storage Heart reachable from <paramref name="near"/>: the nearest Heart / Storage Access / Remote Access within <paramref name="tiles"/> that resolves to one. Null if none or MS is off.</summary>
		public static object FindHeart(Vector2 near, float tiles)
		{
			if (!Enabled) return null;
			try
			{
				uint now = Main.GameUpdateCount;
				if (_compsStamp == uint.MaxValue || now < _compsStamp || now - _compsStamp >= 180)
				{
					_compsStamp = now;
					_comps.Clear();
					foreach (var te in TileEntity.ByID.Values)
						if (te != null && IsRelevant(te.GetType())) _comps.Add(te);
				}
				if (_comps.Count == 0) return null;

				float lim = tiles * 16f; lim *= lim;
				var ordered = new List<(float d, TileEntity te)>();
				foreach (var te in _comps)
				{
					if (!TileEntity.ByID.TryGetValue(te.ID, out var live) || !ReferenceEquals(live, te)) continue; // gone since the cache was built
					float d = Vector2.DistanceSquared(near, new Vector2((te.Position.X + 1f) * 16f, (te.Position.Y + 1f) * 16f));
					if (d <= lim) ordered.Add((d, te));
				}
				ordered.Sort((a, b) => a.d.CompareTo(b.d));
				foreach (var (_, te) in ordered)
				{
					var h = _getHeart.Invoke(te, null) as TileEntity; // a Heart resolves to itself; an Access / Remote Access to its network's heart
					if (h == null || !_heartType.IsInstanceOfType(h)) continue;
					if (!TileEntity.ByID.TryGetValue(h.ID, out var liveH) || !ReferenceEquals(liveH, h)) continue;
					if (_isAlive != null && _isAlive.GetValue(h) is bool alive && !alive) continue;
					return h;
				}
				return null;
			}
			catch (Exception e) { Fail("heart lookup failed: " + e.Message); return null; }
		}

		// ------------------------------------------------------------------ reading

		private static int _snapHeartId = -1; private static uint _snapTick = uint.MaxValue; private static List<Item> _snap;
		private static int _ammoHeartId = -1; private static uint _ammoTick = uint.MaxValue; private static HashSet<int> _ammoIds;
		private static readonly List<Item> NoItems = new();

		/// <summary>
		/// The heart network's stored items (the REAL stored instances; READ-ONLY - never mutate them). Excludes air and favorited singletons (the player's protected items).
		/// Cached for the current tick and dropped after any withdraw. Empty when unavailable.
		/// </summary>
		public static List<Item> Snapshot(object heart)
		{
			if (!Enabled || heart is not TileEntity te) return NoItems;
			try
			{
				if (_snap != null && _snapHeartId == te.ID && _snapTick == Main.GameUpdateCount) return _snap;
				var list = new List<Item>();
				if (_getStored.Invoke(te, null) is IEnumerable<Item> en)
					foreach (var it in en)
						if (!IsAir(it) && !(it.favorited && it.stack <= 1)) list.Add(it);
				_snap = list; _snapHeartId = te.ID; _snapTick = Main.GameUpdateCount;
				return list;
			}
			catch (Exception e) { Fail("GetStoredItems failed: " + e.Message); return NoItems; }
		}

		/// <summary>Every ammo id (<see cref="Item.ammo"/>) the network holds. Cached ~1s; a hint only.</summary>
		public static HashSet<int> AmmoIds(object heart)
		{
			var set = new HashSet<int>();
			if (!Enabled || heart is not TileEntity te) return set;
			try
			{
				uint now = Main.GameUpdateCount;
				if (_ammoIds != null && _ammoHeartId == te.ID && now >= _ammoTick && now - _ammoTick < 60) return _ammoIds;
				foreach (var it in Snapshot(heart)) if (it.ammo > 0 && !it.IsACoin) set.Add(it.ammo);
				_ammoIds = set; _ammoHeartId = te.ID; _ammoTick = now;
			}
			catch (Exception) { }
			return set;
		}

		// ------------------------------------------------------------------ withdrawing

		/// <summary>
		/// Withdraws up to <paramref name="wantStack"/> of the same item (type + prefix) as <paramref name="candidate"/> (an entry of <see cref="Snapshot"/>).
		/// <paramref name="got"/> is EXACTLY what MS handed back (a real item, stack &lt;= wantStack) and the caller MUST give it to a soldier; false (got = null,
		/// nothing taken) when MS returned nothing. The candidate itself is never touched: a one-off clone is the request.
		/// </summary>
		public static bool TryWithdrawExact(object heart, Item candidate, int wantStack, out Item got)
		{
			got = null;
			if (!Enabled || heart is not TileEntity te || IsAir(candidate) || wantStack <= 0) return false;
			try
			{
				var look = candidate.Clone();
				look.stack = Math.Min(wantStack, candidate.stack);
				if (look.stack <= 0) return false;
				var args = new object[2 + _withdrawTail.Length];
				args[0] = look; args[1] = true; // keepOneIfFavorite: never take a player-favorited item's last copy
				for (int i = 0; i < _withdrawTail.Length; i++) args[2 + i] = _withdrawTail[i];
				_snap = null; _snapTick = uint.MaxValue; _ammoIds = null; // the network is about to change
				var res = _withdraw.Invoke(te, args) as Item;
				if (IsAir(res)) return false;
				got = res;
				return true;
			}
			catch (Exception e) { Fail("withdraw failed: " + e.Message); got = null; return false; }
		}

		/// <summary>Convenience: withdraw up to <paramref name="wantStack"/> of the best (by <paramref name="score"/>, else first) stored item passing <paramref name="pred"/>.</summary>
		public static bool TryWithdrawMatch(object heart, Func<Item, bool> pred, int wantStack, out Item got, Func<Item, float> score = null)
		{
			got = null;
			if (!Enabled || pred == null) return false;
			Item best = null; float bestS = float.NegativeInfinity;
			foreach (var it in Snapshot(heart))
			{
				if (IsAir(it) || !pred(it)) continue;
				float s = score != null ? score(it) : 0f;
				if (best == null || s > bestS) { best = it; bestS = s; }
			}
			return best != null && TryWithdrawExact(heart, best, wantStack, out got);
		}
	}
}
