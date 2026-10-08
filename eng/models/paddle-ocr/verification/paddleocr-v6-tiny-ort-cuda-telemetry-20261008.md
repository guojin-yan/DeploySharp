# Representative ORT CUDA PaddleOCR GPU telemetry (2026-10-08)

This record wraps the v6 Tiny complete-pipeline ORT CUDA performance run. It is supporting telemetry, not a new benchmark protocol.

- Samples: `508` total; `188` at or above `10%` GPU utilization.
- Active P-state(s): `P0`.
- GPU utilization: mean `15.2%`, maximum `36%`.
- Graphics clock: min/mean/max `1425/1425/1425 MHz`.
- Power: active-sample mean `29.2 W`; maximum `42.8 W`.
- Temperature: active-sample maximum `51 °C`.
- Power-cap / thermal / hardware slowdown samples: `0` / `0` / `0`.

The graphics clock stayed at 1425 MHz in this capture and no slowdown flags were reported, but utilization was low (mean 15.2%, maximum 36%). The full OCR path is therefore not GPU-saturated on this workload; these numbers must not be presented as the RTX 3060 Laptop's maximum performance or as proof of a locked-frequency setup. Further optimization should profile CPU preprocessing, crop/recognition preparation, synchronization and stage scheduling separately.

The raw telemetry CSV is local-only and is not committed. The JSON retains the aggregate evidence and points to the exact local run directory.
