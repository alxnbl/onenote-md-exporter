# Versioned Copilot guidance proposal

This first block adds repository context, two focused roles, two reusable skills,
and three optional prompt templates. It does not change exporter code, tests,
dependencies, builds, CI, repository settings, or review automation.
Native GitHub Copilot code review remains the default PR-review surface; a
separate review agent would duplicate it.

## Alignment with project principles

The canonical [contribution policy](contribute.md) now makes these previously
implicit or missing expectations explicit: simple human-readable architecture,
reliability/security before secondary features, free/open-source GPL version 3
with compatible reuse and attribution, accessible localized warnings/defaults,
backup and loss-risk disclosure, and independent human validation of AI work.

New features require a feature request and explicit maintainer acceptance before
their implementation PR. Ordinary bug/regression and export-quality fixes need
no new feature request or second feature approval; capability/default changes
must be identified for maintainer decision. Shared instructions, triage,
development, and review consumers apply that distinction. A roadmap item is a
discussion direction, never acceptance or an autonomous task.

This aligns guidance and contribution documentation, **not the application's
technical compliance**. Existing export-loss cases, build/test limitations,
accessibility gaps, and dependency/security questions still need scoped evidence,
discussion, and human validation; updating instructions does not fix them.

## Inventory

| Layer | Files | Purpose |
| --- | --- | --- |
| Shared instructions | [copilot-instructions.md](../.github/copilot-instructions.md) | Canonical context, scope, privacy, validation limits, and review defaults. |
| Path instructions | [export](../.github/instructions/export.instructions.md), [tests](../.github/instructions/tests.instructions.md), [docs/config](../.github/instructions/docs-config.instructions.md) | Focused rules selected by `applyTo` globs. |
| Native review instructions | [review](../.github/instructions/review.instructions.md) | Review-only criteria on GitHub.com; `excludeAgent: "cloud-agent"` excludes the cloud implementation agent. |
| Custom agents | [issue-triage](../.github/agents/issue-triage.agent.md), [export-fix](../.github/agents/export-fix.agent.md) | Read-only analysis and one scoped implementation task. |
| Skills | [export-regression](../.github/skills/export-regression/SKILL.md), [onenote-environment](../.github/skills/onenote-environment/SKILL.md) | Synthetic before/after evidence and safe environment diagnosis. |
| Manual prompts | [analyze-export-issue](../.github/prompts/analyze-export-issue.prompt.md), [fix-export-regression](../.github/prompts/fix-export-regression.prompt.md), [review-export-change](../.github/prompts/review-export-change.prompt.md) | Small compatibility templates referencing the procedures above. |

There is no duplicate `AGENTS.md`, automatic issue assignment, setup workflow,
custom model selection, MCP configuration, or shell preapproval.

## Suggested use

1. **Triage:** select `issue-triage` in a client supporting custom agents. Supply
   the issue/comments, synthetic reproduction, settings, and a recent snapshot
   of related PRs and their dependencies. The role has only `read` and `search`:
   it cannot execute `gh`, fetch live issue metadata, edit, or delegate. Missing
   contribution context is reported, not treated as proof that no overlap exists.
   For a new feature, identify the feature request/maintainer acceptance or
   recommend discussion first; do not create an issue or assume roadmap approval.
2. **Implement:** explicitly request one behavior change with `export-fix`.
   Provide expected output and approved scope. Ask to use `export-regression`;
   use `onenote-environment` only for a real environment blocker. This role has
   `read`, `search`, `edit`, and `execute`, but those capabilities are not
   authorization for privileged commands, Office access, or GitHub mutations.
   If a claimed fix changes capabilities/defaults, flag the product decision for
   the maintainer rather than bypassing the contribution policy.
3. **Review:** when you choose to open a PR, request native Copilot code review
   manually. Look for evidenced defects in changed code and missing focused
   regression/docs, not style noise or unrelated existing issues. The manual
   review template is an alternative for a supplied local diff, not an
   automatic reviewer and not a request to post comments.
   Check feature acceptance and human-reviewable evidence; AI review alone does
   not accept the work.

For example, supply: "Use export-regression to fix this synthetic attachment
collision. Input: two distinct attachments named report.txt. Expected: both
contents retained and every Markdown/Joplin reference points to its own resource.
Do not change CI or dependencies." Choose edge cases relevant to the real task.

## Roadmap for discussion

These are maintainer-proposed directions, not scheduled work or approved
implementation scope. Prioritize technical foundations and fixing poor-quality
or information-losing exports before secondary features. Discuss user benefit,
security/reliability, accessibility, and design/maintenance cost before acceptance.
No timeline, coverage target, automation, or backlog decision is established here.

