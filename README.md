# Nuvol9-DSM

**NVL9.DSM** (Dynamic Stability Models) is a .NET observability library built by **Nuvol 9 Corp**. It wraps every operation result in a structured envelope — `DSMEnvelope<T>` — that carries timing data, caller context, error codes, distributed trace headers, and parent-child relationships. This creates a high-fidelity execution event stream that powers debugging, performance analysis, and AI-driven monitoring.

---

## Table of Contents

- [Nuvol9-DSM](#nuvol9-dsm)
  - [Table of Contents](#table-of-contents)
  - [What is a DSMEnvelope?](#what-is-a-dsmenvelope)
    - [Functions, results, and envelopes](#functions-results-and-envelopes)
    - [What does Pure Function mean here?](#what-does-pure-function-mean-here)
    - [What does snapshot mean?](#what-does-snapshot-mean)
  - [How It Works — Lifecycle](#how-it-works--lifecycle)
    - [Initialization](#initialization)
    - [Success and Error](#success-and-error)
  - [Captured Data](#captured-data)
  - [Error Codes and Validation](#error-codes-and-validation)
    - [Validation Error Collection](#validation-error-collection)
  - [Distributed Tracing and Nested Operations](#distributed-tracing-and-nested-operations)
    - [Identifier scope and current linking behavior](#identifier-scope-and-current-linking-behavior)
    - [Example: a controller calls a service](#example-a-controller-calls-a-service)
  - [Output and Logging](#output-and-logging)
  - [PostSharp Attribute (Zero-Boilerplate)](#postsharp-attribute-zero-boilerplate)
  - [Persistence to Azure Cosmos DB](#persistence-to-azure-cosmos-db)
    - [Setup (`Program.cs`)](#setup-programcs)
    - [Usage](#usage)
  - [Analytics Engine](#analytics-engine)
  - [AI / ML Performance Prediction](#ai--ml-performance-prediction)
  - [Project Structure](#project-structure)
  - [Quick Start](#quick-start)
    - [1. Add the NuGet package](#1-add-the-nuget-package)
    - [2. Instrument a controller](#2-instrument-a-controller)
    - [3. Validate input](#3-validate-input)
  - [NuGet](#nuget)

---

## What is a DSMEnvelope?

A `DSMEnvelope<T>` is a generic wrapper around any return value `T`. Instead of returning a raw object from a method, you return a `DSMEnvelope<T>` that holds both the result and a full execution snapshot:

- **Who called it** — class name, method name, source file path, line number
- **When it ran** — `StartTime`, `EndTime`, and `ExecutionTime` in milliseconds
- **What happened** — structured status code (success, validation failure, auth error, database error, etc.)
- **Trace identity** — unique envelope ID, root envelope ID, parent envelope ID for request-chain reconstruction
- **HTTP trace headers** — `Api-Trace-Id` and `Idempotency-Key-Id` for cross-service correlation
- **Validation errors** — structured field-to-messages dictionary (HTTP 400 pattern)

An envelope can print a color-coded summary to the console and can be serialized to JSON for log pipelines and analytics.

### Functions, results, and envelopes

A function performs the work. Its result is the returned data, such as a person record. A `DSMEnvelope<T>` carries that result in `Value` together with information about the execution: status, timing, source location, and identifiers that connect it to related operations.

An **operation** in this documentation means one instrumented execution of a method or code block. Calling the same method twice creates two operations when each call creates its own envelope. The class and method names identify the code; envelope identifiers distinguish executions of that code.

### What does Pure Function mean here?

Earlier documentation calls instrumented operations **Pure Functions**. In standard programming terminology, a pure function returns the same result for the same inputs and has no observable side effects. DSM can instrument both pure computations and operations that perform I/O, such as database queries or HTTP calls. Wrapping a method in an envelope does not establish purity; generating timestamps and IDs, logging, and persistence are effects of the instrumentation itself.

This documentation uses **operation** for the general case. The earlier phrase “DSMEnvelope as a Pure Function snapshot” describes an envelope recording an operation's execution state; it is not a guarantee of deterministic results or side-effect-free execution.

Deterministic results also do not imply constant execution time. Runtime conditions, scheduling, and resource contention can change duration even when inputs and results are identical. The ML system estimates duration from historical measurements and context; it does not prove correctness or purity.

### What does snapshot mean?

The in-memory envelope is mutable. Initialization sets an in-flight status; `Success(...)`, `CaptureException(...)`, or validation helpers update that same object. A console record or serialized document captures its state when emitted. A previously serialized record does not change when the object changes later.

`InitWithCaller(...)` and `Success(...)` print by default, with options to suppress output. `CaptureException(...)` updates the envelope but does not itself print or freeze the manager stack. Use `PrintEnvelop()` to print the error state and `DSMEnvelopeManager.Instance.FreezeStackWith(envelope)` when stack propagation is required.

Persistence is explicit. `SuccessAndPersistAsync(...)` and `CaptureExceptionAndPersistAsync(...)` save the completion state. The supplied Cosmos repository uses `CreateItemAsync`, with a document ID derived from `UniqueIEID` by replacing `|` with `_`. Saving entry and completion for the same envelope to the same partition therefore conflicts rather than appending a history or updating the first record. Persist once at completion with this repository. Retaining multiple durable snapshots requires a separate event identity/storage strategy that is not implemented here.

---

## How It Works — Lifecycle

```
1. InitWithCaller(...)      → Envelope created, stopwatch started, GEN_COMMON_00001 code set
2. CaptureAndSetHeaders(HttpContext)  → Trace headers captured from incoming HTTP request
3. Success(result)          → Elapsed duration captured, GEN_COMMON_00000 code set, result stored
   — or —
   CaptureException(ex)     → Elapsed duration captured, error code and message set
4. (Optional) PersistAsync()  → Document written to Azure Cosmos DB
```

### Initialization

Use `InitWithCaller` for all methods, especially async ones. It relies on C# compiler attributes (`[CallerMemberName]`, `[CallerFilePath]`, `[CallerLineNumber]`) to capture the real caller, not the async state machine's `MoveNext()`:

```csharp
var envelope = DSMEnvelope<PersonDto>
    .InitWithCaller(nameof(PersonController))
    .CaptureAndSetHeaders(HttpContext);
```

### Success and Error

```csharp
// Success
var person = await _service.GetByIdAsync(id);
envelope.Success(person);
return Ok(envelope);

// Error
catch (Exception ex)
{
    envelope.CaptureException(ex);
    return StatusCode(500, envelope);
}
```

---

## Captured Data

| Property | Description |
|---|---|
| `UniqueIEID` | `{GUID}\|{suffix}` — unique per envelope instance; suffix is a generated random number, not the OS process ID |
| `RootEnvelopID` | Readable ID generated separately for each envelope from application, host, timestamp, UTC offset, and random suffix; also used as the persistence partition key |
| `ParentEnvelopID` | Parent link; the manager writes the parent's `RootEnvelopID`, while analytics expects its `UniqueIEID` (see linking behavior below) |
| `StartTime` / `EndTime` | `DateTimeOffset` timestamps |
| `ExecutionTime` | Milliseconds as a string |
| `Code` | Structured `DSMEnvelopeCode` (code string, numeric value, HTTP status, messages) |
| `DTOMessage` | Human-readable status message |
| `CodeBlockInfo` | Class name, method name, file path, line number |
| `ApiTraceId` | Value of incoming `Api-Trace-Id` HTTP header |
| `IdempotencyKeyId` | Value of incoming `Idempotency-Key-Id` HTTP header |
| `ValidationErrors` | `Dictionary<string, List<string>>` for structured field validation errors |
| `FreezeStatus` | When `true`, the envelope's status cannot be changed further |

---

## Error Codes and Validation

Codes are defined in `DSMEnvelopeCodeEnum` and registered in `DSMEnvelopeCodeManager`:

| Code | HTTP Status | Meaning |
|---|---|---|
| `GEN_COMMON_00000` | 200 | Success |
| `GEN_COMMON_00001` | — | Initialized (in-flight) |
| `API_APPVLD_02000` | 404 | General validation error |
| `API_APPVLD_02001` | 404 | Invalid arguments |
| `API_APPVLD_02010` | 404 | No records found |
| `API_APPVLD_02020` | 400 | Structured validation failure |
| `API_COMMON_01000` | 401 | Authentication failure |
| `API_COMMON_01001` | 401 | Token generation error |
| `API_DATABASE_03020` | 500 | Database execution error |
| `API_DATABASE_03021` | 401 | Session context user not found |
| `API_DATABASE_03022` | 403 | Access denied |
| `API_DATABASE_03023` | 403 | Query returned nothing |

### Validation Error Collection

`ValidationErrorCollector` provides a fluent API for building structured validation payloads:

```csharp
var validator = new ValidationErrorCollector()
    .ValidateRequired(request.Email, "email")
    .ValidateEmail(request.Email, "email")
    .ValidateRequired(request.Phone, "phone")
    .ValidateLength(request.Name, "name", minLength: 2, maxLength: 100);

if (validator.HasErrors())
    return envelope.ValidationFailed(validator);
```

This maps directly to an HTTP 400 response with the `details` dictionary matching the format:

```json
{
  "error": "Validation failed",
  "details": {
    "email": ["Email is required", "Email format is invalid"],
    "phone": ["Phone number is required"]
  },
  "code": "API-APPVLD-02020"
}
```

---

## Distributed Tracing and Nested Operations

`DSMEnvelopeManager` maintains a per-process stack of active envelopes. When a child envelope is pushed, it receives a parent link and copies the parent's `ApiTraceId` and `IdempotencyKeyId`. The intended execution hierarchy is:

```
PersonController.GetById  (Root)
  └─> PersonService.GetByIdAsync  (Child)
       └─> DatabaseQuery.ExecuteAsync  (Grandchild)
```

When an inner envelope fails, `FreezeStackWith(envelope)` propagates the failure state up through all ancestors, preventing any parent from being accidentally marked successful.

### Identifier scope and current linking behavior

The following distinction matters when moving from console traces to persisted graphs:

| Field | Current producer behavior | Analytics/repository expectation |
|---|---|---|
| `UniqueIEID` | A new ID for each envelope instance | Identifies an execution node and supplies the document ID |
| `RootEnvelopID` | Generated for each instance; the manager does not copy the parent's value | A shared request grouping key for single-partition graph queries |
| `ParentEnvelopID` | The manager sets it to the parent's `RootEnvelopID` | `BuildExecutionGraph` matches it to the parent's `UniqueIEID`; repository parent lookups use that ID too |
| `ApiTraceId` | Captured from HTTP headers and copied to managed children | Correlates records across operations/services when consistently propagated |

These producer and consumer conventions currently differ. Automatic manager linking alone does not produce the shared partition key and parent IDs expected by the graph APIs. `SetParentID(parent.UniqueIEID)` can supply the analytics parent link, but there is no public setter to share `RootEnvelopID` across envelopes. A persistence adapter can normalize document fields before saving; the built-in `PersistAsync` extension does not perform that normalization. A request-wide graph query requires consistently grouped documents and matching parent IDs.

### Example: a controller calls a service

This example records two completed operations and returns the service result inside the controller envelope. It explicitly links the child using the ID expected by analytics, without using the manager stack. `PersonDto` and `_people.FindAsync` represent application types and data access.

```csharp
// PersonController
public async Task<DSMEnvelope<PersonDto>> GetPersonById(int id)
{
    var request = DSMEnvelope<PersonDto>.InitWithCaller(nameof(PersonController));
    try
    {
        var lookup = await _personService.GetByIdAsync(id, request.UniqueIEID);
        return request.Success(lookup.Value);
    }
    catch (Exception ex)
    {
        request.CaptureException(ex);
        request.PrintEnvelop();
        return request;
    }
}

// PersonService
public async Task<DSMEnvelope<PersonDto>> GetByIdAsync(int id, string parentId)
{
    var lookup = DSMEnvelope<PersonDto>.InitWithCaller(
        nameof(PersonService), printEnvelop: false);
    lookup.SetParentID(parentId);
    lookup.PrintEnvelop(); // Entry record includes the parent link.
    try
    {
        var person = await _people.FindAsync(id);
        return lookup.Success(person);
    }
    catch (Exception ex)
    {
        lookup.CaptureException(ex);
        lookup.PrintEnvelop();
        throw; // The controller records the failure in its own envelope too.
    }
}
```

Both operations record elapsed duration on success or exception. This example does not persist documents or normalize request grouping keys.

An abbreviated completion record for a successful service call might look like this. This is a projection of selected envelope fields; the full `ToRawJson()` output includes additional metadata and a structured `Code` object.

```json
{
  "UniqueIEID": "22222222-2222-2222-2222-222222222222|12002",
  "RootEnvelopID": "APP001|HOST|2026:11:12:10:00:00:010:-420|12002",
  "ParentEnvelopID": "11111111-1111-1111-1111-111111111111|12001",
  "ExecutionTime": "15",
  "Value": { "Id": 42, "Name": "Ada" },
  "DTOMessage": "Success"
}
```

| Question | Where to look |
|---|---|
| What data came back? | `Value` |
| Did the operation succeed? | `IsSuccessful()` / `Code`; success is `GEN_COMMON_00000` |
| How long did it take? | `ExecutionTime` (milliseconds, recorded at completion) |
| Which execution was this? | `UniqueIEID` |
| Who called it? | `ParentEnvelopID`, matched to the caller's `UniqueIEID` in this example |
| Which code ran? | `CodeBlockInfo` (class, method, file, line) |

---

## Output and Logging

`InitWithCaller(...)` and `Success(...)` emit formatted console blocks by default. Other paths may require an explicit `PrintEnvelop()` call; see [What does snapshot mean?](#what-does-snapshot-mean).

```
|| ********************************************************************************** ||
||                UniqueIEID: a1b2c3d4-...|12345
||           RootEnvelopID: APP001|HOSTNAME|2025:01:15:10:30:00:123:+300|12345
||            Execution Time: 15ms
||                      Code: GEN-COMMON-00000
||              Code Message: Success
||                Class Name: PersonController
||                    Method: GetById
|| ********************************************************************************** ||
```

Call `envelope.ToRawJson()` to get a JSON string suitable for log pipelines, Cosmos DB documents, or AI ingestion.

---

## PostSharp Attribute (Zero-Boilerplate)

`DSMEnvelopeAttribute` is a PostSharp `OnMethodBoundaryAspect` that instruments methods without any manual envelope code:

```csharp
[DSMEnvelope(typeof(DSMEnvelope<PersonDto>))]
public PersonDto GetById(int id)
{
    // No envelope code needed — OnEntry/OnSuccess/OnException handle it
    return _repo.Find(id);
}
```

The attribute handles push/pop on the manager stack, header capture (when on a controller), and `FreezeStackWith` on exception.

---

## Persistence to Azure Cosmos DB

The `Persistence/` layer stores envelope documents so the full execution graph of every request is queryable after the fact.

### Setup (`Program.cs`)

```csharp
var repository = await CosmosDBEnvelopeRepository.CreateAsync(
    builder.Configuration["CosmosDB:ConnectionString"],
    "DSMEnvelopeDB",
    "Envelopes"
);
builder.Services.AddSingleton<IDSMEnvelopeRepository>(repository);
DSMEnvelopePersistenceExtensions.ConfigureRepository(repository);
```

### Usage

```csharp
// Persist on success in one call
await envelope.SuccessAndPersistAsync(person);

// Persist on error
await envelope.CaptureExceptionAndPersistAsync(ex);
```

Cosmos DB uses `RootEnvelopID` as the partition key. An entire request chain occupies one partition only if its documents share that value. The built-in producer currently generates it per envelope; see [Identifier scope and current linking behavior](#identifier-scope-and-current-linking-behavior). The repository creates one document per saved envelope, rather than a sequence of entry/exit snapshots.

See [docs/COSMOS-DB-SETUP.md](docs/COSMOS-DB-SETUP.md) and [NVL9.DSM.Core/Persistence/PERSISTENCE-GUIDE.md](NVL9.DSM.Core/Persistence/PERSISTENCE-GUIDE.md) for full setup instructions.

---

## Analytics Engine

`DSMEnvelopeAnalytics` reconstructs the execution graph from stored documents and produces:

- **Bottleneck detection** — methods consuming >20% of total request time
- **Critical path** — the longest execution chain through nested calls
- **Method statistics** — avg, median, P95, P99, success rate, error distribution
- **Error propagation** — trace the origin of a failure up the call tree
- **Mermaid diagrams** — visual graph export for documentation dashboards

```csharp
var envelopes = await _repository.GetByRootEnvelopIDAsync(rootId);
var analytics = new DSMEnvelopeAnalytics();
var analysis = analytics.BuildExecutionGraph(envelopes);

// Bottlenecks
foreach (var b in analysis.Bottlenecks)
    Console.WriteLine($"{b.MethodName}: {b.ExecutionTimeMs}ms ({b.PercentOfTotal:F1}%)");

// Mermaid diagram
var diagram = analytics.ExportToMermaid(analysis);
```

See [README-ANALYTICS.md](README-ANALYTICS.md) and [docs/ANALYTICS-SYSTEM-SUMMARY.md](docs/ANALYTICS-SYSTEM-SUMMARY.md) for details.

---

## AI / ML Performance Prediction

`NVL9.DSM.ML.Prediction` is a companion .NET project that loads an ONNX model and estimates an operation's execution time before it runs. The model is trained in Python (`ML.Training.Python/`) using Random Forest and XGBoost on historical envelope data. This is a statistical duration estimate, independent of whether the instrumented function is pure.

**12 input features**: class name, method name, call depth, hour of day, day of week, weekend/business-hours flags, historical avg/std/P95, success rate, method popularity.

**Output**: predicted execution time (ms) + confidence interval + recommended action (cache, proceed normally, alert ops).

```csharp
var prediction = _predictor.Predict(new PerformancePredictionInput
{
    ClassName = "PersonController",
    MethodName = "GetById",
    Depth = 0,
    HourOfDay = DateTime.Now.Hour,
    HistoricalAvg = 125.0,
    SuccessRate = 98.5
    // ...
});

if (prediction.IsSlowPrediction)
    return await GetFromCache(id);
```

Model accuracy on synthetic data: R² = 0.88–0.91. On real production data after 1–3 months of training: R² = 0.75–0.95.

See [README-AI-MODEL.md](README-AI-MODEL.md), [QUICKSTART-AI-MODEL.md](QUICKSTART-AI-MODEL.md), and [docs/ML-PREDICTION-GUIDE.md](docs/ML-PREDICTION-GUIDE.md).

---

## Project Structure

```
NVL9.DSM.Core/                  Core library (NuGet package)
  DSMEnvelope.cs                Envelope state and ID generation
  DSMEnvelope.public.cs         Init, Success, CaptureException, header capture
  IDSMEnvelope.cs               Public interface
  DSMEnvelopeManager.cs         Stack manager for nested envelope tracking
  DSMEnvelopeAttribute.cs       PostSharp aspect for zero-boilerplate instrumentation
  Codes/                        DSMEnvelopeCodeEnum, DSMEnvelopeCode, DSMEnvelopeCodeManager
  Models/                       CallerMethodName, CodeBlock, ValidationErrorCollector
  Persistence/                  IDSMEnvelopeRepository, DSMEnvelopeDocument, CosmosDB impl
  Analytics/                    DSMEnvelopeAnalytics, AnalyticsModels

NVL9.DSM.ML.Prediction/         ONNX inference service (.NET)
  Services/DSMPerformancePredictor.cs
  Models/PredictionModels.cs

ML.Training.Python/             Python model training
  generate_synthetic_data.py    Generates 10,000 synthetic records
  train_model.py                Trains RF & XGBoost, exports ONNX
  models/                       Saved model artifacts

NVL9.DSM.WebAPITest/            Sample ASP.NET Core API using the library
```

---

## Quick Start

### 1. Add the NuGet package

```bash
dotnet add package NVL9.DSM.Core
```

### 2. Instrument a controller

```csharp
[HttpGet("{id}")]
public async Task<ActionResult<DSMEnvelope<PersonDto>>> GetPersonById(int id)
{
    var envelope = DSMEnvelope<PersonDto>
        .InitWithCaller(nameof(PersonController))
        .CaptureAndSetHeaders(HttpContext);

    try
    {
        var person = await _personService.GetByIdAsync(id);
        await envelope.SuccessAndPersistAsync(person);
        return Ok(envelope);
    }
    catch (Exception ex)
    {
        await envelope.CaptureExceptionAndPersistAsync(ex);
        return StatusCode(500, envelope);
    }
}
```

### 3. Validate input

```csharp
var validator = new ValidationErrorCollector()
    .ValidateRequired(request.Email, "email")
    .ValidateEmail(request.Email, "email");

if (validator.HasErrors())
    return BadRequest(envelope.ValidationFailed(validator));
```

---

## NuGet

Package: **NVL9.DSM.Core** — version 1.0.1 — targets **net9.0**
Author: Andres Sosa / Nuvol 9 Corp
Repository: https://github.com/SosaNuvol/Nuvol9-DSM
