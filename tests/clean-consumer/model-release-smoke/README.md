# Published detector release smoke consumer

This clean consumer materializes published preview bundles through `ModelFactoryClient`, verifies the cache, and runs real CPU inference with a deterministic image fixture.

Set `DEPLOYSHARP_DETECTOR_RELEASE_SMOKE=1` to run all representative packages:

- YOLOv8 detection, classification, instance segmentation, pose, and OBB through ONNX Runtime.
- RT-DETR decoded vector-count ONNX and raw-query ONNX through ONNX Runtime.
- RT-DETR decoded vector-count OpenVINO IR through OpenVINO CPU.

Set `DEPLOYSHARP_DETECTOR_RELEASE_SMOKE_SCOPE=detr` to run only the three RT-DETR release cases while investigating a new DETR release. `DEPLOYSHARP_MODEL_CACHE` can select an application-owned cache location; when omitted, the consumer creates and removes its own temporary cache.
