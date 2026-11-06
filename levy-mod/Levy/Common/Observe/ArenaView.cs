using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.Graphics.Light;
using Terraria.ModLoader;

namespace Levy.Common.Observe
{
	/// <summary>
	/// The zoomed-out arena view of a static camera: the screen shows <c>Factor</c> times as much world per side.
	/// <para/>HOW (read from the tModLoader 1.4.4 decompile; see PERFORMANCE.md in this folder):
	/// vanilla clamps <c>GameZoomTarget</c> to 1..2 (zoom IN only) and sizes its render targets to the backbuffer, so a plain matrix zoom-out would only fill the middle of the screen.
	/// The only path that draws tiles and walls to the screen with no render target is "draw to screen" (<c>Lighting.UpdateEveryFrame</c>: Retro / Trippy lighting, no zoom-in), the same path the game's own
	/// world-capture uses for huge areas. So while a camera is zoomed out this class
	/// (1) switches lighting to Retro, (2) during the world draw makes the game think the screen is <c>Factor</c> times bigger (<c>Main.screenWidth/Height</c>, which sizes the tile/NPC/dust/gore draw ranges and the lighting area),
	/// (3) shrinks the view matrix by 1/Factor with a 1x1 viewport override so the bigger virtual screen lands on the real one, and
	/// (4) puts the real screen size back before the interface draws. All of it is undone the frame the view ends.
	/// UNVERIFIED on a GPU: written against the decompile and cannot be seen headless.
	/// </summary>
	public static class ArenaView
	{
		public const float MaxFactor = 3f;
		/// <summary>Per-side zoom-out the current view asks for (1 = normal).</summary>
		public static float Factor = 1f;
		/// <summary>The world draw this frame is running with an enlarged virtual screen.</summary>
		public static bool Inflated { get; private set; }
		/// <summary>The arena view is engaged (zoomed out, Retro lighting on).</summary>
		public static bool Active { get; private set; }
		public static bool SkipFx => Active && ObserveConfig.Get != null && ObserveConfig.Get.SkipDustAndGoreInArena;

		private static int _realW, _realH, _builtW, _builtH;
		private static bool _lightSaved, _vpOverridden;
		private static LightMode _savedMode; private static float _savedGameZoom;
		private static float _realFmz = 1f;

		public static void Load()
		{
			Main.OnPostDraw += OnPostDraw;
			On_Dust.NewDust += DustHook;
			On_Gore.NewGore_IEntitySource_Vector2_Vector2_int_float += GoreHook;
		}

		public static void Unload()
		{
			Main.OnPostDraw -= OnPostDraw;
			On_Dust.NewDust -= DustHook;
			On_Gore.NewGore_IEntitySource_Vector2_Vector2_int_float -= GoreHook;
			RestoreSize(); ClearViewport(); ExitLighting();
			Active = false;
		}

		private static int DustHook(On_Dust.orig_NewDust orig, Vector2 position, int width, int height, int type, float speedX, float speedY, int alpha, Color newColor, float scale)
			=> SkipFx ? Main.maxDust : orig(position, width, height, type, speedX, speedY, alpha, newColor, scale);

		private static int GoreHook(On_Gore.orig_NewGore_IEntitySource_Vector2_Vector2_int_float orig, IEntitySource source, Vector2 position, Vector2 velocity, int type, float scale)
			=> SkipFx ? Main.maxGore : orig(source, position, velocity, type, scale);

		private static void OnPostDraw(GameTime gt) => RestoreSize();

		/// <summary>The recommended default, from the measured/estimated costs in PERFORMANCE.md.</summary>
		public static float ClampFactor(float f) => Math.Clamp(float.IsNaN(f) ? 1f : f, 1f, MaxFactor);

