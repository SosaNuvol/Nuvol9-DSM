namespace NVL9.DSM.Core.Models;

using System.Reflection;

public class CodeBlockArgument
{
    public object Value { get; private set; }

    public ParameterInfo Info { get; private set; }

    public CodeBlockArgument(ParameterInfo info, object value)
    {
        Info = info;
        Value = value;
    }
}
