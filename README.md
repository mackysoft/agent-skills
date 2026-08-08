# Agent Skills

Agent Skills helps product teams ship agent SKILL packages and host-specific custom-agent artifacts with their own CLI.

Use it when your product owns:

- the skill catalog and release cadence;
- the category names represented by source definition directories;
- the public CLI shape and output envelope.

Agent Skills provides the build tool, package formats, dependency resolution, host materialization, command runtime, and report data needed to list, export, install, update, uninstall, prune, and diagnose skills and custom agents.

## Packages

| Package | Use it when |
| --- | --- |
| `MackySoft.AgentSkills.Cli` | A product repository needs to build canonical packages, or a user wants to operate the Agent Skills catalog shipped by this repository. |
| `MackySoft.AgentSkills` | A product needs the core package, host, install, export, prune, doctor, and report APIs without a hosted command runtime. |
| `MackySoft.AgentSkills.Hosting` | A product CLI wants the standard Agent Skills command runtime and DI registration. |
| `MackySoft.AgentSkills.ConsoleAppFramework` | A ConsoleAppFramework-based product CLI wants Agent Skills commands registered on its existing builder. |

All packages are versioned together.

The core package uses [`MackySoft.FileSystem`](https://github.com/mackysoft/dotnet-foundations/tree/master/src/MackySoft.FileSystem) for typed lexical paths, physical entry inspection, physical containment resolution, and atomic single-file publication. It uses [`MackySoft.Text.Vocabularies`](https://github.com/mackysoft/dotnet-foundations/tree/master/src/MackySoft.Text.Vocabularies) for stable public literals. Agent Skills retains only product-specific path policies, failure mapping, deterministic package formats, and multi-file transactions.

## Create Distribution Bundles

Agent Skills separates authored definitions from generated packages. Source schema `2` can contain skills, custom agents, or both. The namespaces are separate, and the only distribution dependency direction is Agent to Skill. Skills never depend on agents, and agents do not form a distribution dependency graph with other agents.

### Define the Source Layout

Create this fixed layout in the product repository:

```text
<bundle-root>/
  bundle.json
  definitions/
    skills/
      <category>/<skill-name>/
        skill.json
        SKILL.md.template
        references/
    agents/
      <category>/<agent-name>/
        agent.json
        AGENT.md.template
        hosts/
          codex.json
          claude-code.json
          github-copilot.json
```

Omit `definitions/skills` or `definitions/agents` when the catalog does not define that artifact kind. A namespace must contain at least one definition when present, and `definitions` accepts no other entries.

Create `bundle.json` at the bundle root. One `bundleVersion` covers both package kinds:

```json
{
  "schemaVersion": 2,
  "catalogId": "com.example.agent-assets",
  "bundleVersion": 1
}
```

| Property | JSON type | Meaning |
| --- | --- | --- |
| `schemaVersion` | 32-bit integer | Selects source schema `2`. |
| `catalogId` | string | Provides the stable identity shared by source definitions, generated packages, and managed installations. |
| `bundleVersion` | 32-bit integer | Identifies the revision of the complete generated bundle. A new bundle starts at `1`. |

### Define Skills

For each skill, create `definitions/skills/<category>/<skill-name>/skill.json`. Category and skill name come from the directory names. The metadata contains exactly these properties:

```json
{
  "schemaVersion": 1,
  "displayName": "Example Review",
  "description": "Review a completed example.",
  "dependencies": []
}
```

| Property | JSON type | Meaning |
| --- | --- | --- |
| `schemaVersion` | 32-bit integer | Selects the skill definition contract. The current value is `1`. |
| `displayName` | string | Provides the name shown to users. |
| `description` | string | Provides the host-independent description used for selection and materialization. |
| `dependencies` | array of strings | Names same-bundle skills that must be resolved with this skill. |

Do not repeat bundle identity, category, skill name, reference-file names, digests, or host artifacts in `skill.json`. Those values belong to the bundle, directory layout, reference files, or generated package.

Use the [skill source definition contract](skills/generated/skills/agent-skills-packaging/references/source-definition-contract.md) shipped with `agent-skills-packaging` for the complete skill layout, naming, dependency, content, and encoding rules.

### Define Custom Agents

An agent's `agent.json` contains only host-independent metadata and direct skill dependencies:

```json
{
  "schemaVersion": 1,
  "displayName": "Architect",
  "description": "Creates an implementation-ready design.",
  "skillDependencies": ["claim-grounding"]
}
```

`AGENT.md.template` is the host-independent instruction source. It does not contain host binding fields or require a host-specific skill-reference syntax. `skillDependencies` is the only dependency declaration; dependency resolution starts from that array and then reuses the existing transitive skill graph. The build does not infer dependencies from the instruction text.

Host bindings contain only the model and execution settings owned by one execution host. A definition may contain any non-empty subset of `codex.json`, `claude-code.json`, and `github-copilot.json`.

`codex.json` accepts:

```json
{
  "schemaVersion": 1,
  "model": "gpt-5.6-terra",
  "reasoningEffort": "high",
  "sandboxMode": "workspace-write"
}
```

The Codex names `default`, `worker`, and `explorer` are reserved and cannot be used by distributed custom agents. The adapter generates one `<agent-name>.toml` file and never edits shared `.codex/config.toml` state.

`claude-code.json` accepts:

```json
{
  "schemaVersion": 1,
  "model": "sonnet",
  "tools": ["Read", "Grep"],
  "disallowedTools": ["Write"],
  "permissionMode": "default",
  "maxTurns": 20
}
```

`github-copilot.json` accepts:

```json
{
  "schemaVersion": 1,
  "target": "github-copilot",
  "tools": ["read"],
  "disableModelInvocation": false,
  "userInvocable": true
}
```

For GitHub Copilot, omit `tools` to inherit all tools or set it to an empty array to disable every tool.

Every binding field other than `schemaVersion` is optional. Each host adapter validates only its own binding and generates only its own format.

### Generate Shipped Packages

Install the build tool in the product repository:

```bash
dotnet new tool-manifest
dotnet tool install MackySoft.AgentSkills.Cli --version 1.0.0
```

Build the source bundle:

```bash
dotnet tool run agent-skills -- build --root skills
dotnet tool run agent-skills -- build --root skills --bundle-version 2
dotnet tool run agent-skills -- build --root skills --check
```

The command reads `bundle.json` and `definitions`, then publishes `generated` as one canonical bundle. Do not edit generated files manually. When packaging a product CLI, ship `generated` as `<PackageBaseDirectory>/skills`.

The requested version must equal the authored `bundleVersion` or its next revision. Selecting the next revision updates `bundle.json` and generated output together. Repeating the same build is a no-op.

The generated layout preserves the two artifact namespaces:

```text
generated/
  bundle.json
  skills/<skill-name>/...
  agents/<agent-name>/
    AGENT.md
    agent-manifest.json
    hosts/
      codex/<agent-name>.toml
      claude-code/<agent-name>.md
      github-copilot/<agent-name>.agent.md
```

The build validates the complete source, skill dependency graph, agent references, host bindings, fixed layout, manifests, file sets, and digests before replacing generated output. Repeating a build from the same input produces the same bytes.

### Generated Package Metadata

The generator owns `generated/bundle.json`, skill manifests, agent manifests, and every generated package file. The root descriptor contains `schemaVersion`, `catalogId`, `bundleVersion`, and `bundleDigest`. The digest binds the complete package set independently of its version.

Each `generated/skills/<skill-name>/agent-skill.json` records the skill identity, direct skill dependencies, content and manifest digests, and materialization metadata for every supported host. Each `generated/agents/<agent-name>/agent-manifest.json` records the agent identity, direct skill dependencies, instruction and manifest digests, and the generated artifact path and digest for each declared host. Generated manifests do not contain source paths, timestamps, tool versions, Git commits, install targets, or host capability definitions.

### Verify and Synchronize Generated Packages

When generated output already matches the source definition and bundle version, the command does not write any files. To verify committed output without changing the working tree, use:

```bash
dotnet tool run agent-skills -- build --root skills --check
```

The repository provides separate `verify` and `sync` composite GitHub Actions. Both accept `root`, a bundle root relative to the GitHub workspace that resolves inside the checked-out Git worktree, and restore the CLI version pinned by the caller's .NET tool manifest. `sync` also accepts an optional exact `bundle-version`.

Use `verify` for pull requests and other read-only checks. It runs `build --check`, fails when committed output is stale, and never generates or commits files.

```yaml
- name: Checkout
  uses: actions/checkout@v5

- name: Verify Agent Skills
  uses: mackysoft/agent-skills/actions/verify@1.0.0
  with:
    root: skills
```

Use `sync` only from a branch workflow with `contents: write`. When reconciliation is required, it requires a clean Git index, synchronizes generated output, updates `bundle.json` when the exact next version is selected, stages only `<root>/bundle.json` and `<root>/generated`, creates a `github-actions[bot]` commit, and pushes that commit to the current branch. Its `changed` output is `true` only after that push succeeds.

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
      bundle-version: 2
```

Omit `bundle-version` when synchronization should preserve the value authored in `bundle.json`.

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
using MackySoft.FileSystem;

services.AddAgentSkillsCommandRuntime(options =>
{
    options.ProductName = "Example CLI";
    options.PackageBaseDirectory = AbsolutePath.Parse(AppContext.BaseDirectory);
});
```

The package base directory must contain the shipped generated packages under `skills/`. A schema `1` root contains skill packages directly. A schema `2` root contains separate `skills/` and `agents/` namespaces.

```text
<PackageBaseDirectory>/
  skills/
    bundle.json
    skills/<skill-name>/...
    agents/<agent-name>/...
```

The runtime reads the catalog identity and available categories from the generated bundle descriptor and package manifests. Categories are not configured separately in product code.

Project-scope commands use the current directory when `--repository-root` is omitted. If the product CLI already has a repository-root policy, set `RepositoryRootResolver` to keep Agent Skills commands aligned with it.

```csharp
services.AddAgentSkillsCommandRuntime(options =>
{
    options.RepositoryRootResolver = currentDirectory =>
        AbsolutePath.Parse(ProductRepositoryResolver.Resolve(currentDirectory.Value));
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
using MackySoft.FileSystem;
using Microsoft.Extensions.Hosting;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Services.AddAgentSkillsCommandRuntime(options =>
{
    options.ProductName = "Example CLI";
    options.PackageBaseDirectory = AbsolutePath.Parse(AppContext.BaseDirectory);
});

ConsoleApp.ConsoleAppBuilder app = builder.ToConsoleAppBuilder();

// Register product filters, global options, and product commands as usual.
app.RegisterAgentSkillsCommands();

await app.RunAsync(args);
return Environment.ExitCode;
```

`RegisterAgentSkillsCommands()` adds the fixed, sibling `skills` and `agents` resource groups to the product's command root. It does not own the product executable name, add an extra parent group, create a builder, run the app, set `ConsoleApp.LogError`, replace the service provider, register filters, or change command validation. Command results use the resource path, such as `skills.list` or `agents.list`.

The command examples in this README use ConsoleAppFramework's default lower-kebab-case option names.

### Product Responsibilities

The product CLI still owns:

- when generated packages are built and how they are shipped;
- the source `bundle.json`, category directories, and skill definitions;
- `ProductName`, `PackageBaseDirectory`, and the default repository-root policy;
- the public command surface outside the fixed `skills` and `agents` groups;
- pre-dispatch command validation, help policy, filters, global options, and logging;
- the output envelope, if the default JSON result shape is not appropriate.

Register your own `IAgentSkillsCommandResultEmitter` after `AddAgentSkillsCommandRuntime(...)` when the product needs its own JSON envelope or text output.

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

The standalone `MackySoft.AgentSkills.Cli` is the top-level composition root for the same command adapters and ships this repository's generated `basic/agent-skills-packaging` skill. The executable name supplies `agent-skills`; the resource group remains explicit:

```bash
dotnet tool run agent-skills -- skills list --pretty
dotnet tool run agent-skills -- skills install --host codex --scope project --category basic --dry-run --pretty
dotnet tool run agent-skills -- agents list --pretty
dotnet tool run agent-skills -- agents install --host claude-code --scope project --category orchestration --dry-run --pretty
```

The standalone executable and product integration both use lower-kebab-case option names.

`skills list` can omit selectors and then lists every bundled skill category. Other skill commands require `--category`, `--skill`, or both. `agents list` can likewise omit selectors; other custom-agent commands require `--category`, `--agent`, or both.

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
example agents install --host github-copilot --scope project --category orchestration
example agents update --host github-copilot --scope project --agent architect
example agents uninstall --host github-copilot --scope project --agent architect
example agents prune --host github-copilot --scope project --agent retired-agent
example agents doctor --host github-copilot --scope project --category orchestration
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
| `--category` | all commands | Select custom agents by agent category. It does not select a skill category. |
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

Codex and GitHub Copilot discover skills below an additional catalog directory, so Agent Skills uses that directory as the managed bundle boundary. Claude Code uses a flat skills directory. Each skill is installed directly below the Skill target shown above.

Agent ownership state stays outside each host's discovery directory: below the corresponding `.codex/agent-skills/agents`, `.claude/agent-skills/agents`, `.github/agent-skills/agents`, or user-home equivalent. An explicit Agent target uses a hidden `.agent-skills` sibling state directory. Agent Skills does not edit a host's shared configuration file.

For a default target, Agent Skills first checks the current layout and the host adapter's compatible previous layouts. If exactly one target root already contains the same managed `catalogId`, install, update, uninstall, prune, and doctor continue to use that root. A new catalog uses the current layout. The operation stops instead of choosing arbitrarily if the same catalog exists under multiple compatible roots or the current catalog directory is already occupied by a flat skill.

`--target-dir` identifies the bundle target itself. Agent Skills does not append `<catalogId>` to an explicit target, regardless of host or scope. The catalog directory separates managed files on disk; it does not namespace the skill name exposed to the host.

### Prune Removed Skills

Use `skills prune` when a product removes or renames a managed skill and wants old installed output cleaned up.

Prune compares installed managed skills with the complete current catalog identified by the bundled `catalogId`. A narrow selector limits the installed target directories that prune considers, but prune still reads the full current catalog so valid catalog members are not treated as removed.

`--skill` can name a managed skill that was removed from the current generated package set. `--category` selects installed managed targets whose manifest has that category.

Prune deletes only managed, clean, current-host skill directories that belong to the bundled catalog and no longer exist in the current generated package set. It skips unmanaged directories, foreign catalogs, current catalog members, invalid manifests, name collisions, and host conflicts. `--force` allows deleting locally modified managed orphans, but it does not turn unsafe or foreign targets into delete candidates.

`skills update --prune` is not part of the command set. Run `skills prune` explicitly so product CLIs can report update and cleanup as separate operations.

### Prune Removed Custom Agents

`agents prune` reads the complete current agent catalog before applying its installed-state filters. This prevents a narrow category or name selection from treating an unselected current agent as removed. An exact `--agent` or `--category` may identify an entry no longer present in the current bundle.

Prune deletes only same-catalog custom agents that are absent from the complete current catalog and whose managed artifacts still match their ownership state. It never deletes skill dependencies. `--force` may remove a locally modified managed orphan, but unmanaged artifacts, foreign catalogs, invalid state, and conflicting ownership remain blocked.
