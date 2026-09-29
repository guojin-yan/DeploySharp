# PP-OCRv5 Mobile OpenVINO session-pool comparison (2026-09-29)

This record compares one and two independently-created OpenVINO CPU stage Sessions under the same formal OCR protocol.

## Configuration

- Device: JYPPX, Windows 10 build `10.0.26200`, x64, 16 logical processors.
- Model: PP-OCRv5 Mobile, local `E:\Model\paddleocr` assets.
- Input: `E:\Data\ocr\demo_1.jpg`, SHA-256 `ec81d595407ccb61eb2d4d90e74d976469febb41a74cdbc8dbb8429b1e768f5c`.
- Backend: OpenVINO CPU.
- Protocol: 5 warm-ups, 50 measured calls, recognition batch 4, prepared input reused, no autotune or delay.
- Both runs returned 16 regions and the same recognized-text SHA and result-contract SHA `e3c6b3a715915d9f6069c859a42e0ac2350f44c605bed89a2606e643c7383c99`.

## Results

| Stage Sessions | Total P50 (ms) | Total P95 (ms) | Recognition (ms) | Recognition inference work (ms) | Result contract SHA |
|---:|---:|---:|---:|---:|---|
| 1 | 313.503 | 398.880 | 237.511 | 217.176 | `e3c6b3a715915d9f6069c859a42e0ac2350f44c605bed89a2606e643c7383c99` |
| 2 | 371.550 | 527.348 | 314.759 | 589.760 | `e3c6b3a715915d9f6069c859a42e0ac2350f44c605bed89a2606e643c7383c99` |

On this CPU OpenVINO setup, two Sessions increased total P50 by `18.48%` and P95 by `32.21%`. The contract stayed identical. The extra Session competes for CPU threads and memory, so pool size must be tuned per backend/device; a larger pool is not a universal optimization.

These are steady-state observations on one device and one image. They do not establish long-run stability, GPU scaling or cross-device throughput.
