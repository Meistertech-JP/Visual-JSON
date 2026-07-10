# Changelog

## Unreleased

- No unreleased changes.

## 1.3.0

Internal-quality release: no user-facing feature changes. The structure changed; the behavior did not.

- Split the 3,564-line `MainWindow.xaml.vb` into eleven feature-scoped partial-class files (File, TextEditor, Grid, TableView, Schema, Conversion, Settings, Diagnostics, Commands, Localization, Automation); the root file now holds only fields, the constructor, window lifecycle, and the keyboard router.
- Realized the view models: `MainViewModel` (mode, busy state, status text, selected JSON Pointer), `DocumentViewModel` (path, boundary-synced text, dirty flag, format, encoding/newline), and a new `MessagePaneViewModel` owning the Syntax/Schema/Conversion/Log collections. Status-bar segments now bind to them.
- Introduced gesture-less `RoutedUICommand`s for New/Open/Save/Save As/Exit; keyboard shortcuts remain under the central key router (no double firing).
- Migrated the 82 custom-harness tests to MSTest with byte-identical bodies and preserved display names; `dotnet test` with TRX output is now the single test entry point (97 tests including the new view-model, security, and log-redaction suites).
- Corrected `UT-P2-STA-006` to apply its 200ms requirement to the p95 of each 10MB Capture/Restore/FindNodeAtOffset sample instead of the aggregate time for repeated samples, removing CI runner-speed sensitivity without relaxing the acceptance criterion.
- Added GitHub Actions CI (`build-test-package.yml`): restore, build, test with TRX, package, release-zip verification, and artifact upload on main pushes, pull requests, and manual dispatch. `global.json` pins the SDK feature band.
- Added `scripts/verify-release-package.ps1`: checks the zip/sha256 pair (hash recomputed), required public documents, README link integrity inside the zip, the application executable, and that `docs/` is never bundled — with a self-test covering the failure cases on every CI run.
- Hardened the external-schema URL guard: CGNAT `100.64.0.0/10`, IPv4 multicast/reserved (`224.0.0.0/4`, `240.0.0.0/4`), IPv6 unspecified/multicast, and embedded-IPv4 re-checking for 6to4 (`2002::/16`) and Teredo (`2001:0::/32`). External fetching remains off by default.
- Audited all 60 `Catch` blocks across source, tools, and tests (classification recorded; zero forbidden swallows): every intentional ignore carries a reason comment, and two previously silent schema-probe failures now write typed file-log entries.
- Aligned the README comparison table with the Limitations section (schema validation is a practical keyword subset) in both English and Japanese.

## 1.2.0

- Added Table View for object-majority arrays (up to 10,000 rows): read-only display, scalar cell editing with type inference, missing-cell materialization on the edited row only, `+ Row` / `+ Column`, display-only column sorting, and "Apply to Structure" as one undo step.
- Added cross-parent grid drag-and-drop with key-conflict confirmation (unique key) and rejection of moves into a node's own descendants.
- Added `{}` / `[]` cell input inference to empty object/array.
- Added JSON Schema `const`, `minLength`/`maxLength` (code points), same-document `$ref` with cycle detection and depth limit 32, and `format` warnings (`date-time`/`date`/`time`/`email`/`uri`). External `$ref` warns and is never fetched.
- Added key completion (Ctrl+Space): sibling-object keys plus schema properties minus existing keys, inserted as `"key": `.
- Added XML export options in the preview: array expansion (`<item>` elements or repeated parent name) and null representation (empty element or `xsi:nil="true"`). Defaults keep the v1.1.0 output byte-for-byte.
- Changed JSON Lines saving to line format: one compact JSON per line (see the release notes compatibility section).
- Fixed a latent issue where a JSON Lines document failed revalidation and saving after a grid edit (the array-form editor text was re-wrapped per line by the JSONL preprocessor).
- Verified quote auto-pair type-over behavior in the acceptance harness (C-P2-001; no code change needed).
- Hardened acceptance reporting: SC-P2-003 asserts its own measured conditions, traceability rows derive from measured results, and the P2-0-008 spike status now includes the 300ms threshold.

## 1.1.0

- Added Phase2 R1 state-preserving editor workflow hardening: P2-1/P2-2 acceptance is re-run as part of the R1 gate.
- Added persistent settings under `%LocalAppData%\VisualJson\settings.json` with broken-setting recovery and forward-compatible unknown-key preservation.
- Added Recent Files, history clearing, and file drag-and-drop open through the same Open flow.
- Added UTF-8/UTF-8 BOM/UTF-16 LE/UTF-16 BE detection, encoding preservation on save, and CRLF/LF preservation.
- Added unknown-extension format sniffing for JSON/JSONC/JSON5/JSON Lines.
- Added grid Redo, node Duplicate, and row context menu commands including Copy JSON Pointer and Jump to Text.
- Added About, file logging without document/schema body, and R1 acceptance reporting.

## 1.0.0

- Replaced the RichTextBox-based text editor with AvalonEdit (MIT). Rendering, line numbers, syntax coloring, and error markers are now virtualized per visible line; a 390KB document that previously froze the UI for over 45 seconds now opens with the UI responsive in under 2 seconds.
- Added an optional pretty-print prompt when opening files that contain extremely long single lines, keeping 10MB-class unformatted JSON responsive. Declining opens the file unchanged.
- Added MVP-2 local JSON Schema validation (type, required, properties, additionalProperties, minimum, maximum, pattern, enum, array items).
- Added schema diagnostics with JsonPath, JSON Pointer, body line/column, SchemaPath, and schema URI, plus body jump and schema definition view.
- Added external `$schema` fetching that is off by default, HTTPS-only, and blocks redirects to HTTP, file, UNC, localhost, and private IP ranges. Hostnames are validated after DNS resolution (including IPv4-mapped IPv6) and connections are pinned to validated addresses.
- Added MVP-3 JSON to XML, XML to JSON, JSON to YAML, and YAML to JSON conversion with pre-save preview and a Conversion message tab.
- Added XXE-safe XML reading (DTD, external entities, and external subsets prohibited) and safe temp-file export with backups.
- Added RFC 6901 JSON Pointer to grid nodes and switched the grid Path column to JSON Pointer display (JSONPath in the tooltip).
- Registered committed grid cell edits as one undo operation.
- Removed diagnostic and exception message text from the diagnostics copy so no document fragments can leak.
- Extended ValidationDiagnostic with ErrorCode, JsonPath, JsonPointer, SchemaPath, SchemaUri, and RelatedRange per the detailed design.
- Localized the message pane (Syntax/Schema/Conversion/Log tabs and columns) for English/Japanese switching.

## 0.2.0-beta

- Added MVP-1 JSONC, representative JSON5, JSON Lines, and NDJSON open/parse support.
- Added grid Action and Grip columns with add, delete, move, type-change, drag reorder, and undo operations.
- Added grid filtering by key, value, type, and path while retaining parent context.
- Added English/Japanese UI switching for the main menus, toolbar, tabs, and grid headers.
- Added 50MB+ large-file behavior that keeps text mode available and disables full grid editing.
- Documented JSONC/JSON5 comment-retention limits and JSON5 coverage limits.

## 0.1.0-alpha

- Added MVP-0 standard JSON text editing and hierarchical grid view.
- Added strict syntax diagnostics with line/column jump.
- Added standard JSON formatting.
- Added grid Key/Value editing and Grid to Text synchronization.
- Added save, Save As, pre-overwrite backups, recovery snapshots, and diagnostics copy without JSON body.
- Added Windows x64 self-contained packaging and SHA256 checksum output.
