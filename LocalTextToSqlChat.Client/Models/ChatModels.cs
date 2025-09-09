using System.ComponentModel.DataAnnotations;

namespace LocalTextToSqlChat.Client.Models;

public class ChatRequest
{
    [Required]
    public string Message { get; set; } = string.Empty;
}

public class ChatResponse
{
    public string Response { get; set; } = string.Empty;
    public string? SqlQuery { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public double ProcessingTimeMs { get; set; }
}

public class ChatMessage
{
    public string Message { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty;
    public string? SqlQuery { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public double ProcessingTimeMs { get; set; }
    public bool IsFromUser { get; set; }
}