# Nuvol9-DSM

**NVL9.DSM** (Dynamic Stability Models) is a .NET observability library built by **Nuvol 9 Corp**. It wraps every operation result in a structured envelope — `DSMEnvelope<T>` — that carries timing data, caller context, error codes, distributed trace headers, and parent-child relationships. This creates a high-fidelity execution event stream that powers debugging, performance analysis, and AI-driven monitoring.

---

## Table of Contents

- [Nuvol9-DSM](#nuvol9-dsm)
  - [Table of Contents](#table-of-contents)
  - [What is a DSMEnvelope?](#what-is-a-dsmenvelope)
  - [How It Works — Lifecycle](#how-it-works--lifecycle)
    - [Initialization](#initialization)
    - [Success and Error](#success-and-error)
  - [Captured Data](#captured-data)
  - [Error Codes and Validation](#error-codes-and-validation)
    - [Validation Error Collection](#validation-error-collection)
  - [Distributed Tracing and Nested Operations](#distributed-tracing-and-nested-operations)
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

Every envelope prints a color-coded summary to the console and can be serialized to JSON for log pipelines and analytics.

---

## How It Works — Lifecycle

```
1. InitWithCaller(...)      → Envelope created, stopwatch started, GEN_COMMON_00001 code set
2. CaptureAndSetHeaders(HttpContext)  → Trace headers captured from incoming HTTP request
3. Success(result)          → Stopwatch stopped, GEN_COMMON_00000 code set, result stored
   — or —
   CaptureException(ex)     → Error code set, stack frozen up through parent chain
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
| `UniqueIEID` | `{GUID}\|{PID}` — unique per envelope instance |
| `RootEnvelopID` | `{AppId}\|{Host}\|{Timestamp}\|{UTCOffset}\|{PID}` — identifies the request root |
| `ParentEnvelopID` | Links this envelope to its parent in the call chain |
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

`DSMEnvelopeManager` maintains a per-process stack of active envelopes. When a child envelope is pushed, it automatically inherits the parent's `RootEnvelopID`, `ApiTraceId`, and `IdempotencyKeyId`. This allows reconstruction of the full call chain:

```
PersonController.GetById  (Root)
  └─> PersonService.GetByIdAsync  (Child)
       └─> DatabaseQuery.ExecuteAsync  (Grandchild)
```

When an inner envelope fails, `FreezeStackWith(envelope)` propagates the failure state up through all ancestors, preventing any parent from being accidentally marked successful.

---

## Output and Logging

Every envelope emits a formatted block to the console at both initialization and completion:

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

Cosmos DB is partitioned by `RootEnvelopID` so an entire request chain is always in one partition — single-partition queries, low RU cost.

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

`NVL9.DSM.ML.Prediction` is a companion .NET project that loads an ONNX model and predicts execution time for any Pure Function before it runs. The model is trained in Python (`ML.Training.Python/`) using Random Forest and XGBoost on historical envelope data.

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
