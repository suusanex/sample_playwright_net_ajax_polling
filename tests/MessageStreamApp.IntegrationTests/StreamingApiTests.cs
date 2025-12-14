using System.Net.Http;
using System.Linq;
using System.Text;
using System.Text.Json;
using MessageStreamApp.Models;
using MessageStreamApp.Services;
using Microsoft.Extensions.DependencyInjection;

namespace MessageStreamApp.IntegrationTests;

public sealed class StreamingApiTests : IntegrationTestBase
{
    [Fact]
    public async Task Stream_ReturnsNdjsonLines()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var buffer = Factory.Services.GetRequiredService<MessageBuffer>();
        buffer.Enqueue(new Message
        {
            Id = 999,
            Timestamp = DateTime.UtcNow,
            Content = "seed",
        });

        await Task.Delay(100, cts.Token);
        using var response = await Client.GetAsync("/api/messages/stream", HttpCompletionOption.ResponseHeadersRead, cts.Token);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);

        var bufferBytes = new byte[1024];
        var read = await stream.ReadAsync(bufferBytes, cts.Token);
        Assert.True(read > 0);

        var payload = Encoding.UTF8.GetString(bufferBytes, 0, read);
        var firstLine = payload.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        Assert.False(string.IsNullOrWhiteSpace(firstLine));
        var sanitized = firstLine!.TrimStart('\uFEFF');
        var message = JsonSerializer.Deserialize<Message>(sanitized);
        Assert.NotNull(message);

        cts.Cancel();
    }
}
