import gzip, glob, re, collections, sys

def hist(d):
    c = collections.Counter()
    for f in glob.glob(d + '/jobs/*/try*/server.log.gz'):
        for l in gzip.open(f, 'rt', errors='replace'):
            l = re.sub(r'^\[[^\]]*\] \[[^\]]*\] ', '', l.rstrip())
            l = re.sub(r'key=fight\|[^ ]*', 'key=K', l)
            l = re.sub(r'[0-9]+(\.[0-9]+)?', '#', l)[:140]
            c[l] += 1
    return c

a = hist(sys.argv[1]); b = hist(sys.argv[2])
rows = []
for k in set(a) | set(b):
    x, y = a.get(k, 0), b.get(k, 0)
    if abs(x - y) >= max(5, 0.3 * max(x, y)): rows.append((abs(x - y), x, y, k))
for r in sorted(rows, reverse=True)[:40]: print(r[1], r[2], r[3])
