using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;
using Terraria.UI;
using Levy.Common.UI;

namespace Levy.Common.Perf
{
	/// <summary>
	/// THE POST-FIGHT LAG POPUP. When a boss fight ends and the analyzer judged it laggy (<see cref="PerfAnalysis.Judge"/>), a small window in the corner says so, names the cause plainly, and offers what can be done.
	/// When Levy's share was large: "Lower levy effects" (the Army effects setting: nameplates hidden, levy weapon dust thinned), It's fine, Don't ask again, Open Levy settings. When the cost was another mod's or the base game's: only the last three.
	/// There is no option that touches the army's command points or its numbers: that is a design decision, and the self-check asserts the option list.
	/// It never shows during a fight (a pending popup is dropped the moment a boss is near again), shows at most once in ten minutes (half an hour after "It's fine"), and stays away for good after "Don't ask again".
	/// The window is not modal: the game goes on under it, and it goes away by itself after a minute.
	/// </summary>
	public class PerfPopup : ModSystem
	{
		/// <summary>Self-check: act on a dedicated server (nothing is drawn), and stand-ins for the clock and the settings.</summary>
		internal static bool TestMode; internal static DateTime? TestNow; internal static bool? TestEnabled, TestDontAsk;
		/// <summary>Popups made visible this session, and the last verdict shown (self-check counters).</summary>
		internal static int Shown; internal static LagVerdict Current; internal static string LastRefusal;
		private static DateTime _lastShown = DateTime.MinValue, _snoozeUntil = DateTime.MinValue;
		private static LagVerdict _pending; private static uint _showAtTick; private static uint _hideAtTick;
		private static UserInterface _ui; private static PopupState _state;
		public const int DelayTicks = 180, LifeTicks = 3600;

		private static DateTime Now => TestNow ?? DateTime.UtcNow;
		private static bool Enabled { get { if (TestEnabled.HasValue) return TestEnabled.Value; try { return PerfConfig.Get?.LagPopup ?? true; } catch (Exception) { return true; } } }
		private static bool DontAsk { get { if (TestDontAsk.HasValue) return TestDontAsk.Value; try { return PerfConfig.Get?.LagPopupDontAskAgain ?? false; } catch (Exception) { return false; } } }

		public override void Load() { if (!Main.dedServ) _ui = new UserInterface(); }
		public override void Unload() { _ui = null; _state = null; _pending = null; Current = null; }
		public override void OnWorldUnload() { _pending = null; Hide(); }

		/// <summary>The fight is over and the verdict has options: queue the popup to show a few seconds from now, if the rules allow it.</summary>
		internal static void Offer(LagVerdict v)
		{
			if (v == null || !v.ShowPopup) return;
			if (Main.dedServ && !TestMode) return;
			string why = PerfAnalysis.Refusal(Enabled, DontAsk, PerfAnalyzer.InFight || PerfAnalyzer.BossNear, Now, _lastShown, _snoozeUntil);
			LastRefusal = why;
			if (why != null) { Levy.Instance?.Logger.Info("[PerfAnalyzer] lag popup not shown (" + why + ")"); return; }
			_pending = v; _showAtTick = Main.GameUpdateCount + DelayTicks;
			if (TestMode) Show();
		}

		/// <summary>Every tick (from the analyzer): shows a pending popup when its time has come, drops it when a fight starts again, and hides a shown one after its life.</summary>
		internal static void Tick()
		{
			if (Main.dedServ && !TestMode) return;
			bool bossNear = PerfAnalyzer.BossNear;
			if (_pending != null)
			{
				if (bossNear || Main.gameMenu) { _pending = null; return; }
				if (Main.GameUpdateCount >= _showAtTick) Show();
			}
			else if (_state != null && (Main.GameUpdateCount >= _hideAtTick || bossNear)) Hide();
		}

		private static void Show()
		{
			var v = _pending; _pending = null; if (v == null) return;
			Current = v; Shown++; _lastShown = Now;
			if (TestMode || Main.dedServ || _ui == null) return;
			try
			{
				_state = new PopupState(v); _state.Build(); _ui.SetState(_state); _hideAtTick = Main.GameUpdateCount + LifeTicks;
				Levy.Instance?.Logger.Info("[PerfAnalyzer] lag popup shown: " + string.Join(" ", v.Detail));
			}
			catch (Exception e) { Levy.Instance?.Logger.Warn("[PerfAnalyzer] lag popup could not be drawn: " + e.Message); _state = null; }
		}

		internal static void Hide() { try { _ui?.SetState(null); } catch (Exception) { } _state = null; Current = null; }

		public override void UpdateUI(GameTime gameTime)
		{
			if (Main.dedServ || _ui == null || _state == null) return;
			try
			{
				if (_state.Panel != null && _state.Panel.ContainsPoint(Main.MouseScreen)) Main.LocalPlayer.mouseInterface = true; // not modal: only the window itself takes the click
				_ui.Update(gameTime);
			}
			catch (Exception e) { Levy.Instance?.Logger.Warn("[PerfAnalyzer] lag popup update failed: " + e.Message); Hide(); }
		}

		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
		{
			int mouse = layers.FindIndex(l => l.Name.Equals("Vanilla: Mouse Text"));
			if (mouse < 0) return;
			layers.Insert(mouse, new LegacyGameInterfaceLayer("Levy: Lag Popup", () =>
			{
				try { if (_state != null && _ui?.CurrentState != null) _ui.Draw(Main.spriteBatch, new GameTime()); }
				catch (Exception e) { Levy.Instance?.Logger.Warn("[PerfAnalyzer] lag popup draw failed: " + e.Message); Hide(); }
				return true;
			}, InterfaceScaleType.UI));
		}

