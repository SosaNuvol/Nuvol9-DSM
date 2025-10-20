using NVL9.DSM.Core.Models;
using System.Diagnostics;

namespace NVL9.DSM.Core.Examples;

/// <summary>
/// Example demonstrating how to use CallerMethodName with async methods.
/// This example shows the differences between the old stack-frame based approach
/// and the new compiler-attribute based approach.
/// </summary>
public class AsyncMethodExample
{
    /// <summary>
    /// This async method demonstrates the issue with the old approach and shows the solution.
    /// </summary>
    public async Task<string> ProcessDataAsync(string data)
    {
        // OLD APPROACH (problematic with async methods):
        // This will capture state machine information instead of the actual method name
        var stackTrace = new StackTrace();
        var frame = stackTrace.GetFrame(0);
        var oldApproach = new CallerMethodName(frame!);
        
        Console.WriteLine($"Old approach - Method: {oldApproach.Method}, Class: {oldApproach.ClassName}");
        // Output might be: Method: MoveNext, Class: <ProcessDataAsync>d__1
        
        // NEW RECOMMENDED APPROACH (works correctly with async methods):
        var newApproach = CallerMethodName.FromCaller();
        Console.WriteLine($"New approach - Method: {newApproach.Method}, Class: {newApproach.ClassName}");
        // Output will be: Method: ProcessDataAsync, Class: AsyncMethodExample
        
        // Or with explicit class name:
        var newApproachWithClass = CallerMethodName.FromCallerWithClass(nameof(AsyncMethodExample));
        Console.WriteLine($"New approach with class - Method: {newApproachWithClass.Method}, Class: {newApproachWithClass.ClassName}");
        // Output will be: Method: ProcessDataAsync, Class: AsyncMethodExample
        
        // Simulate some async work
        await Task.Delay(100);
        
        return $"Processed: {data}";
    }
    
    /// <summary>
    /// Example of using the DSMEnvelope with async methods using the new InitWithCaller method.
    /// </summary>
    public async Task<string> ProcessDataWithEnvelopeAsync(string data)
    {
        // Use the new InitWithCaller method for better async support
        var envelope = DSMEnvelope<string>.InitWithCaller(nameof(AsyncMethodExample));
        
        try
        {
            // Simulate some async work
            await Task.Delay(100);
            var result = $"Processed: {data}";
            
            return envelope.Success(result).Value;
        }
        catch (Exception ex)
        {
            envelope.CaptureException(ex);
            throw;
        }
    }
    
    /// <summary>
    /// Alternative approach using the class name inference from file path
    /// </summary>
    public async Task<string> ProcessDataWithAutoClassAsync(string data)
    {
        // Let the system infer the class name from the file path
        var envelope = DSMEnvelope<string>.InitWithCaller();
        
        try
        {
            await Task.Delay(100);
            var result = $"Processed: {data}";
            
            return envelope.Success(result).Value;
        }
        catch (Exception ex)
        {
            envelope.CaptureException(ex);
            throw;
        }
    }
}