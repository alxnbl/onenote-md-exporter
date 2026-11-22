---
applyTo: "**"
excludeAgent: "cloud-agent"
---

# Native Copilot code review

Apply these rules only when performing a review, not an implementation task.

- Apply the [contribution policy](../../doc/contribute.md). For new features,
  check the linked feature request and explicit maintainer acceptance before
  the implementation PR. Missing acceptance is an evidenced scope concern for
  the maintainer, not a functional defect or permission to close/reject a PR.
  Bug/regression and export-quality fixes need no new feature request unless
  they change capabilities/defaults. Roadmap entries alone are not acceptance.
- Read the diff and enough surrounding code to establish causality. Focus on
  high-confidence defects introduced or exposed by the change, not speculative
  redesigns, style preferences, or unrelated baseline problems.
- For each confirmed defect, give the changed file/line, triggering input or
  execution path, impact, and concrete evidence. Use an inline comment only when
  its location is in the diff; put broader validation gaps in the review summary.
- Pay particular attention to content loss, broken image/attachment references,
  filename collisions, link mapping order, Joplin metadata, accidental default
  changes, hidden partial errors, unbounded waits, and unsafe COM/file cleanup.
- Distinguish confirmed defects from concerns requiring reproduction. Mention
  missing targeted tests/docs without claiming tests failed if they were not
  run. No arbitrary percentage coverage gate.
- Flag unrelated build, dependency, or policy changes and overlaps with existing
  contributions as scope/dependency concerns, not proof of a functional defect.
  Do not reject a focused fix merely because other open issues remain.
- Assess human-readable simplicity, reliability/security cost, GPL-compatible
  reuse and attribution, and accessibility of localized warnings/defaults.
  Prioritize export-quality fixes over optional features without prejudging
  individual backlog PRs or categorically rejecting roadmap directions.
- AI-generated code, tests, and instructions still require independent human
  review/validation. Report limitations and evidence; AI approval is not a
  substitute for the maintainer's decision or a trigger for automatic merging.
- Review only: do not edit files, execute fixtures, launch Office, or change
  repository policy. When a skill is relevant, use its evidence checklist, not
  its implementation or execution steps.
