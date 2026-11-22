---
applyTo: "src/**/*Tests*/**/*.cs,src/**/*Tests*/**/*.csproj"
---

# Regression validation

- Existing tests are NUnit integration tests in `OneNoteMdExporter.IntTests`,
  not a reliable COM-free suite. `Helpers/TestHelper.cs` launches the exporter
  against an open notebook; the Joplin image test is unfinished. Distinguish
  these baseline limitations from a regression introduced by the change.
- Add a few behavior-focused cases that fail without the fix: minimal synthetic
  input, exact expected output, the edge case, and one unaffected control.
  Never use an empty result or ignored exception to make a failing export pass.
- Choose only relevant cases: CRLF/LF, nonbreaking spaces, Unicode/escaping,
  duplicate attachments and path collisions, forward links, tables, lists, or
  fenced code. Assert preserved content and resource/link consistency, not just
  file existence or exit status.
- When changed behavior introduces a user-visible warning or partial failure,
  include evidence that it is observable and actionable, with correct localized
  placeholders/fallbacks. Keep cases understandable for independent human
  validation; generated tests or AI approval alone are not acceptance evidence.
- Isolate global `AppSettings` and link metadata state when a test changes it,
  restoring prior values so test order does not change results.
- Prefer deterministic string/XML tests without Office for transformation logic
  if the requested work can introduce a small justified test boundary. A string
  test referencing the current application can still require COM at build time.
  Do not silently refactor project infrastructure to pretend otherwise.
- Keep fixtures synthetic/anonymized and explain their provenance. Live Office
  validation needs an approved disposable notebook and known output/temp paths.
  Report exact commands, actual results, and checks not run with the reason.
