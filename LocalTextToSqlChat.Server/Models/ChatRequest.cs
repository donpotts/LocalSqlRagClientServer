using System.ComponentModel.DataAnnotations;

namespace LocalTextToSqlChat.Server.Models;

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