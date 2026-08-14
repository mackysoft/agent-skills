# Agent Distribution

Agent Distribution helps product teams ship skill packages and host-specific custom-agent artifacts with their own CLI.

Use it when your product owns:

- the skill catalog and release cadence;
- the skill category names represented by source definition directories;
- the public CLI shape and output envelope.

Agent Distribution provides the build tool, package formats, dependency resolution, host materialization, command runtime, and report data needed to list, export, install, update, uninstall, prune, and diagnose skills and custom agents.

## Packages

| Package | Use it when |
| --- | --- |
| `MackySoft.AgentDistribution.Cli` | A product repository needs to build canonical packages, or a user wants to operate the Agent Distribution catalog shipped by this repository. |
| `MackySoft.AgentDistribution` | A product needs the core package, host, install, export, prune, doctor, and report APIs without a hosted command runtime. |
| `MackySoft.AgentDistribution.Hosting` | A product CLI wants the standard Agent Distribution command runtime and DI registration. |
| `MackySoft.AgentDistribution.ConsoleAppFramework` | A ConsoleAppFramework-based product CLI wants Agent Distribution commands registered on its existing builder. |

All packages are versioned together.

Version `3.0.0` is the first release under the Agent Distribution identity. Replace the corresponding `MackySoft.AgentSkills`, `MackySoft.AgentSkills.Cli`, `MackySoft.AgentSkills.Hosting`, and `MackySoft.AgentSkills.ConsoleAppFramework` package references with the package IDs above. The new packages do not provide namespace, assembly, command, or state-path aliases for the previous identity.

