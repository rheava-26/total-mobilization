#!/usr/bin/env python3
"""Condensed tables for warbands-mod/docs/TTK_EXPERT.md. usage: ttk_expert_doc.py npcdef|pvp CALC_RUN_DIR"""
import sys
sys.path.insert(0, __file__.rsplit("/", 1)[0])
import ttk_expert_summary as T

KINDS = ["gun", "bow", "magic", "melee", "sniper"]


def npcdef(rows):
    nd = [r for r in rows if r["kind"] == "npcdef"]
    print("| stage | weapon (hit) | shots/s | dps vs def 0 / stage armor / 55 / 68 / 75, vanilla rules | same, 40-tick i-frames on the target | per hit at def 0 / armor / 68 | TTK at stage armor, life 100/300/1000/3000/shooter/melee (vanilla) |")
    print("|---|---|---|---|---|---|---|")
    for s in ["0", "1", "3", "5"]:
        ar = [r for r in nd if r["s"] == s][0]["armorDef"].replace("armorDef=", "")
        for kind in KINDS:
            g = {(r["def"].replace("def=", ""), r["model"].replace("model=", "")): r for r in nd if r["s"] == s and r["pos"][0] == kind}
            ds = ["0", ar, "55", "68", "75"]
            def v(m): return " / ".join(g[(d, m)]["dps"].replace("dps=", "") if (d, m) in g else "-" for d in ds)
            r0 = g[("0", "vanilla")]
            ph = " / ".join(g[(d, "vanilla")]["perHit"].replace("perHit=", "") for d in ["0", ar, "68"])
            print(f"| {s} (armor {ar}) | {kind} {r0['pos'][1]} ({r0['hit'].replace('hit=', '')}) | {r0['shotsPerSec'].replace('shotsPerSec=', '')} | {v('vanilla')} | {v('iframes')} | {ph} | {g[(ar, 'vanilla')]['ttk'].replace('ttk=', '')} |")


def pvp(rows):
    pv = [r for r in rows if r["kind"] == "pvp"]
    print("| stage (def) | mode | weapon | hit | PvP dps / per hit | hostile projectile dps / per hit | NPC contact dps / per hit | PvP no i-frames dps | hostile proj no i-frames dps |")
    print("|---|---|---|---|---|---|---|---|---|")
    for s in ["0", "1", "3", "5"]:
        for gm, name in [("0", "Classic"), ("1", "Expert"), ("2", "Master")]:
            if gm == "0": continue
            for kind in KINDS:
                g = {(r["rule"].replace("rule=", ""), r["nif"].replace("nif=", "")): r for r in pv if r["s"] == s and r["gm"] == gm and r["pos"][0] == kind}
                if not g: continue
                def c(rule, nif="0"):
                    r = g[(rule, nif)]; return r["dps"].replace("dps=", "") + " / " + r["perHit"].replace("perHit=", "")
                r0 = g[("pvp", "0")]
                print(f"| {s} ({r0['def'].replace('def=', '')}) | {name} | {kind} {r0['pos'][1]} | {r0['hit'].replace('hit=', '')} | {c('pvp')} | {c('proj')} | {c('contact')} | {g[('pvp', '1')]['dps'].replace('dps=', '')} | {g[('proj', '1')]['dps'].replace('dps=', '')} |")


if __name__ == "__main__":
    rows = T.load(sys.argv[2])
    {"npcdef": npcdef, "pvp": pvp}[sys.argv[1]](rows)
