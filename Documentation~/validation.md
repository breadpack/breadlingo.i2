# 0.1.0 preview validation

Performed on 2026-10-09 on Windows with installed Unity 6000.3.9f1.

- All shipped Editor sources compiled using the installed Unity Roslyn compiler, real Unity Editor/Engine assemblies and .NET Standard 2.1 references.
- All shipped test sources compiled against Unity APIs and Unity's NUnit framework.
- **12 managed NUnit test methods passed** in a standalone Mono harness using the actual package code: copy isolation, unsupported structure, duplicate locales/keys, array mismatch, non-text exclusion, change fingerprints, CSV quotes/newlines/raw formula prefixes, unknown source locale, reserved column names, null/empty differences and deterministic ordering.
- The Unity-native `JsonUtility` round-trip test was **not executed**.
- Actual Unity batchmode EditMode tests and a private real-I2 fixture were attempted, but Package Manager stopped before package resolution/compilation with `The "path" argument must be of type string. Received undefined`. The same failure occurred when Unity generated a new project without this package.

Consequently, successful interactive UPM installation, Unity-hosted tests, actual I2 prefab scanning and Player build exclusion have **not yet been runtime-validated**. This preview is not a production-ready sync/apply release. Assembly configuration excludes all tool code from Player compilation; a real Player build remains a separate validation requirement.

To run the native suite on a working Unity installation, install Unity Test Framework, add this package to the project manifest's `testables`, and run EditMode tests filtered to `BreadLingo.I2.Editor.Tests`. I2 is not required for the synthetic suite.

No I2 vendor code, real game text, private project fixtures or compiled Unity assemblies are included in the public package.
