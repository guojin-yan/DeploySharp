#!/usr/bin/env bash
set -euo pipefail

model_root="${DEPLOYSHARP_PADDLEOCR_ROOT:-/home/ygj/models/paddleocr}"
output_root="${DEPLOYSHARP_PADDLEOCR_TENSORRT_ROOT:-${model_root}-trt11-cuda12.9-sm75}"
trtexec_bin="${TRTEXEC:-${JYPPX_TENSORRT_ROOT:-/usr}/bin/trtexec}"
workspace_mib="${DEPLOYSHARP_TENSORRT_WORKSPACE_MIB:-2048}"

if [[ ! -x "$trtexec_bin" ]]; then
  echo "trtexec not found or not executable: $trtexec_bin" >&2
  exit 2
fi

mkdir -p "$output_root"

# Keep ONNX files and dictionaries beside their engines. The benchmark uses the
# ONNX path to identify the PP-OCR version/stage and resolve its sidecar engine,
# while OCR decoding still reads the model-directory text assets.
for pattern in '*.onnx' '*.txt'; do
  while IFS= read -r -d '' asset; do
    relative="${asset#"$model_root"/}"
    mkdir -p "$output_root/$(dirname "$relative")"
    cp -f "$asset" "$output_root/$relative"
  done < <(find "$model_root" -type f -name "$pattern" -print0)
done

resolve_shapes() {
  local path="$1"
  local lower="${path,,}"
  if [[ "$lower" == *"det"* ]]; then
    printf '%s\n' '1x3x736x736' '1x3x736x736' '1x3x736x736'
  elif [[ "$lower" == *"cls"* ]]; then
    if [[ "$lower" == *"pp-ocrv4"* ]]; then
      printf '%s\n' '1x3x48x192' '4x3x48x192' '8x3x48x192'
    else
      printf '%s\n' '1x3x80x160' '4x3x80x160' '8x3x80x160'
    fi
  elif [[ "$lower" == *"rec"* ]]; then
    # Keep the recognizer contract dynamic. Crops are grouped by natural
    # width in the OCR pipeline, so padding every batch to 320 would waste
    # work and would also reject long/narrow text when a static engine is used.
    printf '%s\n' '1x3x48x48' '4x3x48x160' '8x3x48x320'
  else
    return 1
  fi
}

mapfile -d '' models < <(find "$model_root" -type f -name '*.onnx' -print0 | sort -z)
if (( ${#models[@]} == 0 )); then
  echo "No ONNX models found below $model_root" >&2
  exit 3
fi

for model in "${models[@]}"; do
  mapfile -t shapes < <(resolve_shapes "$model")
  if (( ${#shapes[@]} != 3 )); then
    echo "Skip model with unknown PaddleOCR stage: $model" >&2
    continue
  fi
  min_shape="${shapes[0]}"
  opt_shape="${shapes[1]}"
  max_shape="${shapes[2]}"
  relative="${model#"$model_root"/}"
  engine="$output_root/${relative}.engine"
  mkdir -p "$(dirname "$engine")"
  if [[ -s "$engine" && "$engine" -nt "$model" ]]; then
    echo "Reuse $engine"
    continue
  fi

  # PaddleOCR exports used by the demo declare the image input as x.
  input_name="x"
  echo "Build $relative (min=$input_name:$min_shape opt=$input_name:$opt_shape max=$input_name:$max_shape)"
  "$trtexec_bin" \
    --onnx="$model" \
    --saveEngine="$engine" \
    --minShapes="$input_name:$min_shape" \
    --optShapes="$input_name:$opt_shape" \
    --maxShapes="$input_name:$max_shape" \
    --builderOptimizationLevel=3 \
    --memPoolSize="workspace:${workspace_mib}" \
    --skipInference
  if [[ ! -s "$engine" ]]; then
    echo "TensorRT produced an empty engine for $relative" >&2
    exit 4
  fi
done

echo "TensorRT engines written to $output_root"
