#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/.." && pwd)"

usage() {
  echo "usage: bash scripts/verify-dotmet-diff.sh [--base-sha <SHA>] [--head-sha <SHA>] [--output <dir>]" >&2
}

failure() {
  echo "dotmet diff verification: $1" >&2
  exit 2
}

require_full_sha() {
  [[ "$1" =~ ^[[:xdigit:]]{40}$ ]]
}

base_sha=""
head_sha=""
output_dir="$repo_root/artifacts/dotmet-diff"

while [ "$#" -gt 0 ]; do
  case "$1" in
    --base-sha)
      [ "$#" -ge 2 ] || { usage; exit 2; }
      base_sha="$2"
      shift 2
      ;;
    --head-sha)
      [ "$#" -ge 2 ] || { usage; exit 2; }
      head_sha="$2"
      shift 2
      ;;
    --output)
      [ "$#" -ge 2 ] || { usage; exit 2; }
      output_dir="$2"
      shift 2
      ;;
    *)
      usage
      exit 2
      ;;
  esac
done

cd "$repo_root"

current_head="$(git rev-parse --verify HEAD^{commit} 2>/dev/null)" \
  || failure "Current HEAD is not a locally resolvable commit. Fetch the required history before running this script."
if [ -n "$head_sha" ]; then
  require_full_sha "$head_sha" \
    || failure "Head '$head_sha' must be a full 40-character commit SHA."
  requested_head="$(git rev-parse --verify "${head_sha}^{commit}" 2>/dev/null)" \
    || failure "Head '$head_sha' is not a local commit. Fetch it before running this script."
  [ "$requested_head" = "$current_head" ] \
    || failure "Head '$head_sha' resolves to $requested_head, but the current checkout is $current_head. Checkout the requested head; this script never changes checkout."
fi

if [ -n "$base_sha" ]; then
  require_full_sha "$base_sha" \
    || failure "Base '$base_sha' must be a full 40-character commit SHA."
  candidate="$(git rev-parse --verify "${base_sha}^{commit}" 2>/dev/null)" \
    || failure "Base '$base_sha' is not a local commit. Fetch it first, for example: git fetch origin $base_sha"
  candidate_reference="$base_sha"
  resolution_method="explicit-base-sha-merge-base"
else
  origin_head_ref="$(git symbolic-ref --quiet refs/remotes/origin/HEAD 2>/dev/null)" \
    || failure "origin's default branch is not available locally. Fetch origin's default branch and set refs/remotes/origin/HEAD before running this script."
  candidate_reference="${origin_head_ref#refs/remotes/}"
  candidate="$(git rev-parse --verify "${candidate_reference}^{commit}" 2>/dev/null)" \
    || failure "Base candidate '$candidate_reference' is not a local commit. Fetch it first, for example: git fetch origin ${candidate_reference#origin/}"
  resolution_method="origin-default-branch-merge-base"
fi

comparison_base="$(git merge-base "$candidate" "$current_head" 2>/dev/null)" \
  || failure "Could not resolve a merge base between candidate '$candidate' and head '$current_head'. Fetch the required history before running this script."

case "$output_dir" in
  /*)
    ;;
  *)
    output_dir="$repo_root/$output_dir"
    ;;
esac
mkdir -p "$output_dir" \
  || failure "Could not create output directory '$output_dir'."
run_dir="$(mktemp -d "$output_dir/run-XXXXXXXX")" \
  || failure "Could not create a new run directory below '$output_dir'."
run_name="$(basename "$run_dir")"

provenance_path="$run_dir/provenance.json"
rules_report="$run_dir/rules-validation.json"
doctor_report="$run_dir/doctor.json"
analysis_report="$run_dir/analysis.json"

set +e
dotnet tool restore
tool_restore_exit=$?
set -e

run_dotmet() {
  dotnet tool run dotmet -- "$@"
}

if [ "$tool_restore_exit" -eq 0 ]; then
  set +e
  run_dotmet rules validate .dotmet/rules.json \
    --repositoryRoot "$repo_root" \
    --outputPath "$rules_report"
  rules_exit=$?
  run_dotmet doctor \
    --repositoryRoot "$repo_root" \
    --solutionPath AgentDistribution.slnx \
    --rulesPath .dotmet/rules.json \
    --comparisonMode git \
    --comparisonBase "$comparison_base" \
    --outputPath "$doctor_report"
  doctor_exit=$?
  run_dotmet analyze \
    --repositoryRoot "$repo_root" \
    --solutionPath AgentDistribution.slnx \
    --rulesPath .dotmet/rules.json \
    --comparisonMode git \
    --comparisonBase "$comparison_base" \
    --cacheMode off \
    --outputPath "$analysis_report"
  analysis_exit=$?
  set -e
else
  echo "dotmet diff verification: dotnet tool restore failed; report gate will classify the missing reports." >&2
  rules_exit=127
  doctor_exit=127
  analysis_exit=127
fi

printf '{\n  "run": "%s",\n  "candidate": "%s",\n  "candidateReference": "%s",\n  "comparisonBase": "%s",\n  "head": "%s",\n  "resolutionMethod": "%s",\n  "commandExitCodes": {\n    "toolRestore": %s,\n    "rulesValidate": %s,\n    "doctor": %s,\n    "analyze": %s\n  }\n}\n' \
  "$run_name" "$candidate" "$candidate_reference" "$comparison_base" "$current_head" "$resolution_method" \
  "$tool_restore_exit" "$rules_exit" "$doctor_exit" "$analysis_exit" > "$provenance_path"

dotnet run --project "$repo_root/tools/DotmetReportGate/DotmetReportGate.csproj" --configuration Release -- \
  --analysis "$analysis_report" \
  --rules "$rules_report" \
  --doctor "$doctor_report" \
  --provenance "$provenance_path"
