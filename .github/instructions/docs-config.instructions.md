---
applyTo: "**/*.md,.github/**/*.yml,.github/**/*.yaml,src/OneNoteMdExporter/appSettings.json,src/OneNoteMdExporter/Resources/trad.*.json"
---

# Documentation and configuration

- Write English documentation by default; retain each translation file's
  language. Keep option names, enum values, translation keys, and format
  placeholders consistent with the source.
- Keep user-facing explanations accessible to nontechnical, multilingual users.
  Explain limitations, backup, partial failures, and safe next actions clearly.
  Preserve GPL version 3 and third-party attribution notices; do not claim
  licensing compliance without evidence or rewrite existing license text.
- `appSettings.json` intentionally contains comments. Do not rewrite it as strict
  JSON or synchronize its values with C# initializers as unrelated cleanup.
- Describe actual build/runtime prerequisites and validation limitations.
  Historical README/release details may be stale: verify claims against the
  current project files, and do not present an unrun command as known to work.
- Keep Copilot customizations small: shared policy belongs in
  `.github/copilot-instructions.md`, focused rules in path-specific instructions,
  reusable task procedures in skills, and optional manual templates in prompts.
  Use supported frontmatter and update `doc/copilot.md` when behavior or support
  changes. Do not add CI/setup workflows as a side effect.
- Keep the feature-acceptance policy canonical in `doc/contribute.md` and roadmap
  directions in `doc/copilot.md`. A roadmap entry is not feature acceptance;
  policy/instruction changes themselves need human review and validation.
- Check relative links, repository paths, and current official documentation.
  State consumer/version limitations; instructions are advisory, not enforced
  merge requirements. Documentation-only changes need no live Office export.
