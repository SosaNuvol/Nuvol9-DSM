# NVL9.DSM.Core - DSM Envelope Library

## Overview
The **NVL9.DSM.Core** library provides a comprehensive envelope system for tracking, monitoring, and managing the lifecycle of operations in .NET applications. DSMEnvelope wraps your return values with rich metadata including execution time, error tracking, caller information, unique identifiers, and HTTP header capture for distributed tracing.

## Core Concepts

### What is a DSMEnvelope?
A DSMEnvelope is a wrapper around your application's data (`DSMEnvelope<T>`) that provides:
- **Unique Identifiers**: Every envelope has a `UniqueIEID` and `RootEnvelopID` for distributed tracing
- **Execution Tracking**: Automatic timing with `StartTime`, `EndTime`, and `ExecutionTime` 
- **Error Management**: Built-in exception capture with `CaptureException()` and structured error codes
- **Caller Information**: Captures method name, class name, file path, and line number
- **HTTP Header Propagation**: Captures `Api-Trace-Id` and `Idempotency-Key-Id` for request tracking
- **Parent-Child Relationships**: Envelopes can be linked via `ParentEnvelopID` for nested operation tracking
- **Status Codes**: Structured error/success codes via `DSMEnvelopeCode` system
- **Console Logging**: Built-in colored console output for easy debugging

### Key Properties
```csharp
public interface IDSMEnvelope
{
    string UniqueIEID { get; }              // Unique identifier with GUID and PID
    string ErrorIEID { get; }               // Set when an error occurs
    string? ApiTraceId { get; }             // HTTP Api-Trace-Id header value
    string? IdempotencyKeyId { get; }       // HTTP Idempotency-Key-Id header value
    string DTOMessage { get; }              // Human-readable message
    DSMEnvelopeCode Code { get; }           // Structured error/success code
    string RootEnvelopID { get; }           // Root identifier (timestamp-based)
    string ParentEnvelopID { get; }         // Parent envelope ID (for nesting)
    string ExecutionTime { get; }           // Execution time in milliseconds
    DateTimeOffset StartTime { get; }       // When the envelope was created
    DateTimeOffset EndTime { get; }         // When Success() or error was called
    bool FreezeStatus { get; }              // Prevents status changes when true
}
```

## Creating Envelopes

### Method 1: InitWithCaller (Recommended for Async Methods)
Uses compiler attributes to capture caller information, which works correctly with async/await:

```csharp
// With explicit class name (recommended)
var envelope = DSMEnvelope<PersonDto>
    .InitWithCaller(nameof(PersonController));

// With inferred class name from file path
var envelope = DSMEnvelope<PersonDto>
    .InitWithCaller();

// With additional parameters
var envelope = DSMEnvelope<PersonDto>
    .InitWithCaller(
        nameof(PersonController), 
        providedParams: new object[] { /* custom args */ },
        printEnvelop: false  // Suppress console output
    );
```

**Why InitWithCaller?** Async methods use compiler-generated state machines that confuse stack frame inspection. InitWithCaller uses `[CallerMemberName]`, `[CallerFilePath]`, and `[CallerLineNumber]` attributes to capture the actual method name, not the state machine's `MoveNext()` method.

### Method 2: Init (Legacy, problematic with async)
Uses stack frame inspection - not recommended for async methods:

```csharp
var envelope = DSMEnvelope<PersonDto>.Init();
```

## HTTP Header Capture

### Capturing Headers from HttpContext
DSMEnvelope can automatically capture HTTP headers for distributed tracing:

```csharp
// Static method approach
var envelope = DSMEnvelope<PersonDto>.InitWithCaller(nameof(PersonController));
DSMEnvelope<PersonDto>.CaptureAndSetHeaders(envelope, HttpContext);

// Fluent API approach (recommended)
var envelope = DSMEnvelope<PersonDto>
    .InitWithCaller(nameof(PersonController))
    .CaptureAndSetHeaders(HttpContext);
```

**Headers captured:**
- `Api-Trace-Id` → `envelope.ApiTraceId`
- `Idempotency-Key-Id` → `envelope.IdempotencyKeyId`

### Manual Header Setting
```csharp
envelope.SetApiTraceId("trace-123");
envelope.SetIdempotencyKeyId("idempotency-456");
```

## Managing Envelope Lifecycle

### Success States
```csharp
// Mark as successful and set the result value
var result = await _service.GetPerson(id);
envelope.Success(result);  // Automatically prints envelope
return envelope.Value;

// Success without output
envelope.Success(result, outputEnvelop: false);

// Success without value (void operations)
envelope.Success();
```

