"""Download/export official RF-DETR 1.11.2 tasks and retain reproducible local contracts.

Install rfdetr[onnx]==1.11.2 first. All weights and generated data stay in --output.
"""
from __future__ import annotations

import argparse
import hashlib
import importlib.metadata
import json
import os
import shutil
import urllib.request
from datetime import datetime, timezone
from pathlib import Path


def identity(path: Path) -> dict:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return {"path": path.name, "size": path.stat().st_size,
            "sha256": digest.hexdigest()}


def write_json(path: Path, value: dict) -> None:
    # ModelPack checksums must survive Git checkout on Windows as well as Unix.
    with path.open("w", encoding="utf-8", newline="\n") as stream:
        stream.write(json.dumps(value, indent=2) + "\n")


def download_weights(task: str, variant: str, directory: Path) -> Path | None:
    entries = json.loads(Path(__file__).with_name("weights.json").read_text(encoding="utf-8"))
    key = "keypoint-preview" if task == "keypoint" else "segment-" + variant
    if key not in entries:
        return None  # Other variants retain the upstream downloader and remain unverified here.
    return download_asset(entries[key], directory)


def download_asset(entry: dict, directory: Path) -> Path:
    directory.mkdir(parents=True, exist_ok=True)
    destination = directory / entry["file"]
    if not destination.exists():
        partial = destination.with_suffix(destination.suffix + ".partial")
        with urllib.request.urlopen(entry["url"]) as response, partial.open("wb") as stream:
            shutil.copyfileobj(response, stream)
        downloaded = identity(partial)
        if downloaded["size"] != entry["size"] or downloaded["sha256"] != entry["sha256"]:
            partial.unlink()
            raise RuntimeError("Download size/SHA-256 mismatch: " + entry["url"])
        partial.replace(destination)
    current = identity(destination)
    if current["size"] != entry["size"] or current["sha256"] != entry["sha256"]:
        raise RuntimeError("Cached file size/SHA-256 mismatch: " + str(destination))
    return destination


def default_image(directory: Path) -> Path:
    catalog = json.loads((Path(__file__).resolve().parents[2] / "test-assets/test-image-catalog.json").read_text(encoding="utf-8"))
    entry = next(asset for asset in catalog["assets"] if asset["id"] == "visual-bus")
    return download_asset({"file": entry["fileName"], "url": catalog["release"]["baseUrl"] + "/" + entry["fileName"],
                           "size": entry["sizeBytes"], "sha256": entry["sha256"]}, directory)


