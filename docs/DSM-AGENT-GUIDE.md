# DSM Integration Guide for Claude and Other Coding Agents

This is a standalone handoff for an agent integrating DSM into another application. It describes the implementation in this repository, including its limitations. Read the installed package's source or API surface before applying it to another version.

Source context: `feature/AIModels`, reviewed November 12, 2026. The core project declares package `NVL9.DSM.Core` version `1.0.1`, targeting `net9.0`. These are repository declarations, not a claim about the latest published package.

## 1. The purpose of DSM

**DSM means Dynamic Stability Models.** This implementation is a .NET library for representing operation results together with structured execution metadata.

Three concepts must remain distinct:

- **Function or method:** code that performs work.
- **Operation:** one instrumented execution of that code.
- **Envelope:** `DSMEnvelope<T>`, the object that carries the result and describes that execution.

For example, a person lookup returns a `PersonDto`. A DSM-instrumented lookup can return `DSMEnvelope<PersonDto>`: its `Value` holds the person, while other fields describe status, duration, source location, and relationships to other executions. Calling the method twice normally creates two different envelopes.

DSM records support diagnosis, request correlation, execution graphs, performance analysis, and downstream statistical models. DSM does not itself implement your business rules, authentication, retry policy, idempotency enforcement, or outbound HTTP propagation.

### What “Pure Function” means in older material

Earlier project notes call instrumented operations “Pure Functions.” In standard programming terminology, a pure function returns the same result for the same inputs and has no observable side effects. DSM also instruments database access, HTTP calls, and other operations with side effects. Wrapping a method does not make it pure.

Use **operation** in general integration code and documentation. Treat “an envelope is a snapshot of a Pure Function” as historical terminology for recording an operation's state. Timestamps, random identifiers, logging, and persistence are effects of the instrumentation.

Deterministic results do not imply deterministic execution time. Performance predictions estimate duration from measurements and context; they do not establish purity or correctness.

## 2. Integration instructions for the receiving agent

When asked to add DSM to another project:

1. Inspect that project's framework, installed DSM version, response contracts, logging, error handling, and dependency injection conventions.
2. Configure application information before constructing any envelopes.
3. Start with explicit `InitWithCaller(...)`, success/error handling, and deliberate output. Use the class-name overload for reliable caller metadata in async methods.
4. Create an envelope per operation, not a singleton envelope or a cached envelope reused across requests.
5. Choose instrumentation boundaries: controllers, application services, or selected expensive/failure-prone operations. Instrumenting every trivial helper increases output without necessarily improving diagnosis.
6. Preserve the application's public response contract unless changing it is part of the task. Internal envelopes do not require exposing every telemetry field to clients.
7. Pass parent IDs and trace context explicitly for concurrent request handling. The current global manager stack is not request-isolated.
8. Add persistence only when requested or required. Logging and envelope use work without Cosmos DB.
9. If persisted request graphs are needed, normalize documents as described below; the current manager and analytics do not use compatible identifier conventions automatically.
10. Verify success, expected failure, unexpected exception, and concurrent-request behavior appropriate to the integration. Validate against the installed API rather than copying an old sample blindly.

The examples below distinguish **existing DSM APIs** from **application code**. Do not assume example services or context objects ship with the library.

## 3. Installation and application information

For a compatible .NET project, the package declaration in this repository is:

```powershell
dotnet add package NVL9.DSM.Core --version 1.0.1
```

Alternatively, use a project reference when integrating from this source tree. Confirm package availability and framework compatibility in the target environment.

Common namespaces:

```csharp
using NVL9.DSM.Core;
using NVL9.DSM.Core.Codes;
using NVL9.DSM.Core.Models;
// Optional persistence:
using NVL9.DSM.Core.Persistence;
using NVL9.DSM.Core.Persistence.CosmosDB;
```

Add application identity to the host's configuration:

```json
{
  "DSMSettings": {
    "CP": {
      "APPLICATION_ID": "PeopleApi",
      "HOST_NAME": "people-api-instance-01"
    }
  }
}
```

In an ASP.NET Core `Program.cs`, initialize DSM from the existing host configuration before creating envelopes:

