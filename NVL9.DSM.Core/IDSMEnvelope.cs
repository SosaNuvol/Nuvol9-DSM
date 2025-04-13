namespace NVL9.DSM.Core;

using Microsoft.Data.SqlClient;
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

    void Success();

    void SetParentID(string parentId);

    void SetApiTraceId(string? apiTraceId);

    void SetIdempotencyKeyId(string? idempotencyKeyId);

    void SetFreezeStatus(bool status);

    void SetFreezeStatus(IDSMEnvelope envelope);

    void PrintEnvelop();

    void CaptureException(Exception ex);

    void CaptureException(SqlException ex);

    void SetState(DSMEnvelopeCode code, string message);

    void FinishLifeCycle();
}
