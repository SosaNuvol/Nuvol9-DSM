using Microsoft.AspNetCore.Http;

namespace NVL9.DSM.Core;

public partial class DSMEnvelopeManager
{
    private static DSMEnvelopeManager _instance = null!;

    private readonly Stack<DSMEnvelope> _envelopes = null!;

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
        _envelopes = new Stack<DSMEnvelope>();
    }

    public void PushEnvelope(DSMEnvelope envelop)
    {
        _tieToParent(envelop);
        _envelopes.Push(envelop);
    }

    public DSMEnvelope? PopEnvelope()
    {
        return _envelopes.Count == 0 ? null : _envelopes.Pop();
    }

    public DSMEnvelope? PeekEnvelope()
    {
        return _envelopes.Count == 0 ? null : _envelopes.Peek();
    }

    private void _tieToParent(DSMEnvelope envelope)
    {
        var parent = PeekEnvelope();
        if (parent == null) return;

        envelope.SetParentID(parent.RootEnvelopID);
    }

    public void FreezeStackWith(DSMEnvelope senderEnvelope)
    {
        var temp = new Stack<DSMEnvelope>();
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

    public DSMEnvelope InitEnvelope(params object[] providedParams)
    {
        var envelope = DSMEnvelope.Init(providedParams);
        PushEnvelope(envelope);

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

}
