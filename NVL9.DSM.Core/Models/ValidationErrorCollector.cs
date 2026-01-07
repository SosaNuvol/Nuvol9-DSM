namespace NVL9.DSM.Core.Models;

/// <summary>
/// A helper class for collecting validation errors in a structured way.
/// </summary>
public class ValidationErrorCollector
{
    private readonly Dictionary<string, List<string>> _errors = new();

    /// <summary>
    /// Adds a validation error for a specific field.
    /// </summary>
    /// <param name="fieldName">The name of the field</param>
    /// <param name="errorMessage">The error message</param>
    /// <returns>This collector instance for method chaining</returns>
    public ValidationErrorCollector AddError(string fieldName, string errorMessage)
    {
        if (!_errors.ContainsKey(fieldName))
        {
            _errors[fieldName] = new List<string>();
        }
        
        _errors[fieldName].Add(errorMessage);
        return this;
    }

    /// <summary>
    /// Adds multiple validation errors for a specific field.
    /// </summary>
    /// <param name="fieldName">The name of the field</param>
    /// <param name="errorMessages">The error messages</param>
    /// <returns>This collector instance for method chaining</returns>
    public ValidationErrorCollector AddErrors(string fieldName, IEnumerable<string> errorMessages)
    {
        foreach (var errorMessage in errorMessages)
        {
            AddError(fieldName, errorMessage);
        }
        return this;
    }

    /// <summary>
    /// Adds an error if the condition is true.
    /// </summary>
    /// <param name="condition">The condition to check</param>
    /// <param name="fieldName">The field name</param>
    /// <param name="errorMessage">The error message</param>
    /// <returns>This collector instance for method chaining</returns>
    public ValidationErrorCollector AddErrorIf(bool condition, string fieldName, string errorMessage)
    {
        if (condition)
        {
            AddError(fieldName, errorMessage);
        }
        return this;
    }

    /// <summary>
    /// Validates that a string field is not null or empty.
    /// </summary>
    /// <param name="value">The value to check</param>
    /// <param name="fieldName">The field name</param>
    /// <param name="errorMessage">Optional custom error message</param>
    /// <returns>This collector instance for method chaining</returns>
    public ValidationErrorCollector ValidateRequired(string? value, string fieldName, string? errorMessage = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            AddError(fieldName, errorMessage ?? $"{fieldName} is required");
        }
        return this;
    }

    /// <summary>
    /// Validates that an object is not null.
    /// </summary>
    /// <param name="value">The value to check</param>
    /// <param name="fieldName">The field name</param>
    /// <param name="errorMessage">Optional custom error message</param>
    /// <returns>This collector instance for method chaining</returns>
    public ValidationErrorCollector ValidateRequired(object? value, string fieldName, string? errorMessage = null)
    {
        if (value == null)
        {
            AddError(fieldName, errorMessage ?? $"{fieldName} is required");
        }
        return this;
    }

    /// <summary>
    /// Validates email format using a simple pattern.
    /// </summary>
    /// <param name="email">The email to validate</param>
    /// <param name="fieldName">The field name</param>
    /// <param name="errorMessage">Optional custom error message</param>
    /// <returns>This collector instance for method chaining</returns>
    public ValidationErrorCollector ValidateEmail(string? email, string fieldName, string? errorMessage = null)
    {
        if (!string.IsNullOrWhiteSpace(email))
        {
            var emailPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
            if (!System.Text.RegularExpressions.Regex.IsMatch(email, emailPattern))
            {
                AddError(fieldName, errorMessage ?? "Email format is invalid");
            }
        }
        return this;
    }

    /// <summary>
    /// Validates string length.
    /// </summary>
    /// <param name="value">The string to validate</param>
    /// <param name="fieldName">The field name</param>
    /// <param name="minLength">Minimum length (optional)</param>
    /// <param name="maxLength">Maximum length (optional)</param>
    /// <returns>This collector instance for method chaining</returns>
    public ValidationErrorCollector ValidateLength(string? value, string fieldName, int? minLength = null, int? maxLength = null)
    {
        if (value != null)
        {
            if (minLength.HasValue && value.Length < minLength.Value)
            {
                AddError(fieldName, $"{fieldName} must be at least {minLength.Value} characters long");
            }
            
            if (maxLength.HasValue && value.Length > maxLength.Value)
            {
                AddError(fieldName, $"{fieldName} must be no more than {maxLength.Value} characters long");
            }
        }
        return this;
    }

    /// <summary>
    /// Checks if any validation errors have been collected.
    /// </summary>
    /// <returns>True if there are validation errors</returns>
    public bool HasErrors() => _errors.Count > 0;

    /// <summary>
    /// Gets the total count of validation errors.
    /// </summary>
    /// <returns>The total number of error messages</returns>
    public int ErrorCount => _errors.Values.Sum(errors => errors.Count);

    /// <summary>
    /// Gets the collected validation errors.
    /// </summary>
    /// <returns>Dictionary of field names to error messages</returns>
    public Dictionary<string, List<string>> GetErrors() => new(_errors);

    /// <summary>
    /// Gets all error messages as a flat list.
    /// </summary>
    /// <returns>List of all error messages</returns>
    public List<string> GetAllErrorMessages() => _errors.Values.SelectMany(errors => errors).ToList();

    /// <summary>
    /// Clears all collected errors.
    /// </summary>
    public void Clear() => _errors.Clear();
}