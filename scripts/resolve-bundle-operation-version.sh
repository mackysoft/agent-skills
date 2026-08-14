#!/usr/bin/env bash
set -euo pipefail

usage() {
  echo "usage: bash scripts/resolve-bundle-operation-version.sh --operation <verify|verify-release> --source <source-root> --base-ref <git-ref>" >&2
}

operation=""
source_root=""
base_ref=""
while [[ "$#" -gt 0 ]]; do
  case "$1" in
    --operation)
      operation="${2-}"
      shift 2
      ;;
    --source)
      source_root="${2-}"
      shift 2
      ;;
    --base-ref)
      base_ref="${2-}"
      shift 2
      ;;
    *)
      usage
      exit 2
      ;;
  esac
done

if [[ "${operation}" != "verify" && "${operation}" != "verify-release" ]] \
  || [[ -z "${source_root}" || -z "${base_ref}" ]]; then
  usage
  exit 2
fi

if ! command -v jq >/dev/null 2>&1; then
  echo "jq is required to resolve the release bundle version." >&2
  exit 1
fi

repository_root="$(git rev-parse --show-toplevel)"
repository_root="$(cd -- "${repository_root}" && pwd -P)"
if ! resolved_source_root="$(cd -- "${source_root}" && pwd -P)"; then
  echo "The source root does not exist: ${source_root}" >&2
  exit 1
fi

case "${resolved_source_root}" in
  "${repository_root}")
    source_relative=""
    ;;
  "${repository_root}"/*)
    source_relative="${resolved_source_root#"${repository_root}"/}"
    ;;
  *)
    echo "The source root must remain inside the Git worktree: ${source_root}" >&2
    exit 1
    ;;
esac

bundle_path="bundle.json"
if [[ -n "${source_relative}" ]]; then
  bundle_path="${source_relative}/bundle.json"
fi

if ! base_commit="$(git rev-parse --verify --end-of-options "${base_ref}^{commit}")"; then
  echo "The base ref does not resolve to a commit: ${base_ref}" >&2
  exit 1
fi

if ! base_bundle="$(git show "${base_commit}:${bundle_path}" 2>/dev/null)"; then
  echo "The base ref does not contain ${bundle_path}: ${base_ref}" >&2
  exit 1
fi

read_bundle_version() {
  local description="$1"
  jq -er --arg description "${description}" '
    (.bundleVersion // .skillBundleVersion) as $version
    | if ($version | type) == "number"
        and ($version | floor) == $version
        and $version > 0
        and $version <= 2147483647
      then $version
      else error($description + " must contain a positive 32-bit integer bundle version")
      end
  '
}

base_version="$(read_bundle_version "Base bundle" <<< "${base_bundle}")"
current_version="$(read_bundle_version "Current bundle" < "${resolved_source_root}/bundle.json")"

if [[ "${operation}" == "verify" ]]; then
  if [[ "${current_version}" -ne "${base_version}" ]]; then
    echo "Ordinary changes must preserve the base bundle version ${base_version}; current version is ${current_version}." >&2
    exit 1
  fi

  printf '%s\n' "${base_version}"
  exit 0
fi

if [[ "${base_version}" -eq 2147483647 ]]; then
  echo "The base bundle version cannot be incremented beyond 2147483647." >&2
  exit 1
fi

target_version="$((base_version + 1))"
if [[ "${operation}" == "verify-release" ]]; then
  if [[ "${current_version}" -ne "${target_version}" ]]; then
    echo "Current bundle version ${current_version} must equal release target ${target_version}." >&2
    exit 1
  fi

  printf '%s\n' "${target_version}"
  exit 0
fi
