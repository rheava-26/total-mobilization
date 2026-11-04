#!/usr/bin/env bash
# Runs the combat harness as N parallel shards (each its own server process, port and world copy), then merges the TSVs.
# Usage: tools/run_harness_sharded.sh N out.tsv [extra run_harness.sh args, e.g. -m CalamityMod -f substring]
# The vanilla controls run in shard 0 only. enabled.json is set once for all shards and restored when they have all finished.
set -u
N=${1:?shards}; OUT=${2:?out.tsv}; shift 2
HERE=$(cd "$(dirname "$0")" && pwd)
ENABLED=/root/.local/share/Terraria/tModLoader/Mods/enabled.json
MOD=ThoriumMod
args=("$@")
for ((i=0;i<${#args[@]};i++)); do [ "${args[$i]}" = "-m" ] && MOD=${args[$((i+1))]}; done
echo "[\"Levy\",\"Mechanization\",\"$MOD\"]" > "$ENABLED"
PIDS=()
for ((k=0;k<N;k++)); do
  extra=(); [ $k -gt 0 ] && extra=(-c)
  "$HERE/run_harness.sh" -k -s "$k/$N" -p $((7777+k)) -o "$OUT.shard$k" "${extra[@]}" "${args[@]}" > "$OUT.shard$k.out" 2>&1 &
  PIDS+=($!)
  sleep 20   # stagger so the servers do not all unpack the mods at once
done
for p in "${PIDS[@]}"; do wait "$p"; done
echo '["Levy","Mechanization"]' > "$ENABLED"
head -1 "$OUT.shard0" > "$OUT"
for ((k=0;k<N;k++)); do tail -n +2 "$OUT.shard$k" >> "$OUT"; done
echo "merged $(($(wc -l < "$OUT") - 1)) rows -> $OUT ; enabled.json restored: $(cat "$ENABLED")"
