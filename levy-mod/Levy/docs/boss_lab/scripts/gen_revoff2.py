# QB / Eye lethality question: revoff = Revengeance on, Levy rippers (Rage/Adrenaline for levies) off. Writes /home/user/lab/specs2/revoff.tsv
def line(boss, mode, seed, **kw):
    kw.setdefault('gear', 'mod'); kw.setdefault('n', 'cp'); kw.setdefault('cmd', 'near')
    if boss == 'qb': kw.setdefault('jungle', '1')
    return f"fight\t{boss}\t{mode}\t{seed}\t18000" + ''.join(f"\t{k}={v}" for k, v in kw.items())
L = [line(b, 'revoff', s) for s in range(1, 7) for b in ('qb', 'eye')]
open('/home/user/lab/specs2/revoff.tsv', 'w').write('\n'.join(L) + '\n'); print('revoff', len(L))
