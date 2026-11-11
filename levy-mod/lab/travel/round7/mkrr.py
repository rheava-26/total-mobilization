#!/usr/bin/env python3
# mkrr.py : jobs for the noise reruns and the chasm-leg army sizes (both builds)
L = '/home/user/lab/t7'
out = []
for n in range(12, 22):
    out.append(f'bash {L}/rgjob2.sh base cb_n{n} N={n}')
    out.append(f'bash {L}/rgjob2.sh fix2 ca_n{n} N={n}')
for T, G in (('base', 'b3'), ('fix2', 'a3')):
    for rep in (1, 2, 3):
        out.append(f'env CMDFAR=600 HOLD=1 GEAR=hooks N=13 bash {L}/r1.sh {T} {G}_rep{rep}_hfar_M2_x1700 world M2 map 0 800 1700 9000')
        for w, x in (('M1', 700), ('M2', 700), ('M2', 3700)):
            out.append(f'env CMDFAR=0 HOLD=1 CMDTRACK=1 LEVY_TRAVEL_NOHOP=1 GEAR=hooks N=13 bash {L}/r1.sh {T} {G}_rep{rep}_hnear_{w}_x{x} world {w} map 0 800 {x} 9000')
open(f'{L}/jobs_rr.txt', 'w').write('\n'.join(out) + '\n')
print(len(out))
