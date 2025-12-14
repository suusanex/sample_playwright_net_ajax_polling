using System.Threading;
using System.Threading.Tasks;
using MessageStreamApp.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MessageStreamApp.Services;

public sealed class MessageGeneratorService : BackgroundService
{
    private readonly ILogger<MessageGeneratorService> _logger;
    private readonly MessageBuffer _buffer;
    private readonly IOptions<StreamConfiguration> _configuration;
    private long _sequence;

    public MessageGeneratorService(
        ILogger<MessageGeneratorService> logger,
        MessageBuffer buffer,
        IOptions<StreamConfiguration> configuration)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _configuration.Value.Validate();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var id = Interlocked.Increment(ref _sequence);
                var timestamp = DateTime.UtcNow;
                var content = $"Message #{id} at {timestamp:O}";
                var message = new Message
                {
                    Id = id,
                    Timestamp = timestamp,
                    Content = content,
                };

                _buffer.Enqueue(message);
                _logger.LogInformation("Generated message #{Id}", id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate message");
                throw;
            }

            try
            {
                await Task.Delay(_configuration.Value.MessageGenerationIntervalMs, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
