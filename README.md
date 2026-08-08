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

The core package uses [`MackySoft.FileSystem`](https://github.com/mackysoft/dotnet-foundations/tree/master/src/MackySoft.FileSystem) for guarded lexical paths and [`MackySoft.Text.Vocabularies`](https://github.com/mackysoft/dotnet-foundations/tree/master/src/MackySoft.Text.Vocabularies) for stable public literals. Agent Skills still owns physical filesystem checks such as regular-file, reparse-point, and symbolic-link validation.

## Create Skill Packages

Agent Skills separates skill source files from generated packages. Keep the source files in the product repository and ship the generated package directory with the product CLI.

### Define Source Skills

Create `bundle.json` at the bundle root. The source file contains exactly these properties in this order:

```json
{
  "schemaVersion": 1,
  "catalogId": "com.example.skills",
  "skillBundleVersion": 1
}
```

| Property | JSON type | Meaning |
| --- | --- | --- |
| `schemaVersion` | 32-bit integer | Selects the source bundle contract. The current value is `1`. |
| `catalogId` | string | Provides the stable identity shared by the source definition, generated packages, and managed installations. |
| `skillBundleVersion` | 32-bit integer | Identifies the target generated bundle revision. A new bundle starts at `1`. |

For each skill, create `definitions/<category>/<skill-name>/skill.json`. The category and skill name come from those two directory names and are not repeated in the file. The source metadata contains exactly these properties in this order:

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
| `schemaVersion` | 32-bit integer | Selects the source skill contract. The current value is `1`. |
| `displayName` | string | Provides the name shown to users. |
| `description` | string | Provides the host-independent description used for selection and materialization. |
| `dependencies` | array of strings | Names same-bundle skills that must be resolved together with this skill. |

Do not add `catalogId`, `skillBundleVersion`, `category`, `skillName`, a reference-file list, digests, or host-artifact metadata to `skill.json`. Bundle-wide values belong to `bundle.json`; category and skill name come from the directory structure; reference names come from files under `references`; integrity and host-materialization metadata are generated.

Use the [Agent Skills source definition contract](skills/generated/agent-skills-packaging/references/source-definition-contract.md) shipped with the `agent-skills-packaging` skill as the complete source-input contract for layout, metadata, naming, dependencies, content, and canonical file encoding. The examples and tables above show the authored schema shape and ownership boundary; the linked contract is the normative source for all input constraints.

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

By default, the build uses the `skillBundleVersion` authored in `bundle.json` and never infers a new version from source changes. To advance the bundle, provide the exact target version:

```bash
dotnet tool run agent-skills -- build --root skills --skill-bundle-version 2
```

The target must equal the authored version or its next revision. When the next revision is selected, the command updates `bundle.json` and generated output together. Repeating the same exact target is a no-op after the bundle is current. A version-only change preserves `bundleDigest` and each package's `contentDigest`, but changes each `manifestDigest` because it covers `skillBundleVersion`.

### Generated Package Metadata

The generator owns `generated/bundle.json`, every generated `agent-skill.json`, and all generated package files. Do not edit them manually.

The generated root `bundle.json` contains exactly these properties in canonical order:

| Property | Purpose |
| --- | --- |
| `schemaVersion` | Selects the generated bundle contract. It is `1`. |
| `catalogId` | Identifies the owning catalog. It matches the source descriptor. |
| `skillBundleVersion` | Identifies the generated bundle revision. It matches every generated skill manifest. |
| `bundleDigest` | Binds the complete generated package set independently of the bundle version. |

The bundle digest is canonical lowercase SHA-256 text without a prefix.

Each `<skill-name>/agent-skill.json` contains exactly these properties in canonical order:

| Property | Purpose |
| --- | --- |
| `schemaVersion` | Selects the generated manifest contract. It is `1`. |
| `skillBundleVersion` | Identifies the generated skill set and supports installed-version comparisons. It matches the generated root descriptor. |
| `catalogId` | Identifies the owning catalog and prevents operations such as prune from treating another catalog's skills as its own. It matches the generated root descriptor. |
| `category` | Preserves the source category after packages are flattened by skill name and supports category selection and reporting. |
| `skillName` | Identifies the package, dependency graph node, and install directory. The manifest value and directory name must match. |
| `displayName` | Supplies the user-facing name used by reports and host materialization. |
| `description` | Supplies the host-independent description used by reports and host materialization. |
| `dependencies` | Lists the same-bundle skills that must be resolved with this package. |
| `contentDigest` | Binds the paths and normalized contents of `SKILL.md` and `references` files. |
| `manifestDigest` | Binds the canonical manifest fields other than itself, allowing manifest drift to be distinguished from file-content drift. It is an integrity value, not a signature. |
| `hostArtifacts` | Records the generated metadata needed to validate each supported host's materialized frontmatter and optional host-specific file. |

Each `hostArtifacts` entry contains `host` and `materializedFrontmatterDigest`. Hosts that generate a separate metadata file also contain `path` and `digest`; those two properties are either both present or both absent. All digest values use canonical lowercase SHA-256 text without a prefix.

The manifest does not repeat reference file names and does not contain `bundleDigest`, source paths, generation timestamps, Agent Skills tool or NuGet package versions, Git commits, install target paths, reload guidance, or host capability definitions. Reference file names come from the files under `references` in the package set, `bundleDigest` belongs to the generated root descriptor, and the remaining values belong to the source repository or runtime.

## Create Skill and Custom-Agent Bundles

Use source schema `2` when one catalog ships skills, custom agents, or both. Schema `2` keeps the two artifact kinds in separate namespaces and permits only one distribution dependency direction: an agent may depend on skills. Skills cannot depend on agents, and agents do not form a distribution dependency graph with other agents.

Create this fixed source layout:

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
          openai.json
```

Omit `definitions/skills` or `definitions/agents` when the catalog does not define that artifact kind. A namespace must contain at least one definition when it is present, and `definitions` accepts no other entries.

The schema `2` bundle descriptor uses `bundleVersion` because one revision covers both package kinds:

```json
{
  "schemaVersion": 2,
  "catalogId": "com.example.agent-assets",
  "bundleVersion": 1
}
```

Skill definitions retain the schema `1` `skill.json` contract inside `definitions/skills`. An agent's `agent.json` contains only host-independent metadata and direct skill dependencies:

```json
{
  "schemaVersion": 1,
  "displayName": "Architect",
  "description": "Creates an implementation-ready design.",
  "skillDependencies": ["claim-grounding"]
}
```

`AGENT.md.template` is the host-independent instruction source. Each declared skill dependency must be referenced as `$<skill-name>` in that text, and every such reference must be declared. Dependency resolution starts from `skillDependencies` and then reuses the existing transitive skill graph; prose is never used to infer additional dependencies.

Host bindings contain model and execution settings that do not belong in the agent definition. The initial OpenAI/Codex binding contract is:

```json
{
  "schemaVersion": 1,
  "modelProvider": "openai",
  "model": "gpt-5.6-terra",
  "reasoningEffort": "high",
  "verbosity": "low",
  "sandboxMode": "workspace-write",
  "features": {
    "multiAgent": false
  },
  "overridesBuiltIn": false
}
```

The `worker` and `explorer` names require `overridesBuiltIn: true`; other names reject that setting. The adapter generates one `<agent-name>.toml` file and never edits shared `.codex/config.toml` state.

Build schema `2` with the same command used by skill-only bundles:

```bash
dotnet tool run agent-skills -- build --root agent-assets
dotnet tool run agent-skills -- build --root agent-assets --bundle-version 2
dotnet tool run agent-skills -- build --root agent-assets --check
```

The generated layout separates both package kinds:

```text
generated/
  bundle.json
  skills/<skill-name>/...
  agents/<agent-name>/
    AGENT.md
    agent-manifest.json
    hosts/openai/<agent-name>.toml
```

The build validates the complete source, skill dependency graph, agent references, host bindings, fixed layout, manifests, file sets, and digests before replacing generated output. Repeating a build from the same input produces the same bytes. Existing schema `1` bundles and root-level skill commands keep their existing layout and meaning.

### Verify and Synchronize Generated Packages

When generated output already matches the source definition and bundle version, the command does not write any files. To verify committed output without changing the working tree, use:

```bash
dotnet tool run agent-skills -- build --root skills --check
```

The repository provides separate `verify` and `sync` composite GitHub Actions. Both accept `root`, a bundle root relative to the GitHub workspace that resolves inside the checked-out Git worktree, and restore the CLI version pinned by the caller's .NET tool manifest. `sync` also accepts an optional exact `skill-bundle-version`.

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
      skill-bundle-version: 2
```

Omit `skill-bundle-version` when synchronization should preserve the value authored in `bundle.json`.

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
    options.AgentsCommandRoot = "agents";
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
    options.AgentsCommandRoot = "agents";
});

