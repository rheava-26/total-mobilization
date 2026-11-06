using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Compat.Thorium;
using Levy.Common.Systems;
using Levy.Common.Util;
using Levy.Content.NPCs;

namespace Levy.Common.Team
{
	/// <summary>
	/// TEAM ITEMS: the vanilla and mod items that affect OTHER players (teammates), made to reach a commander's levies, and a levy's own copy reaching the commander and the other levies.
	/// Allies are uniform here, as everywhere else in the compat: the commander player and every levy that serves him.
	/// <para/>This file is the worn-item AURA engine. Those items all do the same thing: "while worn, give every teammate (in some reach) buff B for a short time" (vanilla Paladin's Shield, Calamity's
	/// Rampart of Deities, Trinket of Chi, Affliction). The wearer is read off the FLAG the item sets on its wearer's Player or ModPlayer (the commander's own Player, or a levy's equipment dummy, which runs the same
	/// UpdateEquips) so nothing is listed per item: every item that sets <c>hasPaladinShield</c> or <c>trinketOfChi</c> counts, the combined accessories included. A pulse every <see cref="LevyConstants.TeamPulseTicks"/> ticks
	/// (vanilla's own cadence) refreshes the buff on every ally in reach: a levy gets it as an NPC buff (its stat side is applied on the dummy, see <see cref="ApplyStatBuff"/>), the commander as a real player buff.
	/// Server / single player only; the commander's buff reaches his own client by <see cref="TeamNet"/>.
	/// </summary>
	public static class TeamItems
	{
		/// <summary>One worn-item aura: whoever wears an item that raises <see cref="Flag"/> gives every ally within <see cref="RangePx"/> (0 = anywhere) the buff.</summary>
		public sealed class AuraDef
		{
			public string Id, Source;
			public Func<Player, bool> Flag;
			public int Buff;
			public int Ticks = LevyConstants.TeamBuffTicks;
			public float RangePx;
			/// <summary>The wearer must be above a quarter of his life (Paladin's Shield).</summary>
			public bool NeedLife;
		}

		public static readonly List<AuraDef> Auras = new();
		/// <summary>Mod buffs whose stat side runs through the mod's own ModBuff.Update on a levy's dummy (see <see cref="ApplyStatBuff"/>).</summary>
		public static readonly HashSet<int> StatBuffs = new();
		/// <summary>The subset of <see cref="StatBuffs"/> whose flag is read by the mod's later hooks, so a levy holding one runs the per-tick gear frame (Calamity's). Thorium's buffs set stats directly and need none.</summary>
		public static readonly HashSet<int> FrameBuffs = new();
		/// <summary>Projectile auras mirrored onto levies: projectile type -> definition.</summary>
		public static readonly Dictionary<int, ProjAura> ProjAuras = new();
		public static bool Built { get; private set; }
		/// <summary>Harness only: the headless server's stand-in player counts as the local player (a dedicated server has none).</summary>
		public static bool HarnessLocal;
		/// <summary>Harness only: runs right before each pulse and plays the commander's Player.Update equip pass (the headless stand-in is not updated by the server loop).</summary>
		public static Action HarnessPrePulse;
		/// <summary>True when <paramref name="p"/> is the player this machine plays (the owner's client runs a player's hurt, pickup and projectile-hit reactions).</summary>
		public static bool IsLocal(Player p) => p != null && (HarnessLocal || p.whoAmI == Main.myPlayer);
		public static string BuildReport = "";

		/// <summary>A projectile that buffs (and/or cleanses) every player near it: the same radius, buff and duration, laid on the casting commander's levies too.</summary>
		public sealed class ProjAura
		{
			public string Name;
			public Func<Projectile, float> Radius;
			public int Buff;
			public int Ticks;
			public bool Cleanse;
			/// <summary>Once per ally per projectile (the projectile keeps a list), not on every tick.</summary>
			public bool Once;
			/// <summary>The projectile only buffs while this reads true (Death Valley Duster: ai[2] == 1).</summary>
			public Func<Projectile, bool> Active;
		}

