using System.Net.Http.Json;
using MessageStreamApp.Models;

namespace MessageStreamApp.IntegrationTests;

public sealed class PollingApiTests : IntegrationTestBase
{
    [Fact]
    public async Task Poll_ReturnsMessagesThenEmptiesBuffer()
    {
        await Task.Delay(150);
        var first = await Client.GetFromJsonAsync<List<Message>>("/api/messages/poll");
        Assert.NotNull(first);
        Assert.NotEmpty(first!);

        var highestId = first!.Max(m => m.Id);

        var afterFirst = await Client.GetFromJsonAsync<List<Message>>("/api/messages/poll");
        Assert.NotNull(afterFirst);
        if (afterFirst!.Count > 0)
        {
            Assert.True(afterFirst.Min(m => m.Id) > highestId);
        }

        await Task.Delay(200);
        var second = await Client.GetFromJsonAsync<List<Message>>("/api/messages/poll");
        Assert.NotNull(second);
        Assert.NotEmpty(second!);
        Assert.True(second!.Min(m => m.Id) > highestId);
    }

    [Fact]
    public async Task Poll_ReturnsEmptyArray_WhenBufferIsEmpty()
    {
        await using var factory = new TestWebApplicationFactory(TestWebApplicationFactory.CreateFastConfig(), disableGenerator: true);
        using var client = factory.CreateClient();

        var messages = await client.GetFromJsonAsync<List<Message>>("/api/messages/poll");
        Assert.NotNull(messages);
        Assert.Empty(messages!);
    }

    [Fact]
    public async Task Poll_InPollingMode_ReturnsNewMessagesOnSubsequentCalls()
    {
        await using var factory = new TestWebApplicationFactory(TestWebApplicationFactory.CreatePollingConfig());
        using var client = factory.CreateClient();

        await Task.Delay(150);

        var first = await client.GetFromJsonAsync<List<Message>>("/api/messages/poll");
        Assert.NotNull(first);
        Assert.NotEmpty(first!);
        var maxFirst = first!.Max(m => m.Id);

        var immediate = await client.GetFromJsonAsync<List<Message>>("/api/messages/poll");
        Assert.NotNull(immediate);
        Assert.Empty(immediate!);

        await Task.Delay(150);
        var third = await client.GetFromJsonAsync<List<Message>>("/api/messages/poll");
        Assert.NotNull(third);
        Assert.NotEmpty(third!);
        Assert.True(third!.Min(m => m.Id) > maxFirst);
    }
}
