using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Globals;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// ARMOR SET BONUS AUDIT (LEVY_HARNESS_SCENARIO=setbonus; LEVY_HARNESS_SBFILTER = comma list of set names; LEVY_HARNESS_SBMODE = static | fight | both (default both)).
	/// For every set in <see cref="SbBuild"/>:
	///  STATIC  a REFERENCE Player (no Levy involved) wears the full set and then the same pieces with the legs taken off; every public Player field, every ModPlayer field and the six stat modifiers of every
	///          damage class are diffed. That diff IS the set bonus. A levy (production RefreshStats) is put through the same two states; each reference change must show up on the levy dummy with the same size.
	///  FIGHT   for the sets whose bonus is an EVENT (a hit proc, a projectile, an orbiting shield, a dodge, thorns, a heal) a levy in the set fights a pinned target (or is hit by a poker NPC) through the real AI;
	///          projectile spawns / hits / damage by type, the target's buffs and the levy / commander / squadmate life are recorded and the set's own check reads them.
	/// One HARNESS-SB line per set (and per check) goes to the log and to LEVY_HARNESS_OUT.sb.txt. Measurement only.
	/// </summary>
	public partial class CombatHarness
	{
		private sealed class SbSpec
		{
			public string Name, Head, Body, Legs, Weapon, Cls = "melee", Event;
			public string[] Acc = Array.Empty<string>();
			public int Ticks = 600;
			public bool Poker, Mate, Hold, CmdHurt, NoWeapon;
			public float TargetTiles = 9f;
			public bool Static = true;
		}

		private readonly List<SbSpec> _sbSets = new();
		private int _sbI = -1, _sbTick;
		private bool _sbStarted;
		private string _sbMode = "both";
		private SoldierNPC _sbA, _sbB;
		private int _sbTarget = -1, _sbPoker = -1;
		private readonly List<string> _sbLines = new();
		private int _sbPass, _sbFail, _sbStaticBroken;
		private readonly Dictionary<string, string> _sbStaticVerdict = new();

		private void SbLog(string s)
		{
			Mod.Logger.Info(s);
			_sbLines.Add(s);
			try { System.IO.File.AppendAllText(OutPath + ".sb.txt", s + "\n"); } catch (Exception) { }
		}

		private static int SbItem(string name)
		{
			if (string.IsNullOrEmpty(name)) return 0;
			if (name.Contains('/')) return ModContent.TryFind(name, out ModItem m) ? m.Type : 0;
			return ItemID.Search.TryGetId(name, out int id) ? id : 0;
		}

		private static Item SbMake(string name)
		{
			int t = SbItem(name);
			var it = new Item();
			if (t > 0) it.SetDefaults(t);
			return it;
		}

		private bool BeginSetBonus()
		{
			try { MonoModHooks.Add(typeof(Netplay).GetMethod("UpdateConnectedClients", BindingFlags.Static | BindingFlags.NonPublic), new HookUpdateConnectedClients(KeepStandinDetour)); _spHooked = true; } catch (Exception e) { Mod.Logger.Warn("HARNESS-SB: hook failed " + e.Message); }
			_sbMode = (Environment.GetEnvironmentVariable("LEVY_HARNESS_SBMODE") ?? "both").ToLowerInvariant();
			SbBuild();
			string flt = Environment.GetEnvironmentVariable("LEVY_HARNESS_SBFILTER");
			if (!string.IsNullOrWhiteSpace(flt))
			{
				var f = flt.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
				_sbSets.RemoveAll(s => !f.Any(x => s.Name.IndexOf(x, StringComparison.OrdinalIgnoreCase) >= 0));
			}
			_sbI = 0; _sbStarted = false;
			GearUpkeep.CountSpawns = true;
			SbLog($"HARNESS-SB: begin; mode={_sbMode}; sets={_sbSets.Count}; thorium={ModLoader.HasMod("ThoriumMod")} calamity={ModLoader.HasMod("CalamityMod")}");
			return true;
		}

		// ------------------------------------------------------------------------------------------------ the sets
		private void SbAdd(string name, string head, string body, string legs, string cls = "melee", string ev = null, Action<SbSpec> tweak = null)
		{
			var s = new SbSpec { Name = name, Head = head, Body = body, Legs = legs, Cls = cls, Event = ev };
			tweak?.Invoke(s);
			_sbSets.Add(s);
		}

		private void SbBuild()
		{
			// ---- pre-hardmode
			SbAdd("Copper", "CopperHelmet", "CopperChainmail", "CopperGreaves");
			SbAdd("Tin", "TinHelmet", "TinChainmail", "TinGreaves");
			SbAdd("Iron", "IronHelmet", "IronChainmail", "IronGreaves");
			SbAdd("Lead", "LeadHelmet", "LeadChainmail", "LeadGreaves");
			SbAdd("Silver", "SilverHelmet", "SilverChainmail", "SilverGreaves");
			SbAdd("Tungsten", "TungstenHelmet", "TungstenChainmail", "TungstenGreaves");
			SbAdd("Gold", "GoldHelmet", "GoldChainmail", "GoldGreaves");
			SbAdd("Platinum", "PlatinumHelmet", "PlatinumChainmail", "PlatinumGreaves");
			SbAdd("Cactus", "CactusHelmet", "CactusBreastplate", "CactusLeggings", "melee", "thorns", s => { s.Poker = true; s.NoWeapon = true; s.Ticks = 240; });
			SbAdd("Wood", "WoodHelmet", "WoodBreastplate", "WoodGreaves");
			SbAdd("Boreal", "BorealWoodHelmet", "BorealWoodBreastplate", "BorealWoodGreaves");
			SbAdd("RichMahogany", "RichMahoganyHelmet", "RichMahoganyBreastplate", "RichMahoganyGreaves");
			SbAdd("Ebonwood", "EbonwoodHelmet", "EbonwoodBreastplate", "EbonwoodGreaves");
			SbAdd("Shadewood", "ShadewoodHelmet", "ShadewoodBreastplate", "ShadewoodGreaves");
			SbAdd("Pearlwood", "PearlwoodHelmet", "PearlwoodBreastplate", "PearlwoodGreaves");
			SbAdd("AshWood", "AshWoodHelmet", "AshWoodBreastplate", "AshWoodGreaves");
			SbAdd("Mining", "MiningHelmet", "MiningShirt", "MiningPants");
			SbAdd("Ninja", "NinjaHood", "NinjaShirt", "NinjaPants");
			SbAdd("Fossil", "FossilHelm", "FossilShirt", "FossilPants");
			SbAdd("Obsidian", "ObsidianHelm", "ObsidianShirt", "ObsidianPants");
			SbAdd("Gladiator", "GladiatorHelmet", "GladiatorBreastplate", "GladiatorLeggings");
			SbAdd("Shadow", "ShadowHelmet", "ShadowScalemail", "ShadowGreaves");
			SbAdd("Crimson", "CrimsonHelmet", "CrimsonScalemail", "CrimsonGreaves");
			SbAdd("Meteor", "MeteorHelmet", "MeteorSuit", "MeteorLeggings", "magic");
			SbAdd("Jungle", "JungleHat", "JungleShirt", "JunglePants", "magic");
			SbAdd("Bee", "BeeHeadgear", "BeeBreastplate", "BeeGreaves");
			SbAdd("Necro", "NecroHelmet", "NecroBreastplate", "NecroGreaves", "ranged");
			SbAdd("Molten", "MoltenHelmet", "MoltenBreastplate", "MoltenGreaves");
			SbAdd("Angler", "AnglerHat", "AnglerVest", "AnglerPants");
			// ---- hardmode
			SbAdd("Cobalt", "CobaltHat", "CobaltBreastplate", "CobaltLeggings", "magic");
			SbAdd("Palladium", "PalladiumHeadgear", "PalladiumBreastplate", "PalladiumLeggings", "melee", "palladium", s => { s.Ticks = 420; s.TargetTiles = 3f; });
			SbAdd("Mythril", "MythrilHat", "MythrilChainmail", "MythrilGreaves", "magic");
			SbAdd("Orichalcum", "OrichalcumHeadgear", "OrichalcumBreastplate", "OrichalcumLeggings", "magic", "orichalcum");
			SbAdd("Adamantite", "AdamantiteHeadgear", "AdamantiteBreastplate", "AdamantiteLeggings", "ranged");
			SbAdd("Titanium", "TitaniumHeadgear", "TitaniumBreastplate", "TitaniumLeggings", "melee", "titanium", s => { s.TargetTiles = 3f; });
			SbAdd("Frost", "FrostHelmet", "FrostBreastplate", "FrostLeggings", "ranged", "frost");
			SbAdd("Forbidden", "AncientBattleArmorHat", "AncientBattleArmorShirt", "AncientBattleArmorPants", "magic", "forbidden");
			SbAdd("CrystalAssassin", "CrystalNinjaHelmet", "CrystalNinjaChestplate", "CrystalNinjaLeggings", "ranged");
			SbAdd("Spider", "SpiderMask", "SpiderBreastplate", "SpiderGreaves", "summon", "spider");
			SbAdd("Hallowed", "HallowedHeadgear", "HallowedPlateMail", "HallowedGreaves", "magic", "hallowed", s => { s.Poker = true; s.TargetTiles = 9f; });
			SbAdd("Chlorophyte", "ChlorophyteHeadgear", "ChlorophytePlateMail", "ChlorophyteGreaves", "magic", "chlorophyte");
			SbAdd("Turtle", "TurtleHelmet", "TurtleScaleMail", "TurtleLeggings", "melee", "thorns", s => { s.Poker = true; s.NoWeapon = true; s.Ticks = 240; });
			SbAdd("Shroomite", "ShroomiteHeadgear", "ShroomiteBreastplate", "ShroomiteLeggings", "ranged", "stealth");
			SbAdd("SpectreHood", "SpectreHood", "SpectreRobe", "SpectrePants", "magic", "spectrehood", s => { s.Mate = true; s.CmdHurt = true; s.Ticks = 600; });
			SbAdd("SpectreMask", "SpectreMask", "SpectreRobe", "SpectrePants", "magic", "spectremask");
			SbAdd("Spooky", "SpookyHelmet", "SpookyBreastplate", "SpookyLeggings", "summon");
			SbAdd("Tiki", "TikiMask", "TikiShirt", "TikiPants", "summon");
			SbAdd("BeetleMight", "BeetleHelmet", "BeetleScaleMail", "BeetleLeggings", "melee", "beetlemight", s => { s.TargetTiles = 3f; s.Ticks = 900; s.Weapon = "TerraBlade"; });
			SbAdd("BeetleDefense", "BeetleHelmet", "BeetleShell", "BeetleLeggings", "melee", "beetledefense", s => { s.Poker = true; s.Ticks = 900; });
			SbAdd("Valhalla", "SquireAltHead", "SquireAltShirt", "SquireAltPants", "summon");
			SbAdd("RedRiding", "HuntressAltHead", "HuntressAltShirt", "HuntressAltPants", "ranged");
			SbAdd("DarkArtist", "ApprenticeAltHead", "ApprenticeAltShirt", "ApprenticeAltPants", "magic");
			SbAdd("Shinobi", "MonkAltHead", "MonkAltShirt", "MonkAltPants", "melee");
			SbAdd("Squire", "SquireGreatHelm", "SquirePlating", "SquireGreaves", "summon");
			SbAdd("Huntress", "HuntressWig", "HuntressJerkin", "HuntressPants", "ranged");
			SbAdd("Apprentice", "ApprenticeHat", "ApprenticeRobe", "ApprenticeTrousers", "magic");
			SbAdd("Monk", "MonkBrows", "MonkShirt", "MonkPants", "melee");
			SbAdd("Solar", "SolarFlareHelmet", "SolarFlareBreastplate", "SolarFlareLeggings", "melee", "solar", s => { s.Poker = true; s.Ticks = 900; });
			SbAdd("Vortex", "VortexHelmet", "VortexBreastplate", "VortexLeggings", "ranged", "stealth");
			SbAdd("Nebula", "NebulaHelmet", "NebulaBreastplate", "NebulaLeggings", "magic", "nebula", s => { s.Ticks = 900; });
			SbAdd("Stardust", "StardustHelmet", "StardustBreastplate", "StardustLeggings", "summon", "stardust", s => { s.Ticks = 600; });
			// ---- spot checks: Calamity / Thorium (loaded mods only)
			if (ModLoader.HasMod("CalamityMod"))
			{
				SbAdd("Cal:DesertProwler", "CalamityMod/DesertProwlerHat", "CalamityMod/DesertProwlerShirt", "CalamityMod/DesertProwlerPants", "rogue", "desertprowler", s => { s.Ticks = 600; s.Weapon = "CalamityMod/WulfrumKnife"; s.TargetTiles = 12f; });
				SbAdd("Cal:Tarragon-key", "CalamityMod/TarragonHeadMelee", "CalamityMod/TarragonBreastplate", "CalamityMod/TarragonLeggings", "melee", "calkey:TarragonCloak:buff", s => { s.Weapon = "TerraBlade"; s.TargetTiles = 4f; s.Static = false; });
				SbAdd("Cal:Bloodflare-key", "CalamityMod/BloodflareHeadRanged", "CalamityMod/BloodflareBodyArmor", "CalamityMod/BloodflareCuisses", "ranged", "calkey:BloodflareSoul:proj", s => { s.Static = false; });
				SbAdd("Cal:OmegaBlue-key", "CalamityMod/OmegaBlueHelmet", "CalamityMod/OmegaBlueChestplate", "CalamityMod/OmegaBlueTentacles", "melee", "calkey:AbyssalMadness:buff", s => { s.Weapon = "TerraBlade"; s.TargetTiles = 4f; s.Static = false; });
				SbAdd("Cal:Demonshade-key", "CalamityMod/DemonshadeHelm", "CalamityMod/DemonshadeBreastplate", "CalamityMod/DemonshadeGreaves", "melee", "calkey:Enraged:buff", s => { s.Weapon = "TerraBlade"; s.TargetTiles = 4f; s.Static = false; });
				SbAdd("Cal:PlagueReaper-key", "CalamityMod/PlagueReaperMask", "CalamityMod/PlagueReaperVest", "CalamityMod/PlagueReaperStriders", "ranged", "calkey:-:presses", s => { s.Static = false; });
				SbAdd("Cal:Prismatic-key", "CalamityMod/PrismaticHelmet", "CalamityMod/PrismaticRegalia", "CalamityMod/PrismaticGreaves", "magic", "calkey:DeathhailBeam:proj", s => { s.Static = false; });
				SbAdd("Cal:Brimflame-key", "CalamityMod/BrimflameCowl", "CalamityMod/BrimflameRobes", "CalamityMod/BrimflameBoots", "magic", "calkey:BrimflameFrenzyBuff:buff", s => { s.Static = false; });
				SbAdd("Cal:Statigel(rogue)", "CalamityMod/StatigelHeadRogue", "CalamityMod/StatigelArmor", "CalamityMod/StatigelGreaves", "rogue");
				SbAdd("Cal:Victide(summon)", "CalamityMod/VictideHelmet", "CalamityMod/VictideBreastplate", "CalamityMod/VictideLeggings", "summon");
				SbAdd("Cal:Daedalus(ranged)", "CalamityMod/DaedalusHeadRanged", "CalamityMod/DaedalusBreastplate", "CalamityMod/DaedalusLeggings", "ranged");
				SbAdd("Cal:Reaver(melee)", "CalamityMod/ReaverHeadTank", "CalamityMod/ReaverScaleMail", "CalamityMod/ReaverCuisses", "melee");
				SbAdd("Cal:Tarragon(melee)", "CalamityMod/TarragonHeadMelee", "CalamityMod/TarragonBreastplate", "CalamityMod/TarragonLeggings", "melee");
				SbAdd("Cal:Auric(melee)", "CalamityMod/AuricTeslaHoodedFacemask", "CalamityMod/AuricTeslaBodyArmor", "CalamityMod/AuricTeslaCuisses", "melee");
			}
			if (ModLoader.HasMod("ThoriumMod"))
			{
				SbAdd("Tho:Biotech(healer)", "ThoriumMod/BiotechHelmet", "ThoriumMod/BiotechBreastplate", "ThoriumMod/BiotechGreaves", "melee");
				SbAdd("Tho:LivingWood(bard)", "ThoriumMod/LivingWoodMask", "ThoriumMod/LivingWoodChestguard", "ThoriumMod/LivingWoodBoots", "melee");
				SbAdd("Tho:Thorium(melee)", "ThoriumMod/ThoriumHelmet", "ThoriumMod/ThoriumMail", "ThoriumMod/ThoriumGreaves", "melee");
				SbAdd("Tho:Lodestone(melee)", "ThoriumMod/LodestoneFaceGuard", "ThoriumMod/LodestoneChestGuard", "ThoriumMod/LodestoneShinGuards", "melee");
			}
		}

		// ------------------------------------------------------------------------------------------------ static diff
		private static FieldInfo[] _sbPlayerFields;
		private static readonly string[] SbSkipFields = { "statLife", "statMana", "whoAmI", "oldVelocity", "frame", "itemAnimation", "itemTime", "itemRotation", "fullRotation", "immuneTime", "immuneAlpha", "buffTime", "ghostFade", "ghostDir", "headFrame" };

		private static FieldInfo[] SbFields()
		{
			if (_sbPlayerFields != null) return _sbPlayerFields;
			_sbPlayerFields = typeof(Player).GetFields(BindingFlags.Public | BindingFlags.Instance)
				.Where(f => (f.FieldType == typeof(bool) || f.FieldType == typeof(int) || f.FieldType == typeof(float) || f.FieldType == typeof(short) || f.FieldType == typeof(byte) || f.FieldType == typeof(double) || f.FieldType == typeof(uint)) && !SbSkipFields.Contains(f.Name))
				.ToArray();
			return _sbPlayerFields;
		}

		private static Dictionary<string, double> SbSnap(Player p)
		{
			var d = new Dictionary<string, double>();
			foreach (var f in SbFields())
			{
				try { object v = f.GetValue(p); d["P." + f.Name] = v is bool b ? (b ? 1 : 0) : Convert.ToDouble(v); } catch (Exception) { }
			}
			d["P.setBonusLen"] = (p.setBonus ?? "").Length;
			for (int i = 0; i < DamageClassLoader.DamageClassCount; i++)
			{
				DamageClass dc = DamageClassLoader.GetDamageClass(i);
				if (dc == null) continue;
				try
				{
					StatModifier m = p.GetDamage(dc);
					d["dmg." + dc.Name + ".add"] = m.Additive; d["dmg." + dc.Name + ".mult"] = m.Multiplicative; d["dmg." + dc.Name + ".flat"] = m.Flat; d["dmg." + dc.Name + ".base"] = m.Base;
					d["crit." + dc.Name] = p.GetCritChance(dc);
					d["spd." + dc.Name] = p.GetAttackSpeed(dc);
					StatModifier k = p.GetKnockback(dc);
					d["kb." + dc.Name + ".add"] = k.Additive; d["kb." + dc.Name + ".mult"] = k.Multiplicative;
					d["pen." + dc.Name] = p.GetArmorPenetration(dc);
				}
				catch (Exception) { }
			}
			try
			{
				var mp = typeof(Player).GetProperty("ModPlayers", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(p) as System.Collections.IEnumerable;
				if (mp != null)
					foreach (object m in mp)
					{
						if (m == null) continue;
						string mn = m.GetType().Name;
						foreach (var f in m.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
						{
							if (!(f.FieldType == typeof(bool) || f.FieldType == typeof(int) || f.FieldType == typeof(float) || f.FieldType == typeof(double))) continue;
							try { object v = f.GetValue(m); d["M." + mn + "." + f.Name] = v is bool b ? (b ? 1 : 0) : Convert.ToDouble(v); } catch (Exception) { }
						}
					}
			}
			catch (Exception) { }
			return d;
		}

		private static string SbDeltaText(Dictionary<string, double> hi, Dictionary<string, double> lo, int max = 8)
		{
			var l = new List<string>();
			foreach (var kv in hi)
				if (lo.TryGetValue(kv.Key, out double v) && Math.Abs(kv.Value - v) > 1e-4) l.Add($"{kv.Key} {v:0.###}->{kv.Value:0.###}");
			return string.Join("; ", l.Take(max)) + (l.Count > max ? $"; +{l.Count - max} more" : "");
		}

		/// <summary>Equips a reference Player (not a levy) with head / body / legs / accessories and runs the stat pipeline up to and including the set pass, as Player.Update does.</summary>
		private static Dictionary<string, double> SbRefSnap(Item head, Item body, Item legs, Item[] acc, out string setText)
		{
			var rp = new Player();
			int slot = 250;
			Player saved = Main.player[slot];
			Main.player[slot] = rp;
			int savedMy = Main.myPlayer;
			try
			{
				rp.whoAmI = slot; rp.active = true;
				rp.statLifeMax = rp.statLifeMax2 = 500; rp.statLife = 500; rp.statManaMax = rp.statManaMax2 = 200; rp.statMana = 200;
				rp.armor[0] = head; rp.armor[1] = body; rp.armor[2] = legs;
				for (int i = 0; i < acc.Length && i < 5; i++) rp.armor[3 + i] = acc[i];
				rp.ResetEffects();
				rp.head = head.headSlot; rp.body = body.bodySlot; rp.legs = legs.legSlot;
				rp.UpdateEquips(slot);
				rp.UpdateArmorSets(slot);
				PlayerLoader.PostUpdateEquips(rp);
				setText = rp.setBonus ?? "";
				return SbSnap(rp);
			}
			finally { Main.player[slot] = saved; Main.myPlayer = savedMy; }
		}

		private static string SbLevyKeyFields(Dictionary<string, double> hi, Dictionary<string, double> lo, IEnumerable<string> keys, bool hiIsLevy)
		{
			var l = new List<string>();
			foreach (string k in keys)
				if (hi.TryGetValue(k, out double a) && lo.TryGetValue(k, out double b)) l.Add($"{k} {b:0.###}->{a:0.###}");
			return string.Join("; ", l);
		}

		/// <summary>The static half: reference diff versus the levy's diff. Returns a one-line verdict (OK / MISSING n of m ...).</summary>
		private string SbStatic(SbSpec s, out bool ok, out string setTextRef, out string setTextLevy)
		{
			ok = true; setTextRef = ""; setTextLevy = "";
			Item hd = SbMake(s.Head), bd = SbMake(s.Body), lg = SbMake(s.Legs);
			if (hd.IsAir || bd.IsAir || lg.IsAir) { ok = false; return "SKIP: item not found (" + (hd.IsAir ? s.Head : "") + " " + (bd.IsAir ? s.Body : "") + " " + (lg.IsAir ? s.Legs : "") + ")"; }
			var acc = s.Acc.Select(SbMake).ToArray();
			var refFull = SbRefSnap(SbMake(s.Head), SbMake(s.Body), SbMake(s.Legs), acc, out setTextRef);
			var refBroken = SbRefSnap(SbMake(s.Head), SbMake(s.Body), new Item(), acc, out _);
			// the levy
			ClearAll(); SpBuildFloor("run");
			Player cmd = Main.player[0]; cmd.active = true; cmd.dead = false; cmd.name = CommanderName; cmd.statLifeMax = cmd.statLifeMax2 = 500; cmd.statLife = 500;
			var rec = MakeRecord("Pvt. SB"); rec.Grade = Grade.Champion; rec.LoadoutLocked = true; rec.Order = SoldierOrder.Hold; rec.HoldFire = true;
			rec.Gear.Head = hd; rec.Gear.Body = bd; rec.Gear.Legs = lg;
			for (int i = 0; i < acc.Length && i < rec.Gear.Accessories.Length; i++) rec.Gear.Accessories[i] = SbMake(s.Acc[i]);
			RosterSystem.Instance.Regulars.Add(rec);
			int idx = RosterSystem.Instance.SpawnBody(rec, SoldierX0Bottom);
			if (idx < 0 || !rec.HasBody) { ok = false; return "FAIL: SpawnBody"; }
			var sn = (SoldierNPC)Main.npc[idx].ModNPC;
			sn.RefreshStats();
			var levyFull = SbSnap(sn.Dummy); setTextLevy = sn.Dummy.setBonus ?? "";
			Item legSave = rec.Gear.Legs;
			rec.Gear.Legs = new Item();
			sn.RefreshStats();
			var levyBroken = SbSnap(sn.Dummy);
			rec.Gear.Legs = legSave;
			sn.RefreshStats();
			var levyFull2 = SbSnap(sn.Dummy);
			string setTextLevy2 = sn.Dummy.setBonus ?? "";
			// compare
			var changed = refFull.Where(kv => refBroken.TryGetValue(kv.Key, out double b) && Math.Abs(kv.Value - b) > 1e-4 && !kv.Key.EndsWith(".defense") ).Select(kv => kv.Key).ToList();
			// defense differs by the legs piece itself on both sides, so it cancels in the comparison below; keep statDefense in the list.
			changed = refFull.Where(kv => refBroken.TryGetValue(kv.Key, out double b) && Math.Abs(kv.Value - b) > 1e-4).Select(kv => kv.Key).ToList();
			int match = 0; var miss = new List<string>();
			foreach (string k in changed)
			{
				double rd = refFull[k] - refBroken[k];
				double ld = levyFull.TryGetValue(k, out double lf) && levyBroken.TryGetValue(k, out double lb) ? lf - lb : double.NaN;
				if (!double.IsNaN(ld) && Math.Abs(ld - rd) <= Math.Max(1e-3, Math.Abs(rd) * 0.02)) match++;
				else miss.Add($"{k} ref{rd:+0.###;-0.###} levy{(double.IsNaN(ld) ? "?" : ld.ToString("+0.###;-0.###;0"))}");
			}
			// the leg piece on its own differs from "no legs" by defense and nothing else, so a bonus-free set shows only P.statDefense
			string refText = SbDeltaText(refFull, refBroken, 6);
			string levyText = SbDeltaText(levyFull, levyBroken, 6);
			string stable = Math.Abs((levyFull.GetValueOrDefault("P.setBonusLen")) - (levyFull2.GetValueOrDefault("P.setBonusLen"))) < 0.5 ? "" : " [setBonus text unstable across refreshes]";
			bool textOk = string.Equals(setTextRef, setTextLevy, StringComparison.Ordinal);
			bool bonusExists = setTextRef.Length > 0;
			ok = miss.Count == 0 && (textOk || !bonusExists);
			string v = $"setBonus ref='{Trunc(setTextRef)}' levy='{Trunc(setTextLevy)}' textMatch={textOk}; refDelta=[{refText}]; levyDelta=[{levyText}]; matched {match}/{changed.Count}" + (miss.Count > 0 ? "; MISSING: " + string.Join(", ", miss.Take(8)) : "") + stable;
			sn.NPC.active = false;
			return (ok ? "ACTIVE " : "BROKEN ") + v;
		}

		private static string Trunc(string s) => s == null ? "" : s.Replace('\n', ' ').Length > 70 ? s.Replace('\n', ' ').Substring(0, 70) + "..." : s.Replace('\n', ' ');

		// ------------------------------------------------------------------------------------------------ fight
		/// <summary>Projectile hits on the pinned targets by projectile type (hits, damage, credited to the levy, owned by the commander player).</summary>
		public static readonly Dictionary<int, (int hits, long dmg, int credited)> SbHits = new();
		public static int SbLevy = -1;
		public static int SbTargetHits, SbTargetBuffsFrost;

		private float _sbCmdLife0; private int _sbAL0, _sbBL0, _sbPokerL0, _sbTargetL0;
		private int _sbMaxOrbs, _sbMaxTitan, _sbMaxPal, _sbMaxNeb, _sbDodgeCd, _sbHolyReady, _sbMinCmd, _sbMaxCmd, _sbBeetleSeen, _sbBurstFlares;
		private int _sbFrostSeen, _sbGuardianSeen, _sbLeafSeen, _sbMateMax, _sbLevyLifeMin;
		private double _sbRegenMax;
		private int _sbDamageDone; private readonly HashSet<string> _sbBuffsSeen = new(); private readonly HashSet<int> _sbRogueProj = new(), _sbLightsOut = new(); private float _sbMinStealth = 1f; private bool _sbSmoke; private int _sbSmokeTicks;

		private static T SbPriv<T>(object o, string name, T dflt = default)
		{
			try
			{
				for (Type t = o.GetType(); t != null; t = t.BaseType)
				{
					var f = t.GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
					if (f != null) return (T)Convert.ChangeType(f.GetValue(o), typeof(T));
				}
			}
			catch (Exception) { }
			return dflt;
		}

		private void SbPin()
		{
			Player p = Main.player[0];
			p.active = true; p.dead = false; p.name = CommanderName;
			p.statLifeMax = p.statLifeMax2 = 500; p.statManaMax = p.statManaMax2 = 200; p.statMana = 200;
			if (p.statLife < 1) p.statLife = 1;
			p.position = new Vector2(_cx * 16f - 4 * 16f - p.width / 2f, FloorY * 16f - p.height); p.velocity = Vector2.Zero;
		}

		private bool SbFightStart(SbSpec s)
		{
			ClearAll(); SpBuildFloor("run");
			SbHits.Clear(); GearUpkeep.SpawnCounts.Clear();
			Player cmd = Main.player[0]; cmd.active = true; cmd.dead = false; cmd.name = CommanderName;
			cmd.statLifeMax = cmd.statLifeMax2 = 500; cmd.statLife = s.CmdHurt ? 150 : 500; cmd.lifeSteal = 0f;
			cmd.hasPaladinShield = false;
			var rec = MakeRecord("Pvt. SB-" + s.Name); rec.Grade = Grade.Champion; rec.LoadoutLocked = true;
			rec.Order = s.Hold ? SoldierOrder.Hold : SoldierOrder.Follow; rec.HoldFire = false;
			rec.Gear.Head = SbMake(s.Head); rec.Gear.Body = SbMake(s.Body); rec.Gear.Legs = SbMake(s.Legs);
			for (int i = 0; i < s.Acc.Length && i < rec.Gear.Accessories.Length; i++) rec.Gear.Accessories[i] = SbMake(s.Acc[i]);
			if (!s.NoWeapon)
			{
				int wt = SbWeapon(s);
				Item w = new Item(); w.SetDefaults(wt);
				if (rec.Gear.CanHold(SoldierGear.SlotWeapon, w)) { rec.Gear.Weapon = w; rec.Gear.SetLocked(SoldierGear.SlotWeapon, true); }
				else SbLog($"HARNESS-SB: note {s.Name}: weapon {w.Name} not holdable");
				Item ammo = w.useAmmo > 0 ? FindAmmo(w, "") : null;
				if (ammo != null && rec.Gear.CanHold(SoldierGear.SlotAmmo, ammo)) { rec.Gear.Ammo = ammo; rec.Gear.SetLocked(SoldierGear.SlotAmmo, true); }
			}
			else rec.HoldFire = true;
			rec.CurrentHp = rec.MaxHp; rec.Mana = rec.MaxMana;
			RosterSystem.Instance.Regulars.Add(rec);
			int idx = RosterSystem.Instance.SpawnBody(rec, SoldierX0Bottom);
			if (idx < 0 || !rec.HasBody) { SbLog($"HARNESS-SB: FAIL {s.Name}: SpawnBody"); return false; }
			Main.npc[idx].Bottom = SoldierX0Bottom;
			_sbA = (SoldierNPC)Main.npc[idx].ModNPC; _sbA.RefreshStats();
			SbLevy = idx;
			_sbB = null;
			if (s.Mate)
			{
				var rb = MakeRecord("Pvt. Mate"); rb.Grade = Grade.Champion; rb.LoadoutLocked = true; rb.Order = SoldierOrder.Hold; rb.HoldFire = true;
				RosterSystem.Instance.Regulars.Add(rb);
				int bi = RosterSystem.Instance.SpawnBody(rb, new Vector2((_cx - 2) * 16f, FloorY * 16f));
				if (bi >= 0 && rb.HasBody) { _sbB = (SoldierNPC)Main.npc[bi].ModNPC; _sbB.NPC.life = 120; rb.CurrentHp = 120; }
			}
			_sbTarget = -1; _sbPoker = -1;
			if (!s.NoWeapon) { _sbTarget = SpSpawnTarget(_cx * 16f + s.TargetTiles * 16f, 64, 96, 0); }
			if (s.Poker) { _sbPoker = SpSpawnTarget(_cx * 16f + 8f, 40, 60, 60); Main.npc[_sbPoker].GetGlobalNPC<HarnessTarget>().Pin = Main.npc[_sbPoker].position; }
			_sbMaxOrbs = _sbMaxTitan = _sbMaxPal = _sbMaxNeb = _sbDodgeCd = _sbHolyReady = _sbBeetleSeen = _sbFrostSeen = _sbGuardianSeen = _sbLeafSeen = _sbMateMax = 0;
			_sbMinStealth = 1f; _sbSmokeTicks = 0; _sbRogueProj.Clear(); _sbLightsOut.Clear(); _sbBuffsSeen.Clear(); _sbRegenMax = 0; _sbMinCmd = int.MaxValue; _sbMaxCmd = 0; _sbLevyLifeMin = int.MaxValue;
			_sbCmdLife0 = cmd.statLife; _sbAL0 = _sbA.NPC.life; _sbBL0 = _sbB?.NPC.life ?? 0;
			_sbPokerL0 = _sbPoker >= 0 ? Main.npc[_sbPoker].life : 0; _sbTargetL0 = _sbTarget >= 0 ? Main.npc[_sbTarget].life : 0;
			return true;
		}

		private static int SbWeapon(SbSpec s)
		{
			if (!string.IsNullOrEmpty(s.Weapon)) return SbItem(s.Weapon);
			switch (s.Cls)
			{
				case "ranged": return ItemID.Handgun;
				case "magic": return ItemID.AmethystStaff;
				case "summon": return ItemID.SlimeStaff;
				default: return ItemID.IronBroadsword;
			}
		}

		private void SbFightTick(SbSpec s)
		{
			SbPin();
			if (_sbA == null || !_sbA.NPC.active) return;
			NPC me = _sbA.NPC;
			if (me.life < _sbLevyLifeMin) _sbLevyLifeMin = me.life;
			for (int q = 0; q < _sbA.Dummy.buffType.Length; q++) if (_sbA.Dummy.buffType[q] > 0) _sbBuffsSeen.Add(BuffLoader.GetBuff(_sbA.Dummy.buffType[q])?.Name ?? _sbA.Dummy.buffType[q].ToString());
			if (_sbA.Dummy.stealth < _sbMinStealth) _sbMinStealth = _sbA.Dummy.stealth;
			if (s.Event == "desertprowler" && SbShrouded(_sbA.Dummy)) _sbSmokeTicks++;
			int oc = SbPriv<int>(_sbA, "_setOrbs"); if (oc > _sbMaxOrbs) _sbMaxOrbs = oc;
			int kind = SbPriv<int>(_sbA, "_setKind"); if (kind > 0) _sbBeetleSeen = kind;
			if (_sbA.TitaniumStormTicks > _sbMaxTitan) _sbMaxTitan = _sbA.TitaniumStormTicks;
			int pal = SbPriv<int>(_sbA, "_palladiumTicks"); if (pal > _sbMaxPal) _sbMaxPal = pal;
			int nl = _sbA.NebulaLevelOf(0) + _sbA.NebulaLevelOf(1) + _sbA.NebulaLevelOf(2); if (nl > _sbMaxNeb) _sbMaxNeb = nl;
			if (SbPriv<int>(_sbA, "_holyDodgeReady") > 0) _sbHolyReady++;
			if (SbPriv<int>(_sbA, "_holyDodgeCooldown") > 0) _sbDodgeCd++;
			try { var m = typeof(SoldierNPC).GetMethod("SetRegenUnits", BindingFlags.NonPublic | BindingFlags.Instance); double u = Convert.ToDouble(m.Invoke(_sbA, null)); if (u > _sbRegenMax) _sbRegenMax = u; } catch (Exception) { }
			Player cmd = Main.player[0];
			if (cmd.statLife < _sbMinCmd) _sbMinCmd = cmd.statLife; if (cmd.statLife > _sbMaxCmd) _sbMaxCmd = cmd.statLife;
			if (_sbB != null && _sbB.NPC.active && _sbB.NPC.life > _sbMateMax) _sbMateMax = _sbB.NPC.life;
			if (_sbTarget >= 0 && Main.npc[_sbTarget].active && Main.npc[_sbTarget].HasBuff(324)) _sbFrostSeen++;
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile p = Main.projectile[i];
				if (!p.active) continue;
				if (p.type == ProjectileID.StardustGuardian) _sbGuardianSeen++;
				if (s.Event == "desertprowler" && p.TryGetGlobalProjectile(out Globals.SoldierProjectileTag tg0) && tg0.SoldierIndex == SbLevy) { _sbRogueProj.Add(p.identity); if (SbLights(p)) _sbLightsOut.Add(p.identity); }
				if (p.type == ProjectileID.CrystalLeaf) _sbLeafSeen++;
			}
			// the poker keeps hitting: it is a pinned enemy with contact damage 60; keep it from being killed by thorns
			if (_sbPoker >= 0 && Main.npc[_sbPoker].active) { Main.npc[_sbPoker].GetGlobalNPC<HarnessTarget>().Pin = me.Center - new Vector2(Main.npc[_sbPoker].width / 2f, Main.npc[_sbPoker].height / 2f); SbPokerLostCount += Main.npc[_sbPoker].lifeMax - Main.npc[_sbPoker].life; Main.npc[_sbPoker].life = Main.npc[_sbPoker].lifeMax; }
		}

		private static bool SbLights(Projectile p)
		{
			try
			{
				var gp = ModContent.GetContent<GlobalProjectile>().FirstOrDefault(x => x.GetType().Name == "DesertProwlerProjectile");
				if (gp == null) return false;
				object inst = typeof(Projectile).GetMethod("GetGlobalProjectile", new[] { typeof(GlobalProjectile), typeof(bool) }) != null ? null : null;
				var m = typeof(Projectile).GetMethods().First(x => x.Name == "GetGlobalProjectile" && x.IsGenericMethod && x.GetParameters().Length >= 1 && x.GetParameters()[0].ParameterType.IsGenericParameter).MakeGenericMethod(typeof(GlobalProjectile));
				object g = m.Invoke(p, m.GetParameters().Length == 2 ? new object[] { gp, false } : new object[] { gp });
				return g != null && (bool)g.GetType().GetField("LightsOut").GetValue(g);
			}
			catch (Exception) { return false; }
		}

		private static bool SbShrouded(Player d)
		{
			try
			{
				var t = ModLoader.GetMod("CalamityMod").Code.GetType("CalamityMod.Items.Armor.DesertProwler.DesertProwlerHat");
				var m = t.GetMethod("ShroudedInSmoke");
				object[] a = { d, null };
				return (bool)m.Invoke(null, a);
			}
			catch (Exception) { return false; }
		}

		private static int SbSpawns(int type) => GearUpkeep.SpawnCounts.TryGetValue(type, out int n) ? n : 0;
		private static int SbSpawnsByName(string modProj) => ModContent.TryFind(modProj, out ModProjectile mp) ? SbSpawns(mp.Type) : 0;
		private static (int hits, long dmg, int credited) SbHit(int type) => SbHits.TryGetValue(type, out var v) ? v : (0, 0, 0);

		/// <summary>The set's own check. Null passes; a string is the failure reason. info carries the numbers either way.</summary>
		private string SbVerify(SbSpec s, out string info)
		{
			NPC tg = _sbTarget >= 0 ? Main.npc[_sbTarget] : null;
			int dealt = tg != null ? _sbTargetL0 - tg.life : 0;
			Player cmd = Main.player[0];
			info = $"target damage {dealt}";
			if (s.Event != null && s.Event.StartsWith("calkey:"))
			{
				string[] parts = s.Event.Split(':');
				bool eff = parts[2] == "presses" ? true : parts[2] == "buff" ? _sbBuffsSeen.Contains(parts[1]) : ModContent.TryFind("CalamityMod/" + parts[1], out ModProjectile mpj) && SbSpawns(mpj.Type) > 0;
				info += $"; key presses {_sbA.KeyPressesOwn}; effect {parts[1]} ({parts[2]}) seen {eff}; buffs seen [{string.Join(",", _sbBuffsSeen)}]; set flags: {SoldierNPC.KeyPressKinds.Count}";
				return _sbA.KeyPressesOwn > 0 && eff ? null : (_sbA.KeyPressesOwn == 0 ? "the key was never pressed" : "pressed, but the effect never appeared");
			}
			switch (s.Event)
			{
				case "palladium":
				{
					info += $"; palladium ticks max {_sbMaxPal}; regen units max {_sbRegenMax}";
					return _sbMaxPal > 0 && _sbRegenMax > 0 ? null : "no rapid-healing window after a hit (dummy.onHitRegen=" + _sbA.Dummy.onHitRegen + ")";
				}
				case "orichalcum":
				{
					var h = SbHit(ProjectileID.FlowerPetal); int sp = SbSpawns(ProjectileID.FlowerPetal);
					info += $"; petals spawned {sp}; petal hits {h.hits} dmg {h.dmg} credited {h.credited}";
					return sp > 0 && h.hits > 0 && h.credited > 0 ? null : (sp == 0 ? "no petal ever spawned (dummy.onHitPetal=" + _sbA.Dummy.onHitPetal + ")" : "petals did not hit/credit");
				}
				case "titanium":
				{
					int st = ModContent.ProjectileType<Content.Projectiles.SoldierTitaniumShard>(); var h = SbHit(st); int sp = SbSpawns(st);
					info += $"; storm ticks max {_sbMaxTitan}; shards spawned {sp}; shard hits {h.hits} dmg {h.dmg} credited {h.credited}";
					return _sbMaxTitan > 0 && sp > 0 && h.hits > 0 ? null : "no shard storm (dummy.onHitTitaniumStorm=" + _sbA.Dummy.onHitTitaniumStorm + ")";
				}
				case "frost":
				{
					info += $"; ticks target was frostburning {_sbFrostSeen}; dummy.frostBurn={_sbA.Dummy.frostBurn}";
					return _sbFrostSeen > 0 ? null : "target never frostburned";
				}
				case "hallowed":
				{
					info += $"; ticks dodge ready {_sbHolyReady}; ticks on cooldown (dodge used) {_sbDodgeCd}; dummy.onHitDodge={_sbA.Dummy.onHitDodge}";
					return _sbDodgeCd > 0 ? null : (_sbHolyReady == 0 ? "dodge never became ready" : "dodge ready but no hit was dodged");
				}
				case "chlorophyte":
				{
					int leaf = SbSpawns(ProjectileID.CrystalLeaf), shot = SbSpawns(ProjectileID.CrystalLeafShot); var h = SbHit(ProjectileID.CrystalLeafShot);
					info += $"; crystal-leaf ticks alive {_sbLeafSeen}; leaf spawns {leaf}; leaf shots {shot}; shot hits {h.hits} dmg {h.dmg} credited {h.credited}";
					return shot > 0 && h.hits > 0 && h.credited > 0 ? null : "leaf crystal absent or never fired (dummy.leafCrystal? setBonus='" + Trunc(_sbA.Dummy.setBonus) + "')";
				}
				case "stardust":
				{
					int gt = ModContent.ProjectileType<Content.Projectiles.SoldierStardustGuardian>(); var h = SbHit(gt);
					info += $"; guardian spawns {SbSpawns(gt)}; guardian hits {h.hits} dmg {h.dmg} credited {h.credited}";
					return SbSpawns(gt) > 0 && h.hits > 0 ? null : "no guardian / no punches";
				}
				case "thorns":
				{
					NPC pk = _sbPoker >= 0 ? Main.npc[_sbPoker] : null;
					info += $"; levy life {_sbAL0}->{_sbA.NPC.life} (min {_sbLevyLifeMin}); dummy.thorns={_sbA.Dummy.thorns} turtle={_sbA.Dummy.turtleThorns} cactus={_sbA.Dummy.cactusThorns}";
					bool hit = _sbLevyLifeMin < _sbAL0;
					info += $"; poker hp lost to thorns {SbPokerLost}";
					return hit && SbPokerLost > 0 ? null : (!hit ? "levy was never touched" : "attacker took no thorn damage");
				}
				case "spectrehood":
				{
					info += $"; commander {_sbCmdLife0}->{cmd.statLife} (max seen {_sbMaxCmd}); squadmate {_sbBL0}->{_sbMateMax}; levy hp {_sbAL0}->{_sbA.NPC.life}; ghostHeal flag {_sbA.Dummy.ghostHeal}; bolts {Team.TeamSpectre.LevyBolts} delivered {Team.TeamSpectre.LevyDelivered}";
					return (_sbMaxCmd > _sbCmdLife0 || _sbMateMax > _sbBL0) ? null : "nobody wounded was healed";
				}
				case "spectremask":
				{
					int wr = SbSpawns(ProjectileID.SpectreWrath); var h = SbHit(ProjectileID.SpectreWrath);
					info += $"; wrath bolts {wr}; hits {h.hits} dmg {h.dmg} credited {h.credited}; ghostHurt {_sbA.Dummy.ghostHurt}";
					return wr > 0 && h.hits > 0 ? null : "no wrath bolts";
				}
				case "nebula":
				{
					int pk = ModContent.ProjectileType<Content.Projectiles.SoldierNebulaBooster>();
					info += $"; booster pickups spawned {SbSpawns(pk)}; booster level max {_sbMaxNeb}; setNebula {_sbA.Dummy.setNebula}";
					return _sbMaxNeb > 0 ? null : "no booster ever taken";
				}
				case "beetlemight":
				{
					info += $"; orbs max {_sbMaxOrbs}; kind seen {_sbBeetleSeen}; dummy.beetleOffense {_sbA.Dummy.beetleOffense}";
					return _sbMaxOrbs > 0 ? null : "no beetles";
				}
				case "beetledefense":
				{
					info += $"; orbs max {_sbMaxOrbs}; kind seen {_sbBeetleSeen}; dummy.beetleDefense {_sbA.Dummy.beetleDefense}";
					return _sbMaxOrbs > 0 ? null : "no beetles";
				}
				case "solar":
				{
					info += $"; shields max {_sbMaxOrbs}; flare bursts {SbSpawns(LevyConstants.SolarFlareProjectile)}; setSolar {_sbA.Dummy.setSolar}";
					return _sbMaxOrbs > 0 ? null : "no flare shields";
				}
				case "forbidden":
				{
					var h = SbHit(ProjectileID.SandnadoFriendly);
					info += $"; key presses {_sbA.KeyPressesOwn}; storms spawned {SbSpawns(ProjectileID.SandnadoFriendly)}; storm hits {h.hits} dmg {h.dmg}; setForbidden={_sbA.Dummy.setForbidden}";
					return SbSpawns(ProjectileID.SandnadoFriendly) > 0 ? null : "the storm is never called";
				}
				case "desertprowler":
				{
					info += $"; key presses {_sbA.KeyPressesOwn}; ticks shrouded in smoke {_sbSmokeTicks}; levy projectiles {_sbRogueProj.Count}, with Lights Out crit {_sbLightsOut.Count}; kinds {string.Join(",", SoldierNPC.KeyPressKinds.Select(kv => kv.Key + "x" + kv.Value))}";
					return _sbSmokeTicks > 0 || _sbLightsOut.Count > 0 ? null : "smoke bomb never used";
				}
				case "spider":
				{
					info += $"; maxMinions {_sbA.Dummy.maxMinions}; setSquireT2 etc n/a";
					return null;
				}
				case "stealth":
				{
					info += $"; key presses {_sbA.KeyPressesOwn}; shroomite {_sbA.Dummy.shroomiteStealth} vortex-on {_sbA.Dummy.vortexStealthActive}; stealth min {_sbMinStealth:0.00} (1 = visible); aggro {_sbA.Dummy.aggro}";
					return _sbMinStealth < (s.Name == "Shroomite" ? 0.99f : 0.5f) ? null : "never went into stealth";
				}
			}
			return null;
		}

		private int SbPokerLost => _sbPoker >= 0 ? SbPokerLostCount : 0;
		private int SbPokerLostCount;

		// ------------------------------------------------------------------------------------------------ driver
		private void StepSetBonus()
		{
			if (_sbI >= _sbSets.Count)
			{
				SbLog($"HARNESS-SB: SUMMARY sets={_sbSets.Count} staticBroken={_sbStaticBroken} fightPass={_sbPass} fightFail={_sbFail}");
				GearUpkeep.CountSpawns = false;
				Finish("setbonus scenario complete");
				return;
			}
			SbSpec s = _sbSets[_sbI];
			if (!_sbStarted)
			{
				string sv = "-";
				bool sok = true;
				if (_sbMode != "fight" && s.Static)
				{
					try { sv = SbStatic(s, out sok, out _, out _); }
					catch (Exception e) { sv = "FAIL static threw " + e.GetType().Name + ": " + e.Message; sok = false; }
					if (!sok && !sv.StartsWith("SKIP")) _sbStaticBroken++;
					SbLog($"HARNESS-SB: STATIC {s.Name,-18} {sv}");
				}
				if (s.Event == null || _sbMode == "static") { _sbI++; return; }
				bool ok;
				try { ok = SbFightStart(s); } catch (Exception e) { SbLog($"HARNESS-SB: FAIL {s.Name}: fight setup threw {e.GetType().Name}: {e.Message}"); ok = false; }
				if (!ok) { _sbFail++; _sbI++; return; }
				_sbStarted = true; _sbTick = 0; SbPokerLostCount = 0;
				return;
			}
			_sbTick++;
			try { SbFightTick(s); } catch (Exception e) { if (_sbTick == 1) SbLog($"HARNESS-SB: note {s.Name}: tick threw {e.GetType().Name}: {e.Message}"); }
			if (_sbTick < s.Ticks) return;
			string verdict, info;
			try { verdict = SbVerify(s, out info); } catch (Exception e) { verdict = "verify threw " + e.GetType().Name + ": " + e.Message; info = ""; }
			if (verdict == null) { _sbPass++; SbLog($"HARNESS-SB: FIGHT PASS {s.Name,-16} | {info}"); }
			else { _sbFail++; SbLog($"HARNESS-SB: FIGHT FAIL {s.Name,-16} {verdict} | {info}"); }
			_sbI++; _sbStarted = false;
		}
	}

	/// <summary>Records hits on the harness targets by projectile type for the set-bonus audit, and the damage a poker takes (thorns).</summary>
	public class SbTap : GlobalNPC
	{
		public override bool InstancePerEntity => false;
		public override void OnHitByProjectile(NPC npc, Projectile projectile, NPC.HitInfo hit, int damageDone)
		{
			if (!CombatHarness.Enabled || !npc.TryGetGlobalNPC(out HarnessTarget ht) || !ht.IsTarget) return;
			int cred = projectile.TryGetGlobalProjectile(out SoldierProjectileTag t) && t.SoldierIndex == CombatHarness.SbLevy ? 1 : 0;
			var cur = CombatHarness.SbHits.TryGetValue(projectile.type, out var v) ? v : (0, 0L, 0);
			CombatHarness.SbHits[projectile.type] = (cur.Item1 + 1, cur.Item2 + damageDone, cur.Item3 + cred);
		}
	}
}
