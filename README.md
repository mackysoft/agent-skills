# Agent Skills

Agent Skills helps product teams ship agent SKILL packages with their own CLI.

Use it when your product owns:

- the skill catalog and release cadence;
- the category names represented by source definition directories;
- the public CLI shape and output envelope.

Agent Skills provides the build tool, package format, host materialization, command runtime, and report data needed to list, export, install, update, uninstall, prune, and diagnose those skills.

## Packages

| Package | Use it when |
| --- | --- |
| `MackySoft.AgentSkills.Cli` | A product repository needs to build canonical packages, or a user wants to operate the Agent Skills catalog shipped by this repository. |
| `MackySoft.AgentSkills` | A product needs the core package, host, install, export, prune, doctor, and report APIs without a hosted command runtime. |
| `MackySoft.AgentSkills.Hosting` | A product CLI wants the standard Agent Skills command runtime and DI registration. |
| `MackySoft.AgentSkills.ConsoleAppFramework` | A ConsoleAppFramework-based product CLI wants Agent Skills commands registered on its existing builder. |

All packages are versioned together.

## Create Skill Packages

Agent Skills separates skill source files from generated packages. Keep the source files in the product repository and ship the generated package directory with the product CLI.

A source bundle and its generated output use this fixed layout:

```text
skills/
  bundle.json
  definitions/
    <category>/
      <skill-name>/
        skill.json
        SKILL.md.template
        references/
          *.md.template
  generated/
    bundle.json
    <skill-name>/
      agent-skill.json
      SKILL.md
      agents/
      references/
```

### Define Source Skills

Define the catalog and release version once in `bundle.json`.

```json
{
  "schemaVersion": 1,
  "catalogId": "com.example.skills",
  "skillBundleVersion": 1
}
```

Each source definition has a `skill.json` file under exactly one category directory.

```json
{
  "schemaVersion": 1,
  "displayName": "Example Review",
  "description": "Review the example product.",
  "dependencies": []
}
```

Important fields:

- `catalogId` identifies the product-owned skill catalog.
- `skillBundleVersion` is the product's bundled skill-set version. The build command advances it when source changes require new generated output. A manually advanced value is accepted only when it is exactly one greater than the current generated version and the source content changed.
- The category directory name is the skill's category and must be a lowercase hyphenated literal.
- The skill directory name is the exact machine name used by command selection and dependencies.
- `dependencies` contains exact skill names from the same generated package set.
- Reference names are derived from regular `references/*.md.template` files directly under the skill directory. Do not duplicate them in `skill.json`.

### Generate Shipped Packages

Install the build tool in the product repository.

```bash
dotnet new tool-manifest
dotnet tool install MackySoft.AgentSkills.Cli --version 1.0.0
```

Generate the package root from the source definitions.

```bash
dotnet tool run agent-skills -- build --root skills
```

The command reads `bundle.json` and `definitions` under the bundle root and replaces its `generated` directory. Do not edit generated files manually; edit the source bundle and run the build again. When packaging the product CLI, ship the generated directory as `<PackageBaseDirectory>/skills`.

When generated output already matches the source definition and bundle version, the command does not write any files. To verify committed output without changing the working tree, use:

```bash
dotnet tool run agent-skills -- build --root skills --check
```

The repository provides separate `verify` and `sync` composite GitHub Actions. Each Action accepts only `root`, a bundle root relative to the GitHub workspace that resolves inside the checked-out Git worktree. Both restore the CLI version pinned by the caller's .NET tool manifest.

Use `verify` for pull requests and other read-only checks. It runs `build --check`, fails when committed output is stale, and never generates or commits files.

```yaml
- name: Checkout
  uses: actions/checkout@v5

- name: Verify Agent Skills
  uses: mackysoft/agent-skills/actions/verify@1.0.0
  with:
    root: skills
```

Use `sync` only from a branch workflow with `contents: write`. When reconciliation is required, it requires a clean Git index, updates the source bundle and generated output, stages only `<root>/bundle.json` and `<root>/generated`, creates a `github-actions[bot]` commit, and pushes that commit to the current branch. Its `changed` output is `true` only after that push succeeds.

```yaml
permissions:
  contents: write

steps:
  - name: Checkout
    uses: actions/checkout@v5

  - name: Sync Agent Skills
    id: agent-skills
    uses: mackysoft/agent-skills/actions/sync@1.0.0
    with:
      root: skills
```

Pushes made with the default `GITHUB_TOKEN` do not trigger another workflow run. If the caller supplies credentials that do trigger workflows, the synchronized bundle makes the next run a no-op because `build --check` passes. Branch protection still applies; use `verify` when direct bot pushes are not permitted.

## Add Agent Skills to a Product CLI

Use the hosting package when the product CLI wants standard Agent Skills command behavior, report data, and DI registration.

### Command Runtime

Add the hosting package to the product CLI.

```bash
dotnet add <PROJECT>.csproj package MackySoft.AgentSkills.Hosting --version 1.0.0
```

Register the runtime in the product's DI container.

```csharp
using MackySoft.AgentSkills.Hosting.Composition;

services.AddAgentSkillsCommandRuntime(options =>
{
    options.ProductName = "Example CLI";
    options.PackageBaseDirectory = AppContext.BaseDirectory;
    options.CommandRoot = "skills";
});
```

The package base directory must contain the shipped generated packages under `skills/`.

```text
<PackageBaseDirectory>/
  skills/
    bundle.json
    <skill-name>/
      SKILL.md
      agent-skill.json
      agents/
      references/
```

The runtime reads the catalog identity and available categories from the generated bundle descriptor and package manifests. Categories are not configured separately in product code.

