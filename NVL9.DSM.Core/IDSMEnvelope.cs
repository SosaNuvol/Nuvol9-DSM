namespace NVL9.DSM.Core;

using NVL9.DSM.Core.Codes;

public interface IDSMEnvelope
{
    string UniqueIEID { get; }

    string ErrorIEID { get; }

    string? ApiTraceId { get; }

    string? IdempotencyKeyId { get; }

    string DTOMessage { get; }

    DSMEnvelopeCode Code { get; }

    string ObjectSignature { get; }

    string RawJson { get; }

    string ExecutionTime { get; }

    string RootEnvelopID { get; }

    string ParentEnvelopID { get; }

    bool FreezeStatus { get; }

    DateTimeOffset StartTime { get; }

    DateTimeOffset EndTime { get; }


    bool IsSuccessful();

    void SetParentID(string parentId);

    void PrintEnvelop();

}