		/// <summary>Called from ModifyScreenPosition (start of the world draw): centres the view on <paramref name="center"/> and, when zoomed out, enlarges the virtual screen.</summary>
		public static void Frame(Vector2 center, float factor)
		{
			RestoreSize(); // a restore that was missed last frame
			float f = ClampFactor(factor);
			if (f <= 1.001f || Main.mapFullscreen || Main.gameMenu)
			{
				Active = false; ExitLighting(); ClearViewport();
				Main.screenPosition = center - new Vector2(Main.screenWidth, Main.screenHeight) / 2f;
				return;
			}
			Active = true; EnterLighting();
			_realW = Main.screenWidth; _realH = Main.screenHeight;
			Main.screenWidth = (int)(_realW * f); Main.screenHeight = (int)(_realH * f);
			Inflated = true;
			Main.screenPosition = center - new Vector2(Main.screenWidth, Main.screenHeight) / 2f;
			if (_builtW != Main.screenWidth || _builtH != Main.screenHeight)
			{
				_builtW = Main.screenWidth; _builtH = Main.screenHeight;
				try { Lighting.LegacyEngine.Rebuild(); } catch (Exception e) { ModContent.GetInstance<Levy>().Logger.Warn("Levy: arena lighting rebuild failed: " + e.Message); }
			}
		}

		/// <summary>Called from ModifyTransformMatrix (after vanilla set the zoom for the real screen): shrinks the view so the virtual screen fits the real one.</summary>
		public static void Transform(Terraria.Graphics.SpriteViewMatrix m)
		{
			if (!Inflated) return;
			var vp = Main.instance.GraphicsDevice.Viewport;
			_realFmz = RealForcedMinimumZoom();
			Main.ForcedMinimumZoom = _realFmz;
			Main.BackgroundViewMatrix.Zoom = new Vector2(_realFmz);
			m.SetViewportOverride(new Viewport(0, 0, 1, 1)); _vpOverridden = true; // keeps the matrix' own centring offset ~0, so world pixel 0 of the virtual screen is pixel 0 of the real one
			m.Zoom = new Vector2((float)vp.Width / Main.screenWidth, (float)vp.Height / Main.screenHeight);
		}

		/// <summary>The forced minimum zoom the REAL screen size would have (vanilla computes it from the inflated size by then).</summary>
		private static float RealForcedMinimumZoom()
		{
			float cx = Main.MinimumZoomComparerX, cy = Main.MinimumZoomComparerY;
			try
			{
				var fi = typeof(Terraria.ModLoader.ModLoader).GetField("removeForcedMinimumZoom", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
				if (fi != null && fi.GetValue(null) is true) { cx = 8192f; cy = 8192f; } // tML's "remove forced minimum zoom" setting (internal)
			}
			catch (Exception) { }
			return Math.Max(Math.Max(1f, _realW / cx), _realH / cy);
		}

		/// <summary>Puts the real screen size back (before the interface draws, and at the end of the frame).</summary>
		public static void RestoreSize()
		{
			if (!Inflated) return;
			Main.screenWidth = _realW; Main.screenHeight = _realH;
			Inflated = false;
		}

		private static void ClearViewport()
		{
			if (!_vpOverridden) return;
			_vpOverridden = false;
			try { Main.GameViewMatrix.ClearViewportOverride(); } catch (Exception) { }
		}

		private static void EnterLighting()
		{
			if (_lightSaved) return;
			_lightSaved = true;
			_savedMode = Lighting.Mode; _savedGameZoom = Main.GameZoomTarget;
			Main.GameZoomTarget = 1f; // a zoom-in forces render targets, which this path cannot use
			if (Lighting.NotRetro) Lighting.Mode = LightMode.Retro;
		}

		private static void ExitLighting()
		{
			if (!_lightSaved) return;
			_lightSaved = false;
			Main.GameZoomTarget = _savedGameZoom;
			if (Lighting.Mode != _savedMode) Lighting.Mode = _savedMode;
		}

		/// <summary>The view ended: everything back to normal now.</summary>
		public static void Leave()
		{
			Factor = 1f; Active = false;
			RestoreSize(); ClearViewport(); ExitLighting();
		}
	}
}