| Area | Proposed direction and boundary |
| --- | --- |
| Technical foundations | Remove the Visual Studio dependency; [#151](https://github.com/alxnbl/onenote-md-exporter/pull/151) proposes this and was open/unmerged when checked on 2026-11-22. Recheck its status/dependencies and coordinate rather than duplicate or import unmerged work. |
| Delivery and regression | Automate CI/CD and regression testing, after agreeing scope and a supported test/build boundary. Current COM/Office limitations still apply. |
| Contribution backlog | Review open PRs against these priorities and acceptance evidence; discuss declining secondary/gadget proposals case by case, without prejudging individual PRs. |
| Export quality | Improve edge cases reported in existing issues where rendering is poor or information is lost. Preserve Markdown and Joplin behavior and expose residual risks. |
| Everyday use | Discuss a multilingual graphical interface for everyday users while retaining the CLI for automation. This is not authorization to build the GUI. |
| Guidance for users | Discuss a multilingual website/tutorial covering export through import into Joplin or Obsidian. |
| Languages | Discuss support for languages across all European countries, not just EU members. Agree coverage with maintainers and native-language reviewers; no translations are implemented here. |

## Supported surfaces and limits

Official documentation was checked on **2026-11-21**. Availability depends on
the client version, enabled features, and account/organization policy.

- GitHub.com cloud agent and native code review support repository-wide and
  path-specific instructions. `applyTo` is a glob string (comma-separated for
  several patterns). `excludeAgent` accepts `code-review` or `cloud-agent`;
  it excludes a consumer, not file access or GitHub permissions.
- Native code review reads repository/agent instructions and skills from the
  PR **HEAD branch**, not its base. Review changes to guidance like other
  executable-policy-adjacent content; fork instructions do not grant trust.
- Custom profiles use `.github/agents/*.agent.md` with `description`, `name`,
  and explicit `tools`. Here `target` and `model` are omitted for portability
  and inherited model choice. Tool aliases/capabilities can differ by client;
  confirm the enabled tools before use. The read-only role intentionally has
  no shell, edit, delegation, or GitHub MCP tools. Missing tools do not justify
  silently granting broader access.
- Skills use `.github/skills/<name>/SKILL.md` with `name` and a task-specific
  `description`. GitHub documents skills for cloud agent, code review, CLI,
  app, and supported IDE agent modes. Discovery is relevance-based, not a
  guaranteed invocation. These skills have no scripts or `allowed-tools`
  preapprovals. During review, they are evidence checklists only.
- `.github/prompts/*.prompt.md` files are optional, **manually invoked IDE
  templates**, not GitHub.com review/issue-assignment configuration. This set
  uses only `description` frontmatter and no client-specific tool/model names.
  Current VS Code documentation limits prompt execution to **Local harness**
  sessions; Local is deprecated with removal planned by February 2027. Creating
  a prompt in a Copilot harness session does not make it runnable there.
  Other IDE support is version-dependent. Prefer the skills for reusable
  workflows; use the prompt picker/slash command only where supported, or copy
  the template body into chat with the linked guidance as context.
- IDE instruction support is not identical to native GitHub review.
  `excludeAgent` should not be treated as a universal IDE review-mode switch.
  Read-only prompt wording is also not a capability boundary: choose an
  appropriate read-only client mode/profile and inspect requested tool actions.

## Validation reality and safety

The current application and integration tests target `net10.0-windows7.0` and
use OneNote `COMReference`. The application declares `win-x86`. Plain
`dotnet build` is not a supported build path for these projects (MSB4803).
Windows MSBuild/Visual Studio, compatible SDK/Office tooling, and desktop OneNote
and Word must be verified for any proposed build/export command. Existing
README/release build details are not all current; this proposal does not repair
them or claim a universal working command.

Pandoc is bundled as a ZIP under `src/OneNoteMdExporter/pandoc`; a runtime export
needs an extracted executable. Existing NUnit integration tests launch the
exporter against a live sample notebook; the Joplin image test still throws
`NotImplementedException`. Deterministic COM-free string/XML tests are a goal
for future scoped work, **not an existing standalone suite**.

For a fix, capture minimal synthetic input, faulty and expected output, and
actual before/after results where execution is feasible. Preserve both formats
and meaningful content; never hide partial export errors behind success.
Report commands actually run and checks not run with reasons. There is no
arbitrary overall coverage target. Documentation-only proposals need link,
schema, and consistency checks, not a live Office export.

Never commit real notebooks, personal data, secrets, or private logs. Any live
export needs explicit approval for a disposable synthetic notebook and known
output/temp paths. Protect originals; keep new/modified waits and retries
bounded, clean up owned COM references, and do not kill unrelated Office
processes or delete unknown folders.

Instructions and skills are advisory and cannot enforce merge requirements.
Selecting a role or template does not authorize a commit/push, PR, review
request, issue assignment, merge, policy change, or automation. Request those
actions explicitly. Branch protections and access policies remain separate.
Explicit permission to submit a PR does not replace new-feature acceptance.
AI-assisted changes, including instructions themselves, need independent human
review/validation; this workflow does not use AI-only approval or automatic merging.

## Maintenance

Keep shared rules canonical; change path rules or skills only when their
specialized behavior changes. Before implementation, check existing
contributions and dependency chains rather than duplicating or importing
unmerged work. Update this inventory/support note when files or client support
change. Validate frontmatter, matching globs, relative links, referenced source
paths, and conflicting rules. Review the customization diff before adoption.

Official references:

- [Repository instructions and HEAD-branch behavior](https://docs.github.com/en/copilot/how-tos/copilot-on-github/customize-copilot/add-custom-instructions/add-repository-instructions)
- [Instruction support by surface](https://docs.github.com/en/copilot/reference/custom-instructions-support)
- [Custom agent configuration and tool aliases](https://docs.github.com/en/copilot/reference/custom-agents-configuration)
- [Adding agent skills](https://docs.github.com/en/copilot/how-tos/copilot-on-github/customize-copilot/customize-cloud-agent/add-skills)
- [Prompt files](https://docs.github.com/en/copilot/concepts/prompting-and-context/prompt-files)
- [VS Code prompt files and harness limits](https://code.visualstudio.com/docs/agent-customization/prompt-files)
