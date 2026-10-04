# PaddleOCR full-page degradation summary

- Model/backend: `v6/medium` / `openvino`
- Source manifest SHA-256: `59db5f8b6a2d625cfad331c973e9d1bcd289cc096e65b793a60fd5db135be678`
- Records: `60` (`blur, jpeg, low-contrast, noise, normal, shadow`)
- Scope: controlled full-page degradation smoke; the polygons and text come from the original SROIE annotations, so this is not a natural low-quality split or a release accuracy claim.

| Condition | Images | Empty | IoU0.5 F1 | Matched CER | E2E CER | Total P50 ms | Total P95 ms |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| blur | 10 | 0 | 0.7942 | 0.5894 | 0.7173 | 1365.77 | 1835.78 |
| jpeg | 10 | 0 | 0.8970 | 0.3422 | 0.4205 | 1385.47 | 1836.70 |
| low-contrast | 10 | 0 | 0.9093 | 0.3238 | 0.3883 | 1396.08 | 2255.55 |
| noise | 10 | 0 | 0.8924 | 0.3600 | 0.4429 | 1556.20 | 1862.48 |
| normal | 10 | 0 | 0.9049 | 0.3204 | 0.3924 | 1420.46 | 2002.62 |
| shadow | 10 | 0 | 0.9103 | 0.3161 | 0.3761 | 1545.63 | 1944.69 |

The normal row is the page-level baseline for this generated selection. Severe rows are useful for comparing relative degradation and augmentation decisions, but should not be interpreted as a model benchmark outside this controlled protocol.
