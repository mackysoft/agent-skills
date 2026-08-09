#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=scripts/dotnet-common.sh
source "$script_dir/dotnet-common.sh"

usage() {
  echo "usage: bash scripts/prepare-self-hosted-cli.sh --version <semver> --output <package-dir>" >&2
}

package_version=""
package_output=""

while [ "$#" -gt 0 ]; do
  case "$1" in
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

if [ -z "$package_version" ] || [ -z "$package_output" ]; then
  usage
  exit 2
fi

cli_project="$DOTNET_REPO_ROOT/src/MackySoft.AgentDistribution.Cli/MackySoft.AgentDistribution.Cli.csproj"
artifacts_path="$(mktemp -d "${TMPDIR:-/tmp}/agent-distribution-self-host.XXXXXX")"
trap 'rm -rf "$artifacts_path"' EXIT

dotnet restore "$cli_project" --artifacts-path "$artifacts_path"
dotnet pack "$cli_project" \
  --configuration Release \
  --no-restore \
  --artifacts-path "$artifacts_path" \
  -p:Version="$package_version" \
  -p:PackageVersion="$package_version" \
  --output "$package_output"

dotnet nuget add source "$package_output" --name agent-distribution-self
dotnet tool update MackySoft.AgentDistribution.Cli \
  --local \
  --version "$package_version" \
  --add-source "$package_output" \
  --allow-downgrade
