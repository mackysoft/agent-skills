# Agent Skills

Agent Skills is a .NET toolkit for product teams that ship agent SKILL packages with their own repositories or CLIs.

It gives a product two pieces:

- a build-time CLI that turns source SKILL definitions into deterministic generated packages;
- a runtime library that lists, exports, installs, updates, uninstalls, prunes, and diagnoses those generated packages for supported agent hosts.

Use this project when a product owns the skill catalog, versioning, and selection rules, but wants shared packaging, host materialization, state analysis, and report contracts.

## Packages

| Package | Use it when |
| --- | --- |
| `MackySoft.AgentSkills.Cli` | A product repository needs to generate canonical packages from `skills/definitions` into `skills/generated`. |
| `MackySoft.AgentSkills` | A product CLI or application needs to read generated packages and run install, update, uninstall, prune, export, list, or doctor workflows. |

Both packages are versioned together.

## How Products Use Agent Skills

A typical integration has this shape:

1. Keep source-authored skill definitions under `skills/definitions`.
2. Run the CLI during development or packaging to regenerate `skills/generated`.
3. Ship or embed `skills/generated` with the product.
4. In the product CLI, use the runtime library to select packages by tier or exact SKILL name.
5. Materialize selected packages into the requested host target, and report the result through the operation report contracts.

The generated directory is build output. Do not edit files under `skills/generated` manually; change the source definition and run the build again.

## Generate Packages

Install the CLI through a .NET tool manifest in each product repository.

```bash
dotnet new tool-manifest
dotnet tool install MackySoft.AgentSkills.Cli --version 0.7.1
```

Create a product-owned source layout.

```text
skills/
  definitions/
    <skill-name>/
      skill.json
      SKILL.md.template
      references/
        *.md.template
```

Generate canonical packages.

```bash
dotnet tool run agent-skills -- build \
  --definitionsRoot skills/definitions \
  --generatedRoot skills/generated
```

The generated layout is deterministic.

```text
skills/
  generated/
    <skill-name>/
      SKILL.md
      agent-skill.json
      agents/
        openai.yaml
      references/
        *.md
```

`agents/openai.yaml` is emitted for the OpenAI / Codex host metadata artifact. Other supported hosts may use only the generated `SKILL.md` frontmatter.

Build options:

- `--definitionsRoot`: directory that contains one subdirectory per source SKILL definition.
- `--generatedRoot`: canonical generated package root, normally `skills/generated`.

The build validates source metadata, validates dependency declarations, computes deterministic digests, writes `agent-skill.json`, and emits host metadata for every supported host that needs a metadata artifact.

## Define a Skill

Each source definition has a `skill.json` file. The JSON property order shown here is canonical and is validated during generation.

```json
{
  "schemaVersion": 1,
  "skillBundleVersion": 1,
  "catalogId": "com.example.skills",
  "tier": "basic",
  "skillName": "example-review",
  "displayName": "Example Review",
  "description": "Review the example product.",
  "dependencies": [],
  "references": []
}
```

The generated `agent-skill.json` preserves `skillBundleVersion`, `catalogId`, `tier`, `skillName`, `displayName`, `description`, and `dependencies`. Its manifest digest covers the canonical manifest content, including those fields.

The generated manifest property order is `schemaVersion`, `skillBundleVersion`, `catalogId`, `tier`, `skillName`, `displayName`, `description`, `dependencies`, `contentDigest`, `manifestDigest`, and `hostArtifacts`.

## Use the Runtime Library

Add the runtime package to the product CLI or application that will install, update, export, list, prune, uninstall, or diagnose generated skills.

```bash
dotnet add <PROJECT>.csproj package MackySoft.AgentSkills --version 0.7.1
```

Equivalent project file reference:

```xml
<PackageReference Include="MackySoft.AgentSkills" Version="0.7.1" />
```

The runtime library reads generated package directories whose canonical manifest file is `agent-skill.json`. Runtime materialization converts those canonical packages into host-specific install or export contents.

Supported host keys:

| Host key | Host | Project target | User target |
| --- | --- | --- | --- |
| `openai` | OpenAI / Codex | `.agents/skills` | `${CODEX_HOME}/skills` or `~/.codex/skills` |
| `claude` | Claude Code | `.claude/skills` | `~/.claude/skills` |
| `copilot` | GitHub Copilot CLI | `.github/skills` | `~/.copilot/skills` |

Product CLIs should parse user-facing literals with `SkillCommandValueParser`, then pass validated domain values into runtime services. Scope and export-format parsing are case-insensitive for CLI input, while package contract literals such as tiers and SKILL names remain exact.

## Select Packages

Agent Skills does not define a fixed tier set. Each product owns the complete set of accepted tier literals, such as `basic`, `advanced`, and `developer`.

Tier literals are stable machine-readable contract values stored in both `skill.json` and `agent-skill.json`. They must be lowercase ASCII letters or digits, may contain `-` after the first character, and are matched exactly. The framework does not trim, case-fold, or map tier labels.

Product CLIs should require at least one explicit selector for operations that materialize, update, uninstall, export, or diagnose skills. A selector can be a tier selection, an exact SKILL name selection, or both. Discovery commands can omit selection by using the full defined tier set.

