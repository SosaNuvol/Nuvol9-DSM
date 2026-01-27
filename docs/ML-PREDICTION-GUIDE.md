# DSMEnvelope Performance Prediction - Complete Guide

## Overview

This system predicts the execution time of Pure Functions (DSMEnvelopes) using machine learning.

**Components:**
1. **Python Training App** (`ML.Training.Python/`) - Trains models, exports to ONNX
2. **.NET Prediction Service** (`NVL9.DSM.ML.Prediction/`) - Loads ONNX, provides predictions
3. **Integration** - Use predictions in your controllers/services

## Quick Start

### Step 1: Setup Python Environment

```bash
cd ML.Training.Python
pip install -r requirements.txt
```

### Step 2: Generate Synthetic Data

```bash
python generate_synthetic_data.py
```

**Output**: `dsm_synthetic_data.csv` (10,000 records)

### Step 3: Train Models

```bash
python train_model.py
```

**Output** (in `models/` directory):
- `dsm_performance_rf.onnx` - Random Forest model
- `dsm_performance_xgb.onnx` - XGBoost model
- `label_encoders.joblib` - Categorical encoders
- `scaler.joblib` - Feature scaler
- `metrics.json` - Model performance
- `*.png` - Visualization plots

### Step 4: Test .NET Prediction Service

```bash
cd ../NVL9.DSM.ML.Prediction
dotnet run
```

This will:
1. Load the ONNX model
2. Run demo predictions
3. Enter interactive mode

### Step 5: Copy Model Files

Copy Python output files to .NET project:

```bash
# From ML.Training.Python/models/ to NVL9.DSM.ML.Prediction/models/
cp models/dsm_performance_rf.onnx ../NVL9.DSM.ML.Prediction/models/
cp models/feature_names.json ../NVL9.DSM.ML.Prediction/models/
```

Also create encoder JSON files (Python dict to JSON):

```python
# In Python
import json
import joblib

encoders = joblib.load('models/label_encoders.joblib')

# Save className encoder
with open('models/className_encoder.json', 'w') as f:
    json.dump({k: int(v) for k, v in zip(encoders['className'].classes_, range(len(encoders['className'].classes_)))}, f)

# Save methodName encoder
with open('models/methodName_encoder.json', 'w') as f:
    json.dump({k: int(v) for k, v in zip(encoders['methodName'].classes_, range(len(encoders['methodName'].classes_)))}, f)
```

## Usage in Controllers

### Basic Usage

```csharp
public class PersonController : ControllerBase
{
    private readonly DSMPerformancePredictor _predictor;
    
    public PersonController(DSMPerformancePredictor predictor)
    {
        _predictor = predictor;
    }
    
    [HttpGet("{id}")]
    public async Task<ActionResult> GetPersonById(int id)
    {
        // Predict execution time BEFORE calling method
        var prediction = _predictor.Predict(new PerformancePredictionInput
        {
            ClassName = nameof(PersonController),
            MethodName = nameof(GetPersonById),
            Depth = 0,
            HourOfDay = DateTime.Now.Hour,
            DayOfWeek = (int)DateTime.Now.DayOfWeek,
            IsWeekend = DateTime.Now.DayOfWeek >= DayOfWeek.Saturday,
            IsBusinessHours = DateTime.Now.Hour >= 9 && DateTime.Now.Hour <= 17,
            HistoricalAvg = 125.0,  // Get from analytics
            HistoricalStd = 45.0,
            HistoricalP95 = 200.0,
            SuccessRate = 98.5,
            MethodPopularity = 1500
        });
        
        Console.WriteLine($"⏱️ Predicted: {prediction.Output.PredictedExecutionTimeMs:F0}ms");
        
        // Decide on optimization strategy
        if (prediction.IsSlowPrediction)
        {
            // Use cache or alternative code path
            return await GetPersonByIdFromCache(id);
        }
        
        // Normal execution
        var envelope = DSMEnvelope<PersonDto>.InitWithCaller(nameof(PersonController));
        var result = await _personService.GetByIdAsync(id);
        envelope.Success(result);
        
        // Compare actual vs predicted
        var accuracy = Math.Abs(envelope.ExecutionTimeMs - prediction.Output.PredictedExecutionTimeMs) 
                      / envelope.ExecutionTimeMs * 100;
        Console.WriteLine($"📊 Actual: {envelope.ExecutionTimeMs}ms (Accuracy: {100 - accuracy:F1}%)");
        
        return Ok(envelope);
    }
}
```

### Advanced Usage with Historical Features

```csharp
public class SmartController : ControllerBase
{
    private readonly DSMPerformancePredictor _predictor;
    private readonly IDSMEnvelopeRepository _repository;
    
    [HttpGet("smart-predict/{methodName}")]
    public async Task<ActionResult> SmartPredict(string methodName)
    {
        // Get historical stats from repository
        var historical = await _repository.GetByMethodNameAsync(methodName, limit: 100);
        var stats = historical.Any() 
            ? new
            {
                Avg = historical.Average(e => e.ExecutionTimeMs),
                Std = historical.StdDev(e => e.ExecutionTimeMs),
                P95 = historical.Percentile(e => e.ExecutionTimeMs, 95),
                SuccessRate = historical.Count(e => e.IsSuccess) / (double)historical.Count * 100
            }
            : null;
        
        var prediction = _predictor.Predict(new PerformancePredictionInput
        {
            ClassName = nameof(SmartController),
            MethodName = methodName,
            Depth = 0,
            HourOfDay = DateTime.Now.Hour,
            DayOfWeek = (int)DateTime.Now.DayOfWeek,
            IsWeekend = DateTime.Now.DayOfWeek >= DayOfWeek.Saturday,
            IsBusinessHours = DateTime.Now.Hour >= 9 && DateTime.Now.Hour <= 17,
            HistoricalAvg = stats?.Avg ?? 150.0,
            HistoricalStd = stats?.Std ?? 50.0,
            HistoricalP95 = stats?.P95 ?? 250.0,
            SuccessRate = stats?.SuccessRate ?? 95.0,
            MethodPopularity = historical.Count
        });
        
        return Ok(new
        {
            prediction.Output.PredictedExecutionTimeMs,
            prediction.Output.ConfidenceLower,
            prediction.Output.ConfidenceUpper,
            prediction.RecommendedAction,
            prediction.InferenceTimeMs
        });
    }
}
```

