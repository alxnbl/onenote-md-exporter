---
description: Implement a supplied export regression using synthetic before/after evidence.
---

Fix the export behavior identified in this request. Follow
[repository guidance](../copilot-instructions.md), applicable path-specific
instructions, and [export regression](../skills/export-regression/SKILL.md).

Apply the [contribution policy](../../doc/contribute.md). If this is a new feature
or capability/default change rather than a fix, identify the scope for maintainer
decision; a new feature requires a feature request and explicit maintainer
acceptance before its implementation PR. A roadmap entry does not satisfy this.

Confirm the input, faulty output, expected output, settings, affected formats, and
related contribution dependencies before editing. If the intended behavior is
missing, ask for it rather than inventing a feature. Keep the change scoped and
preserve unaffected content and defaults.
Keep the fix human-readable and GPL-compatible; provide clear localized warnings
and backup/limitation advice where behavior changes. Human review/validation is
required; AI-only approval or automatic merging is not acceptance.

Use [environment diagnostics](../skills/onenote-environment/SKILL.md) if validation
is blocked. Report actual before/after evidence, commands/results, and unexecuted
checks; do not imply standalone unit tests exist today. Do not run a live Office
export without approval. This template does not request a commit, push, PR,
review request, merge, issue assignment, or policy/automation change.
