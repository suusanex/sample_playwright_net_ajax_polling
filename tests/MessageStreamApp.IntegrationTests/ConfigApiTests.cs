using System.Net.Http.Json;
using MessageStreamApp.Models;

namespace MessageStreamApp.IntegrationTests;

public sealed class ConfigApiTests : IntegrationTestBase
{
    [Fact]
    public async Task GetConfig_ReturnsConfiguredValues()
    {
        var config = await Client.GetFromJsonAsync<StreamConfiguration>("/api/config");
        Assert.NotNull(config);
        Assert.Equal("Streaming", config!.Mode);
        Assert.Equal(10, config.MessageGenerationIntervalMs);
        Assert.Equal(100, config.ClientFetchIntervalMs);
        Assert.Equal(100, config.BufferCapacity);
    }
}

public sealed class ConfigApiIntervalTests
{
    [Theory]
    [InlineData("500", 500)]
    [InlineData("5000", 5000)]
    public async Task GetConfig_ReflectsClientFetchInterval(string interval, int expected)
    {
        var settings = TestWebApplicationFactory.CreateFastConfig();
        settings[$"{StreamConfiguration.SectionName}:ClientFetchIntervalMs"] = interval;

        await using var factory = new TestWebApplicationFactory(settings);
        using var client = factory.CreateClient();

        var config = await client.GetFromJsonAsync<StreamConfiguration>("/api/config");
        Assert.NotNull(config);
        Assert.Equal(expected, config!.ClientFetchIntervalMs);
    }
}
