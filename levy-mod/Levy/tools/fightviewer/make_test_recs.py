#!/usr/bin/env python3
"""Write a few synthetic format-8 recordings to test viewer.html and the other readers without booting the game.

  make_test_recs.py OUTDIR

Writes into OUTDIR: a won and a lost .levyrec (gzip JSON lines, sync-flushed every 2 s like the game's writer, with prof / dmg / cdeath events and
an end record), a *_crashed.levyrec cut off in the middle of its gzip stream, a plain format-3 .jsonl, and a readme.txt (not a recording).
The data follows the format comment at the top of Common/Recording/FightRecFormat.cs. It is made up (seeded, so reruns match): it checks the file
path and the readers, not the fight.
"""
import gzip, json, math, os, random, sys, zlib

TICKS = 60


def rows_for(boss, seed, secs, win, cmd_deaths, fmt=8, army_n=12, slow=None):
    rnd = random.Random(seed)
    out = []
    hdr = {"k": "hdr", "format": fmt, "meta": {}, "headless": 0, "levy": "0.9.9", "mods": [["Levy", "0.9.9"]], "mode": "Expert", "boss": boss.replace(" ", "_"),
           "player": "Eve", "startTick": 1000, "hz": 30, "world": "Test", "date": "2026-11-14T20:05:12",
           "stateNames": "Idle,Follow,Fight,Flee"}
    if fmt >= 8:
        hdr["meta"] = {"source": "auto", "trigger": "boss", "setting": "Expert", "label": boss.replace(" ", "_")}
        hdr["auto"] = 1
        hdr["gzip"] = 1
        hdr["cmd"] = {"life": 400, "mana": 200, "defense": 22, "armor": [1, 2, 3], "accessories": [4, 5], "hotbar": [10, 11, 12], "ammo": [14]}
        hdr["army"] = {"n": army_n, "avgLife": 180, "classes": {"melee": 5, "ranged": 4, "magic": 3}, "grades": {"A": 4, "B": 8}, "weapons": [[10, 6], [11, 4], [12, 2]]}
    out.append(hdr)
    # two arena records (format 4+): the start area and a strip to its right
    def arena(x0, w):
        h = 30
        rows = []
        for y in range(h):
            rows.append([0, w] if y < 24 else [1, w])
        return {"k": "arena", "x0": x0, "y0": 100, "w": w, "h": h, "rows": rows}
    out.append(arena(1000, 120))
    out.append(arena(1120, 80))
    cx, cy = 1100 * 16, 124 * 16
    n_frames = secs * TICKS // 2
    bhp, bmax = 4000, 4000
    lives = [180] * army_n
    dead = set()
    names = {"items": {"10": "Copper Shortsword", "11": "Wooden Bow", "12": "Wand of Sparking", "21": "Lead Pickaxe"},
             "npcs": {"4": boss, "5": "Servant of Cthulhu"}, "projectiles": {"1": "Wooden Arrow", "2": "Spark"}, "buffs": {}, "soldiers": {str(i): "Levy %d" % i for i in range(army_n)}}
    events = []
    cdeaths_left = list(range(int(secs * TICKS * 0.35), int(secs * TICKS * 0.8), max(60, secs * TICKS // (cmd_deaths + 1) // 2)))[:cmd_deaths] if cmd_deaths else []
    for i in range(n_frames):
        t = i * 2
        ang = t / 90.0
        bx, by = cx + 400 * math.cos(ang), cy - 300 + 120 * math.sin(ang * 1.7)
        bhp = max(0, bhp - 4000 // n_frames * (1.0 if cmd_deaths == 0 else 0.7))
        if rnd.random() < 0.02 and len(dead) < army_n - 2:
            k = rnd.choice([j for j in range(army_n) if j not in dead])
            dead.add(k)
            events.append({"k": "e", "t": t, "e": "ldeath", "id": k, "x": cx, "y": cy, "npc": 4, "by": "body"})
        if rnd.random() < 0.05:
            k = rnd.choice([j for j in range(army_n) if j not in dead])
            events.append({"k": "e", "t": t, "e": "lhit", "id": k, "dmg": rnd.randint(8, 40), "src": "body", "by": "boss", "npc": 4, "proj": 0, "life": 100, "x": cx, "y": cy, "kill": 0})
        px = cx + 120 * math.sin(t / 200.0)
        p = [px, cy, 2, 0, 1, 1, 400, 100, 0, 0, 0, 10, 0, 0, 0, 0]
        if cdeaths_left and t >= cdeaths_left[0]:
            cdeaths_left.pop(0)
            events.append({"k": "e", "t": t, "e": "cdeath", "x": px, "y": cy, "n": 1})
        f = {"k": "f", "t": t, "p": p,
             "b": [[4, 0, bx, by, 3, 1, int(bhp), bmax, 0, 0, 0, 0, 255, 0, 2 if i % 200 < 100 else 1, i % army_n, 100, 100]],
             "l": [[j, cx - 200 + j * 30 + 5 * math.sin(t / 30 + j), cy, 0, 0, 1, lives[j], 2, 10 + j % 3, 0, 180, 20, 42] for j in range(army_n) if j not in dead],
             "j": [[1, bx, by, 0, 4, 20, 8, 8]] if i % 7 == 0 else [], "a": []}
        out.append(f)
    if fmt >= 8:
        classes = [("melee", "item", 10), ("ranged", "item", 11), ("magic", "item", 12), ("ranged", "proj", 1), ("magic", "proj", 2)]
        for s in range(2, secs, 1):
            d = []
            for cl, kind, typ in classes:
                d.append(["levy", cl, kind, typ, 1, rnd.randint(40, 400), rnd.randint(1, 6)])
            d.append(["cmd", "melee", "item", 21, 0, rnd.randint(1, 50), 1])
            events.append({"k": "e", "t": s * TICKS, "e": "dmg", "d": d})
        # profiler windows of 2 s; one stretch of lag
        for s in range(2, secs + 1, 2):
            lag = slow and slow[0] <= s <= slow[1]
            fps = rnd.uniform(14, 22) if lag else rnd.uniform(56, 61)
            events.append({"k": "e", "t": s * TICKS, "e": "prof", "fps": round(fps, 1), "ups": 60 if not lag else 40, "upd": round(rnd.uniform(2, 5) if not lag else rnd.uniform(18, 25), 2),
                           "npc": 1.1, "proj": 0.4, "draw": round(rnd.uniform(6, 10) if not lag else rnd.uniform(30, 50), 2), "ai": 0.9, "gear": 0.1, "top": "combat 3.2, dodge 1.1", "win": 2})
    events.sort(key=lambda e: e["t"])
    # interleave events by tick
    merged, ei = [out[0], out[1], out[2]], 0
    for f in out[3:]:
        while ei < len(events) and events[ei]["t"] <= f["t"]:
            merged.append(events[ei]); ei += 1
        merged.append(f)
    merged += events[ei:]
    return merged, names, bhp


def finish(rows, names, bhp, result, secs, cmd_deaths, army_n, fmt=8):
    rows = list(rows)
    rows.append({"k": "names", **names})
    end = {"k": "end", "reason": "boss killed" if result == "won" else "commander fell", "ticks": secs * TICKS, "frames": sum(1 for r in rows if r["k"] == "f"), "events": sum(1 for r in rows if r["k"] == "e"),
           "outcome": "killed" if result == "won" else "despawn"}
    if fmt >= 8:
        dmg = sum(row[5] for r in rows if r["k"] == "e" and r["e"] == "dmg" for row in r["d"])
        boss = sum(row[5] for r in rows if r["k"] == "e" and r["e"] == "dmg" for row in r["d"] if row[4])
        end.update({"result": result, "durationSec": secs, "bossName": rows[0]["boss"].replace("_", " "), "bosses": 1, "cmdDeaths": cmd_deaths, "levyDeaths": sum(1 for r in rows if r["k"] == "e" and r["e"] == "ldeath"),
                    "levyHits": sum(1 for r in rows if r["k"] == "e" and r["e"] == "lhit"), "armyAlive": army_n - sum(1 for r in rows if r["k"] == "e" and r["e"] == "ldeath"), "armyTotal": army_n,
                    "dmgBoss": boss, "dmgAll": dmg, "profiled": 1, "dropped": 0, "capped": 0})
    rows.append(end)
    return rows


def write_levyrec(path, rows, cut_at=None):
    """gzip with a sync flush every ~2 s of recording, like the game's writer; cut_at (0..1) truncates the file to that fraction (a crash)."""
    z = zlib.compressobj(6, zlib.DEFLATED, 31)
    buf, last = bytearray(), 0
    for r in rows:
        buf += z.compress((json.dumps(r, separators=(",", ":")) + "\n").encode())
        t = r.get("t", 0)
        if t - last >= 2 * TICKS:
            buf += z.flush(zlib.Z_SYNC_FLUSH); last = t
    if cut_at is None:
        buf += z.flush()
    else:
        buf = buf[: int(len(buf) * cut_at)]
    with open(path, "wb") as f:
        f.write(bytes(buf))


def main():
    out = sys.argv[1]
    os.makedirs(out, exist_ok=True)
    r, n, b = rows_for("Eye of Cthulhu", 1, 120, 2, 0, army_n=12, slow=(60, 70))
    write_levyrec(os.path.join(out, "2026-11-14_20-05_Eye_of_Cthulhu_won.levyrec"), finish(r, n, b, "won", 120, 0, 12))
    r, n, b = rows_for("Skeletron", 2, 90, 2, 2, army_n=10, slow=(20, 30))
    r[0]["date"] = "2026-11-13T21:40:00"
    write_levyrec(os.path.join(out, "2026-11-13_21-40_Skeletron_lost.levyrec"), finish(r, n, b, "lost", 90, 2, 10))
    r, n, b = rows_for("Queen Bee", 3, 100, 2, 0, army_n=8)
    r[0]["date"] = "2026-11-15T09:12:00"
    write_levyrec(os.path.join(out, "2026-11-15_09-12_Queen_Bee_won_crashed.levyrec"), finish(r, n, b, "won", 100, 0, 8), cut_at=0.55)
    r, n, b = rows_for("Brain of Cthulhu", 4, 40, 2, 0, fmt=3, army_n=6)
    r[0]["date"] = "2026-10-02T12:00:00"
    r = [x for x in r if x["k"] != "e"]
    r = finish(r, n, b, "won", 40, 0, 6, fmt=3)
    with open(os.path.join(out, "old_format3.jsonl"), "w") as f:
        for x in r:
            f.write(json.dumps(x, separators=(",", ":")) + "\n")
    open(os.path.join(out, "readme.txt"), "w").write("not a recording\n")


if __name__ == "__main__":
    main()
