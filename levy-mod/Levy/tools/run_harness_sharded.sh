#!/usr/bin/env bash
# Runs the combat harness as N parallel shards (each its own server process, port and world copy), then merges the TSVs.
# Usage: tools/run_harness_sharded.sh N out.tsv [run_harness.sh args, e.g. -N -m CalamityMod -f substring -C rogue -A Name1,Name2]
#   -N in the args selects the NEW toolchain (tML 2026.10); default is the old one. The vanilla controls run in shard 0 only.
#   enabled.json (of the chosen toolchain) is set once for all shards and the previous content is restored when all have finished.
set -u
N=${1:?shards}; OUT=${2:?out.tsv}; shift 2
HERE=$(cd "$(dirname "$0")" && pwd)
MOD=ThoriumMod; NEW=""
args=("$@")
for ((i=0;i<${#args[@]};i++)); do
  [ "${args[$i]}" = "-m" ] && MOD=${args[$((i+1))]}
  [ "${args[$i]}" = "-N" ] && NEW=1
done
if [ -n "$NEW" ]; then
  ENABLED=/home/user/tml-save-2026.10/Mods/enabled.json
  if [ "$MOD" = "CalamityMod" ]; then LIST="[\"CalamityModMusic\",\"CalamityMod\",\"Levy\",\"Mechanization\"]"; else LIST="[\"$MOD\",\"Levy\",\"Mechanization\"]"; fi
else
  ENABLED=/root/.local/share/Terraria/tModLoader/Mods/enabled.json
  LIST="[\"Levy\",\"Mechanization\",\"$MOD\"]"
fi
PREV=$(cat "$ENABLED" 2>/dev/null || echo '["Levy","Mechanization"]')
echo "$LIST" > "$ENABLED"
PIDS=()
for ((k=0;k<N;k++)); do
  extra=(); [ $k -gt 0 ] && extra=(-c)
  "$HERE/run_harness.sh" -k -s "$k/$N" -p $((7777+k)) -o "$OUT.shard$k" "${extra[@]}" "${args[@]}" > "$OUT.shard$k.out" 2>&1 &
  PIDS+=($!)
  sleep 20   # stagger so the servers do not all unpack the mods at once
done
for p in "${PIDS[@]}"; do wait "$p"; done
printf '%s' "$PREV" > "$ENABLED"
head -1 "$OUT.shard0" > "$OUT"
for ((k=0;k<N;k++)); do tail -n +2 "$OUT.shard$k" >> "$OUT"; done
echo "merged $(($(wc -l < "$OUT") - 1)) rows -> $OUT ; enabled.json restored: $(cat "$ENABLED" | tr -d '\n ')"
