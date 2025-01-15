using System.Net.Http.Headers;
using System.Text.Json;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using UserService.AsyncDataServices;
using UserService.Data;
using UserService.DTOs;
using UserService.Models;
using UserService.SyncDataServices.Http;

namespace UserService.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController  : ControllerBase
    {
        private readonly IUserRepo _repository;
        private readonly IMapper _mapper;
        private readonly IHobbyDataClient _hobbyDataClient;
        private readonly IMessageBusClient _messageBusClient;
        private readonly IConfiguration _configuration;
        private readonly bool IntegrationMode;
        
        public UsersController(
            IUserRepo repository, 
            IMapper mapper,
            IHobbyDataClient hobbyDataClient,
            IMessageBusClient messageBusClient, 
            IConfiguration configuration
            )
        {
            //This is all registered in the Program.cs inside a builder.services...
            _repository = repository;
            _mapper = mapper;
            _hobbyDataClient = hobbyDataClient;
            _messageBusClient = messageBusClient;
            _configuration = configuration;
            IntegrationMode = _configuration.GetValue<bool>("IntegrationMode");
        }

        
        private bool IsValidJwt()
        {

            var expectedJwt = "eyJhbGciOiJSUzI1NiIsInR5cCIgOiAiSldUIiwia2lkIiA6ICJYc3kxV3dzZTBhaGZrOHZheDI5V2pOR3luLVJzYmxzdjJjNWlsWnZ1bU1jIn0.eyJleHAiOjE3MzYyNzA5MDUsImlhdCI6MTczNjI3MDYwNSwianRpIjoiZjcxMmVjZjYtOWJhNy00N2MwLThiMzktZDhmY2VhOWVlNjg4IiwiaXNzIjoiaHR0cDovL2tleWNsb2FrLWhvYmJ5aHViLmF1c3RyYWxpYWNlbnRyYWwuY2xvdWRhcHAuYXp1cmUuY29tL3JlYWxtcy9Ib2JieUh1YiIsImF1ZCI6ImFjY291bnQiLCJzdWIiOiI0MGRlNmNhNS1mNmQ0LTRkYjgtYTE5Zi1jNTIxMzFhMDMzNTIiLCJ0eXAiOiJCZWFyZXIiLCJhenAiOiJ1c2VyLXNlcnZpY2UiLCJzaWQiOiI1NmRjZDAxZi1mNjJiLTRkZmEtYWY3MS05NTdkNzNiZjdhYjgiLCJhY3IiOiIxIiwiYWxsb3dlZC1vcmlnaW5zIjpbImh0dHBzOi8vaG9iYnlodWIuYXVzdHJhbGlhY2VudHJhbC5jbG91ZGFwcC5henVyZS5jb20vKiJdLCJyZWFsbV9hY2Nlc3MiOnsicm9sZXMiOlsiZGVmYXVsdC1yb2xlcy1ob2JieWh1YiIsIm9mZmxpbmVfYWNjZXNzIiwidW1hX2F1dGhvcml6YXRpb24iXX0sInJlc291cmNlX2FjY2VzcyI6eyJhY2NvdW50Ijp7InJvbGVzIjpbIm1hbmFnZS1hY2NvdW50IiwibWFuYWdlLWFjY291bnQtbGlua3MiLCJ2aWV3LXByb2ZpbGUiXX19LCJzY29wZSI6InByb2ZpbGUgZW1haWwiLCJlbWFpbF92ZXJpZmllZCI6dHJ1ZSwibmFtZSI6InRlc3QgdGVzdCIsInByZWZlcnJlZF91c2VybmFtZSI6InBvc3RtYW4iLCJnaXZlbl9uYW1lIjoidGVzdCIsImZhbWlseV9uYW1lIjoidGVzdCIsImVtYWlsIjoicG9zdG1hbkBwb3N0bWFuLm5sIn0.OwdnatvORryLz7Y-ikh9ekpOgR4Kbz-HzonNbD6W6XSd0oOUYrUIE86cyNGKNnm0A3fCBg7A7q4ToLR1maXoGwGXOiutcT-mjYPIjevSq5yf5oz-MQec8e4MheFpvfGJOUY1cCxEszZQUNCzifmPyOUF7hmxPft9SowdhkcEHBvvECZ658Ye9dcaZUx5FrZcZPk9WMCCMatxY6zpPZV7fwXUC3n1vdn_B_OS9IZHdeRZgd4lyR_SQTlwg9_mUGkAD-EFRBl0O2Dez4BalOA79rGc82N0g0JBcb_i6lN61VLMPDYm4AQ1HA790ng96ZrpLXyfnMw3g8-ZYX-epwA_VQ";
            
                // Extract JWT from headers
                var authorizationHeader = HttpContext.Request.Headers["Authorization"].ToString();
                if (authorizationHeader.StartsWith("Bearer "))
                {
                    Console.WriteLine("INTEGRATION MODE: CHECKED -> STARTS WITH BEARER");
                    var jwt = authorizationHeader.Substring("Bearer ".Length).Trim();
                    Console.WriteLine("INTEGRATION MODE: CHECKED -> JWT TOKEN");
                    return jwt == expectedJwt; // Validate JWT value
                }

                return false; // No valid JWT provided
                
        }

        
        [Authorize]
        [HttpGet("profile")]
        public IActionResult GetProfile()
        {
            return Ok(new
            {
                UserName = User.Identity?.Name,
                Claims = User.Claims.Select(c => new { c.Type, c.Value })
            });
        }
        
        [Authorize]
        [HttpPut("{id}")]
        public ActionResult UpdateUser(int id, UserUpdateDTO userUpdateDTO)
        {
            var userFromRepo = _repository.GetUserById(id);

            if (userFromRepo == null)
            {
                return NotFound(new { Message = "User not found" });
            }

            // Map the updated fields
            _mapper.Map(userUpdateDTO, userFromRepo);

            // Save changes
            _repository.UpdateUser(userFromRepo); // If your repository has an explicit update method
            _repository.SaveChanges();

            return NoContent(); // HTTP 204 indicates a successful update with no content to return
        }

        [Authorize]
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteUser(int id)
        {
            var clientId = _configuration["Keycloak:ClientId"];
            var clientSecret = _configuration["Keycloak:ClientSecret"];
            var authority = _configuration["Keycloak:Authority"];

            var userFromRepo = _repository.GetUserById(id);

            Console.WriteLine($"Keycloak id = {userFromRepo.KeycloakId}");
            if (userFromRepo == null)
            {
                return NotFound(new { Message = "User not found" });
            }

            // Delete user from Keycloak
            try
            {
                var httpClient = new HttpClient();


                var content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "grant_type", "password" },
                    { "client_id", "admin-cli" },
                    { "username", "admin" },
                    { "password", "admin" },
                    { "client_secret", "FEYM3biU4v6gpBgQIApYKrW6fPugKgqC" }
                });


                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Post,
                    RequestUri = new Uri("https://keycloak-hobbyhub.australiacentral.cloudapp.azure.com/realms/master/protocol/openid-connect/token"),
                    Content = content
                };
                
                // content.Headers.Add("Content-Type", "application/x-www-form-urlencoded");
                
                var response = await httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine("Token Response: " + responseContent);
                }
                else
                {
                    return StatusCode(500);
                }

                var tokenContent = await response.Content.ReadAsStringAsync();
                var token = JsonSerializer.Deserialize<JsonElement>(tokenContent)
                    .GetProperty("access_token").GetString();

                Console.WriteLine($"Access Token: {token}");
                // Create the DELETE request
                var client = new HttpClient();
                
                var httprequest = new HttpRequestMessage
                {
                    Method = HttpMethod.Delete,
                    RequestUri =
                        new Uri(
                            "https://keycloak-hobbyhub.australiacentral.cloudapp.azure.com/admin/realms/HobbyHub/users/" + userFromRepo.KeycloakId),
                };

                // Set Authorization header
                httprequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                // httprequest.Headers.Add("Authorization", "Bearer " + clientId);

                try
                {
                    // Send the request
                    var message = await client.SendAsync(httprequest);
                    
                    if (message.IsSuccessStatusCode)
                    {
                        var userReadDto = _mapper.Map<UserReadDTO>(userFromRepo);
                        var userQueryDto = _mapper.Map<UserQueryPublishedDto>(userReadDto);
                        var userCommandDto = _mapper.Map<UserCommandPublishedDto>(userReadDto);
                        userQueryDto.Event = "User_Deleted";
                        userCommandDto.Event = "User_Deleted";
                        
                        _repository.DeleteUser(userFromRepo);
                        _repository.SaveChanges();
                        Console.WriteLine("User deleted successfully.");
                        //After we will send a message to the post service so it will remove all the posts made by that users.
                        try
                        {
                            //publish to the post query service
                            _messageBusClient.PublishQueryUserDeletion(userQueryDto, "user.query.topic", "user.topic.delete");
                            _messageBusClient.PublishCommandUserDeletion(userCommandDto, "user.command.topic", "user.topic.delete");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"--> Could not send async: {ex.Message}");
                        }
                        return Ok();
                    }

                    Console.WriteLine($"Error: {message.StatusCode}, {await message.Content.ReadAsStringAsync()}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Exception: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex.Message}");
            }

            // Delete user from your database
            return NoContent();
        }
        
        [Authorize]
        [HttpDelete("delete-account")]
        public async Task<ActionResult> DeleteUserByKeycloak(string keycloakId)
        {
            var clientId = _configuration["Keycloak:ClientId"];
            var clientSecret = _configuration["Keycloak:ClientSecret"];
            var authority = _configuration["Keycloak:Authority"];

            var userFromRepo = _repository.GetUserByKeycloakId(keycloakId);
            if (userFromRepo == null)
            {
                return NotFound(new { Message = "User not found" });
            }

            await DeleteUser(userFromRepo.Id);
            return Ok();
        }
    
        
        [Authorize]
        [HttpGet]
        public ActionResult<IEnumerable<UserReadDTO>> GetUsers()
        {
            Console.WriteLine("--> Getting users...");

            var userItem = _repository.GetAllUsers();

            return Ok(_mapper.Map<IEnumerable<UserReadDTO>>(userItem));
        }

        [Authorize]
        [HttpGet("{id}", Name = "GetUserById")]
        public ActionResult<UserReadDTO> GetUserById(int id)
        {
      
            var userItem = _repository.GetUserById(id);
            if(userItem != null)
            {
                return Ok(_mapper.Map<UserReadDTO>(userItem));
            
            }
            return NotFound();
        }
        
        [AllowAnonymous]
        [HttpGet("/test/{id}", Name = "GetTestUserById")]
        public ActionResult<UserReadDTO> GetTestUserById(int id)
        {
            if (!IntegrationMode)
            {
                return NotFound();
            }
                if (!IsValidJwt())
                {
                    return Unauthorized();
                }

                var userItem = _repository.GetUserById(id);
                if(userItem != null)
                {
                    return Ok(_mapper.Map<UserReadDTO>(userItem));
            
                }
                return NotFound();

        }
        
        [Authorize]
        [HttpGet("keycloak/{keyCloakId}", Name = "GetKeycloakUserById")]
        public ActionResult<UserReadDTO> GetKeycloakUserById(string keyCloakId)
        {
            

            var userItem = _repository.GetUserByKeycloakId(keyCloakId);
            if(userItem != null)
            {
                return Ok(_mapper.Map<UserReadDTO>(userItem));
            
            }
            return NotFound();
        }

        [Authorize]
        [HttpPost]
        public async Task<ActionResult<UserReadDTO>> CreateUser(UserCreateDTO userCreateDTO)
        {
            var userModel = _mapper.Map<User>(userCreateDTO);
            userModel.Created = DateTime.UtcNow;
            _repository.CreateUser(userModel);
            _repository.SaveChanges();

            var UserReadDTO = _mapper.Map<UserReadDTO>(userModel);
            
            //Send Sync Message
            // try
            // {
            //     await _hobbyDataClient.SendUsersToHobby(UserReadDTO);
            // }
            // catch (Exception ex)
            // {
            //     Console.WriteLine($"--> Could not send user to HobbyService: {ex.Message}");
            // }

            
            //Send Async Message
            // try
            // {
            //     var userPublishedDto = _mapper.Map<UserQueryPublishedDto>(UserReadDTO);
            //     userPublishedDto.Event = "User_Published";
            //     _messageBusClient.PublishUser(userPublishedDto, "user.topic", "user.add");
            // }
            // catch (Exception ex)
            // {
            //     Console.WriteLine($"--> Could not send async: {ex.Message}");
            // }

            return CreatedAtRoute(nameof(GetUserById), new {Id = UserReadDTO.Id}, UserReadDTO);
        }
        
        [HttpGet("public")]
        [Authorize]
        public IActionResult GetPublicInfo()
        {
            return Ok(new { Message = "This is a public endpoint!" });
        }
    }
}