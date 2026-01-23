# DSM Customer Documentation Handoff Packet

## Product summary
DSM (Dynamic Stability Models) in NVL9.DSM.Core is a lightweight envelope system for .NET that wraps every operation result with structured telemetry: timing, error codes, caller context, and trace headers. The output is designed to be logged and analyzed, enabling low-level observability and fast root-cause analysis.

## What a DSM/DSMEnvelope is
- A generic wrapper `DSMEnvelope<T>` around data and execution metadata.
- Each envelope has a unique ID, timestamps, execution time, caller info (class, method, file, line), error state, and optional HTTP trace headers.
- Envelopes can be nested with parent-child IDs for full request traces.

## How it works (lifecycle)
1) Initialize: `InitWithCaller(...)` (recommended for async) or `Init()`
2) Capture headers: `Api-Trace-Id` and `Idempotency-Key-Id` (if available)
3) Execute: `Success(...)` on success, `CaptureException(...)` on failure
4) Optionally “freeze” parent envelopes if an inner operation fails

## Captured data (key fields)
- `UniqueIEID`: GUID + process ID
- `RootEnvelopID`: AppId + host + timestamp + UTC offset + PID
- `ParentEnvelopID`: links nested operations
- `StartTime`, `EndTime`, `ExecutionTime` (ms)
- `Code`: structured status code + message
- `DTOMessage`: human-readable status
- `ApiTraceId`, `IdempotencyKeyId` for distributed tracing
- `CodeBlockInfo`: class, method, file path, line number
- `ValidationErrors`: field-to-error list for structured validation failure output

## Error and validation system
- `DSMEnvelopeCodeEnum` defines standardized codes (success, validation, security, database).
- Validation errors use `API_APPVLD_02020` with HTTP 400.
- Helper `ValidationErrorCollector` supports fluent validation and structured error payloads.

## Distributed tracing and hierarchy
- `DSMEnvelopeManager` provides a stack to track nested envelopes.
- Parent-child relationships are automatic when using the manager.
- Header values propagate from parent to child for trace continuity.
- `FreezeStackWith(...)` locks parent envelopes into a failure state after a downstream error.

## Output formats
- Console output with color-coded status (init, success, error).
- JSON serialization via `ToRawJson()` for logging and analytics pipelines.

## Automation option
- `DSMEnvelopeAttribute` (PostSharp) can auto-wrap methods at entry/exit to eliminate boilerplate.

## AI/MCP/MLM usage positioning
- DSM envelopes create a high-fidelity event stream describing low-level execution, timing, error states, and call context.
- These structured logs can be fed to AI/agent systems or model-based monitoring (MCPs/MLM) to:
  - Detect coding errors quickly
  - Identify suspicious behavior or intrusion signals
  - Surface performance regressions in near real time
  - Build automated diagnostics and recommendations

## Suggested page outline (for marketing doc)
1) What DSMs are (simple explanation + value)
2) How DSM envelopes work (lifecycle + example flow)
3) What gets captured (field list + traceability)
4) Error handling and validation (structured codes + validation errors)
5) Distributed tracing and nested operations (parent/child + freeze)
6) AI/MCP/MLM readiness (structured logs for automated analysis)
7) Use cases (debugging, security, performance, compliance)
8) Quick-start snippet (init, success, error)
9) Integration options (ASP.NET header capture, PostSharp attribute)

## Key facts to keep accurate
- Use `InitWithCaller(...)` for async correctness.
- Captures headers named `Api-Trace-Id` and `Idempotency-Key-Id`.
- `RootEnvelopID` includes app id, host, timestamp, UTC offset, PID.
- Validation errors are collected into a field-to-messages dictionary.
- `ToRawJson()` provides JSON output for logs and pipelines.

## Note for sharing
The root README contains a NuGet API key and should be redacted before any public distribution.
