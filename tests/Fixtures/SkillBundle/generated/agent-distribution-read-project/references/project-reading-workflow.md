# Project Reading Workflow

Use this reference when a task requires Unity project discovery before planning a request.

## Read Order
1. Confirm project resolution with `agent-distribution status` when the target project is ambiguous.
2. Use `agent-distribution ops list` only to discover candidate operation names.
3. Use `agent-distribution ops describe <opName>` before relying on any operation's behavior.
4. Use `agent-distribution query assets find`, `agent-distribution query scene tree`, `agent-distribution query go describe`, `agent-distribution query comp schema`, or `agent-distribution query asset schema` for the narrow state needed by the task.
5. Use `agent-distribution resolve` when a later request needs a stable object reference.

## ReadIndex Handling
- Treat readIndex output as an acceleration layer, not the Unity source of truth.
- Prefer fresh reads when the user asks about current state or when prior commands may have changed the project.
- If a response exposes stale or advisory freshness, state that limitation in the task summary.

## Output
- Report the project path or identity used.
- Report only the selectors, context boundaries, and operation names needed for the next decision.
- Leave exact operation input construction to the request workflow after `agent-distribution ops describe <opName>` has been read.
