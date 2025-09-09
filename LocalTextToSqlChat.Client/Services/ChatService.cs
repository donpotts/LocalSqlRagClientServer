using LocalTextToSqlChat.Client.Models;
using System.Net.Http.Json;

namespace LocalTextToSqlChat.Client.Services;

public class ChatService
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    
    public ChatService(HttpClient httpClient, AppSettings appSettings)
    {
        _httpClient = httpClient;
        _baseUrl = appSettings.ApiSettings.BaseUrl;
    }
    
    public async Task<ChatResponse?> SendMessageAsync(string message)
    {
        try
        {
            var request = new ChatRequest { Message = message };
            var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/chat", request);
            
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<ChatResponse>();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Chat error: {ex.Message}");
        }
        
        return null;
    }
}