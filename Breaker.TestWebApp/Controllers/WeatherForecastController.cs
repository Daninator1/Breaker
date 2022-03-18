using Microsoft.AspNetCore.Mvc;

namespace Breaker.TestWebApp.Controllers;

public class TestClass7 { }

[ApiController]
[Route("[controller]")]
public class WeatherForecastController : ControllerBase
{
    private static readonly string[] Summaries =
    {
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    };

    private readonly ILogger<WeatherForecastController> _logger;

    public WeatherForecastController(ILogger<WeatherForecastController> logger) => this._logger = logger;

    [HttpPost]
    public IEnumerable<IEnumerable<WeatherForecast>> TestEndpoint(IEnumerable<int> ints, int text)
        => Enumerable.Empty<IEnumerable<WeatherForecast>>();

    [HttpPut]
    public IEnumerable<KeyValuePair<string, IEnumerable<WeatherForecast>>> TestEndpoint2()
        => Enumerable.Empty<KeyValuePair<string, IEnumerable<WeatherForecast>>>();

    [HttpDelete]
    public WeatherForecastWrapper TestEndpoint3(IEnumerable<WeatherForecastWrapper> forecastWrappers) => new();

    [HttpGet(Name = "GetWeatherForecast")]
    public IEnumerable<WeatherForecast> Get([FromQuery] string name)
    {
        return Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateTime.Now.AddDays(index),
                TemperatureC = Random.Shared.Next(-20, 55),
                Summary = Summaries[Random.Shared.Next(Summaries.Length)]
            })
            .ToArray();
    }

    public class WeatherForecastWrapper
    {
        public WeatherForecast InnerWeatherForecast { get; set; }
    }
}