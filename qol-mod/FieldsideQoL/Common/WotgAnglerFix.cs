using System;
using System.Reflection;
using MonoMod.RuntimeDetour;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace FieldsideQoL.Common
{
	/// <summary>
	/// Angler fix for Calamity: Wrath of the Gods (mod name "NoxusBoss"), as a soft dependency: everything is looked up by name at runtime, nothing references NoxusBoss.dll.
	///
	/// WHAT WotG DOES. <c>NoxusBoss.Core.World.GameScenes.RiftEclipse.RiftEclipseManagementSystem</c> IL-edits <c>Main.AnglerQuestSwap</c> ("Limit Angler Quests During Rift Eclipse",
	/// <c>LimitQuestFishDuringRiftEclipse</c>). The edit sits after the vanilla roll loop, just before <c>NetMessage.SendAnglerQuest(-1)</c>, and when the static property
	/// <c>RiftEclipseOngoing</c> is true it overwrites <c>Main.anglerQuest</c> with the index of <c>DecideRandomQuestFish()</c>, which always returns the Veilray item.
	/// The vanilla roll before that point is untouched and already runs through <c>ItemLoader.IsAnglerQuestAvailable</c>, which makes Veilray itself a legal pick only during the rift
	/// (<c>Veilray.IsAnglerQuestAvailable =&gt; RiftEclipseOngoing</c>).
	///
	/// WHAT THIS DOES. Two small hooks, no IL: a detour on <c>Main.AnglerQuestSwap</c> and a detour on <c>ItemLoader.IsAnglerQuestAvailable</c> that only watches. During a swap the watcher
	/// remembers the last quest fish the vanilla loop accepted, which is the natural roll. After <c>AnglerQuestSwap</c> has run (with WotG's override applied), if the option is on and the rift
	/// is on, the natural roll is put back and re-sent to clients. Veilray stays one entry of the rotation because the natural roll can pick it. WotG's code, progression and data are not modified.
	/// With the option off, or without WotG, the detours do nothing.
	/// </summary>
	public class WotgAnglerFix : ModSystem
	{
		public const string WotgName = "NoxusBoss";
		const string RiftSystemType = "NoxusBoss.Core.World.GameScenes.RiftEclipse.RiftEclipseManagementSystem";
		const string RiftProperty = "RiftEclipseOngoing";
		const string VeilrayName = "Veilray";

		delegate void OrigIsAnglerQuestAvailable(int itemID, ref bool notAvailable);
		delegate void HookIsAnglerQuestAvailable(OrigIsAnglerQuestAvailable orig, int itemID, ref bool notAvailable);

		static Func<bool> _riftOngoing;
		static Hook _watchHook;
		static bool _hooked;
		static bool _tracking;
		static int _lastAccepted = -1;

		/// <summary>True once WotG was found and the hooks are installed.</summary>
		public static bool Active => _hooked;
		/// <summary>The Veilray item type, or -1 when WotG is not loaded.</summary>
		public static int VeilrayType { get; private set; } = -1;
		/// <summary>True while WotG's rift eclipse is running (false without WotG).</summary>
		public static bool RiftOngoing => _riftOngoing != null && _riftOngoing();

		public override void PostSetupContent()
		{
			_hooked = false;
			VeilrayType = -1;
			if (!ModLoader.TryGetMod(WotgName, out Mod wotg))
			{
				Mod.Logger.Info("Angler fix: Wrath of the Gods is not loaded, nothing to do.");
				return;
			}
			try
			{
				Type rift = wotg.Code?.GetType(RiftSystemType);
				MethodInfo getter = rift?.GetProperty(RiftProperty, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetGetMethod(true);
				if (getter == null || !wotg.TryFind(VeilrayName, out ModItem veilray))
				{
					Mod.Logger.Warn($"Angler fix: Wrath of the Gods {wotg.Version} does not have {RiftSystemType}.{RiftProperty} or the {VeilrayName} item. The fix stays off.");
					return;
				}
				_riftOngoing = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), getter);
				VeilrayType = veilray.Type;

				MethodInfo isAvailable = typeof(ItemLoader).GetMethod("IsAnglerQuestAvailable", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
				if (isAvailable == null)
				{
					Mod.Logger.Warn("Angler fix: ItemLoader.IsAnglerQuestAvailable was not found. The fix stays off.");
					return;
				}
				_watchHook = new Hook(isAvailable, new HookIsAnglerQuestAvailable(WatchIsAnglerQuestAvailable));
				On_Main.AnglerQuestSwap += DetourAnglerQuestSwap;
				_hooked = true;
				Mod.Logger.Info($"Angler fix: hooked Main.AnglerQuestSwap for Wrath of the Gods {wotg.Version} (Veilray item type {VeilrayType}).");
			}
			catch (Exception e)
			{
				Mod.Logger.Warn("Angler fix: could not hook Wrath of the Gods, the fix stays off. " + e.Message);
			}
		}

		public override void Unload()
		{
			if (_hooked) On_Main.AnglerQuestSwap -= DetourAnglerQuestSwap;
			_watchHook?.Dispose();
			_watchHook = null;
			_riftOngoing = null;
			_hooked = false;
			_tracking = false;
			VeilrayType = -1;
		}

		static void WatchIsAnglerQuestAvailable(OrigIsAnglerQuestAvailable orig, int itemID, ref bool notAvailable)
		{
			orig(itemID, ref notAvailable);
			// the vanilla loop sets Main.anglerQuest to the candidate right before this call and stops at the first candidate nobody vetoes
			if (_tracking && !notAvailable) _lastAccepted = Main.anglerQuest;
		}

		static void DetourAnglerQuestSwap(On_Main.orig_AnglerQuestSwap orig)
		{
			if (!_hooked || Main.netMode == NetmodeID.MultiplayerClient)
			{
				orig();
				return;
			}
			_lastAccepted = -1;
			_tracking = true;
			try { orig(); }
			finally { _tracking = false; }
			RestoreNaturalRoll();
		}

		static void RestoreNaturalRoll()
		{
			var cfg = AnglerConfig.Get;
			if (cfg == null || !cfg.NormalQuestsDuringRift) return;
			if (_lastAccepted < 0 || _lastAccepted >= Main.anglerQuestItemNetIDs.Length) return;
			if (Main.anglerQuest == _lastAccepted) return;
			if (!RiftOngoing) return;
			Main.anglerQuest = _lastAccepted;
			if (Main.netMode == NetmodeID.Server) NetMessage.SendAnglerQuest(-1);
		}
	}
}
