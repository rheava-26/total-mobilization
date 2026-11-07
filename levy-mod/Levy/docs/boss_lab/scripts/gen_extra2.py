import sys
# lab 2 extra seeds (7-12) for the cells that were close after 6 seeds. Writes /home/user/lab/specs2/{main_x,fearoff_x,spreadoff_x,spreadon_x}.tsv
def line(boss, mode, seed, **kw):
    cap = kw.pop('cap', 18000)
    kw.setdefault('gear', 'mod'); kw.setdefault('n', 'cp'); kw.setdefault('cmd', 'near')
    if boss == 'qb': kw.setdefault('jungle', '1')
    return f"fight\t{boss}\t{mode}\t{seed}\t{cap}" + ''.join(f"\t{k}={v}" for k, v in kw.items())
def write(name, L): open(f'/home/user/lab/specs2/{name}.tsv', 'w').write('\n'.join(L) + '\n'); print(name, len(L))
S = range(7, 13)
main = [('eye', ['rev']), ('eow', ['exp']), ('skel', ['exp']), ('qb', ['exp', 'rev', 'death']), ('kslime', ['rev', 'death'])]
write('main_x', [line(b, m, s) for s in S for b, ms in main for m in ms])
fo = [('qb', ['exp', 'rev', 'death']), ('eye', ['rev']), ('eow', ['exp'])]
write('fearoff_x', [line(b, m, s, fear='0') for s in S for b, ms in fo for m in ms])
sp = [('ds', ['exp']), ('eow', ['exp'])]
write('spreadoff_x', [line(b, m, s) for s in S for b, ms in sp for m in ms])
write('spreadon_x', [line(b, m, s) for s in S for b, ms in sp for m in ms])