### Error Handling
```csharp
// Capture generic exceptions
try
{
    // ... operation
}
catch (Exception ex)
{
    envelope.CaptureException(ex);  // Sets error code and message
    return StatusCode(500, envelope);
}

// Capture SQL exceptions (special handling)
catch (SqlException ex)
{
    envelope.CaptureException(ex);  // Uses database-specific error code
}

// Custom error codes
envelope.SetState(
    DSMEnvelopeCodeManager.Manager.Find(DSMEnvelopeCodeEnum.API_APPVLD_02010),
    "Custom error message"
);
```

### Checking Status
```csharp
if (envelope.IsSuccessful())  // Code == GEN_COMMON_00000
{
    // Handle success
}

if (envelope.IsInInitializedState())  // Code == GEN_COMMON_00001
{
    // Still in initial state
}
```

## Error Code System

### Built-in Error Codes
```csharp
public enum DSMEnvelopeCodeEnum
{
    GEN_COMMON_00000 = 0,      // Success
    GEN_COMMON_00001 = 1,      // Initialized (default state)
    
    // Security
    API_COMMON_01000 = 01000,
    API_COMMON_01001 = 01001,
    
    // Application Validation
    API_APPVLD_02000 = 02000,   // Generic exception captured
    API_APPVLD_02001 = 02001,
    API_APPVLD_02010 = 02010,
    API_APPVLD_02011 = 02011,
    
    // Database
    API_DATABASE_03020 = 03020,  // SQL exception captured
    API_DATABASE_03021 = 03021,
    API_DATABASE_03022 = 03022,
    API_DATABASE_03023 = 03023,
}
```

### Using Error Codes
```csharp
var code = DSMEnvelopeCodeManager.Manager.Find(DSMEnvelopeCodeEnum.API_APPVLD_02001);
envelope.SetState(code, "Validation failed");
```

## Advanced Features

### Parent-Child Envelope Relationships
```csharp
var parentEnvelope = DSMEnvelope<ParentDto>.InitWithCaller(nameof(ParentService));

var childEnvelope = DSMEnvelope<ChildDto>.InitWithCaller(nameof(ChildService));
childEnvelope.SetParentID(parentEnvelope.RootEnvelopID);

// Headers are automatically propagated from parent to child in DSMEnvelopeManager
```

### Envelope Manager (Stack-based Management)
DSMEnvelopeManager provides a singleton stack for managing nested envelopes:

```csharp
// Initialize and push onto stack
var envelope = DSMEnvelopeManager.Instance
    .InitEnvelopeWithCaller<PersonDto>(
        nameof(PersonController), 
        httpContext: HttpContext
    );

// Peek at current envelope
var current = DSMEnvelopeManager.Instance.PeekEnvelope();

// Pop when done
var completed = DSMEnvelopeManager.Instance.PopEnvelope();

// Freeze entire stack (error propagation)
DSMEnvelopeManager.Instance.FreezeStackWith(errorEnvelope);
```

### Freeze Status (Error Propagation)
When an error occurs in a nested operation, you can freeze all parent envelopes:

```csharp
try
{
    // ... operation
}
catch (Exception ex)
{
    envelope.CaptureException(ex);
    envelope.SetFreezeStatus(true);  // Prevent further status changes
    
    // Freeze all envelopes in the manager stack
    DSMEnvelopeManager.Instance.FreezeStackWith(envelope);
}
```

### ReBase (Inheriting State from Another Envelope)
```csharp
var serviceEnvelope = await _service.GetData();
var controllerEnvelope = DSMEnvelope<DataDto>.InitWithCaller(nameof(DataController));

// Inherit code, message, and error ID from service envelope
controllerEnvelope.ReBase(serviceEnvelope);

if (controllerEnvelope.IsSuccessful())
{
    return Ok(controllerEnvelope);
}
```

### ExecuteTry (Quick Success Check)
```csharp
var serviceEnvelope = await _service.ValidateData(data);
if (!envelope.ExecuteTry(serviceEnvelope))
{
    return BadRequest(envelope);
}
```

## Console Output
Envelopes automatically print to console with color coding:
- **Yellow**: Initialized state
- **Green**: Success state
- **Red**: Error state

```csharp
envelope.PrintEnvelop();  // Manual print
envelope.FinishLifeCycle();  // Print at end of lifecycle
```

Output format:
```
|| ****************************************************************************** ||
||                UniqueIEID: 12345678-90ab-cdef-1234-567890abcdef|12345
||           SourceErrorIEID: 
||              API-Trace-Id: trace-abc-123
||        Idempotency-Key-Id: idem-xyz-789
||   (Private) RootEnvelopID: AppId|Hostname|2026:01:06:15:30:45:123:+300|12345
|| (Private) ParentEnvelopID: 
||            Execution Time: 1250ms
||                      Code: GEN_COMMON_00000: Success
||              Code Message: Success
||                   Message: Operation completed
||                Class Name: PersonController
||                    Method: GetPersonById
||                 File Path: D:\Project\Controllers\PersonController.cs
||               Line Number: 42
|| ****************************************************************************** ||
```

