using System;

namespace MessageStreamApp.Models;

public sealed class Message
{
    public long Id { get; set; }

    public DateTime Timestamp { get; set; }

    public string Content { get; set; } = string.Empty;
}
