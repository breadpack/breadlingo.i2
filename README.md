# BreadLingo I2 Localization

Public MIT Unity Editor tooling for I2 Localization. Install I2 separately under its own license. No vendor code, private service code, game content or Unity Localization Package dependency is included.

## Install

Unity Package Manager → Add package from Git URL:

```text
https://github.com/breadpack/breadlingo.i2.git#v0.2.0
```

The release also provides `com.breadpack.breadlingo.i2-0.2.0.tgz`. Unity 6000.3 is the tested Editor API baseline. All code is Editor-only.

## Bidirectional workflow

1. Create a dedicated BreadLingo project with the exact I2 source and target **locale codes**, such as `ko`, `en`, `ja`, `zh-TW` (not translated language names). This version supports the `main` branch.
2. Create a project-scoped **Unity connector token** in BreadLingo, with `unity:push`, `unity:pull`, `import:create`, `export:create`. Restrict its locales/branch and expiration as appropriate.
3. Open **Window → BreadLingo → I2 Localization**. Set the HTTPS service origin, workspace/project IDs or slugs and token. The token stays in memory; it is never saved in preferences/assets/logs. CI code can supply `BREADLINGO_CONNECTOR_TOKEN`. Browser account pairing is not included in this version.
4. Select a saved prefab with a legacy `I2.Loc.LanguageSource` component, scan, and select the source locale.
5. Choose **Send I2 → BreadLingo**. Normal and touch variants are distinct entries. Source text and description are uploaded; existing translations are registered as `needs_review` only when the remote target is absent. Existing web translations are preserved. Source edits invalidate existing target approvals. Unchanged sources need no repeat upload.
6. Translate and approve the results in BreadLingo's editor/review workflow.
7. Choose **Preview approved BreadLingo → I2**. Only approved translations matching the current source revision are returned. Review per-cell differences; conflicts remain blocked. Resolve a blocked local edit in I2 or revise the translation in BreadLingo, then scan/send/preview again.
8. Choose **Revalidate and apply selected translations**. The service approval digest and local fingerprint are checked again. Only selected normal/touch array cells change; flags, unrelated text, non-text terms and asset references are retained. A prefab backup is created under `Library/BreadLingoI2/Backups`. Saved data is reread and verified; a failed save verification restores the backup.

The initial send and successful applies establish a three-way merge baseline. A changed remote value is applied when the local value still matches that baseline. If both local and remote changed, application is blocked. A remote value unchanged since the baseline does not overwrite a local edit. Missing or unapproved translations are retained.

Sync uses bounded chunks (25 term variants, at most 256 KiB). Source revision checks and each chunk's receipt/mutations are transactional. The local pending request is journaled before sending; the same request is replayed after a lost acknowledgement. Baselines and journals live under `Library/BreadLingoI2/State`, contain private translation data, and must not be committed. Preserve this directory if you need to keep merge history across Library cleanup; a lost baseline requires reconnecting by an explicit send and carefully reviewing local changes. Whole-source sync is a sequence of atomic chunks, not one all-source transaction.

## Supported data and boundaries

- Saved prefab components with public `mLanguages`/`mTerms` fields and the known legacy I2 layout. Modern `LanguageSourceAsset` variants need an adapter and are not supported yet.
- Normal/touch **Text** terms, descriptions and exact locale codes. Non-text translations are excluded from service exchange and preserved locally.
- Empty/duplicate term keys, duplicate locale codes and mismatched arrays stop scanning. Repair ambiguous source data in the I2 tool first; this connector does not guess which duplicate wins.
- Term identity includes the asset GUID, component local file ID, exact key and variant. Different sources cannot collide. A source-locale change requires a separate matching project. Rename/deletion is explicit: absent terms are not automatically deleted from the service or prefab.
- Existing server targets are never overwritten by another send. This version imports existing translations into absent targets, rather than synchronizing locally edited translations back over web edits.
- Selected approvals are rechecked shortly before apply; revalidation taking over 30 seconds is rejected so you can apply a smaller selection. There is no server-side lock extending through the local save.
- Runtime CSV/ZIP/CDN publishing is **not** included. Games that load a separate CSV after the prefab must publish a complete runtime snapshot through their own build pipeline. Otherwise that loader can replace the newly applied prefab values. Native I2 `Import_CSV(Replace)` is never used by this connector.

Local JSON/CSV export remains available. JSON is a text snapshot, not a full asset backup. CSV is a translation exchange file, not an I2 runtime file; it contains normal text only and preserves raw formula-like prefixes. Treat exported data as confidential and review untrusted CSV before opening it in spreadsheet software.

See [validation evidence and limitations](Documentation~/validation.md). Report package issues without attaching credentials, private game text or licensed I2 files.

## 한국어

`Send I2 → BreadLingo`로 원문·기존 다국어 번역·context를 전송하고, 서비스에서 검토·승인한 뒤 `Preview approved BreadLingo → I2`에서 변경을 확인하여 선택 적용합니다. 로컬 수정 충돌이나 원문 버전 불일치는 차단합니다. 토큰은 메모리에만 보관하며 패키지 설치는 누구나 공개 Git URL로 할 수 있습니다. 별도 런타임 CSV/CDN 배포는 게임 빌드 파이프라인에서 처리해야 합니다.
