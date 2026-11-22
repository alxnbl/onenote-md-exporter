---
description: Read-only triage of an export issue, its reproduction, and related contributions.
---

Analyze the supplied issue and comments without editing files or executing
commands. Follow [repository guidance](../copilot-instructions.md) and the
[issue-triage procedure](../agents/issue-triage.agent.md).

Apply the [contribution policy](../../doc/contribute.md). Classify the request:
bug/regression, export quality, or new feature. New features need a feature
request and explicit maintainer acceptance before an implementation PR; do not
create it automatically. Fixes need no new feature request unless they change
capabilities/defaults. Treat the roadmap as discussion, not acceptance.

Use the issue/reproduction and related PR metadata supplied with this request.
If current GitHub context is unavailable, state the gap instead of claiming you
checked existing contributions. Ask only for missing information needed to
identify the behavior, using synthetic data or redacted errors.

Return confirmed facts, the likely affected stage with source evidence,
overlapping contributions/dependencies, and one scoped next task with acceptance
criteria and feasible validation. Stop at analysis; do not implement or assign it.
Explain tradeoffs for reliability/security, simple human-readable design, and
nontechnical/multilingual accessibility so a human can decide.
