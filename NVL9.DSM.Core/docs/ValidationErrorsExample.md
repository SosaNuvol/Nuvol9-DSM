# DSM Validation Error Handling

This document demonstrates how to use the new validation error handling functionality in DSMEnvelope.

## Overview

The DSMEnvelope now supports collecting and managing validation errors in a structured way, similar to the JSON format you requested:

```json
{
  "error": "Validation failed",
  "details": {
    "email": ["Email is required", "Email format is invalid"],
    "phone": ["Phone number is required"]
  },
  "code": "VALIDATION_ERROR",
  "timestamp": "2024-01-15T10:30:00Z",
  "path": "/api/v1/customers"
}
```

## Features Added

### 1. New Validation Error Code
- Added `API_APPVLD_02020` enum value for validation errors
- HTTP status code: 400 (Bad Request)
- Message: "Validation failed"

### 2. ValidationErrors Property
- `Dictionary<string, List<string>> ValidationErrors` property in DSMEnvelope
- Stores field names as keys and lists of error messages as values

### 3. Validation Methods
- `AddValidationError(fieldName, errorMessage)` - Add single error
- `AddValidationErrors(fieldName, errorMessages)` - Add multiple errors for a field
- `SetValidationErrors(validationErrors)` - Set all validation errors at once
- `HasValidationErrors()` - Check if validation errors exist
- `GetValidationErrorCount()` - Get total count of validation errors
- `ClearValidationErrors()` - Clear all validation errors

### 4. ValidationErrorCollector Helper Class
A fluent helper class for collecting validation errors:
- `AddError(fieldName, errorMessage)` - Add custom error
- `ValidateRequired(value, fieldName)` - Check for required fields
- `ValidateEmail(email, fieldName)` - Validate email format
- `ValidateLength(value, fieldName, minLength, maxLength)` - Validate string length
- Method chaining support for fluent syntax

### 5. Integration Methods
- `ApplyValidationErrors(collector)` - Apply errors from ValidationErrorCollector
- `ValidationFailed(collector)` - Shorthand for applying validation errors

## Usage Examples

### Basic Usage

```csharp
public DSMEnvelope<CustomerResponse> CreateCustomer(CustomerRequest request)
{
    var envelope = DSMEnvelope<CustomerResponse>.InitWithCaller(nameof(CustomerService));
    
    // Method 1: Direct validation error addition
    if (string.IsNullOrEmpty(request.Email))
    {
        envelope.AddValidationError("email", "Email is required");
    }
    
    if (!IsValidEmail(request.Email))
    {
        envelope.AddValidationError("email", "Email format is invalid");
    }
    
    if (string.IsNullOrEmpty(request.Phone))
    {
        envelope.AddValidationError("phone", "Phone number is required");
    }
    
    // Check if we have validation errors
    if (envelope.HasValidationErrors())
    {
        return envelope; // Returns with validation error state
    }
    
    // Continue with business logic...
    var customer = new CustomerResponse(Guid.NewGuid(), request.Name, request.Email, request.Phone);
    return envelope.Success(customer);
}
```

### Using ValidationErrorCollector (Recommended)

```csharp
public DSMEnvelope<CustomerResponse> CreateCustomer(CustomerRequest request)
{
    var envelope = DSMEnvelope<CustomerResponse>.InitWithCaller(nameof(CustomerService));
    
    // Use the validation collector for cleaner code
    var validator = new ValidationErrorCollector()
        .ValidateRequired(request.Email, "email")
        .ValidateEmail(request.Email, "email")
        .ValidateRequired(request.Phone, "phone")
        .ValidateLength(request.Name, "name", minLength: 2, maxLength: 100)
        .AddErrorIf(request.Age < 18, "age", "Must be at least 18 years old");
    
    // Apply validation errors if any exist
    if (validator.HasErrors())
    {
        return envelope.ValidationFailed(validator);
    }
    
    // Continue with business logic...
    var customer = new CustomerResponse(Guid.NewGuid(), request.Name, request.Email, request.Phone);
    return envelope.Success(customer);
}
```

### Custom Validation Logic

```csharp
public DSMEnvelope<UserResponse> RegisterUser(UserRequest request)
{
    var envelope = DSMEnvelope<UserResponse>.InitWithCaller(nameof(UserService));
    var validator = new ValidationErrorCollector();
    
    // Basic validation
    validator
        .ValidateRequired(request.Username, "username")
        .ValidateRequired(request.Password, "password")
        .ValidateEmail(request.Email, "email");
    
    // Custom business rules
    if (!string.IsNullOrEmpty(request.Username) && UserExists(request.Username))
    {
        validator.AddError("username", "Username is already taken");
    }
    
    if (!string.IsNullOrEmpty(request.Password) && !IsStrongPassword(request.Password))
    {
        validator.AddErrors("password", new[] 
        {
            "Password must be at least 8 characters",
            "Password must contain uppercase letter",
            "Password must contain lowercase letter",
            "Password must contain number"
        });
    }
    
    // Apply validation errors
    if (validator.HasErrors())
    {
        return envelope.ValidationFailed(validator);
    }
    
    // Continue with registration...
    var user = CreateNewUser(request);
    return envelope.Success(user);
}
```

## Console Output

When validation errors are present, the DSMEnvelope will display them in the console output:

```
|| ********************************************************************************** ||
||                UniqueIEID: a1b2c3d4-e5f6-7890-abcd-ef1234567890|12345
||           SourceErrorIEID: a1b2c3d4-e5f6-7890-abcd-ef1234567890|12345
||   (Private) RootEnvelopID: APP001|HOSTNAME|2024:01:15:10:30:00:123:+300|12345
|| (Private) ParentEnvelopID: 
||            Execution Time: 15ms
||                      Code: API-APPVLD-02020
||              Code Message: Validation failed
||                   Message: Validation failed
||         Validation Errors: 3 error(s) found
||                     - email: Email is required
||                     - email: Email format is invalid
||                     - phone: Phone number is required
||                Class Name: CustomerService
||                    Method: CreateCustomer
||                 File Path: /src/Services/CustomerService.cs
||               Line Number: 15
|| ********************************************************************************** ||
```

## Integration with Web APIs

The validation errors integrate seamlessly with ASP.NET Core and can be easily converted to appropriate HTTP responses:

```csharp
app.MapPost("/customers", (CustomerRequest request) =>
{
    var result = customerService.CreateCustomer(request);
    
    if (result.HasValidationErrors())
    {
        return Results.BadRequest(new
        {
            error = result.DTOMessage,
            details = result.ValidationErrors,
            code = result.Code.StatusCode,
            timestamp = DateTime.UtcNow,
            path = "/api/customers"
        });
    }
    
    return Results.Ok(result.Value);
});
```

This produces the exact JSON format you requested when validation fails.