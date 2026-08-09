#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=scripts/dotnet-common.sh
source "$script_dir/dotnet-common.sh"

usage() {
  echo "usage: bash scripts/verify-packages.sh [--configuration <name>] [--version <semver>] [--repository-commit <sha>] [--output <package-dir>]" >&2
}

configuration="Release"
package_version=""
repository_commit=""
package_output=""

while [ "$#" -gt 0 ]; do
  case "$1" in
    --configuration)
      if [ "$#" -lt 2 ]; then
        usage
        exit 2
      fi

      configuration="$2"
      shift 2
      ;;
    --configuration=*)
      configuration="${1#--configuration=}"
      shift
      ;;
    --version)
      if [ "$#" -lt 2 ]; then
        usage
        exit 2
      fi

      package_version="$2"
      shift 2
      ;;
    --version=*)
      package_version="${1#--version=}"
      shift
      ;;
    --repository-commit)
      if [ "$#" -lt 2 ]; then
        usage
        exit 2
      fi

      repository_commit="$2"
      shift 2
      ;;
    --repository-commit=*)
      repository_commit="${1#--repository-commit=}"
      shift
      ;;
    --output)
      if [ "$#" -lt 2 ]; then
        usage
        exit 2
      fi

      package_output="$2"
      shift 2
      ;;
    --output=*)
      package_output="${1#--output=}"
      shift
      ;;
    *)
      usage
      exit 2
      ;;
  esac
done

if [ -z "$configuration" ]; then
  usage
  exit 2
fi

if [ -z "$package_version" ]; then
  package_version="$(sed -nE 's:.*<Version>([^<]+)</Version>.*:\1:p' "$DOTNET_REPO_ROOT/Directory.Build.props" | head -n 1)"
fi

semver_pattern='^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(-((0|[1-9][0-9]*|[0-9A-Za-z-]*[A-Za-z-][0-9A-Za-z-]*)(\.(0|[1-9][0-9]*|[0-9A-Za-z-]*[A-Za-z-][0-9A-Za-z-]*))*))?$'
if [[ ! "$package_version" =~ $semver_pattern ]]; then
  echo "package version must be SemVer without a leading v: $package_version" >&2
  exit 2
fi

if [[ -n "$repository_commit" && ! "$repository_commit" =~ ^[0-9a-fA-F]{40}$ ]]; then
  echo "repository commit must be a 40-character Git SHA: $repository_commit" >&2
  exit 2
fi

work_root="$(mktemp -d "${TMPDIR:-/tmp}/agent-skills-packages.XXXXXX")"
cleanup_work_root=true
trap 'if [ "$cleanup_work_root" = true ]; then rm -rf "$work_root"; fi' EXIT

export DOTNET_CLI_HOME="$work_root/dotnet-home"
export NUGET_PACKAGES="$work_root/nuget-packages"
mkdir -p "$DOTNET_CLI_HOME" "$NUGET_PACKAGES"

