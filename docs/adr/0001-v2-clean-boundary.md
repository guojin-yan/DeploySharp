# ADR 0001: V2 uses a clean architecture boundary

- Status: Accepted
- Date: 2026-08-04

## Decision

DeploySharp V2 is developed on the orphan branch `DeploySharpV2.0`. It does not inherit the V1 file tree or promise source, binary, configuration, result, or behavioral compatibility.

V1 may be consulted only to inventory model families, export signatures, and known issues. V2 does not ship compatibility packages, obsolete aliases, or dual API paths.

## Consequences

- Public APIs are designed from V2 requirements.
- Existing V1 applications must write a new integration layer.
- Models previously supported by V1 are independently implemented and verified in V2.
- The old branches and packages remain historical artifacts outside the V2 maintenance scope.
