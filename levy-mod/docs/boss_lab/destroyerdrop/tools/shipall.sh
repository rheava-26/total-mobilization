#!/usr/bin/env bash
# the full ship pipeline on PRIVATE paths, worlds one after another: shipall.sh REPO_DIR
set -u
REPO_DIR=$1
. "$(dirname "${BASH_SOURCE[0]}")/shipenv.sh"
export REPO=$REPO_DIR SHIPWORLD_SERIAL=1
cd "$REPO_DIR"
exec levy-mod/tools/ship/shipboth.sh