```csharp
var builder = WebApplication.CreateBuilder(args);

ApplicationInformationManager.SetApplicationInformation(
    new ApplicationInformation(builder.Configuration));

builder.Services.AddControllers();
// Register application services here.

var app = builder.Build();
app.MapControllers();
app.Run();
```

This uses the host's normal configuration sources. Without explicit initialization, DSM lazily loads a required `appsettings.json` from `Directory.GetCurrentDirectory()`. A missing file can therefore break the first envelope initialization. Missing identity settings resolve to `NOTSET`. Supply meaningful application/instance values rather than relying on those defaults.

## 4. Envelope fields and lifecycle

| Member | Meaning and usage |
|---|---|
| `Value` | The result of type `T`; assigned by `Success(value)` |
| `Code` | A `DSMEnvelopeCode` object, not a string or enum |
| `DTOMessage` | Specific operation message; exception capture assigns `ex.Message` |
| `UniqueIEID` | A GUID plus generated numeric suffix, unique per envelope instance |
| `RootEnvelopID` | A readable ID generated per envelope; also the default persistence partition key |
| `ParentEnvelopID` | Parent relationship; conventions differ between manager and analytics, as explained below |
| `ErrorIEID` | Identifies an error-origin envelope; some propagation helpers copy it |
| `StartTime`, `EndTime` | `DateTimeOffset` execution timestamps |
| `ExecutionTime` | Elapsed milliseconds as a string, populated by completion-related methods |
| `CodeBlockInfo` | Class, method, source file, and line identifying the instrumented code |
| `ApiTraceId`, `IdempotencyKeyId` | Optional values captured from request headers or set explicitly |
| `ValidationErrors` | Optional field-to-message-list dictionary |
| `FreezeStatus` | Used by success/stack helpers; not a general immutability guarantee |

The numeric suffix in envelope IDs is generated randomly. Despite names such as `_pid` in the implementation and “PID” in older descriptions, it is not the OS process ID. `RootEnvelopID` is not a stable method identifier; use source metadata to identify the method and `UniqueIEID` to distinguish executions.

Typical lifecycle:

```text
Initialize envelope -> GEN_COMMON_00001 (in flight)
Execute operation
  -> Success(value)          -> GEN_COMMON_00000
  -> ValidationFailed(...)  -> API_APPVLD_02020
  -> CaptureException(ex)   -> exception mapping described below
Emit/serialize the completion state
Optionally persist once
```

### Exact behavior of important methods

| Method | Changes state/result | Captures completion timing | Prints by default |
|---|---|---|---|
| `InitWithCaller(...)` | Creates initialized envelope | Starts stopwatch and sets `StartTime` | Yes; `printEnvelop: false` suppresses it |
| `Success(value)` | Sets `Value`; sets success only if initialized and not frozen | Yes | Yes; `outputEnvelop: false` suppresses it |
| `Success()` | Same status rule, without assigning a value | Yes | Yes |
| `CaptureException(ex)` | Sets error code, error ID, and message | Yes | No |
| `SetValidationErrors(...)` / `ValidationFailed(...)` with errors | Sets validation code and details | Yes | Yes |
| `SetState(code, message)` | Assigns code/message directly | No | Yes |
| `PrintEnvelop()` / `FinishLifeCycle()` | Emits the current state | No | Yes |
| `ToRawJson()` | Returns serialized current state | No | No |

Timing capture reads the stopwatch's elapsed value and sets `EndTime`; the current code does not stop the stopwatch. Calling a completion method later can therefore recalculate a longer duration. Treat completion as a single deliberate action.

`Success(...)` does not clear a prior failure status, but `Success(value)` still assigns `Value` and captures timing even when status is frozen. Other mutators do not universally honor `FreezeStatus`; do not use freezing as an immutable-object contract. `SetState(...)` does not finalize timing. If a custom business outcome needs both a code and duration, implement and verify an explicit application completion policy rather than assuming `SetState` handles both.

### Snapshot semantics

An envelope is mutable in memory. A log entry or serialized document is a snapshot at emission time. Multiple console records may describe the same `UniqueIEID` at entry and completion. Holding a reference to the envelope is not a frozen snapshot.

