using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using UserService.DTOs;

namespace UserService.AsyncDataServices;

public class MessageBusClient : IMessageBusClient
{
    private readonly IConfiguration _config;
    private readonly IConnection _connection;
    private readonly IModel _channel;

    public MessageBusClient(IConfiguration config)
    {
        _config = config;
        var factory = new ConnectionFactory(){ 
            HostName = _config["RabbitMQHost"], 
            Port = int.Parse(_config["RabbitMQPort"] ?? string.Empty),
            ClientProvidedName = "UserService"
        }; //this needs to be the same as the appsettings.json file settings
        try
        {
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
            
            _channel.ExchangeDeclare(exchange: "user.topic", type: ExchangeType.Topic, durable: false);

            _connection.ConnectionShutdown += RabbitMq_ConnectionShutDown;
            Console.WriteLine("--> Connected to RabbitMQ");
        }
        catch (Exception exception)
        {
            Console.WriteLine($"--> Could not connect to message bus: {exception.Message}");
        }
    }
    public void PublishNewUser(UserPublishedDTO userPublishedDto)
    {
        var message = JsonSerializer.Serialize(userPublishedDto);

        if (_connection.IsOpen)
        {
            Console.WriteLine($"--> Sending message to RabbitMQ: {message}");
            SendMessage(message);
        }
        else
        {
            Console.WriteLine($"--> RabbitMQ is closed, not able to send message");
        }
    }

    private void SendMessage(string message)
    {
        var body = Encoding.UTF8.GetBytes(message);
        _channel.BasicPublish(
            exchange: "user.topic",
            routingKey: "user.add",
            basicProperties: null,
            body: Encoding.UTF8.GetBytes(message));
        Console.WriteLine($"--> Sent message to RabbitMQ: {message}");
    }

    private void Dispose()
    {
        Console.WriteLine("--> Disposing of RabbitMQ");
        if (_channel.IsOpen)
        {
            _channel.Close();
            _connection.Close();
        }
    }

    private static void RabbitMq_ConnectionShutDown(object? sender, ShutdownEventArgs e)
    {
        Console.WriteLine("--> RabbitMQ Connection Shutdown");
    }
}