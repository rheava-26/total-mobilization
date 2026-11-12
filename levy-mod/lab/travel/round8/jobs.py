#!/usr/bin/env python3
# jobs.py TMOD TAG SET   -> prints one shell command per run (SET: mixed | climb | far | near | farclimb | nearclimb | fol)
import sys
t, tag, st = sys.argv[1:4]
import os
R = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'r1.sh')
def job(env, name, course, world, mode, f, to, x0, cap):
    print(f"env {env} bash {R} {t} {tag}_{name} {course} {world} {mode} {f} {to} {x0} {cap}")
def worlds(env, longonly=False, med=True, prefix=''):
    for w in ('M1', 'M2'):
        for x in (700, 1700, 2700, 3700, 4700):
            job(env, f"{prefix}{w}_x{x}_long", 'world', w, 'map', 0, 800, x, 9000)
            if not longonly:
                job(env, f"{prefix}{w}_x{x}_med1", 'world', w, 'map', 0, 150, x, 4000)
                job(env, f"{prefix}{w}_x{x}_med2", 'world', w, 'map', 0, 150, x + 300, 4000)
                job(env, f"{prefix}{w}_x{x}_med3", 'world', w, 'map', 0, -150, x + 600, 4000)
def courses(env, prefix=''):
    for k in range(6):
        f = 20 + 150 * k
        job(env, f"{prefix}zoo_Z{k}", 'zoo', 'LevyTest', 'map', f, f + 150, 0, 6000)
    for k in range(5):
        f = 20 + 150 * k
        job(env, f"{prefix}cor_C{k}", 'corrupt', 'LevyTest', 'map', f, f + 150, 0, 6000)
    for k in range(6):
        f = 20 + 150 * k
        job(env, f"{prefix}walk_L{k}", 'walk', 'LevyTest', 'map', f, f + 150, 0, 6000)
    job(env, f"{prefix}walk_full", 'walk', 'LevyTest', 'map', 0, 920, 0, 12000)
if st == 'mixed':
    e = 'CMDFAR=20'; worlds(e); courses(e)
elif st == 'climb':
    e = 'CMDFAR=20 GEAR=climb'; worlds(e, prefix='g_'); courses(e, prefix='g_')
elif st == 'far':
    worlds('CMDFAR=600 HOLD=1', longonly=True, prefix='far_')
elif st == 'farclimb':
    worlds('CMDFAR=600 HOLD=1 GEAR=climb', longonly=True, prefix='farg_')
elif st == 'near':
    worlds('CMDFAR=0 HOLD=1 CMDTRACK=1', longonly=True, prefix='near_')
elif st == 'nearclimb':
    worlds('CMDFAR=0 HOLD=1 CMDTRACK=1 GEAR=climb', longonly=True, prefix='nearg_')
elif st == 'fol':
    job('CMDFAR=20', 'walk_follow_full', 'walk', 'LevyTest', 'follow', 0, 920, 0, 12000)
    for w in ('M1', 'M2'):
        for x in (700, 2700):
            job('CMDFAR=20', f"{w}_x{x}_fol", 'world', w, 'follow', 0, 800, x, 9000)
elif st == 'zoonear':
    courses('CMDFAR=0 HOLD=1 CMDTRACK=1 GEAR=climb', prefix='zn_')
elif st == 'wall':
    for h in (10, 20, 40, 80, 150):
        job(f'LEVY_TRAVEL_WALLH={h} LEVY_TRAVEL_NOHOP=1 GEAR=hooks N=13 CMDFAR=20', f'wall{h}', 'wall', 'LevyTest', 'map', 40, 250, 0, 9000)
    for w in (14, 20, 30):
        job(f'LEVY_TRAVEL_WALLH=-{w} LEVY_TRAVEL_NOHOP=1 GEAR=hooks N=13 CMDFAR=20', f'chasm{w}', 'wall', 'LevyTest', 'map', 40, 250, 0, 9000)
