using UserService.DTOs;

namespace UserService.AsyncDataServices;

public interface IMessageBusClient
{
    void PublishCommandUserDeletion(UserCommandPublishedDto userQueryPublishedDto, string exchange, string routingKey);
    void PublishQueryUserDeletion(UserQueryPublishedDto userQueryPublishedDto, string exchange, string routingKey);
}