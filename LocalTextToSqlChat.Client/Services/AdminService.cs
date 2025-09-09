using LocalTextToSqlChat.Client.Models;
using System.Net.Http.Json;

namespace LocalTextToSqlChat.Client.Services;

public class AdminService
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    
    public AdminService(HttpClient httpClient, AppSettings appSettings)
    {
        _httpClient = httpClient;
        _baseUrl = appSettings.ApiSettings.BaseUrl;
    }
    
    public async Task<List<UserDto>?> GetAllUsersAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/admin/users");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<List<UserDto>>();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting users: {ex.Message}");
        }
        
        return null;
    }
    
    public async Task<UserDto?> GetUserAsync(int id)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/admin/users/{id}");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<UserDto>();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting user: {ex.Message}");
        }
        
        return null;
    }
    
    public async Task<bool> UpdateUserAsync(int id, UpdateUserRequest request)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"{_baseUrl}/admin/users/{id}", request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating user: {ex.Message}");
            return false;
        }
    }
    
    public async Task<bool> ToggleUserAdminAsync(int id)
    {
        try
        {
            var response = await _httpClient.PostAsync($"{_baseUrl}/admin/users/{id}/toggle-admin", null);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error toggling admin status: {ex.Message}");
            return false;
        }
    }
    
    public async Task<bool> DeleteUserAsync(int id)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"{_baseUrl}/admin/users/{id}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting user: {ex.Message}");
            return false;
        }
    }
}