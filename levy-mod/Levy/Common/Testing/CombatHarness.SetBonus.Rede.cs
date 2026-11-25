using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Soldiers;
using Levy.Common.Systems;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	/// <summary>
	/// The set-bonus audit's second half for a big content mod (written for Redemption, generic by reflection): (1) every armor set the mod defines, found by asking each head piece's own IsArmorSet over the body and leg
	/// pieces of its namespace, goes through the STATIC diff (reference Player versus levy: every Player field, ModPlayer field and class stat the set changes must change on the levy by the same amount);
	/// (2) every accessory of the mod worn alone goes through the same diff (reference Player with the accessory minus without it, against the levy's). Selected with LEVY_HARNESS_SBFILTER=Rede (sets) and
	/// LEVY_HARNESS_SBFILTER=RedeAcc (accessories); both are only built when the mod named by LEVY_HARNESS_SBMOD (default Redemption) is loaded. Measurement only.
	/// </summary>
	public partial class CombatHarness
	{
		private void SbBuildMod()
		{
			string modName = Environment.GetEnvironmentVariable("LEVY_HARNESS_SBMOD") ?? "Redemption";
			if (!ModLoader.TryGetMod(modName, out Mod mod)) return;
			string tag = modName == "Redemption" ? "Rede" : modName;
			var heads = new List<Item>(); var bodies = new List<Item>(); var legs = new List<Item>(); var accs = new List<Item>();
			for (int t = ItemID.Count; t < ItemLoader.ItemCount; t++)
			{
				if (!ContentSamples.ItemsByType.TryGetValue(t, out Item s) || s == null || s.IsAir || s.ModItem?.Mod != mod) continue;
				if (s.headSlot >= 0) heads.Add(s); else if (s.bodySlot >= 0) bodies.Add(s); else if (s.legSlot >= 0) legs.Add(s);
				else if (s.accessory && !s.vanity) accs.Add(s);
			}
			int sets = 0;
			foreach (Item h in heads)
			{
				string ns = h.ModItem.GetType().Namespace;
				// the head's own namespace first (a mod that keeps each set in a folder); a mod that mixes its sets in a few namespaces (Thorium) is asked over every body and leg piece
				for (int pass = 0; pass < 2; pass++)
				{
					int before = sets;
					foreach (Item b in bodies.Where(x => pass == 1 || x.ModItem.GetType().Namespace == ns))
						foreach (Item l in legs.Where(x => pass == 1 || x.ModItem.GetType().Namespace == ns))
						{
							bool set = false; try { set = h.ModItem.IsArmorSet(h, b, l); } catch (Exception) { }
							if (!set) continue;
							string cls = h.CountsAsClass(DamageClass.Ranged) ? "ranged" : h.CountsAsClass(DamageClass.Magic) ? "magic" : h.CountsAsClass(DamageClass.Summon) ? "summon" : "melee";
							string hn = h.ModItem.Name; string wpn = null;
							if (modName == "ThoriumMod") { (cls, wpn) = ThoriumSetClass(hn, cls); }
							if (modName == "CalamityMod") { cls = hn.Contains("Rogue") ? "rogue" : hn.Contains("Magic") || hn.Contains("Mage") || hn.StartsWith("Brimflame") ? "magic" : hn.Contains("Ranged") ? "ranged" : hn.Contains("Summon") ? "summon" : hn.Contains("Melee") ? "melee" : cls; switch (hn) { case "EmpyreanMask": case "ForbiddenCirclet": case "UmbraphileHood": case "SnowRuffianMask": cls = "rogue"; break; case "FearmongerGreathelm": case "PlaguebringerVisor": case "TitanHeartMask": cls = "summon"; break; case "PlagueReaperMask": case "GemTechHeadgear": cls = "ranged"; break; } if (cls == "rogue") wpn = "CalamityMod/WulfrumKnife"; }
							string[] ws = modName == "Redemption" ? null : (Environment.GetEnvironmentVariable("LEVY_HARNESS_SBWEAPONS") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries);
							SbAdd($"{tag}:{hn}", mod.Name + "/" + hn, mod.Name + "/" + b.ModItem.Name, mod.Name + "/" + l.ModItem.Name, cls, modName == "Redemption" ? null : "observe", sp => { sp.Poker = true; sp.Ticks = 600; sp.TargetTiles = cls == "melee" ? 3f : 9f; if (wpn != null) sp.Weapon = wpn; });
							sets++;
						}
					if (sets > before) break;
				}
			}
			if (Environment.GetEnvironmentVariable("LEVY_HARNESS_SBNOACC") != "1")
			foreach (Item a in accs)
				SbAdd($"{tag}Acc:{a.ModItem.Name}", "", "", "", "melee", null, s => { s.AccOnly = true; s.Acc = new[] { mod.Name + "/" + a.ModItem.Name }; });
			Mod.Logger.Info($"HARNESS-SB: {modName}: {sets} armor sets, {accs.Count} accessories queued");
		}

		/// <summary>The class and weapon a Thorium set's fight is run with (the observe fight reads the set's on-hit and on-hurt effects; a vanilla weapon stands in where the mod's own class needs gear the lab levy does not hold).</summary>
		private static (string cls, string weapon) ThoriumSetClass(string head, string dflt)
		{
			switch (head)
			{
				case "CriersCap": case "CyberPunkHeadset": case "JestersMask": case "MaestroWig": case "MarchingBandShako": case "NoblesHat": case "OrnateHat": case "ShootingStarHat": case "SoloistHat": case "InspiratorsHelmet": case "HallowedChapeau":
					return ("melee", "ThoriumMod/Bongos"); // bard
				case "BioTechHood": case "BloomingCrown": case "CelestialCrown": case "EbonHood": case "FallenPaladinFaceguard": case "IridescentHelmet": case "LifeBinderMask": case "NoviceClericCowl": case "SacredHelmet": case "TemplarsCirclet": case "WarlockHood": case "HallowedCowl": case "DemonBloodHelmet": case "LodeStoneFaceGuard": case "CoralHelmet": case "GeodeHelmet":
					return ("melee", "ThoriumMod/BoneReaper"); // healer
				case "AstroHelmet": case "LifeBloomMask": case "LivingWoodMask": case "SpiritTrapperCowl": case "SpiritTrapperMask": return ("summon", null);
				case "CryomancersCrown": case "LichCowl": case "MagmaSeersMask": case "PyromancerCowl": case "WhiteKnightMask": case "SilkHat": case "NagaSkinMask": return ("magic", null);
				case "MasterArbalestHood": case "MasterMarksmansScouter": case "YewWoodHelmet": case "TideHunterCap": case "TideTurnersGaze": return ("ranged", null);
				case "FungusHat": case "PlagueDoctorsMask": case "ShadeMasterMask": case "WhiteDwarfMask": return ("ranged", "ThrowingKnife"); // thrower
				default: return (dflt, null);
			}
		}

		/// <summary>The static diff for one accessory worn alone: reference Player with it minus without it, against the same on a levy.</summary>
		private string SbStaticAcc(SbSpec s, out bool ok)
		{
			ok = true;
			Item acc = SbMake(s.Acc[0]);
			if (acc.IsAir) { ok = false; return "SKIP: item not found " + s.Acc[0]; }
			var refWith = SbRefSnap(new Item(), new Item(), new Item(), new[] { SbMake(s.Acc[0]) }, out _);
			var refWithout = SbRefSnap(new Item(), new Item(), new Item(), Array.Empty<Item>(), out _);
			ClearAll(); SpBuildFloor("run");
			Player cmd = Main.player[0]; cmd.active = true; cmd.dead = false; cmd.name = CommanderName; cmd.statLifeMax = cmd.statLifeMax2 = 500; cmd.statLife = 500;
			var rec = MakeRecord("Pvt. SBA"); rec.Grade = Grade.Champion; rec.LoadoutLocked = true; rec.Order = SoldierOrder.Hold; rec.HoldFire = true;
			RosterSystem.Instance.Regulars.Add(rec);
			int idx = RosterSystem.Instance.SpawnBody(rec, SoldierX0Bottom);
			if (idx < 0 || !rec.HasBody) { ok = false; return "FAIL: SpawnBody"; }
			var sn = (SoldierNPC)Main.npc[idx].ModNPC;
			sn.RefreshStats();
			var levyWithout = SbSnap(sn.Dummy);
			rec.Gear.Accessories[0] = SbMake(s.Acc[0]);
			sn.RefreshStats();
			var levyWith = SbSnap(sn.Dummy);
			sn.NPC.active = false;
			var changed = refWith.Where(kv => refWithout.TryGetValue(kv.Key, out double b) && Math.Abs(kv.Value - b) > 1e-4).Select(kv => kv.Key).ToList();
			int match = 0; var miss = new List<string>();
			foreach (string k in changed)
			{
				double rd = refWith[k] - refWithout[k];
				double ld = levyWith.TryGetValue(k, out double lf) && levyWithout.TryGetValue(k, out double lb) ? lf - lb : double.NaN;
				if (!double.IsNaN(ld) && Math.Abs(ld - rd) <= Math.Max(1e-3, Math.Abs(rd) * 0.02)) match++;
				else miss.Add($"{k} ref{rd:+0.###;-0.###} levy{(double.IsNaN(ld) ? "?" : ld.ToString("+0.###;-0.###;0"))}");
			}
			ok = miss.Count == 0;
			string state = changed.Count == 0 ? "NOSTAT " : ok ? "ACTIVE " : "BROKEN ";
			return state + $"refDelta=[{SbDeltaText(refWith, refWithout, 6)}]; levyDelta=[{SbDeltaText(levyWith, levyWithout, 6)}]; matched {match}/{changed.Count}" + (miss.Count > 0 ? "; MISSING: " + string.Join(", ", miss.Take(8)) : "");
		}
	}
}
