#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=scripts/dotnet-common.sh
source "$script_dir/dotnet-common.sh"

usage() {
  echo "usage: bash scripts/generate-repository-skills.sh [--no-restore] [--output <generated-skills-dir>]" >&2
}

restore=true
output_root="$DOTNET_REPO_ROOT/skills/generated"

while [ "$#" -gt 0 ]; do
  case "$1" in
    --no-restore)
      restore=false
      shift
      ;;
    --output)
      if [ "$#" -lt 2 ]; then
        usage
        exit 2
      fi
      output_root="$2"
      shift 2
      ;;
    --output=*)
      output_root="${1#--output=}"
      shift
      ;;
    *)
      usage
      exit 2
      ;;
  esac
done

case "$output_root" in
  /*)
    ;;
  *)
    output_root="$DOTNET_REPO_ROOT/$output_root"
    ;;
esac

output_parent="$(dirname "$output_root")"
mkdir -p "$output_parent"
output_parent="$(cd "$output_parent" && pwd -P)"
output_root="$output_parent/$(basename "$output_root")"

if [ "$output_root" = "$DOTNET_REPO_ROOT" ]; then
  echo "output path must not be the repository root." >&2
  exit 2
fi

case "$output_root/" in
  "$DOTNET_REPO_ROOT"/*)
    ;;
  *)
    echo "output path must stay under the repository root: $output_root" >&2
    exit 2
    ;;
esac

if [ -L "$output_root" ]; then
  echo "output path must not be a symbolic link: $output_root" >&2
  exit 2
fi

run_options=(
  --project "$DOTNET_REPO_ROOT/src/MackySoft.AgentSkills.Cli/MackySoft.AgentSkills.Cli.csproj"
  --configuration Release
)
if [ "$restore" = false ]; then
  run_options+=(--no-restore)
fi

dotnet run "${run_options[@]}" -- \
  build \
  --definitionsRoot "$DOTNET_REPO_ROOT/skills/definitions" \
  --generatedRoot "$output_root"