		// ------------------------------------------------------------------------------------------------------------
		// Registry
		// ------------------------------------------------------------------------------------------------------------
		public static void Build()
		{
			Auras.Clear(); StatBuffs.Clear(); FrameBuffs.Clear(); ProjAuras.Clear();
			int added = 0, missing = 0;
			void Aura(string id, string source, Func<Player, bool> flag, int buff, float range, bool life)
			{
				if (flag == null || buff <= 0) { missing++; return; }
				Auras.Add(new AuraDef { Id = id, Source = source, Flag = flag, Buff = buff, RangePx = range, NeedLife = life });
				added++;
			}
			// Vanilla: Paladin's Shield / Frozen Shield / Hero's Shield, and Calamity's Rampart of Deities (it raises the same hasPaladinShield in its PostUpdateEquips).
			Aura("paladin", "Paladin's Shield family, Rampart of Deities", p => p.hasPaladinShield, BuffID.PaladinsShield, LevyConstants.TeamPaladinRangePx, true);
			// Calamity: both give a buff to every teammate in the world (no reach in the item's code).
			Aura("chi", "Trinket of Chi", ModFlag("CalamityMod", "CalamityMod.CalPlayer.CalamityPlayer", "trinketOfChi"), ModBuffType("CalamityMod/ChiRegenBuff"), 0f, false);
			Aura("affliction", "Affliction", ModFlag("CalamityMod", "CalamityMod.CalPlayer.CalamityPlayer", "affliction"), ModBuffType("CalamityMod/Afflicted"), 0f, false);
			foreach (string b in new[] { "ChiRegenBuff", "Afflicted", "AbsorberRegen", "PinkJellyRegen", "GreenJellyRegen", "SandsWindBuff", "AeolianEarthBuff", "Mushy", "AvertorBonus" })
			{
				int t = ModBuffType("CalamityMod/" + b);
				if (t > 0) { StatBuffs.Add(t); FrameBuffs.Add(t); }
			}

			// Calamity healing auras (each is a projectile that loops Main.player and buffs the players in a circle).
			void Proj(string mod, string proj, Func<Projectile, float> radius, string buff, int ticks, bool cleanse, bool once, Func<Projectile, bool> active = null)
			{
				if (!ModContent.TryFind(mod + "/" + proj, out ModProjectile mp)) { missing++; return; }
				int bt = buff == null ? 0 : ModBuffType(mod + "/" + buff);
				if (buff != null && bt <= 0) { missing++; return; }
				ProjAuras[mp.Type] = new ProjAura { Name = proj, Radius = radius, Buff = bt, Ticks = ticks, Cleanse = cleanse, Once = once, Active = active };
				added++;
			}
			Proj("CalamityMod", "AbsorberAura", _ => 310f, "AbsorberRegen", 600, true, false);
			Proj("CalamityMod", "PinkJellyAura", _ => 165f, "PinkJellyRegen", 300, false, false);
			Proj("CalamityMod", "GreenJellyAura", _ => 245f, "GreenJellyRegen", 480, false, false);
			Proj("CalamityMod", "BlueJellyAura", _ => 165f, null, 0, true, true);
			Proj("CalamityMod", "DeathValleyDusterProjectile", p => p.width * 0.5f * p.scale, "SandsWindBuff", 720, false, true, p => p.ai[2] == 1f);
			Proj("CalamityMod", "PrimordialEarthProjectile", p => p.width * 0.5f * p.scale, "SandsWindBuff", 840, false, true, p => p.ai[2] == 1f);
			Proj("CalamityMod", "PrimordialAncientProjectile", p => p.width * 0.5f * p.scale, "AeolianEarthBuff", 1080, false, true, p => p.ai[2] == 1f);
			// Thorium: the healer's Aura Defense staff gives every player within 88 px (7744 squared) 12% damage reduction for 5 s; Dream Weaver's Mask set boosts everyone's speed (see TeamAuraProjectile.OnSpawn).
			Proj("ThoriumMod", "AuraDefense", _ => 88f, "AuraDamageReduction", 300, false, false);
			foreach (string b in new[] { "AuraDamageReduction", "DistortedTimeBoost" })
			{
				int t = ModBuffType("ThoriumMod/" + b);
				if (t > 0) StatBuffs.Add(t);
			}

			Built = true;
			BuildReport = $"{Auras.Count} worn-item auras, {ProjAuras.Count} projectile auras, {StatBuffs.Count} mod stat buffs ({added} entries added, {missing} not present in this load order)";
		}

		/// <summary>Reads a bool field of a ModPlayer in another mod without a compile-time reference (Calamity is not referenced by Levy). Null when the mod or field is absent.</summary>
		public static Func<Player, bool> ModFlag(string mod, string typeName, string field)
		{
			try
			{
				if (!ModLoader.TryGetMod(mod, out Mod m) || m.Code == null) return null;
				Type t = m.Code.GetType(typeName);
				FieldInfo f = t?.GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (f == null || f.FieldType != typeof(bool)) return null;
				ModPlayer baseInstance = ModContent.GetContent<ModPlayer>().FirstOrDefault(x => x.GetType() == t);
				if (baseInstance == null) return null;
				return p => { try { return (bool)f.GetValue(p.GetModPlayer(baseInstance)); } catch (Exception) { return false; } };
			}
			catch (Exception) { return null; }
		}

