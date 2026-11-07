import sys
sys.path.insert(0, '/home/user/lab')
from gen_opt import *
# combos: the best fear model + the best survivability option; plus the same with formation stacked
write('o_combo_ep', arm_lines('\tfm=events\tsurv=parity'))
write('o_combo_epf', arm_lines('\tfm=events\tsurv=parity,formation'))
