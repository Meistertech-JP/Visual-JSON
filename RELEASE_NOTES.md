# Visual JSON 1.2.0 Release Notes

This release completes Phase 2 (R2): a Table View for object arrays, cross-parent drag-and-drop, JSON Schema extensions (`const`, `minLength`/`maxLength`, local `$ref`, `format` warnings), XML conversion options, JSON Lines line-format saving, and key completion.

## New in 1.2.0

- Table View: object-majority arrays open as "row = element, column = property" from the grid Action column. Missing properties show as empty cells. Up to 10,000 rows; larger arrays warn and stay in the tree.
- Table editing: scalar cells edit in place with the standard type-inference rules; editing an empty cell creates the property on that row only. `+ Row` appends an empty object row, `+ Column` adds a display column that materializes per edited row. Each committed cell edit is one undo step.
- Table sorting: column-header sorting changes the display only. "Apply to Structure" writes the display order into the array as one undo step; the `#` column restores the structural order.
- Grid: drag-and-drop between different parents. Key conflicts ask for confirmation and are resolved with a unique key; moving a node into its own descendants is rejected.
- Grid: entering exactly `{}` or `[]` in a value cell converts the node to an empty object or array.
- Schema validation adds `const`, `minLength`/`maxLength` (code points), and same-document `$ref` (`#/...`) with cycle detection (`SCH-REF-CYCLE`) and a depth limit of 32. External `$ref` URLs are reported as a warning and never accessed. `format` (`date-time`/`date`/`time`/`email`/`uri`) reports warnings.
- Key completion (Ctrl+Space in text mode): suggests keys used by sibling objects in the same array plus schema `properties`, minus existing keys, and inserts `"key": ` from the keyboard.
- XML export options in the preview: arrays as repeated `<item>` elements (default) or repeated parent-name elements; `null` as an empty element (default) or `xsi:nil="true"` with the `xmlns:xsi` declaration. Options are not persisted; every export starts from the defaults.
- JSON Lines documents are now saved in line format: one compact JSON per line. The editor continues to display the array-form standard JSON.
- Fixed a latent v1.1.0 issue where a JSON Lines document could no longer be revalidated or saved after a grid edit.

## Compatibility Notes

- **JSON Lines save format changed.** v1.1.0 saved `.jsonl`/`.ndjson` documents as a pretty-printed standard JSON array. v1.2.0 saves them as one compact JSON per line (the conventional JSONL form). Blank lines and per-line formatting of the original file are not preserved, and the file always ends with a single trailing newline. If you need the v1.1.0 behavior, save the document with a `.json` extension instead.

## Included (carried over from 1.1.0)

- AvalonEdit-based text editor with virtualized rendering, built-in line numbers, visible-line syntax coloring, and error-line markers. Large documents stay responsive.
- An optional pretty-print prompt when opening files with extremely long single lines (unformatted machine JSON).
- Standard JSON, JSONC, representative JSON5, JSON Lines, and NDJSON open/edit/save workflow.
- Text and grid modes with synchronization; grid shows JSON Pointer paths. Text/grid position sync, folding, replace, and search highlighting.
- Syntax diagnostics with error codes and error jump.
- Grid Action and Grip columns for add, delete, move, type change, drag reorder, Duplicate, Redo, row context menus, Copy JSON Pointer, and Jump to Text.
- Grid filtering with parent context retained.
- English/Japanese UI switching, including the message pane and all Table View text.
- Save-before-overwrite backup with temp-file replace saving; recovery snapshot prompt on next launch.
- Diagnostics copy without JSON body (message text excluded; error codes, exception type, and stack trace only).
- External `$schema` fetching off by default; HTTPS-only with dangerous-redirect blocking, DNS-resolution checks (including IPv4-mapped IPv6), and connection pinning to validated addresses when enabled.
- JSON to XML / XML to JSON and JSON to YAML / YAML to JSON conversion with pre-save preview and Conversion warnings tab. XXE-safe XML reading.
- Failed conversions and cancelled previews never modify any file.
- Settings persist language, window placement, backup behavior, external schema permission, bracket completion, schema search paths, and recent files; broken settings are moved aside and defaults load.
- Recent Files keeps up to 10 paths, can be cleared, and removes missing files when selected. Drag-and-drop open.
- UTF-8, UTF-8 BOM, UTF-16 LE, and UTF-16 BE are detected and preserved on save; CRLF/LF is preserved by majority detection. Unknown extensions are sniffed.
- File logs are written under `%LocalAppData%\VisualJson\logs` without document or schema body content.

## Known Limitations

- JSONC/JSON5 comments are not preserved after formatting, grid synchronization, save, or conversion (best effort).
- JSON5 support is limited to representative syntax, not the full JSON5 language.
- JSON Lines is handled as an array-equivalent document; the saved line format does not preserve blank lines or original per-line formatting.
- Schema validation supports the listed keyword subset; `allOf`/`anyOf`/`oneOf`, `$id`, and remote `$ref` are not supported.
- Table View sorting compares values type-aware (missing < null < boolean < number < string < containers); container cells are read-only summaries.
- YAML to JSON supports a representative block-style subset; anchors, aliases, tags, block scalars, and non-empty flow collections are rejected.
- XML to JSON keeps values as strings by default.
- Files of 50MB or larger open in text mode with full grid editing disabled for the session.
- 100MB-class full grid editing is not guaranteed.
- BSON, split view, and multiple tabs remain later-phase candidates.
- The Windows package is unsigned; SmartScreen may warn on first launch. Verify the SHA256 checksum.
- Lines longer than about 8,000 characters are rendered without token coloring to preserve responsiveness.
