---
name: export-regression
description: Reproduce and validate a Markdown or Joplin export regression with minimal synthetic input and before/after output; use for transformations, attachments, paths, or links.
---

# Synthetic export regression

Use this workflow for an explicitly requested behavioral fix or its validation.
During code review, inspect existing evidence only; do not execute these steps,
edit files, or launch Office.

## Procedure

1. Read the applicable repository instructions and trace the affected stage.
   Record the export format, relevant setting values, input, current faulty
   output, and expected output. Check relevant contribution dependencies before
   implementing an overlapping fix.
2. Minimize the reproduction to synthetic text/XML, mock page metadata, or
   artificial resource files. Remove personal paths, author identities, real
   notebook IDs, embedded secrets, and private binaries. Keep a short provenance
   explanation with any committed fixture.
3. Choose the narrowest viable validation boundary. Pure string/XML tests are
   preferred, but no standalone COM-free test project currently exists.
   Referencing the application does not avoid its `COMReference` build limit.
   If no supported runner is available, prepare explicit input/expected-output
   cases and report them as unexecuted, not passing tests.
4. Cover the triggering case and an unaffected control, then relevant edges:
   - Formatting: CRLF/LF, whitespace/nonbreaking spaces, Unicode, HTML/Markdown
     escaping, meaningful quotes, tables, lists, and fenced code.
   - Resources: the same attachment repeated, different files with the same
     filename, truncated/case-colliding page names, and relative references.
   - Links: a target visited after the source, unresolved targets, configured
     link modes, and consistency between names, IDs, and written destinations.
   Use only cases relevant to the change, not an exhaustive unrelated suite.
5. For shared behavior, include Markdown and Joplin evidence. Check retained
   content, resource identity and bytes, references, and format metadata rather
   than relying on file existence or a success exit code.
6. Demonstrate that the targeted case fails before the fix and passes after it
   using an isolated, non-destructive comparison when the runner is available.
   Do not revert unrelated work. Restore any modified global settings/mappings.
7. If live Office export is necessary, first obtain approval for a disposable
   synthetic notebook and verified output/temp folders. Protect originals,
   keep process waits/retries finite, release owned COM references, and never
   terminate unrelated Office processes. Report partial failures explicitly.

## Evidence to return

Provide the reproduction and expected output, observed before/after difference,
checks for unaffected content and both formats where relevant, and exact commands
actually executed with results. Separate unexecuted cases and environment
limitations from passing checks. Do not claim a missing test boundary exists or
require an arbitrary overall coverage percentage.
