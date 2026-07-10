# Visual JSON

A Windows-first JSON editor built with VB.NET and WPF. Edit JSON as text or as a structured grid, view object arrays as an editable table, validate against JSON Schema, and convert to and from XML and YAML — with strict guarantees that saving and converting never corrupt your files.

日本語版は [README.ja.md](README.ja.md) を参照してください。

![Visual JSON workflow demo: deep JSON, grid pointer sync, table editing, schema jump, and safe conversion preview](assets/demo/visual-json-workflow.gif)

## Why Visual JSON

| Feature | VS Code | Online JSON tools | Visual JSON |
| --- | --- | --- | --- |
| Local-only workflow | Yes | No | Yes |
| Grid editing | Extension-dependent | Limited | Built in |
| Table View | Limited | Limited | Built in |
| Schema diagnostic jump | Yes | Partial | Yes * |
| XML/YAML conversion preview | Partial | Partial | Built in |
| Pre-save validation / backup / recovery | Partial | No | Built in |

\* Schema validation covers a practical subset of JSON Schema keywords; `allOf`/`anyOf`/`oneOf`, `$id`, and remote `$ref` are not supported. See [Limitations](#limitations-v120).

## Sample Files

The [`samples/`](samples/) folder is included so you can see the value quickly:

- [`api-response-invalid.json`](samples/api-response-invalid.json): open it with [`schema/user.schema.json`](samples/schema/user.schema.json) to try schema diagnostics and click-to-jump.
- [`users-array.json`](samples/users-array.json): switch to Grid, open Table View, sort, and edit a scalar cell.
- [`config.jsonc`](samples/config.jsonc): try JSONC comments, trailing commas, validation, and formatting.
- [`events.jsonl`](samples/events.jsonl): open JSON Lines as an array-equivalent document and save back as line format.

## Screenshots

| Table View | Schema diagnostics |
| --- | --- |
| ![Visual JSON Table View showing an editable object-array table](assets/screenshots/table-view.png) | ![Visual JSON Schema diagnostics showing validation errors with pointers and schema paths](assets/screenshots/schema-diagnostics.png) |

## Highlights

- **Fast text editing** — AvalonEdit-based editor with line numbers, folding, search/replace (regex supported), and search highlighting. Rendering and syntax coloring are virtualized per visible line, so multi-MB documents stay responsive.
- **Grid editing** — valid JSON becomes a hierarchical tree with Key, Value, Type, and JSON Pointer columns. Add, delete, move, duplicate, retype, drag-and-drop (including across parents), and undo/redo structural edits.
- **Table View** — object arrays open as "row = element, column = property" for spreadsheet-style browsing, editing, and sorting.
- **JSON Schema validation** — a practical keyword subset including `const`, length limits, and same-document `$ref`, with diagnostics that jump to the exact body position.
- **Safe conversion** — JSON ⇔ XML and JSON ⇔ YAML with a mandatory preview; a cancelled or failed conversion never touches any file.
- **English / Japanese UI** — switch the entire UI, including the message pane and Table View, at runtime.

## Features (v1.2.0)

### Text editing

- Open and edit standard `.json` plus `.jsonc`, `.json5`, `.jsonl`, and `.ndjson` through the same workflow; unknown extensions are sniffed from content.
- Syntax validation after a 500 ms debounce with line/column diagnostics and click-to-jump.
- Folding, find/replace with optional regex and case sensitivity, search highlighting, and bracket/quote auto-pairing (toggleable).
- Key completion with Ctrl+Space: keys used by sibling objects in the same array plus schema `properties`, minus keys already present.
- Files with extremely long single lines (unformatted machine output) trigger an optional pretty-print prompt; declining opens the file unchanged.

### Grid and Table View

- Hierarchical grid with JSON Pointer paths, filtering with parent context, and a row context menu (Add child / Add sibling / Delete / Move / Duplicate / Copy JSON Pointer / Jump to Text).
- Structural edits — including committed cell edits — register as single undo steps, with multi-step redo.
- Drag rows within a parent or to a different parent; key conflicts ask for confirmation and resolve with a unique key, and moving a node into its own descendants is rejected.
- Enter exactly `{}` or `[]` in a value cell to convert the node to an empty object or array.
- **Table View** for object-majority arrays (up to 10,000 rows; larger arrays warn and stay in the tree):
  - Missing properties display as empty cells; editing one creates the property on that row only.
  - `+ Row` appends an empty object row; `+ Column` adds a display column that materializes per edited row.
  - Column-header sorting is display-only until you press **Apply to Structure** (one undo step); the `#` column restores the structural order.
  - Container cells show read-only summaries (`{…}`, `[n]`); double-click to jump to that node in the tree.

### JSON Schema validation

- Supported keywords: `type`, `required`, `properties`, `additionalProperties`, `minimum`, `maximum`, `pattern`, `enum`, array `items`, `const`, `minLength`/`maxLength` (counted in code points), and same-document `$ref` (`#/...`) with cycle detection and a depth limit of 32.
- `format` checks (`date-time`, `date`, `time`, `email`, `uri`) report warnings; unknown formats are ignored.
- Diagnostics carry JsonPath, JSON Pointer, body line/column, SchemaPath, and schema URI; click to jump to the body position or open the schema definition view at the failing keyword.
- External `$schema` fetching is **off by default**. When explicitly enabled, only HTTPS is allowed; redirects to HTTP, `file:`, UNC, localhost, and private/link-local ranges are blocked; hostnames are re-checked after DNS resolution (including IPv4-mapped IPv6) and connections are pinned to the validated addresses. External `$ref` URLs inside a schema are reported as warnings and never fetched.

### Conversion

- Export JSON as XML (`@name` → attribute, `#text` → element text) or as YAML with 2-space indentation; open XML/YAML files as JSON documents.
- XML export options in the preview: arrays as repeated `<item>` elements (default) or repeated parent-name elements; `null` as an empty element (default) or `xsi:nil="true"`. Options re-convert the preview instantly and always reset to the defaults.
- Every export shows a preview first; cancelling changes nothing, and a failed conversion never touches the original file.
- XML reading prohibits DTDs, external entities, and external subsets (XXE-safe).

### File safety and environment

- Save As / overwrite saves validate strict JSON first, write through a temp file, and create a generation backup.
- JSON Lines documents save in line format (one compact JSON per line) while the editor displays the array-form standard JSON. **Note:** this changed in v1.2.0 — see [RELEASE_NOTES.md](RELEASE_NOTES.md) for the compatibility details.
- UTF-8, UTF-8 BOM, UTF-16 LE, and UTF-16 BE are detected and preserved on save; CRLF/LF is preserved by majority detection.
- Recovery snapshots for unsaved edits, restorable on the next launch.
- Settings (language, window placement, backups, schema options, recent files) persist under `%LocalAppData%\VisualJson\settings.json`; broken settings are quarantined and defaults load.
- File logs under `%LocalAppData%\VisualJson\logs` never contain document or schema body content, and neither does the diagnostics copy.

## Getting started

Download `visual-json-v1.2.0-win-x64.zip`, verify the SHA256 checksum shipped next to it, extract, and run `VisualJson.App.exe`. The package is self-contained — no .NET installation is required. The binary is unsigned, so SmartScreen may warn on first launch.

## Building from source

Requires the .NET 10 SDK on Windows.

```powershell
# Build
dotnet build VisualJson.slnx

# Run
dotnet run --project src\VisualJson.App\VisualJson.App.vbproj

# Test
dotnet run --project tests\VisualJson.Tests\VisualJson.Tests.vbproj

# Package (outputs zip + sha256 under artifacts/)
.\scripts\package-windows-x64.ps1
```

## Limitations (v1.2.0)

- JSONC / JSON5 input is normalized to standard JSON for parsing. Comments are not preserved after formatting, grid sync, save, or conversion (best effort by design).
- JSON5 support covers representative syntax (comments, single-quoted strings, unquoted keys, trailing commas), not the full JSON5 language.
- JSON Lines is treated as an array-equivalent document. Saving uses the line format and does not preserve blank lines or the original per-line formatting.
- Schema validation covers the keyword subset listed above. `allOf`/`anyOf`/`oneOf`, `$id`, remote `$ref`, and draft-specific behaviors are not supported.
- YAML to JSON covers a representative block-style subset. Anchors, aliases, tags, block scalars (`|`, `>`), and non-empty flow collections are rejected with a clear error.
- XML to JSON keeps values as strings by default.
- Files of 50 MB or larger open in text mode with full grid editing disabled for the session; 100 MB-class full grid editing is not guaranteed.
- Lines longer than about 8,000 characters render without token coloring to preserve responsiveness.
- Table View container cells are read-only summaries.
- BSON, split view, and multiple tabs are later-phase candidates.

## License

Source code is licensed under the Mozilla Public License Version 2.0 (MPL-2.0); see [LICENSE](LICENSE). Corresponding Source Code Form for executable packages is available from the matching GitHub tag, for example [`v1.2.0`](https://github.com/Meistertech-JP/Visual-JSON/tree/v1.2.0). The Visual JSON name, logo, screenshots, demo GIFs, image assets, and project branding are reserved and are not licensed under the MPL-2.0; see [NOTICE.md](NOTICE.md). AvalonEdit remains licensed separately under MIT; see [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md). External contributions use DCO sign-off; see [CONTRIBUTING.md](CONTRIBUTING.md).
