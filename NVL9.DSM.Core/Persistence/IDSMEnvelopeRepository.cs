using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NVL9.DSM.Core.Persistence
{
    /// <summary>
    /// Repository interface for persisting and querying DSMEnvelope data.
    /// Supports graph traversal and analytics queries.
    /// </summary>
    public interface IDSMEnvelopeRepository
    {
        /// <summary>
        /// Save a single envelope document
        /// </summary>
        Task<DSMEnvelopeDocument> SaveAsync(DSMEnvelopeDocument document);

        /// <summary>
        /// Batch save multiple envelopes (optimized for bulk inserts)
        /// </summary>
        Task<int> SaveBatchAsync(IEnumerable<DSMEnvelopeDocument> documents);

        /// <summary>
        /// Get a single envelope by its UniqueIEID
        /// </summary>
        Task<DSMEnvelopeDocument?> GetByIdAsync(string uniqueIEID, string rootEnvelopID);

        /// <summary>
        /// Get all envelopes for a specific request (by RootEnvelopID)
        /// Returns the entire execution graph for a request.
        /// </summary>
        Task<List<DSMEnvelopeDocument>> GetByRootEnvelopIDAsync(string rootEnvelopID);

        /// <summary>
        /// Get all child envelopes for a specific parent
        /// </summary>
        Task<List<DSMEnvelopeDocument>> GetChildrenAsync(string parentEnvelopID, string rootEnvelopID);

        /// <summary>
        /// Get the full execution chain from root to a specific envelope
        /// </summary>
        Task<List<DSMEnvelopeDocument>> GetExecutionChainAsync(string uniqueIEID, string rootEnvelopID);

        /// <summary>
        /// Find all errors within a request
        /// </summary>
        Task<List<DSMEnvelopeDocument>> GetErrorsAsync(string rootEnvelopID);

        /// <summary>
        /// Get envelopes by method name (across all requests)
        /// </summary>
        Task<List<DSMEnvelopeDocument>> GetByMethodNameAsync(string methodName, int limit = 100);

        /// <summary>
        /// Get envelopes by class name (across all requests)
        /// </summary>
        Task<List<DSMEnvelopeDocument>> GetByClassNameAsync(string className, int limit = 100);

        /// <summary>
        /// Find slow executions (execution time > threshold)
        /// </summary>
        Task<List<DSMEnvelopeDocument>> GetSlowExecutionsAsync(long thresholdMs, int limit = 100);

        /// <summary>
        /// Get envelopes within a time range
        /// </summary>
        Task<List<DSMEnvelopeDocument>> GetByTimeRangeAsync(
            DateTimeOffset startTime, 
            DateTimeOffset endTime, 
            int limit = 1000);

        /// <summary>
        /// Get envelopes by HTTP trace ID
        /// </summary>
        Task<List<DSMEnvelopeDocument>> GetByApiTraceIdAsync(string apiTraceId);

        /// <summary>
        /// Delete envelopes older than a specific date (for data retention)
        /// </summary>
        Task<int> DeleteOlderThanAsync(DateTimeOffset cutoffDate);
    }
}
