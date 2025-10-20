# CallerMethodName Async Method Fix

## Problem
The original `CallerMethodName` class had issues when used inside async methods because the C# compiler transforms async methods into state machines. When using `StackFrame.GetMethod()`, it would return information about the generated state machine methods (like `MoveNext`) rather than the original async method name.

## Solution
The `CallerMethodName` class has been enhanced with:

### 1. Improved StackFrame Constructor
- Added logic to detect async state machine classes (marked with `CompilerGeneratedAttribute`)
- Extracts the original method name from state machine class names (e.g., `<OriginalMethodName>d__1`)
- Retrieves the actual declaring type instead of the state machine type

### 2. New Static Factory Methods
- `FromCaller()` - Uses compiler attributes to get caller information (recommended for async methods)
- `FromCallerWithClass(string className)` - Uses compiler attributes with explicit class name

### 3. Enhanced DSMEnvelope Initialization
- `InitWithCaller(string className)` - Recommended for async methods with explicit class name
- `InitWithCaller()` - Infers class name from source file path

## Usage Examples

### For Async Methods (Recommended)
```csharp
public async Task<string> ProcessAsync(string data)
{
    // Using the new static method (works correctly with async)
    var callerInfo = CallerMethodName.FromCallerWithClass(nameof(MyClass));
    
    // Or with DSMEnvelope
    var envelope = DSMEnvelope<string>.InitWithCaller(nameof(MyClass));
    
    await Task.Delay(100);
    return envelope.Success($"Processed: {data}").Value;
}
```

### For Regular Methods (Still Works)
```csharp
public string Process(string data)
{
    // Original approach still works for non-async methods
    var stackTrace = new StackTrace();
    var callerInfo = new CallerMethodName(stackTrace.GetFrame(0));
    
    // Or use the new approach (works for all methods)
    var callerInfo2 = CallerMethodName.FromCallerWithClass(nameof(MyClass));
    
    return $"Processed: {data}";
}
```

## Key Benefits
1. **Async Method Support**: Correctly identifies the original method name in async methods
2. **Backwards Compatibility**: Existing code continues to work
3. **Improved Reliability**: Uses compiler attributes instead of runtime stack inspection
4. **Better Performance**: Compiler attributes are resolved at compile time