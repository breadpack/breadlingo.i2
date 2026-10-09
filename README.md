# BreadLingo I2 Localization

Public, MIT-licensed Unity Editor tooling for projects using [I2 Localization](https://www.inter-illusion.com/tools/i2-localization).

**0.1.0 is a read-only preview.** It detects legacy I2 `LanguageSource` prefab components, inspects language/text data, and exports a local JSON text snapshot or translation-exchange CSV. It does **not** connect to the BreadLingo service, upload data, pull approved translations, modify I2 assets, or generate game runtime CSV/ZIP files. These integrations are planned for later releases.

## Install

Unity Package Manager → **Add package from Git URL**:

```text
https://github.com/breadpack/breadlingo.i2.git#v0.1.0
```

No GitHub credentials or BreadLingo account are required to install or use local tools. Install I2 separately under its own license. This package has no Unity Localization Package dependency and does not include vendor code or game content.

The release also provides `com.breadpack.breadlingo.i2-0.1.0.tgz` for **Add package from tarball**. Commit the project's package manifest and lock file to share the pinned version with your team.

## Use

1. Open **Window → BreadLingo → I2 Localization**.
2. Select a saved prefab containing `I2.Loc.LanguageSource` in the Project window, then choose **Use selected prefab** or assign it directly.
3. Choose **Scan source** and select the source language by code.
4. Export JSON or CSV to an explicit local destination.

The tool never calls I2 import, localization-manager or lifecycle methods. It reads saved objects and copies text data through a small reflection boundary. Unsupported structures, duplicate keys/codes and mismatched arrays stop the scan. It can be installed without I2; the window then shows installation guidance.

JSON includes normal/touch text arrays, descriptions and flags, with non-text term counts. It excludes non-text localization and asset references and is **not a complete I2 backup or apply artifact**. CSV contains normal text only; touch data stays in JSON. CSV header:

```text
key,source_locale,source_text,description,i2_source_id,<locale codes...>
```

You can use BreadLingo's general CSV import and explicitly map columns. There is no automatic I2 profile or supported return/apply path in this version. Neither export is a native I2 runtime CSV. Never replace your runtime language file with it.

Exports contain your actual translation data; keep them private unless you intend to share them. CSV preserves raw text, including formula-like prefixes. Review untrusted text before opening CSV in spreadsheet software; it does not add spreadsheet-protection apostrophes that would change game strings.

## Compatibility

Initial target: Windows, Unity **6000.3.9f1**, legacy component-based I2 sources exposing public `mTerms`, `mLanguages`, `Languages`, `Languages_Touch` and `Flags` fields. Newer `LanguageSourceAsset`/`LanguageSourceData` structures and scene-local sources are not supported yet. See the [validation notes](Documentation~/validation.md) for evidence and the current Unity runtime validation limitation; compatibility with an arbitrary I2 version is not implied by successful type detection.

All tool code is Editor-only. No runtime component, account token, network sync or game data is bundled.

## Development and roadmap

Enable package tests by adding `com.breadpack.breadlingo.i2` to the project manifest's `testables` list, with Unity Test Framework installed. Tests use synthetic data only.

**Bidirectional service synchronization is the required scope of the next feature release.** The local-export-only 0.1.0 preview does not satisfy that integration requirement.

| Direction | Required behavior (not implemented in 0.1.0) |
| --- | --- |
| I2 → BreadLingo | Send source changes, existing multilingual translations and context, preserving key identity and normal/touch variants. Confirm the results in the service editor. Existing translations are not automatically approved. |
| BreadLingo → I2 | Fetch approved translations for the current source revision, map language codes and entry identities, preview differences/conflicts, then apply and verify the saved I2 values. Preserve entries absent from the response. |

Acceptance requires a real round trip: change an I2 source → send to BreadLingo → translate/review → fetch approved results → resolve local conflicts → save and reread I2 → verify game display with matching runtime data. Repeating a push/pull must not create duplicate entries or unrelated changes. A file download alone does not count as a successful import/apply.

Project-scoped account connection, incremental jobs, three-way conflict resolution, fixed translation releases and project-specific runtime export codecs support this required workflow. Online features will require authorized BreadLingo project access. Local installation remains publicly accessible. Account connection, sync and apply remain unavailable in the current preview.

Report reproducible package issues on this repository. Do not attach account tokens, private game text, licensed I2 source or full production snapshots to public issues.

## 한국어 안내

현재 버전은 **로컬 조회·내보내기만 가능한 preview**입니다. 설치는 누구나 무료로 할 수 있으며 계정 로그인이 필요 없습니다. I2는 별도로 설치해야 합니다. BreadLingo 온라인 동기화·승인 번역 가져오기·게임 반영 기능은 아직 제공하지 않습니다. CSV는 번역 교환용이며 게임의 I2 언어 파일로 사용할 수 없습니다.
