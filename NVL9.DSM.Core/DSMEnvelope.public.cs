namespace NVL9.DSM.Core;

using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using NVL9.DSM.Core.Codes;
using NVL9.DSM.Core.Models;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

public partial class DSMEnvelope<T>
{
    private const int _LINE_LENGTH_OUTPUT = 82;

    public static DSMEnvelope<T> Init(params object[] providedParams)
    {
        _initProvidedParams(providedParams, out var showArguments, out var obfuscatedListOfArguments);

        var callerMethod = providedParams
           .FirstOrDefault(param => param is ICallerMethodName) as ICallerMethodName ?? new CallerMethodName("NotSet", "NotSet");

        var result = new DSMEnvelope<T>(showArguments, obfuscatedListOfArguments);
        var stackTrace = new StackTrace();
        var frames = stackTrace.GetFrames();

        result.CodeBlockInfo = new CodeBlock(frames[_ONE_DEEP_INTO_STACK], providedParams, callerMethod);

        return result;
    }

    /// <summary>
    /// Initializes a DSMEnvelope with caller information automatically provided by the compiler.
    /// This method works reliably with async methods and is the recommended approach.
    /// </summary>
    /// <param name="className">The name of the calling class</param>
    /// <param name="providedParams">Additional parameters for the envelope</param>
    /// <param name="callerMemberName">Automatically filled by the compiler with the calling method name</param>
    /// <returns>A new DSMEnvelope instance</returns>
    public static DSMEnvelope<T> InitWithCaller(
        string className,
        object[]? providedParams = null,
        [CallerMemberName] string callerMemberName = "")
    {
        providedParams ??= Array.Empty<object>();
        
        _initProvidedParams(providedParams, out var showArguments, out var obfuscatedListOfArguments);

        var callerMethod = CallerMethodName.FromCallerWithClass(className, callerMemberName);
        
        var result = new DSMEnvelope<T>(showArguments, obfuscatedListOfArguments);
        var stackTrace = new StackTrace();
        var frames = stackTrace.GetFrames();

        result.CodeBlockInfo = new CodeBlock(frames[_ONE_DEEP_INTO_STACK], providedParams, callerMethod);

        result.PrintEnvelop();

        return result;
    }

    /// <summary>
    /// Initializes a DSMEnvelope with caller information automatically provided by the compiler.
    /// This method works reliably with async methods and is the recommended approach.
    /// Class name will be inferred from the source file path.
    /// </summary>
    /// <param name="providedParams">Additional parameters for the envelope</param>
    /// <param name="callerMemberName">Automatically filled by the compiler with the calling method name</param>
    /// <param name="callerFilePath">Automatically filled by the compiler with the source file path</param>
    /// <returns>A new DSMEnvelope instance</returns>
    public static DSMEnvelope<T> InitWithCaller(
        object[]? providedParams = null,
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "")
    {
        providedParams ??= Array.Empty<object>();
        
        _initProvidedParams(providedParams, out var showArguments, out var obfuscatedListOfArguments);

        var callerMethod = CallerMethodName.FromCaller(callerMemberName, callerFilePath);
        
        var result = new DSMEnvelope<T>(showArguments, obfuscatedListOfArguments);
        var stackTrace = new StackTrace();
        var frames = stackTrace.GetFrames();

        result.CodeBlockInfo = new CodeBlock(frames[_ONE_DEEP_INTO_STACK], providedParams, callerMethod);

        result.PrintEnvelop();

        return result;
    }


    public bool IsSuccessful()
    {
        return Code == DSMEnvelopeCodeManager.Manager.Find(DSMEnvelopeCodeEnum.GEN_COMMON_00000);
    }

    public bool IsInInitializedState()
    {
        return Code == DSMEnvelopeCodeManager.Manager.Find(DSMEnvelopeCodeEnum.GEN_COMMON_00001);
    }

    public DSMEnvelope<T> Success(T result, bool outputEnvelop = true)
    {
        _calculateExecutionTime();
        Value = result;
        if (!FreezeStatus && IsInInitializedState())
        {
            Code = DSMEnvelopeCodeManager.Manager.Find(DSMEnvelopeCodeEnum.GEN_COMMON_00000);
            DTOMessage = Code.ErrorMessage;
        }
        if (outputEnvelop) PrintEnvelop();
        return this;
    }

    public DSMEnvelope<T> Success(bool outputEnvelop = true)
    {
        _calculateExecutionTime();

        if (!FreezeStatus && IsInInitializedState())
        {
            Code = DSMEnvelopeCodeManager.Manager.Find(DSMEnvelopeCodeEnum.GEN_COMMON_00000);
            DTOMessage = Code.ErrorMessage;
        }

        if (outputEnvelop) PrintEnvelop();

        return this;
    }

