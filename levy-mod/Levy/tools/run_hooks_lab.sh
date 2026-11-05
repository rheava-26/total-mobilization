#!/usr/bin/env bash
# HOOKS LAB runner (Common/Testing/HooksLab.cs; feasibility study for soldier grappling hooks; measurement only).
# Usage: tools/run_hooks_lab.sh [-N] [-S saveDir] [-p port] [-o out.tsv] [-b] <spec | @suitefile>
#   spec: ';'-separated  kind|item|N|ticks|flags   (flags documented at the top of HooksLab.cs); @file = one scenario per line, '#' comments
#   -b  build Levy first (tools/lab_build.sh).  -N  new toolchain (Calamity).  Default save dir: /home/user/tml-save-hooks (needs Mods/ThoriumMod.tmod; never the shared Mods dir).
# Writes <out> (one row per scenario) and <out>.trace.tsv (per-tick trace of soldier 0). The columns are named in the header.
# Suites and the recorded results of the 2026-11-05 study are in tools/hooks_lab_data/.
set -u
NEW=""; SAVE=/home/user/tml-save-hooks; PORT=7851; OUT=""; BUILD=""
while getopts "NS:p:o:b" o; do case $o in N) NEW=1;; S) SAVE=$OPTARG;; p) PORT=$OPTARG;; o) OUT=$OPTARG;; b) BUILD=1;; esac; done
shift $((OPTIND-1))
SPEC="${1:?spec or @file}"
HERE=$(cd "$(dirname "$0")" && pwd)
if [ -n "$BUILD" ]; then
  if [ -n "$NEW" ]; then bash "$HERE/lab_build.sh" -N -S "$SAVE"; else bash "$HERE/lab_build.sh" -S "$SAVE"; fi
fi
ARGS=(-S "$SAVE" -p "$PORT" -s "$SPEC")
[ -n "$OUT" ] && ARGS+=(-o "$OUT")
if [ -n "$NEW" ]; then bash "$HERE/run_lab.sh" -N -m CalamityMod -x CalamityModMusic "${ARGS[@]}"; else bash "$HERE/run_lab.sh" "${ARGS[@]}"; fi
