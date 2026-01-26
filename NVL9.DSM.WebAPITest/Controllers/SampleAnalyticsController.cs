using Microsoft.AspNetCore.Mvc;
using NVL9.DSM.Core;
using NVL9.DSM.Core.Codes;
using NVL9.DSM.Core.Persistence;

namespace NVL9.DSM.WebAPITest.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SampleAnalyticsController : ControllerBase
    {
        private readonly IDSMEnvelopeRepository _repository;

        public SampleAnalyticsController(IDSMEnvelopeRepository repository)
        {
            _repository = repository;
        }

        /// <summary>
        /// Example: Create and persist an envelope
        /// </summary>
        [HttpGet("create-sample")]
        public async Task<ActionResult<DSMEnvelope<string>>> CreateSample()
        {
            var envelope = DSMEnvelope<string>
                .InitWithCaller(nameof(SampleAnalyticsController))
                .CaptureAndSetHeaders(HttpContext);

            try
            {
                // Simulate some work
                await Task.Delay(100);
                
                var result = $"Sample data created at {DateTime.UtcNow}";
                
                // Persist with success
                await envelope.SuccessAndPersistAsync(result, includePayload: true);
                
                return Ok(envelope);
            }
            catch (Exception ex)
            {
                await envelope.CaptureExceptionAndPersistAsync(ex);
                return StatusCode(500, envelope);
            }
        }

        /// <summary>
        /// Example: Analyze a request by RootEnvelopID
        /// </summary>
        [HttpGet("analyze/{rootEnvelopID}")]
        public async Task<ActionResult> AnalyzeRequest(string rootEnvelopID)
        {
            var envelope = DSMEnvelope<object>
                .InitWithCaller(nameof(SampleAnalyticsController))
                .CaptureAndSetHeaders(HttpContext);

            try
            {
                // Get all envelopes for this request
                var envelopes = await _repository.GetByRootEnvelopIDAsync(rootEnvelopID);
                
                if (!envelopes.Any())
                {
                    envelope.SetState(
                        DSMEnvelopeCodeManager.Manager.Find(DSMEnvelopeCodeEnum.API_APPVLD_02001),
                        $"No envelopes found for RootEnvelopID: {rootEnvelopID}"
                    );
                    return NotFound(envelope);
                }

                // Build execution graph
                var analytics = new NVL9.DSM.Core.Analytics.DSMEnvelopeAnalytics();
                var analysis = analytics.BuildExecutionGraph(envelopes);

                var result = new
                {
                    rootEnvelopID,
                    totalExecutionTimeMs = analysis.TotalExecutionTimeMs,
                    totalNodes = analysis.TotalNodes,
                    maxDepth = analysis.MaxDepth,
                    errorCount = analysis.ErrorCount,
                    isSuccessful = analysis.IsSuccessful,
                    errors = analysis.Errors.Select(e => new
                    {
                        e.MethodName,
                        e.ClassName,
                        e.Code,
                        e.Message,
                        e.ExecutionTimeMs
                    }),
                    bottlenecks = analysis.Bottlenecks.Select(b => new
                    {
                        b.MethodName,
                        b.ClassName,
                        b.ExecutionTimeMs,
                        percentOfTotal = $"{b.PercentOfTotal:F1}%",
                        b.Reason
                    }),
                    criticalPath = analysis.CriticalPath.Select(n => new
                    {
                        n.MethodName,
                        n.ExecutionTimeMs
                    })
                };

                envelope.Success(result);
                return Ok(result);
            }
            catch (Exception ex)
            {
                envelope.CaptureException(ex);
                return StatusCode(500, envelope);
            }
        }

        /// <summary>
        /// Example: Get method statistics
        /// </summary>
        [HttpGet("method-stats/{methodName}")]
        public async Task<ActionResult> GetMethodStats(string methodName)
        {
            var envelope = DSMEnvelope<object>
                .InitWithCaller(nameof(SampleAnalyticsController))
                .CaptureAndSetHeaders(HttpContext);

            try
            {
                var envelopes = await _repository.GetByMethodNameAsync(methodName, limit: 1000);
                
                if (!envelopes.Any())
                {
                    envelope.SetState(
                        DSMEnvelopeCodeManager.Manager.Find(DSMEnvelopeCodeEnum.API_APPVLD_02001),
                        $"No data found for method: {methodName}"
                    );
                    return NotFound(envelope);
                }

                var analytics = new NVL9.DSM.Core.Analytics.DSMEnvelopeAnalytics();
                var stats = analytics.CalculateMethodStatistics(envelopes);

                var result = new
                {
                    stats.MethodName,
                    stats.ClassName,
                    stats.TotalCalls,
                    stats.SuccessCount,
                    stats.ErrorCount,
                    successRate = $"{stats.SuccessRate:F2}%",
                    performance = new
                    {
                        minMs = stats.MinExecutionTimeMs,
                        maxMs = stats.MaxExecutionTimeMs,
                        avgMs = $"{stats.AvgExecutionTimeMs:F2}",
                        medianMs = $"{stats.MedianExecutionTimeMs:F2}",
                        p95Ms = $"{stats.P95ExecutionTimeMs:F2}",
                        p99Ms = $"{stats.P99ExecutionTimeMs:F2}"
                    },
                    stats.ErrorCodeDistribution
                };

                envelope.Success(result);
                return Ok(result);
            }
            catch (Exception ex)
            {
                envelope.CaptureException(ex);
                return StatusCode(500, envelope);
            }
        }

        /// <summary>
        /// Example: Find slow executions
        /// </summary>
        [HttpGet("slow-executions")]
        public async Task<ActionResult> GetSlowExecutions([FromQuery] long thresholdMs = 1000)
        {
            var envelope = DSMEnvelope<object>
                .InitWithCaller(nameof(SampleAnalyticsController))
                .CaptureAndSetHeaders(HttpContext);

            try
            {
                var slowOps = await _repository.GetSlowExecutionsAsync(thresholdMs, limit: 50);
                
                var result = slowOps.Select(e => new
                {
                    e.RootEnvelopID,
                    e.MethodName,
                    e.ClassName,
                    e.ExecutionTimeMs,
                    e.StartTime,
                    e.Code,
                    e.Message,
                    e.FilePath,
                    e.LineNumber
                }).ToList();

                envelope.Success(result);
                return Ok(result);
            }
            catch (Exception ex)
            {
                envelope.CaptureException(ex);
                return StatusCode(500, envelope);
            }
        }

        /// <summary>
        /// Example: Generate Mermaid graph for a request
        /// </summary>
        [HttpGet("mermaid/{rootEnvelopID}")]
        public async Task<ActionResult<string>> GetMermaidGraph(string rootEnvelopID)
        {
            var envelope = DSMEnvelope<string>
                .InitWithCaller(nameof(SampleAnalyticsController))
                .CaptureAndSetHeaders(HttpContext);

            try
            {
                var envelopes = await _repository.GetByRootEnvelopIDAsync(rootEnvelopID);
                
                if (!envelopes.Any())
                {
                    envelope.SetState(
                        DSMEnvelopeCodeManager.Manager.Find(DSMEnvelopeCodeEnum.API_APPVLD_02001),
                        $"No envelopes found for RootEnvelopID: {rootEnvelopID}"
                    );
                    return NotFound(envelope);
                }

                var analytics = new NVL9.DSM.Core.Analytics.DSMEnvelopeAnalytics();
                var analysis = analytics.BuildExecutionGraph(envelopes);
                var mermaidCode = analytics.ExportToMermaid(analysis);

                envelope.Success(mermaidCode);
                return Content(mermaidCode, "text/plain");
            }
            catch (Exception ex)
            {
                envelope.CaptureException(ex);
                return StatusCode(500, envelope);
            }
        }

        /// <summary>
        /// Example: Track distributed request by ApiTraceId
        /// </summary>
        [HttpGet("trace/{apiTraceId}")]
        public async Task<ActionResult> TraceDistributedRequest(string apiTraceId)
        {
            var envelope = DSMEnvelope<object>
                .InitWithCaller(nameof(SampleAnalyticsController))
                .CaptureAndSetHeaders(HttpContext);

            try
            {
                var envelopes = await _repository.GetByApiTraceIdAsync(apiTraceId);
                
                if (!envelopes.Any())
                {
                    envelope.SetState(
                        DSMEnvelopeCodeManager.Manager.Find(DSMEnvelopeCodeEnum.API_APPVLD_02001),
                        $"No envelopes found for ApiTraceId: {apiTraceId}"
                    );
                    return NotFound(envelope);
                }

                var result = envelopes
                    .OrderBy(e => e.StartTime)
                    .Select(e => new
                    {
                        e.RootEnvelopID,
                        e.UniqueIEID,
                        e.MethodName,
                        e.ClassName,
                        e.ExecutionTimeMs,
                        e.StartTime,
                        e.Code,
                        e.IsError,
                        e.Depth
                    })
                    .ToList();

                envelope.Success(result);
                return Ok(result);
            }
            catch (Exception ex)
            {
                envelope.CaptureException(ex);
                return StatusCode(500, envelope);
            }
        }
    }
}
