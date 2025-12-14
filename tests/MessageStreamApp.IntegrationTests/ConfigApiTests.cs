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