def write_package(output: Path, metadata: dict) -> None:
    """Create a local, integrity-checked ModelPack without claiming a published asset."""
    shutil.copyfile(Path(__file__).with_name("LICENSE.rf-detr.txt"), output / "LICENSE.rf-detr.txt")
    contract = output / "rfdetr.json"
    write_json(contract, metadata)
    model = output / metadata["model"]["path"]
    if identity(model) != metadata["model"]:
        raise RuntimeError("The local ONNX identity no longer matches rfdetr.json.")
    keypoint = metadata["task"] == "keypoint"
    model_id = metadata["modelId"]
    revision = metadata["upstreamCommit"]
    size = metadata["modelSize"]
    files = []
    for path, media_type, role in [(model, "application/onnx", "model"), (contract, "application/json", "configuration"),
                                    (output / "LICENSE.rf-detr.txt", "text/plain", "license")]:
        file = identity(path)
        files.append({"relativePath": file["path"], "sha256": file["sha256"], "size": file["size"], "mediaType": media_type, "role": role})
    manifest = {
        "schemaVersion": "2.0", "modelId": model_id,
        "name": "RF-DETR Keypoint Preview" if keypoint else "RF-DETR Seg " + metadata["variant"].title(),
        "family": "rf-detr", "task": "pose-estimation" if keypoint else "instance-segmentation", "modelVersion": "1.11.2",
        "exporter": {"name": "rfdetr.export", "version": "1.11.2-opset17", "sourceRevision": revision},
        "source": {"sourceUrl": "https://github.com/roboflow/rf-detr/tree/" + revision,
                   "projectUrl": "https://github.com/roboflow/rf-detr", "revision": revision, "author": "Roboflow",
                   "licenseExpression": "Apache-2.0", "licenseFile": "LICENSE.rf-detr.txt", "redistributionAllowed": True},
        "generatedAt": datetime.now(timezone.utc).isoformat(),
        "profileId": ("rfdetr.keypoint-preview." if keypoint else "portable.rfdetrseg.") + model_id,
        "inputs": [{"name": metadata["inputName"], "elementType": "float32", "shape": [1, 3, size, size]}],
        "outputs": [{"name": port["name"], "elementType": "float32", "shape": port["shape"]} for port in metadata["outputs"]],
        "artifacts": [{"artifactId": "onnx.fp32", "format": "onnx", "locationKind": "file", "entrypoint": model.name,
                       "compatibleBackends": ["onnxruntime", "openvino"], "files": files,
                       "precision": "fp32", "quantization": "none", "opset": 17, "portable": True,
                       "extensions": {"deploysharp.validation-status": "local-export-unverified",
                                      "deploysharp.preprocessing-version": "rfdetr-float-resize-imagenet-v1",
                                      "deploysharp.postprocessing-version": "rfdetr-keypoint-trace-v1" if keypoint else "rfdetr-mask-logit-bilinear-v1"}}],
        "extensions": {"deploysharp.publication-status": "unpublished-local", "deploysharp.downloadable": "false"}}
    write_json(output / "modelpack.json", manifest)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--task", choices=["keypoint", "segment"], required=True)
    parser.add_argument("--variant", choices=["nano", "small", "medium", "large", "xlarge", "2xlarge"], default="nano")
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--weights", type=Path)
    parser.add_argument("--image", type=Path, help="Defaults to the integrity-checked DeploySharp bus.jpg test asset.")
    parser.add_argument("--threshold", type=float, default=0.4)
    parser.add_argument("--package-only", action="store_true", help="Package an existing model.onnx/rfdetr.json without re-exporting or importing ML runtimes.")
    args = parser.parse_args()
    if not 0 <= args.threshold <= 1:
        parser.error("--threshold must be within [0,1].")
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    if args.package_only:
        write_package(output, json.loads((output / "rfdetr.json").read_text(encoding="utf-8")))
        return
    if args.image is None:
        args.image = default_image(output / "test-images")
    if importlib.metadata.version("rfdetr") != "1.11.2":
        raise RuntimeError("Use rfdetr==1.11.2 so the recorded processor/export contract is reproducible.")
    os.environ["RF_HOME"] = str(output / "weights")
    import numpy as np
    import onnx
    import onnxruntime as ort
    import rfdetr
    import torch
    from PIL import Image, ImageOps
    from torchvision.transforms import functional as F
    from rfdetr.assets.coco_classes import COCO_CLASSES
    from rfdetr.models.postprocess import PostProcess

    torch.set_num_threads(4)
    if args.task == "keypoint":
        model_class = rfdetr.RFDETRKeypointPreview
        model_id = "rf-detr/keypoint-preview"
    else:
        classes = {"nano": rfdetr.RFDETRSegNano, "small": rfdetr.RFDETRSegSmall,
                   "medium": rfdetr.RFDETRSegMedium, "large": rfdetr.RFDETRSegLarge,
                   "xlarge": rfdetr.RFDETRSegXLarge, "2xlarge": rfdetr.RFDETRSeg2XLarge}
        model_class = classes[args.variant]
        model_id = "rf-detr/segment/" + args.variant
    kwargs = {"device": "cpu"}
    weights_path = args.weights.resolve() if args.weights else download_weights(args.task, args.variant, output / "weights")
    if weights_path:
        kwargs["pretrain_weights"] = str(weights_path)
    model = model_class(**kwargs)
    with Image.open(args.image) as encoded:
        image = ImageOps.exif_transpose(encoded).convert("RGB")
    size = model.model.resolution
    tensor = F.normalize(F.resize(F.to_tensor(image), [size, size], antialias=False), model.means, model.stds).unsqueeze(0)
    module = model.model.model.eval()
    with torch.no_grad():
        raw = module(tensor)
        reference = PostProcess(num_select=model.model_config.num_select,
                                num_keypoints_per_class=model.model_config.num_keypoints_per_class)(
            raw, torch.tensor([[image.height, image.width]]))[0]
    # The public exporter prepares fixed position embeddings and preserves named raw outputs.
    exported = Path(model.export(output_dir=str(output), batch_size=1, dynamic_batch=False, opset_version=17,
                                 verbose=False, output_name="model.onnx"))
    graph = onnx.load(str(exported))
    onnx.checker.check_model(graph)
    if any(tensor.data_location == onnx.TensorProto.EXTERNAL for tensor in onnx.load(str(exported), load_external_data=False).graph.initializer):
        raise RuntimeError("This exporter requires a single-file ONNX; external data needs a separate complete ModelPack contract.")
    tensor.numpy().astype("<f4").tofile(output / "input.f32")
    session = ort.InferenceSession(str(exported), providers=["CPUExecutionProvider"])
    arrays = session.run(None, {"input": tensor.numpy()})
    ports = []
    for port, array in zip(session.get_outputs(), arrays):
        np.asarray(array, dtype="<f4").tofile(output / (port.name + ".f32"))
        ports.append({"name": port.name, "shape": list(array.shape), "file": port.name + ".f32"})
    labels = ["background", "person"] if args.task == "keypoint" else [COCO_CLASSES.get(i, "N/A") for i in range(arrays[1].shape[2])]
    labels = [name if labels.count(name) == 1 else f"{name}-{index}" for index, name in enumerate(labels)]
    metadata = {"modelId": model_id, "task": args.task, "variant": args.variant if args.task == "segment" else "preview",
                "upstreamVersion": "1.11.2", "upstreamCommit": "7433d5919d0f7e72cc6cb78663e0a731afd499e0",
                "model": identity(exported), "image": identity(args.image), "sourceSize": [image.width, image.height],
                "modelSize": size, "queryCount": int(arrays[0].shape[1]), "labels": labels, "opset": 17,
                "inputName": "input", "outputs": ports, "threshold": args.threshold,
                "runtime": {name: importlib.metadata.version(name) for name in ["torch", "onnx", "onnxruntime", "rfdetr"]}}
    keep = (reference["scores"] > args.threshold) & (reference["labels"] != 0) if args.task == "keypoint" else reference["scores"] > args.threshold
    result = {key: value[keep].tolist() for key, value in reference.items() if key != "masks"}
    if args.task == "segment":
        masks = reference["masks"][keep].cpu().numpy().astype(np.uint8)
        masks.tofile(output / "reference-masks.u8")
        result["maskShape"] = list(masks.shape)
    write_json(output / "reference.json", result)
    weights = list((output / "weights").glob("*"))
    if args.weights:
        weights.append(args.weights.resolve())
    metadata["weights"] = [identity(path) for path in sorted({path.resolve() for path in weights if path.is_file()})]
    write_package(output, metadata)
    print(json.dumps(metadata, indent=2))


if __name__ == "__main__":
    main()
