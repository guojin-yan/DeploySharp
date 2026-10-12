# FUNSD parent-linked degradation crop evidence (2026-10-12)

This is a controlled severe-degradation smoke over source-linked FUNSD word crops. It is not line/page accuracy, natural robustness, or an approval to enable enhancement by default.

- Parent pages: **40**; source crops: **40**; records: **240**
- Conditions: `blur, jpeg, low-contrast, noise, normal, shadow`; severity: `severe`
- Source manifest SHA-256: `5d58429c21d4df671654b8016a58d47d758ff69b7e3378c6e42b571cb360cab3`
- Generated manifest SHA-256: `0183f280b393703b0034f9c7313425219d23f21f735043a732d4d08b0790aaa9`

| Backend | Condition | Records | Empty | Exact (case-folded) | Mean CER | Mean WER |
|---|---|---:|---:|---:|---:|---:|
| onnxruntime | blur | 40 | 19 | 2 | 0.9261 | 0.9667 |
| onnxruntime | jpeg | 40 | 1 | 26 | 0.1950 | 0.3667 |
| onnxruntime | low-contrast | 40 | 2 | 29 | 0.1724 | 0.2917 |
| onnxruntime | noise | 40 | 0 | 26 | 0.1977 | 0.3667 |
| onnxruntime | normal | 40 | 2 | 29 | 0.1724 | 0.2917 |
| onnxruntime | shadow | 40 | 5 | 30 | 0.1722 | 0.2750 |
| openvino | blur | 40 | 19 | 2 | 0.9261 | 0.9667 |
| openvino | jpeg | 40 | 1 | 26 | 0.1950 | 0.3667 |
| openvino | low-contrast | 40 | 2 | 29 | 0.1724 | 0.2917 |
| openvino | noise | 40 | 0 | 26 | 0.1977 | 0.3667 |
| openvino | normal | 40 | 2 | 29 | 0.1724 | 0.2917 |
| openvino | shadow | 40 | 5 | 30 | 0.1722 | 0.2750 |

## Interpretation

Blur is the dominant failure mode in this generated FUNSD sample; the other conditions mostly preserve the same word-crop error profile. The two CPU backends are compared on identical generated inputs and this run does not authorize a production enhancement policy.

A larger, quality-controlled line/page set and explicit false-correction/cost gates are still required for B1b/B2/B3.
