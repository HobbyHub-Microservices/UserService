using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using UserService.Data;
using UserService.DTOs;
using UserService.Models;

namespace UserService.AsyncDataServices;

public class MessageBusClient : IMessageBusClient
{
    private readonly IConfiguration _config;
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private string _queueName;
    private readonly IServiceProvider _services;

    public MessageBusClient(IConfiguration config, IServiceProvider services)
    {
        _services = services;
        _config = config;
        Console.WriteLine($"RabbitMQHost = {_config["RabbitMQHost"]}");
        Console.WriteLine($"RabbitMQPort = {_config["RabbitMQPort"]}");
        Console.WriteLine($"RabbitMQUsername = {_config["RabbitMQUsername"]}");
        Console.WriteLine($"RabbitMQPassword = {_config["RabbitMQPassword"]}");
        
        var factory = new ConnectionFactory(){ 
            HostName = _config["RabbitMQHost"], 
            Port = int.Parse(_config["RabbitMQPort"] ?? string.Empty),
            ClientProvidedName = "UserService",
            UserName = _config["RabbitMQUsername"],
            Password = _config["RabbitMQPassword"]
        }; //this needs to be the same as the appsettings.json file settings
        try
        {
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
            _channel.ExchangeDeclare(exchange: "user.topic", type: ExchangeType.Topic, durable: false);
            _channel.ExchangeDeclare(exchange: "user.command.topic", type: ExchangeType.Topic, durable: true);
            _channel.ExchangeDeclare(exchange: "user.query.topic", type: ExchangeType.Topic, durable: true);
            _channel.ExchangeDeclare(exchange: "amq.topic", type: ExchangeType.Topic, durable: true);
           
            
            
            _connection.ConnectionShutdown += RabbitMq_ConnectionShutDown;
            Console.WriteLine("--> Connected to RabbitMQ");
        }
        catch (Exception exception)
        {
            Console.WriteLine($"--> Could not connect to message bus: {exception.Message}");
        }
    }
    public void PublishQueryUserDeletion(UserQueryPublishedDto userQueryPublishedDto, string exchange, string routingKey)
    {
        var message = JsonSerializer.Serialize(userQueryPublishedDto);

        if (_connection.IsOpen)
        {
            Console.WriteLine($"--> Sending message to RabbitMQ: {message}");
            SendMessage(message, exchange, routingKey);
        }
        else
        {
            Console.WriteLine($"--> RabbitMQ is closed, not able to send message");
        }
    }
    
    public void PublishCommandUserDeletion(UserCommandPublishedDto userQueryPublishedDto, string exchange, string routingKey)
    {
        var message = JsonSerializer.Serialize(userQueryPublishedDto);

        if (_connection.IsOpen)
        {
            Console.WriteLine($"--> Sending message to RabbitMQ: {message}");
            SendMessage(message, exchange, routingKey);
        }
        else
        {
            Console.WriteLine($"--> RabbitMQ is closed, not able to send message");
        }
    }
    
   

    private void SendMessage(string message, string exchange, string routingKey)
    {
        var body = Encoding.UTF8.GetBytes(message);
        _channel.BasicPublish(
            exchange: exchange,
            routingKey: routingKey,
            basicProperties: null,
            body: Encoding.UTF8.GetBytes(message));
        Console.WriteLine($"--> Sent message to RabbitMQ: {message}");
    }
    
    
    
    public void StartListening(string routingKey)
    {
       
        // Declare a queue and bind it to the topic exchange
        _queueName = _channel.QueueDeclare().QueueName;
        _channel.QueueBind(queue: _queueName, exchange: "amq.topic", routingKey: routingKey);
        Console.WriteLine($"--> Listening for RabbitMQ events on {routingKey}");
        
        var consumer = new EventingBasicConsumer(_channel);

        consumer.Received += (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            Console.WriteLine($"--> Received message: {message}");

            // Process the message
            HandleMessage(ea.RoutingKey, message);
        };

        _channel.BasicConsume(queue: _queueName, autoAck: true, consumer: consumer);
        Console.WriteLine($"--> Listening for messages with routing key: {routingKey}");
    }

    private void HandleMessage(string routingKey, string message)
    {
        // Implement your message handling logic here
        try
        {
            // Deserialize the message
            var eventMessage = JsonSerializer.Deserialize<EventMessage>(message);

            if (eventMessage == null)
            {
                Console.WriteLine("Unable to deserialize message");
                return;
            }
            Console.WriteLine($"--> Received message: {message}");

            // Check if the type matches REGISTER

                if (eventMessage.Type == "REGISTER")
                {
                    Console.WriteLine($"New User: {eventMessage.Details.Username}");
                    Console.WriteLine($"New EMAIL: {eventMessage.Details.Email}");
                    Console.WriteLine($"New ID: {eventMessage.UserId}");
                    User user = new User
                    {
                        KeycloakId = eventMessage.UserId,
                        Created = DateTime.UtcNow,
                        Name = eventMessage.Details.Username
                    };
                    Task.Run(() =>
                    {
                        using (var scope = _services.CreateScope())
                        {
                            var processor = scope.ServiceProvider.GetRequiredService<IUserRepo>();
                            processor.CreateUser(user);
                            processor.SaveChanges();
                        }
                    });
                }

                if (eventMessage.Type == "UPDATE_PROFILE")
                {
                    
                    Console.WriteLine($"Received PROFILE event for user: {eventMessage.Details.Context}");
                }
                else
                {
                    Console.WriteLine($"Received event of type {eventMessage}");
                }

        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing message: {ex.Message}");
        }
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
