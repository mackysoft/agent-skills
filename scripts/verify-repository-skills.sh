#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=scripts/dotnet-common.sh
source "$script_dir/dotnet-common.sh"

usage() {
  echo "usage: bash scripts/verify-repository-skills.sh [--no-restore]" >&2
}

restore=true
while [ "$#" -gt 0 ]; do
  case "$1" in
    --no-restore)
      restore=false
      shift
      ;;
    *)
      usage
      exit 2
      ;;
  esac
done

run_options=(
  --project "$DOTNET_REPO_ROOT/src/MackySoft.AgentSkills.Cli/MackySoft.AgentSkills.Cli.csproj"
  --configuration Release
)
if [ "$restore" = false ]; then
  run_options+=(--no-restore)
fi

dotnet run "${run_options[@]}" -- build --root "$DOTNET_REPO_ROOT/skills" --check
