# Agent Skills

Agent Skills provides reusable .NET services and a CLI tool for product-owned agent skill packages. Product repositories keep their SKILL definitions and generated package outputs, while this repository owns the canonical package format, host materialization rules, install/update/uninstall/export operations, and doctor diagnostics.

## Packages

| Package | Purpose |
| --- | --- |
| `MackySoft.AgentSkills` | Runtime library for reading generated packages, materializing them for supported hosts, and running install/export/doctor workflows. |
| `MackySoft.AgentSkills.Cli` | .NET tool that generates canonical packages from `skills/definitions` into `skills/generated`. |

Both packages are versioned together.

## Product Layout

Each product repository owns its source definitions and generated outputs.

```text
skills/
  definitions/
    <skill-name>/
      skill.json
      SKILL.md.template
      references/
        *.md.template
  generated/
    <skill-name>/
      SKILL.md
      agent-skill.json
      agents/
        openai.yaml
      references/
        *.md
```

`skills/definitions` is the source of truth. `skills/generated` is deterministic build output; do not edit it manually.

Source definition metadata includes a product-owned `skillBundleVersion`, a product-owned `tier` literal, and dependency metadata:

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

Generated package manifests preserve `skillBundleVersion`, `tier`, and `dependencies` in `agent-skill.json`. The manifest digest covers the canonical manifest content, including these fields.

The JSON property order shown above is canonical for `skill.json` and is validated during generation.

## CLI Tool

Install the CLI through a .NET tool manifest in each product repository.

```bash
dotnet new tool-manifest
dotnet tool install MackySoft.AgentSkills.Cli --version 0.7.0
```

Generate canonical packages.

```bash
dotnet tool run agent-skills -- build \
  --definitionsRoot skills/definitions \
  --generatedRoot skills/generated
```

Build options:

- `--definitionsRoot`: directory that contains one subdirectory per source SKILL definition.
- `--generatedRoot`: canonical generated package root, normally `skills/generated`.

The CLI validates definition metadata, computes deterministic digests, writes `agent-skill.json`, and emits host artifact metadata for every supported host.

## Skill Bundle Versions

`skillBundleVersion` is a product-owned positive integer stored in every source `skill.json` and stamped into every generated `agent-skill.json`. A single build must use one `skillBundleVersion` across all source definitions.

`schemaVersion` remains the package format version and is currently `1`. `skillBundleVersion` is separate from `schemaVersion`; it is the product's bundled SKILL set version.

Increase `skillBundleVersion` when a product ships a new bundled SKILL set and wants installed clean packages to be recognized as outdated. Runtime state and operation reports include the installed and bundled bundle versions when they are known. If an installed package has a newer `skillBundleVersion` than the bundled package, update/install flows report a version-ahead target state so callers can require an explicit force operation before overwriting it.

## Tiers

Agent Skills does not define a fixed tier set. Each product owns the complete set of accepted tier literals, such as `basic`, `advanced`, and `developer`.

Tier literals are stable machine-readable contract values stored in `skill.json` and `agent-skill.json`. They must be lowercase ASCII letters or digits, may contain `-` after the first character, and are matched exactly. The framework does not trim, case-fold, or map labels.

Product CLIs should require at least one explicit selector for operations that materialize, update, uninstall, export, or diagnose skills. A selector can be a tier selection, an exact SKILL name selection, or both. Discovery commands can omit selection by using the full defined tier set:

```csharp
string[] definedTiers = ["basic", "advanced", "developer"];
string[] selectedTiers = ["basic"];

var packagesResult = await packageProvider.GetPackagesAsync(
    definedTiers,
    selectedTiers,
    cancellationToken);
```

`SkillTierLiteralParser.ParseSelectedTiers` validates and normalizes raw CLI literals when a product wants to parse once and pass `IReadOnlyList<SkillTier>` onward. `SkillPackageProvider.GetPackagesAsync` verifies the product definitions, rejects unsupported selected tiers, rejects generated packages whose manifest tier is not defined by the product, and returns an empty package list when the selected tiers are valid but no bundled package belongs to them.

## Dependencies

Use `dependencies` when one skill intentionally invokes another skill. Dependencies are exact `skillName` literals from the same generated package set:

```json
{
  "schemaVersion": 1,
  "skillBundleVersion": 1,
  "catalogId": "com.example.skills",
  "tier": "basic",
  "skillName": "example-plan",
  "displayName": "Example Plan",
  "description": "Plan the example workflow, then use $example-review for review.",
  "dependencies": [
    "example-review"
  ],
  "references": []
}
```

The build validates dependency consistency before writing generated packages. Every dependency must appear as `$skill-name` in source-owned text (`description`, `SKILL.md.template`, or `references/*.md.template`), and every `$skill-name` reference to a known skill must be declared in `dependencies`. Self-dependencies, duplicate dependencies, missing dependency packages, and dependency cycles are rejected.

The generated `agent-skill.json` order is `schemaVersion`, `skillBundleVersion`, `catalogId`, `tier`, `skillName`, `displayName`, `description`, `dependencies`, `contentDigest`, `manifestDigest`, and `hostArtifacts`.

Package selection APIs return the selected root packages plus their transitive dependencies. `SelectedTiers` and `SelectedSkillNames` still report the caller's root selection; dependency packages are added only to the returned package list. A dependency can belong to a different defined tier than the selected root package.

Use exact SKILL names when a command must target specific packages rather than a whole tier:

```csharp
string[] selectedSkillNames = ["example-review", "example-docs"];

var packagesResult = await packageProvider.GetPackagesBySkillNamesAsync(
    definedTiers,
    selectedSkillNames,
    cancellationToken);
```

`SkillNameLiteralParser.ParseSelectedSkillNames` validates and normalizes raw CLI literals when a product wants to parse name selections before resolving packages. Name filters match `skillName` exactly using ordinal comparison. The provider rejects unknown names and rejects a selected name when it exists but does not belong to the selected tiers.

Use `SkillPackageProvider.GetPackageCatalogAsync` for list-style discovery. The catalog records the normalized selected tiers and selected skill names, returns only matching packages, and includes every product-defined tier with its bundled package count, including valid tiers that currently contain no skills. Report builders consume the catalog directly so `tiers`, `skillNames`, `availableTiers`, and `skills` come from one validated provider result.

## Runtime Library

Use the library from product CLI or application code that needs to list, export, install, update, uninstall, or diagnose generated agent skills.

```bash
dotnet add <PROJECT>.csproj package MackySoft.AgentSkills --version 0.7.0
```

Equivalent project file reference:

```xml
<PackageReference Include="MackySoft.AgentSkills" Version="0.7.0" />
```

The canonical package manifest file is `agent-skill.json`. Generated package directories are canonical package inputs; runtime materialization converts those packages into host-specific install/export contents.

Supported host keys:

- `openai`
- `claude`
- `copilot`
