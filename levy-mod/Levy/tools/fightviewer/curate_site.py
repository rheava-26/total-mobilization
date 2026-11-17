#!/usr/bin/env python3
"""Build the CURATED Levy fight-replay site: a short list with a Levy version stamp on every fight.

usage: curate_site.py <outdir> --eve-recs DIR --old-data DIR --old-manifest FILE --lab DIR --battery DIR [--files-json FILE] [--max-mb 225] [--max-files 260]

Sets, in page order:
  Your fights                     every recording Eve made herself (new .levyrec files plus already converted older data JSON), newest first, grouped by day
  Lab: hardmode bosses            the 2.0.66 sweep (<lab>/recordings/fights.csv): per boss up to 4 wins and 4 losses (Master+Death first) plus every odd fight
  Lab: Destroyer and Aquatic Scourge tests   three fights from <battery>/fights.csv, notes say what was held fixed
  Older lab fights (outdated)     converted older pre-hardmode lab fights, kept because no newer recording exists

The manifest carries `current` and `eras` (see make_site.ERAS); editing them in data/manifest.json re-stamps every fight without rebuilding data.
Reuses make_site's reader, summary and converter.
"""
import argparse, csv, json, os, re, shutil, statistics, sys
from multiprocessing import Pool

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import make_site as ms

# slug -> (title, progression)
BOSS = {
    "crab": ("Crabulon", 2.7), "hive": ("Hive Mind", 3.98), "perf": ("The Perforators", 3.99), "deer": ("Deerclops", 6.0), "wof": ("Wall of Flesh", 7.0),
    "qslime": ("Queen Slime", 8.0), "cryo": ("Cryogen", 8.5), "twins": ("The Twins", 9.0), "aqua": ("Aquatic Scourge", 9.5), "destroyer": ("The Destroyer", 10.0),
    "brim": ("Brimstone Elemental", 10.5), "prime": ("Skeletron Prime", 11.0), "cal": ("Calamitas Clone", 11.7), "plant": ("Plantera", 12.0),
    "levi": ("Leviathan and Anahita", 12.8), "aureus": ("Astrum Aureus", 12.81), "golem": ("Golem", 13.0), "fish": ("Duke Fishron", 14.0),
    "pbg": ("The Plaguebringer Goliath", 14.5), "eol": ("Empress of Light", 15.0), "cultist": ("Lunatic Cultist", 15.5), "ravager": ("Ravager", 16.5), "ml": ("Moon Lord", 18.0),
}
ALIAS = {"Anahita": "Leviathan and Anahita", "Leviathan": "Leviathan and Anahita", "Retinazer": "The Twins", "Spazmatism": "The Twins", "Empress of Light": "Empress of Light"}
PAIR = {"rev": "Expert+Rev", "mdeath": "Master+Death"}
RES = {"win": "win", "loss": "loss", "timeout": "timeout", "despawn": "despawn"}

# hand-written notes for the notable lab fights: (slug, pair, kit, seed) -> sentence appended to the facts
NOTABLE = {
    ("levi", "mdeath", "on", "1"): "The Leviathan chased the hidden commander all fight and barely touched the army. Fixed in 2.0.67.",
    ("twins", "mdeath", "on", "1"): "Spazmatism went after the hidden commander. Fixed in 2.0.67.",
    ("fish", "mdeath", "on", "2"): "The whole army falls.",
    ("ml", "mdeath", "on", "2"): "The first version where his shots hit levies.",
    ("deer", "mdeath", "on", "1"): "The first fights after the crash fix.",
}
CAL_NOTABLE_SEED = "3"   # one Calamitas Clone Master+Death win carries the 1-damage-shots note
CAL_NOTE = "Her shots hit levies for 1 damage each. Fixed in the next version."

