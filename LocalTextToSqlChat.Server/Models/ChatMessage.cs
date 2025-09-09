namespace LocalTextToSqlChat.Server.Models;

public class ChatMessage
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty;
    public string? SqlQuery { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}