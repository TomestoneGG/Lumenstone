#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'EOF'
Usage:
  ./regen_icon_chunks.sh "<sqpackPath>" [options]

Options:
  --patch <name>         Patch argument passed to dotnet run (default: latest)
  --start <id>           First icon id (default: 1)
  --end <id>             Last icon id (default: 250000)
  --chunk-size <n>       Icons per chunk (default: 10000)
  --from-chunk <n>       Starting zero-based chunk index (default: 0)
  --to-chunk <n>         Ending zero-based chunk index (default: auto)
  --no-pause             Do not pause between chunks
  -h, --help             Show this help

Examples:
  ./regen_icon_chunks.sh "~/Library/Application Support/FINAL FANTASY XIV ONLINE/Bottles/published_Final_Fantasy/drive_c/Program Files (x86)/SquareEnix/FINAL FANTASY XIV - A Realm Reborn/game/sqpack"
  ./regen_icon_chunks.sh "/path/to/sqpack" --chunk-size 5000 --from-chunk 3 --to-chunk 6
EOF
}

if [[ $# -lt 1 ]]; then
  usage
  exit 1
fi

SQPACK_PATH="$1"
shift

PATCH_NAME="latest"
ICON_START=1
ICON_END=250000
CHUNK_SIZE=10000
FROM_CHUNK=0
TO_CHUNK=""
PAUSE_BETWEEN=1

while [[ $# -gt 0 ]]; do
  case "$1" in
    --patch)
      PATCH_NAME="$2"
      shift 2
      ;;
    --start)
      ICON_START="$2"
      shift 2
      ;;
    --end)
      ICON_END="$2"
      shift 2
      ;;
    --chunk-size)
      CHUNK_SIZE="$2"
      shift 2
      ;;
    --from-chunk)
      FROM_CHUNK="$2"
      shift 2
      ;;
    --to-chunk)
      TO_CHUNK="$2"
      shift 2
      ;;
    --no-pause)
      PAUSE_BETWEEN=0
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown option: $1" >&2
      usage
      exit 1
      ;;
  esac
done

int_re='^[0-9]+$'
for n in "$ICON_START" "$ICON_END" "$CHUNK_SIZE" "$FROM_CHUNK"; do
  if ! [[ "$n" =~ $int_re ]]; then
    echo "Expected integer value, got '$n'." >&2
    exit 1
  fi
done

if [[ -n "$TO_CHUNK" ]] && ! [[ "$TO_CHUNK" =~ $int_re ]]; then
  echo "Expected integer value for --to-chunk, got '$TO_CHUNK'." >&2
  exit 1
fi

if (( CHUNK_SIZE <= 0 )); then
  echo "--chunk-size must be > 0." >&2
  exit 1
fi

if (( ICON_START < 1 || ICON_END < ICON_START )); then
  echo "Invalid icon range: ${ICON_START}-${ICON_END}." >&2
  exit 1
fi

TOTAL_ICONS=$((ICON_END - ICON_START + 1))
LAST_CHUNK=$(((TOTAL_ICONS - 1) / CHUNK_SIZE))

if [[ -z "$TO_CHUNK" ]]; then
  TO_CHUNK="$LAST_CHUNK"
fi

if (( FROM_CHUNK > TO_CHUNK )); then
  echo "--from-chunk cannot be greater than --to-chunk." >&2
  exit 1
fi

if (( FROM_CHUNK > LAST_CHUNK )); then
  echo "--from-chunk (${FROM_CHUNK}) is outside the available range 0-${LAST_CHUNK}." >&2
  exit 1
fi

if (( TO_CHUNK > LAST_CHUNK )); then
  echo "--to-chunk (${TO_CHUNK}) exceeds last chunk ${LAST_CHUNK}; clamping." >&2
  TO_CHUNK="$LAST_CHUNK"
fi

echo "sqpack: ${SQPACK_PATH}"
echo "patch: ${PATCH_NAME}"
echo "icons: ${ICON_START}-${ICON_END}"
echo "chunk-size: ${CHUNK_SIZE}"
echo "chunk-index range: ${FROM_CHUNK}-${TO_CHUNK}"
echo

for ((chunk=FROM_CHUNK; chunk<=TO_CHUNK; chunk++)); do
  chunk_start=$((ICON_START + (CHUNK_SIZE * chunk)))
  chunk_end=$((chunk_start + CHUNK_SIZE - 1))
  if (( chunk_end > ICON_END )); then
    chunk_end=$ICON_END
  fi

  echo "=== Chunk ${chunk} (${chunk_start}-${chunk_end}) ==="

  dotnet run -- "$SQPACK_PATH" "$PATCH_NAME" \
    --icon-start "$ICON_START" \
    --icon-end "$ICON_END" \
    --icon-chunk-size "$CHUNK_SIZE" \
    --icon-chunk-index "$chunk"

  if (( PAUSE_BETWEEN == 1 )) && (( chunk < TO_CHUNK )); then
    echo
    echo "Chunk ${chunk} finished."
    echo "Commit this batch now, then press Enter to continue."
    echo "Type 'q' then Enter to stop."
    read -r answer
    if [[ "$answer" == "q" || "$answer" == "Q" ]]; then
      echo "Stopped at chunk ${chunk}."
      exit 0
    fi
  fi
done

echo
echo "All requested chunks completed."
