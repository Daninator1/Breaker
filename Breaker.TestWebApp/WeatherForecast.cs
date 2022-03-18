namespace Breaker.TestWebApp;

public class WeatherForecast
{
    public DateTime Date { get; set; }

    public float TemperatureC { get; set; }

    public int TemperatureF => 32 + (int)(this.TemperatureC / 0.5556);

    public string? Summary { get; set; }
}