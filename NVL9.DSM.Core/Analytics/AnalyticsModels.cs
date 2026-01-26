using System;
using System.Collections.Generic;
using System.Linq;

namespace NVL9.DSM.Core.Analytics
{
    /// <summary>
    /// Represents a node in the DSMEnvelope execution graph
    /// </summary>
    public class EnvelopeGraphNode
    {
        public string UniqueIEID { get; set; } = string.Empty;
        public string RootEnvelopID { get; set; } = string.Empty;
        public string ParentEnvelopID { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public string MethodName { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public long ExecutionTimeMs { get; set; }
        public DateTimeOffset StartTime { get; set; }
        public DateTimeOffset EndTime { get; set; }
        public bool IsError { get; set; }
        public bool IsSuccess { get; set; }
        public int Depth { get; set; }
        
        public List<EnvelopeGraphNode> Children { get; set; } = new List<EnvelopeGraphNode>();
    }

    /// <summary>
    /// Analytics result for a request execution graph
    /// </summary>
    public class RequestExecutionAnalysis
    {
        public string RootEnvelopID { get; set; } = string.Empty;
        public EnvelopeGraphNode RootNode { get; set; } = new EnvelopeGraphNode();
        
        /// <summary>
        /// Total execution time for entire request
        /// </summary>
        public long TotalExecutionTimeMs { get; set; }
        
        /// <summary>
        /// Total number of envelopes in the graph
        /// </summary>
        public int TotalNodes { get; set; }
        
        /// <summary>
        /// Maximum depth of the call chain
        /// </summary>
        public int MaxDepth { get; set; }
        
        /// <summary>
        /// Number of errors encountered
        /// </summary>
        public int ErrorCount { get; set; }
        
        /// <summary>
        /// Whether the overall request succeeded
        /// </summary>
        public bool IsSuccessful { get; set; }
        
        /// <summary>
        /// List of all errors in execution order
        /// </summary>
        public List<EnvelopeGraphNode> Errors { get; set; } = new List<EnvelopeGraphNode>();
        
        /// <summary>
        /// Bottlenecks: nodes with execution time > threshold
        /// </summary>
        public List<BottleneckInfo> Bottlenecks { get; set; } = new List<BottleneckInfo>();
        
        /// <summary>
        /// Critical path: longest execution path through the graph
        /// </summary>
        public List<EnvelopeGraphNode> CriticalPath { get; set; } = new List<EnvelopeGraphNode>();
        
        /// <summary>
        /// Start time of the request
        /// </summary>
        public DateTimeOffset RequestStartTime { get; set; }
        
        /// <summary>
        /// End time of the request
        /// </summary>
        public DateTimeOffset RequestEndTime { get; set; }
    }

    /// <summary>
    /// Information about a performance bottleneck
    /// </summary>
    public class BottleneckInfo
    {
        public string UniqueIEID { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public string MethodName { get; set; } = string.Empty;
        public long ExecutionTimeMs { get; set; }
        public double PercentOfTotal { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public int LineNumber { get; set; }
        
        /// <summary>
        /// Why this is considered a bottleneck
        /// </summary>
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>
    /// Aggregated statistics for a specific method across multiple requests
    /// </summary>
    public class MethodStatistics
    {
        public string MethodName { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public int TotalCalls { get; set; }
        public int SuccessCount { get; set; }
        public int ErrorCount { get; set; }
        public double SuccessRate { get; set; }
        
        public long MinExecutionTimeMs { get; set; }
        public long MaxExecutionTimeMs { get; set; }
        public double AvgExecutionTimeMs { get; set; }
        public double MedianExecutionTimeMs { get; set; }
        public double P95ExecutionTimeMs { get; set; }
        public double P99ExecutionTimeMs { get; set; }
        
        /// <summary>
        /// Most common error codes for this method
        /// </summary>
        public Dictionary<string, int> ErrorCodeDistribution { get; set; } = new Dictionary<string, int>();
    }

    /// <summary>
    /// Time-series data point for trend analysis
    /// </summary>
    public class TimeSeriesDataPoint
    {
        public DateTimeOffset Timestamp { get; set; }
        public string MethodName { get; set; } = string.Empty;
        public long ExecutionTimeMs { get; set; }
        public bool IsError { get; set; }
        public string Code { get; set; } = string.Empty;
    }
}
