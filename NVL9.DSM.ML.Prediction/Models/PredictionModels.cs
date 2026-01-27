using System;
using System.Collections.Generic;
using System.Linq;

namespace NVL9.DSM.ML.Prediction.Models
{
    /// <summary>
    /// Input features for performance prediction
    /// </summary>
    public class PerformancePredictionInput
    {
        // Categorical features
        public string ClassName { get; set; } = string.Empty;
        public string MethodName { get; set; } = string.Empty;
        
        // Numerical features
        public int Depth { get; set; }
        public int HourOfDay { get; set; }
        public int DayOfWeek { get; set; }
        public bool IsWeekend { get; set; }
        public bool IsBusinessHours { get; set; }
        
        // Historical features
        public double HistoricalAvg { get; set; }
        public double HistoricalStd { get; set; }
        public double HistoricalP95 { get; set; }
        public double SuccessRate { get; set; }
        public int MethodPopularity { get; set; }
        
        /// <summary>
        /// Convert to feature vector for ONNX model
        /// Features must be in the exact order used during training
        /// </summary>
        public float[] ToFeatureVector(
            Dictionary<string, int> classNameEncoder,
            Dictionary<string, int> methodNameEncoder)
        {
            // Get encoded categorical values
            var classNameEncoded = classNameEncoder.GetValueOrDefault(ClassName, -1);
            var methodNameEncoded = methodNameEncoder.GetValueOrDefault(MethodName, -1);
            
            // If unknown class/method, use fallback (e.g., most common)
            if (classNameEncoded == -1)
                classNameEncoded = classNameEncoder.Values.FirstOrDefault();
            if (methodNameEncoded == -1)
                methodNameEncoded = methodNameEncoder.Values.FirstOrDefault();
            
            // Feature order MUST match training:
            // [className, methodName, depth, hourOfDay, dayOfWeek, isWeekend, isBusinessHours,
            //  historical_avg, historical_std, historical_p95, success_rate, method_popularity]
            
            return new float[]
            {
                classNameEncoded,               // 0: className (encoded)
                methodNameEncoded,              // 1: methodName (encoded)
                Depth,                          // 2: depth
                HourOfDay,                      // 3: hourOfDay
                DayOfWeek,                      // 4: dayOfWeek
                IsWeekend ? 1f : 0f,           // 5: isWeekend (scaled)
                IsBusinessHours ? 1f : 0f,     // 6: isBusinessHours (scaled)
                (float)HistoricalAvg,          // 7: historical_avg (scaled)
                (float)HistoricalStd,          // 8: historical_std (scaled)
                (float)HistoricalP95,          // 9: historical_p95 (scaled)
                (float)SuccessRate,            // 10: success_rate (scaled)
                MethodPopularity               // 11: method_popularity (scaled)
            };
        }
    }
    
    /// <summary>
    /// Output of performance prediction
    /// </summary>
    public class PerformancePredictionOutput
    {
        public float PredictedExecutionTimeMs { get; set; }
        public float ConfidenceLower { get; set; }  // 5th percentile
        public float ConfidenceUpper { get; set; }  // 95th percentile
        public DateTime PredictionTimestamp { get; set; }
        public string ModelVersion { get; set; } = string.Empty;
        
        public override string ToString()
        {
            return $"Predicted: {PredictedExecutionTimeMs:F2}ms " +
                   $"(Range: {ConfidenceLower:F2}-{ConfidenceUpper:F2}ms)";
        }
    }
    
    /// <summary>
    /// Prediction result with additional metadata
    /// </summary>
    public class PredictionResult
    {
        public PerformancePredictionInput Input { get; set; } = new();
        public PerformancePredictionOutput Output { get; set; } = new();
        public double InferenceTimeMs { get; set; }
        public bool IsSlowPrediction { get; set; }  // > 1000ms
        public string RecommendedAction { get; set; } = string.Empty;
        
        public PredictionResult()
        {
        }
        
        public PredictionResult(
            PerformancePredictionInput input,
            PerformancePredictionOutput output,
            double inferenceTimeMs)
        {
            Input = input;
            Output = output;
            InferenceTimeMs = inferenceTimeMs;
            IsSlowPrediction = output.PredictedExecutionTimeMs > 1000;
            
            // Generate recommendation
            if (IsSlowPrediction)
            {
                RecommendedAction = "Consider using cache or optimizing this method";
            }
            else if (output.PredictedExecutionTimeMs > 500)
            {
                RecommendedAction = "Monitor execution time closely";
            }
            else
            {
                RecommendedAction = "Normal execution expected";
            }
        }
    }
}
