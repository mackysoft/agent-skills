# Agent Skills

Agent Skills helps product teams ship agent SKILL packages with their own CLI.

Use it when your product owns:

- the skill catalog and release cadence;
- the accepted tier names, such as `basic`, `advanced`, and `developer`;
- the public CLI shape and output envelope.

Agent Skills provides the build tool, package format, host materialization, command runtime, and report data needed to list, export, install, update, uninstall, prune, and diagnose those skills.

## Packages

| Package | Use it when |
| --- | --- |
| `MackySoft.AgentSkills.Cli` | A product repository needs to generate canonical packages from source skill definitions. |
| `MackySoft.AgentSkills` | A product needs the core package, host, install, export, prune, doctor, and report APIs without a hosted command runtime. |
| `MackySoft.AgentSkills.Hosting` | A product CLI wants the standard Agent Skills command runtime and DI registration. |
| `MackySoft.AgentSkills.ConsoleAppFramework` | A ConsoleAppFramework-based product CLI wants the standard `skills` command group registered on its existing builder. |

All packages are versioned together.

## Generate Packages

Install the build tool in the product repository.

```bash
dotnet new tool-manifest
dotnet tool install MackySoft.AgentSkills.Cli --version 0.7.1
```

Keep source-authored definitions under a product-owned directory.

```text
skills/
  definitions/
    <skill-name>/
      skill.json
      SKILL.md.template
      references/
        *.md.template
```

Generate the package directory that your product will ship.

```bash
dotnet tool run agent-skills -- build \
  --definitionsRoot skills/definitions \
  --generatedRoot skills/generated
```

The generated directory is build output. Do not edit it manually; edit the source definition and run the build again.

## Define a Skill

Each source definition has a `skill.json` file.

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

Important fields:

- `catalogId` identifies the product-owned skill catalog. Use the same value when registering the command runtime.
- `tier` must be one of the product-defined tier literals.
- `skillName` is the exact machine name used by command selection and dependencies.
- `skillBundleVersion` is the product's bundled skill-set version. Increase it when shipped skills change.
- `dependencies` contains exact skill names from the same generated package set.

## Add the Command Runtime

Add the hosting package to the product CLI.

```bash
dotnet add <PROJECT>.csproj package MackySoft.AgentSkills.Hosting --version 0.7.1
```

Register the runtime in the product's DI container.

```csharp
using MackySoft.AgentSkills.Hosting.Composition;

services.AddAgentSkillsCommandRuntime(options =>
{
    options.ProductName = "Example CLI";
    options.CatalogId = "com.example.skills";
    options.DefinedTiers = ["basic", "advanced", "developer"];
    options.PackageBaseDirectory = AppContext.BaseDirectory;
    options.CommandRoot = "skills";
});
```

The package base directory must contain the shipped generated packages under `skills/`.

```text
<PackageBaseDirectory>/
  skills/
    <skill-name>/
      SKILL.md
      agent-skill.json
      agents/
      references/
```

## Register ConsoleAppFramework Commands

If the product CLI uses ConsoleAppFramework, add the integration package.

```bash
dotnet add <PROJECT>.csproj package MackySoft.AgentSkills.ConsoleAppFramework --version 0.7.1
```

Register Agent Skills on the product's existing `ConsoleAppBuilder`.

```csharp
using ConsoleAppFramework;
using MackySoft.AgentSkills.ConsoleAppFramework;
using MackySoft.AgentSkills.Hosting.Composition;
using Microsoft.Extensions.Hosting;

HostApplicationBuilder builder = Host.CreateApplicationBuilder();

builder.Services.AddAgentSkillsCommandRuntime(options =>
{
    options.ProductName = "Example CLI";
    options.CatalogId = "com.example.skills";
    options.DefinedTiers = ["basic", "advanced", "developer"];
    options.PackageBaseDirectory = AppContext.BaseDirectory;
    options.CommandRoot = "skills";
});

ConsoleApp.ConsoleAppBuilder app = builder.ToConsoleAppBuilder();

// Register product filters, global options, and product commands as usual.
app.RegisterAgentSkillsCommands();

await app.RunAsync(args);
return Environment.ExitCode;
```

`RegisterAgentSkillsCommands()` adds the Agent Skills command group to the builder. It does not create a builder, run the app, set `ConsoleApp.LogError`, replace the service provider, register filters, or change command validation.