## Model Retraining (Daily)

### Option 1: Windows Task Scheduler

Create `retrain.bat`:
```batch
cd C:\path\to\ML.Training.Python
python train_model.py
copy models\*.onnx ..\NVL9.DSM.ML.Prediction\models\
```

Schedule daily at 2 AM.

### Option 2: Linux Cron

```bash
0 2 * * * cd /path/to/ML.Training.Python && python train_model.py && cp models/*.onnx ../NVL9.DSM.ML.Prediction/models/
```

### Option 3: Azure Function (Recommended)

Create a timer-triggered Azure Function that:
1. Queries Cosmos DB for last 30 days of data
2. Trains model
3. Uploads ONNX to Azure Blob Storage
4. Prediction service downloads on startup

## Model Performance Metrics

### Expected Performance (Synthetic Data)
- **R² Score**: 0.85-0.95
- **MAE**: 20-50ms
- **RMSE**: 30-80ms
- **Inference Time**: <1ms

### Real Data
Initial performance may be lower (R² ~0.60-0.75), improving as more data is collected.

**Minimum data requirements:**
- 1,000 envelopes per method (basic)
- 10,000+ envelopes per method (good)
- 100,000+ total envelopes (excellent)

## Features Explained

| Feature | Description | Impact |
|---------|-------------|--------|
| `className` | Controller/class name | High - different classes have different patterns |
| `methodName` | Pure Function name | Very High - main predictor |
| `depth` | Call stack depth | Medium - deeper = usually slower |
| `hourOfDay` | Hour (0-23) | Medium - business hours slower |
| `dayOfWeek` | Day (0-6) | Low - Monday/Friday patterns |
| `isWeekend` | Boolean | Low - different load patterns |
| `isBusinessHours` | 9 AM - 5 PM | Medium - peak load indicator |
| `historical_avg` | Average last 100 calls | Very High - strong baseline |
| `historical_std` | Std dev last 100 | Medium - variability indicator |
| `historical_p95` | 95th percentile | High - upper bound estimate |
| `success_rate` | Success % last 100 | Low - error correlation |
| `method_popularity` | Total call count | Low - popularity indicator |

## Troubleshooting

### "ONNX model not found"
- Copy `dsm_performance_rf.onnx` to `NVL9.DSM.ML.Prediction/models/`
- Ensure file is copied to output directory (check `.csproj`)

### "Label encoder not found"
- Run Python script to export encoders as JSON
- Copy to `models/` directory

### "Prediction always returns same value"
- Check feature scaling - numerical features should be normalized
- Verify encoders are loaded correctly

### "Low accuracy (high MAE)"
- Train on more data (10,000+ samples recommended)
- Check for data quality issues
- Verify feature engineering is correct

## Integration with DSMEnvelope Workflow

```csharp
// 1. Predict before execution
var predicted = _predictor.Predict(input);

// 2. Execute with DSMEnvelope
var envelope = DSMEnvelope<T>.InitWithCaller(className);
var result = await ExecuteMethod();
await envelope.SuccessAndPersistAsync(result);

// 3. Compare and log
var accuracy = CalculateAccuracy(predicted, envelope.ExecutionTimeMs);
await LogPredictionAccuracy(predicted, envelope.ExecutionTimeMs, accuracy);

// 4. Use for optimization decisions
if (predicted.Output.PredictedExecutionTimeMs > threshold)
{
    // Use cache, queue for later, or use alternative path
}
```

## Architecture Diagram

```
┌─────────────────────┐
│  Cosmos DB          │
│  (Historical Data)  │
└──────┬──────────────┘
       │
       │ Query last 30 days
       ▼
┌─────────────────────┐
│  Python Training    │
│  - Load data        │
│  - Feature eng      │
│  - Train models     │
│  - Export ONNX      │
└──────┬──────────────┘
       │
       │ ONNX file
       ▼
┌─────────────────────┐
│  .NET Prediction    │
│  - Load ONNX        │
│  - Real-time pred   │
│  - <1ms inference   │
└──────┬──────────────┘
       │
       │ Predictions
       ▼
┌─────────────────────┐
│  Controllers        │
│  - Pre-execution    │
│  - Optimization     │
│  - Validation       │
└─────────────────────┘
```

## Next Steps

1. ✅ Generate synthetic data
2. ✅ Train models
3. ✅ Test .NET prediction
4. ⬜ Integrate with real Cosmos DB data
5. ⬜ Add to controllers
6. ⬜ Set up daily retraining
7. ⬜ Build monitoring dashboard
8. ⬜ Optimize based on accuracy metrics
