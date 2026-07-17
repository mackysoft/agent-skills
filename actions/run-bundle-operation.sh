#!/usr/bin/env bash
set -euo pipefail

operation="${1-}"
case "${operation}" in
  verify|sync)
    ;;
  *)
    echo "Unsupported Agent Skills bundle operation: ${operation:-<empty>}" >&2
    exit 1
    ;;
esac

if [[ -z "${AGENT_SKILLS_ROOT:-}" \
  || "${AGENT_SKILLS_ROOT}" == /* \
  || "${AGENT_SKILLS_ROOT}" == \\* \
  || "${AGENT_SKILLS_ROOT}" =~ ^[[:alpha:]]: \
  || "${AGENT_SKILLS_ROOT}" == *$'\n'* \
  || "${AGENT_SKILLS_ROOT}" == *$'\r'* ]]; then
  echo "The root input must be a non-empty path relative to the GitHub workspace." >&2
  exit 1
fi

workspace_root="$(pwd -P)"
if ! bundle_root="$(cd -- "${AGENT_SKILLS_ROOT}" && pwd -P)"; then
  echo "The bundle root does not exist: ${AGENT_SKILLS_ROOT}" >&2
  exit 1
fi

case "${bundle_root}" in
  "${workspace_root}"|"${workspace_root}"/*)
    ;;
  *)
    echo "The bundle root must remain inside the GitHub workspace: ${AGENT_SKILLS_ROOT}" >&2
    exit 1
    ;;
esac

if ! repository_root="$(git -C "${bundle_root}" rev-parse --show-toplevel)"; then
  echo "The bundle root must be inside a checked-out Git worktree: ${AGENT_SKILLS_ROOT}" >&2
  exit 1
fi
repository_root="$(cd -- "${repository_root}" && pwd -P)"

if [[ "${bundle_root}" == "${repository_root}" ]]; then
  cli_root="."
elif [[ "${bundle_root}" == "${repository_root}"/* ]]; then
  bundle_relative="${bundle_root#"${repository_root}"/}"
  cli_root="./${bundle_relative}"
else
  echo "The bundle root must remain inside its Git worktree: ${AGENT_SKILLS_ROOT}" >&2
  exit 1
fi

cd -- "${repository_root}"
dotnet tool restore

if [[ "${operation}" == "verify" ]]; then
  dotnet tool run agent-skills -- build --root "${cli_root}" --check
  exit 0
fi

: "${GITHUB_OUTPUT:?The sync action requires GITHUB_OUTPUT.}"
if dotnet tool run agent-skills -- build --root "${cli_root}" --check; then
  echo "changed=false" >> "${GITHUB_OUTPUT}"
  exit 0
fi

if ! git diff --cached --quiet; then
  echo "The sync action requires a clean Git index before generating a commit." >&2
  exit 1
fi

if [[ "${GITHUB_REF:-}" != refs/heads/* ]]; then
  echo "The sync action can commit only from a branch ref." >&2
  exit 1
fi
branch_name="${GITHUB_REF#refs/heads/}"

dotnet tool run agent-skills -- build --root "${cli_root}"

git add --all --force -- "${cli_root}/bundle.json" "${cli_root}/generated"
if git diff --cached --quiet; then
  echo "The Agent Skills CLI reported changes, but no bundle changes were staged." >&2
  exit 1
fi

git config user.name "github-actions[bot]"
git config user.email "41898282+github-actions[bot]@users.noreply.github.com"
git commit -m "chore(agent-skills): sync generated bundle"
git push origin "HEAD:${branch_name}"
echo "changed=true" >> "${GITHUB_OUTPUT}"