Project-scope commands use the current directory when `--repository-root` is omitted. If the product CLI already has a repository-root policy, set `RepositoryRootResolver` to keep Agent Skills commands aligned with it.

```csharp
services.AddAgentSkillsCommandRuntime(options =>
{
    options.RepositoryRootResolver = currentDirectory =>
        ProductRepositoryResolver.Resolve(currentDirectory);
    // Set the required options shown above.
});
```

### ConsoleAppFramework Integration

Use the ConsoleAppFramework integration when the product CLI already uses ConsoleAppFramework and wants Agent Skills to add the standard command group to the existing app builder.

Add the integration package to the product CLI.

```bash
dotnet add <PROJECT>.csproj package MackySoft.AgentSkills.ConsoleAppFramework --version 1.0.0
dotnet add <PROJECT>.csproj package Microsoft.Extensions.Hosting
```

Register Agent Skills on the product's existing `ConsoleAppBuilder`. The product still creates and runs the builder.

```csharp
using ConsoleAppFramework;
using MackySoft.AgentSkills.ConsoleAppFramework;
using MackySoft.AgentSkills.Hosting.Composition;
using Microsoft.Extensions.Hosting;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Services.AddAgentSkillsCommandRuntime(options =>
{
    options.ProductName = "Example CLI";
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

The command examples in this README use ConsoleAppFramework's default kebab-case option names. If the product exposes different public option names, keep that compatibility in the product CLI before ConsoleAppFramework dispatch.

### Product Responsibilities

The product CLI still owns:

- when generated packages are built and how they are shipped;
- the source `bundle.json`, category directories, and skill definitions;
- `ProductName`, `PackageBaseDirectory`, `CommandRoot`, and the default repository-root policy;
- the public command surface outside the configured Agent Skills command root;
- pre-dispatch command validation, option-name compatibility, help policy, filters, global options, and logging;
- the output envelope, if the default JSON result shape is not appropriate.

Register your own `IAgentSkillsCommandResultEmitter` after `AddAgentSkillsCommandRuntime(...)` when the product needs its own JSON envelope or text output.

## Run Standard Commands

The ConsoleAppFramework integration registers these commands under the configured command root. With the default root, the commands are:

```text
skills list
skills export
skills install
skills update
skills uninstall
skills prune
skills doctor
```

The standalone `MackySoft.AgentSkills.Cli` is the top-level composition root for the same command adapter and ships this repository's generated `basic/agent-skills-packaging` skill. Its standard commands are registered at the process root because the executable name already supplies the command root:

```bash
dotnet tool run agent-skills -- list --pretty
dotnet tool run agent-skills -- install --host openai --scope project --category basic --dryRun --pretty
```

The standalone executable preserves exact C# parameter names for multiword options: `--repositoryRoot`, `--targetDir`, `--dryRun`, and `--printDiff`. The product CLI examples and common-options table below use ConsoleAppFramework's default kebab-case conversion instead.

`skills list` can omit selectors and then lists every bundled category. Other commands require `--category`, `--skill`, or both.

### Examples

```bash
example skills list
example skills export --host openai --category core --output ./exported-skills
example skills install --host openai --scope project --category core
example skills update --host openai --scope project --skill example-review
example skills uninstall --host openai --scope project --skill example-review
example skills prune --host openai --scope project --category core
example skills doctor --host openai --scope project --category core
```

### Common Options

| Option | Applies to | Meaning |
| --- | --- | --- |
| `--host` | export, install, update, uninstall, prune, doctor | Target host literal: `claude`, `copilot`, or `openai`. |
| `--scope` | install, update, uninstall, prune, doctor | `project` or `user`. |
| `--category` | all commands | Select packages by bundled category. |
| `--skill` | all commands | Select exact skill names. |
| `--repository-root` | project scope | Project root. Defaults to the configured repository-root resolver for project scope. |
| `--target-dir` | install, update, uninstall, prune, doctor | Override the host target directory. |
| `--dry-run` | install, update, uninstall, prune | Report planned changes without writing files. |
| `--force` | install, update, uninstall, prune | Allow supported overwrite or delete operations that otherwise require confirmation. |
| `--print-diff` | install, update | Include file diffs in the operation report. |
| `--pretty` | all commands | Indent default JSON output. |

### Supported Hosts

| Host literal | Host | Project target | User target |
| --- | --- | --- | --- |
| `openai` | OpenAI / Codex | `.agents/skills` | `${CODEX_HOME}/skills` or `~/.codex/skills` |
| `claude` | Claude Code | `.claude/skills` | `~/.claude/skills` |
| `copilot` | GitHub Copilot CLI | `.github/skills` | `~/.copilot/skills` |

### Prune Removed Skills

Use `skills prune` when a product removes or renames a managed skill and wants old installed output cleaned up.

Prune compares installed managed skills with the complete current catalog identified by the bundled `catalogId`. A narrow selector limits the installed target directories that prune considers, but prune still reads the full current catalog so valid catalog members are not treated as removed.

`--skill` can name a managed skill that was removed from the current generated package set. `--category` selects installed managed targets whose manifest has that category.

Prune deletes only managed, clean, current-host skill directories that belong to the bundled catalog and no longer exist in the current generated package set. It skips unmanaged directories, foreign catalogs, current catalog members, invalid manifests, name collisions, and host conflicts. `--force` allows deleting locally modified managed orphans, but it does not turn unsafe or foreign targets into delete candidates.

`skills update --prune` is not part of the command set. Run `skills prune` explicitly so product CLIs can report update and cleanup as separate operations.
