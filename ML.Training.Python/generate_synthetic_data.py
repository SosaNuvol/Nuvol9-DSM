"""
Synthetic Data Generator for DSMEnvelope Performance Prediction

Generates realistic DSMEnvelope execution data for training ML models.
Simulates various scenarios: normal execution, bottlenecks, errors, time-of-day patterns.
"""

import numpy as np
import pandas as pd
from datetime import datetime, timedelta
from typing import List, Dict
import random
import uuid


class DSMSyntheticDataGenerator:
    """Generates synthetic DSMEnvelope data with realistic patterns"""
    
    # Common method names in a typical application
    CONTROLLERS = [
        "PersonController", "OrderController", "ProductController",
        "UserController", "PaymentController", "AuthController",
        "ReportController", "NotificationController"
    ]
    
    METHODS = {
        "PersonController": ["GetById", "GetAll", "Create", "Update", "Delete", "Search"],
        "OrderController": ["GetById", "GetAll", "CreateOrder", "UpdateStatus", "Cancel", "GetByCustomer"],
        "ProductController": ["GetById", "GetAll", "Create", "Update", "Delete", "Search", "GetByCategory"],
        "UserController": ["GetById", "GetAll", "Register", "Login", "UpdateProfile", "Delete"],
        "PaymentController": ["ProcessPayment", "RefundPayment", "GetTransactionHistory", "ValidateCard"],
        "AuthController": ["Login", "Logout", "RefreshToken", "ValidateToken", "ChangePassword"],
        "ReportController": ["GenerateSalesReport", "GenerateUserReport", "ExportData"],
        "NotificationController": ["SendEmail", "SendSMS", "GetNotifications", "MarkAsRead"]
    }
    
    # Base execution times (in milliseconds) for different method types
    BASE_TIMES = {
        "GetById": (50, 150),        # Fast database query
        "GetAll": (100, 300),         # Slower, pagination
        "Create": (80, 200),
        "Update": (70, 180),
        "Delete": (60, 150),
        "Search": (150, 400),         # Complex query
        "ProcessPayment": (500, 1500), # External API call
        "Login": (100, 250),
        "GenerateSalesReport": (1000, 3000), # Heavy computation
        "SendEmail": (200, 600),      # External service
        "Default": (50, 200)
    }
    
    def __init__(self, seed: int = 42):
        """Initialize generator with random seed for reproducibility"""
        np.random.seed(seed)
        random.seed(seed)
    
    def generate_dataset(
        self, 
        num_samples: int = 10000,
        start_date: datetime = None,
        end_date: datetime = None
    ) -> pd.DataFrame:
        """
        Generate synthetic DSMEnvelope dataset
        
        Args:
            num_samples: Number of envelope samples to generate
            start_date: Start date for timestamp generation
            end_date: End date for timestamp generation
            
        Returns:
            DataFrame with synthetic DSMEnvelope data
        """
        if start_date is None:
            start_date = datetime.now() - timedelta(days=30)
        if end_date is None:
            end_date = datetime.now()
        
        data = []
        
        for i in range(num_samples):
            record = self._generate_single_record(start_date, end_date)
            data.append(record)
        
        df = pd.DataFrame(data)
        
        # Add derived features
        df = self._add_derived_features(df)
        
        return df
    
    def _generate_single_record(self, start_date: datetime, end_date: datetime) -> Dict:
        """Generate a single DSMEnvelope record"""
        
        # Random timestamp
        time_diff = end_date - start_date
        random_seconds = random.randint(0, int(time_diff.total_seconds()))
        timestamp = start_date + timedelta(seconds=random_seconds)
        
        # Select controller and method
        controller = random.choice(self.CONTROLLERS)
        method = random.choice(self.METHODS[controller])
        
        # Base execution time
        base_min, base_max = self.BASE_TIMES.get(method, self.BASE_TIMES["Default"])
        base_time = np.random.uniform(base_min, base_max)
        
        # Apply patterns and variations
        execution_time = self._apply_patterns(base_time, timestamp, controller, method)
        
        # Determine success/error
        is_error = self._determine_error(method, execution_time)
        code = self._determine_code(is_error)
        
        # Graph depth (0 = controller, 1 = service, 2 = database)
        depth = random.choices([0, 1, 2], weights=[0.3, 0.5, 0.2])[0]
        
        # Generate IDs
        unique_id = str(uuid.uuid4())
        root_id = f"App|Host|{timestamp.strftime('%Y:%m:%d:%H:%M:%S')}|{random.randint(1000, 9999)}"
        
        return {
            "uniqueIEID": unique_id,
            "rootEnvelopID": root_id,
            "className": controller,
            "methodName": method,
            "executionTimeMs": int(execution_time),
            "startTime": timestamp,
            "code": code,
            "isError": is_error,
            "isSuccess": not is_error and code == "GEN_COMMON_00000",
            "depth": depth,
            "hourOfDay": timestamp.hour,
            "dayOfWeek": timestamp.weekday(),
            "isWeekend": timestamp.weekday() >= 5,
            "isBusinessHours": 9 <= timestamp.hour <= 17
        }
    
    def _apply_patterns(
        self, 
        base_time: float, 
        timestamp: datetime, 
        controller: str, 
        method: str
    ) -> float:
        """Apply realistic patterns to execution time"""
        
        time = base_time
        
        # Time of day pattern (slower during business hours due to load)
        hour = timestamp.hour
        if 9 <= hour <= 17:  # Business hours
            time *= np.random.uniform(1.2, 1.8)
        elif 0 <= hour <= 6:  # Night (faster, less load)
            time *= np.random.uniform(0.7, 0.9)
        
        # Day of week pattern (slower on Monday/Friday)
        if timestamp.weekday() in [0, 4]:  # Monday or Friday
            time *= np.random.uniform(1.1, 1.3)
        
        # Random spikes (simulating database slow queries, network issues)
        if np.random.random() < 0.05:  # 5% chance of spike
            time *= np.random.uniform(2.0, 5.0)
        
        # Method-specific patterns
        if "Report" in method:
            # Reports are slower with more data
            time *= np.random.uniform(1.5, 3.0)
        
        if "Payment" in method:
            # External API variability
            time *= np.random.uniform(0.8, 2.5)
        
        # Add small random noise
        time *= np.random.uniform(0.95, 1.05)
        
        return max(10, time)  # Minimum 10ms
    
    def _determine_error(self, method: str, execution_time: float) -> bool:
        """Determine if this execution results in error"""
        
        # Base error rate
        error_rate = 0.02  # 2% base error rate
        
        # Higher error rate for slow executions
        if execution_time > 2000:
            error_rate = 0.15  # 15% for very slow operations
        elif execution_time > 1000:
            error_rate = 0.08  # 8% for slow operations
        
        # Method-specific error rates
        if "Payment" in method:
            error_rate = 0.05  # 5% for payment operations
        elif "External" in method or "API" in method:
            error_rate = 0.07  # 7% for external calls
        
        return np.random.random() < error_rate
    
    def _determine_code(self, is_error: bool) -> str:
        """Determine status code"""
        if not is_error:
            return "GEN_COMMON_00000"  # Success
        
        # Various error codes
        error_codes = [
            "API_APPVLD_02001",  # Validation error
            "API_APPVLD_02010",  # Application error
            "API_DATABASE_03020", # Database error
            "API_DATABASE_03021", # Timeout
            "API_COMMON_01001"    # Authorization error
        ]
        return random.choice(error_codes)
    
    def _add_derived_features(self, df: pd.DataFrame) -> pd.DataFrame:
        """Add derived features for ML training"""
        
        # Historical aggregations per method
        df = df.sort_values('startTime')
        
        # Rolling statistics (last 100 calls)
        df['historical_avg'] = df.groupby('methodName')['executionTimeMs'].transform(
            lambda x: x.rolling(window=100, min_periods=1).mean()
        )
        
        df['historical_std'] = df.groupby('methodName')['executionTimeMs'].transform(
            lambda x: x.rolling(window=100, min_periods=1).std().fillna(0)
        )
        
        df['historical_p95'] = df.groupby('methodName')['executionTimeMs'].transform(
            lambda x: x.rolling(window=100, min_periods=1).quantile(0.95)
        )
        
        # Success rate (last 100 calls)
        df['success_rate'] = df.groupby('methodName')['isSuccess'].transform(
            lambda x: x.rolling(window=100, min_periods=1).mean() * 100
        )
        
        # Recent call frequency (last 5 minutes)
        df['recent_call_count'] = 0  # Simplified for synthetic data
        
        # Method popularity (total calls)
        method_counts = df['methodName'].value_counts().to_dict()
        df['method_popularity'] = df['methodName'].map(method_counts)
        
        return df
    
    def save_to_csv(self, df: pd.DataFrame, filepath: str):
        """Save dataset to CSV file"""
        df.to_csv(filepath, index=False)
        print(f"✅ Saved {len(df)} records to {filepath}")


if __name__ == "__main__":
    # Generate training dataset
    generator = DSMSyntheticDataGenerator(seed=42)
    
    # Generate 10,000 samples over 30 days
    print("Generating synthetic DSMEnvelope data...")
    df = generator.generate_dataset(
        num_samples=10000,
        start_date=datetime.now() - timedelta(days=30),
        end_date=datetime.now()
    )
    
    # Display statistics
    print("\n📊 Dataset Statistics:")
    print(f"Total records: {len(df)}")
    print(f"Date range: {df['startTime'].min()} to {df['startTime'].max()}")
    print(f"\nExecution time statistics:")
    print(df['executionTimeMs'].describe())
    print(f"\nSuccess rate: {df['isSuccess'].mean() * 100:.2f}%")
    print(f"Error rate: {df['isError'].mean() * 100:.2f}%")
    print(f"\nMethods distribution:")
    print(df['methodName'].value_counts().head(10))
    
    # Save to file
    generator.save_to_csv(df, "dsm_synthetic_data.csv")
    
    print("\n✅ Synthetic data generation complete!")
