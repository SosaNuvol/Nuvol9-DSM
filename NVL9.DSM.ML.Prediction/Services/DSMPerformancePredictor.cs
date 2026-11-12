using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.ML;
using Microsoft.ML.Data;
using Newtonsoft.Json;
using NVL9.DSM.ML.Prediction.Models;

namespace NVL9.DSM.ML.Prediction.Services
{
    /// <summary>
    /// ONNX-based performance prediction service
    /// Loads trained ONNX model and provides real-time predictions
    /// </summary>
    public class DSMPerformancePredictor : IDisposable
    {
        private readonly MLContext _mlContext;
        private readonly ITransformer? _model;
        private readonly PredictionEngine<OnnxInput, OnnxOutput>? _predictionEngine;
        
        private readonly Dictionary<string, int> _classNameEncoder;
        private readonly Dictionary<string, int> _methodNameEncoder;
        private readonly List<string> _featureNames;
        
        private readonly string _modelVersion;
        private bool _disposed = false;
        
        public bool IsLoaded => _model != null && _predictionEngine != null;
        
        public DSMPerformancePredictor(string modelPath = "models/dsm_performance_rf.onnx")
        {
            _mlContext = new MLContext(seed: 42);
            
            Console.WriteLine($"Loading ONNX model from: {modelPath}");
            
            if (!File.Exists(modelPath))
            {
                throw new FileNotFoundException($"ONNX model not found: {modelPath}");
            }
            
            try
            {
                // Load label encoders from training
                _classNameEncoder = LoadLabelEncoder("models/className_encoder.json");
                _methodNameEncoder = LoadLabelEncoder("models/methodName_encoder.json");
                
                // Load feature names
                var featuresPath = "models/feature_names.json";
                if (File.Exists(featuresPath))
                {
                    var json = File.ReadAllText(featuresPath);
                    _featureNames = JsonConvert.DeserializeObject<List<string>>(json) ?? new List<string>();
                }
                else
                {
                    _featureNames = new List<string>();
                }
                
                // Create ONNX pipeline
                var pipeline = _mlContext.Transforms.ApplyOnnxModel(
                    modelFile: modelPath,
                    outputColumnNames: new[] { "variable" },
                    inputColumnNames: new[] { "float_input" }
                );
                
                // Create dummy data for schema
                var emptyData = _mlContext.Data.LoadFromEnumerable(new List<OnnxInput>());
                _model = pipeline.Fit(emptyData);
                
                // Create prediction engine
                _predictionEngine = _mlContext.Model.CreatePredictionEngine<OnnxInput, OnnxOutput>(_model);
                
                _modelVersion = $"ONNX-{DateTime.Now:yyyyMMdd}";
                
                Console.WriteLine("✅ ONNX model loaded successfully");
                Console.WriteLine($"   Features: {_featureNames.Count}");
                Console.WriteLine($"   ClassNames: {_classNameEncoder.Count}");
                Console.WriteLine($"   MethodNames: {_methodNameEncoder.Count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error loading ONNX model: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Predict execution time for a Pure Function
        /// </summary>
        public PredictionResult Predict(PerformancePredictionInput input)
        {
            if (!IsLoaded)
            {
                throw new InvalidOperationException("Model not loaded");
            }
            
            var stopwatch = Stopwatch.StartNew();
            
            // Convert to feature vector
            var features = input.ToFeatureVector(_classNameEncoder, _methodNameEncoder);
            
            // Create ONNX input
            var onnxInput = new OnnxInput { float_input = features };
            
            // Predict
            var onnxOutput = _predictionEngine!.Predict(onnxInput);
            
            stopwatch.Stop();
            
            // Extract prediction (first value in output array)
            var predictedTime = onnxOutput.variable[0];
            
            // Calculate confidence interval (±20% for now, can be improved with quantile regression)
            var confidenceLower = predictedTime * 0.8f;
            var confidenceUpper = predictedTime * 1.2f;
            
            var output = new PerformancePredictionOutput
            {
                PredictedExecutionTimeMs = predictedTime,
                ConfidenceLower = confidenceLower,
                ConfidenceUpper = confidenceUpper,
                PredictionTimestamp = DateTime.UtcNow,
                ModelVersion = _modelVersion
            };
            
            return new PredictionResult(input, output, stopwatch.Elapsed.TotalMilliseconds);
        }
        
        /// <summary>
        /// Batch predict for multiple inputs
        /// </summary>
        public List<PredictionResult> PredictBatch(List<PerformancePredictionInput> inputs)
        {
            return inputs.Select(Predict).ToList();
        }
        
        /// <summary>
        /// Load label encoder from JSON file
        /// </summary>
        private Dictionary<string, int> LoadLabelEncoder(string path)
        {
            if (!File.Exists(path))
            {
                Console.WriteLine($"⚠️ Label encoder not found: {path}, using defaults");
                return new Dictionary<string, int>();
            }
            
            var json = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<Dictionary<string, int>>(json) ?? new Dictionary<string, int>();
        }
        
        public void Dispose()
        {
            if (!_disposed)
            {
                _predictionEngine?.Dispose();
                _disposed = true;
            }
        }
    }
    
    /// <summary>
    /// ONNX input class (must match ONNX model input)
    /// </summary>
    public class OnnxInput
    {
        [VectorType(12)]  // Must match number of features
        [ColumnName("float_input")]
        public float[] float_input { get; set; } = Array.Empty<float>();
    }
    
    /// <summary>
    /// ONNX output class (must match ONNX model output)
    /// </summary>
    public class OnnxOutput
    {
        [VectorType(1)]  // Single prediction value
        [ColumnName("variable")]
        public float[] variable { get; set; } = Array.Empty<float>();
    }
}
