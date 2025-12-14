namespace MessageStreamApp.Models;

public sealed class StreamConfiguration
{
    public const string SectionName = "MessageStream";

    public string Mode { get; set; } = "Streaming";

    public int MessageGenerationIntervalMs { get; set; } = 500;

    public int ClientFetchIntervalMs { get; set; } = 2000;

    public int BufferCapacity { get; set; } = 100;

    public void Validate()
    {
        if (MessageGenerationIntervalMs is < 10 or > 10000)
        {
            throw new ArgumentOutOfRangeException(nameof(MessageGenerationIntervalMs), "MessageGenerationIntervalMs must be between 10 and 10000");
        }

        if (ClientFetchIntervalMs is < 100 or > 30000)
        {
            throw new ArgumentOutOfRangeException(nameof(ClientFetchIntervalMs), "ClientFetchIntervalMs must be between 100 and 30000");
        }

        if (BufferCapacity is < 10 or > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(BufferCapacity), "BufferCapacity must be between 10 and 1000");
        }

        if (string.IsNullOrWhiteSpace(Mode))
        {
            throw new ArgumentException("Mode is required", nameof(Mode));
        }
    }
}
