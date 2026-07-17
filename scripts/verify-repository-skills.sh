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

temp_root="$DOTNET_REPO_ROOT/obj"
mkdir -p "$temp_root"
work_dir="$(mktemp -d "${temp_root%/}/agent-skills-source-verify.XXXXXX")"
trap 'rm -rf "$work_dir"' EXIT

expected_root="$work_dir/generated"
generate_options=(--output "$expected_root")
if [ "$restore" = false ]; then
  generate_options=(--no-restore "${generate_options[@]}")
fi
bash "$script_dir/generate-repository-skills.sh" "${generate_options[@]}" >/dev/null

if ! diff -ruN "$DOTNET_REPO_ROOT/skills/generated" "$expected_root"; then
  echo "Generated repository skills are out of date. Run: bash scripts/generate-repository-skills.sh" >&2
  exit 1
fi

echo "Generated repository skills are up to date."
