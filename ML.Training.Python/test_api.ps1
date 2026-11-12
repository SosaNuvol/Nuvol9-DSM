# Test Prediction API

Write-Host "Testing DSMEnvelope Prediction API" -ForegroundColor Cyan
Write-Host "=" * 80 -ForegroundColor Cyan

# 1. Health Check
Write-Host "`n1. Health Check" -ForegroundColor Yellow
try {
    $health = Invoke-RestMethod -Uri "http://localhost:5000/health" -Method GET
    Write-Host "   Status: $($health.status)" -ForegroundColor Green
    Write-Host "   Model Loaded: $($health.model_loaded)" -ForegroundColor Green
    Write-Host "   Features: $($health.features)" -ForegroundColor Green
} catch {
    Write-Host "   Error: $_" -ForegroundColor Red
    Write-Host "`n   Make sure the API is running:" -ForegroundColor Yellow
    Write-Host "   python prediction_api.py" -ForegroundColor White
    exit 1
}

# 2. Single Prediction
Write-Host "`n2. Single Prediction Test" -ForegroundColor Yellow

$testInput = @{
    className = "PersonController"
    methodName = "GetById"
    depth = 0
    hourOfDay = 14
    dayOfWeek = 2
    isWeekend = $false
    isBusinessHours = $true
    historicalAvg = 125.0
    historicalStd = 45.0
    historicalP95 = 200.0
    successRate = 98.5
    methodPopularity = 1500
}

Write-Host "   Input: PersonController.GetById (2 PM, Wednesday)" -ForegroundColor White

try {
    $body = $testInput | ConvertTo-Json
    $prediction = Invoke-RestMethod -Uri "http://localhost:5000/predict" -Method POST -Body $body -ContentType "application/json"
    
    Write-Host "   Predicted Time: $($prediction.predictedExecutionTimeMs) ms" -ForegroundColor Green
    Write-Host "   Confidence: $($prediction.confidence)" -ForegroundColor Green
    Write-Host "   Is Slow: $($prediction.isSlowPrediction)" -ForegroundColor $(if ($prediction.isSlowPrediction) { "Red" } else { "Green" })
    Write-Host "   Model: $($prediction.model)" -ForegroundColor Green
} catch {
    Write-Host "   Error: $_" -ForegroundColor Red
}

# 3. Slow Operation Test
Write-Host "`n3. Slow Operation Test (Report Generation)" -ForegroundColor Yellow

$slowInput = @{
    className = "ReportController"
    methodName = "GenerateAnnualReport"
    depth = 0
    hourOfDay = 9
    dayOfWeek = 1
    isWeekend = $false
    isBusinessHours = $true
    historicalAvg = 5200.0
    historicalStd = 1200.0
    historicalP95 = 8000.0
    successRate = 95.0
    methodPopularity = 50
}

Write-Host "   Input: ReportController.GenerateAnnualReport" -ForegroundColor White

try {
    $body = $slowInput | ConvertTo-Json
    $prediction = Invoke-RestMethod -Uri "http://localhost:5000/predict" -Method POST -Body $body -ContentType "application/json"
    
    Write-Host "   Predicted Time: $($prediction.predictedExecutionTimeMs) ms" -ForegroundColor $(if ($prediction.isSlowPrediction) { "Red" } else { "Green" })
    Write-Host "   Confidence: $($prediction.confidence)" -ForegroundColor Green
    Write-Host "   Is Slow: $($prediction.isSlowPrediction)" -ForegroundColor $(if ($prediction.isSlowPrediction) { "Red" } else { "Green" })
} catch {
    Write-Host "   Error: $_" -ForegroundColor Red
}

# 4. Batch Prediction
Write-Host "`n4. Batch Prediction Test" -ForegroundColor Yellow

$batchInput = @{
    inputs = @(
        @{
            className = "PersonController"
            methodName = "GetById"
            depth = 0
            hourOfDay = 10
            dayOfWeek = 2
            isWeekend = $false
            isBusinessHours = $true
            historicalAvg = 125.0
            historicalStd = 45.0
            historicalP95 = 200.0
            successRate = 98.5
            methodPopularity = 1500
        },
        @{
            className = "OrderController"
            methodName = "Create"
            depth = 0
            hourOfDay = 14
            dayOfWeek = 3
            isWeekend = $false
            isBusinessHours = $true
            historicalAvg = 350.0
            historicalStd = 120.0
            historicalP95 = 600.0
            successRate = 97.0
            methodPopularity = 800
        },
        @{
            className = "ReportController"
            methodName = "GenerateAnnualReport"
            depth = 0
            hourOfDay = 2
            dayOfWeek = 1
            isWeekend = $false
            isBusinessHours = $false
            historicalAvg = 5200.0
            historicalStd = 1200.0
            historicalP95 = 8000.0
            successRate = 95.0
            methodPopularity = 50
        }
    )
}

Write-Host "   Testing 3 predictions..." -ForegroundColor White

try {
    $body = $batchInput | ConvertTo-Json -Depth 10
    $batchResult = Invoke-RestMethod -Uri "http://localhost:5000/predict/batch" -Method POST -Body $body -ContentType "application/json"
    
    Write-Host "   Predictions:" -ForegroundColor Green
    for ($i = 0; $i -lt $batchResult.predictions.Count; $i++) {
        $pred = $batchResult.predictions[$i]
        $color = if ($pred -gt 1000) { "Red" } else { "Green" }
        Write-Host "      [$($i+1)] $pred ms" -ForegroundColor $color
    }
    Write-Host "   Total: $($batchResult.count)" -ForegroundColor Green
} catch {
    Write-Host "   Error: $_" -ForegroundColor Red
}

# 5. Model Info
Write-Host "`n5. Model Information" -ForegroundColor Yellow

try {
    $info = Invoke-RestMethod -Uri "http://localhost:5000/model/info" -Method GET
    Write-Host "   Model Type: $($info.model_type)" -ForegroundColor Green
    Write-Host "   Features: $($info.feature_count)" -ForegroundColor Green
    Write-Host "   R² Score: $($info.metrics.r2_score)" -ForegroundColor Green
    Write-Host "   MAE: $($info.metrics.mae) ms" -ForegroundColor Green
} catch {
    Write-Host "   Error: $_" -ForegroundColor Red
}

Write-Host "`n" + "=" * 80 -ForegroundColor Cyan
Write-Host "✅ API Testing Complete!" -ForegroundColor Green
