# Changelog

## 0.3.0 — 2026-10-11 (beta)

- Browser account pairing, automatic workspace/project setup and source-identity reconnection.
- Target language selection without manual token or project-ID entry; additive service language setup.
- Windows current-user DPAPI protects credentials and pending requests outside the project; domain reload recovery and disconnect/revocation.
- Lightweight I2 language discovery: malformed term keys do not prevent onboarding; strict sync validation stays in place.
- Connection does not upload game text or start AI jobs. New local I2 language columns are still an explicit I2 operation.

## 0.2.0 — 2026-10-09 (Beta)

- Project-scoped HTTPS connector connection, in-memory token and CI environment token support.
- Bounded normal/touch source pushes, existing-target preservation, source revision fences and crash-safe chunk replay.
- Approved-only pull, three-way per-cell merge preview, local/remote revalidation and selective prefab apply with backups and saved-data verification.
- Append-only private local baseline journals, avoiding repeated full-game baseline rewrites.
- Native Unity/I2 prefab and runtime lookup validation; runtime CSV/CDN publication remains a separate project step.

## 0.1.0 — 2026-10-09 (Preview)

- Public UPM package with no dependency on the Unity Localization Package or private repositories.
- Editor-only detection of legacy I2 LanguageSource components, including installation guidance when I2 is absent.
- Read-only prefab inspection with language-code, key and array-shape validation.
- Local JSON text snapshots and normal-text translation CSV export.
- Explicit unsupported-feature boundaries: no service connection, upload, pull, apply or runtime export yet.