`ToRawJson()` serializes the current object with Newtonsoft.Json. It is not the same schema as `DSMEnvelopeDocument`: the latter flattens source/status metadata and adds persistence fields. Use `ToRawJson()` for serialization rather than assuming the `RawJson` property refreshes itself.

## 5. Working example: controller and service

This example uses explicit parent and trace values, avoiding the global manager. The service's data is deliberately synthetic so the example has no database dependency. Replace that one lookup with the application's real operation. Register `PersonService` with `builder.Services.AddScoped<PersonService>()` in the host shown above.

Place the following types in the target web project; the record and service are **application code**, not DSM library types:

```csharp
using Microsoft.AspNetCore.Mvc;
using NVL9.DSM.Core;
using NVL9.DSM.Core.Models;

public record PersonDto(int Id, string Name);

public sealed class PersonService
{
    public async Task<DSMEnvelope<PersonDto>> GetByIdAsync(
        int id,
        string parentUniqueId,
        string? apiTraceId,
        string? idempotencyKeyId)
    {
        var envelope = DSMEnvelope<PersonDto>.InitWithCaller(
            nameof(PersonService), printEnvelop: false);

        // Analytics-compatible parent link; does not change RootEnvelopID.
        envelope.SetParentID(parentUniqueId);
        envelope.SetApiTraceId(apiTraceId);
        envelope.SetIdempotencyKeyId(idempotencyKeyId);
        envelope.PrintEnvelop();

        try
        {
            // Demo only: replace with real application data access.
            var person = await Task.FromResult(new PersonDto(id, "Ada"));
            return envelope.Success(person);
        }
        catch (Exception ex)
        {
            envelope.CaptureException(ex);
            envelope.PrintEnvelop();
            throw; // Caller records the exception in its own envelope.
        }
    }
}

[ApiController]
[Route("api/people")]
public sealed class PeopleController : ControllerBase
{
    private readonly PersonService _people;

    public PeopleController(PersonService people) => _people = people;

    [HttpGet("{id:int}")]
    public async Task<ActionResult<DSMEnvelope<PersonDto>>> GetById(int id)
    {
        var envelope = DSMEnvelope<PersonDto>.InitWithCaller(
            nameof(PeopleController), printEnvelop: false);
        envelope.CaptureAndSetHeaders(HttpContext, printEnvelop: false);
        envelope.PrintEnvelop();

        var validation = new ValidationErrorCollector()
            .AddErrorIf(id <= 0, "id", "Id must be greater than zero.");

        if (validation.HasErrors())
            return BadRequest(envelope.ValidationFailed(validation));

        try
        {
            var lookup = await _people.GetByIdAsync(
                id, envelope.UniqueIEID,
                envelope.ApiTraceId, envelope.IdempotencyKeyId);

            return Ok(envelope.Success(lookup.Value));
        }
        catch (Exception ex)
        {
            envelope.CaptureException(ex);
            envelope.PrintEnvelop();
            return StatusCode(500, envelope);
        }
    }
}
```

The example service either returns success or throws. If your real service returns failure envelopes instead, the controller must check `IsSuccessful()` and preserve that outcome rather than blindly wrapping `lookup.Value` as success. Define this result-versus-exception contract consistently across layers.

The example exposes envelopes to illustrate the structure. In an existing public API, consider keeping DSM internally and returning the established response DTO. Envelope serialization includes metadata such as file paths, messages, and payloads; choose intentionally what your clients and logs receive.

No persistence occurs in this example. The two envelopes retain different `RootEnvelopID` values even though their parent link is compatible with the analytics tree builder.

## 6. Status codes, HTTP responses, and exceptions

Enum names contain underscores; serialized/status strings contain hyphens:

```csharp
var code = DSMEnvelopeCodeManager.Manager.Find(
    DSMEnvelopeCodeEnum.API_APPVLD_02010);

// code.StatusCode and code.ToString(): "API-APPVLD-02010"
// code.HttpStatus: "404" (a string)
// code.Code: 2010 (an integer)
```

Selected registered codes:

