# Chart2Table 48-image tensorrt-cuda quality expansion (2026-10-13)

This report summarizes a deterministic 48-image selection from ChartQA `val` at revision `044eabfc306abfe9340c5741f0093aefc5973d06`. It validates the external manifest and every image/table hash before aggregating the checked-in inference report.

| Metric | Result |
|---|---:|
| Samples / EOS | 48 / 48 |
| Structurally valid | 47/48 |
| Matching dimensions | 33/48 |
| Exact table text | 17/48 |
| Row exact (micro) | 205/319 (64.26%) |
| Cell exact (micro) | 450/1051 (42.82%) |
| Total latency P50 / P95 | 7614.772 / 25819.988 ms |
| Vision latency P50 / P95 | 139.532 / 204.089 ms |
| Generated tokens P50 / P95 | 98.00 / 290.85 |

## Provenance and consistency

- Source report: `chart2table-extended-quality-tensorrt-48-20261013.json`; SHA-256 `7884172477ea92dceed9b5c23c30f8ee407487a6b0007055fe55b9a1bb1998da`.
- Selection identity SHA-256: `137dee01beac021e004d6ec2af0c00dc94cd4ef834558f2bbbe61863f9aaedcb`; manifest file SHA-256: `66b8c6d0698e3619cb49f77f4e971e51d5b25ebca46d6926d0c7aed98528fb83`.
- Selection groups: OECD=16, other=16, two_col=16. The identity hash excludes the non-deterministic manifest generation timestamp.
- Superset check: all 24 rows from `chart2table-extended-quality-tensorrt-20261012.json` are retained; artifact/output mismatches: 0.
- Structurally invalid outputs: `OECD_HOUSEHOLDS_WITH_BROADBAND_ACCESS_CAN_SVN_USA_000027.png` (4x8 expected, 7x4 generated).

## Boundary

- This is a bounded table-reconstruction selection, not ChartQA split accuracy or question-answering accuracy.
- Latency is an observation from the quality run. It is not a warm-up-controlled benchmark or a backend ranking.
- Exact/row/cell metrics are literal table-text diagnostics and do not provide semantic equivalence or chart-understanding guarantees.
- The external images, CSV files and manifest remain outside Git; the repository retains their hashes and the generated evidence only.
