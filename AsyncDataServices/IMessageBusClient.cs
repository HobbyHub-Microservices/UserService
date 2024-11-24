using UserService.DTOs;

namespace UserService.AsyncDataServices;

public interface IMessageBusClient
{
    void PublishNewUser(UserPublishedDTO userPublishedDto);
}