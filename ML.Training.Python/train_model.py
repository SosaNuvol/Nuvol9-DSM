"""
DSMEnvelope Performance Prediction Model Trainer

Trains ML models to predict execution time of Pure Functions based on historical data.
Supports multiple algorithms: Random Forest, XGBoost, and Neural Network.
Exports trained model to ONNX format for .NET integration.
"""

import numpy as np
import pandas as pd
from sklearn.model_selection import train_test_split
from sklearn.preprocessing import LabelEncoder, StandardScaler
from sklearn.ensemble import RandomForestRegressor
from sklearn.metrics import mean_absolute_error, mean_squared_error, r2_score
import xgboost as xgb
import joblib
from skl2onnx import convert_sklearn
from skl2onnx.common.data_types import FloatTensorType, Int64TensorType
import matplotlib.pyplot as plt
import seaborn as sns
from datetime import datetime
import json
import os


class DSMPerformancePredictionTrainer:
    """Train and export performance prediction models"""
    
    def __init__(self, model_output_dir: str = "models"):
        """
        Initialize trainer
        
        Args:
            model_output_dir: Directory to save trained models
        """
        self.model_output_dir = model_output_dir
        os.makedirs(model_output_dir, exist_ok=True)
        
        self.label_encoders = {}
        self.scaler = None
        self.feature_names = None
        self.model = None
        self.model_type = None
        
    def prepare_features(self, df: pd.DataFrame) -> tuple:
        """
        Prepare features for training
        
        Args:
            df: Raw dataframe with DSMEnvelope data
            
        Returns:
            X, y: Features and target variable
        """
        print("📊 Preparing features...")
        
        # Select features
        categorical_features = ['className', 'methodName']
        numerical_features = [
            'depth', 'hourOfDay', 'dayOfWeek', 'isWeekend', 'isBusinessHours',
            'historical_avg', 'historical_std', 'historical_p95',
            'success_rate', 'method_popularity'
        ]
        
        # Encode categorical features
        df_encoded = df.copy()
        for col in categorical_features:
            if col not in self.label_encoders:
                self.label_encoders[col] = LabelEncoder()
                df_encoded[col] = self.label_encoders[col].fit_transform(df[col])
            else:
                df_encoded[col] = self.label_encoders[col].transform(df[col])
        
        # Combine features
        self.feature_names = categorical_features + numerical_features
        X = df_encoded[self.feature_names]
        
        # Target variable
        y = df['executionTimeMs'].values
        
        # Scale numerical features
        if self.scaler is None:
            self.scaler = StandardScaler()
            X[numerical_features] = self.scaler.fit_transform(X[numerical_features])
        else:
            X[numerical_features] = self.scaler.transform(X[numerical_features])
        
        print(f"✅ Features prepared: {X.shape[1]} features, {X.shape[0]} samples")
        print(f"   Categorical: {categorical_features}")
        print(f"   Numerical: {numerical_features}")
        
        return X.values, y
    
    def train_random_forest(
        self, 
        X_train: np.ndarray, 
        y_train: np.ndarray,
        n_estimators: int = 100,
        max_depth: int = 20
    ):
        """Train Random Forest model"""
        print(f"\n🌲 Training Random Forest (n_estimators={n_estimators}, max_depth={max_depth})...")
        
        self.model = RandomForestRegressor(
            n_estimators=n_estimators,
            max_depth=max_depth,
            random_state=42,
            n_jobs=-1,
            verbose=0
        )
        
        self.model.fit(X_train, y_train)
        self.model_type = "RandomForest"
        
        print("✅ Random Forest training complete")
    
    def train_xgboost(
        self,
        X_train: np.ndarray,
        y_train: np.ndarray,
        n_estimators: int = 100,
        max_depth: int = 6,
        learning_rate: float = 0.1
    ):
        """Train XGBoost model"""
        print(f"\n🚀 Training XGBoost (n_estimators={n_estimators}, max_depth={max_depth}, lr={learning_rate})...")
        
        self.model = xgb.XGBRegressor(
            n_estimators=n_estimators,
            max_depth=max_depth,
            learning_rate=learning_rate,
            random_state=42,
            n_jobs=-1,
            verbosity=0
        )
        
        self.model.fit(X_train, y_train)
        self.model_type = "XGBoost"
        
        print("✅ XGBoost training complete")
    
    def evaluate(self, X_test: np.ndarray, y_test: np.ndarray) -> dict:
        """Evaluate model performance"""
        print("\n📈 Evaluating model...")
        
        y_pred = self.model.predict(X_test)
        
        mae = mean_absolute_error(y_test, y_pred)
        rmse = np.sqrt(mean_squared_error(y_test, y_pred))
        r2 = r2_score(y_test, y_pred)
        
        # Calculate percentage errors
        mape = np.mean(np.abs((y_test - y_pred) / y_test)) * 100
        
        # Percentile accuracies
        errors = np.abs(y_test - y_pred)
        p50_error = np.percentile(errors, 50)
        p95_error = np.percentile(errors, 95)
        p99_error = np.percentile(errors, 99)
        
        metrics = {
            "mae": float(mae),
            "rmse": float(rmse),
            "r2": float(r2),
            "mape": float(mape),
            "p50_error": float(p50_error),
            "p95_error": float(p95_error),
            "p99_error": float(p99_error),
            "model_type": self.model_type,
            "timestamp": datetime.now().isoformat()
        }
        
        print(f"\n🎯 Model Performance:")
        print(f"   MAE (Mean Absolute Error): {mae:.2f} ms")
        print(f"   RMSE (Root Mean Squared Error): {rmse:.2f} ms")
        print(f"   R² Score: {r2:.4f}")
        print(f"   MAPE (Mean Abs % Error): {mape:.2f}%")
        print(f"   P50 Error: {p50_error:.2f} ms")
        print(f"   P95 Error: {p95_error:.2f} ms")
        print(f"   P99 Error: {p99_error:.2f} ms")
        
        return metrics
    
    def plot_results(self, X_test: np.ndarray, y_test: np.ndarray, output_path: str = None):
        """Plot prediction results"""
        print("\n📊 Generating plots...")
        
        y_pred = self.model.predict(X_test)
        
        fig, axes = plt.subplots(2, 2, figsize=(15, 12))
        
        # 1. Actual vs Predicted
        axes[0, 0].scatter(y_test, y_pred, alpha=0.5, s=10)
        axes[0, 0].plot([y_test.min(), y_test.max()], [y_test.min(), y_test.max()], 'r--', lw=2)
        axes[0, 0].set_xlabel('Actual Execution Time (ms)')
        axes[0, 0].set_ylabel('Predicted Execution Time (ms)')
        axes[0, 0].set_title('Actual vs Predicted')
        axes[0, 0].grid(True, alpha=0.3)
        
        # 2. Residual plot
        residuals = y_test - y_pred
        axes[0, 1].scatter(y_pred, residuals, alpha=0.5, s=10)
        axes[0, 1].axhline(y=0, color='r', linestyle='--', lw=2)
        axes[0, 1].set_xlabel('Predicted Execution Time (ms)')
        axes[0, 1].set_ylabel('Residuals (ms)')
        axes[0, 1].set_title('Residual Plot')
        axes[0, 1].grid(True, alpha=0.3)
        
        # 3. Error distribution
        errors = np.abs(residuals)
        axes[1, 0].hist(errors, bins=50, edgecolor='black', alpha=0.7)
        axes[1, 0].set_xlabel('Absolute Error (ms)')
        axes[1, 0].set_ylabel('Frequency')
        axes[1, 0].set_title('Error Distribution')
        axes[1, 0].axvline(x=np.mean(errors), color='r', linestyle='--', label=f'Mean: {np.mean(errors):.2f}ms')
        axes[1, 0].legend()
        axes[1, 0].grid(True, alpha=0.3)
        
        # 4. Feature importance (if available)
        if hasattr(self.model, 'feature_importances_'):
            importances = self.model.feature_importances_
            indices = np.argsort(importances)[::-1][:10]  # Top 10
            
            axes[1, 1].barh(range(len(indices)), importances[indices])
            axes[1, 1].set_yticks(range(len(indices)))
            axes[1, 1].set_yticklabels([self.feature_names[i] for i in indices])
            axes[1, 1].set_xlabel('Importance')
            axes[1, 1].set_title('Top 10 Feature Importances')
            axes[1, 1].grid(True, alpha=0.3)
        
        plt.tight_layout()
        
        if output_path:
            plt.savefig(output_path, dpi=300, bbox_inches='tight')
            print(f"✅ Plots saved to {output_path}")
        else:
            plt.show()
        
        plt.close()
    
    def export_to_onnx(self, model_name: str = "dsm_performance_model.onnx"):
        """Export model to ONNX format"""
        print(f"\n📦 Exporting model to ONNX format...")
        
        output_path = os.path.join(self.model_output_dir, model_name)
        
        # Define input types (must match feature count and types)
        n_features = len(self.feature_names)
        initial_type = [('float_input', FloatTensorType([None, n_features]))]
        
        # Convert to ONNX
        onnx_model = convert_sklearn(
            self.model,
            initial_types=initial_type,
            target_opset=12
        )
        
        # Save ONNX model
        with open(output_path, "wb") as f:
            f.write(onnx_model.SerializeToString())
        
        print(f"✅ ONNX model saved to {output_path}")
        
        return output_path
    
    def save_artifacts(self, metrics: dict):
        """Save all training artifacts"""
        print("\n💾 Saving training artifacts...")
        
        # Save label encoders
        encoders_path = os.path.join(self.model_output_dir, "label_encoders.joblib")
        joblib.dump(self.label_encoders, encoders_path)
        
        # Save scaler
        scaler_path = os.path.join(self.model_output_dir, "scaler.joblib")
        joblib.dump(self.scaler, scaler_path)
        
        # Save feature names
        features_path = os.path.join(self.model_output_dir, "feature_names.json")
        with open(features_path, 'w') as f:
            json.dump(self.feature_names, f, indent=2)
        
        # Save metrics
        metrics_path = os.path.join(self.model_output_dir, "metrics.json")
        with open(metrics_path, 'w') as f:
            json.dump(metrics, f, indent=2)
        
        # Save sklearn model
        model_path = os.path.join(self.model_output_dir, "sklearn_model.joblib")
        joblib.dump(self.model, model_path)
        
        print(f"✅ Artifacts saved to {self.model_output_dir}/")


