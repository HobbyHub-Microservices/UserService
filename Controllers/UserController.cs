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
        }
        
        [HttpGet("profile")]
        public IActionResult GetProfile()
        {
            return Ok(new
            {
                UserName = User.Identity?.Name,
                Claims = User.Claims.Select(c => new { c.Type, c.Value })
            });
        }
        [AllowAnonymous]
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

        [AllowAnonymous]
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
                    { "client_secret", "jLGWqa9rna565mxfkDNd3T5dGcUPKlU8" }
                });


                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Post,
                    RequestUri = new Uri("http://hobbyhub.com:8080/realms/master/protocol/openid-connect/token"),
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
                            "http://hobbyhub.com:8080/admin/realms/HobbyHub/users/" + userFromRepo.KeycloakId),
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
        
        [AllowAnonymous]
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
    
        
        [AllowAnonymous]
        [HttpGet]
        public ActionResult<IEnumerable<UserReadDTO>> GetUsers()
        {
            Console.WriteLine("--> Getting users...");

            var userItem = _repository.GetAllUsers();

            return Ok(_mapper.Map<IEnumerable<UserReadDTO>>(userItem));
        }

        [AllowAnonymous]
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

        [AllowAnonymous]
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
        [AllowAnonymous]
        public IActionResult GetPublicInfo()
        {
            return Ok(new { Message = "This is a public endpoint!" });
        }
    }
}