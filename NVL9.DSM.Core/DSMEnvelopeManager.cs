namespace NVL9.DSM.Core;

using Microsoft.AspNetCore.Http;

public partial class DSMEnvelopeManager
{
    private static DSMEnvelopeManager _instance = null!;

    private readonly Stack<IDSMEnvelope> _envelopes = null!;

    public static DSMEnvelopeManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new DSMEnvelopeManager();
            }

            return _instance;
        }
    }

    private DSMEnvelopeManager()
    {
        _envelopes = new Stack<IDSMEnvelope>();
    }

    public void PushEnvelope(IDSMEnvelope envelop)
    {
        _tieToParent(envelop);
        _envelopes.Push(envelop);
    }

    public IDSMEnvelope? PopEnvelope()
    {
        return _envelopes.Count == 0 ? null : _envelopes.Pop();
    }

    public IDSMEnvelope? PeekEnvelope()
    {
        return _envelopes.Count == 0 ? null : _envelopes.Peek();
    }

    private void _tieToParent(IDSMEnvelope envelope)
    {
        var parent = PeekEnvelope();
        if (parent == null) return;

        envelope.SetParentID(parent.RootEnvelopID);
        envelope.SetApiTraceId(parent.ApiTraceId);
        envelope.SetIdempotencyKeyId(parent.IdempotencyKeyId);
        //envelope.SetApiTraceId(parent.IdempotencyKeyId);
    }

    public void FreezeStackWith(IDSMEnvelope senderEnvelope)
    {
        var temp = new Stack<IDSMEnvelope>();
        while (_envelopes.Count > 0)
        {
            var envelope = _envelopes.Pop();

            // Modify the envelope here
            if (envelope.UniqueIEID == senderEnvelope.UniqueIEID)
            {
                envelope.SetFreezeStatus(true);
            }
            else
            {
                envelope.SetFreezeStatus(senderEnvelope);
            }

            temp.Push(envelope);
        }

        // Rehydratge the stack
        while (temp.Count > 0)
        {
            _envelopes.Push(temp.Pop());
        }
    }

    public DSMEnvelope<T> InitEnvelope<T>(object[] providedParams) where T : class, new()
    {
        var envelope = DSMEnvelope<T>.Init(providedParams);

        PushEnvelope((IDSMEnvelope)envelope);

        _captureHeaderValues(providedParams);

        envelope.PrintEnvelop();

        return envelope;
    }
    public DSMEnvelope<T> InitEnvelopeAsync<T>(params object[] providedParams) where T : class, new()
    {
        var envelope = DSMEnvelope<T>.Init(providedParams);

        PushEnvelope((IDSMEnvelope)envelope);

        _captureHeaderValues(providedParams);

        envelope.PrintEnvelop();

        return envelope;
    }

    private void _captureHeaderValues(params object[] providedParams)
    {
        // Find the parameter named "httpHeaders"
        var headers = providedParams.FirstOrDefault(param => param is IHeaderDictionary) as IHeaderDictionary;

        if (headers == null)
        {
            return;
        }

        // Extract the API-Trace-Id and Idempotency-Key-Id headers
        string? apiTraceId = headers?["API-Trace-Id"];
        string? idempotencyKeyId = headers?["Idempotency-Key-Id"];
        var envelope = DSMEnvelopeManager.Instance.PeekEnvelope();

        if (envelope == null)
        {
            return;
        }

        // Assign to the current envelope
        if (apiTraceId != null)
        {
            envelope.SetApiTraceId(apiTraceId);
        }
        if (idempotencyKeyId != null)
        {
            envelope.SetIdempotencyKeyId(idempotencyKeyId);
        }
    }

    public static string GetCallerClassName()
    {
        var stackFrame = new System.Diagnostics.StackFrame(1, false);
        var method = stackFrame.GetMethod();
        return CleanClassName(method?.DeclaringType?.FullName ?? "UnknownClass");
    }

    public static string GetCallerMethodName([System.Runtime.CompilerServices.CallerMemberName] string callerName = "")
    {
        return callerName;
    }

    public static string CleanClassName(string rawClass)
    {
        if (rawClass.IndexOf("+") > 0)
        {
            rawClass = rawClass.Substring(0, rawClass.IndexOf("+"));
        }

        return rawClass;
    }
}
