using AutoMapper;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using UserService.AsyncDataServices;
using UserService.Data;
using UserService.DTOs;
using UserService.Models;
using UserService.SyncDataServices.Http;

namespace UserService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController  : ControllerBase
    {
        private readonly IUserRepo _repository;
        private readonly IMapper _mapper;
        private readonly IHobbyDataClient _hobbyDataClient;
        private readonly IMessageBusClient _messageBusClient;

        public UsersController(
            IUserRepo repository, 
            IMapper mapper,
            IHobbyDataClient hobbyDataClient,
            IMessageBusClient messageBusClient)
        {
            //This is all registered in the Program.cs inside a builder.services...
            _repository = repository;
            _mapper = mapper;
            _hobbyDataClient = hobbyDataClient;
            _messageBusClient = messageBusClient;
        }
        
        [HttpGet]
        public ActionResult<IEnumerable<UserReadDTO>> GetUsers()
        {
            Console.WriteLine("--> Getting users...");

            var userItem = _repository.GetAllUsers();

            return Ok(_mapper.Map<IEnumerable<UserReadDTO>>(userItem));
        }

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

        [HttpPost]
        public async Task<ActionResult<UserReadDTO>> CreateUser(UserCreateDTO userCreateDTO)
        {
            var userModel = _mapper.Map<User>(userCreateDTO);
            _repository.CreateUser(userModel);
            _repository.SaveChanges();

            var UserReadDTO = _mapper.Map<UserReadDTO>(userModel);
            
            //Send Sync Message
            try
            {
                await _hobbyDataClient.SendUsersToHobby(UserReadDTO);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"--> Could not send user to HobbyService: {ex.Message}");
            }
            
            //Send Async Message
            try
            {
                var userPublishedDto = _mapper.Map<UserPublishedDTO>(UserReadDTO);
                userPublishedDto.Event = "User_Published";
                _messageBusClient.PublishNewUser(userPublishedDto);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"--> Could not send async: {ex.Message}");
            }

            return CreatedAtRoute(nameof(GetUserById), new {Id = UserReadDTO.Id}, UserReadDTO);
        }
    }
}