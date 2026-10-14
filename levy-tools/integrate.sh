#!/usr/bin/env bash
# Merge every agent worktree branch that has commits beyond the base into the current branch, then compile.
set -u
cd /home/user/total-mobilization
BASE=$(git merge-base HEAD $(git for-each-ref --format='%(refname:short)' refs/heads/worktree-agent-* | head -1))
for b in $(git for-each-ref --format='%(refname:short)' refs/heads/worktree-agent-*); do
  n=$(git rev-list --count HEAD..$b)
  if [ "$n" -eq 0 ]; then echo "skip $b (no new commits)"; continue; fi
  echo "== merging $b ($n commits): $(git log --oneline -1 $b | cut -c1-80)"
  if ! git -c user.name=Claude -c user.email=noreply@anthropic.com merge --no-ff --no-edit "$b" >/tmp/merge.log 2>&1; then
    echo "!! CONFLICT merging $b"; git diff --name-only --diff-filter=U; git merge --abort; echo "   aborted; resolve manually"
  fi
done
/home/user/tools/compile.sh /home/user/total-mobilization/levy 2>&1 | grep -v "^warning" | tail -40
