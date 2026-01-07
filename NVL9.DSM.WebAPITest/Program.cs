using NVL9.DSM.Core;
using NVL9.DSM.Core.Models;
using NVL9.DSM.WebAPITest;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet($"/{CoreConstants.RootEndPoint}", () =>
{
    DSMEnvelope<WeatherForecast[]>.Init();
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName(CoreConstants.GetRootEndPoint)
.WithOpenApi();

// Example endpoint demonstrating validation errors
app.MapPost("/customers", (CustomerRequest request) =>
{
    var envelope = DSMEnvelope<CustomerResponse>.InitWithCaller("Program");
    
    // Collect validation errors
    var validator = new ValidationErrorCollector()
        .ValidateRequired(request.Email, "email")
        .ValidateEmail(request.Email, "email")
        .ValidateRequired(request.Phone, "phone")
        .ValidateLength(request.Name, "name", minLength: 2, maxLength: 100);
    
    // Check for validation errors
    if (validator.HasErrors())
    {
        return envelope.ValidationFailed(validator);
    }
    
    // Process successful request
    var customer = new CustomerResponse(Guid.NewGuid(), request.Name!, request.Email!, request.Phone!);
    return envelope.Success(customer);
})
.WithName("CreateCustomer")
.WithOpenApi();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

record CustomerRequest(string? Name, string? Email, string? Phone);
record CustomerResponse(Guid Id, string Name, string Email, string Phone);
