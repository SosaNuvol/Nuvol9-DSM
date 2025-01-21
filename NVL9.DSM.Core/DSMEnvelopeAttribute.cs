namespace NVL9.DSM.Core;

using Microsoft.Data.SqlClient;
using PostSharp.Aspects;
using PostSharp.Serialization;

[PSerializable]
public class DSMEnvelopeAttribute : OnMethodBoundaryAspect
{
    public override void OnEntry(MethodExecutionArgs args)
    {
        var envelope = DSMEnvelope.Init(args.Arguments.ToArray());
        args.MethodExecutionTag = envelope;
        DSMEnvelopeManager.Instance.PushEnvelope(envelope);

        _captureHeaderValues(args);

        envelope.PrintEnvelop();
    }

    public override void OnSuccess(MethodExecutionArgs args)
    {
        var envelope = (DSMEnvelope)args.MethodExecutionTag;
        envelope.Success();
    }

    public override void OnException(MethodExecutionArgs args)
    {
        var envelope = (DSMEnvelope)args.MethodExecutionTag;

        args.FlowBehavior = FlowBehavior.Return;

        if (args.Exception is SqlException sqlException)
        {
            envelope.CaptureException(sqlException);
        }
        else
        {
            envelope.CaptureException(args.Exception);
        }

        DSMEnvelopeManager.Instance.FreezeStackWith(envelope);

        base.OnException(args);
    }

    public override void OnExit(MethodExecutionArgs args)
    {
        var envelope = (DSMEnvelope)args.MethodExecutionTag;

        if (!envelope.IsSuccessful())
        {
            DSMEnvelopeManager.Instance.FreezeStackWith(envelope);
        }

        envelope.FinishLifeCycle();

        DSMEnvelopeManager.Instance.PopEnvelope();
    }

    private void _captureHeaderValues(MethodExecutionArgs args)
    {
        if (args.Instance == null)
        {
            return;
        }
        // var httpContextAccessor = (IHttpContextAccessor) args.Instance.GetType().GetProperty("HttpContextAccessor").GetValue(args.Instance, null);
        // var controller = (Microsoft.AspNetCore.Mvc.ControllerBase)args.Instance;
        if (!(args.Instance is Microsoft.AspNetCore.Mvc.ControllerBase controller))
        {
            return;
        }
        //var httpContextAccessor = (IHttpContextAccessor) args.Instance;
        var httpContext = controller?.HttpContext;

        if (httpContext == null)
        {
            return;
        }

        // Now you can access the headers
        var headers = httpContext.Request.Headers;

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