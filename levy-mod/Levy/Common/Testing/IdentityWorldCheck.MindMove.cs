using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Levy.Common.Observe;
using Levy.Common.Soldiers;
using Levy.Content.NPCs;

namespace Levy.Common.Testing
{
	public partial class IdentityWorldCheck
	{
		// ---------------------------------------------------------------------------------------------- MIND CONTROL, the air and the rigging
		// Eve (11-19): "you are forced to fly", "you can't use ropes or hooks". Each case is a key script on a levy she drives, compared with the stand-in commander's real Player.Update where one exists:
		// walking with wings or rocket boots keeps the feet on the floor; a jump on wings is a jump until jump is held in the air; down + jump hovers on hover wings (Vortex Booster, Hadarian Wings) and not on others;
		// the grapple key throws the real hook at the cursor and the body is pulled to it, a jump lets go; up on a rope climbs it and the body holds on it.

		/// <summary>A hook the next MindLevyTrace puts in the levy's hook slot (null: none).</summary>
		private static Item _mindHookGear;

		private static void MindRigging(bool on, bool rope)
		{
			// a stone ceiling 14 rows over the floor (the hook's anchor), or a rope from the floor up 20 rows in the levy's column
			if (rope) { for (int y = FloorY - 20; y <= FloorY - 1; y++) { Tile t = Main.tile[Cx, y]; t.ClearEverything(); if (on) { t.HasTile = true; t.TileType = TileID.Rope; } } return; }
			for (int x = Cx - 8; x <= Cx + 8; x++) { Tile t = Main.tile[x, FloorY - 14]; t.ClearEverything(); if (on) { t.HasTile = true; t.TileType = TileID.Stone; } }
		}