if [ -n "$package_output" ]; then
  case "$package_output" in
    /*)
      package_dir="$package_output"
      ;;
    *)
      package_dir="$DOTNET_REPO_ROOT/$package_output"
      ;;
  esac
else
  package_dir="$work_root/packages"
fi

mkdir -p "$package_dir"
rm -f "$package_dir"/*.nupkg

pack_properties=(
  -p:Version="$package_version"
  -p:PackageVersion="$package_version"
)
if [ -n "$repository_commit" ]; then
  pack_properties+=(-p:RepositoryCommit="$repository_commit")
fi

cd "$DOTNET_REPO_ROOT"
dotnet restore AgentSkills.slnx
dotnet pack src/MackySoft.AgentSkills/MackySoft.AgentSkills.csproj \
  --configuration "$configuration" \
  --no-restore \
  "${pack_properties[@]}" \
  --output "$package_dir"
dotnet pack src/MackySoft.AgentSkills.Cli/MackySoft.AgentSkills.Cli.csproj \
  --configuration "$configuration" \
  --no-restore \
  "${pack_properties[@]}" \
  --output "$package_dir"
dotnet pack src/MackySoft.AgentSkills.Hosting/MackySoft.AgentSkills.Hosting.csproj \
  --configuration "$configuration" \
  --no-restore \
  "${pack_properties[@]}" \
  --output "$package_dir"
dotnet pack src/MackySoft.AgentSkills.ConsoleAppFramework/MackySoft.AgentSkills.ConsoleAppFramework.csproj \
  --configuration "$configuration" \
  --no-restore \
  "${pack_properties[@]}" \
  --output "$package_dir"

library_package="$package_dir/MackySoft.AgentSkills.$package_version.nupkg"
cli_package="$package_dir/MackySoft.AgentSkills.Cli.$package_version.nupkg"
hosting_package="$package_dir/MackySoft.AgentSkills.Hosting.$package_version.nupkg"
consoleappframework_package="$package_dir/MackySoft.AgentSkills.ConsoleAppFramework.$package_version.nupkg"
expected_package_files=(
  "MackySoft.AgentSkills.$package_version.nupkg"
  "MackySoft.AgentSkills.Cli.$package_version.nupkg"
  "MackySoft.AgentSkills.Hosting.$package_version.nupkg"
  "MackySoft.AgentSkills.ConsoleAppFramework.$package_version.nupkg"
)

for package_file in "${expected_package_files[@]}"; do
  if [ ! -f "$package_dir/$package_file" ]; then
    echo "package was not created: $package_dir/$package_file" >&2
    exit 1
  fi
done

actual_package_files=()
while IFS= read -r package_file; do
  actual_package_files+=("$package_file")
done < <(find "$package_dir" -maxdepth 1 -type f -name '*.nupkg' -exec basename {} \; | sort)

sorted_expected_package_files=()
while IFS= read -r package_file; do
  sorted_expected_package_files+=("$package_file")
done < <(printf '%s\n' "${expected_package_files[@]}" | sort)

if [ "${#actual_package_files[@]}" -ne "${#sorted_expected_package_files[@]}" ]; then
  printf 'package output contained unexpected .nupkg files:\n' >&2
  printf '  %s\n' "${actual_package_files[@]}" >&2
  exit 1
fi

for index in "${!sorted_expected_package_files[@]}"; do
  if [ "${actual_package_files[$index]}" != "${sorted_expected_package_files[$index]}" ]; then
    printf 'package output contained unexpected .nupkg files:\n' >&2
    printf '  %s\n' "${actual_package_files[@]}" >&2
    exit 1
  fi
done

consoleappframework_package_files="$(unzip -Z1 "$consoleappframework_package")"
grep -Fxq 'lib/net8.0/MackySoft.AgentSkills.ConsoleAppFramework.dll' <<< "$consoleappframework_package_files"
grep -Fxq 'buildTransitive/MackySoft.AgentSkills.ConsoleAppFramework.props' <<< "$consoleappframework_package_files"

cli_package_files="$(unzip -Z1 "$cli_package")"
grep -Fxq 'tools/net8.0/any/skills/bundle.json' <<< "$cli_package_files"
grep -Fxq 'tools/net8.0/any/skills/skills/agent-skills-packaging/agent-skill.json' <<< "$cli_package_files"

if [ -n "$repository_commit" ]; then
  bash "$script_dir/validate-nuget-package-repository-commit.sh" \
    --package-id MackySoft.AgentSkills \
    --package-path "$library_package" \
    --expected-commit "$repository_commit"
  bash "$script_dir/validate-nuget-package-repository-commit.sh" \
    --package-id MackySoft.AgentSkills.Cli \
    --package-path "$cli_package" \
    --expected-commit "$repository_commit"
  bash "$script_dir/validate-nuget-package-repository-commit.sh" \
    --package-id MackySoft.AgentSkills.Hosting \
    --package-path "$hosting_package" \
    --expected-commit "$repository_commit"
  bash "$script_dir/validate-nuget-package-repository-commit.sh" \
    --package-id MackySoft.AgentSkills.ConsoleAppFramework \
    --package-path "$consoleappframework_package" \
    --expected-commit "$repository_commit"
fi

consumer_dir="$work_root/consumer"
dotnet new console --output "$consumer_dir" --no-restore >/dev/null
dotnet add "$consumer_dir/consumer.csproj" package MackySoft.AgentSkills \
  --version "$package_version" \
  --source "$package_dir" >/dev/null
dotnet restore "$consumer_dir/consumer.csproj" \
  --source "$package_dir" \
  --source https://api.nuget.org/v3/index.json >/dev/null
dotnet build "$consumer_dir/consumer.csproj" --configuration "$configuration" --no-restore >/dev/null

console_consumer_dir="$work_root/console-consumer"
dotnet new console --output "$console_consumer_dir" --no-restore >/dev/null
cp -R "$DOTNET_REPO_ROOT/tests/Fixtures/AgentSkillsBundle/generated" "$console_consumer_dir/skills"
dotnet add "$console_consumer_dir/console-consumer.csproj" package MackySoft.AgentSkills.ConsoleAppFramework \
  --version "$package_version" \
  --source "$package_dir" >/dev/null
dotnet add "$console_consumer_dir/console-consumer.csproj" package Microsoft.Extensions.Hosting >/dev/null
cat > "$console_consumer_dir/Program.cs" <<'CS'
using System;
using System.IO;
using ConsoleAppFramework;
using MackySoft.AgentSkills.ConsoleAppFramework;
using MackySoft.AgentSkills.Hosting.Composition;
using MackySoft.FileSystem;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddAgentSkillsCommandRuntime(options =>
{
    options.ProductName = "Smoke CLI";
    options.PackageBaseDirectory = AbsolutePath.Parse(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..")));
});

var app = builder.ToConsoleAppBuilder();
app.RegisterAgentSkillsCommands();
await app.RunAsync(args);
return Environment.ExitCode;
CS
dotnet restore "$console_consumer_dir/console-consumer.csproj" \
  --source "$package_dir" \
  --source https://api.nuget.org/v3/index.json >/dev/null
dotnet build "$console_consumer_dir/console-consumer.csproj" \
  --configuration "$configuration" \
  --no-restore \
  -p:ImplicitUsings=disable \
  -p:Nullable=enable \
  -p:TreatWarningsAsErrors=true >/dev/null
dotnet run \
  --project "$console_consumer_dir/console-consumer.csproj" \
  --configuration "$configuration" \
  --no-build \
  -- --help > "$work_root/console-help.txt"
grep -Eq '^  skills list[[:space:]]' "$work_root/console-help.txt"
grep -Eq '^  agents list[[:space:]]' "$work_root/console-help.txt"
if grep -Eq '^  (list|export|install|update|uninstall|prune|doctor)[[:space:]]' "$work_root/console-help.txt"; then
  printf 'ConsoleAppFramework consumer exposed a root-level artifact operation.\n' >&2
  exit 1
fi
dotnet run \
  --project "$console_consumer_dir/console-consumer.csproj" \
  --configuration "$configuration" \
  --no-build \
  -- skills list --pretty > "$work_root/skills-list.json"
grep -q '"Command": "skills.list"' "$work_root/skills-list.json"
grep -q '"Status": "ok"' "$work_root/skills-list.json"
grep -q '"SkillName": "review-context"' "$work_root/skills-list.json"
dotnet run \
  --project "$console_consumer_dir/console-consumer.csproj" \
  --configuration "$configuration" \
  --no-build \
  -- agents list --pretty > "$work_root/agents-list.json"
grep -q '"Command": "agents.list"' "$work_root/agents-list.json"
grep -q '"Status": "ok"' "$work_root/agents-list.json"
grep -q '"AgentName": "architect"' "$work_root/agents-list.json"
grep -q '"ResolvedSkills":' "$work_root/agents-list.json"
grep -q '"review-context"' "$work_root/agents-list.json"

for host in codex claude-code github-copilot; do
  export_root="$work_root/export-$host"
  export_report="$work_root/agents-export-$host.json"
  dotnet run \
    --project "$console_consumer_dir/console-consumer.csproj" \
    --configuration "$configuration" \
    --no-build \
    -- agents export \
    --host "$host" \
    --agent architect \
    --output "$export_root" \
    --format directory \
    --pretty > "$export_report"
  grep -q '"Command": "agents.export"' "$export_report"
  grep -q '"Status": "ok"' "$export_report"
  test -f "$export_root/skills/review-context/SKILL.md"

  install_root="$work_root/install-$host"
  install_report="$work_root/agents-install-$host.json"
  mkdir -p "$install_root"
  dotnet run \
    --project "$console_consumer_dir/console-consumer.csproj" \
    --configuration "$configuration" \
    --no-build \
    -- agents install \
    --host "$host" \
    --scope project \
    --agent architect \
    --repository-root "$install_root" \
    --pretty > "$install_report"
  grep -q '"Command": "agents.install"' "$install_report"
  grep -q '"Status": "ok"' "$install_report"

  case "$host" in
    codex)
      test -f "$export_root/agents/architect.toml"
      test -f "$install_root/.codex/agents/architect.toml"
      test -f "$install_root/.agents/skills/com.mackysoft.agent-skills.consumer-tests/review-context/SKILL.md"
      ;;
    claude-code)
      test -f "$export_root/agents/architect.md"
      test -f "$install_root/.claude/agents/architect.md"
      test -f "$install_root/.claude/skills/review-context/SKILL.md"
      ;;
    github-copilot)
      test -f "$export_root/agents/architect.agent.md"
      test -f "$install_root/.github/agents/architect.agent.md"
      test -f "$install_root/.github/skills/com.mackysoft.agent-skills.consumer-tests/review-context/SKILL.md"
      ;;
  esac
done

mkdir -p "$work_root/install-target"
dotnet run \
  --project "$console_consumer_dir/console-consumer.csproj" \
  --configuration "$configuration" \
  --no-build \
  -- skills install --host codex --scope project --category core --repository-root "$work_root/install-target" --dry-run --pretty > "$work_root/skills-install.json"
grep -q '"Command": "skills.install"' "$work_root/skills-install.json"
grep -q '"Status": "ok"' "$work_root/skills-install.json"
grep -q '"DryRun": true' "$work_root/skills-install.json"

tool_dir="$work_root/tool"
mkdir -p "$tool_dir"
(
  cd "$tool_dir"
  dotnet new tool-manifest >/dev/null
  dotnet tool install MackySoft.AgentSkills.Cli \
    --version "$package_version" \
    --add-source "$package_dir" >/dev/null

  dotnet tool run agent-skills -- --help > "$work_root/tool-help.txt"
  grep -Eq '^  skills list[[:space:]]' "$work_root/tool-help.txt"
  grep -Eq '^  agents list[[:space:]]' "$work_root/tool-help.txt"
  if grep -Eq '^  (list|export|install|update|uninstall|prune|doctor)[[:space:]]' "$work_root/tool-help.txt"; then
    printf 'Standalone CLI exposed a root-level artifact operation.\n' >&2
    exit 1
  fi

  bundle_root="$work_root/skill-bundle"
  cp -R "$DOTNET_REPO_ROOT/tests/Fixtures/SkillBundle" "$bundle_root"
  rm -rf "$bundle_root/generated"
  dotnet tool run agent-skills -- build --root "$bundle_root" >/dev/null

  diff -ruN "$DOTNET_REPO_ROOT/tests/Fixtures/SkillBundle/generated" "$bundle_root/generated"

  agent_bundle_root="$work_root/agent-bundle"
  cp -R "$DOTNET_REPO_ROOT/tests/Fixtures/AgentSkillsBundle" "$agent_bundle_root"
  rm -rf "$agent_bundle_root/generated"
  dotnet tool run agent-skills -- build --root "$agent_bundle_root" >/dev/null

  diff -ruN "$DOTNET_REPO_ROOT/tests/Fixtures/AgentSkillsBundle/generated" "$agent_bundle_root/generated"

  dotnet tool run agent-skills -- skills list --pretty > "$work_root/agent-skills-own-list.json"
  grep -q '"Command": "skills.list"' "$work_root/agent-skills-own-list.json"
  grep -q '"Status": "ok"' "$work_root/agent-skills-own-list.json"
  grep -q '"Category": "basic"' "$work_root/agent-skills-own-list.json"
  grep -q '"SkillName": "agent-skills-packaging"' "$work_root/agent-skills-own-list.json"

  dotnet tool run agent-skills -- agents list --pretty > "$work_root/agent-skills-own-agents-list.json"
  grep -q '"Command": "agents.list"' "$work_root/agent-skills-own-agents-list.json"
  grep -q '"Status": "ok"' "$work_root/agent-skills-own-agents-list.json"

  own_install_root="$work_root/agent-skills-own-install"
  mkdir -p "$own_install_root"
  dotnet tool run agent-skills -- skills install \
    --host codex \
    --scope project \
    --category basic \
    --repository-root "$own_install_root" \
    --dry-run \
    --pretty > "$work_root/agent-skills-own-install.json"
  grep -q '"Command": "skills.install"' "$work_root/agent-skills-own-install.json"
  grep -q '"Status": "ok"' "$work_root/agent-skills-own-install.json"
  grep -q '"DryRun": true' "$work_root/agent-skills-own-install.json"
)

echo "NuGet packages verified: $package_dir"
