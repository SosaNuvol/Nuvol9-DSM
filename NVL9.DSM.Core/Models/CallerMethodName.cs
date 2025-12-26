using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace NVL9.DSM.Core.Models;

public interface ICallerMethodName
{
    string Method { get; }

    string ClassName { get; }
    string FilePath { get; }
    int LineNumber { get; }

}

public class CallerMethodName : ICallerMethodName
{
    public string Method { get; private set; }
    public string ClassName { get; private set; }
    public string FilePath { get; private set; }
    public int LineNumber { get; private set; }

    public CallerMethodName(StackFrame stackFrame)
    {
        if (stackFrame == null) return;
        var methodObject = stackFrame.GetMethod();
        if (methodObject == null) return;

        // Handle async methods by looking for the original method name
        var methodName = methodObject.Name;
        var declaringType = methodObject.DeclaringType;

        // Check if this is an async state machine
        if (declaringType != null && declaringType.GetCustomAttributes(typeof(CompilerGeneratedAttribute), false).Length > 0)
        {
            // Try to extract the original method name from the state machine class name
            // Async state machines are typically named like "<OriginalMethodName>d__1"
            var typeName = declaringType.Name;
            var startIndex = typeName.IndexOf('<') + 1;
            var endIndex = typeName.IndexOf('>');
            
            if (startIndex > 0 && endIndex > startIndex)
            {
                methodName = typeName.Substring(startIndex, endIndex - startIndex);
                // Get the actual declaring type (parent of the state machine)
                declaringType = declaringType.DeclaringType;
            }
        }

        Method = methodName;
        ClassName = declaringType?.Name;
    }

    public CallerMethodName(string methodName, string className, string filePath, int lineNumber)
    {
        Method = methodName;
        ClassName = className;
        FilePath = filePath;
        LineNumber = lineNumber;
    }

    /// <summary>
    /// Creates a CallerMethodName using compiler-provided caller information.
    /// This is the recommended way to get caller information, especially for async methods.
    /// Class name will be inferred from the source file path.
    /// </summary>
    /// <param name="memberName">Automatically filled by the compiler with the calling method name</param>
    /// <param name="sourceFilePath">Automatically filled by the compiler with the source file path</param>
    /// <returns>A CallerMethodName instance with the caller information</returns>
    public static CallerMethodName FromCaller(
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        // Extract class name from the file path as a fallback
        var fileName = System.IO.Path.GetFileNameWithoutExtension(sourceFilePath);
        return new CallerMethodName(memberName, fileName, sourceFilePath, lineNumber);
    }

    /// <summary>
    /// Creates a CallerMethodName with explicit class name using compiler-provided caller information.
    /// </summary>
    /// <param name="className">The class name to use</param>
    /// <param name="memberName">Automatically filled by the compiler with the calling method name</param>
    /// <returns>A CallerMethodName instance with the caller information</returns>
    public static CallerMethodName FromCallerWithClass(
        string className,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        return new CallerMethodName(memberName, className, filePath, lineNumber);
    }
}