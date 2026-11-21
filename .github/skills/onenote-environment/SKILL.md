---
name: onenote-environment
description: Diagnose Windows, .NET, COM, Office, or Pandoc blockers for OneNote Md Exporter without modifying the installation or touching original notebooks.
---

# OneNote environment diagnostics

Use when an export or proposed validation is blocked by the environment.
This is diagnosis, not an installer, build refactor, or automatic live export.
During code review, inspect supplied environment evidence only; run no commands.

## Procedure

1. Capture the failing stage, exact attempted command, exit code, and a minimal
   redacted error. Distinguish build failure, COM activation/OneNote publish,
   Pandoc conversion, and output processing. Never request a whole private log
   directory, notebook dump, credentials, or environment-variable dump.
2. Read the current application and integration-test project files. Both target
   `net10.0-windows7.0` and use OneNote `COMReference`; the application specifies
   `win-x86`. Do not assume a machine's installed Office/toolchain architecture
   is compatible, or that changing the RID will fix the problem.
3. With permitted read-only commands, inspect the SDK (`dotnet --info`) and
   discover available MSBuild/Visual Studio tooling (for example,
   `Get-Command MSBuild.exe -ErrorAction SilentlyContinue`). A missing PATH entry
   does not prove Visual Studio is absent. Report discovered facts and unknowns;
   no full build is required for diagnosis.
4. Verify Windows and desktop OneNote/Word availability from safe local evidence
   or the user's report. The Store-only OneNote application is not the required
   desktop interop environment. Do not instantiate COM merely to probe it,
   repair Office, change registry entries, reinstall software, or kill processes.
5. Inspect the bundled `src/OneNoteMdExporter/pandoc` files and the current working
   directory used by `ConverterService.ConvertDocxToMd`. The shipped ZIP is not
   an executable; the converter looks for `pandoc\pandoc.exe` and then
   `pandoc.exe`. Do not download/replace/extract binaries without approval.
6. State the validation boundary: plain `dotnet build` cannot resolve these
   `COMReference` projects (MSB4803); current integration tests need Office and
   an open sample notebook, and the Joplin image test is unimplemented. A
   different runner or COM-free tests are future work, not an existing fallback.
7. Recommend one bounded next action justified by the evidence. Any subsequent
   live export needs explicit approval for a disposable synthetic notebook and
   known output/temp paths. Preserve originals and expose failures honestly.

## Evidence to return

Return the failing stage, verified prerequisites, commands actually run and
results, remaining unknowns, and the smallest safe next step. Do not label an
export validated after only checking the SDK or finding an executable.
