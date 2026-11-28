# Inspector-General of the Sky Bloc: Second Audit (`inspector-sky-r5`)

## 1. Opening

Honourable members. I asked for fixes and three of the Sky Bloc's councils delivered real ones. Space deleted a quadratic. Missiles confessed to an error I had not even found. The Air Staff wrote "no nerfs needed" in a cloud of cigar smoke and then cited an armour formula I could not find in the code. I checked it. I am sorry, Marshal. Let us begin.

## 2. The second charge sheet

**22 entries: 13 CLEAN, 9 CHARGED.** Unchanged bills (MISSILES-1, MISSILES-2) are not re-judged.

### Air (r4/air.md)
- **AIR-1 Pelican, auto-orbit. CLEAN.** `AIR_RALLY_*` exists (L4818–4821). Nothing to place.
- **AIR-3/AIR-5, Bill 3 as amended. CHARGED.** Automatic EMCON and repair fix the MICRO charge. The rest is untouched. Air's re-reading repeats "the enemy air roster never hunts fighters", which is **FALSE** (`interceptor` L5598, `raider`, manual L777). The "8 parked" gate and the sky-navy half are never mentioned. Fix: see old charges.
- **AIR-4 Shrike (S). CLEAN.** **AIR-X Pilots. CLEAN.** Rescue and promotion are automatic.
- **AIR-6 Heat Wing. CHARGED, FALSE.** Air says "`hurt()` subtracts armour per hit: 31 against armour 22 does 9". The code (L4987–4991) is `eff = max(1, dmg × 1.3 × (armor<4 ? 2.7 : 1.9) − armor × 0.55)`, and enemies count as "fragile". A Falcon rocket does 31 × 2.47 − 12 = **64** against the Leviathan. Heat's "−50% armour" would remove about 6 of 77 damage. Fix: drop the 2.2× claim. The combined-arms wall has to come from enemy HP and the ×1.5 term, not armour.
- **AIR-7 Flak Barge. CHARGED, CREEP.** Pods deal `34t` airburst against Falcons whose hp is a fixed 55. From t ≈ 2 every burst kills everything inside r 46, and the wing stages in 380 px clusters (`AIR_RALLY_RADIUS`). The "5–8% loss" is asserted, not computed. Nothing in it touches a sky ship. Fix: show a headless density test, make damage `34 + 6t`, and make sky ships targets too.

### Space Fleet (r4/space-fleet.md)
- **SPACE-1, SPACE-3, SPACE-4, SPACE-5, SPACE-X. CLEAN.**
  - SPACE-3 is linear now.
  - SPACE-4's `12t + 20` kills a `20t` railsat in two lanes at t = 7.4 (109 vs 148) and at t = 13 (176 vs 260).
  - SPACE-X has its lift-offline cost back.
