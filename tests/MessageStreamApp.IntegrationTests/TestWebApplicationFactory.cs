using MessageStreamApp.Models;
using MessageStreamApp.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;

namespace MessageStreamApp.IntegrationTests;

public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly IDictionary<string, string?> _configuration;
    private readonly bool _disableGenerator;

    public TestWebApplicationFactory(IDictionary<string, string?> configuration, bool disableGenerator = false)
    {
        _configuration = configuration;
        _disableGenerator = disableGenerator;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(_configuration);
        });

        builder.ConfigureServices(services =>
        {
            if (_disableGenerator)
            {
                var descriptor = services.SingleOrDefault(s => s.ImplementationType == typeof(MessageGeneratorService));
                if (descriptor is not null)
                {
                    services.Remove(descriptor);
                }
            }
        });
    }

    public static IDictionary<string, string?> CreateFastConfig()
    {
        return new Dictionary<string, string?>
        {
            [$"{StreamConfiguration.SectionName}:Mode"] = "Streaming",
            [$"{StreamConfiguration.SectionName}:MessageGenerationIntervalMs"] = "10",
            [$"{StreamConfiguration.SectionName}:ClientFetchIntervalMs"] = "100",
            [$"{StreamConfiguration.SectionName}:BufferCapacity"] = "100",
        };
    }

    public static IDictionary<string, string?> CreatePollingConfig()
    {
        return new Dictionary<string, string?>
        {
            [$"{StreamConfiguration.SectionName}:Mode"] = "Polling",
            [$"{StreamConfiguration.SectionName}:MessageGenerationIntervalMs"] = "200",
            [$"{StreamConfiguration.SectionName}:ClientFetchIntervalMs"] = "200",
            [$"{StreamConfiguration.SectionName}:BufferCapacity"] = "100",
        };
    }
}
