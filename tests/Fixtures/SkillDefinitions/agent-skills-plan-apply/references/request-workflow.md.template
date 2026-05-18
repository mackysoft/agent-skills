# Request Workflow

Use this reference when constructing or applying a Agent Skills JSON request.

## Request Construction
1. Start from the user's intended outcome, not from a guessed operation shape.
2. Read enough project state to identify context, target selector, and save boundary.
3. Run `agent-skills ops describe <opName>` for each primitive operation being used.
4. Prefer one focused request over a broad batch unless the edits share one review boundary.
5. Keep request metadata stable and let the CLI supply internal protocol fields.

## Execution
1. Run `agent-skills validate` and fix static contract errors first.
2. Run `agent-skills plan` to preview targets, changed state, touched contexts, and plan token behavior.
3. Run `agent-skills call` only after reviewing the plan.
4. Preserve returned evidence, especially `payload.opResults[].applied`, `changed`, `touched`, and any error payload.
5. When `readPostcondition` is present, perform the requested read before reporting final state.

## Failure Handling
- Parse and validation failures mean the request has not reached Unity execution.
- Lifecycle, disconnect, crash, and `IPC_TIMEOUT` failures can still include partial or applied results.
- Retry only after checking returned evidence and logs. Avoid blind replay of mutating requests.
