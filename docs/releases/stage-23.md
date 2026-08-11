# Stage 23: Open-vocabulary detection and Grounded-SAM / 阶段 23：开放词汇检测与 Grounded-SAM

- Added artifact-bound Grounding DINO, YOLO-World, YOLOE, vocabulary/tokenizer/embedding identity and blocker contracts in existing Visual packages. / 在既有包新增开放词汇完整 Identity 与 blocker 合同。
- Fixed `person,bus` YOLO-Worldv2 runs on ORT/OpenVINO CPU and matches official ONNX predictor fields. Grounded-SAM reuses canonical boxes/masks and matches five official SAM source masks plus reset determinism. / 固定 YOLO-Worldv2 双后端与官方字段对齐；Grounded-SAM 复用规范结果并完成五个官方 Mask 与 Reset 验证。
- ModelFactory adds tokenizer/vocabulary-mode bundle queries. Five External manifests and a package-only consumer were added. / ModelFactory 新增查询；新增五份 External 清单与仅包 Consumer。
- Grounding DINO, identity-less MMYOLO and YOLOE remain blockers. Catalog, Release, Actions and user assets were not changed. TensorRT remains unimplemented. / 不完整路径保持阻断；目录、Release、Actions 与用户资产未写入；TensorRT 仍未实现。
