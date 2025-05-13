namespace NVL9.DSM.WebAPITest.Controllers;

using Microsoft.AspNetCore.Mvc;
using NVL9.DSM.Core;
using NVL9.DSM.Core.Codes;
using NVL9.DSM.Core.Models;
using NVL9.DSM.WebAPITest.Business;

[ApiController]
[Route("[controller]")]
public class WeatherForecastController : ControllerBase
{
    //private static readonly string[] Summaries = new[]
    //{
    //    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    //};

    private WeatherForecastFacade _weatherForecastFacade;

    public WeatherForecastController()
    {
        _weatherForecastFacade = new WeatherForecastFacade();
    }

    [HttpGet]
    public async Task<DSMEnvelope<List<WeatherForecast>>> Get()
    {
        var callerMethodName = new CallerMethodName(DSMEnvelopeManager.GetCallerClassName(), DSMEnvelopeManager.GetCallerMethodName());
        var envelope = DSMEnvelopeManager.Instance.InitEnvelopeAsync<List<WeatherForecast>>(callerMethodName);

        try
        {
            var forecast = await _weatherForecastFacade.GetWeatherForecastAsync("You Be Here");

            if (forecast.Code != DSMEnvelopeCodeManager.Success)
            {
                return envelope.ReBase(forecast);
            }

            envelope.Success(forecast.Value);
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

        //return Enumerable.Range(1, 5).Select(index =>
        //    new WeatherForecast
        //    (
        //        DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
        //        Random.Shared.Next(-20, 55),
        //        Summaries[Random.Shared.Next(Summaries.Length)]
        //    ))
        //    .ToArray();
    }
}
