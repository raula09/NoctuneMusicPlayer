using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MusicPlayerApp.Services;

public class AuthService
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "http://localhost:5042"; // Your API URL

    public AuthService()
    {
        _httpClient = new HttpClient();
    }

    public async Task<bool> RegisterAsync(string email, string password)
    {
        try
        {
            var payload = new
            {
                email = email,
                password = password
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{BaseUrl}/auth/register", content);
            
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Register error: {ex.Message}");
            return false;
        }
    }

    public async Task<(bool success, string message)> VerifyAsync(string code)
    {
        try
        {
            var payload = new
            {
                code = code
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{BaseUrl}/auth/verify", content);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Verify error response: {errorBody}");
                return (false, $"Server error: {response.StatusCode}");
            }

            return (true, "Success");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Verify error: {ex.Message}");
            return (false, $"Error: {ex.Message}");
        }
    }

    public async Task<string?> LoginAsync(string email, string password)
    {
        try
        {
            var payload = new
            {
                email = email,
                password = password
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{BaseUrl}/auth/login", content);
            
            if (!response.IsSuccessStatusCode)
                return null;

            var responseBody = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<LoginResponse>(responseBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return result?.Token;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Login error: {ex.Message}");
            return null;
        }
    }

    private class LoginResponse
    {
        public string Token { get; set; }
        public string Email { get; set; }
    }
}