#!/usr/bin/env bash

# set -o xtrace
set -o errexit
set -o pipefail
set -o nounset

FROM_REF="${1:-"origin/master"}"

git log --name-only --pretty=oneline --full-index $FROM_REF..HEAD \
  | grep -E '^(json)/' || true
