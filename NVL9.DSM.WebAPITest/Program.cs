using NVL9.DSM.Core;
using NVL9.DSM.Core.Models;

public class Program {

    public static async Task Main(string[] args)
    {
        var callerMethodName = new CallerMethodName(DSMEnvelopeManager.GetCallerClassName(), DSMEnvelopeManager.GetCallerMethodName());
        var envelope = DSMEnvelopeManager.Instance.InitEnvelopeAsync<WeatherForecast>(args, callerMethodName);

        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddControllers();
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

        app.MapControllers();

        var forcast = new WeatherForecast();
        envelope.Success(forcast);
        envelope.FinishLifeCycle();

        app.Run();
    }
}

public class WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public WeatherForecast() : this(DateOnly.MinValue, 0, null) { }
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
