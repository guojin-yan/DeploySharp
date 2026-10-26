#!/usr/bin/env bash
set -euo pipefail

if (( $# < 5 || $# > 7 )); then
  printf 'Usage: %s PYTHON REFERENCE MODEL_ROOT MANIFEST REPORT_DIR [START_OFFSET] [SHARD_SIZE]\n' "$0" >&2
  exit 2
fi

python=$1
reference=$(realpath "$2")
model_root=$(realpath "$3")
manifest=$(realpath "$4")
report_dir=$(realpath -m "$5")
start_offset=${6:-0}
shard_size=${7:-5}

if ! [[ $start_offset =~ ^[0-9]+$ && $shard_size =~ ^[0-9]+$ ]] ||
   (( shard_size < 1 || shard_size > 5 || start_offset % shard_size != 0 )); then
  printf 'START_OFFSET must be a shard boundary; SHARD_SIZE must be 1..5.\n' >&2
  exit 2
fi
for required in "$python" "$reference" "$manifest" "$model_root/model.safetensors"; do
  if [[ ! -f $required ]]; then
    printf 'Required input is missing: %s\n' "$required" >&2
    exit 2
  fi
done
if [[ ! -x $python ]]; then
  printf 'Python executable is not executable: %s\n' "$python" >&2
  exit 2
fi

mapfile -t ids < <("$python" -c '
import json, sys
with open(sys.argv[1], encoding="utf-8") as stream:
    manifest = json.load(stream)
cases = manifest["cases"]
if len(cases) != 121 or len({case["id"] for case in cases}) != 121:
    raise SystemExit("Expected 121 unique realFormula case IDs")
for case in cases:
    print(case["id"])
' "$manifest")
if (( ${#ids[@]} != 121 || start_offset >= ${#ids[@]} )); then
  printf 'Manifest has no complete 121-image selection at the requested offset.\n' >&2
  exit 2
fi

mkdir -p "$report_dir"
printf 'Official realFormula count=%d shard size=%d\n' "${#ids[@]}" "$shard_size"
for (( offset = start_offset; offset < ${#ids[@]}; offset += shard_size )); do
  report="$report_dir/official-offset-${offset}.json"
  if [[ -e $report ]]; then
    printf 'Refusing to overwrite an existing shard: %s\n' "$report" >&2
    exit 2
  fi
  args=(--model-root "$model_root" --manifest "$manifest" --output "$report"
        --device cpu --dtype float32 --processor-backend pil --attention sdpa
        --threads 4 --maximum-new-tokens 512 --use-cache)
  for (( index = offset; index < offset + shard_size && index < ${#ids[@]}; index++ )); do
    args+=(--case "${ids[$index]}")
  done
  printf 'Running official realFormula shard offset=%d count=%d\n' \
    "$offset" "$(( index - offset ))"
  timeout 900s "$python" "$reference" "${args[@]}"
done
