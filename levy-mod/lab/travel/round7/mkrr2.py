#!/usr/bin/env python3
# mkrr2.py : final-build confirmation: chasm army sizes on fix3, and 6 more reps of the real-world hook leg that moved, both builds
L = '/home/user/lab/t7'
out = []
for n in range(12, 22):
    out.append(f'bash {L}/rgjob2.sh fix3 cc_n{n} N={n}')
for T, G in (('base', 'b4'), ('fix3', 'a4')):
    for rep in range(1, 7):
        out.append(f'env CMDFAR=600 HOLD=1 GEAR=hooks N=13 bash {L}/r1.sh {T} {G}_rep{rep}_hfar_M2_x1700 world M2 map 0 800 1700 9000')
    for rep in range(1, 4):
        out.append(f'env GEAR=hooks N=13 CMDFAR=20 LEVY_TRAVEL_NOHOP=1 bash {L}/r1.sh {T} {G}_rep{rep}_cor_C2 corrupt LevyTest map 320 470 0 6000')
open(f'{L}/jobs_rr2.txt', 'w').write('\n'.join(out) + '\n')
print(len(out))