		// ------------------------------------------------------------------------------------------ what the buttons do
		internal static void Act(PopupOption o)
		{
			try
			{
				switch (o)
				{
					case PopupOption.LowerLevyEffects:
						SetEffectsReduced();
						Say("Levy effects are lowered. Nameplates are hidden and levy weapon dust is thinned. Change it in the Levy performance settings.");
						break;
					case PopupOption.ReserveBank:
						// HOOK(reserve bank): call the reserve bank here once it exists.
						break;
					case PopupOption.ItsFine: _snoozeUntil = Now.AddSeconds(PerfThresholds.FineGapSecs); break;
					case PopupOption.DontAskAgain: SetDontAsk(); Say("The lag note will not show again. Turn it back on in the Levy performance settings."); break;
					case PopupOption.OpenSettings: OpenSettings(); break;
				}
			}
			catch (Exception e) { Levy.Instance?.Logger.Warn("[PerfAnalyzer] popup action " + o + " failed: " + e.Message); }
			Hide();
		}

		private static void Say(string s) { if (!Main.dedServ) Main.NewText(s, 200, 220, 255); else Levy.Instance?.Logger.Info("[PerfAnalyzer] " + s); }

		internal static void SetEffectsReduced()
		{
			var c = PerfConfig.Get; if (c != null) { c.LevyEffects = LevyEffectsQuality.Reduced; SaveConfig(c); }
			PerfAnalyzer.ConfigChanged();
		}

		private static void SetDontAsk() { var c = PerfConfig.Get; if (c != null) { c.LagPopupDontAskAgain = true; SaveConfig(c); } }

		private static void SaveConfig(ModConfig c)
		{
			if (Main.dedServ) return;
			try { typeof(ConfigManager).GetMethod("Save", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(ModConfig) }, null)?.Invoke(null, new object[] { c }); }
			catch (Exception e) { Levy.Instance?.Logger.Warn("[PerfAnalyzer] config save failed: " + e.Message); }
		}

		/// <summary>Opens the tModLoader configuration screen for Levy's performance settings. When the game's screen cannot be reached, says where the settings are.</summary>
		private static void OpenSettings()
		{
			if (Main.dedServ) return;
			try
			{
				Type iface = typeof(ModConfig).Assembly.GetType("Terraria.ModLoader.UI.Interface");
				object cfgUi = iface?.GetField("modConfig", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(null);
				MethodInfo setMod = cfgUi?.GetType().GetMethod("SetMod", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (setMod != null && cfgUi is UIState state)
				{
					var ps = setMod.GetParameters(); var args = new object[ps.Length];
					args[0] = Levy.Instance; args[1] = PerfConfig.Get;
					for (int i = 2; i < ps.Length; i++) args[i] = ps[i].ParameterType == typeof(bool) ? (object)(i == 2 ? true : false) : null;
					setMod.Invoke(cfgUi, args);
					IngameFancyUI.CoverNextFrame(); Main.playerInventory = false; Main.editChest = false; Main.npcChatText = ""; Main.inFancyUI = true;
					Main.InGameUI.SetState(state);
					return;
				}
			}
			catch (Exception e) { Levy.Instance?.Logger.Warn("[PerfAnalyzer] could not open the settings screen: " + e.Message); }
			Say("Open Settings, Mod Configuration, Levy: Performance.");
		}

		// ------------------------------------------------------------------------------------------ the window
		private sealed class PopupState : UIState
		{
			private readonly LagVerdict _v; public UIPanel Panel;
			public PopupState(LagVerdict v) { _v = v; }

			public void Build()
			{
				RemoveAllChildren();
				var opts = _v.Options; int rows = (opts.Count + 1) / 2;
				float w = 400f;
				int bodyLines = _v.Detail.Sum(d => 1 + d.Length / 52);
				float h = 12f + 26f + bodyLines * 20f + 10f + rows * 34f + 12f;
				Panel = new UIPanel { BackgroundColor = new Color(18, 24, 50) * 0.97f };
				Panel.Width.Set(w, 0f); Panel.Height.Set(h, 0f); Panel.HAlign = 1f; Panel.VAlign = 0.32f; Panel.Left.Set(-16f, 0f); Panel.SetPadding(10f);
				Append(Panel);
				var title = new UIText(_v.Headline, 0.95f); title.Top.Set(0f, 0f); Panel.Append(title);
				var body = new UIText(string.Join("\n", _v.Detail), 0.78f) { IsWrapped = true, WrappedTextBottomPadding = 0f };
				body.Width.Set(0, 1f); body.Height.Set(bodyLines * 20f, 0f); body.Top.Set(28f, 0f); Panel.Append(body);
				float y = 28f + bodyLines * 20f + 10f;
				for (int i = 0; i < opts.Count; i++)
				{
					PopupOption o = opts[i];
					var b = new LevyButton(PerfAnalysis.Label(o), 0.78f, 184f, 28f, () => Act(o));
					b.Left.Set((i % 2) * 192f, 0f); b.Top.Set(y + (i / 2) * 34f, 0f); Panel.Append(b);
				}
				Recalculate();
			}
		}
	}
}
