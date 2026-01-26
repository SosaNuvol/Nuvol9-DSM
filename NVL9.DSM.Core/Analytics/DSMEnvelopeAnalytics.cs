using System;
using System.Collections.Generic;
using System.Linq;
using NVL9.DSM.Core.Persistence;

namespace NVL9.DSM.Core.Analytics
{
    /// <summary>
    /// Service for analyzing DSMEnvelope execution graphs and generating insights.
    /// Provides bottleneck detection, critical path analysis, and performance metrics.
    /// </summary>
    public class DSMEnvelopeAnalytics
    {
        /// <summary>
        /// Build an execution graph from a list of envelope documents
        /// </summary>
        public RequestExecutionAnalysis BuildExecutionGraph(List<DSMEnvelopeDocument> envelopes)
        {
            if (envelopes == null || !envelopes.Any())
            {
                throw new ArgumentException("Envelope list cannot be empty", nameof(envelopes));
            }

            // Find root node (no parent or parent is empty)
            var root = envelopes.FirstOrDefault(e => string.IsNullOrEmpty(e.ParentEnvelopID));
            if (root == null)
            {
                // If no explicit root, use the earliest envelope
                root = envelopes.OrderBy(e => e.StartTime).First();
            }

            var rootNode = BuildNodeTree(root, envelopes);
            
            var analysis = new RequestExecutionAnalysis
            {
                RootEnvelopID = root.RootEnvelopID,
                RootNode = rootNode,
                TotalNodes = envelopes.Count,
                RequestStartTime = envelopes.Min(e => e.StartTime),
                RequestEndTime = envelopes.Max(e => e.EndTime)
            };

            analysis.TotalExecutionTimeMs = (long)(analysis.RequestEndTime - analysis.RequestStartTime).TotalMilliseconds;
            analysis.MaxDepth = CalculateMaxDepth(rootNode);
            analysis.ErrorCount = envelopes.Count(e => e.IsError);
            analysis.IsSuccessful = envelopes.All(e => e.IsSuccess || e.Code.Contains("00001"));
            analysis.Errors = GetAllErrors(rootNode);
            analysis.Bottlenecks = DetectBottlenecks(rootNode, analysis.TotalExecutionTimeMs);
            analysis.CriticalPath = FindCriticalPath(rootNode);

            return analysis;
        }

        /// <summary>
        /// Build a hierarchical tree of nodes
        /// </summary>
        private EnvelopeGraphNode BuildNodeTree(DSMEnvelopeDocument current, List<DSMEnvelopeDocument> allEnvelopes)
        {
            var node = new EnvelopeGraphNode
            {
                UniqueIEID = current.UniqueIEID,
                RootEnvelopID = current.RootEnvelopID,
                ParentEnvelopID = current.ParentEnvelopID,
                ClassName = current.ClassName,
                MethodName = current.MethodName,
                Code = current.Code,
                Message = current.Message,
                ExecutionTimeMs = current.ExecutionTimeMs,
                StartTime = current.StartTime,
                EndTime = current.EndTime,
                IsError = current.IsError,
                IsSuccess = current.IsSuccess,
                Depth = current.Depth
            };

            // Find all children
            var children = allEnvelopes.Where(e => e.ParentEnvelopID == current.UniqueIEID).ToList();
            foreach (var child in children)
            {
                node.Children.Add(BuildNodeTree(child, allEnvelopes));
            }

            return node;
        }

        /// <summary>
        /// Calculate the maximum depth of the tree
        /// </summary>
        private int CalculateMaxDepth(EnvelopeGraphNode node, int currentDepth = 0)
        {
            if (!node.Children.Any())
                return currentDepth;

            return node.Children.Max(child => CalculateMaxDepth(child, currentDepth + 1));
        }

        /// <summary>
        /// Get all error nodes in the tree
        /// </summary>
        private List<EnvelopeGraphNode> GetAllErrors(EnvelopeGraphNode node)
        {
            var errors = new List<EnvelopeGraphNode>();
            
            if (node.IsError)
                errors.Add(node);

            foreach (var child in node.Children)
            {
                errors.AddRange(GetAllErrors(child));
            }

            return errors;
        }

        /// <summary>
        /// Detect bottlenecks based on execution time thresholds
        /// </summary>
        private List<BottleneckInfo> DetectBottlenecks(EnvelopeGraphNode node, long totalExecutionTime, double thresholdPercent = 20.0)
        {
            var bottlenecks = new List<BottleneckInfo>();
            var threshold = totalExecutionTime * (thresholdPercent / 100.0);

            CollectBottlenecks(node, totalExecutionTime, threshold, bottlenecks);

            return bottlenecks.OrderByDescending(b => b.ExecutionTimeMs).ToList();
        }

