namespace NVL9.DSM.Core.Models;

using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;

public class CodeBlock
{
    public string? Method { get; private set; }

    public string? ClassName { get; private set; }

    public IList<CodeBlockArgument> Arguments { get; private set; } = new List<CodeBlockArgument>();

    public bool IsValidArgList { get; private set; }

    public CodeBlock(StackFrame stackFrame, object[] providedArguments, ICallerMethodName callerMethodName)
    {
        if (callerMethodName != null)
        {
            _saveCallerMethodName(callerMethodName);
        } else
        {
            _saveCallerMethodName(stackFrame);
        }
        if (stackFrame == null) return;

        var methodObject = stackFrame.GetMethod();
        if (methodObject == null) return;

        IsValidArgList = _processArgumentsTry(stackFrame, providedArguments);

        _initArguments(methodObject.GetParameters(), providedArguments);
    }

    private void _saveCallerMethodName(ICallerMethodName callerMethodName)
    {
        if (callerMethodName == null) return;
        Method = callerMethodName.Method;
        ClassName = callerMethodName.ClassName;
    }

    private void _saveCallerMethodName(StackFrame stackFrame)
    {
        if (stackFrame == null) return;
        var methodObject = stackFrame.GetMethod();
        if (methodObject == null) return;
        Method = methodObject.Name;
        ClassName = methodObject.DeclaringType?.FullName ?? string.Empty;
    }

    private bool _processArgumentsTry(StackFrame stackFrame, object[] providedArguments)
    {
        var method = stackFrame?.GetMethod()?.GetParameters();

        return (method?.Length == providedArguments.Length);
    }

    private void _initArguments(ParameterInfo[] parameters, object[] providedArguments)
    {
        if (!IsValidArgList) return;

        var indexPointer = 0;
        foreach (var parameterInfo in parameters)
        {
            var paramValue = providedArguments[indexPointer];
            indexPointer++;

            Arguments.Add(new CodeBlockArgument(parameterInfo, paramValue));
        }
    }
}
