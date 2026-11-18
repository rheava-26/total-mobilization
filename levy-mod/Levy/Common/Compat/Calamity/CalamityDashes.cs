using System;
using System.Reflection;
using Terraria;
using Terraria.ModLoader;

namespace Levy.Common.Compat.Calamity
{
	/// <summary>
	/// Calamity's accessory dashes (Asgardian Aegis, Elysian Aegis, Statis' Ninja Belt, Statis' Void Sash, Counter Scarf, Ornate Shield, Deep Diver, V8 Engine, Asgard's Valor ...). They do not set Player.dashType (the accessories write 0 there):
	/// each one sets <c>CalamityPlayer.DashID</c> in UpdateAccessory and CalamityMod's PlayerDashEffect for that id knows the dash speed. Read by reflection (Levy has no compile-time reference to CalamityMod); a missing member turns it off.
	/// The public <c>DashID</c> getter answers Calamity's default dash for every player with no dash gear, so the private backing field (<c>dashID</c>), which only an accessory writes, is what is read.
	/// </summary>
	public static class CalamityDashes
	{
		private static bool _init, _ok;
		private static FieldInfo _backing; private static MethodInfo _find, _speed; private static Type _cpType;

		private static void Init()
		{
			if (_init) return; _init = true;
			try
			{
				if (!ModLoader.TryGetMod("CalamityMod", out Mod cal)) return;
				_cpType = cal.Code.GetType("CalamityMod.CalPlayer.CalamityPlayer");
				Type mgr = cal.Code.GetType("CalamityMod.CalPlayer.Dashes.PlayerDashManager");
				Type eff = cal.Code.GetType("CalamityMod.CalPlayer.Dashes.PlayerDashEffect");
				_backing = _cpType?.GetField("dashID", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
				_find = mgr?.GetMethod("FindByID", BindingFlags.Public | BindingFlags.Static);
				_speed = eff?.GetMethod("CalculateDashSpeed", BindingFlags.Public | BindingFlags.Instance);
				_ok = _cpType != null && _backing != null && _find != null && _speed != null;
			}
			catch (Exception) { _ok = false; }
		}

		/// <summary>True when CalamityMod is loaded.</summary>
		public static bool Loaded => ModLoader.HasMod("CalamityMod");

		/// <summary>True when an accessory on <paramref name="d"/> set a Calamity dash; <paramref name="speed"/> is that dash's start speed (px/tick, as its own CalculateDashSpeed answers).</summary>
		public static bool TryRead(Player d, out float speed)
		{
			speed = 0f;
			Init();
			if (!_ok || d == null) return false;
			try
			{
				ModPlayer cp = null;
				foreach (var mp in d.ModPlayers) if (_cpType.IsInstanceOfType(mp)) { cp = mp; break; }
				if (cp == null) return false;
				string id = _backing.GetValue(cp) as string;
				if (string.IsNullOrEmpty(id)) return false;
				var args = new object[] { id, null };
				if (!(bool)_find.Invoke(null, args) || args[1] == null) { speed = 16.9f; return true; } // a dash Calamity does not know: a plain dash
				speed = (float)_speed.Invoke(args[1], new object[] { d });
				return true;
			}
			catch (Exception) { return false; }
		}
	}
}
