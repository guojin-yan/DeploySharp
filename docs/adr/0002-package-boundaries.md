# ADR 0002: Core and backend package boundaries

- Status: Accepted
- Date: 2026-08-04

## Decision

`JYPPX.DeploySharp.Core` has no runtime NuGet dependencies. It owns stable inference contracts, tensor abstractions, model metadata, canonical result DTOs, diagnostics, errors, and explicit registration.

Domain packages depend on Core. Backend packages implement Core capabilities and may depend only on the library they adapt. Platform-native runtimes and vendor components are selected by the consuming application and are not bundled into generic DeploySharp packages.

## Consequences

- Installing one backend does not install unrelated backends.
- Backend-native types never appear in Core public APIs.
- Logging framework integrations are optional packages rather than Core dependencies.
- Runtime availability is validated when a backend session is created.