elif st == 'zoohook':
    for k in (0, 1, 2, 3, 5):
        f = 20 + 150 * k
        job('GEAR=hooks N=13 CMDFAR=20 LEVY_TRAVEL_NOHOP=1', f'zoo_Z{k}', 'zoo', 'LevyTest', 'map', f, f + 150, 0, 6000)
    for k in range(5):
        f = 20 + 150 * k
        job('GEAR=hooks N=13 CMDFAR=20 LEVY_TRAVEL_NOHOP=1', f'cor_C{k}', 'corrupt', 'LevyTest', 'map', f, f + 150, 0, 6000)
elif st == 'quick':
    job('GEAR=hooks N=13 CMDFAR=20 LEVY_TRAVEL_NOHOP=1', 'cor_C2', 'corrupt', 'LevyTest', 'map', 320, 470, 0, 6000)
    job('GEAR=hooks N=13 CMDFAR=20 LEVY_TRAVEL_NOHOP=1', 'zoo_Z5', 'zoo', 'LevyTest', 'map', 770, 920, 0, 6000)
    job('GEAR=hooks N=13 CMDFAR=20 LEVY_TRAVEL_NOHOP=1', 'zoo_Z1', 'zoo', 'LevyTest', 'map', 170, 320, 0, 6000)
elif st == 'hnear':
    worlds('CMDFAR=0 HOLD=1 CMDTRACK=1 LEVY_TRAVEL_NOHOP=1 GEAR=hooks N=13', longonly=True, prefix='hnear_')
elif st == 'hfar':
    worlds('CMDFAR=600 HOLD=1 GEAR=hooks N=13', longonly=True, prefix='hfar_')
elif st == 'hmed':
    worlds('CMDFAR=20 LEVY_TRAVEL_NOHOP=1 GEAR=hooks N=13', prefix='hmed_')
elif st == 'mixednh':
    e = 'CMDFAR=20 LEVY_TRAVEL_NOHOP=1'; worlds(e, prefix='nh_'); courses(e, prefix='nh_')
elif st == 'rwq':
    for (w, x) in (('M2', 3700), ('M2', 1700), ('M1', 1700)):
        job('CMDFAR=600 HOLD=1 GEAR=hooks N=13', f'hfar_{w}_x{x}_long', 'world', w, 'map', 0, 800, x, 9000)
    for (w, x) in (('M1', 700), ('M2', 700), ('M2', 3700)):
        job('CMDFAR=0 HOLD=1 CMDTRACK=1 LEVY_TRAVEL_NOHOP=1 GEAR=hooks N=13', f'hnear_{w}_x{x}_long', 'world', w, 'map', 0, 800, x, 9000)
elif st == 'lava':
    e = 'CMDFAR=20 LEVY_TRAVEL_NOHOP=1'
    for k in range(6):
        f = 20 + 150 * k
        job(e, f'lava_V{k}', 'lava', 'LevyTest', 'map', f, f + 150, 0, 6000)
    for k in (1, 3, 5):
        f = 20 + 150 * k
        job(e + ' LEVY_LAVA_KNOCK=1', f'lavak_V{k}', 'lava', 'LevyTest', 'map', f, f + 150, 0, 6000)
    job(e + ' LEVY_LAVA_DROP=1', 'lavad_V4', 'lava', 'LevyTest', 'map', 620, 770, 0, 6000)
    job(e + ' LEVY_LAVA_DROP=1 GEAR=hooks N=13', 'lavad_hooks', 'lava', 'LevyTest', 'map', 620, 770, 0, 6000)
    job(e + ' GEAR=hooks N=13', 'lavah_V1', 'lava', 'LevyTest', 'map', 170, 320, 0, 6000)
    job(e + ' GEAR=hooks N=13', 'lavah_V5', 'lava', 'LevyTest', 'map', 770, 920, 0, 6000)
