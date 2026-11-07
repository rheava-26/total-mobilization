using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.Achievements;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Systems;

namespace Levy.Common.Progression
{
	/// <summary>
	/// Orb loot scaling. Vanilla rolls an orb's drops inline inside WorldGen.CheckOrb (and mods add to them through TileLoader.Drop), so the pool is read by
	/// running that same code as a dry run: before the real smash, CheckOrb is replayed N times with every side effect muted (the tile is not killed, items are
	/// captured instead of spawned, no chat, sound or achievement, orb counter restored). If the real call then drops items, the captured rolls are spawned at
	/// the orb. Meteors, the orb counter and the goblin army are untouched because only the real call runs them.
	/// </summary>
	public class OrbLootSystem : ModSystem
	{
		struct Cap { public IEntitySource Src; public int X, Y, W, H, Type, Stack, Prefix; public Item Clone; }

		public static bool Simulating;
		static bool _real; static int _realDrops;
		static List<Cap> _cur;
		/// <summary>Self-check hook: forces the levy count read for the nearest player.</summary>
		public static int LeviesOverride = -1;
		public static int LastBankRolls, LastBankItems;

		public static int ExtraRolls(int levies, int perRoll) => perRoll < 1 || levies < 0 ? 0 : levies / perRoll;

		static int ActiveLevies(int tx, int ty)
		{
			if (LeviesOverride >= 0) return LeviesOverride;
			var roster = RosterSystem.Instance; if (roster == null) return 0;
			int best = -1; float bd = float.MaxValue;
			for (int p = 0; p < Main.maxPlayers; p++)
			{
				var pl = Main.player[p]; if (pl == null || !pl.active) continue;
				float d = Math.Abs(pl.Center.X - tx * 16f) + Math.Abs(pl.Center.Y - ty * 16f);
				if (d < bd) { bd = d; best = p; }
			}
			if (best < 0) return 0;
			int n = 0; foreach (var r in roster.RegularsOf(Main.player[best])) if (!r.Reserve) n++;
			return n;
		}

		public override void Load()
		{
			On_WorldGen.CheckOrb += CheckOrbDetour;
			On_WorldGen.KillTile += KillTileDetour;
			On_Item.NewItem_Inner += InnerDetour;
			On_Terraria_Chat_ChatHelper_BroadcastChatMessage_Safe();
		}

		static void On_Terraria_Chat_ChatHelper_BroadcastChatMessage_Safe()
		{
			Terraria.Chat.On_ChatHelper.BroadcastChatMessage += (orig, text, color, excl) => { if (Simulating) return; orig(text, color, excl); };
			On_Main.NewText_string_byte_byte_byte += (orig, s, r, g, b) => { if (Simulating) return; orig(s, r, g, b); };
			On_AchievementsHelper.NotifyProgressionEvent += (orig, id) => { if (Simulating) return; orig(id); };
			Terraria.Audio.On_SoundEngine.PlaySound_refSoundStyle_Nullable1_SoundUpdateCallback += (Terraria.Audio.On_SoundEngine.orig_PlaySound_refSoundStyle_Nullable1_SoundUpdateCallback orig, ref Terraria.Audio.SoundStyle st, Microsoft.Xna.Framework.Vector2? pos, Terraria.Audio.SoundUpdateCallback cb) => Simulating ? default(ReLogic.Utilities.SlotId) : orig(ref st, pos, cb);
		}

		static void KillTileDetour(On_WorldGen.orig_KillTile orig, int i, int j, bool fail, bool effectOnly, bool noItem)
		{
			if (Simulating) return;
			orig(i, j, fail, effectOnly, noItem);
		}

		static void Mod_Log(string s) { ModContent.GetInstance<Levy>().Logger.Info("[OrbLoot] " + s); }

		static int InnerDetour(On_Item.orig_NewItem_Inner orig, IEntitySource src, int X, int Y, int W, int H, Item clone, int type, int stack, bool noBroadcast, int pfix, bool noGrab, bool reverse)
		{
			if (Simulating) { _cur?.Add(new Cap { Src = src, X = X, Y = Y, W = W, H = H, Type = type, Stack = stack, Prefix = pfix, Clone = clone?.Clone() }); return Main.maxItems; }
			if (_real) _realDrops++;
			return orig(src, X, Y, W, H, clone, type, stack, noBroadcast, pfix, noGrab, reverse);
		}

		static void CheckOrbDetour(On_WorldGen.orig_CheckOrb orig, int i, int j, int type)
		{
			if (Simulating || _real || Main.netMode == NetmodeID.MultiplayerClient) { orig(i, j, type); return; }
			var cfg = LevyProgressionConfig.Get;
			int extra = cfg != null && cfg.OrbLootScaling ? ExtraRolls(ActiveLevies(i, j), cfg.LeviesPerExtraOrbRoll) : 0;
			List<List<Cap>> bank = null;
			if (extra > 0)
			{
				bank = new List<List<Cap>>();
				int sc = WorldGen.shadowOrbCount; bool ss = WorldGen.shadowOrbSmashed; bool dobj = WorldGen.destroyObject;
				Simulating = true;
				try
				{
					for (int k = 0; k < extra; k++)
					{
						_cur = new List<Cap>();
						WorldGen.shadowOrbCount = Main.rand.Next(2);   // the counter after the sim stays below the boss threshold
						orig(i, j, type);
						if (_cur.Count > 0) bank.Add(_cur);
					}
				}
				catch (Exception e) { ModContent.GetInstance<Levy>().Logger.Warn("[OrbLoot] dry run failed: " + e.Message); bank.Clear(); }
				finally { Simulating = false; _cur = null; WorldGen.shadowOrbCount = sc; WorldGen.shadowOrbSmashed = ss; WorldGen.destroyObject = dobj; }
			}
			_real = true; _realDrops = 0;
			try { orig(i, j, type); } finally { _real = false; }
			if (bank == null || _realDrops == 0) return;
			LastBankRolls = 0; LastBankItems = 0;
			foreach (var roll in bank)
			{
				LastBankRolls++;
				foreach (var c in roll) { LastBankItems++; if (c.Clone != null) Item.NewItem(c.Src, c.X, c.Y, c.W, c.H, c.Clone); else Item.NewItem(c.Src, c.X, c.Y, c.W, c.H, c.Type, c.Stack, false, c.Prefix); }
			}
		}

		public override void Unload()
		{
			try { On_WorldGen.CheckOrb -= CheckOrbDetour; On_WorldGen.KillTile -= KillTileDetour; On_Item.NewItem_Inner -= InnerDetour; } catch { }
		}
	}
}
