using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Diagnostics;
using UserService.DTOs;

namespace UserService.SyncDataServices.Http;

public class HttpHobbyDataClient : IHobbyDataClient
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public HttpHobbyDataClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }
    
    public async Task SendUsersToHobby(UserReadDTO userReadDto)
    {
        var httpContent = new StringContent(JsonSerializer.Serialize(userReadDto),
        Encoding.UTF8,
        "application/json"
        );
        
        var response = await _httpClient.PostAsync($"{_configuration["HobbyService"]}", httpContent);
        //_configuration["HobbyService"]}/api/h/users/
        
        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine("--> Sync POST to HobbyService was NOT OK!");
        }
        else
        {
            Console.WriteLine("--> Sync POST to HobbyService was OK!");
        }
    }
}