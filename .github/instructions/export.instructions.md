---
applyTo: "src/OneNoteMdExporter/**/*.cs"
---

# Export implementation

- Trace the affected stage before editing: `Services/ConverterService.cs`,
  `Services/Export/ExportServiceBase.cs`, the format-specific
  `MdExportService.cs` / `JoplinExportService.cs`, and the relevant models/helpers.
  Shared changes must consider both formats, including Joplin IDs, `:/` resource
  references, hierarchy, and metadata.
- Preserve intentional whitespace and Unicode. For regex/XML transformations,
  distinguish formatting artifacts from user content; retain escaping and
  meaningful tables, lists, quotes, and fenced code. Avoid broad replacements
  that silently remove valid text.
- For attachment/path changes, consider repeated attachments, distinct files
  sharing a name, truncated titles, Windows case-insensitive collisions, invalid
  filename characters, relative paths, and link targets. Naming must stay
  consistent with the references written to both output formats.
- For link changes, inspect `OneNoteLinkTranslatorService.cs` and format-specific
  link generation. Test a target exported after the referencing page, unresolved
  targets, and configured link modes. Do not silently expand supported link
  types or alter the documented fallback.
- New options must update `Infrastructure/AppSettings.cs`, `appSettings.json`,
  relevant documentation, and user-facing keys in `Resources/trad.*.json` when
  applicable. Preserve the English fallback and format placeholders.
- Errors must remain visible in existing logging and export results. Do not
  report complete success after losing an image, attachment, or page. Keep
  expected partial-export behavior explicit rather than adding broad catches.
- New or modified process waits/retries need explicit finite bounds, observable
  failure, and appropriate output/error handling. Release owned COM references
  on failure as well as success; do not terminate unrelated Office sessions.
  Validate cleanup paths and keep changes to original notebooks off limits.
