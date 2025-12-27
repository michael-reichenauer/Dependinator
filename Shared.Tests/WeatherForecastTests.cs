using Dependinator.Shared;

namespace Dependinator.Shared.Tests;

public class WeatherForecastTests
{
    [Fact]
    public void TemperatureF_ShouldConvertFromCelsius()
    {
        var forecast = new WeatherForecast { TemperatureC = 0 };

        var fahrenheit = forecast.TemperatureF;

        Assert.Equal(32, fahrenheit);
    }
}
