using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace NVL9.DSM.Core.Persistence
{
    /// <summary>
    /// Document model for persisting DSMEnvelope data to database (e.g., Cosmos DB).
    /// Optimized for graph traversal and time-series analysis.
    /// </summary>
    public class DSMEnvelopeDocument
    {
        /// <summary>
        /// Unique document ID (Cosmos DB 'id' field). Use UniqueIEID for uniqueness.
        /// </summary>
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Partition key: RootEnvelopID enables efficient querying of entire request chains.
        /// </summary>
        [JsonProperty("partitionKey")]
        public string PartitionKey { get; set; } = string.Empty;

        /// <summary>
        /// Unique envelope identifier (GUID|PID format)
        /// </summary>
        [JsonProperty("uniqueIEID")]
        public string UniqueIEID { get; set; } = string.Empty;

        /// <summary>
        /// Root envelope ID (timestamp-based identifier for the request)
        /// </summary>
        [JsonProperty("rootEnvelopID")]
        public string RootEnvelopID { get; set; } = string.Empty;

        /// <summary>
        /// Parent envelope ID (for graph traversal). Empty if this is the root.
        /// </summary>
        [JsonProperty("parentEnvelopID")]
        public string ParentEnvelopID { get; set; } = string.Empty;

        /// <summary>
        /// Error envelope ID (set when error occurs)
        /// </summary>
        [JsonProperty("errorIEID")]
        public string? ErrorIEID { get; set; }

        /// <summary>
        /// HTTP Api-Trace-Id header value for distributed tracing
        /// </summary>
        [JsonProperty("apiTraceId")]
        public string? ApiTraceId { get; set; }

        /// <summary>
        /// HTTP Idempotency-Key-Id header value
        /// </summary>
        [JsonProperty("idempotencyKeyId")]
        public string? IdempotencyKeyId { get; set; }

        /// <summary>
        /// Status code (GEN_COMMON_00000 = success, GEN_COMMON_00001 = initialized, etc.)
        /// </summary>
        [JsonProperty("code")]
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// Numeric code value for easier filtering
        /// </summary>
        [JsonProperty("codeValue")]
        public int CodeValue { get; set; }

        /// <summary>
        /// Canned message tied to the code
        /// </summary>
        [JsonProperty("codeMessage")]
        public string CodeMessage { get; set; } = string.Empty;

        /// <summary>
        /// System/code-generated message (specific to error/warning)
        /// </summary>
        [JsonProperty("message")]
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Execution time in milliseconds
        /// </summary>
        [JsonProperty("executionTimeMs")]
        public long ExecutionTimeMs { get; set; }

        /// <summary>
        /// Execution time as formatted string
        /// </summary>
        [JsonProperty("executionTime")]
        public string ExecutionTime { get; set; } = string.Empty;

        /// <summary>
        /// Start timestamp (ISO 8601 format)
        /// </summary>
        [JsonProperty("startTime")]
        public DateTimeOffset StartTime { get; set; }

        /// <summary>
        /// End timestamp (ISO 8601 format)
        /// </summary>
        [JsonProperty("endTime")]
        public DateTimeOffset EndTime { get; set; }

        /// <summary>
        /// Class name in C# code
        /// </summary>
        [JsonProperty("className")]
        public string ClassName { get; set; } = string.Empty;

        /// <summary>
        /// Method/function name (the Pure Function)
        /// </summary>
        [JsonProperty("methodName")]
        public string MethodName { get; set; } = string.Empty;

        /// <summary>
        /// File path where the code lives
        /// </summary>
        [JsonProperty("filePath")]
        public string FilePath { get; set; } = string.Empty;

        /// <summary>
        /// Line number in the file
        /// </summary>
        [JsonProperty("lineNumber")]
        public int LineNumber { get; set; }

        /// <summary>
        /// Document type for polymorphic queries
        /// </summary>
        [JsonProperty("documentType")]
        public string DocumentType { get; set; } = "DSMEnvelope";

        /// <summary>
        /// Creation timestamp (when document was persisted)
        /// </summary>
        [JsonProperty("createdAt")]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Application name/ID for multi-tenant scenarios
        /// </summary>
        [JsonProperty("applicationId")]
        public string? ApplicationId { get; set; }

        /// <summary>
        /// Environment (dev, staging, prod)
        /// </summary>
        [JsonProperty("environment")]
        public string? Environment { get; set; }

        /// <summary>
        /// Optional: Serialized payload data (if needed for analysis)
        /// </summary>
        [JsonProperty("payloadSnapshot")]
        public string? PayloadSnapshot { get; set; }

        /// <summary>
        /// Graph depth (0 = root, 1 = first level child, etc.)
        /// Calculated during persistence for easier querying.
        /// </summary>
        [JsonProperty("depth")]
        public int Depth { get; set; }

        /// <summary>
        /// Whether this envelope represents an error state
        /// </summary>
        [JsonProperty("isError")]
        public bool IsError { get; set; }

        /// <summary>
        /// Whether this envelope represents a successful state
        /// </summary>
        [JsonProperty("isSuccess")]
        public bool IsSuccess { get; set; }

        /// <summary>
        /// Tags for custom categorization and filtering
        /// </summary>
        [JsonProperty("tags")]
        public List<string> Tags { get; set; } = new List<string>();

        /// <summary>
        /// Creates a document from a DSMEnvelope instance
        /// </summary>
        public static DSMEnvelopeDocument FromEnvelope<T>(IDSMEnvelope envelope, string? payloadJson = null)
        {
            var codeString = envelope.Code?.ToString() ?? "UNKNOWN";
            var codeValue = envelope.Code?.Code ?? -1;
            var isSuccess = codeString.Contains("00000");
            var isError = !isSuccess && !codeString.Contains("00001");

            // Cast to concrete type to access CodeBlockInfo
            var concreteEnvelope = envelope as DSMEnvelope<T>;
            var codeBlock = concreteEnvelope?.CodeBlockInfo;

            return new DSMEnvelopeDocument
            {
                Id = envelope.UniqueIEID.Replace("|", "_"), // Cosmos DB ID friendly
                PartitionKey = envelope.RootEnvelopID,
                UniqueIEID = envelope.UniqueIEID,
                RootEnvelopID = envelope.RootEnvelopID,
                ParentEnvelopID = envelope.ParentEnvelopID ?? string.Empty,
                ErrorIEID = envelope.ErrorIEID,
                ApiTraceId = envelope.ApiTraceId,
                IdempotencyKeyId = envelope.IdempotencyKeyId,
                Code = codeString,
                CodeValue = codeValue,
                CodeMessage = envelope.Code?.ErrorMessage ?? string.Empty,
                Message = envelope.DTOMessage,
                ExecutionTimeMs = ParseExecutionTimeMs(envelope.ExecutionTime),
                ExecutionTime = envelope.ExecutionTime,
                StartTime = envelope.StartTime,
                EndTime = envelope.EndTime,
                ClassName = codeBlock?.ClassName ?? string.Empty,
                MethodName = codeBlock?.Method ?? string.Empty,
                FilePath = codeBlock?.FilePath?.ToString() ?? string.Empty,
                LineNumber = codeBlock?.LineNumber != null ? Convert.ToInt32(codeBlock.LineNumber) : 0,
                PayloadSnapshot = payloadJson,
                IsError = isError,
                IsSuccess = isSuccess,
                // Depth will be calculated by repository based on parent chain
                Depth = 0
            };
        }

        private static long ParseExecutionTimeMs(string executionTime)
        {
            if (string.IsNullOrEmpty(executionTime))
                return 0;

            // Parse "1250ms" format
            var cleaned = executionTime.Replace("ms", "").Trim();
            return long.TryParse(cleaned, out var ms) ? ms : 0;
        }
    }
}
