import sys, os
# usage: gen_opt.py  -> writes /home/user/lab/specs_o/*.tsv
os.makedirs('/home/user/lab/specs_o', exist_ok=True)
CAP = 18000
CELLS = [('eye', 'rev', ''), ('brain', 'rev', ''), ('qb', 'exp', '\tjungle=1'), ('eow', 'exp', ''), ('ds', 'exp', ''), ('skel', 'exp', '')]
ARMS = {
    'base': '',
    'fm_events': '\tfm=events',
    'fm_slow': '\tfm=proximity_slow',
    'fm_rally': '\tfm=rally',
    'sv_parity': '\tsurv=parity',
    'sv_veteran': '\tsurv=veteran',
    'sv_formation': '\tsurv=formation',
}
def write(name, lines):
    open(f'/home/user/lab/specs_o/{name}.tsv', 'w').write('\n'.join(lines) + '\n'); print(name, len(lines))
def arm_lines(extra, seeds=range(1, 7), cells=CELLS):
    L = []
    for seed in seeds:
        for boss, mode, jg in cells:
            L.append(f"fight\t{boss}\t{mode}\t{seed}\t{CAP}\tgear=mod\tn=cp\tcmd=near{jg}{extra}")
    return L
if __name__ == '__main__':
    # smoke: eye rev seed 1, every arm
    write('smoke', [f"fight\teye\trev\t1\t{CAP}\tgear=mod\tn=cp\tcmd=near{e}" for e in ARMS.values()])
    for k, e in ARMS.items():
        write('o_' + k, arm_lines(e))
