#!/usr/bin/env bash

# set -o xtrace
set -o errexit
set -o pipefail
set -o nounset

SCRIPT_DIR=$( cd -- "$( dirname -- "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )

FROM_REF="${1:-"origin/master"}"

S3_BUCKET="${S3_BUCKET:-"combatlogassets"}"
CP_ARGS="--cache-control 'public, max-age=31536000, immutable'"

AWS_DRY_RUN="${AWS_DRY_RUN:-""}"
if [ ! -z "$AWS_DRY_RUN" ]; then
  CP_ARGS="${CP_ARGS} --dryrun"
fi;

echo "=> About to copy"
${SCRIPT_DIR}/changed-since.sh ${FROM_REF}

echo "=> Copying"
${SCRIPT_DIR}/changed-since.sh ${FROM_REF} \
  | xargs -I{} -- \
    bash -c "if [ -f '{}' ]; then aws s3 cp ${CP_ARGS} '{}' 's3://${S3_BUCKET}/{}'; fi;"