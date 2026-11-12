# Holding Banner

A placeable war banner (2 x 4 tiles, on the ground, a platform or a table; any number). Work Bench: 8 wood + 3 silk.

## How it fits the orders
- A new order on the radial, **Hold the banners** (`SoldierOrder.HoldBanners`, byte 7; same Order packet, Command Radio reaches every Regular). Follow still follows the commander, Hold still holds where the levy stands.
- Each Regular on the order walks to its own stand at the nearest banner (planned route: hooks, wings, climbs, platforms, as for a map order), stops and stays. Stands: the ground columns within 4 tiles of the banner's centre at the banner's own level, nearest first (up to 10; a platform or a tower top gets only the stands that exist). A banner holds as many levies as it has stands; the nearest banner with a free stand takes the next levy, and when all are full the nearest takes the extra ones.
- The stand is also the levy's Hold post: it fights from it (ranged fire; melee inside the 15-tile Hold leash) and walks back to it afterwards. With no banner (or none it can reach, after about 60 s of no progress, retried every 90 s) the order is plain Hold where it stands.
- Placing or breaking a banner reassigns the whole army to the nearest banner.
- The buff is not tied to the order: any soldier within 5 tiles of a banner (and not more than 4 rows above its foot) carries **Holding the Banner**: +12 defense, 20% less damage taken, knockback immunity. (Levies already have no knockback, so the third line only matters for a Fizzy body.)

## Numbers
`LevyConstants.Banner`: recipe 8 wood + 3 silk; stands within 4 columns, up to 10; aura 5 tiles, 4 rows; defense 12; damage reduction 0.20; pulse every 20 ticks, buff 90 ticks.

## Art
`levy-mod/docs/banners/holding_banner_preview*.png` (8x, next to vanilla banners and the vanilla lamp post; `_dark` on a dark backdrop, `_context` on ground). Generator: `levy-mod/tools/gen_holding_banner_art.py` (vanilla red banner palette and lamp-post wood).

## Lab (travelab, `lab/travel/specs/banner_towers.yaml`, `banner_hold.yaml`)
12 levies of mixed gear (3 bare, 2 boots, 2 Angel Wings, 2 Grappling Hook, 1 Hermes+Cloud+Hook, 1 Lightning Boots+Flame Wings+Dual Hook, 1 Terraspark+Solar Wings+Ninja Gear+Lunar Hook), 12 runs per cell (starts 120 tiles apart), two generated worlds (seeds 11111 and 80808), no catch-up hop, any teleport counts as a failure. Arrived = standing at its own stand inside the aura.

| cell | world 11111 | world 80808 |
|---|---|---|
| banner on the ground (control) | 144/144 | 144/144 |
| platform floor, 14 rows up, platform stairs | 144/144 | 144/144 |
| platform floor on a 35-row tower, platform stairs | 144/144 | 144/144 |
| top of a sheer 35-row stone pillar | 72/144 | 72/144 |

On the pillar: every levy with a hook (Grappling Hook and up) and every better kit (Hermes+Cloud+Hook and up) arrives (60/60 per world), plain Angel Wings 12/24, bare and Hermes Boots 0/60 (they cannot climb a sheer face; they hold at its foot). Time: 6 s on the ground, 11-14 s to 14 rows, 14-15 s to 35 rows by stairs, 15-17 s up the pillar. No levy teleported in any cell.

Under attack (`banner_hold.yaml`, 60 s, a foe every 2 s from 15 tiles, handguns): ground and tower, 4 runs each: 12/12 levies on their stands at the end, 99.6-100% of the time on the stand and 100% in the aura, farthest stray 3.6 tiles, no chases past 8 tiles, 12/12 carry the buff. (Handgun levies shoot the foes before they arrive: this measures ranged holders, not melee.)
