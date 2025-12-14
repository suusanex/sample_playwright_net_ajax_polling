using System.Text.Json;
using System.Text.Json.Serialization;
using MessageStreamApp.Models;
using MessageStreamApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

AppConfiguration.ConfigureServices(builder);

var app = builder.Build();

AppConfiguration.ConfigurePipeline(app);

app.Run();

public partial class Program
{
}

public static class AppConfiguration
{
    public static void ConfigureServices(WebApplicationBuilder builder)
    {
        builder.Services.AddOptions<StreamConfiguration>()
            .Bind(builder.Configuration.GetSection(StreamConfiguration.SectionName))
            .Validate(
                config =>
                {
                    try
                    {
                        config.Validate();
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                },
                "StreamConfiguration validation failed");

        builder.Services.AddSingleton<MessageBuffer>();
        builder.Services.AddHostedService<MessageGeneratorService>();
        builder.Services.AddControllers();
        builder.Services.Configure<JsonOptions>(options =>
        {
            options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });
    }

    public static void ConfigurePipeline(WebApplication app)
    {
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("RequestLogging");
        var config = app.Services.GetRequiredService<IOptions<StreamConfiguration>>().Value;
        if (config.ClientFetchIntervalMs < config.MessageGenerationIntervalMs)
        {
            logger.LogWarning("ClientFetchIntervalMs {Fetch} is less than MessageGenerationIntervalMs {Gen}; buffer may grow", config.ClientFetchIntervalMs, config.MessageGenerationIntervalMs);
        }

        app.Use(async (context, next) =>
        {
            logger.LogInformation("Request {Method} {Path}", context.Request.Method, context.Request.Path);
            await next();
        });

        app.UseDefaultFiles();
        app.UseStaticFiles();

        app.MapControllers();
    }
}
