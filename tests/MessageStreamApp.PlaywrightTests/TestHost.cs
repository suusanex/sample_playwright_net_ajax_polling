using MessageStreamApp.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System.IO;

namespace MessageStreamApp.PlaywrightTests;

public sealed class TestHost : IAsyncDisposable
{
    private IHost? _host;

    public Uri BaseAddress { get; private set; } = null!;

    public async Task StartAsync(IDictionary<string, string?> configuration)
    {
        var contentRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "MessageStreamApp"));
        var options = new WebApplicationOptions
        {
            ContentRootPath = contentRoot,
            WebRootPath = Path.Combine(contentRoot, "wwwroot"),
            ApplicationName = typeof(Program).Assembly.FullName,
        };

        var builder = WebApplication.CreateBuilder(options);
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(configuration);
        AppConfiguration.ConfigureServices(builder);

        var app = builder.Build();
        AppConfiguration.ConfigurePipeline(app);

        await app.StartAsync();
        var url = app.Urls.First();
        BaseAddress = new Uri(url);
        _host = app;
    }

    public async ValueTask DisposeAsync()
    {
        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
    }

    public static IDictionary<string, string?> CreateConfig(string mode)
    {
        return new Dictionary<string, string?>
        {
            [$"{StreamConfiguration.SectionName}:Mode"] = mode,
            [$"{StreamConfiguration.SectionName}:MessageGenerationIntervalMs"] = "100",
            [$"{StreamConfiguration.SectionName}:ClientFetchIntervalMs"] = "100",
            [$"{StreamConfiguration.SectionName}:BufferCapacity"] = "1000",
        };
    }
}
