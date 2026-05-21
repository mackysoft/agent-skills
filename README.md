# Agent Skills

Agent Skills provides reusable .NET services and a CLI tool for product-owned agent skill packages. Product repositories keep their SKILL definitions and generated package outputs, while this repository owns the canonical package format, host materialization rules, install/update/uninstall/export operations, and doctor diagnostics.

## Packages

| Package | Purpose |
| --- | --- |
| `MackySoft.AgentSkills` | Runtime library for reading generated packages, materializing them for supported hosts, and running install/export/doctor workflows. |
| `MackySoft.AgentSkills.Cli` | .NET tool that generates canonical packages from `skills/definitions` into `skills/generated`. |

Both packages are versioned together. The NuGet version is read from `Directory.Build.props`, and version changes must be reviewed as normal pull requests before publishing.

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

## CLI Tool

Install the CLI through a .NET tool manifest in each product repository.

```bash
dotnet new tool-manifest
dotnet tool install MackySoft.AgentSkills.Cli --version 0.2.0
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

## Runtime Library

Use the library from product CLI or application code that needs to list, export, install, update, uninstall, or diagnose generated agent skills.

```bash
dotnet add <PROJECT>.csproj package MackySoft.AgentSkills --version 0.2.0
```

Equivalent project file reference:

```xml
<PackageReference Include="MackySoft.AgentSkills" Version="0.2.0" />
```

The canonical package manifest file is `agent-skill.json`. Generated package directories are canonical package inputs; runtime materialization converts those packages into host-specific install/export contents.

Supported host keys:

- `openai`
- `claude`
- `copilot`

## Release

Publishing is performed by the `nuget-package` GitHub Actions workflow. The package version is declared in `Directory.Build.props`, and the release tag must use the same SemVer value without a leading `v`. To publish a new version:

1. Update `<Version>` in `Directory.Build.props` and update package usage examples.
2. Open and merge a pull request for that version change into the default branch.
3. Create and push the matching release tag from the default branch commit.
4. Update the GitHub Release notes after the workflow mirrors the published packages.

The workflow publishes both `MackySoft.AgentSkills` and `MackySoft.AgentSkills.Cli` for the same version, waits until both packages are available on nuget.org, and mirrors the published package artifacts to the GitHub Release.
