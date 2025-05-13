using NVL9.DSM.Core;
using NVL9.DSM.Core.Models;

namespace NVL9.DSM.WebAPITest.Business;

public class WeatherForecastFacade
{
    private static readonly string[] Summaries = new[]
{
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    };

    public async Task<DSMEnvelope<List<WeatherForecast>>> GetWeatherForecastAsync(string args)
    {
        var callerMethodName = new CallerMethodName(DSMEnvelopeManager.GetCallerClassName(), DSMEnvelopeManager.GetCallerMethodName());
        var envelope = DSMEnvelopeManager.Instance.InitEnvelopeAsync<List<WeatherForecast>>(args, callerMethodName);

        try
        {
            var forecast = Enumerable.Range(1, 5).Select(index =>
                new WeatherForecast
                (
                    DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                    Random.Shared.Next(-20, 55),
                    Summaries[Random.Shared.Next(Summaries.Length)]
                ))
                .ToList();
            envelope.Success(forecast);
        }
        catch (Exception ex)
        {
            envelope.CaptureException(ex);
        }
        finally
        {
            envelope.FinishLifeCycle();
        }

        return envelope;
    }
}
