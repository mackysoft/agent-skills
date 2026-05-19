#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=scripts/dotnet-common.sh
source "$script_dir/dotnet-common.sh"

usage() {
  echo "usage: bash scripts/verify-packages.sh [--configuration <name>] [--version <semver>] [--output <package-dir>]" >&2
}

configuration="Release"
package_version=""
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

if [[ ! "$package_version" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z][0-9A-Za-z.-]*)?$ ]]; then
  echo "package version must be SemVer without a leading v: $package_version" >&2
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
rm -f "$package_dir"/MackySoft.AgentSkills.Builder."$package_version".nupkg

cd "$DOTNET_REPO_ROOT"
dotnet restore AgentSkills.slnx
dotnet pack src/MackySoft.AgentSkills/MackySoft.AgentSkills.csproj \
  --configuration "$configuration" \
  --no-restore \
  -p:Version="$package_version" \
  -p:PackageVersion="$package_version" \
  --output "$package_dir"
dotnet pack src/MackySoft.AgentSkills.Builder/MackySoft.AgentSkills.Builder.csproj \
  --configuration "$configuration" \
  --no-restore \
  -p:Version="$package_version" \
  -p:PackageVersion="$package_version" \
  --output "$package_dir"

library_package="$package_dir/MackySoft.AgentSkills.$package_version.nupkg"
builder_package="$package_dir/MackySoft.AgentSkills.Builder.$package_version.nupkg"
if [ ! -f "$library_package" ]; then
  echo "library package was not created: $library_package" >&2
  exit 1
fi

if [ ! -f "$builder_package" ]; then
  echo "builder package was not created: $builder_package" >&2
  exit 1
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

tool_dir="$work_root/tool"
mkdir -p "$tool_dir"
(
  cd "$tool_dir"
  dotnet new tool-manifest >/dev/null
  dotnet tool install MackySoft.AgentSkills.Builder \
    --version "$package_version" \
    --add-source "$package_dir" >/dev/null

  generated_dir="$work_root/generated"
  dotnet tool run agent-skills -- build \
    --definitionsRoot "$DOTNET_REPO_ROOT/tests/Fixtures/SkillDefinitions" \
    --generatedRoot "$generated_dir" >/dev/null

  diff -ruN "$DOTNET_REPO_ROOT/tests/Fixtures/generated" "$generated_dir"
)

echo "NuGet packages verified: $package_dir"
