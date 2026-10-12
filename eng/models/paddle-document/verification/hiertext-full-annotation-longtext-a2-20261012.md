# HierText full annotation long-text coverage audit (2026-10-12)

This report audits the complete official validation annotation archive. It is a data-availability/provenance result only; it does not run OCR or redistribute images.

- Images: **1724**; annotated lines: **104139**; total annotated characters: **964340**
- Annotation gzip bytes: **12274736**; SHA-256: `ce7085d8bf24c2f13df852715c34494e6748cafcabb4bfc7a6e2bb320f58d23a`
- HierText revision: `70b6620b2b112597d8219e11eee9773a1403827c`; annotation license: CC-BY-SA-4.0
- Maximum natural line: **182 characters**
- Maximum page aggregate: **4073 characters** (not a continuous line)
- Natural lines >= 3,200 characters: **0**

## Boundary

The complete validation annotation archive contains no natural continuous line at or above 3,200 characters. This closes the question of whether the current local annotation cache merely omitted a long line, but it does not close A2: a legal, attributable long-line corpus or a different threshold is still required for real accuracy and seam-quality acceptance.
