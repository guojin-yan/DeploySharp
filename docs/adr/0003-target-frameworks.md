# ADR 0003: Target framework strategy

- Status: Accepted
- Date: 2026-08-04

## Decision

Core libraries explicitly target .NET Framework 4.6 through 4.8.1, .NET Core 3.1, and .NET 5 through .NET 10.

The common public surface uses APIs available on all targets. Modern targets may add optimized implementations behind centralized capability constants without changing common semantics. A target framework asset indicates tested compatibility, not continued security support from Microsoft.

## Consequences

- Pull requests build representative targets: net46, net481, netcoreapp3.1, net8.0, and net10.0.
- Nightly and release validation builds the complete framework matrix.
- Backend packages publish only the subset supported by their upstream dependency.
- Production documentation recommends only frameworks still supported by Microsoft at release time.
