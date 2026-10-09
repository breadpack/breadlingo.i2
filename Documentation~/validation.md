# 0.2.0 validation

Verified on Windows / Unity 6000.3.9f1 on 2026-10-09.

- All shipped Editor and test sources compiled against actual Unity assemblies.
- 21 managed NUnit tests passed: snapshots, normal/touch merging, three-way conflicts, stale source/locale/baseline checks and all-cell preflight before mutation.
- All 22 NUnit tests, including native JsonUtility serialization, passed inside Unity with and without I2 installed. Without I2, type detection returned absent and the Editor window opened safely.
- A private fixture with an actual separately licensed I2 LanguageSource component passed native prefab creation, scan, baseline/pending-request serialization, applying normal/touch cells, saved-prefab reread, preservation of unrelated text/flags/non-text data, and I2's actual translation lookup.
- The service API's authenticated HTTP/transactional D1 integration tests verify multilingual/variant import, approved-only pull, idempotent replay, replay mismatch, source invalidation, conflict rollback, project/locale/branch/scope boundaries, protected tokens and concurrent source changes.

Unity Package Manager startup fails on this machine even for a fresh project unrelated to this package (`The "path" argument must be of type string. Received undefined`). Native tests were therefore hosted with the documented `-noUpm` option, source installed into an isolated fixture's Editor directory, and the fixture's licensed I2 dependencies loaded privately. This validates Unity execution, not an interactive Git-URL UPM installation. A real Player build exclusion test has not been run; assembly configuration restricts all shipped code to Editor.

Production deployment was verified against the matching commit/assets (23 checks per environment). A real Unity client completed a synthetic production round trip: source push → approved-only pull → selective prefab apply → saved-data verification → actual I2 translation lookup. Five live checks also covered receipt replay, invalid credentials, payload mismatch and source invalidation. Approval status in this acceptance fixture was prepared directly in D1; the browser review/approval workflow was not exercised. The temporary QA connector token was revoked afterward.

The public repository's CI verifies package boundaries and the npm tarball. It does not execute licensed I2 or Unity. No vendor source, compiled Unity assemblies, game text or private test fixtures are published.

This release is a beta for bidirectional text sync. Runtime CSV/ZIP/CDN publishing, browser account pairing, modern LanguageSourceAsset layouts, automatic delete/rename and overwriting existing server translations are outside this release.
