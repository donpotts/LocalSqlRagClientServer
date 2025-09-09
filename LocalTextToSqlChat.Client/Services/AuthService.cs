using Blazored.LocalStorage;
using LocalTextToSqlChat.Client.Models;
using System.Net.Http.Json;
using System.Text.Json;

namespace LocalTextToSqlChat.Client.Services;

public class AuthService
{
    private readonly HttpClient _httpClient;
    private readonly ILocalStorageService _localStorage;
    private readonly string _baseUrl;
    
    public AuthService(HttpClient httpClient, ILocalStorageService localStorage, AppSettings appSettings)
    {
        _httpClient = httpClient;
        _localStorage = localStorage;
        _baseUrl = appSettings.ApiSettings.BaseUrl;
    }
    
    public async Task<bool> LoginAsync(LoginRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/auth/login", request);
            if (response.IsSuccessStatusCode)
            {
                var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();
                if (authResponse != null)
                {
                    await _localStorage.SetItemAsync("authToken", authResponse.Token);
                    await _localStorage.SetItemAsync("username", authResponse.Username);
                    await _localStorage.SetItemAsync("isAdmin", authResponse.IsAdmin);
                    await _localStorage.SetItemAsync("tokenExpires", authResponse.Expires);
                    
                    _httpClient.DefaultRequestHeaders.Authorization = 
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authResponse.Token);
                    
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Login error: {ex.Message}");
        }
        
        return false;
    }
    
    public async Task<bool> RegisterAsync(RegisterRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/auth/register", request);
            if (response.IsSuccessStatusCode)
            {
                var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();
                if (authResponse != null)
                {
                    await _localStorage.SetItemAsync("authToken", authResponse.Token);
                    await _localStorage.SetItemAsync("username", authResponse.Username);
                    await _localStorage.SetItemAsync("isAdmin", authResponse.IsAdmin);
                    await _localStorage.SetItemAsync("tokenExpires", authResponse.Expires);
                    
                    _httpClient.DefaultRequestHeaders.Authorization = 
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authResponse.Token);
                    
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Register error: {ex.Message}");
        }
        
        return false;
    }
    
    public async Task<bool> IsAuthenticatedAsync()
    {
        try
        {
            var token = await _localStorage.GetItemAsync<string>("authToken");
            var expires = await _localStorage.GetItemAsync<DateTime?>("tokenExpires");
            
            if (string.IsNullOrEmpty(token) || expires == null || expires < DateTime.UtcNow)
            {
                await LogoutAsync();
                return false;
            }
            
            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    public async Task<string?> GetUsernameAsync()
    {
        return await _localStorage.GetItemAsync<string>("username");
    }
    
    public async Task<bool> IsAdminAsync()
    {
        return await _localStorage.GetItemAsync<bool>("isAdmin");
    }
    
    public async Task LogoutAsync()
    {
        await _localStorage.RemoveItemAsync("authToken");
        await _localStorage.RemoveItemAsync("username");
        await _localStorage.RemoveItemAsync("isAdmin");
        await _localStorage.RemoveItemAsync("tokenExpires");
        
        _httpClient.DefaultRequestHeaders.Authorization = null;
    }
}