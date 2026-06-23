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

Source definition metadata includes a product-owned `tier` literal:

```json
{
  "schemaVersion": 1,
  "skillName": "example-review",
  "displayName": "Example Review",
  "description": "Review the example product.",
  "tier": "basic",
  "references": []
}
```

Generated package manifests preserve that value in `agent-skill.json`. The manifest digest covers the canonical manifest content, including `tier`.

## CLI Tool

Install the CLI through a .NET tool manifest in each product repository.

```bash
dotnet new tool-manifest
dotnet tool install MackySoft.AgentSkills.Cli --version 0.3.0
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

## Tiers

Agent Skills does not define a fixed tier set. Each product owns the complete set of accepted tier literals, such as `basic`, `advanced`, and `developer`.

Tier literals are stable machine-readable contract values stored in `skill.json` and `agent-skill.json`. They must be lowercase ASCII letters or digits, may contain `-` after the first character, and are matched exactly. The framework does not trim, case-fold, or map labels.

Product CLIs should require tier selection for operations that materialize, update, uninstall, export, or diagnose skills. Discovery commands can omit selection by using the full defined tier set:

```csharp
string[] definedTiers = ["basic", "advanced", "developer"];
string[] selectedTiers = ["basic"];

var packagesResult = await packageProvider.GetPackagesAsync(
    definedTiers,
    selectedTiers,
    cancellationToken);
```

`SkillTierLiteralParser.ParseSelectedTiers` validates and normalizes raw CLI literals when a product wants to parse once and pass `IReadOnlyList<SkillTier>` onward. `SkillPackageProvider.GetPackagesAsync` verifies the product definitions, rejects unsupported selected tiers, rejects generated packages whose manifest tier is not defined by the product, and returns an empty package list when the selected tiers are valid but no bundled package belongs to them.

Use `SkillPackageProvider.GetPackageCatalogAsync` for list-style discovery. The catalog records the normalized selected tiers, returns only matching packages, and includes every product-defined tier with its bundled package count, including valid tiers that currently contain no skills. Report builders consume the catalog directly so `tiers`, `availableTiers`, and `skills` come from one validated provider result.

## Runtime Library

Use the library from product CLI or application code that needs to list, export, install, update, uninstall, or diagnose generated agent skills.

```bash
dotnet add <PROJECT>.csproj package MackySoft.AgentSkills --version 0.3.0
```

Equivalent project file reference:

```xml
<PackageReference Include="MackySoft.AgentSkills" Version="0.3.0" />
```

The canonical package manifest file is `agent-skill.json`. Generated package directories are canonical package inputs; runtime materialization converts those packages into host-specific install/export contents.

Supported host keys:

- `openai`
- `claude`
- `copilot`
