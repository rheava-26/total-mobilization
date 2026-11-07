import os, gzip, json, re, sys, csv
D = '/home/user/lab/rec'
idx = {}
for fn in sorted(os.listdir(D)):
    p = os.path.join(D, fn)
    op = gzip.open if fn.endswith('.gz') else open
    try:
        with op(p, 'rt') as fh:
            head = fh.readline()
        m = re.search(r'"cellkey"\s*:\s*"([^"]*)"', head)
        idx.setdefault(m.group(1) if m else '?', []).append(fn)
    except Exception as e:
        pass
json.dump(idx, open('/home/user/lab/rec_index.json', 'w'), indent=0)
print(len(idx), 'cells,', sum(len(v) for v in idx.values()), 'files')
print(list(idx.items())[:3])