| Enum | Meaning | Registered HTTP string |
|---|---|---|
| `GEN_COMMON_00000` | Success | `200` |
| `GEN_COMMON_00001` | Initialized/in flight | null |
| `API_APPVLD_02000` | General application validation error | `404` |
| `API_APPVLD_02001` | Invalid application arguments | `404` |
| `API_APPVLD_02010` | No records found | `404` |
| `API_APPVLD_02020` | Structured validation failure | `400` |
| `API_COMMON_01000` | Authentication failure | `401` |
| `API_DATABASE_03020` | Database execution error | `500` |
| `API_DATABASE_03022` | Access denied | `403` |

DSM does not set the HTTP response status automatically. The controller or middleware chooses `Ok`, `BadRequest`, `NotFound`, or another response. Preserve the target application's intentional mappings rather than assuming the registered HTTP string always fits its contract.

Specific implementation details:

- `CaptureException(Exception)` maps to `API_APPVLD_02000`, even for unexpected exceptions; its registered HTTP string is `404`. The controller example deliberately returns HTTP 500 for an unexpected exception. If clients consume both, define an application mapping that keeps them consistent.
- `CaptureException(SqlException)` maps to `API_DATABASE_03020`. C# selects the overload using the argument's compile-time type: catching everything as `Exception` invokes the generic overload unless you explicitly dispatch SQL exceptions.
- `CaptureException` returns `void`. Do not chain it or assume it prints/freezes/persists.
- `IsSuccessful()` compares against the registered success object. Obtain codes through `DSMEnvelopeCodeManager.Manager.Find(...)` rather than constructing lookalike success objects.
- `Code.ErrorMessage` is the registered generic message; `DTOMessage` describes the particular operation. A normal failure envelope does not require an exception to exist.

For domain outcomes, `SetState(...)` accepts the registered code object and a message. Remember that it prints but does not capture completion timing. `ReBase(...)` and `ExecuteTry(...)` also change relationships: `ReBase` writes the child's parent link using the current envelope's `RootEnvelopID`. They are not a substitute for the explicit analytics-compatible linking policy above.

## 7. Validation

`ValidationErrorCollector` supports required values, email, length, conditional errors, and explicit messages:

```csharp
var errors = new ValidationErrorCollector()
    .ValidateRequired(email, "email")
    .ValidateEmail(email, "email")
    .ValidateLength(name, "name", minLength: 2, maxLength: 100);

if (errors.HasErrors())
{
    envelope.ValidationFailed(errors);
    // A controller can now return BadRequest(envelope).
}
```

Here `email`, `name`, and `envelope` are application values. `ValidationFailed` stores the errors, assigns `API_APPVLD_02020`, captures duration, and prints by default. `ValidationErrors` is `Dictionary<string, List<string>>`.

`AddValidationError(...)` also updates validation status/timing; adding several errors can capture duration more than once. `ClearValidationErrors()` clears the collection but does not reset the code to success. Do not assume clearing the messages repairs a failed operation.

## 8. Tracing, hierarchy, and concurrency

Incoming headers recognized by DSM are `Api-Trace-Id` and `Idempotency-Key-Id`. `CaptureAndSetHeaders(HttpContext)` copies their values. It does not create missing headers, enforce idempotency, or send headers on outgoing requests. An HTTP client adapter must explicitly propagate the values when calling another service.

For a request hierarchy, the conceptual shape is:

```text
PeopleController.GetById             UniqueIEID = A
  PersonService.GetByIdAsync         UniqueIEID = B, parent = A
    Database operation              UniqueIEID = C, parent = B
```

### Current manager behavior

`DSMEnvelopeManager.Instance` owns a single process-wide `Stack<IDSMEnvelope>`. It is not scoped to an HTTP request and does not use `AsyncLocal` or synchronization. Overlapping requests or parallel tasks can interfere with nesting, headers, pops, and failure propagation. Prefer explicit context passing for concurrent applications until a request-isolated manager is implemented and tested.

`InitEnvelopeWithCaller(...)` creates and pushes an envelope. A matching `PopEnvelope()` is the caller's responsibility, normally in `finally`, if using the manager manually. Plain `DSMEnvelope<T>.InitWithCaller(...)` does not push to the stack. `CaptureException(...)` does not pop it.