The core package uses [`MackySoft.FileSystem`](https://github.com/mackysoft/dotnet-foundations/tree/master/src/MackySoft.FileSystem) for typed lexical paths, physical entry inspection, physical containment resolution, and atomic single-file publication. It uses [`MackySoft.Text.Vocabularies`](https://github.com/mackysoft/dotnet-foundations/tree/master/src/MackySoft.Text.Vocabularies) for stable public literals. Agent Distribution retains only product-specific path policies, failure mapping, deterministic package formats, and multi-file transactions.

## Create Distribution Bundles

Agent Distribution separates authored definitions from canonical runtime packages. Source schema `4` can contain skills, custom agents, or both. Skills and agents have separate namespaces; the only distribution dependency direction is Agent to Skill.

### Define the Source Layout

Create this fixed layout in the product repository:

```text
agent-distribution/
  bundle.json
  skills/
    <category>/<skill-name>/
      skill.json
      SKILL.md.template
      references/
      scripts/
  agents/
    <agent-name>/
      agent.json
      AGENT.md.template
      hosts/
        codex.json
        claude-code.json
        github-copilot.json
```

Omit `skills` or `agents` when the catalog does not define that artifact kind. Each directory that is present contains at least one definition. Source schema `4` accepts no other root entries.

Create `bundle.json` at the source root. One `bundleVersion` covers both package kinds:

```json
{
  "schemaVersion": 4,
  "catalogId": "com.example.agent-assets",
  "bundleVersion": 1
}
```

`catalogId` is the stable identity shared by the source, canonical packages, and managed installations. `bundleVersion` identifies one complete canonical bundle.

### Define Skills and Custom Agents

Create a skill at `skills/<category>/<skill-name>` and a custom agent at `agents/<agent-name>`. Category, skill name, and agent name come from their directory names.

`skill.json` defines the skill's display metadata and same-bundle dependencies. Its current schema remains `1`:

```json
{
  "schemaVersion": 1,
  "displayName": "Example Review",
  "description": "Review a completed example.",
  "dependencies": []
}
```

`agent.json` defines host-independent metadata and direct skill dependencies:

```json
{
  "schemaVersion": 1,
  "displayName": "Architect",
  "description": "Creates an implementation-ready design.",
  "skillDependencies": ["claim-grounding"]
}
```

`AGENT.md.template` is host-independent. Host bindings contain only the settings owned by one execution host. A custom agent may define any non-empty subset of `codex.json`, `claude-code.json`, and `github-copilot.json`; every binding field other than `schemaVersion` is optional.

Use the [skill source definition contract](agent-distribution/skills/basic/agent-distribution-packaging/references/source-definition-contract.md.template) for the complete skill layout, naming, dependency, content, and encoding rules.

`scripts/**` is optional host-independent text content. Each script is published at the same relative path in the canonical package and every host materialization, and participates in package and bundle integrity checks. Agent Distribution does not execute scripts or read and preserve executable permissions; interpreter selection and execution remain the responsibility of a separate runner.

### Build Canonical Packages

Install the build tool in the product repository:

```bash
dotnet new tool-manifest
dotnet tool install MackySoft.AgentDistribution.Cli --version 6.1.0
```

Build source into a separate artifact root. The output directory name is always `agent-distribution`:

```bash
dotnet tool run agent-distribution -- build \
  --source agent-distribution \
  --output artifacts/agent-distribution
```

The command reads the source root and publishes the canonical bundle below the explicit output root. Do not edit `artifacts/agent-distribution` manually or commit it. Before packing a product CLI, run the build in a separate process and include `artifacts/agent-distribution/**/*` in the package as `agent-distribution/`.

`build --check` verifies an existing explicit output without writing it. The build always preserves the version authored in `bundle.json`; release preparation updates only that source descriptor.

The canonical bundle contains `bundle.json`, `skills/<skill-name>/...`, and `agents/<agent-name>/...`. The generator owns every canonical file, including manifests and digests. It validates the complete source, dependency graph, host bindings, file sets, and digests before replacing the output. Repeating a build from the same source produces the same bytes.

### Verify Source Builds in GitHub Actions

The `verify` composite Action builds its bundled CLI source into `${RUNNER_TEMP}/agent-distribution` from a fresh checkout. It does not compare with or commit generated output.

```yaml
- name: Checkout
  uses: actions/checkout@v5

- name: Verify Agent Distribution source
  uses: mackysoft/agent-distribution/actions/verify@6.1.0
  with:
    source: agent-distribution
```

## Add Agent Distribution to a Product CLI

Use the hosting package when the product CLI wants standard Agent Distribution command behavior, report data, and DI registration.

### Command Runtime

Add the hosting package to the product CLI.

```bash
dotnet add <PROJECT>.csproj package MackySoft.AgentDistribution.Hosting --version 6.1.0
```

Register the runtime in the product's DI container.

```csharp
using MackySoft.AgentDistribution.Hosting.Composition;
using MackySoft.FileSystem;

services.AddAgentDistributionCommandRuntime(options =>
{
    options.ProductName = "Example CLI";
    options.PackageBaseDirectory = AbsolutePath.Parse(AppContext.BaseDirectory);
});
```

The package base directory must contain the shipped generated bundle at `<PackageBaseDirectory>/agent-distribution/`. A schema `1` bundle uses that physical root as the direct container for skill packages. A schema `3` bundle places `bundle.json`, `skills/`, and `agents/` directly below that physical root.

```text
<PackageBaseDirectory>/
  agent-distribution/
    bundle.json
    skills/<skill-name>/...
    agents/<agent-name>/...
```

The runtime reads the catalog identity, available skill categories, and agent names from the generated bundle descriptor and package manifests. Skill categories are not configured separately in product code.

Project-scope commands use the current directory when `--repository-root` is omitted. If the product CLI already has a repository-root policy, set `RepositoryRootResolver` to keep Agent Distribution commands aligned with it.

```csharp
services.AddAgentDistributionCommandRuntime(options =>
{
    options.RepositoryRootResolver = currentDirectory =>
        AbsolutePath.Parse(ProductRepositoryResolver.Resolve(currentDirectory.Value));
    // Set the required options shown above.
});
```

### ConsoleAppFramework Integration

Use the ConsoleAppFramework integration when the product CLI already uses ConsoleAppFramework and wants Agent Distribution to add the standard command group to the existing app builder.

Add the integration package to the product CLI.

```bash
dotnet add <PROJECT>.csproj package MackySoft.AgentDistribution.ConsoleAppFramework --version 6.1.0
dotnet add <PROJECT>.csproj package Microsoft.Extensions.Hosting
```

Register Agent Distribution on the product's existing `ConsoleAppBuilder`. The product still creates and runs the builder.

```csharp
using ConsoleAppFramework;
using MackySoft.AgentDistribution.ConsoleAppFramework;
using MackySoft.AgentDistribution.Hosting.Composition;
using MackySoft.FileSystem;
using Microsoft.Extensions.Hosting;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Services.AddAgentDistributionCommandRuntime(options =>
{
    options.ProductName = "Example CLI";
    options.PackageBaseDirectory = AbsolutePath.Parse(AppContext.BaseDirectory);
});

ConsoleApp.ConsoleAppBuilder app = builder.ToConsoleAppBuilder();

// Register product filters, global options, and product commands as usual.
app.RegisterAgentDistributionCommands();

await app.RunAsync(args);
return Environment.ExitCode;
```

`RegisterAgentDistributionCommands()` adds the fixed, sibling `skills` and `agents` resource groups to the product's command root. It does not own the product executable name, add an extra parent group, create a builder, run the app, set `ConsoleApp.LogError`, replace the service provider, register filters, or change command validation. Command results use the resource path, such as `skills.list` or `agents.list`.

The command examples in this README use ConsoleAppFramework's default lower-kebab-case option names.

### Product Responsibilities

The product CLI still owns:

- when generated packages are built and how they are shipped;
- the source `bundle.json`, skill category directories, skill definitions, and agent definitions;
- `ProductName`, `PackageBaseDirectory`, and the default repository-root policy;
- the public command surface outside the fixed `skills` and `agents` groups;
- pre-dispatch command validation, help policy, filters, global options, and logging;
- the output envelope, if the default JSON result shape is not appropriate.

Register your own `IAgentDistributionCommandResultEmitter` after `AddAgentDistributionCommandRuntime(...)` when the product needs its own JSON envelope or text output.

## Run Standard Commands

The ConsoleAppFramework integration registers these resource groups at the product's command root:

```text
skills list
skills export
skills install
skills update
skills uninstall
skills prune
skills doctor

agents list
agents export
agents install
agents update
agents uninstall
agents prune
agents doctor
```

The standalone `MackySoft.AgentDistribution.Cli` is the top-level composition root for the same command adapters and ships this repository's generated `basic/agent-distribution-packaging` skill. The executable name supplies `agent-distribution`; the resource group remains explicit:

```bash
dotnet tool run agent-distribution -- skills list --pretty
dotnet tool run agent-distribution -- skills install --host codex --scope project --category basic --dry-run --pretty
dotnet tool run agent-distribution -- agents list --pretty
dotnet tool run agent-distribution -- agents install --host claude-code --scope project --agent architect --dry-run --pretty
```

The standalone executable and product integration both use lower-kebab-case option names.

`skills list` can omit selectors and then lists every bundled skill category. Other skill commands require `--category`, `--skill`, or both. `agents list` can omit its selector and then lists every bundled custom agent; other custom-agent commands require `--agent`.

### Examples

```bash
example skills list
example skills export --host codex --category core --output ./exported-skills
example skills install --host codex --scope project --category core
example skills update --host codex --scope project --skill example-review
example skills uninstall --host codex --scope project --skill example-review
example skills prune --host codex --scope project --category core
example skills doctor --host codex --scope project --category core

example agents list
example agents export --host github-copilot --agent architect --output ./exported-agent-assets
example agents install --host github-copilot --scope project --agent architect
example agents update --host github-copilot --scope project --agent architect
example agents uninstall --host github-copilot --scope project --agent architect
example agents prune --host github-copilot --scope project --agent retired-agent
example agents doctor --host github-copilot --scope project --agent architect
```

### Skill Command Options

| Option | Applies to | Meaning |
| --- | --- | --- |
| `--host` | export, install, update, uninstall, prune, doctor | Target host literal: `codex`, `claude-code`, or `github-copilot`. |
| `--scope` | install, update, uninstall, prune, doctor | `project` or `user`. |
| `--category` | all commands | Select packages by bundled category. |
| `--skill` | all commands | Select exact skill names. |
| `--repository-root` | project scope | Project root. Defaults to the configured repository-root resolver for project scope. |
| `--target-dir` | install, update, uninstall, prune, doctor | Use an exact bundle target directory instead of the host default. |
| `--dry-run` | install, update, uninstall, prune | Report planned changes without writing files. |
| `--force` | install, update, uninstall, prune | Allow supported overwrite or delete operations that otherwise require confirmation. |
| `--print-diff` | install, update | Include file diffs in the operation report. |
| `--pretty` | all commands | Indent default JSON output. |

### Custom-Agent Command Options

| Option | Applies to | Meaning |
| --- | --- | --- |
| `--host` | export, install, update, uninstall, prune, doctor | Target host literal: `codex`, `claude-code`, or `github-copilot`. |
| `--scope` | install, update, uninstall, prune, doctor | `project` or `user`. |
| `--agent` | all commands | Select exact custom-agent names. `prune` also accepts names removed from the current catalog. |
| `--repository-root` | project scope | Project root. Defaults to the configured repository-root resolver. |
| `--agent-target-dir` | install, update, uninstall, prune, doctor | Use an exact host-discovered custom-agent artifact directory. |
| `--skill-target-dir` | install, update, doctor | Use an exact bundle target for the resolved skill dependency closure. |
| `--dry-run` | install, update, uninstall, prune | Report planned changes without writing files. |
| `--force` | install, update, uninstall, prune | Allow supported overwrite or delete operations that otherwise block. |
| `--print-diff` | install, update | Include custom-agent and skill file differences in the operation result. |
| `--pretty` | all commands | Indent default JSON output. |

### Supported Hosts

| Host literal | Host | Project Skill target | User Skill target | Project Agent target | User Agent target |
| --- | --- | --- | --- | --- | --- |
| `codex` | Codex | `.agents/skills/<catalogId>` | `${CODEX_HOME}/skills/<catalogId>` or `~/.codex/skills/<catalogId>` | `.codex/agents` | `${CODEX_HOME}/agents` or `~/.codex/agents` |
| `claude-code` | Claude Code | `.claude/skills` | `~/.claude/skills` | `.claude/agents` | `~/.claude/agents` |
| `github-copilot` | GitHub Copilot | `.github/skills/<catalogId>` | `~/.copilot/skills/<catalogId>` | `.github/agents` | `~/.copilot/agents` |

Codex and GitHub Copilot discover skills below an additional catalog directory, so Agent Distribution uses that directory as the managed bundle boundary. Claude Code uses a flat skills directory. Each skill is installed directly below the Skill target shown above.

Agent ownership state stays outside each host's discovery directory: below the corresponding `.codex/agent-distribution/agents`, `.claude/agent-distribution/agents`, `.github/agent-distribution/agents`, or user-home equivalent. An explicit Agent target uses a hidden `.agent-distribution` sibling state directory. Agent Distribution does not edit a host's shared configuration file.

For a default target, Agent Distribution first checks the current layout and the host adapter's compatible previous layouts. If exactly one target root already contains the same managed `catalogId`, install, update, uninstall, prune, and doctor continue to use that root. A new catalog uses the current layout. The operation stops instead of choosing arbitrarily if the same catalog exists under multiple compatible roots or the current catalog directory is already occupied by a flat skill.

`--target-dir` identifies the bundle target itself. Agent Distribution does not append `<catalogId>` to an explicit target, regardless of host or scope. The catalog directory separates managed files on disk; it does not namespace the skill name exposed to the host.

### Prune Removed Skills

Use `skills prune` when a product removes or renames a managed skill and wants old installed output cleaned up.

Prune compares installed managed skills with the complete current catalog identified by the bundled `catalogId`. A narrow selector limits the installed target directories that prune considers, but prune still reads the full current catalog so valid catalog members are not treated as removed.

`--skill` can name a managed skill that was removed from the current generated package set. `--category` selects installed managed targets whose manifest has that category.

Prune deletes only managed, clean, current-host skill directories that belong to the bundled catalog and no longer exist in the current generated package set. It skips unmanaged directories, foreign catalogs, current catalog members, invalid manifests, name collisions, and host conflicts. `--force` allows deleting locally modified managed orphans, but it does not turn unsafe or foreign targets into delete candidates.

`skills update --prune` is not part of the command set. Run `skills prune` explicitly so product CLIs can report update and cleanup as separate operations.

### Prune Removed Custom Agents

`agents prune` reads the complete current agent catalog before applying its installed-state filters. This prevents an exact name selection from treating another current agent as removed. `--agent` may identify an entry no longer present in the current bundle.

Prune deletes only same-catalog custom agents that are absent from the complete current catalog and whose managed artifacts still match their ownership state. It never deletes skill dependencies. `--force` may remove a locally modified managed orphan, but unmanaged artifacts, foreign catalogs, invalid state, and conflicting ownership remain blocked.
