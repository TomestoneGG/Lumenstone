#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'EOF'
Usage:
  ./regen_icon_groups.sh "<sqpackPath>" <group> [<group> ...] [options]
  ./regen_icon_groups.sh "<sqpackPath>" --all-existing [options]

Groups are icon subdirectories such as 068000. Short forms like 68 and 068
are also accepted and normalize to 068000.

Options:
  --patch <name>         Patch argument passed to dotnet run (default: latest)
  --all-existing         Regenerate every existing icons/NNN000 directory
  --no-commit            Regenerate groups but do not commit them
  --no-push              Commit groups but do not push them
  --no-pause             Do not prompt between groups
  --message-prefix <msg> Commit message prefix (default: Regenerate icons)
  --dry-run              Print the plan without running dotnet or git
  -h, --help             Show this help

Examples:
  ./regen_icon_groups.sh "/path/to/sqpack" 068000
  ./regen_icon_groups.sh "/path/to/sqpack" 068000 069000 --no-commit
  ./regen_icon_groups.sh "/path/to/sqpack" --all-existing --message-prefix "Refresh icons"
EOF
}

if [[ $# -lt 1 ]]; then
  usage
  exit 1
fi

SQPACK_PATH="$1"
shift

PATCH_NAME="latest"
ALL_EXISTING=0
COMMIT_EACH=1
PUSH_EACH=1
PAUSE_BETWEEN=1
DRY_RUN=0
MESSAGE_PREFIX="Regenerate icons"
REQUESTED_GROUPS=()

while [[ $# -gt 0 ]]; do
  case "$1" in
    --patch)
      PATCH_NAME="$2"
      shift 2
      ;;
    --all-existing)
      ALL_EXISTING=1
      shift
      ;;
    --no-commit)
      COMMIT_EACH=0
      PUSH_EACH=0
      shift
      ;;
    --no-push)
      PUSH_EACH=0
      shift
      ;;
    --no-pause)
      PAUSE_BETWEEN=0
      shift
      ;;
    --message-prefix)
      MESSAGE_PREFIX="$2"
      shift 2
      ;;
    --dry-run)
      DRY_RUN=1
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    --*)
      echo "Unknown option: $1" >&2
      usage
      exit 1
      ;;
    *)
      REQUESTED_GROUPS+=("$1")
      shift
      ;;
  esac
done

normalize_group() {
  local raw="$1"
  local n

  if [[ "$raw" =~ ^[0-9]{6}$ ]] && [[ "${raw:3:3}" == "000" ]]; then
    printf "%s\n" "$raw"
    return
  fi

  if [[ "$raw" =~ ^[0-9]{1,3}$ ]]; then
    printf "%03d000\n" "$((10#$raw))"
    return
  fi

  if [[ "$raw" =~ ^[0-9]{4,6}$ ]]; then
    n=$((10#$raw))
    if (( n % 1000 == 0 )); then
      printf "%06d\n" "$n"
      return
    fi
  fi

  echo "Invalid icon group '$raw'. Expected a directory like 068000, or short form 68/068." >&2
  exit 1
}

if (( ALL_EXISTING == 1 )); then
  if (( ${#REQUESTED_GROUPS[@]} > 0 )); then
    echo "Pass either explicit groups or --all-existing, not both." >&2
    exit 1
  fi

  while IFS= read -r group_dir; do
    REQUESTED_GROUPS+=("$(basename "$group_dir")")
  done < <(find icons -maxdepth 1 -mindepth 1 -type d -name '[0-9][0-9][0-9]000' | sort)
fi

if (( ${#REQUESTED_GROUPS[@]} == 0 )); then
  echo "No icon groups requested." >&2
  usage
  exit 1
fi

NORMALIZED_GROUPS=()
for group in "${REQUESTED_GROUPS[@]}"; do
  NORMALIZED_GROUPS+=("$(normalize_group "$group")")
done

echo "sqpack: ${SQPACK_PATH}"
echo "patch: ${PATCH_NAME}"
echo "commit-each-group: ${COMMIT_EACH}"
echo "push-each-commit: ${PUSH_EACH}"
echo "groups: ${NORMALIZED_GROUPS[*]}"
echo

for index in "${!NORMALIZED_GROUPS[@]}"; do
  group="${NORMALIZED_GROUPS[$index]}"
  group_number=$((10#${group:0:3}))
  icon_start=$((group_number * 1000))
  icon_end=$((icon_start + 999))
  icon_path="icons/${group}"

  echo "=== ${group} (${icon_start}-${icon_end}) ==="

  if (( DRY_RUN == 1 )); then
    echo "dotnet run -- \"${SQPACK_PATH}\" \"${PATCH_NAME}\" --icons-only --icons-full --icon-range ${icon_start}-${icon_end}"
    if (( COMMIT_EACH == 1 )); then
      echo "git add -- ${icon_path}"
      echo "git commit -m \"${MESSAGE_PREFIX} ${group}\""
      if (( PUSH_EACH == 1 )); then
        echo "git push"
      fi
    fi
  else
    dotnet run -- "$SQPACK_PATH" "$PATCH_NAME" \
      --icons-only \
      --icons-full \
      --icon-range "${icon_start}-${icon_end}"

    if (( COMMIT_EACH == 1 )); then
      if git diff --quiet -- "$icon_path"; then
        echo "No changes in ${icon_path}; skipping commit."
      else
        git add -- "$icon_path"
        git commit -m "${MESSAGE_PREFIX} ${group}"
        if (( PUSH_EACH == 1 )); then
          git push
        fi
      fi
    fi
  fi

  if (( PAUSE_BETWEEN == 1 )) && (( index + 1 < ${#NORMALIZED_GROUPS[@]} )); then
    echo
    echo "Group ${group} finished."
    echo "Press Enter to continue, or type 'q' then Enter to stop."
    read -r answer
    if [[ "$answer" == "q" || "$answer" == "Q" ]]; then
      echo "Stopped after ${group}."
      exit 0
    fi
  fi

  echo
done

echo "All requested icon groups completed."
