"""Helper: montage of art3 concept PNGs next to vanilla gadget/staff refs (reads scratchpad copies)."""
import glob
import subprocess
import sys

S = '/tmp/claude-0/-home-user-total-mobilization/9fbf7c25-c34e-53c7-98cd-36a181fc49d3/scratchpad'
A = S + '/art3png/'
names = ['command_radio', 'gopro_a_cam', 'gopro_carry_icon', 'guide_to_autonomy', 'sentry_rod', 'sentry_cane',
         'sentry_staff', 'static_camera_item', 'static_camera_tile']
from PIL import Image
for n in names:
    print(n, Image.open(A + n + '.png').size)
files = [A + n + '.png' for n in names] + ['Cell_Phone', 'Radar', 'Ballista_Rod', 'Ballista_Cane', 'Ballista_Staff',
                                           'Flameburst_Rod', 'Film_Projector', 'Radio_Thing', 'Weather_Radio']
subprocess.check_call([sys.executable, '-I', S + '/../../../../home/user/total-mobilization/.claude/worktrees/agent-aa2cf3c0bc464d2ea/levy-mod/Levy/tools/art4/view.py'
                       if False else __file__.rsplit('/', 1)[0] + '/view.py', S + '/art4/v_art3.png', '5'] + files)