    /// <summary>
    /// Implementation of IDSMEnvelope.Success() method
    /// </summary>
    void IDSMEnvelope.Success()
    {
        Success(outputEnvelop: false);
    }

    public void FinishLifeCycle()
    {
        PrintEnvelop();
    }

    public DSMEnvelope<T> ReBase(IDSMEnvelope envelop, bool calculateExecutionTime = true)
    {
        if (calculateExecutionTime) _calculateExecutionTime();

        Code = envelop.Code;
        DTOMessage = envelop.DTOMessage;
        envelop.SetParentID(RootEnvelopID);
        ErrorIEID = envelop.ErrorIEID;

        envelop.PrintEnvelop();

        return this;
    }

    public void SetState(DSMEnvelopeCode code, string message)
    {
        Code = code;
        DTOMessage = message;

        if (Code != DSMEnvelopeCodeManager.Manager.Find(DSMEnvelopeCodeEnum.GEN_COMMON_00000))
        {
            ErrorIEID = UniqueIEID;
        }
    }

    public void CaptureException(Exception ex)
    {
        _calculateExecutionTime();
        ErrorIEID = UniqueIEID;
        Code = DSMEnvelopeCodeManager.Manager.Find(DSMEnvelopeCodeEnum.API_APPVLD_02000);
        DTOMessage = ex.Message;
    }

    public void CaptureException(SqlException ex)
    {
        _calculateExecutionTime();
        ErrorIEID = UniqueIEID;
        Code = DSMEnvelopeCodeManager.Manager.Find(DSMEnvelopeCodeEnum.API_DATABASE_03020);
        DTOMessage = ex.Message;
    }

    public bool ExecuteTry(IDSMEnvelope envelop)
    {
        _calculateExecutionTime();
        return ReBase(envelop, false).IsSuccessful();
    }

    public void SetParentID(string parentId)
    {
        ParentEnvelopID = parentId;
    }

    public void SetFreezeStatus(bool status)
    {
        FreezeStatus = status;
    }

    public void SetFreezeStatus(DSMEnvelope<T> senderEnvelope)
    {
        FreezeStatus = senderEnvelope.FreezeStatus;
        ErrorIEID = senderEnvelope.ErrorIEID;
        Code = senderEnvelope.Code;
        DTOMessage = senderEnvelope.DTOMessage;
    }

    public void SetFreezeStatus(IDSMEnvelope envelope)
    {
        FreezeStatus = envelope.FreezeStatus;
        ErrorIEID = envelope.ErrorIEID;
        Code = envelope.Code;
        DTOMessage = envelope.DTOMessage;
    }

    public void SetApiTraceId(string? apiTraceId)
    {
        ApiTraceId = apiTraceId;
    }

    public void SetIdempotencyKeyId(string? idempotencyKeyId)
    {
        IdempotencyKeyId = idempotencyKeyId;
    }

    public override string ToString()
    {
        var sb = new StringBuilder();

        sb.AppendLine($"|| {new string('*', _LINE_LENGTH_OUTPUT)} ||");

        sb.AppendLine($"||                UniqueIEID: {UniqueIEID}");
        sb.AppendLine($"||           SourceErrorIEID: {ErrorIEID}");
        _addHeaderValues(sb);
        sb.AppendLine($"||   (Private) RootEnvelopID: {RootEnvelopID}");
        sb.AppendLine($"|| (Private) ParentEnvelopID: {ParentEnvelopID}");
        sb.AppendLine($"||            Execution Time: {ExecutionTime ?? "0"}ms");
        sb.AppendLine($"||                      Code: {Code}");
        sb.AppendLine($"||              Code Message: {Code.ErrorMessage}");
        sb.AppendLine($"||                   Message: {DTOMessage}");
        sb.AppendLine($"||                Class Name: {CodeBlockInfo.ClassName}");
        sb.AppendLine($"||                    Method: {CodeBlockInfo.Method}");

        sb.Append($"|| {new string('*', _LINE_LENGTH_OUTPUT)} ||");

        return sb.ToString();
    }

    private void _addHeaderValues(StringBuilder sb)
    {
        if (ApiTraceId == null && IdempotencyKeyId == null) return;

        if (ApiTraceId != null)
        {
            sb.AppendLine($"||              API-Trace-Id: {ApiTraceId}");
        }
        if (IdempotencyKeyId != null)
        {
            sb.AppendLine($"||        Idempotency-Key-Id: {IdempotencyKeyId}");
        }
    }

    public string ToRawJson()
    {
        return JsonConvert.SerializeObject(this, Formatting.Indented);
    }


    public void PrintEnvelop()
    {
        _setForgroundColor();

        Console.WriteLine(ToString());

        Console.ForegroundColor = ConsoleColor.White;
    }
}
