---
description: Review a specified export diff for evidenced defects and regression gaps without mutations.
---

Review the supplied diff or PR snapshot without editing files, executing code,
or posting comments. Follow [repository guidance](../copilot-instructions.md)
and [review criteria](../instructions/review.instructions.md).

Check the [contribution policy](../../doc/contribute.md), including explicit
maintainer acceptance on a feature request before a new-feature implementation
PR. Distinguish ordinary fixes from capability/default changes and unapproved
feature scope; roadmap entries are not acceptance. Flag evidence for the
maintainer without closing/rejecting PRs or treating AI-only review as approval.
Keep evidence understandable for independent human review and validation.

Confirm the diff's base/head and requested scope from the supplied context; if
the diff is unavailable, request it. Read enough surrounding source to establish
impact. Use [export regression](../skills/export-regression/SKILL.md) only as an
evidence checklist, not an instruction to execute tests or implement a fix.

Return only actionable, high-confidence defects with changed location, trigger,
impact, and evidence. Separately list material unverified concerns and missing
targeted tests/docs. State when no confirmed defect is found and what remains
unverified. Do not block on unrelated baseline issues or invent a coverage gate.
