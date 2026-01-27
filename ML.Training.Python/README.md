# DSMEnvelope Performance Prediction - Python Training

This directory contains Python scripts for training ML models to predict DSMEnvelope execution times.

## Setup

### 1. Install Python Dependencies

```bash
cd ML.Training.Python
pip install -r requirements.txt
```

### 2. Generate Synthetic Data

```bash
python generate_synthetic_data.py
```

This creates `dsm_synthetic_data.csv` with 10,000 synthetic DSMEnvelope records.

### 3. Train Models

```bash
python train_model.py
```

This will:
- Load the synthetic dataset
- Train Random Forest and XGBoost models
- Evaluate both models
- Export to ONNX format
- Save all artifacts to `models/` directory

## Output Files

After training, you'll have:

```
models/
├── dsm_performance_rf.onnx          # Random Forest ONNX model
├── dsm_performance_xgb.onnx         # XGBoost ONNX model
├── label_encoders.joblib            # Categorical encoders
├── scaler.joblib                    # Feature scaler
├── feature_names.json               # Feature list
├── metrics.json                     # Model performance metrics
├── sklearn_model.joblib             # Original sklearn model
├── random_forest_results.png        # RF visualization
└── xgboost_results.png              # XGBoost visualization
```

## Model Features

The models use these features to predict execution time:

**Categorical:**
- `className`: Controller/class name
- `methodName`: Pure Function name

**Numerical:**
- `depth`: Call stack depth (0=controller, 1=service, 2=database)
- `hourOfDay`: Hour (0-23)
- `dayOfWeek`: Day (0=Monday, 6=Sunday)
- `isWeekend`: Boolean
- `isBusinessHours`: Boolean (9AM-5PM)
- `historical_avg`: Average execution time (last 100 calls)
- `historical_std`: Standard deviation (last 100 calls)
- `historical_p95`: 95th percentile (last 100 calls)
- `success_rate`: Success rate % (last 100 calls)
- `method_popularity`: Total call count

## Expected Performance

With synthetic data:
- **R² Score**: 0.80-0.95 (80-95% variance explained)
- **MAE**: 20-50ms (Mean Absolute Error)
- **RMSE**: 30-80ms (Root Mean Squared Error)

With real data, expect slightly lower R² initially, improving as more data is collected.

## Using with Real Data

To train on real Cosmos DB data, modify `train_model.py`:

```python
# Instead of loading CSV
from azure.cosmos import CosmosClient

client = CosmosClient(url, credential)
database = client.get_database_client("DSMEnvelopeDB")
container = database.get_container_client("Envelopes")

# Query data
query = "SELECT * FROM c WHERE c.createdAt >= '2026-01-01'"
items = list(container.query_items(query, enable_cross_partition_query=True))
df = pd.DataFrame(items)
```

## Daily Retraining

Set up a scheduled task (cron/Task Scheduler) to run:

```bash
# Daily at 2 AM
0 2 * * * cd /path/to/ML.Training.Python && python train_model.py
```

Or use Azure Functions, AWS Lambda, or similar for cloud scheduling.

## Next Steps

1. ✅ Generate synthetic data
2. ✅ Train models
3. ✅ Export to ONNX
4. ➡️ Integrate ONNX model in .NET prediction service
5. ➡️ Deploy prediction service
