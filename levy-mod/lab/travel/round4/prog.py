#!/usr/bin/env python3
# prog.py TAG PREFIX : per run, median end offset of hook levies and arrivals, falls
import sys, glob, csv, os, statistics
tag, pre = sys.argv[1:3]
for f in sorted(glob.glob(f'/home/user/lab/t4/res/{tag}_{pre}*.csv')):
    rows = [r for r in csv.DictReader(open(f)) if r['gear'].startswith('hook')]
    if not rows: continue
    ends = [float(r['endOffset']) for r in rows]
    print(os.path.basename(f)[len(tag) + 1:-4].ljust(24), 'arr', sum(int(r['arrived']) for r in rows), '/', len(rows), 'medEnd', statistics.median(ends), 'falls', sum(int(r['falls']) for r in rows), 'bans', sum(int(r['rtBans']) for r in rows), 'casts', sum(int(r['hkCasts']) for r in rows))