def main():
    """Main training pipeline"""
    print("=" * 80)
    print("DSMEnvelope Performance Prediction Model Training")
    print("=" * 80)
    
    # Load data
    print("\n📂 Loading dataset...")
    df = pd.read_csv("dsm_synthetic_data.csv")
    print(f"✅ Loaded {len(df)} records")
    
    # Initialize trainer
    trainer = DSMPerformancePredictionTrainer(model_output_dir="models")
    
    # Prepare features
    X, y = trainer.prepare_features(df)
    
    # Train/test split
    print("\n✂️ Splitting data (80% train, 20% test)...")
    X_train, X_test, y_train, y_test = train_test_split(
        X, y, test_size=0.2, random_state=42
    )
    print(f"   Training set: {X_train.shape[0]} samples")
    print(f"   Test set: {X_test.shape[0]} samples")
    
    # Train model (try both and compare)
    print("\n" + "=" * 80)
    print("Training Models")
    print("=" * 80)
    
    # Train Random Forest
    trainer.train_random_forest(X_train, y_train, n_estimators=100, max_depth=20)
    rf_metrics = trainer.evaluate(X_test, y_test)
    trainer.plot_results(X_test, y_test, "models/random_forest_results.png")
    trainer.export_to_onnx("dsm_performance_rf.onnx")
    trainer.save_artifacts(rf_metrics)
    
    # Train XGBoost
    print("\n" + "-" * 80)
    trainer.train_xgboost(X_train, y_train, n_estimators=100, max_depth=6, learning_rate=0.1)
    xgb_metrics = trainer.evaluate(X_test, y_test)
    trainer.plot_results(X_test, y_test, "models/xgboost_results.png")
    trainer.export_to_onnx("dsm_performance_xgb.onnx")
    
    # Compare models
    print("\n" + "=" * 80)
    print("Model Comparison")
    print("=" * 80)
    print(f"\nRandom Forest:")
    print(f"   R² Score: {rf_metrics['r2']:.4f}")
    print(f"   MAE: {rf_metrics['mae']:.2f} ms")
    print(f"   RMSE: {rf_metrics['rmse']:.2f} ms")
    
    print(f"\nXGBoost:")
    print(f"   R² Score: {xgb_metrics['r2']:.4f}")
    print(f"   MAE: {xgb_metrics['mae']:.2f} ms")
    print(f"   RMSE: {xgb_metrics['rmse']:.2f} ms")
    
    # Recommend best model
    best_model = "Random Forest" if rf_metrics['r2'] > xgb_metrics['r2'] else "XGBoost"
    print(f"\n🏆 Best Model: {best_model}")
    
    print("\n" + "=" * 80)
    print("✅ Training Complete!")
    print("=" * 80)
    print(f"\nOutput files:")
    print(f"   📦 ONNX Models: models/dsm_performance_rf.onnx, models/dsm_performance_xgb.onnx")
    print(f"   📊 Plots: models/random_forest_results.png, models/xgboost_results.png")
    print(f"   💾 Artifacts: models/label_encoders.joblib, models/scaler.joblib")
    print(f"   📈 Metrics: models/metrics.json")


if __name__ == "__main__":
    main()
