#!/usr/bin/env bash
set -euo pipefail

operation="${1-}"
case "${operation}" in
  verify|sync|release)
    ;;
  *)
    echo "Unsupported Agent Distribution bundle operation: ${operation:-<empty>}" >&2
    exit 1
    ;;
esac

if [[ -z "${AGENT_DISTRIBUTION_ROOT:-}" \
  || "${AGENT_DISTRIBUTION_ROOT}" == /* \
  || "${AGENT_DISTRIBUTION_ROOT}" == \\* \
  || "${AGENT_DISTRIBUTION_ROOT}" =~ ^[[:alpha:]]: \
  || "${AGENT_DISTRIBUTION_ROOT}" == *$'\n'* \
  || "${AGENT_DISTRIBUTION_ROOT}" == *$'\r'* ]]; then
  echo "The root input must be a non-empty path relative to the GitHub workspace." >&2
  exit 1
fi

workspace_root="$(pwd -P)"
if ! bundle_root="$(cd -- "${AGENT_DISTRIBUTION_ROOT}" && pwd -P)"; then
  echo "The bundle root does not exist: ${AGENT_DISTRIBUTION_ROOT}" >&2
  exit 1
fi

case "${bundle_root}" in
  "${workspace_root}"|"${workspace_root}"/*)
    ;;
  *)
    echo "The bundle root must remain inside the GitHub workspace: ${AGENT_DISTRIBUTION_ROOT}" >&2
    exit 1
    ;;
esac

if ! repository_root="$(git -C "${bundle_root}" rev-parse --show-toplevel)"; then
  echo "The bundle root must be inside a checked-out Git worktree: ${AGENT_DISTRIBUTION_ROOT}" >&2
  exit 1
fi
repository_root="$(cd -- "${repository_root}" && pwd -P)"

if [[ "${bundle_root}" == "${repository_root}" ]]; then
  cli_root="."
elif [[ "${bundle_root}" == "${repository_root}"/* ]]; then
  bundle_relative="${bundle_root#"${repository_root}"/}"
  cli_root="./${bundle_relative}"
else
  echo "The bundle root must remain inside its Git worktree: ${AGENT_DISTRIBUTION_ROOT}" >&2
  exit 1
fi

cd -- "${repository_root}"
dotnet tool restore

if [[ "${operation}" == "release" ]]; then
  if [[ ! "${AGENT_DISTRIBUTION_RELEASE_BUNDLE_VERSION:-}" =~ ^[1-9][0-9]*$ ]]; then
    echo "The release action requires a positive exact bundle version." >&2
    exit 1
  fi

  bundle_arguments=(
    tool run agent-distribution -- prepare-release
    --bundle-version "${AGENT_DISTRIBUTION_RELEASE_BUNDLE_VERSION}"
    --root "${cli_root}"
  )
else
  bundle_arguments=(tool run agent-distribution -- build --root "${cli_root}")
fi

if [[ "${operation}" == "verify" ]]; then
  dotnet "${bundle_arguments[@]}" --check
  exit 0
fi

: "${GITHUB_OUTPUT:?The sync and release actions require GITHUB_OUTPUT.}"
if dotnet "${bundle_arguments[@]}" --check; then
  echo "changed=false" >> "${GITHUB_OUTPUT}"
  exit 0
fi

if ! git diff --cached --quiet; then
  echo "The mutating bundle action requires a clean Git index before generating a commit." >&2
  exit 1
fi

if [[ "${GITHUB_REF:-}" != refs/heads/* ]]; then
  echo "The mutating bundle action can commit only from a branch ref." >&2
  exit 1
fi
branch_name="${GITHUB_REF#refs/heads/}"

dotnet "${bundle_arguments[@]}"

git add --all --force -- "${cli_root}/bundle.json" "${cli_root}/generated"
if git diff --cached --quiet; then
  echo "The Agent Distribution CLI reported changes, but no bundle changes were staged." >&2
  exit 1
fi

git config user.name "github-actions[bot]"
git config user.email "41898282+github-actions[bot]@users.noreply.github.com"
if [[ "${operation}" == "release" ]]; then
  commit_message="chore(release): prepare bundle version ${AGENT_DISTRIBUTION_RELEASE_BUNDLE_VERSION}"
else
  commit_message="chore(agent-distribution): sync generated bundle"
fi

git commit -m "${commit_message}"
git push origin "HEAD:${branch_name}"
echo "changed=true" >> "${GITHUB_OUTPUT}"
