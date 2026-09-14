using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MafiaCorp.Client.Models;

namespace MafiaCorp.Client.Services;

public class ApiService
{
    private readonly HttpClient _httpClient;

    public ApiService()
    {
        _httpClient = new HttpClient { BaseAddress = new Uri(AppConfig.ApiBaseUrl) };
    }

    public async Task<bool> LoginAsync(string username, string password)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/auth/login", new { Username = username, Password = password });
        
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadFromJsonAsync<LoginResponse>();
            if (content?.Token != null)
            {
                await SecureStorage.SetAsync("auth_token", content.Token);
                return true;
            }
        }
        return false;
    }

    public async Task<bool> RegisterAsync(string username, string password)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/auth/register", new { Username = username, Password = password });
        return response.IsSuccessStatusCode;
    }

    public async Task<PlayerStats?> GetMyStatsAsync()
    {
        var token = await SecureStorage.GetAsync("auth_token");
        if (string.IsNullOrEmpty(token)) return null;

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/stats/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.SendAsync(request);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<PlayerStats>();
        }
        return null;
    }
    
    public async Task<UserDto?> GetMeAsync()
    {
        var token = await SecureStorage.GetAsync("auth_token");
        if (string.IsNullOrEmpty(token)) return null;

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/users/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.SendAsync(request);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<UserDto>();
        }
        return null;
    }

    public void Logout()
    {
        SecureStorage.Remove("auth_token");
    }
}