elif st == 'lava5':
    e = 'CMDFAR=20 LEVY_TRAVEL_NOHOP=1 GEAR=lavamix N=16'
    for rep in range(3):
        job(e + ' LEVY_LAVA_CLIFF=1 LEVY_LAVA_GLIDE=1', f'lavacliff{rep}_V0', 'lava', 'LevyTest', 'map', 20, 170, 0, 3600)
    for k in (1, 3, 5):
        f = 20 + 150 * k
        job(e + ' LEVY_LAVA_KNOCK=1', f'lavakm_V{k}', 'lava', 'LevyTest', 'map', f, f + 150, 0, 6000)
    job(e + ' LEVY_LAVA_DROP=1', 'lavadm_V4', 'lava', 'LevyTest', 'map', 620, 770, 0, 6000)
    for k in (0, 1, 2, 3, 5):
        f = 20 + 150 * k
        job(e, f'lavam_V{k}', 'lava', 'LevyTest', 'map', f, f + 150, 0, 6000)
elif st == 'reg':
    e = 'GEAR=hooks N=13 CMDFAR=20 LEVY_TRAVEL_NOHOP=1'
    job(e, 'cor_C2', 'corrupt', 'LevyTest', 'map', 320, 470, 0, 6000)
    job(e, 'zoo_Z5', 'zoo', 'LevyTest', 'map', 770, 920, 0, 6000)
    job(e, 'zoo_Z1', 'zoo', 'LevyTest', 'map', 170, 320, 0, 6000)
    for (w, x) in (('M2', 3700), ('M2', 1700), ('M1', 1700)):
        job('CMDFAR=600 HOLD=1 GEAR=hooks N=13', f'hfar_{w}_x{x}_long', 'world', w, 'map', 0, 800, x, 9000)
    for (w, x) in (('M1', 700), ('M2', 700), ('M2', 3700)):
        job('CMDFAR=0 HOLD=1 CMDTRACK=1 LEVY_TRAVEL_NOHOP=1 GEAR=hooks N=13', f'hnear_{w}_x{x}_long', 'world', w, 'map', 0, 800, x, 9000)
    courses('CMDFAR=20 LEVY_TRAVEL_NOHOP=1', prefix='nh_')
elif st in ('ravw', 'ravb', 'lipw', 'lipb', 'lipbo'):
    # carved seeded courses, whole trek 20 -> 920, commander on screen (no hops)
    course = 'ravine' if st.startswith('rav') else 'lips'
    gear = {'ravw': 'wmix', 'ravb': 'boots', 'lipw': 'wmix', 'lipb': 'boots', 'lipbo': 'none'}[st]
    n = 8 if gear == 'wmix' else 6
    extra = ' LEVY_TRAVEL_RAVMODE=walk' if st in ('ravb',) else ''
    for sd in range(1, 13):
        job(f'CMDFAR=20 LEVY_TRAVEL_NOHOP=1 GEAR={gear} N={n} LEVY_TRAVEL_SEED={sd}{extra}', f'{st}_s{sd}', course, 'LevyTest', 'map', 20, 920, 0, 14000)
elif st in ('wrw', 'wrb'):
    gear = 'wmix' if st == 'wrw' else 'boots'
    n = 8 if st == 'wrw' else 6
    for w in ('M1', 'M2', 'W3', 'W4', 'W5', 'W6'):
        for x in (900, 2300, 3700, 5100):
            job(f'CMDFAR=20 LEVY_TRAVEL_NOHOP=1 GEAR={gear} N={n}', f'{st}_{w}_x{x}', 'world', w, 'map', 0, 800, x, 9000)
elif st == 'q':
    job('CMDFAR=20 LEVY_TRAVEL_NOHOP=1 GEAR=wmix N=8 LEVY_TRAVEL_SEED=1', 'q_ravw_s1', 'ravine', 'LevyTest', 'map', 20, 920, 0, 14000)
    job('CMDFAR=20 LEVY_TRAVEL_NOHOP=1 GEAR=wmix N=8 LEVY_TRAVEL_SEED=1', 'q_lipw_s1', 'lips', 'LevyTest', 'map', 20, 920, 0, 14000)
