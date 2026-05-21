#!/usr/bin/env bash
set -euo pipefail

usage() {
  echo "usage: bash scripts/validate-nuget-package-repository-commit.sh --package-id <id> --package-path <path> --expected-commit <sha>" >&2
}

package_id=""
package_path=""
expected_commit=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --package-id)
      [[ $# -ge 2 ]] || { usage; exit 2; }
      package_id="$2"
      shift 2
      ;;
    --package-path)
      [[ $# -ge 2 ]] || { usage; exit 2; }
      package_path="$2"
      shift 2
      ;;
    --expected-commit)
      [[ $# -ge 2 ]] || { usage; exit 2; }
      expected_commit="$2"
      shift 2
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      usage
      exit 2
      ;;
  esac
done

if [[ -z "${package_id}" || -z "${package_path}" || -z "${expected_commit}" ]]; then
  usage
  exit 2
fi

if [[ ! "${expected_commit}" =~ ^[0-9a-fA-F]{40}$ ]]; then
  echo "Expected repository commit must be a 40-character Git SHA: ${expected_commit}" >&2
  exit 2
fi

if [[ ! -f "${package_path}" ]]; then
  echo "NuGet package artifact was not found: ${package_path}" >&2
  exit 1
fi

package_commit="$(
  unzip -p "${package_path}" "*.nuspec" |
    sed -nE 's/.*<repository[^>]* commit="([^"]+)".*/\1/p' |
    head -n 1
)"
if [[ -z "${package_commit}" ]]; then
  echo "NuGet package is missing repository commit metadata: ${package_id}" >&2
  exit 1
fi

if [[ "${package_commit}" != "${expected_commit}" ]]; then
  echo "NuGet package does not match release tag: ${package_id}" >&2
  echo "Expected repository commit: ${expected_commit}" >&2
  echo "Actual repository commit: ${package_commit}" >&2
  exit 1
fi