```csharp
string[] definedTiers = ["basic", "advanced", "developer"];
string[] selectedTiers = ["basic"];

var packagesResult = await packageProvider.GetPackagesAsync(
    definedTiers,
    selectedTiers,
    cancellationToken);
```

`SkillTierLiteralParser.ParseSelectedTiers` validates and normalizes raw CLI literals when a product wants to parse once and pass `IReadOnlyList<SkillTier>` onward.

`SkillPackageProvider.GetPackagesAsync` verifies the product definitions, rejects unsupported selected tiers, rejects generated packages whose manifest tier is not defined by the product, and returns an empty package list when the selected tiers are valid but no bundled package belongs to them.

Use exact SKILL names when a command must target specific packages rather than a whole tier.

```csharp
string[] selectedSkillNames = ["example-review", "example-docs"];

var packagesResult = await packageProvider.GetPackagesBySkillNamesAsync(
    definedTiers,
    selectedSkillNames,
    cancellationToken);
```

`SkillNameLiteralParser.ParseSelectedSkillNames` validates and normalizes raw CLI literals when a product wants to parse name selections before resolving packages. Name filters match `skillName` exactly using ordinal comparison. The provider rejects unknown names and rejects a selected name when it exists but does not belong to the selected tiers.

Use `SkillPackageProvider.GetPackageCatalogAsync` for list-style discovery. The catalog records the normalized selected tiers and selected skill names, returns only matching packages, and includes every product-defined tier with its bundled package count, including valid tiers that currently contain no skills. Report builders consume the catalog directly so `tiers`, `skillNames`, `availableTiers`, and `skills` come from one validated provider result.

## Declare Dependencies

Use `dependencies` when one skill intentionally invokes another skill. Dependencies are exact `skillName` literals from the same generated package set.

```json
{
  "schemaVersion": 1,
  "skillBundleVersion": 1,
  "catalogId": "com.example.skills",
  "tier": "basic",
  "skillName": "example-plan",
  "displayName": "Example Plan",
  "description": "Plan the example workflow before review.",
  "dependencies": [
    "example-review"
  ],
  "references": []
}
```

The build validates dependency consistency before writing generated packages. Every dependency must appear as `$skill-name` in source-authored body content (`SKILL.md.template` or `references/*.md.template`), and every `$skill-name` reference to a known skill in those files must be declared in `dependencies`.

Source metadata such as `skill.json` `description` and generated host artifacts are not dependency-detection inputs. Self-dependencies, duplicate dependencies, missing dependency packages, and dependency cycles are rejected.

Package selection APIs return the selected root packages plus their transitive dependencies. `SelectedTiers` and `SelectedSkillNames` still report the caller's root selection; dependency packages are added only to the returned package list. A dependency can belong to a different defined tier than the selected root package.

## Version Bundled Skills

`skillBundleVersion` is a product-owned positive integer stored in every source `skill.json` and stamped into every generated `agent-skill.json`. A single build must use one `skillBundleVersion` across all source definitions.

`schemaVersion` is the package format version and is currently `1`. `skillBundleVersion` is separate from `schemaVersion`; it is the product's bundled SKILL set version.

Increase `skillBundleVersion` when a product ships a new bundled SKILL set and wants installed clean packages to be recognized as outdated. Runtime state and operation reports include the installed and bundled bundle versions when they are known.

If an installed package has a newer `skillBundleVersion` than the bundled package, update and install flows report a version-ahead target state. Product CLIs can use that state to require an explicit force operation before overwriting the installed package.

## Prune Removed Managed Installs

Use `SkillPruneService` when a product removes or renames a SKILL and wants previously installed managed output cleaned up. Prune is intentionally separate from install and update; expose it as an explicit product CLI option such as `update --prune`, or as a standalone prune command.

Prune must receive the complete current package set for the product catalog, not just the packages selected for the current install or update operation. For example, `update --skill <name> --prune` must not treat every other valid catalog member as removed just because it was outside the user's selector.

`SkillPruneService` only deletes top-level target directories that:

- contain a valid, digest-matched Agent Skills `agent-skill.json`;
- have the requested `catalogId`;
- have a `skillName` that is absent from the complete current catalog package set;
- are materialized for the requested host; and
- are clean according to their own installed manifest.

Prune skips unmanaged directories, foreign catalogs, and current catalog members. Invalid, noncanonical, or digest-mismatched manifests, name collisions, host conflicts, and locally modified managed orphans are reported as blocked actions.

`Force` allows deleting locally modified managed orphans. It does not allow deleting unmanaged directories, foreign catalogs, invalid manifests, name collisions, host conflicts, or path-safety failures.

## Report Results

Runtime services return structured results that product CLIs can turn into product-neutral reports with `SkillOperationReportBuilder`.

Report literals are stable contract values. Enum-backed report literals are formatted through `ContractLiteralCodec`; product command input normalization belongs in command parsing APIs such as `SkillCommandValueParser`, `SkillTierLiteralParser`, and `SkillNameLiteralParser`.
