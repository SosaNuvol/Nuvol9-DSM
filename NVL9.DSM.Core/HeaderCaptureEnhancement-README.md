# DSMEnvelope Header Capture Enhancement

## Overview
The `DSMEnvelope` class has been enhanced with built-in header capture functionality, eliminating the need for controllers to implement their own header extraction logic.

## New Methods Added to DSMEnvelope

### Static Method
```csharp
/// <summary>
/// Captures the API trace and idempotency headers from the HTTP context and sets them on the specified envelope.
/// </summary>
/// <param name="envelope">The DSM envelope to set the headers on</param>
/// <param name="httpContext">The HTTP context to extract headers from</param>
public static void CaptureAndSetHeaders(IDSMEnvelope envelope, HttpContext httpContext)
```

### Instance Method (Fluent API)
```csharp
/// <summary>
/// Captures headers and sets them on this envelope instance.
/// </summary>
/// <param name="httpContext">The HTTP context to extract headers from</param>
/// <returns>This envelope instance for method chaining</returns>
public DSMEnvelope<T> CaptureAndSetHeaders(HttpContext httpContext)
```

## Usage Examples

### Method 1: Static Method Approach
```csharp
public async Task<ActionResult<DSMEnvelopeDto<IList<PersonDto>>>> GetPersonsAsync()
{
    var envelope = DSMEnvelope<IList<PersonDto>>.InitWithCaller(nameof(PersonController));
    DSMEnvelope<IList<PersonDto>>.CaptureAndSetHeaders(envelope, HttpContext);
    
    // Rest of the method...
}
```

### Method 2: Fluent API Approach (Recommended)
```csharp
public async Task<ActionResult<DSMEnvelopeDto<IList<PersonDto>>>> GetPersonsAsync()
{
    var envelope = DSMEnvelope<IList<PersonDto>>
        .InitWithCaller(nameof(PersonController))
        .CaptureAndSetHeaders(HttpContext);
    
    // Rest of the method...
}
```

### Method 3: For Existing Envelopes from Facades
```csharp
public async Task<ActionResult<DSMEnvelope<PersonDto>>> GetPersonById(int id)
{
    try
    {
        var result = await _personFacade.GetPersonById(id);
        DSMEnvelope<PersonDto>.CaptureAndSetHeaders(result, HttpContext);
        
        return Ok(result);
    }
    catch (Exception ex)
    {
        var errorEnvelope = DSMEnvelope<PersonDto>
            .InitWithCaller(nameof(PersonController))
            .CaptureAndSetHeaders(HttpContext);
        errorEnvelope.CaptureException(ex);
        return StatusCode(500, errorEnvelope);
    }
}
```

## Headers Captured

The methods automatically extract and set the following HTTP headers:
- `Api-Trace-Id` ? Sets `envelope.ApiTraceId`
- `Idempotency-Key-Id` ? Sets `envelope.IdempotencyKeyId`

## Benefits

1. **Centralized Logic**: Header capture logic is now part of the DSMEnvelope class
2. **Reusability**: Can be used across all controllers without code duplication
3. **Null Safety**: Built-in null checks for HttpContext and headers
4. **Fluent API**: Method chaining support for cleaner code
5. **Consistency**: Standardized approach across the application

## Dependencies Added

The DSMEnvelope project now includes:
```xml
<PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="9.0.4" />
```
This provides the required `HttpContext` types.

## Backward Compatibility

- All existing DSMEnvelope functionality remains unchanged
- Controllers without header capture continue to work as before
- The new methods are additive and don't break existing code

## Recommended Pattern for Controllers

```csharp
public async Task<ActionResult<DSMEnvelope<TResult>>> SomeAction()
{
    try
    {
        // For new envelopes
        var envelope = DSMEnvelope<TResult>
            .InitWithCaller(nameof(ControllerName))
            .CaptureAndSetHeaders(HttpContext);
            
        // For facade results
        var result = await _facade.SomeOperation();
        DSMEnvelope<TResult>.CaptureAndSetHeaders(result, HttpContext);
        
        return Ok(result);
    }
    catch (Exception ex)
    {
        var errorEnvelope = DSMEnvelope<TResult>
            .InitWithCaller(nameof(ControllerName))
            .CaptureAndSetHeaders(HttpContext);
        errorEnvelope.CaptureException(ex);
        return StatusCode(500, errorEnvelope);
    }
}
```

This enhancement makes header capture a first-class feature of the DSMEnvelope system.