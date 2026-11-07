#!/usr/bin/env bash
# usage: sync.sh "<short message>"   copies raw rows into the repo, commits and pushes boss-lab
set -u
W=/home/user/total-mobilization/.claude/worktrees/agent-a7e7652babfd3ac91
R=$W/levy-mod/Levy/docs/boss_lab
mkdir -p $R/raw $R/scripts
cp /home/user/lab/out/*.tsv $R/raw/ 2>/dev/null
cp /home/user/lab/out/*.dbg.tsv $R/raw/ 2>/dev/null
cp /home/user/lab/gen_specs.py /home/user/lab/tab.py $R/ 2>/dev/null
cp /home/user/lab/build.sh /home/user/lab/run.sh /home/user/lab/sync.sh /home/user/lab/queue.sh $R/scripts/ 2>/dev/null
cp /home/user/lab/specs/*.tsv $R/scripts/ 2>/dev/null
[ -f /home/user/lab/out/tiers.md ] && cp /home/user/lab/out/tiers.md $R/tiers_dump.md
cd $W
printf '%s\n\nCo-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>\nClaude-Session: https://claude.ai/code/session_01QKZziVb146pFyiSgEKZHkk\n' "${1:-boss-lab batch}" > /home/user/lab/commitmsg.txt
git add -A levy-mod
git commit -q -F /home/user/lab/commitmsg.txt && echo committed || echo "nothing to commit"
git push -q origin boss-lab 2>&1 | tail -1
git log --oneline | head -1
