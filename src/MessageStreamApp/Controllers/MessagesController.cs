using System.Net.Mime;
using System.Text;
using System.Text.Json;
using MessageStreamApp.Models;
using MessageStreamApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace MessageStreamApp.Controllers;

[ApiController]
[Route("api/messages")]
public sealed class MessagesController : ControllerBase
{
    private readonly MessageBuffer _buffer;
    private readonly IOptions<StreamConfiguration> _configuration;
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly ILogger<MessagesController> _logger;

    public MessagesController(
        MessageBuffer buffer,
        IOptions<StreamConfiguration> configuration,
        IOptions<JsonOptions> jsonOptions,
        ILogger<MessagesController> logger)
    {
        _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (jsonOptions is null)
        {
            throw new ArgumentNullException(nameof(jsonOptions));
        }

        _serializerOptions = jsonOptions.Value.JsonSerializerOptions;
        _configuration.Value.Validate();
    }

    [HttpGet("poll")]
    [Produces(MediaTypeNames.Application.Json)]
    public ActionResult<IReadOnlyList<Message>> Poll()
    {
        var messages = _buffer.DequeueAll();
        _logger.LogInformation("Polling returned {Count} messages", messages.Count);
        return Ok(messages);
    }

    [HttpGet("stream")]
    [Produces("application/x-ndjson")]
    public async Task Stream()
    {
        Response.StatusCode = StatusCodes.Status200OK;
        Response.ContentType = "application/x-ndjson";

        await using var writer = new StreamWriter(Response.Body, Encoding.UTF8, leaveOpen: true);
        var token = HttpContext.RequestAborted;

        while (!token.IsCancellationRequested)
        {
            IReadOnlyList<Message> messages;
            try
            {
                messages = _buffer.DequeueAll();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to dequeue messages for stream");
                throw;
            }

            if (messages.Count > 0)
            {
                foreach (var message in messages)
                {
                    var json = JsonSerializer.Serialize(message, _serializerOptions);
                    await writer.WriteLineAsync(json);
                }

                await writer.FlushAsync();
                await Response.Body.FlushAsync(token);
                _logger.LogInformation("Streaming sent {Count} messages", messages.Count);
            }
            else
            {
                await Task.Delay(_configuration.Value.MessageGenerationIntervalMs, token);
            }

            await Task.Delay(_configuration.Value.ClientFetchIntervalMs, token);
        }
    }
}