# hand-written notes for the already converted older fights, by data file name
OLD_NOTES = {
    "eve__ks-solo-1-death.json": "Solo, no levies. Won in 86 s. 4 hits taken, 83 damage.",
    "eve__ks-solo-2.json": "Solo, no levies. Won in 79 s. 12 hits taken, 508 damage.",
    "eve__ks-solo-3.json": "Solo, no levies. Won in 97 s. 4 hits taken, 147 damage.",
    "eve__ks-solo-4.json": "Solo, no levies. Won in 97 s. 6 hits taken, 186 damage.",
    "eve__ks-team-1.json": "You and 13 levies. Won in 113 s. 11 of 13 levies lost, 4 in the first 4 s. The boss aimed at levies 88% of the time. You took 13 hits, 611 damage.",
    "eve__ks-team-3.json": "You and 13 levies. Won in 102 s. 11 of 13 levies lost, most to one King Slime body hit each. The boss aimed at levies 64% of the time.",
    "eve__goblin-army.json": "Goblin Army invasion with 25 levies. The recording was stopped by command at 41 s. 1 levy lost.",
    "eve__ds-solo-1.json": "Solo, no levies. Won in 71 s. 4 hits taken, 201 damage.",
    "eve__ds-solo-2.json": "Solo, no levies. Won in about 60 s. 3 hits taken.",
    "eve__ds-solo-3.json": "Solo, no levies. Won in about 51 s. 7 hits taken.",
    "eve__ds-solo-4.json": "Solo, no levies. Won in about 63 s. 7 hits taken.",
    "eve__ds-team-1.json": "You and 7 levies. Won in about 57 s. 8 levies lost, reinforcements included. The boss aimed at levies 70% of the time.",
    "eve__ds-team-2.json": "You and 12 levies. Won in about 35 s. 8 of 12 levies lost, 4 in the first second. The boss aimed at levies 53% of the time.",
    "eve__ds-team-3.json": "You and 12 levies. Won in about 34 s. 5 of 12 levies lost. The boss aimed at levies 80% of the time. You took 2 hits.",
    "paladin__pl-1a-shield45-s1.json": "45 levies in Paladin's Shield, no armor, 400 HP, seed 1. Won in 35 s. 3 levies lost, the first at 26 s.",
    "deathladder__dl-a-preevil-best-s1.json": "13 levies in the best pre-evil gear, 4 worms. Lost at 44 s. All 13 levies died. 4.1k of 30k boss life left.",
    "adds__04-slimegod-expert-spectator.json": "16 levies. Lost at 52 s. All 16 levies died. Paladins and slime spawn aimed at levies 100% of the time.",
    "adds__05-desertscourge-expert-spectator.json": "11 levies. Won in 137 s. 9 levies lost. The worm adds aimed at levies 100% of the time.",
    "adds__06-brain-rev-spectator.json": "13 levies. Won in 112 s. 3 levies lost. Creepers aimed at levies 100% of the time.",
    "adds__07-kingslime-masterdeath-spectator-jewel.json": "10 levies. Lost at 53 s. All 10 levies died. The Ruby Jewel aimed at a levy 100% of the time.",
    "wormbody__before-ds-exp-s2.json": "11 levies, seed 2, worm body not modelled. Lost at 31 s. All 11 levies died.",
    "moblab__calrev-queen-bee-a3-s1-mexp-mod-n15-01.json": "15 levies, no mobility gear, seed 1. Lost at 35 s. All 15 levies died.",
    "moblab__calrev-queen-bee-a3-s1-mexp-mod-n15-13.json": "15 levies, Cloud in a Bottle only, seed 1. Won in 33 s. 2 levies lost.",
    "moblab__calrev-brain-of-cthulhu-a3-s1-mrev-mod-n13-01.json": "13 levies, no mobility gear, seed 1. Lost at 48 s. All 13 levies died.",
    "moblab__calrev-skeletron-a3-s2-mexp-mod-n16-02.json": "16 levies, no mobility gear, seed 2. Won in 34 s. No levies lost.",
    "vert1__before-main-calrev-king-slime-a3-s1-mexp-mod-n10-43.json": "10 levies, seed 1. Won in 38 s. 4 levies lost.",
    "lab1__calrev-eye-of-cthulhu-surf-s7-mrev-mod-n16-20.json": "16 levies, seed 7. Lost at 245 s. All 16 levies died.",
    "worm__skel-exp-s1-after.json": "16 levies, seed 1. Lost at 159 s. All 16 levies died.",
    "rec2__calrev-eye-of-cthulhu-surf-s1-mexp-mod-n11-01.json": "11 levies, seed 1. Won in 95 s. 2 levies lost.",
}
OLD_RESULT = {"eve__goblin-army.json": "ended"}
OLD_BOSS = {"eve__goblin-army.json": "Goblin Army"}
EVE_OLD = [f for f in OLD_NOTES if f.startswith("eve__")]