`FreezeStackWith(sender)` copies failure information through managed stack entries. It operates on that shared stack; it is not automatic for manual envelopes and should not be used as cross-request failure handling.

### Identifier mismatch that affects stored graphs

| Concern | Current producer behavior | Consumer expectation |
|---|---|---|
| Parent link | Manager sets child `ParentEnvelopID = parent.RootEnvelopID` | Analytics and repository parent lookups expect `parent.UniqueIEID` |
| Request grouping | Each envelope generates its own `RootEnvelopID`; manager leaves it unchanged | Request graph queries expect all documents to share one `RootEnvelopID` |
| Document lookup | Document ID derives from `UniqueIEID` | Parent lookups use that ID within the shared request partition |

Setting `SetParentID(parent.UniqueIEID)` solves the tree-builder parent convention only. There is no public envelope setter for sharing `RootEnvelopID`. For persisted graphs, normalize writable `DSMEnvelopeDocument` fields through an application adapter, as illustrated below. Do not claim that the current manager automatically produces complete request graphs.

## 9. Optional persistence

Persistence is optional and explicit. `IDSMEnvelopeRepository` separates storage from envelope creation. The included implementation is `CosmosDBEnvelopeRepository`.

The following startup fragment assumes the host already has configuration for an existing intended Cosmos account. Calling `CreateAsync` may create its database/container if absent; this is application setup code, not a prerequisite for basic DSM usage.

```csharp
var repository = await CosmosDBEnvelopeRepository.CreateAsync(
    builder.Configuration["CosmosDB:ConnectionString"]
        ?? throw new InvalidOperationException("Missing CosmosDB connection string"),
    databaseName: "DSMEnvelopeDB",
    containerName: "Envelopes");

builder.Services.AddSingleton<IDSMEnvelopeRepository>(repository);
DSMEnvelopePersistenceExtensions.ConfigureRepository(repository);
```

The extension configuration is static/process-wide. Configure once at startup. Do not use it as if it selected a different repository automatically for each tenant/request.

Built-in helpers:

```csharp
// Alternative completion paths, not sequential saves of one envelope:
await envelope.SuccessAndPersistAsync(result, includePayload: false);
// OR, from an exception path:
await envelope.CaptureExceptionAndPersistAsync(exception);
```

These snippets assume an envelope, result, and exception supplied by application code. `SuccessAndPersistAsync` includes the payload by default; passing `includePayload: false` omits it. Both helpers return a `DSMEnvelopeDocument`, not the envelope. `CaptureExceptionAndPersistAsync` accepts `Exception`, so it uses the generic exception mapping internally.

The lower-level generic call is `await envelope.PersistAsync<PersonDto>()`; the type parameter matters to metadata extraction and may need to be explicit when no payload is supplied.

### Storage identity and snapshot history

- `DSMEnvelopeDocument.FromEnvelope<T>(...)` projects the current state into a document. Use the envelope's actual payload type for `T` so its concrete cast can extract source metadata.
- `Id` is `UniqueIEID.Replace("|", "_")`.
- `PartitionKey` defaults to `RootEnvelopID`; the container uses the single path `/partitionKey`, despite “hierarchical partition” wording in older comments.
- `SaveAsync` and `SaveBatchAsync` create items, rather than upserting them.
- Saving the same envelope twice in the same partition conflicts. The default repository does not retain entry/completion histories or update the first save.
- Persist once at completion unless you implement a separate event identity/storage strategy. Retrying after an uncertain write outcome also needs an application policy for duplicate IDs.
- The persistence document has numeric `ExecutionTimeMs`, flattened status/source metadata, and optional `PayloadSnapshot`. It does not preserve every field of the raw envelope; for example, it has no structured `ValidationErrors` property.

Keep business execution and telemetry persistence failures distinguishable. A failed telemetry write after a successful operation should follow an explicit application policy; do not blindly reinterpret it as business failure or retry an already-completed business action. Decide whether persistence is required for the transaction or best-effort telemetry.

### Example application adapter for normalized graph documents

