using System.Diagnostics;

namespace NVL9.DSM.Core.Models;

public interface ICallerMethodName
{
    string Method { get; }

    string ClassName { get; }
}

public class CallerMethodName : ICallerMethodName
{
    public string Method { get; private set; }
    public string ClassName { get; private set; }
    public CallerMethodName(StackFrame stackFrame)
    {
        if (stackFrame == null) return;
        var methodObject = stackFrame.GetMethod();
        if (methodObject == null) return;
        Method = methodObject.Name;
        ClassName = methodObject.ReflectedType != null
            ? methodObject.ReflectedType?.Name
            : null;
    }

    public CallerMethodName(string className, string methodName)
    {
        Method = methodName;
        ClassName = className;
    }
}