def slug(s):
    return re.sub(r"[^A-Za-z0-9]+", "-", s).strip("-").lower()


def vkey(v):
    return tuple(int(x) for x in re.findall(r"\d+", v.split(" ")[0])[:3])


# ---------------------------------------------------------------- jobs (run in a pool)
def build_blob(args):
    path, hz_target = args[0], args[1]
    suffix = args[2] if len(args) > 2 else ""
    rows = dict(ms.episode_list(path))[suffix]     # a recording that holds event NPCs (Celestial Pillars ...) is cut into its fights: suffix -ep1, -ep2 ...
    hdr = next((r for r in rows if r.get("k") == "hdr"), {})
    hz0 = hdr.get("hz", 30)
    event = hdr.get("kind") == "event"
    factor = max(1, -(-hz0 // (min(hz_target, ms.EVENT_HZ) if event else hz_target)))
    blob = ms.dump(ms.convert(rows, factor))
    while event and len(blob) > ms.MAX_FILE and factor < ms.EVENT_MAX_FACTOR:
        factor *= 2
        blob = ms.dump(ms.convert(rows, factor))
    return blob, max(1, hz0 // factor)


def scan_rec(path):
    """One info dict per fight in the recording (several when it held event NPCs that older recorders kept in the fight)."""
    eps = ms.episode_list(path)
    out = []
    for suffix, rows in eps:
        info = _scan_rows(rows)
        info["suffix"] = suffix
        out.append(info)
    return out


def _scan_rows(rows):
    s = ms.summarize(rows)
    hdr = next((r for r in rows if r.get("k") == "hdr"), {})
    ev = [r for r in rows if r.get("k") == "e"]
    end = next((r for r in rows if r.get("k") == "end"), {})
    frames = [r for r in rows if r.get("k") == "f"]
    life = None
    for f in reversed(frames):
        mb = ms.main_boss(f)
        if mb is not None:
            life = mb[6] / max(1, mb[7]); break
    bevs = [e for e in ev if e.get("e") in ("bdeath", "bdespawn") and e.get("final", 1) != 0]
    if bevs and bevs[-1].get("max"):
        life = max(0.0, bevs[-1]["life"] / bevs[-1]["max"]) if bevs[-1]["e"] == "bdespawn" else 0.0
    bnames = {e["npc"]: e.get("name") for e in ev if e.get("e") == "boss" and "npc" in e}
    cd = [e for e in ev if e.get("e") == "cdeath"]
    last = None
    if cd:
        hits = [e for e in ev if e.get("e") == "hit" and cd[0]["t"] - 120 <= e["t"] <= cd[0]["t"]]
        if hits:
            h = hits[-1]
            last = ("contact with " + (bnames.get(h["npc"]) or "an enemy")) if h.get("npc", -1) >= 0 else ("a projectile" if h.get("proj", -1) >= 0 else "an unknown source")
    return {"s": s, "end": end, "life": life, "cdeaths": [round(e["t"] / 60.0) for e in cd], "last": last, "date": hdr.get("date")}


# ---------------------------------------------------------------- notes
def eve_note(info, s):
    e = info["end"]
    won = e.get("result") == "won"
    fled = e.get("result") == "fled"
    dur = e.get("durationSec") or s["dur"]
    boss = s["boss"]
    bits = ["%s in %.0f s." % ("Won" if won else "Lost", dur) if won else ("The boss left at %.0f s with you alive." % dur if fled else ("The recording ended at %.0f s." % dur if e.get("result") == "ended" else "Lost at %.0f s." % dur))]
    if not won and info["life"] is not None:
        bits.append("%s had %.0f%% life left." % (boss, 100 * info["life"]))
    if info["cdeaths"]:
        t = ", ".join("%d s" % x for x in info["cdeaths"][:3])
        bits.append("You died at %s%s." % (t, ("; the last hit was " + info["last"]) if info["last"] else ""))
    nl = e.get("levyDeaths", s["deaths"])
    tot = e.get("armyTotal") or s["levies"]
    if tot and nl:
        bits.append("%d of %d levies lost." % (min(nl, tot), tot))
    tg = e.get("target") or {}
    n = sum(tg.get(k, 0) for k in ("none", "player", "levy", "spite")) or 1
    if tg.get("player", 0) / n >= .5:
        bits.append("%s aimed at you %.0f%% of the time." % (boss, 100 * tg["player"] / n))
    elif tg.get("levy", 0) / n >= .5:
        bits.append("%s aimed at levies %.0f%% of the time." % (boss, 100 * tg["levy"] / n))
    return " ".join(bits)


def lab_note(r, extra=None):
    sec, n, d = float(r["sec"]), int(r["levies"]), int(r["deaths"])
    res = r["result"]
    bits = ["Won in %.0f s." % sec if res == "win" else ("Lost at %.0f s." % sec if res == "loss" else "Timed out at %.0f s." % sec if res == "timeout" else "Boss left at %.0f s." % sec)]
    if res != "win":
        try:
            left, mx = float(r["bossLeft"]), float(r["bossMax"])
            if mx and left > 0 and r["slug"] != "fish" and left <= mx:
                pct = 100 * left / mx
                bits.append("Boss had %s life left." % ("under 1%" if pct < 1 else "%.0f%%" % pct))
        except ValueError:
            pass
    if d == 0:
        bits.append("No levies lost.")
    elif d == 0 and False:
        pass
    elif d >= n:
        bits.append("All %d levies died%s." % (n, (", the first at %.0f s" % float(r["firstDeathSec"])) if r["firstDeathSec"] not in ("", "-") else ""))
    else:
        bits.append("%d of %d levies lost%s." % (d, n, (", the first at %.0f s" % float(r["firstDeathSec"])) if r["firstDeathSec"] not in ("", "-") else ""))
    c, p = float(r["takenContact"] or 0), float(r["takenProj"] or 0)
    if d and c + p > 0:
        bits.append("%.0f%% of the damage was contact." % (100 * c / (c + p)))
    elif not d and c + p > 0:
        bits.append("Levies took %d damage." % (c + p))
    if r.get("kit") == "mid":
        bits.append("Gear one tier below.")
    if extra:
        bits.append(extra)
    return " ".join(bits)


# ---------------------------------------------------------------- selection
def pick_lab(rows):
    by = {}
    for r in rows:
        by.setdefault(r["slug"], []).append(r)
    chosen, bnotes = [], {}
    for sl, rs in by.items():
        rs.sort(key=lambda r: (r["pair"] != "mdeath", r["kit"] != "on", int(r["seed"])))
        meds = {}
        for pr in PAIR:
            v = [float(r["takenContact"] or 0) + float(r["takenProj"] or 0) for r in rs if r["pair"] == pr]
            meds[pr] = statistics.median(v) if v else 0
        picked = []

        def add(r):
            if r not in picked:
                picked.append(r)
        wins = [r for r in rs if r["result"] == "win"]
        loss = [r for r in rs if r["result"] == "loss"]
        for r in wins[:4]: add(r)
        for r in loss[:4]: add(r)
        weird = []
        for r in rs:
            t = float(r["takenContact"] or 0) + float(r["takenProj"] or 0)
            key = (sl, r["pair"], r["kit"], r["seed"])
            med = meds[r["pair"]]
            odd = (r["result"] not in ("win", "loss") or key in NOTABLE or (sl == "cal" and r["pair"] == "mdeath" and r["kit"] == "on" and r["seed"] == CAL_NOTABLE_SEED)
                   or float(r["tgtPlayerPct"] or 0) >= 3 or float(r["tgtLevyPct"] or 100) < 90 or (med > 1500 and t < 0.12 * med and r["result"] == "win"))
            if odd:
                weird.append(r); add(r)
        chosen += [(sl, r, r in weird) for r in picked]
        nw, nl, nt = len(wins), len(loss), len(rs) - len(wins) - len(loss)
        txt = "%d fights recorded: %d win%s, %d loss%s%s." % (len(rs), nw, "" if nw == 1 else "s", nl, "" if nl == 1 else "es", (", %d other" % nt) if nt else "")
        if not nw: txt += " No wins in %d fights." % len(rs)
        if not nl: txt += " No losses in %d fights." % len(rs)
        shown = len(picked)
        if shown < len(rs): txt += " %d shown." % shown
        bnotes[BOSS[sl][0]] = txt
    return chosen, bnotes


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("outdir")
    ap.add_argument("--eve-recs", required=True); ap.add_argument("--old-data", required=True); ap.add_argument("--old-manifest", required=True)
    ap.add_argument("--lab", default="/home/user/lab/hmbattery2/recordings"); ap.add_argument("--battery", default="/home/user/lab/hmbattery2/battery")
    ap.add_argument("--files-json", default=None); ap.add_argument("--max-mb", type=float, default=225); ap.add_argument("--max-files", type=int, default=260)
    ap.add_argument("--hz", type=int, default=15); ap.add_argument("--jobs", type=int, default=4)
    a = ap.parse_args()
    out = os.path.abspath(a.outdir)
    shutil.rmtree(out, ignore_errors=True)
    os.makedirs(os.path.join(out, "data"))

    # entries: dict(id, set, path|None, oldfile|None, fight(dict without file/bytes))
    ents = []
    # --- Eve's new recordings
    recs = sorted(f for f in os.listdir(a.eve_recs) if f.endswith(".levyrec"))
    infos = {}
    with Pool(a.jobs) as pool:
        res = pool.map(scan_rec, [os.path.join(a.eve_recs, f) for f in recs])
    for fn, infos in zip(recs, res):
        for info in infos:
            suffix = info["suffix"]
            s = info["s"]; e = info["end"]
            s["boss"] = ALIAS.get(e.get("bossName") or s["boss"], e.get("bossName") or s["boss"])
            s["boss"] = ALIAS.get(s["boss"], s["boss"])
            # the recorder's own word, read by the result rule (a boss that leaves after the commander fell is a loss, with her alive it is fled)
            if s.get("rule"):
                e = info["end"] = dict(e, result=s["rule"])      # the rule on the recording itself beats the word an older recorder wrote
            if e.get("result") in ("won", "lost", "fled"):
                s["result"] = {"won": "win", "lost": "loss", "fled": "fled"}[e["result"]]
            s["note"] = ms.event_note(s) if s.get("kind") == "event" else eve_note(info, s)
            s["mode"] = s["mode"] if s["mode"] != "?" else "Master+Death"
            ents.append({"set": "eve", "path": os.path.join(a.eve_recs, fn), "suffix": suffix, "id": "eve__" + slug(re.sub(r"\.levyrec$", "", fn) + suffix), "f": s, "src": fn + suffix})

    # --- Eve's older fights and the outdated pre-hardmode lab fights (already converted)
    oldm = json.load(open(a.old_manifest))
    for st in oldm["sets"]:
        for f in st["fights"]:
            b = os.path.basename(f["file"])
            if b not in OLD_NOTES:
                continue
            p = os.path.join(a.old_data, b)
            if not os.path.exists(p):
                print("missing old data", b, file=sys.stderr); continue
            hdr = json.load(open(p))["hdr"]
            f = dict(f)
            f["ver"], f["verAbout"] = ms.version_of(hdr)
            f["date"] = hdr.get("date")
            f["note"] = OLD_NOTES[b]
            f["result"] = OLD_RESULT.get(b, f["result"])
            f["boss"] = OLD_BOSS.get(b, f["boss"])
            ents.append({"set": "eve" if b in EVE_OLD else "old", "old": p, "id": slug(re.sub(r"\.json$", "", b)), "f": f, "src": b})

    # --- lab sweep
    rows = list(csv.DictReader(open(os.path.join(a.lab, "fights.csv"))))
    chosen, bnotes = pick_lab(rows)
    chosen.sort(key=lambda x: (BOSS[x[0]][1], x[1]["pair"] != "mdeath", x[1]["result"] != "win", x[1]["kit"] != "on", int(x[1]["seed"])))
    for sl, r, weird in chosen:
        key = (sl, r["pair"], r["kit"], r["seed"])
        extra = NOTABLE.get(key) or (CAL_NOTE if (sl == "cal" and r["pair"] == "mdeath" and r["kit"] == "on" and r["seed"] == CAL_NOTABLE_SEED) else None)
        path = os.path.join(a.lab, r["file"])
        ents.append({"set": "lab", "path": path, "id": "lab__" + slug(os.path.basename(r["file"]).replace(".levyrec", "")), "src": os.path.basename(r["file"]),
                     "f": {"boss": BOSS[sl][0], "note": lab_note(r, extra), "result": RES.get(r["result"], r["result"]), "_odd": bool(weird), "_row": r}})

    # --- Destroyer / Aquatic Scourge tests
    brows = list(csv.DictReader(open(os.path.join(a.battery, "fights.csv"))))
    FIX = "Held fixed: Master+Death, the same 34-levy army, standing Spectator commander, platform arena. Only the seed varies."

    def med_row(rs):
        rs = sorted(rs, key=lambda r: float(r["sec"]))
        return rs[len(rs) // 2]
    tests = []
    for boss, res, label in (("destroyer", "WIN", "Destroyer win"), ("destroyer", "LOSS", "Destroyer loss"), ("aqua", "WIN", "Aquatic Scourge win")):
        rs = [r for r in brows if r["boss"] == boss and r["cell"] == "baseline" and r["result"] == res]
        r = med_row(rs)
        sec, d = float(r["sec"]), int(r["deaths"])
        t = ("Won in %.0f s. %s." % (sec, ("%d of %d levies lost" % (d, int(r["n"]))) if d else "No levies lost")) if res == "WIN" else ("Lost at %.0f s. All %d levies died." % (sec, d))
        tests.append((boss, r, "Baseline, seed %s. %s %s" % (r["seed"], t, FIX)))
    for boss, r, note in tests:
        ents.append({"set": "test", "path": os.path.join(a.battery, r["file"]), "id": "test__" + slug(os.path.basename(r["file"]).replace(".levyrec", "")), "src": os.path.basename(r["file"]),
                     "f": {"boss": BOSS[boss][0], "note": note, "result": r["result"].lower(), "_row": r}})

    # --- make the data
    jobs = [(e["path"], a.hz, e.get("suffix", "")) for e in ents if "path" in e]
    blobs, hzs = {}, {}
    steps = [a.hz, 10, 6]
    level = {e["id"]: 0 for e in ents if "path" in e}
    paths = {e["id"]: (e["path"], e.get("suffix", "")) for e in ents if "path" in e}
    with Pool(a.jobs) as pool:
        for e, (blob, hz) in zip([e for e in ents if "path" in e], pool.map(build_blob, jobs)):
            blobs[e["id"]] = blob; hzs[e["id"]] = hz
        for e in ents:
            if "old" in e:
                blobs[e["id"]] = open(e["old"], "rb").read()
                hzs[e["id"]] = json.loads(blobs[e["id"]])["hdr"].get("hz", 15)
        limit = int(a.max_mb * 1048576)
        while sum(map(len, blobs.values())) + 400000 > limit or max(map(len, blobs.values())) > ms.MAX_FILE:
            cand = [i for i in level if level[i] < 2 and not i.startswith("eve__")] or [i for i in level if level[i] < 2]
            if not cand:
                break
            big = max(cand, key=lambda i: len(blobs[i]))
            level[big] += 1
            blobs[big], hzs[big] = build_blob((paths[big][0], steps[level[big]], paths[big][1]))
            print("thinned %s to %d Hz" % (big, hzs[big]), file=sys.stderr)
    # summaries for the lab/test fights (need the recording itself)
    with Pool(a.jobs) as pool:
        sums = [x[0] for x in pool.map(scan_rec, [e["path"] for e in ents if e["set"] in ("lab", "test")])]
    for e, info in zip([e for e in ents if e["set"] in ("lab", "test")], sums):
        s = info["s"]; f = e["f"]
        row = f.pop("_row")
        s.update({"boss": f["boss"], "note": f["note"], "result": f["result"]})
        s["mode"] = PAIR.get(row.get("pair") or "mdeath", "Master+Death")
        if row.get("levies"): s["levies"] = int(row["levies"])
        elif row.get("n"): s["levies"] = int(row["n"])
        e["f"] = dict(s, _odd=f.get("_odd", False))
    for e in ents:
        if e["set"] == "eve" and "path" in e:
            pass

    for e in ents:
        f = e["f"]; f.pop("_odd", None)
        f.pop("bytes_raw", None)
        f.update({"id": e["id"], "file": "data/%s.json" % e["id"], "bytes": len(blobs[e["id"]]), "hz": hzs[e["id"]], "src": e["src"]})
        open(os.path.join(out, "data", e["id"] + ".json"), "wb").write(blobs[e["id"]])

    def srt_new(es):
        return sorted(es, key=lambda e: e["f"].get("date") or "", reverse=True)
    for e in ents:
        if e["set"] == "eve" and e["f"].get("kind") == "event":
            e["set"] = "events"
    events = srt_new([e for e in ents if e["set"] == "events"])
    eve = srt_new([e for e in ents if e["set"] == "eve"])
    lab = [e for e in ents if e["set"] == "lab"]
    test = [e for e in ents if e["set"] == "test"]
    old = sorted([e for e in ents if e["set"] == "old"], key=lambda e: (e["f"]["boss"], e["f"].get("date") or ""))
    sets = ([{"id": "events", "title": "Events", "kind": "event", "intro": "Whole events: every levy, militia outpost and enemy, from the start of the event to its end.", "fights": [e["f"] for e in events]}] if events else []) + [
        {"id": "eve", "title": "Your fights", "intro": "Everything you recorded, newest first.", "groupBy": "day", "fights": [e["f"] for e in eve]},
        {"id": "lab", "title": "Lab: hardmode bosses", "intro": "Levy 2.0.66 sweep: 34 levies, a standing Spectator commander. Up to 4 wins and 4 losses per boss, Master+Death first, plus every odd fight.",
         "groupBy": "boss", "groupNotes": bnotes, "fights": [e["f"] for e in lab]},
        {"id": "test", "title": "Lab: Destroyer and Aquatic Scourge tests", "intro": "Baseline cells of the Destroyer and Aquatic Scourge batteries.", "fights": [e["f"] for e in test]},
        {"id": "old", "title": "Older lab fights (outdated)", "intro": "Kept because no newer recording exists for these bosses.", "fights": [e["f"] for e in old]},
    ]
    # default: Eve's most recent Plantera loss
    pl = [e for e in eve if e["f"]["boss"] == "Plantera" and e["f"]["result"] == "loss"]
    default = pl[0]["id"] if pl else (eve or events)[0]["id"]
    man = {"version": 2, "default": default, "current": ms.CURRENT, "eras": ms.ERAS, "sets": sets}
    json.dump(man, open(os.path.join(out, "data", "manifest.json"), "w"), separators=(",", ":"))
    shutil.copyfile(os.path.join(HERE, "site", "index.html"), os.path.join(out, "index.html"))
    files = sorted("data/" + f for f in os.listdir(os.path.join(out, "data")))
    if a.files_json:
        json.dump(files, open(a.files_json, "w"), indent=0)
    total = sum(os.path.getsize(os.path.join(out, x)) for x in files) + os.path.getsize(os.path.join(out, "index.html"))
    print("sets:", [(s["title"], len(s["fights"])) for s in sets], file=sys.stderr)
    print("data files %d (with manifest), total %.1f MB, largest %.2f MB" % (len(files), total / 1048576, max(len(b) for b in blobs.values()) / 1048576), file=sys.stderr)
    if len(files) > a.max_files:
        print("WARNING: over the file ceiling", file=sys.stderr)


if __name__ == "__main__":
    main()