This helper is **proposed application code**, not a built-in DSM method. It uses existing public document properties and repository APIs. The caller supplies one request grouping key, the parent execution ID, and the correct depth. A request grouping key can be the root envelope's readable ID, passed explicitly to all child operations.

```csharp
public static async Task<DSMEnvelopeDocument> SaveCompletedAsync<T>(
    IDSMEnvelopeRepository repository,
    DSMEnvelope<T> envelope,
    string requestRootId,
    string? parentUniqueId,
    int depth)
{
    if (envelope.IsInInitializedState())
        throw new InvalidOperationException("Complete the operation before saving.");

    var document = DSMEnvelopeDocument.FromEnvelope<T>(envelope);
    document.RootEnvelopID = requestRootId;
    document.PartitionKey = requestRootId;
    document.ParentEnvelopID = parentUniqueId ?? string.Empty;
    document.Depth = depth;

    return await repository.SaveAsync(document);
}
```

For a root use `parentUniqueId: null`, `depth: 0`; for its child use the root's `UniqueIEID`, `depth: 1`. Every document in the request must receive the same `requestRootId`. This normalizes persisted documents; it does not change the original envelope's `RootEnvelopID` or its console records. Treat normalization as part of the application's telemetry contract.

The built-in persistence extension computes depth by querying for a parent in the child's partition and falls back to zero if none is found. Unnormalized identifiers or persisting a child before its parent can therefore produce incorrect depths. The explicit-depth adapter avoids that dependency; it still requires correct caller context and one save per envelope.

## 10. Analytics and performance prediction

For consistently normalized completion documents:

```csharp
using NVL9.DSM.Core.Analytics;

var documents = await repository.GetByRootEnvelopIDAsync(requestRootId);
if (documents.Count > 0)
{
    var analytics = new DSMEnvelopeAnalytics();
    var analysis = analytics.BuildExecutionGraph(documents);
    var mermaid = analytics.ExportToMermaid(analysis);
    // Store or display mermaid through the application's chosen output.
}
```

`repository` and `requestRootId` are application values. The graph builder links `ParentEnvelopID` to `UniqueIEID`. Supply a complete, acyclic request tree with one root. It does not validate arbitrary cycles before recursively building the tree. Missing parents can leave nodes disconnected even when `TotalNodes` counts their documents.

Analysis provides error lists, depth, bottlenecks, a computed critical path, and method statistics. Interpret duration carefully: parent elapsed time includes awaited child time, so summing nested durations double-counts work. The current critical-path calculation is a tree-based duration heuristic, not a full causal scheduler analysis for parallel/distributed execution. Analyze completed records; in-flight envelopes do not yet have final timing.

The companion `NVL9.DSM.ML.Prediction` project and `ML.Training.Python` directory support duration prediction from method identity, call depth, time context, historical averages/deviation/P95, success rate, and popularity. This capability is separate from core DSM integration. It requires model artifacts and compatible feature encoding/order/scaling; envelopes alone do not train or deploy a model. Evaluate with the target application's data rather than treating accuracy claims from examples or synthetic data as guarantees.

## 11. PostSharp instrumentation

`DSMEnvelopeAttribute` is an optional method-boundary aspect intended to automate envelope entry/exit handling. Do not assume the attribute is a drop-in replacement for the explicit pattern in this guide.

In this source:

- `OnEntry` uses `Activator.CreateInstance(type, methodArguments)`, while `DSMEnvelope<T>` has private constructors and is normally created through its static factories. Verify compatible construction before relying on the aspect.
- `OnSuccess` calls the non-generic interface `Success()`; it does not assign the method's return value into `Value`.
- `OnException` sets `FlowBehavior.Return`, which can suppress the original exception and alter application behavior.
- The aspect uses the shared manager stack and inherits its concurrency/linking limitations.
- Build-time PostSharp setup and async method behavior need verification in the target project.

Prefer the explicit pattern until these points are resolved for the actual integration. This guide does not modify the aspect or manager implementation.

## 12. Troubleshooting and verification

