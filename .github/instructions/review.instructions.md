---
applyTo: "**"
excludeAgent: "cloud-agent"
---

# Native Copilot code review

Apply these rules only when performing a review, not an implementation task.

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
- Review only: do not edit files, execute fixtures, launch Office, or change
  repository policy. When a skill is relevant, use its evidence checklist, not
  its implementation or execution steps.
