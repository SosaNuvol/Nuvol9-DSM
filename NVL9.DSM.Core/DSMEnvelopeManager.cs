using Microsoft.AspNetCore.Http;

namespace NVL9.DSM.Core;

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

    public DSMEnvelope<T> InitEnvelope<T>(params object[] providedParams)
    {
        var envelope = DSMEnvelope<T>.Init(providedParams);
        PushEnvelope((IDSMEnvelope)envelope);

        _captureHeaderValues(providedParams);

        envelope.PrintEnvelop();

        return envelope;
    }

    /// <summary>
    /// Initializes an envelope using the async-friendly InitWithCaller method and manages it in the envelope stack.
    /// </summary>
    /// <typeparam name="T">The type of data the envelope will contain</typeparam>
    /// <param name="className">The name of the calling class</param>
    /// <param name="httpContext">The HTTP context to extract headers from</param>
    /// <param name="providedParams">Additional parameters for the envelope</param>
    /// <param name="printEnvelop">Whether to print the envelope on initialization</param>
    /// <returns>A new DSMEnvelope instance managed by the stack</returns>
    public DSMEnvelope<T> InitEnvelopeWithCaller<T>(
        string className, 
        HttpContext? httpContext = null,
        object[]? providedParams = null, 
        bool printEnvelop = true)
    {
        var envelope = DSMEnvelope<T>.InitWithCaller(className, providedParams, printEnvelop: false);
        
        // Capture headers if HttpContext is provided
        if (httpContext != null)
        {
            envelope.CaptureAndSetHeaders(httpContext);
        }

        PushEnvelope((IDSMEnvelope)envelope);

        if(printEnvelop) envelope.PrintEnvelop();

        return envelope;
    }

    /// <summary>
    /// Initializes an envelope using the async-friendly InitWithCaller method (with file path inference) and manages it in the envelope stack.
    /// </summary>
    /// <typeparam name="T">The type of data the envelope will contain</typeparam>
    /// <param name="httpContext">The HTTP context to extract headers from</param>
    /// <param name="providedParams">Additional parameters for the envelope</param>
    /// <param name="printEnvelop">Whether to print the envelope on initialization</param>
    /// <returns>A new DSMEnvelope instance managed by the stack</returns>
    public DSMEnvelope<T> InitEnvelopeWithCaller<T>(
        HttpContext? httpContext = null,
        object[]? providedParams = null, 
        bool printEnvelop = true)
    {
        var envelope = DSMEnvelope<T>.InitWithCaller(providedParams, printEnvelop: false);
        
        // Capture headers if HttpContext is provided
        if (httpContext != null)
        {
            envelope.CaptureAndSetHeaders(httpContext);
        }

        PushEnvelope((IDSMEnvelope)envelope);

        if (printEnvelop) envelope.PrintEnvelop();

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

}
