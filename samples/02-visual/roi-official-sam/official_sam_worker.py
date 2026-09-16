"""Official Meta predictors behind a bounded JSON-lines application adapter.

Input coordinates are source pixels. SAM3 uses the official SAM2-task recipe:
https://github.com/facebookresearch/sam3/blob/main/examples/sam3_for_sam2_video_task_example.ipynb
Only a short, explicitly bounded video is decoded. stdout is protocol-only.
"""
import argparse
import contextlib
import hashlib
import json
import pathlib
import sys
import time
import traceback


def digest(path):
    with open(path, "rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--family", choices=("sam2", "sam3"), required=True)
    parser.add_argument("--checkpoint", required=True)
    parser.add_argument("--video", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--revision", required=True)
    parser.add_argument("--frames", type=int, default=6)
    args = parser.parse_args()
    if not 3 <= args.frames <= 100:
        raise ValueError("Short video test requires 3..100 frames")
    wire = sys.stdout
    sys.stdout = sys.stderr
    import cv2
    import numpy as np
    import torch

    def send(value):
        wire.write(json.dumps(value, allow_nan=False) + "\n")
        wire.flush()

    output = pathlib.Path(args.output)
    output.mkdir(parents=True, exist_ok=True)
    frame_dir = output / "frames"
    # Keep the decoder input immutable and reject stale frame directories.
    frame_dir.mkdir(exist_ok=False)
    decoder = cv2.VideoCapture(args.video)
    try:
        if not decoder.isOpened():
            raise RuntimeError("OpenCV could not open the video")
        fps = decoder.get(cv2.CAP_PROP_FPS)
        if not np.isfinite(fps) or fps <= 0:
            raise ValueError("Video has no valid frame rate")
        for index in range(args.frames):
            ok, frame = decoder.read()
            if not ok:
                raise ValueError("Video contains fewer frames than requested")
            height, width = frame.shape[:2]
            if not cv2.imwrite(str(frame_dir / f"{index:05d}.jpg"), frame):
                raise RuntimeError("Cannot write predictor frame")
    finally:
        decoder.release()

    device = "cuda" if torch.cuda.is_available() else "cpu"
    # RTX 2060 (Turing) does not support BF16. Do not copy the upstream
    # Ampere-only notebook setting unconditionally.
    amp_dtype = torch.bfloat16 if device == "cuda" and torch.cuda.is_bf16_supported() else torch.float16
    autocast = lambda: torch.autocast("cuda", dtype=amp_dtype) if device == "cuda" else contextlib.nullcontext()
    model_start = time.perf_counter()
    if args.family == "sam2":
        from sam2.build_sam import build_sam2_video_predictor
        predictor = build_sam2_video_predictor("configs/sam2.1/sam2.1_hiera_t.yaml", args.checkpoint, device=device)
    else:
        from sam3.model_builder import build_sam3_video_model
        # Same official tracker/backbone wiring as the Meta video-task example.
        model = build_sam3_video_model(checkpoint_path=args.checkpoint, load_from_HF=False, device=device)
        predictor = model.tracker
        predictor.backbone = model.detector.backbone
        del model  # unused detector/decoder modules can be reclaimed
    predictor.eval()
    with torch.inference_mode(), autocast():
        state = predictor.init_state(video_path=str(frame_dir), offload_video_to_cpu=True, offload_state_to_cpu=True)
    if device == "cuda":
        torch.cuda.synchronize()
    send({"ready": True, "family": args.family, "width": width, "height": height, "fps": fps,
          "frames": args.frames, "torch": torch.__version__, "cuda": torch.version.cuda,
          "device": device, "gpu": torch.cuda.get_device_name() if device == "cuda" else None,
          "precision": str(amp_dtype) if device == "cuda" else "float32",
          "weight_dtype": str(next(predictor.parameters()).dtype),
          "checkpoint_sha256": digest(args.checkpoint), "video_sha256": digest(args.video),
          "revision": args.revision, "setup_ms": (time.perf_counter() - model_start) * 1000})
    last_frame = -1
    identities = {}
    for line in sys.stdin:
        try:
            request = json.loads(line)
            with torch.inference_mode(), autocast():
                if request["operation"] == "reset":
                    predictor.reset_state(state)
                    last_frame = -1
                    identities.clear()
                    send({"reset": True})
                    continue
                index = request["frame_index"]
                if not 0 <= index < args.frames or index <= last_frame:
                    raise ValueError("Frame index must be bounded and strictly ascending")
                initialize = request["mode"] == "Initialize"
                if initialize != (last_frame == -1):
                    raise ValueError("Initialize must be the first frame after reset")
                started = time.perf_counter()
                for item in request["items"]:
                    object_id = item["object_id"]
                    if object_id in identities and identities[object_id] != item["roi_id"]:
                        raise ValueError("Object id was rebound to another ROI")
                    identities[object_id] = item["roi_id"]
                    if request["mode"] == "Propagate":
                        continue
                    points = np.asarray(item["points"], dtype=np.float32).reshape(-1, 2)
                    labels = np.asarray(item["labels"], dtype=np.int32)
                    box = np.asarray(item["box"], dtype=np.float32)
                    kwargs = {}
                    if args.family == "sam3":
                        points = torch.as_tensor(points / [width, height], dtype=torch.float32)
                        box = torch.as_tensor(box / [width, height, width, height], dtype=torch.float32)
                        kwargs["rel_coordinates"] = True
                    predictor.add_new_points_or_box(state, frame_idx=index, obj_id=object_id,
                                                    points=points, labels=labels, box=box, **kwargs)
                kwargs = {"propagate_preflight": True, "tqdm_disable": True} if args.family == "sam3" else {}
                stream = predictor.propagate_in_video(state, start_frame_idx=index, max_frame_num_to_track=0, reverse=False, **kwargs)
                try:
                    value = next(stream)
                finally:
                    stream.close()
                actual_index, object_ids = value[:2]
                masks = value[3] if args.family == "sam3" else value[2]
                if actual_index != index:
                    raise ValueError("Predictor returned an unexpected frame")
                if device == "cuda":
                    torch.cuda.synchronize()
                inference_ms = (time.perf_counter() - started) * 1000
                export_start = time.perf_counter()
                objects = []
                for object_id, logits in zip(object_ids, masks):
                    mask = (logits.squeeze() > 0).cpu().numpy().astype(np.uint8)
                    if mask.shape != (height, width):
                        raise ValueError("Predictor mask is not in source-image coordinates")
                    ys, xs = np.nonzero(mask)
                    name = f"{index:05d}-{object_id}.png"
                    if not cv2.imwrite(str(output / name), mask * 255):
                        raise RuntimeError("Cannot save mask")
                    objects.append({"roi_id": identities[int(object_id)], "object_id": int(object_id),
                                    "pixels": int(mask.sum()), "center_x": float(xs.mean()) if len(xs) else 0,
                                    "center_y": float(ys.mean()) if len(ys) else 0, "mask_file": name,
                                    "mask_sha256": hashlib.sha256(mask.tobytes()).hexdigest()})
                last_frame = index
                send({"frame_index": index, "objects": objects, "predictor_ms": inference_ms,
                      "mask_export_ms": (time.perf_counter() - export_start) * 1000,
                      "gpu_allocated_bytes": torch.cuda.memory_allocated() if device == "cuda" else 0})
        except Exception as error:
            traceback.print_exc(file=sys.stderr)
            send({"error": f"{type(error).__name__}: {error}"})
            break  # mutated state is never reused after a failed operation


if __name__ == "__main__":
    main()
