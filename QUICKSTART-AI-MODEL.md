# 🚀 Quick Start - DSMEnvelope AI Model

Train a performance prediction model in **5 minutes**.

---

## Step 1: Train the Model (Python)

```bash
cd ML.Training.Python

# Install dependencies (first time only)
pip install -r requirements.txt

# Generate 10,000 synthetic training samples
python generate_synthetic_data.py

# Train Random Forest & XGBoost models, export to ONNX
python train_model.py
```

**Output**:
```
✅ Generated dsm_synthetic_data.csv (10,000 samples)
✅ Trained models (R² = 0.88-0.91)
✅ Exported ONNX files to models/
   - dsm_performance_rf.onnx
   - dsm_performance_xgb.onnx
   - className_encoder.json
   - methodName_encoder.json
   - feature_names.json
   - metrics.json
```

---

## Step 2: Copy Models to .NET Project

```bash
# Windows
copy ML.Training.Python\models\*.* NVL9.DSM.ML.Prediction\models\

# Linux/Mac
cp ML.Training.Python/models/* NVL9.DSM.ML.Prediction/models/
```

---

## Step 3: Test Predictions (.NET)

```bash
cd NVL9.DSM.ML.Prediction
dotnet run
```

**Output**:
```
================================================================================
DSMEnvelope Performance Prediction Service
================================================================================

✅ Model loaded successfully!

Demo Predictions:
──────────────────────────────────────────────────────────────────────────────
Controller: PersonController | Method: GetById
⏱️ Predicted: 125ms (Fast ✅)

Controller: OrderController | Method: ProcessPayment
⏱️ Predicted: 850ms (Moderate ⚠️)

Controller: ReportController | Method: GenerateAnnualReport
⏱️ Predicted: 5200ms (Slow ❌ - Consider optimization)
```

---

## Step 4: Integrate in Your Controllers

```csharp
// Startup.cs or Program.cs
services.AddSingleton<DSMPerformancePredictor>(sp =>
    new DSMPerformancePredictor("models/dsm_performance_rf.onnx"));

// PersonController.cs
public class PersonController : ControllerBase
{
    private readonly DSMPerformancePredictor _predictor;
    
    public PersonController(DSMPerformancePredictor predictor)
    {
        _predictor = predictor;
    }
    
    [HttpGet("{id}")]
    public async Task<ActionResult> GetById(int id)
    {
        // Predict execution time
        var prediction = _predictor.Predict(new PerformancePredictionInput
        {
            ClassName = "PersonController",
            MethodName = nameof(GetById),
            Depth = 0,
            HourOfDay = DateTime.Now.Hour,
            DayOfWeek = (int)DateTime.Now.DayOfWeek,
            IsWeekend = DateTime.Now.DayOfWeek >= DayOfWeek.Saturday,
            IsBusinessHours = DateTime.Now.Hour >= 9 && DateTime.Now.Hour <= 17,
            HistoricalAvg = 125.0,   // From Cosmos DB analytics
            HistoricalStd = 45.0,
            HistoricalP95 = 200.0,
            SuccessRate = 98.5,
            MethodPopularity = 1500
        });
        
        // Optimize based on prediction
        if (prediction.IsSlowPrediction)  // > 1000ms
        {
            return await GetFromCache(id);
        }
        
        // Execute normally
        var envelope = DSMEnvelope<PersonDto>.InitWithCaller(nameof(PersonController));
        var result = await _service.GetByIdAsync(id);
        await envelope.SuccessAndPersistAsync(result);
        
        return Ok(envelope);
    }
}
```

---

## ⏱️ What You Get

| Feature | Value |
|---------|-------|
| **Training Time** | ~30 seconds (10K samples) |
| **Model File Size** | ~500 KB |
| **Inference Time** | <1ms per prediction |
| **Accuracy (Synthetic)** | R² = 0.88-0.91 |
| **Accuracy (Real Data)** | R² = 0.75-0.95 (after 1-3 months) |

---

## 🎯 Use Cases

