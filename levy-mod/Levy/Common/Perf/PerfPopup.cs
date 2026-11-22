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
	/// When Levy's share was large: "Lower levy effects" (the Army effects setting: nameplates hidden, levy weapon dust thinned), the active army size when one would help (a stepper set to the suggested size, then
	/// "Set active army size"), It's fine, Don't ask again, Open Levy settings. When the cost was another mod's or the base game's: only the last three.
	/// Nothing here touches the army's command points or takes a soldier from it: the active army size only sets how many are in the field at once, and only when the player presses it.
	/// THE LIVE ARMY NOTICE (<see cref="CheckLive"/>): once a session, during a fight whose updates stay over budget because of the army, the same window says so with the suggested size (Set it / settings / Don't ask again).
	/// It never shows during a fight (a pending popup is dropped the moment a boss is near again), shows at most once in ten minutes (half an hour after "It's fine"), and stays away for good after "Don't ask again".
	/// The window is not modal: the game goes on under it, and it goes away by itself after a minute.
	/// </summary>
	public class PerfPopup : ModSystem
	{
		/// <summary>Self-check: act on a dedicated server (nothing is drawn), and stand-ins for the clock and the settings.</summary>
		internal static bool TestMode; internal static DateTime? TestNow; internal static bool? TestEnabled, TestDontAsk;
		/// <summary>Popups made visible this session, and the last verdict shown (self-check counters).</summary>
		internal static int Shown; internal static LagVerdict Current; internal static string LastRefusal;
		/// <summary>The live army notice was shown this session (it shows once), and the size the popup's stepper holds.</summary>
		internal static bool ArmyNoticeShown; internal static int ArmyStep;
		internal static bool? TestArmyDontAsk;
		private static bool _live;
		/// <summary>Self-check: the popup's clock and counters, saved and given back by a check that shows a popup, so the next check starts from the same state.</summary>
		internal static (DateTime last, DateTime snooze, int shown, bool notice) TestState { get => (_lastShown, _snoozeUntil, Shown, ArmyNoticeShown); set { _lastShown = value.last; _snoozeUntil = value.snooze; Shown = value.shown; ArmyNoticeShown = value.notice; _pending = null; } }
		private static bool ArmyDontAsk { get { if (TestArmyDontAsk.HasValue) return TestArmyDontAsk.Value; try { return PerfConfig.Get?.ArmyNoticeDontAskAgain ?? false; } catch (Exception) { return false; } } }
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
			else if (_state != null && (Main.GameUpdateCount >= _hideAtTick || (bossNear && !_live))) Hide(); // the live army notice is about the fight: it stays through it
		}

		/// <summary>
		/// The live army notice: called as each analyzer window closes. Once a session, unless "Don't ask again", when <see cref="PerfAnalysis.ArmyNotice"/> finds the window's updates over budget because of the army,
		/// the notice shows at once (it replaces a post-fight popup that is waiting). <paramref name="mine"/>: the local player's own soldiers in the field.
		/// </summary>
		internal static LagVerdict CheckLive(PerfWindow w, int mine)
		{
			if (ArmyNoticeShown || ArmyDontAsk) return null;
			if (Main.dedServ && !TestMode) return null;
			var v = PerfAnalysis.ArmyNotice(w, mine);
			if (v == null) return null;
			ArmyNoticeShown = true;
			Levy.Instance?.Logger.Info($"[PerfAnalyzer] army notice: {w.UpdMs:0.0} ms an update, army {v.ArmyCostMs:0.0} ms, {mine} soldiers in the field, suggested {v.SuggestedArmy}");
			_pending = v; Show(); _live = true;
			return v;
		}

		private static void Show()
		{
			var v = _pending; _pending = null; if (v == null) return;
			Current = v; Shown++; _lastShown = Now; _live = false;
			if (v.SuggestedArmy > 0) ArmyStep = v.SuggestedArmy;
			if (TestMode || Main.dedServ || _ui == null) return;
			try
			{
				_state = new PopupState(v); _state.Build(); _ui.SetState(_state); _hideAtTick = Main.GameUpdateCount + LifeTicks;
				Levy.Instance?.Logger.Info("[PerfAnalyzer] lag popup shown: " + string.Join(" ", v.Detail));
			}
			catch (Exception e) { Levy.Instance?.Logger.Warn("[PerfAnalyzer] lag popup could not be drawn: " + e.Message); _state = null; }
		}

		internal static void Hide() { try { _ui?.SetState(null); } catch (Exception) { } _state = null; Current = null; _live = false; }

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
					case PopupOption.ActiveArmy:
						if (!TestMode) UI.UIActions.SetActiveArmy(Math.Max(1, ArmyStep)); // the roster says it in chat
						break;
					case PopupOption.ItsFine: _snoozeUntil = Now.AddSeconds(PerfThresholds.FineGapSecs); break;
					case PopupOption.DontAskAgain:
						if (Current != null && Current.Kind == LagKind.ArmyTooBig) { SetArmyDontAsk(); Say("This note will not show again. Turn it back on in the Levy performance settings."); }
						else { SetDontAsk(); Say("The lag note will not show again. Turn it back on in the Levy performance settings."); }
						break;
					case PopupOption.OpenSettings: OpenSettings(); break;
					case PopupOption.FrameSkipSubtle:
						PerfConfig.SetFrameSkip(FrameSkipChoice.Subtle);
						Say("Frame Skip is set to Subtle. In a heavy fight the game now slows down instead of dropping frames. Change it in Settings, Video, or in the Levy performance settings.");
						break;
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
		private static void SetArmyDontAsk() { if (TestMode) { TestArmyDontAsk = true; return; } var c = PerfConfig.Get; if (c != null) { c.ArmyNoticeDontAskAgain = true; SaveConfig(c); } }

		internal static void SaveConfig(ModConfig c)
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

			private LevyButton _armyBtn; private UIText _armyVal;

			public void Build()
			{
				RemoveAllChildren();
				var opts = _v.Options; int rows = (opts.Count + 1) / 2;
				float w = 400f;
				int bodyLines = _v.Detail.Sum(d => 1 + d.Length / 52);
				bool army = opts.Contains(PopupOption.ActiveArmy);
				float stepH = army ? 34f : 0f;
				float h = 12f + 26f + bodyLines * 20f + 10f + stepH + rows * 34f + 12f;
				Panel = new UIPanel { BackgroundColor = new Color(18, 24, 50) * 0.97f };
				Panel.Width.Set(w, 0f); Panel.Height.Set(h, 0f); Panel.HAlign = 1f; Panel.VAlign = 0.32f; Panel.Left.Set(-16f, 0f); Panel.SetPadding(10f);
				Append(Panel);
				var title = new UIText(_v.Headline, 0.95f); title.Top.Set(0f, 0f); Panel.Append(title);
				var body = new UIText(string.Join("\n", _v.Detail), 0.78f) { IsWrapped = true, WrappedTextBottomPadding = 0f };
				body.Width.Set(0, 1f); body.Height.Set(bodyLines * 20f, 0f); body.Top.Set(28f, 0f); Panel.Append(body);
				float y = 28f + bodyLines * 20f + 10f;
				if (army)
				{
					// the active army size, right here: - and + (Shift: 5 at a time) around the size the button will set, and the same "?" as the Barracks
					var lbl = new UIText(global::Levy.Common.Util.LevyUtil.T("UI2.ActiveArmyShort"), 0.8f); lbl.Top.Set(y + 6f, 0f); Panel.Append(lbl);
					var minus = new LevyButton("-", 0.8f, 26f, 26f, () => StepArmy(-1)); minus.Left.Set(150f, 0f); minus.Top.Set(y, 0f); Panel.Append(minus);
					_armyVal = new UIText("", 0.85f); _armyVal.Left.Set(182f, 0f); _armyVal.Top.Set(y + 5f, 0f); Panel.Append(_armyVal);
					var plus = new LevyButton("+", 0.8f, 26f, 26f, () => StepArmy(1)); plus.Left.Set(222f, 0f); plus.Top.Set(y, 0f); Panel.Append(plus);
					var help = new LevyButton("?", 0.8f, 26f, 26f, null, global::Levy.Common.Util.LevyUtil.T("UI2.ActiveArmyHelp")); help.Left.Set(254f, 0f); help.Top.Set(y, 0f); Panel.Append(help);
					y += stepH;
				}
				for (int i = 0; i < opts.Count; i++)
				{
					PopupOption o = opts[i];
					var b = new LevyButton(PerfAnalysis.Label(o), 0.78f, 184f, 28f, () => Act(o));
					b.Left.Set((i % 2) * 192f, 0f); b.Top.Set(y + (i / 2) * 34f, 0f); Panel.Append(b);
					if (o == PopupOption.ActiveArmy) _armyBtn = b;
				}
				ShowArmy();
				Recalculate();
			}

			private void StepArmy(int dir)
			{
				int step = Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.LeftShift) || Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.RightShift) ? 5 : 1;
				ArmyStep = Math.Clamp(ArmyStep + dir * step, 1, LevyConstants.ActiveArmyMax);
				ShowArmy();
			}

			private void ShowArmy()
			{
				if (_armyVal != null) _armyVal.SetText(ArmyStep.ToString());
				if (_armyBtn != null) _armyBtn.SetText(global::Levy.Common.Util.LevyUtil.T("UI2.ActiveArmySetTo", ArmyStep));
			}
		}
	}
}
