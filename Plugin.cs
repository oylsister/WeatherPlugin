using System.Runtime.CompilerServices;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace WeatherPlugin;
public class WeatherPlugin : BasePlugin, IPluginConfig<APIConfigs>
{
    public override string ModuleName => "Weather Plugin via openweathermap.org";
    public override string ModuleDescription => "Get Weather status base city name with HttpsRequest to api.openweathermap.org";
    public override string ModuleVersion => "1.0";
    public override string ModuleAuthor => "Oylsister";

    public APIConfigs Config {  get; set; } = new APIConfigs();
    private readonly WeatherHttpClient _weatherClient;

    public WeatherPlugin(WeatherHttpClient weatherClient)
    {
        _weatherClient = weatherClient;
    }

    public void OnConfigParsed(APIConfigs config)
    {
        Config = config;
        _weatherClient.Initialize(config);
    }

    [CommandHelper(1, "css_weather <city name>")]
    [ConsoleCommand("css_weather")]
    public async void WeatherCommandAsync(CCSPlayerController? player, CommandInfo info)
    {
        var response = await _weatherClient.GetWeatherCity(info.GetArg(1));

        if (response == null)
        {
            info.ReplyToCommand($"[Weather] result is null!");
            return;
        }

        var data = response;

        Server.NextFrame(() => {
            if (player == null)
                return;

            if (data.Status != 200)
            {
                player.PrintToChat($" {ChatColors.DarkRed}####### WEATHER ERROR #######");
                player.PrintToChat($"Status Code: {data.Status}");
                player.PrintToChat($"Reason: {data.Message}");
                return;
            }

            //info.ReplyToCommand($"Status Code: {result["cod"]}");
            player.PrintToChat($" {ChatColors.Green}####### WEATHER #######");
            player.PrintToChat($" {ChatColors.Lime}Location: {ChatColors.Default}{data.City}, {data.Country} {data.CountryShort}");
            player.PrintToChat($" {ChatColors.Lime}Temperature: {ChatColors.Default}{data.Temperature}°C | Feels like: {data.FeelTemperature}°C");
            player.PrintToChat($" {ChatColors.Lime}Weather: {ChatColors.Default}{data.Weather}, {data.WeatherDescription}");
        });
    }
}

public class WeatherRequestInjection : IPluginServiceCollection<WeatherPlugin>
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddHttpClient<IWeatherService, WeatherHttpClient>();
        services.AddSingleton<WeatherHttpClient>();
        services.AddSingleton<HttpClient>();
    }
}

public interface IWeatherService
{
    Task<WeatherData> GetWeatherCity(string city);
    void Initialize(APIConfigs configs);
}

public class WeatherHttpClient : IWeatherService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WeatherHttpClient> _logger;
    private APIConfigs? _apiConfigs;

    public WeatherHttpClient(HttpClient httpClient, ILogger<WeatherHttpClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public void Initialize(APIConfigs configs)
    {
        _apiConfigs = configs;
        _httpClient.BaseAddress = new Uri(_apiConfigs.WeatherUrl);
    }

    public async Task<WeatherData> GetWeatherCity(string city)
    {
        var values = $"weather?q={city}&appid={_apiConfigs?.Token}";
        using HttpResponseMessage response = await _httpClient.GetAsync(values);

        var request = response.RequestMessage?.RequestUri?.OriginalString;
        //_logger.LogInformation(request!);

        var status = (int)response.StatusCode;

        var jsonReponse = await response.Content.ReadAsStringAsync();
        var result = JObject.Parse(jsonReponse);

        WeatherData data = new WeatherData();

        data.Status = status;

        if(response.IsSuccessStatusCode)
        {
            data.City = (string)result["name"]!;
            data.CountryShort = (string)result["sys"]?["country"]!;
            data.Country = WeatherData.GetCountryNameFromShort(data.CountryShort);
            data.Temperature = WeatherData.KelvinToCelsius((float)result["main"]?["temp"]!);
            data.FeelTemperature = WeatherData.KelvinToCelsius((float)result["main"]?["feels_like"]!);
            data.Weather = (string)result["weather"]?[0]?["main"]!;
            data.WeatherDescription = (string)result["weather"]?[0]?["description"]!;
        }

        else
        {
            data.Message = (string)result["message"]!;
        }

        return data;
    }
}