		private static void MindAirAudit(bool chk)
		{
			void Cmp(string name, string pl, string lv, bool ok) { Note($"mind {name}: player [{pl}] levy [{lv}]"); if (chk) Check($"mind control matches a player: {name} (player {pl}; levy {lv})", ok); }
			bool NearF(float a, float b, float rel, float abs) => Math.Abs(a - b) <= Math.Max(abs, rel * Math.Abs(b));
			Item angel = MindItem(ItemID.AngelWings), vortex = MindItem(ItemID.WingsVortex), rocket = MindItem(ItemID.RocketBoots);
			SoldierRecord rec;

			// ---- walking with wings, then with rocket boots: no jump key, the feet stay on the floor ----
			foreach (var (label, boots, wings) in new[] { ("Angel Wings", (Item)null, angel), ("Rocket Boots", rocket, (Item)null) })
			{
				Func<int, MindInputState> script = t => t < 150 ? K(r: true) : K();
				MTrace pl = MindPlayerTrace(boots, wings, null, 180, script, false), lv = MindLevyTrace(boots, wings, null, 180, script, false, out rec);
				float pa = pl.Apex(0, 180, 0f), la = lv.Apex(0, 180, 0f);
				Cmp($"walking right with {label} and no jump key: highest the feet leave the floor", $"{pa:0.0} px", $"{la:0.0} px", la < 4f);
			}

			// ---- a jump on wings: a tap is a plain jump, a short hold is a jump and a brief flight ----
			foreach (var (label, len) in new[] { ("tap", 2), ("held 40 ticks", 40) })
			{
				Func<int, MindInputState> script = t => t >= 10 && t < 10 + len ? K(j: true) : K();
				MTrace pl = MindPlayerTrace(null, angel, null, 160, script, false), lv = MindLevyTrace(null, angel, null, 160, script, false, out rec);
				float pa = pl.Apex(0, 160, 0f), la = lv.Apex(0, 160, 0f);
				Cmp($"jump {label} with Angel Wings: apex", $"{pa:0.0} px", $"{la:0.0} px", NearF(la, pa, 0.12f, 6f));
			}

			// ---- down + jump in the air: a hover on hover wings, a climb on others ----
			Item hadarian = ModContent.TryFind<ModItem>("CalamityMod/HadarianWings", out var hw) ? MindItem(hw.Type) : null;
			foreach (var (label, wings, hovers) in new[] { ("Vortex Booster", vortex, true), ("Hadarian Wings", hadarian, true), ("Angel Wings", angel, false) })
			{
				if (wings == null) continue;
				Func<int, MindInputState> script = t => t >= 10 && t < 30 ? K(j: true) : t >= 30 && t < 170 ? K(j: true, d: true) : K();
				MTrace pl = MindPlayerTrace(null, wings, null, 180, script, false), lv = MindLevyTrace(null, wings, null, 180, script, false, out rec);
				float pMove = Math.Abs(pl.Y[110] - pl.Y[60]), lMove = Math.Abs(lv.Y[110] - lv.Y[60]);
				Cmp($"down + jump in the air with {label}: {(hovers ? "the body holds its height (a hover)" : "no hover (the wings climb)")}, feet moved between tick 60 and 110",
					$"{pMove:0.0} px", $"{lMove:0.0} px (aloft {-lv.Y[85]:0} px)", hovers ? lMove < 10f && lv.Y[85] < -20f : lMove > 30f);
			}

			// ---- the grapple key: the hook flies at the cursor, latches the ceiling and pulls the body up; a jump lets go ----
			MindRigging(true, false);
			try
			{
				_mindHookGear = MindItem(ItemID.GrapplingHook);
				var aim = new Vector2(Cx * 16f + 8f, (FloorY - 14) * 16f + 8f);
				Func<int, MindInputState> script = t => t >= 10 && t < 12 ? new MindInputState { Hook = true, Aim = aim } : t >= 110 && t < 113 ? K(j: true) : K();
				long casts0 = SoldierNPC.MindHookCasts, latch0 = SoldierNPC.MindHookLatchTicks;
				MTrace lv = MindLevyTrace(null, null, null, 220, script, false, out rec);
				float rise = lv.Apex(0, 110, 0f);
				long casts = SoldierNPC.MindHookCasts - casts0, latched = SoldierNPC.MindHookLatchTicks - latch0;
				Note($"mind hook: {casts} casts, {latched} latched ticks, rose {rise:0} px by tick 110, feet at {lv.Y[109]:0} px then {lv.Y[219]:0} px");
				if (chk) Check($"mind control: the grapple key throws the hook at the cursor and pulls the body up to the ceiling ({casts} casts, {latched} latched ticks, rose {rise:0} px; the ceiling is 14 rows up)", casts > 0 && latched > 0 && rise > 120f);
				if (chk) Check($"mind control: a jump lets go of the hook and the body comes back down (feet {lv.Y[219]:0} px from the floor 110 ticks after the jump)", lv.Y[219] > -4f);
			}
			finally { _mindHookGear = null; MindRigging(false, false); }

			// ---- a rope: up climbs it, nothing held keeps the body on it ----
			MindRigging(true, true);
			try
			{
				Func<int, MindInputState> script = t => t >= 10 && t < 90 ? new MindInputState { Up = true } : K();
				long rope0 = SoldierNPC.MindRopeTicks;
				MTrace lv = MindLevyTrace(null, null, null, 150, script, false, out rec);
				float rise = lv.Apex(0, 90, 0f), held = Math.Abs(lv.Y[145] - lv.Y[95]);
				Note($"mind rope: {SoldierNPC.MindRopeTicks - rope0} rope ticks, climbed {rise:0} px in 80 ticks of up, then moved {held:0} px in 50 ticks with no key");
				if (chk) Check($"mind control: up on a rope climbs it (climbed {rise:0} px in 80 ticks)", rise > 160f);
				if (chk) Check($"mind control: on a rope with no key the body holds on (moved {held:0} px in 50 ticks, {-lv.Y[145]:0} px up)", held < 6f && lv.Y[145] < -100f);
			}
			finally { MindRigging(false, true); }
		}
	}
}
