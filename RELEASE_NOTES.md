# Visual JSON 1.3.0 Release Notes

This is an **internal-quality release**: it contains no new user-facing features and no changes to the UI, shortcuts, save formats, or settings. The motto of this cycle: change the structure, never the behavior.

## What changed in 1.3.0

- **Maintainable UI code.** The single 3,564-line main-window file was split into eleven feature-scoped files, and application state moved into view models (document state, message panes, status bar). The File menu and toolbar now run on WPF commands. Everything looks and behaves exactly as in 1.2.0.
- **Standard test tooling.** All tests migrated from a custom console runner to MSTest with identical inputs and expectations; `dotnet test` (with TRX result files) is now the single entry point. The suite grew from 82 to 94 tests.
- **Reproducible builds.** GitHub Actions now restores, builds, tests, packages, and verifies the release zip on every push and pull request, uploading the zip, checksum, and test results as artifacts. Because the binaries are unsigned, this reproducibility plus the published SHA256 is the trust basis for the download.
- **Verified packaging.** A new verification script checks every release zip: checksum matches, all public documents present, README links resolve inside the zip, the application executable exists, and internal specification documents are never bundled.
- **Tighter network guard.** The external-schema URL guard (still **off by default**) additionally blocks CGNAT (`100.64.0.0/10`), IPv4 multicast/reserved ranges, IPv6 unspecified/multicast, and 6to4/Teredo addresses whose embedded IPv4 falls in a blocked range.
- **Exception-handling audit.** All 45 `Catch` blocks were classified; none silently swallow data-affecting errors. Intentional ignores carry reason comments, and two schema-probe failures that previously vanished are now recorded in the file log (types and stack traces only — never document content).
- **Honest documentation.** The README comparison table now footnotes that schema validation covers a practical keyword subset, matching the Limitations section, in both English and Japanese.

## Compatibility

- No changes to file formats, save behavior, settings (`%LocalAppData%\VisualJson\settings.json`), logs, shortcuts, or the UI. Documents saved by 1.2.0 and 1.3.0 are interchangeable.
- The JSON Lines line-format saving introduced in 1.2.0 is unchanged; see the 1.2.0 notes if you are upgrading from 1.1.0.

## Included features (unchanged from 1.2.0)

- AvalonEdit-based text editing with folding, search/replace (regex), key completion (Ctrl+Space), and virtualized rendering for multi-MB documents.
- Hierarchical grid editing with JSON Pointer paths, filtering, drag-and-drop (including across parents), duplicate, and undo/redo.
- Table View for object-majority arrays (up to 10,000 rows) with per-cell editing, `+ Row` / `+ Column`, display sorting, and "Apply to Structure".
- JSON Schema validation (practical keyword subset incl. `const`, length limits, same-document `$ref`, `format` warnings) with click-to-jump diagnostics and a schema definition view.
- JSON ⇔ XML and JSON ⇔ YAML conversion with mandatory previews; failed or cancelled conversions never touch any file. XXE-safe XML reading.
- Save safety: strict validation before save, temp-file replacement, generation backups, and recovery snapshots. Encoding (UTF-8/UTF-8 BOM/UTF-16 LE/BE) and newline style are preserved.
- English/Japanese UI switching.

## Known limitations

Unchanged from 1.2.0 — see the Limitations section in [README.md](README.md).

## Verification

- Unit/integration: `dotnet test` 97/97 pass (TRX recorded).
- Regression: the full Phase 2 acceptance harness (real-window automation across text/grid/table/schema/conversion/save flows) passes with exit code 0.
- Package: `verify-release-package.ps1` passes all checks including the docs-exclusion scan.

The Windows ZIP includes README (EN/JA), LICENSE, NOTICE, CONTRIBUTING, third-party notices, CHANGELOG, these release notes, samples, and demo assets. Corresponding source is available from the `v1.3.0` GitHub tag.
