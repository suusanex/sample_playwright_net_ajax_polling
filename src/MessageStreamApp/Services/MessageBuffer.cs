using System.Collections.Generic;
using System.Threading.Channels;
using MessageStreamApp.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MessageStreamApp.Services;

public sealed class MessageBuffer
{
    private readonly ChannelReader<Message> _reader;
    private readonly ChannelWriter<Message> _writer;
    private readonly ILogger<MessageBuffer> _logger;

    public MessageBuffer(ILogger<MessageBuffer> logger, IOptions<StreamConfiguration> configuration)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        if (configuration is null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        configuration.Value.Validate();

        var options = new BoundedChannelOptions(configuration.Value.BufferCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        };

        var channel = Channel.CreateBounded<Message>(options);
        _writer = channel.Writer;
        _reader = channel.Reader;
    }

    public ChannelWriter<Message> Writer => _writer;

    public ChannelReader<Message> Reader => _reader;

    public void Enqueue(Message message)
    {
        if (message is null)
        {
            throw new ArgumentNullException(nameof(message));
        }

        if (!_writer.TryWrite(message))
        {
            _logger.LogWarning("Buffer write rejected: channel is closed");
        }
    }

    public IReadOnlyList<Message> DequeueAll()
    {
        var messages = new List<Message>();

        while (_reader.TryRead(out var message))
        {
            messages.Add(message);
        }

        return messages;
    }
}