### 1. Cache Decisions
```csharp
if (predicted > 500ms)
    return await GetFromCache(id);
```

### 2. Load Balancing
```csharp
Route to server with lowest predicted load
```

### 3. SLA Monitoring
```csharp
if (predicted > SLA_threshold)
    AlertOpsTeam();
```

### 4. Auto-Scaling
```csharp
if (predicted > 1000ms && request_count > 100)
    TriggerAutoScale();
```

---

## 🔄 Daily Retraining

### Windows Task Scheduler

Create `retrain.bat`:
```batch
@echo off
cd C:\path\to\ML.Training.Python
python train_model.py
copy models\*.onnx ..\NVL9.DSM.ML.Prediction\models\
echo Model retrained at %date% %time% >> retrain.log
```

Schedule at **2 AM daily**.

### Linux Cron

```bash
0 2 * * * cd /path/to/ML.Training.Python && python train_model.py && cp models/*.onnx ../NVL9.DSM.ML.Prediction/models/
```

### Azure Function (Recommended)

```csharp
[FunctionName("RetrainModel")]
public static async Task Run(
    [TimerTrigger("0 0 2 * * *")] TimerInfo timer,  // 2 AM daily
    ILogger log)
{
    // 1. Query Cosmos DB for last 30 days
    var data = await cosmosClient.GetEnvelopesAsync(startDate, endDate);
    
    // 2. Export to CSV
    await File.WriteAllLinesAsync("training_data.csv", data.ToCsv());
    
    // 3. Run Python training script
    var process = Process.Start("python", "train_model.py");
    await process.WaitForExitAsync();
    
    // 4. Upload ONNX to Blob Storage
    await blobClient.UploadAsync("models/dsm_performance_rf.onnx");
    
    log.LogInformation($"Model retrained at {DateTime.UtcNow}");
}
```

---

## 📊 Monitor Accuracy

```csharp
public class PredictionAccuracyMonitor
{
    public void LogPrediction(double predicted, double actual)
    {
        var error = Math.Abs(predicted - actual);
        var percentError = (error / actual) * 100;
        
        _logger.LogInformation($"Predicted: {predicted}ms | Actual: {actual}ms | Error: {percentError:F1}%");
        
        // Alert if accuracy drops
        if (percentError > 50)
        {
            _logger.LogWarning("⚠️ High prediction error - consider retraining");
        }
    }
}
```

---

## 🆘 Troubleshooting

### "Model file not found"
```bash
# Ensure models are copied
ls NVL9.DSM.ML.Prediction/models/
# Should see: dsm_performance_rf.onnx
```

### "Label encoder not found"
```bash
# Check for encoder files
ls ML.Training.Python/models/*.json
# Should see: className_encoder.json, methodName_encoder.json
```

### Low accuracy (R² < 0.6)
- Need more training data (minimum 1,000 samples per method)
- Run for longer period (1-3 months)
- Check for data quality issues

### High prediction errors
- Retrain model with recent data
- Verify historical features are correct
- Check for outliers in training data

---

## 📚 Full Documentation

- **Complete Guide**: [ML-PREDICTION-GUIDE.md](docs/ML-PREDICTION-GUIDE.md)
- **AI Model Overview**: [README-AI-MODEL.md](README-AI-MODEL.md)
- **Training Instructions**: [ML.Training.Python/README.md](ML.Training.Python/README.md)

---

## ✅ Checklist

- [ ] Installed Python dependencies (`pip install -r requirements.txt`)
- [ ] Generated synthetic data (`python generate_synthetic_data.py`)
- [ ] Trained model (`python train_model.py`)
- [ ] Copied ONNX files to .NET project
- [ ] Tested predictions (`dotnet run` in ML.Prediction)
- [ ] Integrated in controllers
- [ ] Configured daily retraining
- [ ] Set up accuracy monitoring

---

**You're ready to predict Pure Function execution times!** 🎉

For questions, see [docs/ML-PREDICTION-GUIDE.md](docs/ML-PREDICTION-GUIDE.md).
