using System.Collections.Concurrent;
using System.Threading;
using MessageStreamApp.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MessageStreamApp.Services;

public sealed class MessageBuffer
{
    private readonly ConcurrentQueue<Message> _queue = new();
    private readonly ILogger<MessageBuffer> _logger;
    private readonly int _capacity;
    private int _count;

    public MessageBuffer(ILogger<MessageBuffer> logger, IOptions<StreamConfiguration> configuration)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        if (configuration is null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        configuration.Value.Validate();
        _capacity = configuration.Value.BufferCapacity;
    }

    public int CurrentCount => Volatile.Read(ref _count);

    public void Enqueue(Message message)
    {
        if (message is null)
        {
            throw new ArgumentNullException(nameof(message));
        }

        _queue.Enqueue(message);
        var newCount = Interlocked.Increment(ref _count);

        if (newCount > _capacity && _queue.TryDequeue(out _))
        {
            Interlocked.Decrement(ref _count);
            _logger.LogWarning("Buffer overflow: dropped oldest message");
        }
    }

    public IReadOnlyList<Message> DequeueAll()
    {
        var result = new List<Message>();

        while (_queue.TryDequeue(out var message))
        {
            Interlocked.Decrement(ref _count);
            result.Add(message);
        }

        return result;
    }
}
