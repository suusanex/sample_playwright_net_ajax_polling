using System.Text.Json;
using System.Text.Json.Serialization;
using MessageStreamApp.Controllers;
using MessageStreamApp.Models;
using MessageStreamApp.Services;
using Microsoft.AspNetCore.Mvc;

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
        builder.Services.Configure<StreamConfiguration>(builder.Configuration.GetSection(StreamConfiguration.SectionName));
        builder.Services.AddSingleton<MessageBuffer>();
        builder.Services.AddHostedService<MessageGeneratorService>();
        builder.Services.AddControllers().AddApplicationPart(typeof(MessagesController).Assembly);
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
