namespace Levy.Common.Util
{
	/// <summary>
	/// Entry counters of the projectile-AI detours, read by the dead-hook world check ([IdentityWorld] "deadhooks: ..."). A detour that sits on a method the JIT has inlined into its caller (Projectile.AI into Projectile.Update)
	/// is never reached, so every wrapper that must run records here each time it is entered and each time it acted, and the check requires the count to move. Plain ints: lab reads only, never read in play.
	/// </summary>
	public static class HookProbe
	{
		/// <summary>Entered (the detour ran, whether or not it acted) per hook point: the classic On_Projectile.AI stub, On_Projectile.VanillaAI, and ProjectileLoader.ProjectileAI (what runs a modded projectile's PreAI / AI).</summary>
		public static int HeldAi, HeldVanilla, HeldLoader;
		public static int MiscAi, MiscVanilla, MiscLoader, MiscActed;
		public static int FlyerAi, FlyerVanilla, FlyerLoader, FlyerActed;
		public static int SoundAi, SoundUpdate, SoundClassified;
		public static int ViewAi, ViewVanilla, ViewLoader, ViewActed, ViewMismatch;
		public static int SoundInAi, SoundOutAi;
		/// <summary>Set by ArmySoundThrottle when its projectile detour is installed (a client always, a server only for the self-check world): the army-kind probe in the soldier-shot PreAI then counts.</summary>
		public static bool SoundWatch;

		private static readonly System.Reflection.FieldInfo[] _f = System.Array.FindAll(typeof(HookProbe).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static), f => f.FieldType == typeof(int));
		/// <summary>All counters, for before / after deltas.</summary>
		public static int[] Snap() { var a = new int[_f.Length]; for (int i = 0; i < a.Length; i++) a[i] = (int)_f[i].GetValue(null); return a; }
		public static int[] Delta(int[] since) { var a = Snap(); for (int i = 0; i < a.Length; i++) a[i] -= since[i]; return a; }
		public static int Part(int[] a, string name) { for (int i = 0; i < _f.Length; i++) if (_f[i].Name == name) return a[i]; return 0; }
		public static string Describe(int[] a) { var sb = new System.Text.StringBuilder(); for (int i = 0; i < _f.Length; i++) if (a[i] != 0) sb.Append(_f[i].Name).Append('=').Append(a[i]).Append(' '); return sb.Length == 0 ? "(nothing fired)" : sb.ToString().TrimEnd(); }

		public static string Snapshot() =>
			$"held ai/vanilla/loader={HeldAi}/{HeldVanilla}/{HeldLoader} misc ai/vanilla/loader/acted={MiscAi}/{MiscVanilla}/{MiscLoader}/{MiscActed} flyer ai/vanilla/loader/acted={FlyerAi}/{FlyerVanilla}/{FlyerLoader}/{FlyerActed} sound ai/update/classified={SoundAi}/{SoundUpdate}/{SoundClassified} view ai/vanilla/loader/acted={ViewAi}/{ViewVanilla}/{ViewLoader}/{ViewActed}";
	}
}
