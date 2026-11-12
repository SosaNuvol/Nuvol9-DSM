using System;
using System.Collections.Generic;
using NVL9.DSM.ML.Prediction.Models;
using NVL9.DSM.ML.Prediction.Services;

namespace NVL9.DSM.ML.Prediction
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine(new string('=', 80));
            Console.WriteLine("DSMEnvelope Performance Prediction Service");
            Console.WriteLine(new string('=', 80));
            
            try
            {
                // Initialize predictor
                using var predictor = new DSMPerformancePredictor("models/dsm_performance_rf.onnx");
                
                if (!predictor.IsLoaded)
                {
                    Console.WriteLine("❌ Failed to load model");
                    return;
                }
                
                Console.WriteLine("\n✅ Model loaded successfully!\n");
                
                // Run demo predictions
                RunDemoPredictions(predictor);
                
                // Interactive mode
                Console.WriteLine("\n" + new string('=', 80));
                Console.WriteLine("Interactive Prediction Mode");
                Console.WriteLine(new string('=', 80));
                Console.WriteLine("Enter 'q' to quit\n");
                
                while (true)
                {
                    Console.Write("Enter method name (or 'q' to quit): ");
                    var input = Console.ReadLine();
                    
                    if (string.IsNullOrWhiteSpace(input) || input.ToLower() == "q")
                        break;
                    
                    // Create prediction input
                    var predInput = new PerformancePredictionInput
                    {
                        ClassName = "PersonController",
                        MethodName = input,
                        Depth = 0,
                        HourOfDay = DateTime.Now.Hour,
                        DayOfWeek = (int)DateTime.Now.DayOfWeek,
                        IsWeekend = DateTime.Now.DayOfWeek >= DayOfWeek.Saturday,
                        IsBusinessHours = DateTime.Now.Hour >= 9 && DateTime.Now.Hour <= 17,
                        HistoricalAvg = 150.0,
                        HistoricalStd = 50.0,
                        HistoricalP95 = 250.0,
                        SuccessRate = 98.5,
                        MethodPopularity = 1000
                    };
                    
                    var result = predictor.Predict(predInput);
                    
                    Console.WriteLine($"\n🎯 Prediction Result:");
                    Console.WriteLine($"   {result.Output}");
                    Console.WriteLine($"   Inference Time: {result.InferenceTimeMs:F2}ms");
                    Console.WriteLine($"   Recommendation: {result.RecommendedAction}\n");
                }
                
                Console.WriteLine("\n👋 Goodbye!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ Error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }
        
        static void RunDemoPredictions(DSMPerformancePredictor predictor)
        {
            Console.WriteLine("📊 Running Demo Predictions...\n");
            
            var demoInputs = new List<(string className, string methodName, int depth)>
            {
                ("PersonController", "GetById", 0),
                ("PersonController", "GetAll", 0),
                ("OrderController", "CreateOrder", 0),
                ("PaymentController", "ProcessPayment", 0),
                ("ReportController", "GenerateSalesReport", 0),
            };
            
            foreach (var (className, methodName, depth) in demoInputs)
            {
                var input = new PerformancePredictionInput
                {
                    ClassName = className,
                    MethodName = methodName,
                    Depth = depth,
                    HourOfDay = 14,  // 2 PM (business hours)
                    DayOfWeek = 2,   // Wednesday
                    IsWeekend = false,
                    IsBusinessHours = true,
                    HistoricalAvg = 150.0,
                    HistoricalStd = 50.0,
                    HistoricalP95 = 250.0,
                    SuccessRate = 98.5,
                    MethodPopularity = 1000
                };
                
                var result = predictor.Predict(input);
                
                Console.WriteLine($"Method: {className}.{methodName}");
                Console.WriteLine($"   {result.Output}");
                Console.WriteLine($"   Inference: {result.InferenceTimeMs:F2}ms");
                Console.WriteLine($"   {result.RecommendedAction}");
                Console.WriteLine();
            }
        }
    }
}