The default command root is `skills`. To expose another root, set the ConsoleAppFramework integration's MSBuild property and keep the runtime option aligned.

```xml
<PropertyGroup>
  <AgentSkillsConsoleAppFrameworkCommandRoot>agent-skills</AgentSkillsConsoleAppFrameworkCommandRoot>
</PropertyGroup>
```

```csharp
services.AddAgentSkillsCommandRuntime(options =>
{
    options.CommandRoot = "agent-skills";
    // Set the other required options here.
});
```

The command root must be one or more lower-kebab command tokens separated by a single space, such as `skills`, `agent-skills`, or `tools skills`.
Command results use dot-separated names, so `tools skills list` is reported as `tools.skills.list`.

If your CLI validates unknown commands before ConsoleAppFramework dispatch, keep that product policy in sync with the configured command root. `AgentSkillsCommandNames` and `AgentSkillsCommandMetadata` provide stable subcommand literals for that purpose.

## Product Responsibilities

The product CLI still owns:

- when generated packages are built and how they are shipped;
- `ProductName`, `CatalogId`, `DefinedTiers`, `PackageBaseDirectory`, and `CommandRoot`;
- the public command surface outside the standard `skills` group;
- pre-dispatch command validation, help policy, filters, global options, and logging;
- the output envelope, if the default JSON result shape is not appropriate.

Register your own `IAgentSkillsCommandResultEmitter` after `AddAgentSkillsCommandRuntime(...)` when the product needs its own JSON envelope or text output.

## Standard Commands

The ConsoleAppFramework integration registers this v1 command group:

```text
skills list
skills export
skills install
skills update
skills uninstall
skills prune
skills doctor
```

`skills list` can omit selectors and then lists every defined tier. Other commands require `--tier`, `--skill`, or both.

Examples:

```bash
example skills list
example skills export --host openai --tier basic --output ./exported-skills
example skills install --host openai --scope project --tier basic
example skills update --host openai --scope project --skill example-review
example skills uninstall --host openai --scope project --skill example-review
example skills prune --host openai --scope project --tier basic
example skills doctor --host openai --scope project --tier basic
```

Common options:

| Option | Applies to | Meaning |
| --- | --- | --- |
| `--host` | export, install, update, uninstall, prune, doctor | Target host key. |
| `--scope` | install, update, uninstall, prune, doctor | `project` or `user`. |
| `--tier` | all commands | Select packages by product-defined tier. |
| `--skill` | all commands | Select exact skill names. |
| `--repository-root` | project scope | Project root. Defaults to the current directory for project scope. |
| `--target-dir` | install, update, uninstall, prune, doctor | Override the host target directory. |
| `--dry-run` | install, update, uninstall, prune | Report planned changes without writing files. |
| `--force` | install, update, uninstall, prune | Allow supported overwrite or delete operations that otherwise require confirmation. |
| `--print-diff` | install, update | Include file diffs in the operation report. |
| `--pretty` | all commands | Indent default JSON output. |

## Supported Hosts

| Host key | Host | Project target | User target |
| --- | --- | --- | --- |
| `openai` | OpenAI / Codex | `.agents/skills` | `${CODEX_HOME}/skills` or `~/.codex/skills` |
| `claude` | Claude Code | `.claude/skills` | `~/.claude/skills` |
| `copilot` | GitHub Copilot CLI | `.github/skills` | `~/.copilot/skills` |

## Prune Removed Skills

Use `skills prune` when a product removes or renames a managed skill and wants old installed output cleaned up.

Prune compares installed managed skills with the complete current catalog for the registered `CatalogId`. A narrow selector such as `--skill example-review` controls the report context and target selection, but prune still uses the full current catalog so other valid catalog members are not treated as removed.

Prune deletes only managed, clean, current-host skill directories that belong to the registered catalog and no longer exist in the current generated package set. It skips unmanaged directories, foreign catalogs, current catalog members, invalid manifests, name collisions, and host conflicts. `--force` allows deleting locally modified managed orphans, but it does not turn unsafe or foreign targets into delete candidates.

`skills update --prune` is intentionally not part of v1. Run `skills prune` explicitly so product CLIs can report update and cleanup as separate operations.
