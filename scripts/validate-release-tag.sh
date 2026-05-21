#!/usr/bin/env bash
set -euo pipefail

usage() {
  echo "usage: bash scripts/validate-release-tag.sh --tag-name <tag> --expected-sha <sha> [--remote <name>]" >&2
}

tag_name=""
expected_sha=""
remote="origin"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --tag-name)
      [[ $# -ge 2 ]] || { usage; exit 2; }
      tag_name="$2"
      shift 2
      ;;
    --expected-sha)
      [[ $# -ge 2 ]] || { usage; exit 2; }
      expected_sha="$2"
      shift 2
      ;;
    --remote)
      [[ $# -ge 2 ]] || { usage; exit 2; }
      remote="$2"
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

if [[ -z "${tag_name}" || -z "${expected_sha}" || -z "${remote}" ]]; then
  usage
  exit 2
fi

if [[ ! "${expected_sha}" =~ ^[0-9a-fA-F]{40}$ ]]; then
  echo "Expected release SHA must be a 40-character Git SHA: ${expected_sha}" >&2
  exit 2
fi

git fetch --force "${remote}" "refs/tags/${tag_name}:refs/tags/${tag_name}"
tag_sha="$(git rev-list -n 1 "refs/tags/${tag_name}")"
if [[ "${tag_sha}" != "${expected_sha}" ]]; then
  echo "Release tag moved after package validation. Expected: ${expected_sha}. Actual: ${tag_sha}" >&2
  exit 1
fi