		/// <summary>The type of a mod buff by full name ("CalamityMod/ChiRegenBuff"), or 0 when that mod or buff is not loaded.</summary>
		public static int ModBuffType(string fullName) => ModContent.TryFind(fullName, out ModBuff b) ? b.Type : 0;

		// ------------------------------------------------------------------------------------------------------------
		// Allies
		// ------------------------------------------------------------------------------------------------------------
		/// <summary>True when <paramref name="s"/> serves <paramref name="commander"/> (by name, else the lone local player in single player).</summary>
		public static bool Serves(SoldierNPC s, Player commander) => s?.Record != null && commander != null && (CompanionRequest.Serves(s, commander) || CompatAllies.CommanderOf(s) == commander);

		/// <summary>The living rank-and-file levies of <paramref name="commander"/> (officer avatars keep their own constant stats and are left out, as in the roster scans).</summary>
		public static List<SoldierNPC> Levies(Player commander)
		{
			var list = new List<SoldierNPC>();
			if (commander == null || RosterSystem.Instance == null) return list;
			foreach (SoldierNPC s in RosterSystem.Instance.ActiveBodies())
				if (s != null && s.IsAlive && !s.OfficerMode && Serves(s, commander)) list.Add(s);
			return list;
		}

		/// <summary>The commander of this levy as a live Player, or null.</summary>
		public static Player CommanderOf(SoldierNPC s) => CompatAllies.CommanderOf(s);

		// ------------------------------------------------------------------------------------------------------------
		// The pulse (server / single player)
		// ------------------------------------------------------------------------------------------------------------
		/// <summary>One team-item pulse over every commander. Returns how many buffs were laid (for the self-check).</summary>
		public static int Pulse()
		{
			if (!Built || LevyUtil.IsClient || Auras.Count == 0) return 0;
			Pulses++;
			HarnessPrePulse?.Invoke();
			if (HarnessPrePulse != null) PulseGcLog = (PulseGcLog + " " + Main.GameUpdateCount); if (PulseGcLog.Length > 60) PulseGcLog = PulseGcLog.Substring(PulseGcLog.Length - 40);
			int grants = 0;
			for (int pi = 0; pi < Main.maxPlayers; pi++)
			{
				Player p = Main.player[pi];
				if (HarnessPrePulse != null && pi == 0) LastPulse = "gc=" + Main.GameUpdateCount + " active=" + p.active + " dead=" + p.dead;
				if (p == null || !p.active || p.dead) continue;
				List<SoldierNPC> mine = Levies(p);
				if (HarnessPrePulse != null) LastPulse = "gc=" + Main.GameUpdateCount + " cmd flags=" + string.Join(",", Auras.ConvertAll(a => a.Id + "=" + SafeFlag(a, p))) + " levies=" + mine.Count + " levyFlags=" + string.Join(";", mine.ConvertAll(m => string.Join(",", Auras.ConvertAll(a => LevyProvides(a, m).ToString()))));
				if (mine.Count == 0) continue;
				for (int a = 0; a < Auras.Count; a++)
				{
					AuraDef def = Auras[a];
					// the commander wears it: every levy in reach gets the buff
					if (SafeFlag(def, p))
						for (int i = 0; i < mine.Count; i++)
							if (InReach(def, p.Center, mine[i].NPC.Center) && GrantLevy(mine[i], def.Buff, def.Ticks)) grants++;
					// a levy wears it: the commander and every other levy in reach get the buff
					for (int i = 0; i < mine.Count; i++)
					{
						SoldierNPC w = mine[i];
						if (!LevyProvides(def, w)) continue;
						if (InReach(def, w.NPC.Center, p.Center) && GrantPlayer(p, def.Buff, def.Ticks)) grants++;
						for (int j = 0; j < mine.Count; j++)
							if (j != i && InReach(def, w.NPC.Center, mine[j].NPC.Center) && GrantLevy(mine[j], def.Buff, def.Ticks)) grants++;
					}
				}
			}
			TotalGrants += grants;
			return grants;
		}

		/// <summary>Self-check / harness counters: pulses run, and buffs newly laid.</summary>
		public static int Pulses, TotalGrants;
		/// <summary>Harness: what the last pulse read (provider flags per aura).</summary>
		public static string LastPulse = "", PulseGcLog = "";
		public static int GrantCalls, DreamWeaverGrants;

		private static bool SafeFlag(AuraDef def, Player p)
		{
			try { return def.Flag(p); } catch (Exception) { return false; }
		}

		private static bool LevyProvides(AuraDef def, SoldierNPC w)
		{
			if (w.Dummy == null || !SafeFlag(def, w.Dummy)) return false;
			return !def.NeedLife || w.NPC.life > w.NPC.lifeMax * LevyConstants.PaladinHealthFloor;
		}

