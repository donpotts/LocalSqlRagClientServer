namespace LocalTextToSqlChat.Client.Models;

public class AppSettings
{
    public ApiSettings ApiSettings { get; set; } = new();
}

public class ApiSettings
{
    public string BaseUrl { get; set; } = string.Empty;
}