        /// <summary>
        /// Recursively collect bottleneck nodes
        /// </summary>
        private void CollectBottlenecks(EnvelopeGraphNode node, long totalTime, double threshold, List<BottleneckInfo> bottlenecks)
        {
            if (node.ExecutionTimeMs >= threshold)
            {
                var percentOfTotal = (node.ExecutionTimeMs / (double)totalTime) * 100.0;
                bottlenecks.Add(new BottleneckInfo
                {
                    UniqueIEID = node.UniqueIEID,
                    ClassName = node.ClassName,
                    MethodName = node.MethodName,
                    ExecutionTimeMs = node.ExecutionTimeMs,
                    PercentOfTotal = percentOfTotal,
                    Reason = $"Execution time {node.ExecutionTimeMs}ms is {percentOfTotal:F1}% of total request time"
                });
            }

            foreach (var child in node.Children)
            {
                CollectBottlenecks(child, totalTime, threshold, bottlenecks);
            }
        }

        /// <summary>
        /// Find the critical path (longest execution path through the graph)
        /// </summary>
        private List<EnvelopeGraphNode> FindCriticalPath(EnvelopeGraphNode node)
        {
            var path = new List<EnvelopeGraphNode> { node };

            if (!node.Children.Any())
                return path;

            // Find the child with the longest execution time
            var longestChild = node.Children.OrderByDescending(c => c.ExecutionTimeMs).First();
            path.AddRange(FindCriticalPath(longestChild));

            return path;
        }

        /// <summary>
        /// Calculate statistics for a specific method across multiple requests
        /// </summary>
        public MethodStatistics CalculateMethodStatistics(List<DSMEnvelopeDocument> envelopes)
        {
            if (envelopes == null || !envelopes.Any())
            {
                throw new ArgumentException("Envelope list cannot be empty", nameof(envelopes));
            }

            var first = envelopes.First();
            var executionTimes = envelopes.Select(e => e.ExecutionTimeMs).OrderBy(t => t).ToList();
            var successCount = envelopes.Count(e => e.IsSuccess);
            var errorCount = envelopes.Count(e => e.IsError);

            var stats = new MethodStatistics
            {
                MethodName = first.MethodName,
                ClassName = first.ClassName,
                TotalCalls = envelopes.Count,
                SuccessCount = successCount,
                ErrorCount = errorCount,
                SuccessRate = (successCount / (double)envelopes.Count) * 100.0,
                MinExecutionTimeMs = executionTimes.Min(),
                MaxExecutionTimeMs = executionTimes.Max(),
                AvgExecutionTimeMs = executionTimes.Average(),
                MedianExecutionTimeMs = CalculatePercentile(executionTimes, 50),
                P95ExecutionTimeMs = CalculatePercentile(executionTimes, 95),
                P99ExecutionTimeMs = CalculatePercentile(executionTimes, 99)
            };

            // Error code distribution
            var errorCodes = envelopes
                .Where(e => e.IsError)
                .GroupBy(e => e.Code)
                .ToDictionary(g => g.Key, g => g.Count());

            stats.ErrorCodeDistribution = errorCodes;

            return stats;
        }

        /// <summary>
        /// Calculate percentile value from sorted list
        /// </summary>
        private double CalculatePercentile(List<long> sortedValues, double percentile)
        {
            if (!sortedValues.Any())
                return 0;

            var index = (percentile / 100.0) * (sortedValues.Count - 1);
            var lower = (int)Math.Floor(index);
            var upper = (int)Math.Ceiling(index);

            if (lower == upper)
                return sortedValues[lower];

            var weight = index - lower;
            return sortedValues[lower] * (1 - weight) + sortedValues[upper] * weight;
        }

        /// <summary>
        /// Export execution graph as Mermaid diagram syntax
        /// </summary>
        public string ExportToMermaid(RequestExecutionAnalysis analysis)
        {
            var lines = new List<string>
            {
                "graph TD",
                $"    classDef success fill:#90EE90,stroke:#333,stroke-width:2px",
                $"    classDef error fill:#FFB6C1,stroke:#333,stroke-width:2px",
                $"    classDef warning fill:#FFE4B5,stroke:#333,stroke-width:2px",
                ""
            };

            ExportNodeToMermaid(analysis.RootNode, lines);

            return string.Join(Environment.NewLine, lines);
        }

        /// <summary>
        /// Recursively export nodes to Mermaid syntax
        /// </summary>
        private void ExportNodeToMermaid(EnvelopeGraphNode node, List<string> lines, string? parentId = null)
        {
            var nodeId = SanitizeNodeId(node.UniqueIEID);
            var label = $"{node.MethodName}<br/>{node.ExecutionTimeMs}ms";
            var cssClass = node.IsError ? "error" : node.IsSuccess ? "success" : "warning";

            lines.Add($"    {nodeId}[\"{label}\"]:::{cssClass}");

            if (parentId != null)
            {
                lines.Add($"    {parentId} --> {nodeId}");
            }

            foreach (var child in node.Children)
            {
                ExportNodeToMermaid(child, lines, nodeId);
            }
        }

        /// <summary>
        /// Sanitize node ID for Mermaid syntax
        /// </summary>
        private string SanitizeNodeId(string uniqueIEID)
        {
            return uniqueIEID.Replace("|", "_").Replace("-", "_").Replace(":", "_");
        }
    }
}
