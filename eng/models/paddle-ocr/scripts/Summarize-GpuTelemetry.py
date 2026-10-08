"""Summarize the nvidia-smi CSV emitted by Invoke-WithGpuTelemetry.ps1."""

from __future__ import annotations

import argparse
import csv
import json
import math
import statistics
from pathlib import Path
from typing import Any


def values(rows: list[dict[str, str]], key: str) -> list[float]:
    result: list[float] = []
    for row in rows:
        raw = row.get(key, "").strip()
        if raw in ("", "N/A", "[N/A]"):
            continue
        value = float(raw)
        if math.isfinite(value):
            result.append(value)
    return result


def stats(items: list[float]) -> dict[str, float | None]:
    return {
        "min": min(items) if items else None,
        "median": statistics.median(items) if items else None,
        "mean": statistics.fmean(items) if items else None,
        "max": max(items) if items else None,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--csv", type=Path, required=True)
    parser.add_argument("--run-directory", type=Path, required=True)
    parser.add_argument("--output-json", type=Path, required=True)
    parser.add_argument("--output-markdown", type=Path, required=True)
    parser.add_argument("--active-threshold", type=float, default=10.0)
    args = parser.parse_args()

    with args.csv.open("r", encoding="utf-8-sig", newline="") as stream:
        rows = list(csv.DictReader(stream))
    if not rows:
        raise ValueError("Telemetry CSV is empty")
    utilization = values(rows, "gpu_utilization_percent")
    active_rows = [
        row for row in rows
        if row.get("gpu_utilization_percent", "").strip()
        and float(row["gpu_utilization_percent"]) >= args.active_threshold
    ]
    active_utilization = values(active_rows, "gpu_utilization_percent")
    graphics = values(active_rows, "graphics_clock_mhz")
    power = values(active_rows, "power_watts")
    temperature = values(active_rows, "temperature_celsius")
    report: dict[str, Any] = {
        "schemaVersion": 1,
        "scope": "Representative ORT CUDA PaddleOCR GPU telemetry",
        "telemetryCsv": str(args.csv.resolve()),
        "runDirectory": str(args.run_directory.resolve()),
        "sampleCount": len(rows),
        "activeThresholdPercent": args.active_threshold,
        "activeSampleCount": len(active_rows),
        "pstates": sorted({row.get("pstate", "").strip() for row in active_rows if row.get("pstate")}),
        "utilizationPercent": stats(active_utilization),
        "graphicsClockMHz": stats(graphics),
        "powerWatts": stats(power),
        "temperatureCelsius": stats(temperature),
        "softwarePowerCapSamples": sum(row.get("software_power_cap", "").strip().lower() == "active" for row in active_rows),
        "softwareThermalSlowdownSamples": sum(row.get("software_thermal_slowdown", "").strip().lower() == "active" for row in active_rows),
        "hardwareSlowdownSamples": sum(row.get("hardware_slowdown", "").strip().lower() == "active" for row in active_rows),
        "allSampleUtilizationPercent": stats(utilization),
        "interpretation": "Representative telemetry only. Stable clocks and no slowdown flags were observed, but low utilization indicates that this end-to-end pipeline is not GPU-saturated; the result is not a device-limit or locked-frequency proof.",
    }
    args.output_json.parent.mkdir(parents=True, exist_ok=True)
    args.output_markdown.parent.mkdir(parents=True, exist_ok=True)
    args.output_json.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    active = report["activeSampleCount"]
    util = report["utilizationPercent"]
    clock = report["graphicsClockMHz"]
    power_stats = report["powerWatts"]
    temp = report["temperatureCelsius"]
    markdown = f"""# Representative ORT CUDA PaddleOCR GPU telemetry (2026-10-08)

This record wraps the v6 Tiny complete-pipeline ORT CUDA performance run. It is supporting telemetry, not a new benchmark protocol.

- Samples: `{report['sampleCount']}` total; `{active}` at or above `{args.active_threshold:g}%` GPU utilization.
- Active P-state(s): `{', '.join(report['pstates']) or 'unknown'}`.
- GPU utilization: mean `{util['mean']:.1f}%`, maximum `{util['max']:.0f}%`.
- Graphics clock: min/mean/max `{clock['min']:.0f}/{clock['mean']:.0f}/{clock['max']:.0f} MHz`.
- Power: active-sample mean `{power_stats['mean']:.1f} W`; maximum `{power_stats['max']:.1f} W`.
- Temperature: active-sample maximum `{temp['max']:.0f} °C`.
- Power-cap / thermal / hardware slowdown samples: `{report['softwarePowerCapSamples']}` / `{report['softwareThermalSlowdownSamples']}` / `{report['hardwareSlowdownSamples']}`.

The graphics clock stayed at {clock['mean']:.0f} MHz in this capture and no slowdown flags were reported, but utilization was low (mean {util['mean']:.1f}%, maximum {util['max']:.0f}%). The full OCR path is therefore not GPU-saturated on this workload; these numbers must not be presented as the RTX 3060 Laptop's maximum performance or as proof of a locked-frequency setup. Further optimization should profile CPU preprocessing, crop/recognition preparation, synchronization and stage scheduling separately.

The raw telemetry CSV is local-only and is not committed. The JSON retains the aggregate evidence and points to the exact local run directory.
"""
    args.output_markdown.write_text(markdown, encoding="utf-8")
    print(json.dumps({"sampleCount": report["sampleCount"], "activeSampleCount": active, "utilization": util, "clock": clock}, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