## Recommended Patterns for Controllers

### Pattern 1: New Operation
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
        if (person == null)
        {
            envelope.SetState(
                DSMEnvelopeCodeManager.Manager.Find(DSMEnvelopeCodeEnum.API_APPVLD_02001),
                $"Person with ID {id} not found"
            );
            return NotFound(envelope);
        }
        
        envelope.Success(person);
        return Ok(envelope);
    }
    catch (Exception ex)
    {
        envelope.CaptureException(ex);
        return StatusCode(500, envelope);
    }
}
```

### Pattern 2: Wrapping Facade Results
```csharp
[HttpGet]
public async Task<ActionResult<DSMEnvelope<IList<PersonDto>>>> GetAllPersons()
{
    var envelope = DSMEnvelope<IList<PersonDto>>
        .InitWithCaller(nameof(PersonController))
        .CaptureAndSetHeaders(HttpContext);
    
    try
    {
        // Facade already returns DSMEnvelope
        var result = await _personFacade.GetAllAsync();
        
        // Propagate headers to the facade result
        DSMEnvelope<IList<PersonDto>>.CaptureAndSetHeaders(result, HttpContext);
        
        // Inherit state from facade
        envelope.ReBase(result);
        
        return envelope.IsSuccessful() 
            ? Ok(envelope) 
            : StatusCode(500, envelope);
    }
    catch (Exception ex)
    {
        envelope.CaptureException(ex);
        return StatusCode(500, envelope);
    }
}
```

### Pattern 3: Using DSMEnvelopeManager
```csharp
[HttpPost]
public async Task<ActionResult<DSMEnvelope<PersonDto>>> CreatePerson(PersonCreateDto dto)
{
    var envelope = DSMEnvelopeManager.Instance
        .InitEnvelopeWithCaller<PersonDto>(
            nameof(PersonController),
            httpContext: HttpContext
        );
    
    try
    {
        var created = await _personService.CreateAsync(dto);
        envelope.Success(created);
        return CreatedAtAction(nameof(GetPersonById), new { id = created.Id }, envelope);
    }
    catch (Exception ex)
    {
        envelope.CaptureException(ex);
        DSMEnvelopeManager.Instance.FreezeStackWith(envelope);
        return StatusCode(500, envelope);
    }
    finally
    {
        DSMEnvelopeManager.Instance.PopEnvelope();
    }
}
```

## Dependencies

```xml
<PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="9.0.4" />
<PackageReference Include="Microsoft.Data.SqlClient" Version="6.0.2" />
<PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
<PackageReference Include="NLog" Version="5.4.0" />
<PackageReference Include="PostSharp" Version="2025.1.2" />
```

## Key Benefits

1. **Distributed Tracing**: Track requests across services via ApiTraceId and IdempotencyKeyId
2. **Automatic Timing**: No manual stopwatch management
3. **Rich Context**: Captures caller method, class, file path, and line number
4. **Standardized Errors**: Structured error codes with consistent formatting
5. **Fluent API**: Method chaining for readable code
6. **Async-Friendly**: InitWithCaller works correctly with async/await
7. **Parent-Child Tracking**: Link related operations via envelope IDs
8. **Visual Debugging**: Color-coded console output
9. **Null Safety**: Built-in null checks throughout

## Best Practices

1. **Use InitWithCaller**: Preferred over Init(), especially for async methods
2. **Capture Headers Early**: Call CaptureAndSetHeaders() immediately after InitWithCaller
3. **Let Envelopes Print**: The console output is valuable for debugging (use `printEnvelop: false` to suppress)
4. **Propagate Headers**: When calling services/facades, ensure headers are passed down
5. **Use ReBase for Facades**: Inherit state from lower-level envelopes instead of duplicating logic
6. **Freeze on Errors**: Use FreezeStatus to prevent state changes after errors in nested operations
7. **Structured Error Codes**: Use DSMEnvelopeCodeEnum instead of generic exceptions when possible
8. **Check IsSuccessful()**: Always verify envelope state before returning success responses

## Version Information

- **Package**: NVL9.DSM.Core
- **Version**: 1.0.1
- **Target Framework**: .NET 9.0
- **Author**: Andres Sosa
- **Company**: Nuvol 9 Corp
- **Repository**: https://github.com/SosaNuvol/Nuvol9-DSM