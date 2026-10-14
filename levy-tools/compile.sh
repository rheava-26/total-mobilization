#!/usr/bin/env bash
# Compile a tModLoader mod source folder against the real tModLoader 1.4.4 (2026.08.3.0) assemblies with Roslyn.
# Usage: compile.sh <mod dir> [extra csc args]   -> prints errors; exit code 0 on success.
set -u
MOD="${1:-/home/user/total-mobilization/levy}"; shift || true
R=/home/user/dotnet; D=/home/user/dotnet-pkgs; T=/home/user/tml-bin/tml
export DOTNET_ROOT=$R
OUT=$(mktemp -d)
REFS=$( (ls $D/x/ref/ref/net8.0/*.dll; echo $T/tModLoader.dll; find $T/Libraries -name "*.dll" | grep -v "/Native/" | grep -v "/runtime" | grep -v "\.resources\.dll" | grep -v tModCodeAssist) | sed 's/^/\/r:/' | tr '\n' ' ')
SRCS=$(find "$MOD" -name "*.cs" -not -path "*/obj/*" -not -path "*/bin/*" -not -path "*/.vs/*")
$R/dotnet $D/x/roslyn412/tasks/netcore/bincore/csc.dll /noconfig /nostdlib /nologo /target:library /langversion:12.0 /nullable:disable /unsafe- \
  /define:JETBRAINS_ANNOTATIONS\;TML_2026_08 /nowarn:CS1701,CS1702,CS1591,CS0618 /warn:3 /out:$OUT/Levy.dll $REFS "$@" $SRCS
RC=$?
if [ $RC -eq 0 ]; then echo "COMPILE OK: $(echo "$SRCS" | wc -l) files -> $OUT/Levy.dll"; fi
rm -rf $OUT
exit $RC
