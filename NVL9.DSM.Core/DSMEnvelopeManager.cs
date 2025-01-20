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
}
