#!/usr/bin/env bash
set -euo pipefail

if (( $# < 7 || $# > 9 )); then
  printf 'Usage: %s RUNNER BUNDLE TOKENIZER DATA REPORT_DIR BACKEND SOURCE_COMMIT [START_OFFSET] [SHARD_SIZE]\n' "$0" >&2
  exit 2
fi

runner=$(realpath "$1")
bundle=$(realpath "$2")
tokenizer=$(realpath "$3")
data=$(realpath "$4")
report_dir=$(realpath -m "$5")
backend=$6
source_commit=$7
start_offset=${8:-0}
shard_size=${9:-5}

if [[ $backend != onnxruntime-cpu && $backend != openvino-cpu ]]; then
  printf 'Unsupported backend: %s\n' "$backend" >&2
  exit 2
fi
if [[ ! $source_commit =~ ^[0-9a-f]{40}$ ]]; then
  printf 'SOURCE_COMMIT must be a complete lowercase Git commit SHA.\n' >&2
  exit 2
fi
if ! [[ $start_offset =~ ^[0-9]+$ && $shard_size =~ ^[0-9]+$ ]] ||
   (( shard_size < 1 || shard_size > 5 || start_offset >= 121 || start_offset % shard_size != 0 )); then
  printf 'START_OFFSET must be a shard boundary below 121; SHARD_SIZE must be 1..5.\n' >&2
  exit 2
fi
if [[ -z ${DOTNET_ROOT:-} || ! -x $DOTNET_ROOT/dotnet ]]; then
  printf 'DOTNET_ROOT must point to an installed .NET runtime.\n' >&2
  exit 2
fi
for required in "$runner/DeploySharp.Visual.OpenCV.Tests.dll" "$bundle/vision.onnx" \
  "$tokenizer/tokenizer.json" "$data/realFormula-manifest.jsonl"; do
  if [[ ! -f $required ]]; then
    printf 'Required input is missing: %s\n' "$required" >&2
    exit 2
  fi
done

mkdir -p "$report_dir"
export LD_LIBRARY_PATH="$runner${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
export MALLOC_ARENA_MAX=2 DOTNET_GCHeapHardLimit=0x40000000
export DEPLOYSHARP_PADDLEOCR_VL_REAL_FORMULA_RUN=1
export DEPLOYSHARP_PADDLEOCR_VL_REAL_FORMULA_BACKEND="$backend"
export DEPLOYSHARP_PADDLEOCR_VL_REAL_FORMULA_LIMIT="$shard_size"
export DEPLOYSHARP_PADDLEOCR_VL_REAL_FORMULA_RECORD_TOKEN_IDS=1
export DEPLOYSHARP_PADDLE_REAL_FORMULA_ROOT="$data"
export DEPLOYSHARP_PADDLEOCR_VL_BUNDLE_ROOT="$bundle"
export DEPLOYSHARP_PADDLEOCR_VL_MODEL_ROOT="$tokenizer"
export DEPLOYSHARP_PADDLEOCR_VL_SOURCE_COMMIT="$source_commit"
unset DEPLOYSHARP_PADDLEOCR_VL_REAL_FORMULA_IMAGES

for (( offset = start_offset; offset < 121; offset += shard_size )); do
  report="$report_dir/${backend}-offset-${offset}.json"
  if [[ -e $report ]]; then
    printf 'Refusing to overwrite an existing shard: %s\n' "$report" >&2
    exit 2
  fi
  export DEPLOYSHARP_PADDLEOCR_VL_REAL_FORMULA_OFFSET="$offset"
  export DEPLOYSHARP_PADDLEOCR_VL_REAL_FORMULA_REPORT_PATH="$report"
  printf 'Running %s realFormula shard offset=%d limit=%d\n' "$backend" "$offset" "$shard_size"
  timeout 900s "$DOTNET_ROOT/dotnet" vstest "$runner/DeploySharp.Visual.OpenCV.Tests.dll" \
    '--TestCaseFilter:FullyQualifiedName~EvaluateAnnotatedRealFormulaCorpus' \
    '--Logger:console;verbosity=minimal'
done
