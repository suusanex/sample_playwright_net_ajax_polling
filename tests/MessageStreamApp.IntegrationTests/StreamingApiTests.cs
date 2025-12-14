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
    private static readonly JsonSerializerOptions CaseInsensitiveOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

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
        var message = JsonSerializer.Deserialize<Message>(sanitized, CaseInsensitiveOptions);
        Assert.NotNull(message);

        cts.Cancel();
    }

    [Fact]
    public async Task Stream_SendsMultipleBatches()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var buffer = Factory.Services.GetRequiredService<MessageBuffer>();
        buffer.Enqueue(new Message
        {
            Id = 0,
            Timestamp = DateTime.UtcNow,
            Content = "seed",
        });

        await Task.Delay(100, cts.Token);
        using var response = await Client.GetAsync("/api/messages/stream", HttpCompletionOption.ResponseHeadersRead, cts.Token);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
        using var reader = new StreamReader(stream);

        var firstLine = await reader.ReadLineAsync(cts.Token);
        Assert.False(string.IsNullOrWhiteSpace(firstLine));
        var sanitizedFirst = firstLine!.TrimStart('\uFEFF');
        var first = JsonSerializer.Deserialize<Message>(sanitizedFirst, CaseInsensitiveOptions);
        Assert.NotNull(first);

        var secondLine = await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(3), cts.Token);
        Assert.False(string.IsNullOrWhiteSpace(secondLine));

        var second = JsonSerializer.Deserialize<Message>(secondLine!.Trim(), CaseInsensitiveOptions);
        Assert.NotNull(second);
        Assert.True(
            second!.Id > first!.Id,
            $"Second message id {second.Id} is not greater than first {first!.Id}. FirstLine='{sanitizedFirst}', SecondLine='{secondLine}'.");

        cts.Cancel();
    }
}
