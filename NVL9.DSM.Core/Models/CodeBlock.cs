namespace NVL9.DSM.Core.Models;

using System.Diagnostics;
using System.Reflection;

public class CodeBlock
{
    public string? Method { get; private set; }

    public string? ClassName { get; private set; }

    public IList<CodeBlockArgument> Arguments { get; private set; } = new List<CodeBlockArgument>();

    public bool IsValidArgList { get; private set; }

    public CodeBlock(StackFrame stackFrame, object[] providedArguments)
    {
        if (stackFrame == null) return;

        var methodObject = stackFrame.GetMethod();
        if (methodObject == null) return;

        Method = methodObject.Name ?? string.Empty;

        ClassName = methodObject.ReflectedType != null
            ? methodObject.ReflectedType?.Name
            : null;

        IsValidArgList = _processArgumentsTry(stackFrame, providedArguments);

        _initArguments(methodObject.GetParameters(), providedArguments);
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
