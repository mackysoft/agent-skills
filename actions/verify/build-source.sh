#!/usr/bin/env bash
set -euo pipefail

source_root="${AGENT_DISTRIBUTION_SOURCE:-}"
if [[ -z "${source_root}" \
  || "${source_root}" == /* \
  || "${source_root}" == \\* \
  || "${source_root}" =~ ^[[:alpha:]]: \
  || "${source_root}" == *$'\n'* \
  || "${source_root}" == *$'\r'* ]]; then
  echo "The source input must be a non-empty path relative to the GitHub workspace." >&2
  exit 1
fi

workspace_root="$(pwd -P)"
if ! source_path="$(cd -- "${source_root}" && pwd -P)"; then
  echo "The source root does not exist: ${source_root}" >&2
  exit 1
fi

case "${source_path}" in
  "${workspace_root}"|"${workspace_root}"/*)
    ;;
  *)
    echo "The source root must remain inside the GitHub workspace: ${source_root}" >&2
    exit 1
    ;;
esac

: "${RUNNER_TEMP:?The verify action requires RUNNER_TEMP.}"
action_root="$(cd -- "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd -P)"
cli_project="${action_root}/src/MackySoft.AgentDistribution.Cli/MackySoft.AgentDistribution.Cli.csproj"
if [[ ! -f "${cli_project}" ]]; then
  echo "The verify action does not contain its CLI project: ${cli_project}" >&2
  exit 1
fi

dotnet run \
  --project "${cli_project}" \
  --configuration Release \
  -- build \
  --source "${source_path}" \
  --output "${RUNNER_TEMP}/agent-distribution"
