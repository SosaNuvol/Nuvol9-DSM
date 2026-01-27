"""
DSMEnvelope Performance Prediction API

Flask REST API for serving ML predictions to .NET applications.
Loads trained pickle models and provides HTTP endpoint for predictions.
"""

from flask import Flask, request, jsonify
import joblib
import numpy as np
import json
import os
from typing import Dict, List

app = Flask(__name__)

# Global model and artifacts
model = None
label_encoders = None
scaler = None
feature_names = None


def load_model_artifacts(model_path: str = "models/dsm_performance_rf.pkl"):
    """Load model and preprocessing artifacts"""
    global model, label_encoders, scaler, feature_names
    
    print(f"Loading model from {model_path}...")
    model = joblib.load(model_path)
    
    print("Loading label encoders...")
    label_encoders = joblib.load("models/label_encoders.joblib")
    
    print("Loading scaler...")
    scaler = joblib.load("models/scaler.joblib")
    
    print("Loading feature names...")
    with open("models/feature_names.json", 'r') as f:
        feature_names = json.load(f)
    
    print(f"✅ Model loaded successfully! Features: {len(feature_names)}")


def prepare_features(input_data: Dict) -> np.ndarray:
    """
    Convert input JSON to feature vector
    
    Expected input format:
    {
        "className": "PersonController",
        "methodName": "GetById",
        "depth": 0,
        "hourOfDay": 14,
        "dayOfWeek": 2,
        "isWeekend": false,
        "isBusinessHours": true,
        "historicalAvg": 125.0,
        "historicalStd": 45.0,
        "historicalP95": 200.0,
        "successRate": 98.5,
        "methodPopularity": 1500
    }
    """
    # Extract values in feature order
    features = []
    
    # Categorical features (need encoding)
    categorical_features = ['className', 'methodName']
    for feat in categorical_features:
        value = input_data.get(feat, 'Unknown')
        encoder = label_encoders[feat]
        
        # Handle unseen categories
        if value not in encoder.classes_:
            # Use most common class (index 0)
            encoded = 0
        else:
            encoded = encoder.transform([value])[0]
        
        features.append(encoded)
    
    # Numerical features (need scaling)
    numerical_features = [
        'depth', 'hourOfDay', 'dayOfWeek', 'isWeekend', 'isBusinessHours',
        'historicalAvg', 'historicalStd', 'historicalP95',
        'successRate', 'methodPopularity'
    ]
    
    numerical_values = []
    for feat in numerical_features:
        value = input_data.get(feat, 0)
        # Convert boolean to int
        if isinstance(value, bool):
            value = 1 if value else 0
        numerical_values.append(value)
    
    # Scale numerical features
    numerical_values = np.array(numerical_values).reshape(1, -1)
    scaled_values = scaler.transform(numerical_values)[0]
    
    features.extend(scaled_values)
    
    return np.array(features).reshape(1, -1)


@app.route('/health', methods=['GET'])
def health_check():
    """Health check endpoint"""
    return jsonify({
        "status": "healthy",
        "model_loaded": model is not None,
        "features": len(feature_names) if feature_names else 0
    })


@app.route('/predict', methods=['POST'])
def predict():
    """
    Prediction endpoint
    
    POST /predict
    {
        "className": "PersonController",
        "methodName": "GetById",
        "depth": 0,
        ...
    }
    
    Returns:
    {
        "predictedExecutionTimeMs": 125.5,
        "confidence": "high",
        "isSlowPrediction": false
    }
    """
    try:
        # Get input data
        input_data = request.get_json()
        
        if not input_data:
            return jsonify({"error": "No input data provided"}), 400
        
        # Prepare features
        features = prepare_features(input_data)
        
        # Make prediction
        prediction = model.predict(features)[0]
        
        # Determine confidence based on input data quality
        has_history = (
            input_data.get('historicalAvg', 0) > 0 and
            input_data.get('methodPopularity', 0) > 0
        )
        confidence = "high" if has_history else "low"
        
        # Classify as slow if > 1000ms
        is_slow = prediction > 1000
        
        return jsonify({
            "predictedExecutionTimeMs": round(prediction, 2),
            "confidence": confidence,
            "isSlowPrediction": is_slow,
            "model": "RandomForest",
            "features_used": len(feature_names)
        })
    
    except Exception as e:
        return jsonify({
            "error": str(e),
            "type": type(e).__name__
        }), 500


@app.route('/predict/batch', methods=['POST'])
def predict_batch():
    """
    Batch prediction endpoint
    
    POST /predict/batch
    {
        "inputs": [
            {...input1...},
            {...input2...}
        ]
    }
    
    Returns:
    {
        "predictions": [125.5, 350.2, ...]
    }
    """
    try:
        data = request.get_json()
        inputs = data.get('inputs', [])
        
        if not inputs:
            return jsonify({"error": "No inputs provided"}), 400
        
        predictions = []
        for input_data in inputs:
            features = prepare_features(input_data)
            prediction = model.predict(features)[0]
            predictions.append(round(prediction, 2))
        
        return jsonify({
            "predictions": predictions,
            "count": len(predictions)
        })
    
    except Exception as e:
        return jsonify({
            "error": str(e),
            "type": type(e).__name__
        }), 500


@app.route('/model/info', methods=['GET'])
def model_info():
    """Get model information"""
    try:
        with open("models/metrics.json", 'r') as f:
            metrics = json.load(f)
        
        return jsonify({
            "model_type": "RandomForest",
            "features": feature_names,
            "feature_count": len(feature_names),
            "metrics": metrics.get("RandomForest", {}),
            "categorical_classes": {
                k: list(v.classes_) for k, v in label_encoders.items()
            }
        })
    
    except Exception as e:
        return jsonify({
            "error": str(e)
        }), 500


if __name__ == '__main__':
    # Load model on startup
    model_path = os.getenv('MODEL_PATH', 'models/dsm_performance_rf.pkl')
    load_model_artifacts(model_path)
    
    # Start server
    port = int(os.getenv('PORT', 5000))
    app.run(host='0.0.0.0', port=port, debug=False)
