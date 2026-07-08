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
rm -f "$package_dir"/MackySoft.AgentSkills."$package_version".nupkg
rm -f "$package_dir"/MackySoft.AgentSkills.Cli."$package_version".nupkg
rm -f "$package_dir"/MackySoft.AgentSkills.Hosting."$package_version".nupkg
rm -f "$package_dir"/MackySoft.AgentSkills.ConsoleAppFramework."$package_version".nupkg

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
if [ ! -f "$library_package" ]; then
  echo "library package was not created: $library_package" >&2
  exit 1
fi

if [ ! -f "$cli_package" ]; then
  echo "CLI package was not created: $cli_package" >&2
  exit 1
fi

if [ ! -f "$hosting_package" ]; then
  echo "hosting package was not created: $hosting_package" >&2
  exit 1
fi

if [ ! -f "$consoleappframework_package" ]; then
  echo "ConsoleAppFramework package was not created: $consoleappframework_package" >&2
  exit 1
fi

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
cp -R "$DOTNET_REPO_ROOT/tests/Fixtures/generated" "$console_consumer_dir/skills"
dotnet add "$console_consumer_dir/console-consumer.csproj" package MackySoft.AgentSkills.ConsoleAppFramework \
  --version "$package_version" \
  --source "$package_dir" >/dev/null
cat > "$console_consumer_dir/Program.cs" <<'CS'
using ConsoleAppFramework;
using MackySoft.AgentSkills.ConsoleAppFramework;
using MackySoft.AgentSkills.Hosting.Composition;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddAgentSkillsCommandRuntime(options =>
{
    options.ProductName = "Smoke CLI";
    options.CatalogId = "com.mackysoft.agent-skills";
    options.DefinedTiers = ["basic", "advanced", "developer"];
    options.PackageBaseDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
});

using var provider = services.BuildServiceProvider();
var previousProvider = ConsoleApp.ServiceProvider;
ConsoleApp.ServiceProvider = provider;

try
{
    var app = ConsoleApp.Create();
    app.RegisterAgentSkillsCommands();
    await app.RunAsync(args, disposeServiceProvider: false);
    return Environment.ExitCode;
}
finally
{
    ConsoleApp.ServiceProvider = previousProvider;
}
CS
dotnet restore "$console_consumer_dir/console-consumer.csproj" \
  --source "$package_dir" \
  --source https://api.nuget.org/v3/index.json >/dev/null
dotnet build "$console_consumer_dir/console-consumer.csproj" --configuration "$configuration" --no-restore >/dev/null
dotnet run \
  --project "$console_consumer_dir/console-consumer.csproj" \
  --configuration "$configuration" \
  --no-build \
  -- skills list --pretty > "$work_root/skills-list.json"
grep -q '"Command": "skills.list"' "$work_root/skills-list.json"
grep -q '"Status": "ok"' "$work_root/skills-list.json"
grep -q '"SkillName": "agent-skills-plan-apply"' "$work_root/skills-list.json"

tool_dir="$work_root/tool"
mkdir -p "$tool_dir"
(
  cd "$tool_dir"
  dotnet new tool-manifest >/dev/null
  dotnet tool install MackySoft.AgentSkills.Cli \
    --version "$package_version" \
    --add-source "$package_dir" >/dev/null

  generated_dir="$work_root/generated"
  dotnet tool run agent-skills -- build \
    --definitionsRoot "$DOTNET_REPO_ROOT/tests/Fixtures/SkillDefinitions" \
    --generatedRoot "$generated_dir" >/dev/null

  diff -ruN "$DOTNET_REPO_ROOT/tests/Fixtures/generated" "$generated_dir"
)

echo "NuGet packages verified: $package_dir"
