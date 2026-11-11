using System;
using System.Collections;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.Events;
using Terraria.ModLoader;
using Levy.Common.Targeting;

namespace Levy.Common.Observe
{
	/// <summary>
	/// While a camera, a GoPro levy or a mind-linked levy is on screen, the BACKGROUND, SKY and MUSIC follow the biome at that spot instead of the one the player's body stands in.
	/// The player's real zone flags are never changed for gameplay: a scratch Player stands at the viewed point, a private <see cref="SceneMetrics"/> scans the tiles around it (swapped into
	/// Main.SceneMetrics for that one call), and its zone flags are copied onto the real local player only for the length of <c>Main.DoDraw</c> and <c>Main.UpdateAudio</c>, then put back.
	/// What this cannot do: the scan runs a few times a second (not every frame), and a scan at the viewed point also lets nearby-tile hooks of that area fire once; the mod systems' tile counts are
	/// handed back to the real scan straight afterwards. Modded biomes follow too when their IsBiomeActive reads those counts or the player's position.
	/// </summary>
	public class ViewBiome : ModSystem
	{
		/// <summary>Seconds between scans of the viewed point (ticks).</summary>
		public const uint RefreshTicks = 30;

		private static Player _scratch, _real, _view;
		private static SceneMetrics _metrics;
		private static bool _have; private static uint _nextScan;
		private static readonly FieldInfo TileCounts = typeof(SceneMetrics).GetField("_tileCounts", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

		/// <summary>True while the visual zones come from the viewed point.</summary>
		public static bool Active => _hooked && _have && !Main.gameMenu && ObserveSystem.Kind != ViewKind.None;

		public override void Load()
		{
			if (Main.dedServ) return;
			try { On_Main.DoDraw += DoDraw; On_Main.UpdateAudio += UpdateAudio; _hooked = true; }
			catch (Exception e) { Mod.Logger.Warn("[ViewBiome] could not hook the draw and audio calls; the camera view keeps the player's biome: " + e.Message); }
		}
		private static bool _hooked;

		public override void Unload()
		{
			if (_hooked) { On_Main.DoDraw -= DoDraw; On_Main.UpdateAudio -= UpdateAudio; _hooked = false; }
			_scratch = _real = _view = null; _metrics = null; _have = false;
		}

		public override void OnWorldUnload() { _have = false; }

		public override void PostUpdateEverything()
		{
			if (Main.dedServ || Main.gameMenu) return;
			if (ObserveSystem.Kind == ViewKind.None || !ObserveSystem.TryViewCenter(out Vector2 c)) { _have = false; return; }
			if (_have && Main.GameUpdateCount < _nextScan) return;
			_nextScan = Main.GameUpdateCount + RefreshTicks;
			try { _have = Scan(c); }
			catch (Exception e) { _have = false; _nextScan = Main.GameUpdateCount + 600u; ModContent.GetInstance<ViewBiome>().Mod.Logger.Warn("[ViewBiome] scan failed, the view keeps the player's biome: " + e.Message); }
		}

		/// <summary>Scans the tiles around <paramref name="center"/> with a private SceneMetrics and reads the zones off a scratch player standing there.</summary>
		private static bool Scan(Vector2 center)
		{
			var real = Main.SceneMetrics; if (real == null) return false;
			_scratch ??= new Player(); _view ??= new Player(); _metrics ??= new SceneMetrics();
			_scratch.Center = center;
			Main.SceneMetrics = _metrics;
			try
			{
				_metrics.ScanAndExportToMain(new SceneMetricsScanSettings { BiomeScanCenterPositionInWorld = center, ScanOreFinderData = false });
				_scratch.UpdateBiomes();
			}
			finally
			{
				Main.SceneMetrics = real;
				try { if (TileCounts?.GetValue(real) is int[] counts) SystemLoader.TileCountsAvailable(counts); } catch (Exception) { } // hand the mod systems their real counts back
			}
			BiomeMirror.Copy(_scratch, _view);
			return true;
		}

		/// <summary>Puts <paramref name="from"/>'s zone flags on the local player, runs <paramref name="run"/>, and always puts the real ones back.</summary>
		private static void WithViewZones(Action run)
		{
			if (!Active) { run(); return; }
			var me = Main.player[Main.myPlayer];
			_real ??= new Player();
			try { BiomeMirror.Copy(me, _real); BiomeMirror.Copy(_view, me); }
			catch (Exception) { run(); return; }
			try { run(); }
			finally { BiomeMirror.Copy(_real, me); }
		}

		private static void DoDraw(On_Main.orig_DoDraw orig, Main self, Microsoft.Xna.Framework.GameTime gameTime) => WithViewZones(() => orig(self, gameTime));
		private static void UpdateAudio(On_Main.orig_UpdateAudio orig, Main self) => WithViewZones(() => orig(self));

		/// <summary>Boot check: the swap puts the view's zones on a player and the real ones back, flag for flag.</summary>
		internal static bool SelfCheck(out string why)
		{
			why = "";
			var me = new Player(); var view = new Player(); var saved = new Player();
			me.ZoneOverworldHeight = true;
			view.ZoneSnow = true; view.ZoneRockLayerHeight = true; view.ZoneCorrupt = true;
			BiomeMirror.Copy(me, saved); BiomeMirror.Copy(view, me);
			bool during = me.ZoneSnow && me.ZoneCorrupt && me.ZoneRockLayerHeight && !me.ZoneOverworldHeight;
			BiomeMirror.Copy(saved, me);
			bool after = !me.ZoneSnow && !me.ZoneCorrupt && !me.ZoneRockLayerHeight && me.ZoneOverworldHeight;
			if (!during) why = "the view's zones did not reach the player"; else if (!after) why = "the player's real zones did not come back";
			return during && after;
		}
	}

	/// <summary>Logs the view-biome swap self-check at boot.</summary>
	public class ViewBiomeCheck : ModSystem
	{
		public override void PostSetupContent()
		{
			global::Levy.Common.Util.LoadSafe.Run(() =>
			{
				bool ok = ViewBiome.SelfCheck(out string why);
				if (ok) Mod.Logger.Info("[ViewBiome] self-check ok (zones swap onto the player and back, flag for flag)"); else Mod.Logger.Error("[ViewBiome] self-check FAILED " + why);
			});
		}
	}
}
