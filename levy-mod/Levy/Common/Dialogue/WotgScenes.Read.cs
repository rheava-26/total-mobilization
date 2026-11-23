using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Terraria;
using Terraria.ModLoader;

namespace Levy.Common.Dialogue
{
	/// <summary>
	/// What <see cref="WotgScenes"/> reads from Wrath of the Gods (NoxusBoss), by name and reflection only (no reference; every member optional, a missing one reads as off and is named in <see cref="Problem"/>):
	///   NPCs AvatarRift, AvatarOfEmptiness, NamelessDeityBoss; on each the public CurrentState (AvatarOfEmptiness, NamelessDeityBoss) or CurrentAttack (AvatarRift) enum, read as its name.
	///   GenesisVisualsSystem.EffectActive (static; the Genesis firing that opens the rift).
	///   SolynWorldDialogueManager.DialogueInstances (private static list): Solyn's speech bubbles on screen.
	///   Every ModSystem deriving from NamelessDeitySubtitleSystem: DialogueTimer and SubtitleDisappearTime, the Nameless Deity's subtitles on screen.
	///   Mod.Call("GetBossDefeated", "avatarofemptiness" / "namelessdeity").
	/// </summary>
	public partial class WotgScenes
	{
		private static bool _resolved, _installed;
		private static Mod _mod;
		private static int _riftType = -1, _avatarType = -1, _namelessType = -1;
		private static PropertyInfo _genesisActive;
		private static FieldInfo _solynList;
		private static readonly List<(object sys, PropertyInfo timer, PropertyInfo disappear)> Subtitles = new();
		private static readonly Dictionary<Type, PropertyInfo> StateProps = new();
		/// <summary>What could not be found (empty when everything resolved, "not installed" without the mod), and what was.</summary>
		public static string Problem { get; private set; } = "";
		public static string Found { get; private set; } = "";

		public static bool Installed { get { Resolve(); return _installed; } }
		internal static int RiftType { get { Resolve(); return _riftType; } }
		internal static int AvatarType { get { Resolve(); return _avatarType; } }
		internal static int NamelessType { get { Resolve(); return _namelessType; } }

		private static void Resolve()
		{
			if (_resolved) return;
			_resolved = true;
			try
			{
				if (!ModLoader.TryGetMod("NoxusBoss", out _mod) || _mod == null) { Problem = "not installed"; return; }
				_installed = true;
				var problems = new System.Text.StringBuilder(); var found = new System.Text.StringBuilder();
				if (_mod.TryFind("AvatarRift", out ModNPC r)) { _riftType = r.Type; found.Append("AvatarRift, "); } else problems.Append("AvatarRift missing; ");
				if (_mod.TryFind("AvatarOfEmptiness", out ModNPC a)) { _avatarType = a.Type; found.Append("AvatarOfEmptiness, "); } else problems.Append("AvatarOfEmptiness missing; ");
				if (_mod.TryFind("NamelessDeityBoss", out ModNPC n)) { _namelessType = n.Type; found.Append("NamelessDeityBoss, "); } else problems.Append("NamelessDeityBoss missing; ");
				const BindingFlags S = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, I = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
				Type gen = _mod.Code.GetType("NoxusBoss.Core.Graphics.GenesisEffects.GenesisVisualsSystem");
				_genesisActive = gen?.GetProperty("EffectActive", S);
				if (_genesisActive?.PropertyType == typeof(bool)) found.Append("GenesisVisualsSystem.EffectActive, "); else { _genesisActive = null; problems.Append("GenesisVisualsSystem.EffectActive missing; "); }
				Type sol = _mod.Code.GetType("NoxusBoss.Content.NPCs.Bosses.Avatar.SpecificEffectManagers.SolynDialogue.SolynWorldDialogueManager");
				_solynList = sol?.GetField("DialogueInstances", S);
				if (_solynList != null && typeof(ICollection).IsAssignableFrom(_solynList.FieldType)) found.Append("SolynWorldDialogueManager.DialogueInstances, "); else { _solynList = null; problems.Append("SolynWorldDialogueManager.DialogueInstances missing; "); }
				foreach (ModSystem sys in _mod.GetContent<ModSystem>())
				{
					for (Type t = sys.GetType().BaseType; t != null && t != typeof(ModSystem); t = t.BaseType)
					{
						if (t.Name != "NamelessDeitySubtitleSystem") continue;
						PropertyInfo timer = t.GetProperty("DialogueTimer", I), dis = t.GetProperty("SubtitleDisappearTime", I);
						if (timer != null && dis != null) Subtitles.Add((sys, timer, dis));
						break;
					}
				}
				found.Append(Subtitles.Count).Append(" subtitle systems");
				Problem = problems.ToString().TrimEnd(' ', ';');
				Found = found.ToString();
			}
			catch (Exception e) { Problem = "resolve threw " + e.GetType().Name + ": " + e.Message; }
		}

		private static T Read<T>(PropertyInfo p, object target, T fallback)
		{
			if (p == null) return fallback;
			try { return p.GetValue(target) is T v ? v : fallback; } catch (Exception) { return fallback; }
		}

		/// <summary>The name of a NoxusBoss boss's current state (CurrentState, else CurrentAttack), or null.</summary>
		internal static string StateOf(NPC n)
		{
			if (n?.ModNPC == null) return null;
			Type t = n.ModNPC.GetType();
			if (!StateProps.TryGetValue(t, out PropertyInfo p))
			{
				p = t.GetProperty("CurrentState", BindingFlags.Instance | BindingFlags.Public) ?? t.GetProperty("CurrentAttack", BindingFlags.Instance | BindingFlags.Public);
				if (p != null && !p.PropertyType.IsEnum) p = null;
				StateProps[t] = p;
			}
			if (p == null) return null;
			try { return p.GetValue(n.ModNPC)?.ToString(); } catch (Exception) { return null; }
		}

		private static bool CallDefeated(string boss)
		{
			Resolve();
			if (_mod == null) return false;
			try { return _mod.Call("GetBossDefeated", boss) is bool b && b; } catch (Exception) { return false; }
		}

		private static bool SolynTalking()
		{
			if (_solynList == null) return false;
			try { return _solynList.GetValue(null) is ICollection c && c.Count > 0; } catch (Exception) { return false; }
		}

		private static bool SubtitlesShowing()
		{
			foreach (var (sys, timer, dis) in Subtitles)
			{
				try
				{
					int t = (int)timer.GetValue(sys); float d = Convert.ToSingle(dis.GetValue(sys));
					if (t > 0 && t < d) return true;
				}
				catch (Exception) { }
			}
			return false;
		}
	}
}
