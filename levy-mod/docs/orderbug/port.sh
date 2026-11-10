#!/usr/bin/env bash
# usage: port.sh <rev> <name> : exports levy-mod/Levy at <rev> into src/<name>, drops the orderobey harness onto it, builds <name>.tmod
set -eu
REV="$1"; N="$2"
B=/home/user/lab/ob; W=/home/user/total-mobilization/.claude/worktrees/agent-a6fe3ea2f81f8378b
rm -rf $B/src/$N; mkdir -p $B/src/$N
cd $W
git archive "$REV" levy-mod/Levy -o $B/src/$N.tar
tar -x -f $B/src/$N.tar -C $B/src/$N
D=$B/src/$N/levy-mod/Levy
S=$W/levy-mod/Levy
cp $S/Common/Testing/CombatHarness.OrderObey.cs $S/Common/Testing/CombatHarness.OrderProbe.cs $D/Common/Testing/
python3 - "$D" <<'PYEOF'
import sys
p=sys.argv[1]+'/Common/Testing/CombatHarness.Movement.cs'
s=open(p).read()
if 'orderobey' not in s:
    s=s.replace('''			if (string.Equals(Scenario, "ordermix", StringComparison.OrdinalIgnoreCase)) return BeginOrderMix();
''','''			if (string.Equals(Scenario, "ordermix", StringComparison.OrdinalIgnoreCase)) return BeginOrderMix();
			if (string.Equals(Scenario, "orderobey", StringComparison.OrdinalIgnoreCase)) return BeginOrderObey();
''',1)
    s=s.replace('''			if (string.Equals(Scenario, "ordermix", StringComparison.OrdinalIgnoreCase)) { StepOrderMix(); return; }
''','''			if (string.Equals(Scenario, "ordermix", StringComparison.OrdinalIgnoreCase)) { StepOrderMix(); return; }
			if (string.Equals(Scenario, "orderobey", StringComparison.OrdinalIgnoreCase)) { StepOrderObey(); return; }
''',1)
    open(p,'w').write(s)
p=sys.argv[1]+'/Common/Testing/CombatHarness.CalRev.cs'
s=open(p).read()
k='\t\t\tint alive = 0;\n\t\t\tforeach (var l in st.Levies)\n\t\t\t{\n\t\t\t\tif (l.Dead) continue;'
if 'CrOrderProbe' not in s and k in s:
    s=s.replace(k,'\t\t\tCrOrderProbe(st);\n'+k,1); open(p,'w').write(s)
PYEOF
cd $B && ./build.sh $D $B/$N.tmod