| Symptom | Check |
|---|---|
| First envelope throws while initializing | Explicitly configure `ApplicationInformationManager`; otherwise a working-directory `appsettings.json` is required |
| Method appears as `MoveNext` or metadata is wrong | Use the class-name `InitWithCaller` overload directly at the instrumented call site; wrappers must forward caller information |
| Envelope stays initialized | Make sure every outcome completes with success, validation failure, or explicit failure handling |
| Error is absent from console | `CaptureException` does not print; explicitly emit its state |
| Duration is absent after `SetState` | That method does not calculate timing; `FinishLifeCycle` only prints |
| SQL exception has the general validation code | Check overload selection and whether an extension receives only `Exception` |
| HTTP status disagrees with envelope metadata | HTTP response selection is application-controlled; generic exception mapping is registered as 404 |
| Cosmos save conflicts | Check for repeated saves of the same `UniqueIEID` in one partition |
| Graph has only one node or wrong depths | Check shared document root/partition, parent `UniqueIEID` links, completion state, and parent availability during depth lookup |
| Requests have mixed parents or trace values | Avoid the process-wide manager for overlapping operations; pass request context explicitly |
| Child has no trace header | Headers are not generated automatically; set/propagate the values explicitly |

For an integration change, verify that an operation returns its expected data and successful status, invalid input returns structured field errors, an exception records a failure without being accidentally converted to success, and two concurrent requests retain separate context. For persistence, verify one successful save per envelope and a complete normalized parent/child tree. These checks should exercise the target application's behavior, not merely mirror helper implementations.

## 13. Source map and additional reading

When this guide and another version disagree, inspect these implementation points:

| Source file | What to verify |
|---|---|
| `NVL9.DSM.Core/DSMEnvelope.cs` | Fields, initialization, IDs, and timing calculation |
| `NVL9.DSM.Core/DSMEnvelope.public.cs` | Factory overloads, success, exceptions, validation, printing, serialization |
| `NVL9.DSM.Core/IDSMEnvelope.cs` | Interface members; it omits generic `Value` and some concrete metadata |
| `NVL9.DSM.Core/ApplicationInformation*.cs` | Configuration keys and lazy initialization |
| `NVL9.DSM.Core/DSMEnvelopeManager.cs` | Stack lifetime, linking, propagation, and freeze behavior |
| `NVL9.DSM.Core/Codes/DSMEnvelopeCodeManager.cs` | Registered status strings, messages, and HTTP mappings |
| `NVL9.DSM.Core/Models/ValidationErrorCollector.cs` | Validation helpers |
| `NVL9.DSM.Core/Persistence/DSMEnvelopeDocument.cs` | Projection, JSON names, document ID, partition, and stored fields |
| `NVL9.DSM.Core/Persistence/DSMEnvelopePersistenceExtensions.cs` | Static repository configuration, completion helpers, and depth calculation |
| `NVL9.DSM.Core/Persistence/CosmosDB/CosmosDBEnvelopeRepository.cs` | Create-only saves, point reads, queries, and container setup |
| `NVL9.DSM.Core/Analytics/DSMEnvelopeAnalytics.cs` | Graph linking and analysis algorithms |
| `NVL9.DSM.Core/DSMEnvelopeAttribute.cs` | PostSharp construction and control-flow effects |

Related project documentation: [main README](../README.md), [analytics guide](../README-ANALYTICS.md), [persistence guide](../NVL9.DSM.Core/Persistence/PERSISTENCE-GUIDE.md), and [ML prediction guide](ML-PREDICTION-GUIDE.md). These links are for this repository; the integration instructions above remain useful when this file is copied alone into another project.

## 14. Copyable instruction for Claude

> Use this guide as context for integrating NVL9.DSM into this application. Inspect the installed version and existing architecture before editing. Configure application identity, create explicit per-operation envelopes, use the async-friendly caller factory, preserve success/failure semantics, and emit completion records deliberately. Pass trace and parent context explicitly for concurrent work. Keep existing public API contracts unless the task calls for a change. Persistence is optional; if requested, save once at completion and reconcile document grouping/parent conventions before using graph analytics. Separate proposed application adapters from existing DSM APIs. Report what changed, what was verified, and any implementation limitations relevant to the integration.
