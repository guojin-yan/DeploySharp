# PaddleOCR Release catalog transient-failure retry

Date: 2026-10-27. Scope: `PaddleOcrReleaseClient` catalog loading and concurrent waiter behavior. No model asset was downloaded or executed by this test slice.

## Problem

`GetCatalogAsync` shares one catalog task so concurrent callers do not issue duplicate requests. The same task was retained after an HTTP failure or request-timeout cancellation. Every later call on that client instance therefore observed the old terminal exception, even after the network recovered. Creating a new client worked, but normal retry on the existing client did not.

## Change

The shared catalog task is replaced only when it is absent, faulted or cancelled. A successfully completed task remains cached. Cancellation of one caller while it waits does not cancel or replace an in-flight shared request used by another caller.

Focused contracts cover both boundaries:

| Contract | Result |
| --- | --- |
| First catalog request returns HTTP 503; second call on the same client succeeds | passed |
| First underlying catalog request is cancelled; second call succeeds | passed |
| Third call reuses the successful catalog without another request | passed |
| One concurrent waiter cancels while another continues | passed |
| Shared request completes once and remains cached | passed |

Before the implementation change, the transient-failure test failed on its second call with the original `ModelFactoryException`; the concurrent-waiter test already passed. After the change, all `PaddleOcrReleaseClientTests` passed (`10/10`). The full `DeploySharp.ModelFactory.Tests` project passed `70`, skipped `3` explicitly optional live-release tests, and failed `0`. `DeploySharp.ModelFactory` built in Release for `netstandard2.0`, `net8.0`, `net9.0` and `net10.0` with zero warnings and errors.

## Boundary

This proves retry state management using deterministic HTTP handlers. It does not prove throughput or completion of a large GitHub Release asset transfer, and it does not change model/backend support status. Asset downloads still use their configured request timeout, temporary-file cleanup, size limit and SHA-256 validation. A corrupt or incomplete asset is never promoted into the verified cache.
