using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Levy.Common.Movement
{
	/// <summary>How a hook behaves once cast. Normal hooks pull the body at <see cref="HookInfo.PullPx"/> toward the anchor.</summary>
	public enum HookKind : byte
	{
		Normal,
		/// <summary>Static Hook: no pull of its own; the body is steered along the line with the direction keys.</summary>
		Steered,
		/// <summary>Anti-Gravity Hook: stands the body off the anchor and never arrives. Not used.</summary>
		StandOff,
		/// <summary>Thorium Spring Hook: moves the player in its own AI. Not used.</summary>
		Spring,
	}

	/// <summary>Reach, pull and count of a hook item. Vanilla values come from the hook AI (Projectile.AI_007_GrapplingHooks, Player.GetGrapplingForces); modded ones from the measured study, else a default.</summary>
	public readonly struct HookInfo
	{
		public readonly bool IsHook;
		public readonly float ReachPx, PullPx;
		public readonly int Count;
		public readonly HookKind Kind;

		public HookInfo(bool isHook, float reachPx, float pullPx, int count, HookKind kind) { IsHook = isHook; ReachPx = reachPx; PullPx = pullPx; Count = count; Kind = kind; }

		/// <summary>A hook a soldier can plan with.</summary>
		public bool Usable => IsHook && (LabAllowAll || (Kind != HookKind.StandOff && Kind != HookKind.Spring)) && ReachPx >= LevyConstants.Hooks.MinUsefulReachTiles * 16f;
		/// <summary>HARNESS: treat the stand-off and Spring hooks as usable too (to exercise their code paths).</summary>
		public static bool LabAllowAll;

		/// <summary>Armory ranking: reach first, then pull, then how many hooks it holds.</summary>
		public float Score => !Usable ? -1f : ReachPx + PullPx * 8f + Math.Min(Count, LevyConstants.Hooks.MaxHooksPerPlan) * 12f;

		public static readonly HookInfo None = new(false, 0f, 0f, 0, HookKind.Normal);

		private static readonly Dictionary<int, HookInfo> _cache = new();

		public static bool IsHookItem(Item it) => it != null && !it.IsAir && it.shoot > 0 && it.shoot < Main.projHook.Length && Main.projHook[it.shoot];

		public static HookInfo Of(Item it)
		{
			if (!IsHookItem(it)) return None;
			if (_cache.TryGetValue(it.type, out var hit)) return hit;
			HookInfo info;
			try { info = Build(it); } catch (Exception) { info = new HookInfo(true, 18f * 16f, 11f, 1, HookKind.Normal); }
			_cache[it.type] = info;
			return info;
		}

		internal static void ClearCache() => _cache.Clear();

		private static HookInfo Build(Item it)
		{
			int t = it.shoot;
			var mp = ProjectileLoader.GetProjectile(t);
			if (mp == null)
			{
				float reach = t switch
				{
					13 => 300f, 32 => 400f, 73 => 440f, 74 => 440f, 165 => 375f, 256 => 350f, 315 => 500f, 322 => 550f, 331 => 400f, 332 => 550f, 372 => 400f, 396 => 300f,
					>= 646 and <= 649 => 550f, 652 => 600f, 753 => 420f, 865 => 300f, 935 => 500f, >= 486 and <= 489 => 480f, 446 => 500f,
					>= 230 and <= 235 => 300f + 30f * (t - 230),
					_ => 18f * 16f,
				};
				float pull = t == 315 ? 14f : t == 487 ? 12f : (t >= 646 && t <= 649) ? 16f : 11f;
				int count = ProjectileID.Sets.SingleGrappleHook[t] ? 1 : t switch { 165 => 8, 256 => 2, 372 => 2, 652 => 1, 73 => 2, >= 646 and <= 649 => 4, _ => 3 }; // a single-hook type: QuickGrapple kills the old hook when a new one is cast
				HookKind kind = t == 446 ? HookKind.StandOff : t == 652 ? HookKind.Steered : HookKind.Normal;
				if (kind == HookKind.Steered) pull = 6f;
				return new HookInfo(true, reach * 0.97f, pull, count, kind);
			}
			// modded: the study measured the Thorium and Calamity hooks (flight distance in tiles, reel speed in px per tick)
			string key = mp.Mod.Name + "/" + mp.Name;
			float reachTiles = 18f, pullPx = 11f; HookKind k = HookKind.Normal;
			switch (key)
			{
				case "ThoriumMod/DevilsReach": reachTiles = 32.8f; break;
				case "ThoriumMod/MushroomHookProjectile": reachTiles = 31f; break;
				case "ThoriumMod/NeptuneHook": reachTiles = 30.9f; break;
				case "ThoriumMod/ZephyrsGripPro": reachTiles = 10f; pullPx = 9.5f; break;
				case "ThoriumMod/KrakenHook": reachTiles = 27.1f; break;
				case "ThoriumMod/AmmutsebaSashPro": reachTiles = 30.9f; break;
				case "ThoriumMod/JewellersWallGripPro": reachTiles = 30f; break;
				case "ThoriumMod/AquamarineHookPro": reachTiles = 22.2f; break;
				case "ThoriumMod/OpalHookPro": reachTiles = 21.5f; break;
				case "ThoriumMod/SpringHookPro": reachTiles = 15.8f; pullPx = 12f; k = HookKind.Spring; break;
				case "ThoriumMod/GhostlyGrappleHook": reachTiles = 35f; pullPx = 16f; break;
				case "CalamityMod/SerpentsBiteHook": reachTiles = 20f; pullPx = 12f; break;
				case "CalamityMod/BobbitHead": reachTiles = 20f; pullPx = 24f; break;
			}
			int n = 3;
			try { var sample = ContentSamples.ProjectilesByType[t]; ProjectileLoader.NumGrappleHooks(sample, new Player(), ref n); } catch (Exception) { }
			if (ProjectileID.Sets.SingleGrappleHook[t]) n = 1;
			return new HookInfo(true, reachTiles * 16f * 0.97f, pullPx, Math.Max(1, n), k);
		}
	}
}
