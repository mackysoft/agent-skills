#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
fixture_root="$repo_root/tools/DotmetReportGate/Fixtures"
project="$repo_root/tools/DotmetReportGate/DotmetReportGate.csproj"
no_restore=false

if [ "$#" -eq 1 ] && [ "$1" = "--no-restore" ]; then
  no_restore=true
elif [ "$#" -ne 0 ]; then
  echo "usage: bash scripts/test-dotmet-report-gate.sh [--no-restore]" >&2
  exit 2
fi

temporary_directory="$(mktemp -d)"
trap 'rm -rf "$temporary_directory"' EXIT

run_case() {
  local expected_exit_code="$1"
  local analysis="$2"
  local provenance="${3:-$fixture_root/provenance.json}"
  local actual_exit_code

  set +e
  if [ "$no_restore" = true ]; then
    dotnet run --project "$project" --configuration Release --no-restore -- \
      --analysis "$analysis" \
      --rules "$fixture_root/rules-validation.json" \
      --doctor "$fixture_root/doctor.json" \
      --provenance "$provenance"
  else
    dotnet run --project "$project" --configuration Release -- \
      --analysis "$analysis" \
      --rules "$fixture_root/rules-validation.json" \
      --doctor "$fixture_root/doctor.json" \
      --provenance "$provenance"
  fi
  actual_exit_code=$?
  set -e

  if [ "$actual_exit_code" -ne "$expected_exit_code" ]; then
    echo "Expected gate exit $expected_exit_code for $analysis, but was $actual_exit_code." >&2
    exit 1
  fi
}

warn_analysis="$fixture_root/analysis-warn.json"
run_case 0 "$warn_analysis"
run_case 1 "$fixture_root/analysis-fail.json" "$fixture_root/provenance-fail.json"
run_case 2 "$fixture_root/analysis-incomplete.json"

sed 's/"engineVersion": "0.3.0"/"engineVersion": "0.3.1"/' "$warn_analysis" > "$temporary_directory/engine-mismatch.json"
run_case 2 "$temporary_directory/engine-mismatch.json"

sed 's/"ruleAwareEvaluation": "full"/"ruleAwareEvaluation": "low"/' "$warn_analysis" > "$temporary_directory/comparison-coverage-low.json"
run_case 2 "$temporary_directory/comparison-coverage-low.json"

sed 's/"partialFailures": \[\]/"partialFailures": [{ "code": "RULES_CONFIG_NOT_REPLAYABLE" }]/' "$warn_analysis" > "$temporary_directory/rules-not-replayable.json"
run_case 2 "$temporary_directory/rules-not-replayable.json"

sed 's/"reviewRequired": false/"reviewRequired": true/' "$warn_analysis" > "$temporary_directory/rules-review-required.json"
run_case 2 "$temporary_directory/rules-review-required.json"

sed -e 's/"changeState": "unchanged"/"changeState": "changed"/' -e 's/"changed": false/"changed": true/' "$warn_analysis" > "$temporary_directory/rules-changed.json"
run_case 2 "$temporary_directory/rules-changed.json"

sed 's/"toolRestore": 0/"toolRestore": 1/' "$fixture_root/provenance.json" > "$temporary_directory/provenance-command-failure.json"
run_case 2 "$warn_analysis" "$temporary_directory/provenance-command-failure.json"
run_case 2 "$temporary_directory/missing-analysis.json"