ConsoleApp.ConsoleAppBuilder app = builder.ToConsoleAppBuilder();

// Register product filters, global options, and product commands as usual.
app.RegisterAgentSkillsCommands();
app.RegisterAgentSkillsAgentsCommands();

await app.RunAsync(args);
return Environment.ExitCode;
```

`RegisterAgentSkillsCommands()` adds the skill command group and `RegisterAgentSkillsAgentsCommands()` adds the custom-agent command group. Either registrar can be called independently. They do not create a builder, run the app, set `ConsoleApp.LogError`, replace the service provider, register filters, or change command validation.

The default roots are `skills` and `agents`. To expose different roots, set the ConsoleAppFramework integration's independent MSBuild properties and keep the runtime options aligned.

```xml
<PropertyGroup>
  <AgentSkillsConsoleAppFrameworkCommandRoot>agent-skills</AgentSkillsConsoleAppFrameworkCommandRoot>
  <AgentSkillsConsoleAppFrameworkAgentsCommandRoot>agent-assets</AgentSkillsConsoleAppFrameworkAgentsCommandRoot>
</PropertyGroup>
```

```csharp
services.AddAgentSkillsCommandRuntime(options =>
{
    options.CommandRoot = "agent-skills";
    options.AgentsCommandRoot = "agent-assets";
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
- `ProductName`, `PackageBaseDirectory`, `CommandRoot`, `AgentsCommandRoot`, and the default repository-root policy;
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

agents list
agents export
agents install
agents update
agents uninstall
agents prune
agents doctor
```

The standalone `MackySoft.AgentSkills.Cli` is the top-level composition root for the same command adapter and ships this repository's generated `basic/agent-skills-packaging` skill. Its standard commands are registered at the process root because the executable name already supplies the command root:

```bash
dotnet tool run agent-skills -- list --pretty
dotnet tool run agent-skills -- install --host openai --scope project --category basic --dryRun --pretty
dotnet tool run agent-skills -- agents list --pretty
dotnet tool run agent-skills -- agents install --host openai --scope project --category orchestration --dryRun --pretty
```

The standalone executable preserves exact C# parameter names for existing skill multiword options: `--repositoryRoot`, `--targetDir`, `--dryRun`, and `--printDiff`. Custom-agent target options use the explicit public names `--agent-target-dir` and `--skill-target-dir`. The product CLI examples and common-options tables below use kebab-case names.

`skills list` can omit selectors and then lists every bundled skill category. Other skill commands require `--category`, `--skill`, or both. `agents list` can likewise omit selectors; other custom-agent commands require `--category`, `--agent`, or both.

### Examples

```bash
example skills list
example skills export --host openai --category core --output ./exported-skills
example skills install --host openai --scope project --category core
example skills update --host openai --scope project --skill example-review
example skills uninstall --host openai --scope project --skill example-review
example skills prune --host openai --scope project --category core
example skills doctor --host openai --scope project --category core

example agents list
example agents export --host openai --agent architect --output ./exported-agent-assets
example agents install --host openai --scope project --category orchestration
example agents update --host openai --scope project --agent architect
example agents uninstall --host openai --scope project --agent architect
example agents prune --host openai --scope project --agent retired-agent
example agents doctor --host openai --scope project --category orchestration
```

### Skill Command Options

| Option | Applies to | Meaning |
| --- | --- | --- |
| `--host` | export, install, update, uninstall, prune, doctor | Target host literal: `claude`, `copilot`, or `openai`. |
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
| `--host` | export, install, update, uninstall, prune, doctor | Target host literal. The initial custom-agent adapter uses `openai`. |
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

| Host literal | Host | Project bundle target | User bundle target |
| --- | --- | --- | --- |
| `openai` | OpenAI / Codex | `.agents/skills/<catalogId>` | `${CODEX_HOME}/skills/<catalogId>` or `~/.codex/skills/<catalogId>` |
| `claude` | Claude Code | `.claude/skills` | `~/.claude/skills` |
| `copilot` | GitHub Copilot CLI | `.github/skills/<catalogId>` | `~/.copilot/skills/<catalogId>` |

OpenAI / Codex and GitHub Copilot CLI discover skills below an additional catalog directory, so Agent Skills uses that directory as the managed bundle boundary. Claude Code uses a flat skills directory because its plain skill discovery does not treat an arbitrary parent directory as a package boundary. Each skill is installed directly below the bundle target shown above.

The initial custom-agent host adapter is OpenAI/Codex. It installs agent artifacts into `.codex/agents` for project scope or `${CODEX_HOME}/agents` for user scope, falling back to `~/.codex/agents`. Ownership state is kept outside the host discovery directory under `.codex/agent-skills/agents` or `${CODEX_HOME}/agent-skills/agents`. An explicit agent target uses a hidden `.agent-skills` sibling state directory. Agent Skills does not edit shared `.codex/config.toml`.

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
