#!/usr/bin/env bash
# Sampling profile of a levylab fight (any spec): the .NET EventPipe sample profiler writes a .nettrace for the server process, nettrace/ turns it into folded stacks, q.py queries them.
#   perf/profile.sh SPEC.yaml OUT_PREFIX        # runs the spec with the profiler on (use a 1-cell, 1-seed spec), writes OUT_PREFIX.nettrace and OUT_PREFIX.folded
#   python3 perf/q.py OUT_PREFIX.folded callees "SoldierNPC.AIBody" 1 30     # what a frame's samples are spent in (aggregated over every call site)
#   python3 perf/q.py OUT_PREFIX.folded callers "Array.Copy"                 # who calls it;  top N = exclusive top
# Needs the .NET 10 SDK at /usr/lib/dotnet (builds nettrace/ once, NuGet: Microsoft.Diagnostics.Tracing.TraceEvent). Caveat: the sampler only stops threads at GC safe points, so time in leaf calls
# (memmove, clock reads, P/Invokes) is over-reported there; trust inclusive numbers of managed frames, cross-check small exclusive ones with LEVY_PROF buckets.
set -eu
SPEC="$1"; OUT="$2"; HERE="$(cd "$(dirname "$0")" && pwd)"
export DOTNET_EnableEventPipe=1 DOTNET_EventPipeOutputPath="$OUT.nettrace" DOTNET_EventPipeConfig='Microsoft-DotNETCore-SampleProfiler:0:5,Microsoft-Windows-DotNETRuntime:0x14C14FCCBD:5'
python3 "$HERE/../levylab" run "$SPEC" --jobs 1
unset DOTNET_EnableEventPipe DOTNET_EventPipeOutputPath DOTNET_EventPipeConfig
[ -f "$HERE/nettrace/bin/Release/net10.0/nt.dll" ] || (cd "$HERE/nettrace" && DOTNET_NOLOGO=1 /usr/lib/dotnet/dotnet build -c Release 2>&1 | tail -2)
/usr/lib/dotnet/dotnet "$HERE/nettrace/bin/Release/net10.0/nt.dll" "$OUT.nettrace" Terraria.Main.DoUpdate 60 7 1 "$OUT.folded" | head -60
