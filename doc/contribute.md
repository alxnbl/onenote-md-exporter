# Contribution policy

## Principles and priorities

Keep OneNote Md Exporter free and open source under [GPL version 3](../LICENSE).
Preserve the existing license, copyright, and third-party attribution notices.
Check that new dependencies or reused code have compatible licensing; raise
unresolved questions for maintainer review rather than claiming legal assurance.

Favor a simple architecture and code a human can understand. Reliability,
cybersecurity, and simplicity come before secondary features. Prioritize bug
fixes and export fidelity: better rendering and less lost information.
Assess optional features by user benefit, accessibility, security/operational
cost, and maintenance complexity, not novelty alone.

Make the tool usable by nontechnical and multilingual users. Preserve offline
operation, safe defaults, and both Markdown and Joplin exports. Use clear,
localized, actionable warnings about limitations and partial failures. Protect
original notebooks and meaningful content; never hide loss behind success.
Advise users to keep a backup and check the exported content before migration.
Loss prevention is the goal, not a guarantee that current exports are lossless.

## Propose new features before implementation

Before submitting an implementation PR for a **new feature**, open or reuse a
relevant [feature request](../.github/ISSUE_TEMPLATE/feature_request.md) and obtain
**explicit maintainer acceptance** of its scope. Link that request and the
acceptance in the PR. An issue's existence, a roadmap entry, an AI suggestion,
or silence is not acceptance. Discuss the proposal first rather than spending
effort on an unaccepted implementation.

Ordinary bug/regression fixes and export-quality improvements do not require a
new feature request or a second feature approval. Check relevant existing issues,
PRs, and dependencies to avoid duplicate work. If a claimed fix adds capabilities
or changes defaults, identify that product decision for the maintainer; apply
the new-feature gate when it is feature work. Do not import unmerged work without
approval or prejudge individual open PRs.

The [roadmap directions](copilot.md#roadmap-for-discussion) are candidates for
discussion and acceptance, not autonomous assignments or approved features.

## Human-reviewable contributions

AI-assisted contributions must be bounded, understandable, and independently
reviewed and validated by a human, including generated tests and instruction
changes. Explain the behavior change, expected/observed output, actual checks
and limitations, and relevant security/licensing tradeoffs. Minimize unrelated
diffs and dependencies. AI-only approval or automatic merging is not a substitute
for human validation; this policy does not configure GitHub merge controls.

Include a minimal synthetic or anonymized reproduction with your pull
request so the change can be tested. Never attach real notebooks, personal
information, or private logs.

# GitHub Copilot guidance

See the [versioned Copilot guidance proposal](copilot.md) for repository
instructions, focused agents, reusable skills, manual prompt templates, and
current validation and client-support limits.

# Add a new export setting

For feature work, complete the feature-request acceptance process above first.

- Add the setting in [AppSettings.cs](/src/OneNoteMdExporter/Infrastructure/AppSettings.cs) with a description
- Add the setting in [appSettings.json](/src/OneNoteMdExporter/appSettings.json) with a description
- Use your settings in ExportServiceBase.cs / JoplinExportService or MdExportService using the `AppSettings` class

# Add or improve a translation

Discuss new language support through the feature-request process above; fixes
to existing translations do not require a new feature request.

- Fork the repository.
- Use [the English resource](../src/OneNoteMdExporter/Resources/trad.en.json)
  as a reference. Resource names use a two-letter language code from the user's
  .NET culture, not a country code; keep translation keys and placeholders.
- Save the resource as `trad.<TwoLetterLanguageCode>.json` in
  `src/OneNoteMdExporter/Resources`, and include it in the project consistently
  with existing translations when implementing accepted language support.
- Submit a focused PR referencing the accepted request when adding a language.
  Have a human familiar with that language review the wording, especially
  warnings and recovery steps.

# Add a new export format

Complete feature-request acceptance above before submitting its implementation
PR; a roadmap direction is not approval.

## Getting started

[ExportServiceBase.cs](/src/OneNoteMdExporter/Services/Export/ExportServiceBase.cs) is an Abstract class that implements the shared logic for exporting a Notebook.

## Implement a new export service

You needs to inherit from [ExportServiceBase.cs](/src/OneNoteMdExporter/Services/Export/ExportServiceBase.cs) and implement the abstract methods. You can take inspiration from [JoplinExportService.cs](/src/OneNoteMdExporter/Services/Export/JoplinExportService.cs) and [MdExportService.cs](/src/OneNoteMdExporter/Services/Export/MdExportService.cs).

## Integrate the new export service

- Update [ExportServiceFactory.cs](/src/OneNoteMdExporter/Infrastructure/ExportServiceFactory.cs) to register your new exporter