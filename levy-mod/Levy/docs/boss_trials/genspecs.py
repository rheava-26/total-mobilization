#!/usr/bin/env python3
# generates spec_A..F.tsv (tab separated). columns: mode diff order auto comp stats tier grade trials/cfgs cap extra
import os
DIR = os.path.dirname(os.path.abspath(__file__))

def line(mode, diff, order, auto, comp, stats, tier, grade, seeds, cap, extra):
    return "\t".join([mode, diff, order, str(auto), comp, stats, tier, grade, seeds, str(cap), extra])

def w(name, lines):
    open(f"{DIR}/spec_{name}.tsv", "w").write("\n".join(lines) + "\n")

def roll(diff, order, auto, boss, cfgs, extra="", comp="any"):
    tier = "post" if boss == "brain" else "pre"
    return line("roll", diff, order, auto, comp, "roll", tier, "levy", cfgs, 18000, f"boss={boss};trials=3" + (";" + extra if extra else ""))

# A: classic main, 20 configs x 3 trials, both orders, 3 bosses
A = [roll("classic", o, 1, b, f"{c}-{c}") for c in range(1, 13) for b in ("eye", "kslime", "brain") for o in ("free", "sd")]
# B: expert main
B = [roll("expert", o, 1, b, f"{c}-{c}") for c in range(1, 9) for b in ("eye", "kslime", "brain") for o in ("free", "sd")]
# C: paired toggles on the first 12 configs (classic, free rein)
C = []
for c in range(1, 7):
    for b in ("eye", "brain"):
        C.append(roll("classic", "free", 0, b, f"{c}-{c}"))                      # no autonomy
        for tog in ("camp=0", "potions=0", "arena=flat", "arena=nat", "arena=small", "honey=1", "camp=0;potions=0"):
            C.append(roll("classic", "free", 1, b, f"{c}-{c}", tog))
# D: the count curve (identical loadouts, only the count changes): rangers and mages, Eye + Brain, both difficulties
D = []
for diff in ("classic", "expert"):
    for b in ("eye", "brain"):
        for cls in ("R", "M"):
            for n in (4, 6, 8, 10, 12, 15, 20, 30):
                D.append(line("fight", diff, "free", 1, f"{cls}{n}", "real", "opt", "levy", "1-3", 18000, f"boss={b}"))
MIX = {4: "R1M1E1S1", 6: "R2M2E1S1", 8: "R2M2E2S2", 10: "R3M3E2S2", 12: "R4M4E2S2", 15: "R5M5E3S2", 20: "R6M6E4S4", 30: "R9M9E6S6"}
for diff in ("classic", "expert"):
    for b in ("eye", "brain"):
        for n, code in MIX.items():
            D.append(line("fight", diff, "free", 1, code, "real", "opt", "levy", "1-3", 18000, f"boss={b}"))
# E: fixed tiers (junk / suboptimal / optimal) x compositions x stats, Eye, both difficulties, 5 seeds
E = []
for diff in ("classic", "expert"):
    for comp in ("m10", "a4", "b4"):
        for stats in ("best", "real"):
            for tier in ("opt", "sub", "junk"):
                E.append(line("fight", diff, "free", 1, comp, stats, tier, "levy", "1-5", 18000, "boss=eye"))
    for comp in ("m10", "a4", "b4"):
        for stats in ("best", "real"):
            for tier in ("opt", "sub", "junk"):
                E.append(line("fight", diff, "free", 1, comp, stats, tier, "levy", "1-5", 18000, "boss=brain"))
    for comp in ("m10", "a4"):
        for tier in ("opt", "sub"):
            for b in ("kslime",):
                E.append(line("fight", diff, "free", 1, comp, "real", tier, "levy", "1-5", 18000, f"boss={b}"))
# best possible per boss (400 hp / 200 mana, six potions, ten levies) and the melee pick comparison (part C)
E.append(line("fight", "classic", "free", 1, "m10", "best", "opt", "champ", "1-5", 18000, "boss=eye"))
for b in ("kslime", "eye", "brain"):
    for diff in ("classic", "expert"):
        E.append(line("fight", diff, "free", 1, "m10", "best", "opt", "levy", "1-5", 18000, f"boss={b}"))
pool = "Enchanted Sword|Starfury|Blade of Grass|Katana|Platinum Broadsword|Gold Broadsword"
for beam in ("0", "1"):
    for tier in ("opt",):
        for diff in ("classic", "expert"):
            E.append(line("fight", diff, "free", 1, "E4", "best", tier, "levy", "1-5", 18000, f"boss=eye;beam={beam};pick:E={pool}"))
            E.append(line("fight", diff, "free", 1, "E4", "real", tier, "levy", "1-5", 18000, f"boss=eye;beam={beam};pick:E={pool}"))
            E.append(line("fight", diff, "free", 1, "E4", "best", tier, "levy", "1-5", 18000, f"boss=kslime;beam={beam};pick:E={pool}"))
for diff in ("classic", "expert"):
    for b in ("eye", "brain"):
        for w_ in ("Enchanted Sword", "Platinum Broadsword", "Copper Shortsword"):
            for stats in ("real", "best"):
                E.append(line("fight", diff, "free", 1, "E4", stats, "opt", "levy", "1-5", 18000, f"boss={b};w:E={w_}"))
# F: player-reference DPS control (levy vs player, same gear), classic eye dummy
F = []
refs = [("R", "Minishark;a:R=Silver Bullet"), ("R", "Boomstick;a:R=Musket Ball"), ("R", "Demon Bow;a:R=Flaming Arrow"), ("R", "Tendon Bow;a:R=Flaming Arrow"), ("R", "Wooden Bow;a:R=Wooden Arrow"),
        ("R", "Phoenix Blaster;a:R=Silver Bullet"), ("R", "Handgun;a:R=Silver Bullet"),
        ("M", "Diamond Staff;a:M="), ("M", "Ruby Staff;a:M="), ("M", "Wand of Sparking;a:M="), ("M", "Vilethorn;a:M="), ("M", "Space Gun;a:M="), ("M", "Amethyst Staff;a:M="), ("M", "Aqua Scepter;a:M="), ("M", "Magic Missile;a:M="),
        ("E", "Enchanted Sword"), ("E", "Starfury"), ("E", "Blade of Grass"), ("E", "Muramasa"), ("E", "Platinum Broadsword"), ("E", "Night's Edge"), ("E", "Copper Shortsword"),
        ("S", "Slime Staff"), ("S", "Finch Staff"), ("S", "Flinx Staff"), ("S", "Imp Staff")]
for cls, wpn in refs:
    name = wpn.split(";")[0]
    for stats in ("best", "real"):
        for diff in ("classic",):
            F.append(line("ref", diff, "free", 1, f"{cls}1", stats, "opt", "levy", "1", 0, f"boss=eye;w:{cls}={wpn}"))
E2 = []
for seed in range(1, 4):
    for l in E:
        a = l.split("\t"); a[8] = f"{seed}-{seed}" if a[8] == "1-5" else a[8]
        if a[8] == f"{seed}-{seed}": E2.append("\t".join(a))
E = E2
for n, L in (("A", A), ("B", B), ("C", C), ("D", D), ("E", E), ("F", F)):
    w(n, L)
    print(n, len(L))