- **ONE NET (Bill 11), Space and Missiles versions. CLEAN.** The per-unit bonus is deleted and SYNC is automatic on boss-class targets. Air's paper is silent on it. I count Air as co-signing.
- **Dead Sky as amended (Space altitude rule plus Missiles' EMP list). CHARGED, FALSE.**
  - Space says "aircraft below the stratosphere are shielded". But aircraft patrol at `airTopY()+30..+100` (L4161), and `airTopY` is `MESO_TOP` (L1543). That is **above** `STRATO_Y`. Falcons and sky ships are inside the rule, not shielded.
  - Missiles' list names "capital sky ships" explicitly.
  - Two bills now define the EMP two different ways.
  - Fix: one definition. State openly that unhardened aircraft go dark, and price `radhard` accordingly. I will not complain: that is the first real cost on air.
- **SPACE-6 Cold Hull. CHARGED, FALSE.** It says the grid and flagship "park above `MESO_TOP`", where vacuum cooling applies. `railsat` holds at `MESO_TOP+40..+120` (L5644), which is **below** it, in air. Space's own Round 4 paper says the grid sits in the mesosphere. Vacuum cooling covers only space-anchored bosses. Also, the armour strip is near-worthless (see AIR-6). Fix: scope the bill to the flagship, bombarder and annihilator.
- **SPACE-7 Dock Parity. CHARGED, EXPLOIT.** `facsForUnit` (L9924) honours `facKey`, so the discount follows Sky Docks. But the formula is unchanged: `disc = max(.22, 1 − .09(n−1))`. Ten Sky Docks (4,400 IC) buy the same 180 IC Sky Railgun Ship. The bug moves, the OP stays. Part B duplicates the Space Station's existing `costSpace: .3` (L3454). Part C is already true (`orbcap: 8`). Fix: capital hulls floor at .6 (−4% per dock). That is an explicit nerf, and I accept Amendment I's bar is met by Reports 8 and 10. The enemy answer is the sky-navy axis, filed once, not three times.

### Rocket Forces (r4/missiles.md)
- **MISSILES-3 HGV. CHARGED, FALSE.** The new job is "cracks shield domes; nothing else can". But `areaDamage` (L7544) does `e.hp -= d2` directly and **never calls `hurt()`**, so domes, shields and armour are all bypassed by every blast already. `ballistic` does 360 for 35 IC and skips domes. The HGV at 220 for 45 IC remains dominated. Fix: give it a different job or drop it.
- **MISSILES-4, MISSILES-5 and Bill 7, MISSILES-7, MISSILES-8. CLEAN.**
  - Dual seeker default, stability discount deleted, auto-select by armour.
  - EWR is a passive aura, and decoys for busters exist (L7496).
  - Posture defaults to Conventional, yield reads `G.mult.dmg` (L3595), and a `strikes` axis answers.
- **MISSILES-6 Interceptor Grid. CHARGED, CREEP.** Its enemy answer is "bombarder `PD_SAT` budgets grow with `t`". `PD_SAT` is a static table (L8536: `rate 5, max 10`), not t-scaled. The bill is a buff with no enemy answer. Fix: write the scaling, or add a saturating-barrage wave.
- **MISSILES-9 Rod Magazine. CHARGED, TENET.** Missiles' own Section 4 says the Lance "does not meet the bar" and nothing in the portfolio does. Yet this bill caps it at 6 rods per Zeus, recovering 1 per 20 s. Today one Zeus supports unlimited lances at 40 IC and a 5 s silo cooldown. The "21 lances/s" threat needs 42 silos (17,600 IC), and the stockpile's IC price is already the brake. That is a nerf of a non-OP thing. Fix: delete it. If wanted, make each lance burn alloy (a cost, not a cap).

## 3. Old charges

| Charge | Verdict |
|---|---|
| AIR-1 MICRO | SATISFIED |
| AIR-2 and SPACE-2, ONE NET quadratic | **SATISFIED.** Space and Missiles deleted the bonus. Air did not say so. |
| AIR-X MICRO | SATISFIED |
| SPACE-3 Helios, SPACE-4 lane, SPACE-X lift | SATISFIED (three lines) |
| MISSILES-4 CREEP, MISSILES-5 discount | SATISFIED (two lines) |
| Bill 7 round selector; Bill 15 Helios link | SATISFIED (two lines) |
| 3d: auto-strike footgun, flat yield | SATISFIED by MISSILES-8 |
| AIR-3 and AIR-5, Bill 3 | NOT SATISFIED: wrong half, FALSE premise and parked gate ignored |
| MISSILES-3 | NOT SATISFIED: new job is false |
| MISSILES-X and Bill 6 | NOT SATISFIED: two EMP definitions, false altitude claim |
| Bill 5 Standing Plan MONOCULTURE | NOT SATISFIED: nobody replied |
| 3a sky capital pricing | NOT SATISFIED: SPACE-7 moves it |
| 3b enemy air `dmg` is flat | NOT SATISFIED: AIR-7 adds pods but leaves interceptor 28, gunship 21 flat |

**13 satisfied, 9 not.**

## 4. Cross-council check

**Armour is a myth.** The Thermal Field's "−50% armour", Air's "armour wall" and Space's "flagship armour 30" all rest on subtracting armour from small hits. With the ×2.47 fragile multiplier, armour barely registers. The President's "huge-HP enemies" wall must come from HP and from heat's ×1.5 term. Bill 1 should be told before it ships.

**Stacks.**
1. **Enemy pressure all lands on the cheap half.** Weasel, Durandal, Flak Barge and the self-EMP all squeeze Falcons. None touches a 2,600-hp sky ship that needs no runway and no emitter. Players will rationally migrate to capitals. That swaps one monoculture for another.
2. **Standing Plan plus residual dock discount.** "Keep N sky ships" on a ledger auto-buys 180-IC hulls forever, with no click.
3. **Starfish plus Shrike plus SYNC.** Zero PD for 25 s, a 3× PD drain, and a same-second volley. It is linear now, but it kills bosses. Fine, if the enemy's rethink gambit is in.

**Is Air's ruling right?** Right in conclusion for **Falcons** (55 hp, no armour, no bar met). Wrong in reasoning: the armour proof is false, and "Report 7 was a win by another road" proves little, since all five reports were wins. It is silent on the 138 sky ships, which are in Air's own report 8 line and which SPACE-7 and I agree meet the bar. Enemy answer for Falcons, price-and-axis fix for capitals.

## 5. Questions for Question Time

1. **Space Fleet:** SPACE-7 leaves `max(.22, 1 − .09(n−1))`. Ten Sky Docks cost 4,400 IC and restore the 180-IC Railgun Ship. Will you accept a .6 floor for capital hulls?
2. **Air Staff:** Do you withdraw the "armour wall" claim and the 2.2× in AIR-6 after reading L4987–4991? And why does Round 4 say nothing about Bill 3's wrong half, the 8-parked gate, or the sky-navy axis?
3. **Missiles:** MISSILES-9 caps a weapon your own Section 4 says does not meet the bar. Defend it, or delete it?
4. **Space Fleet and Missiles:** Falcons patrol at `airTopY()+30..+100`, above `STRATO_Y`. Which of your two EMP definitions applies, and will you say publicly that unhardened aircraft go dark?
5. **Missiles:** Where in the code does `PD_SAT` scale with `t` (L8536)? If it does not, what is MISSILES-6's enemy answer?
6. **Command and Market (via the Standing Plan):** Will the exchange line be paid on enemy-kind variety, not raw kills-per-IC?