		private static bool InReach(AuraDef def, Vector2 from, Vector2 to) => def.RangePx <= 0f || Vector2.DistanceSquared(from, to) < def.RangePx * def.RangePx;

		/// <summary>Lay buff <paramref name="buff"/> on a levy (refreshing, never stacking) and flag his stats for a recompute the first time. Returns true when it was newly applied.</summary>
		public static bool GrantLevy(SoldierNPC s, int buff, int ticks)
		{
			if (s == null || !s.NPC.active || buff <= 0 || ticks <= 0) return false;
			GrantCalls++;
			bool had = s.NPC.HasBuff(buff);
			s.NPC.buffImmune[buff] = false; // an ally's buff is never resisted (Calamity makes town-like NPCs immune to regular buffs; see SoldierNPC.AllowFriendlyBuffs)
			s.NPC.AddBuff(buff, ticks);
			if (!had) s.StatsDirty = true;
			return !had;
		}

		/// <summary>Lay a buff on the commander player: directly in single player, by packet to his own client in multiplayer (a player's buffs belong to that player's machine).</summary>
		public static bool GrantPlayer(Player p, int buff, int ticks)
		{
			if (p == null || !p.active || p.dead || buff <= 0 || ticks <= 0) return false;
			bool had = p.HasBuff(buff);
			TeamNet.Buff(p, buff, ticks);
			return !had;
		}

		// ------------------------------------------------------------------------------------------------------------
		// A levy's stat side of a mod buff
		// ------------------------------------------------------------------------------------------------------------
		/// <summary>True while this NPC holds a mod stat buff (its effect needs the per-tick gear frame, since the mod reads the flag in a later hook).</summary>
		public static bool HoldsStatBuff(NPC npc)
		{
			if (FrameBuffs.Count == 0) return false;
			for (int i = 0; i < NPC.maxBuffs; i++) if (npc.buffType[i] > 0 && npc.buffTime[i] > 0 && FrameBuffs.Contains(npc.buffType[i])) return true;
			return false;
		}

		/// <summary>
		/// The mod's own <c>ModBuff.Update(Player)</c> on the dummy, so the buff does exactly what it does to a player (Calamity's just raise a flag on its ModPlayer that the rest of its hooks read: its life regeneration is added
		/// in its PostUpdateEquips, which the gear frame runs right after the buffs, and SoldierNPC.GearFrame turns what the mods add to lifeRegen into the levy's regeneration). Run through a scratch buff slot so a buff
		/// that reads its own index finds a real one. True when the type is one of ours.
		/// </summary>
		public static bool ApplyStatBuff(Player d, int type)
		{
			if (!StatBuffs.Contains(type)) return false;
			ModBuff mb = BuffLoader.GetBuff(type);
			if (mb == null) return false;
			int slot = d.buffType.Length - 1;
			int oldType = d.buffType[slot], oldTime = d.buffTime[slot];
			d.buffType[slot] = type; d.buffTime[slot] = 60;
			try { int idx = slot; mb.Update(d, ref idx); }
			catch (Exception e) { WarnOnce("buff " + mb.FullName, e); }
			finally { d.buffType[slot] = oldType; d.buffTime[slot] = oldTime; }
			if (mb.Mod.Name == "ThoriumMod") d.endurance += ThoriumEndurance(d); // Thorium keeps its damage reduction in its own ModPlayer field, read by its hurt hook (which a levy never runs)
			return true;
		}

		private static FieldInfo _thEnd; private static ModPlayer _thBase; private static bool _thTried;
		/// <summary>ThoriumPlayer.thoriumEndurance (damage reduction its buffs add) on this dummy, then cleared; 0 when Thorium is absent.</summary>
		private static float ThoriumEndurance(Player d)
		{
			try
			{
				if (!_thTried)
				{
					_thTried = true;
					if (ModLoader.TryGetMod("ThoriumMod", out Mod m) && m.Code != null)
					{
						Type t = m.Code.GetType("ThoriumMod.ThoriumPlayer");
						_thEnd = t?.GetField("thoriumEndurance", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
						_thBase = ModContent.GetContent<ModPlayer>().FirstOrDefault(x => x.GetType() == t);
					}
				}
				if (_thEnd == null || _thBase == null) return 0f;
				ModPlayer mp = d.GetModPlayer(_thBase);
				float v = (float)_thEnd.GetValue(mp);
				_thEnd.SetValue(mp, 0f);
				return Math.Max(0f, v);
			}
			catch (Exception) { return 0f; }
		}

		private static readonly HashSet<string> _warned = new();
		public static void WarnOnce(string what, Exception e)
		{
			if (_warned.Add(what)) Levy.Instance?.Logger.Warn("Levy team items: " + what + " failed (once): " + e.Message);
		}
	}
}
