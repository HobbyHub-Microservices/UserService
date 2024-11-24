using UserService.DTOs;
namespace UserService.SyncDataServices.Http;

public interface IHobbyDataClient
{
    Task SendUsersToHobby(UserReadDTO userReadDto);
}