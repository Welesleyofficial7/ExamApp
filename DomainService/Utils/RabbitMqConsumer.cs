using EasyNetQ;
using ExamApp.Models;
using ExamApp.Repositories;


namespace ExamApp;

public class RabbitMqConsumer
{
    private readonly IBus _bus;
    private readonly IClientRepository _repository;
    private readonly ILogger<RabbitMqConsumer> _logger;

    public RabbitMqConsumer(IBus bus, IClientRepository repository, ILogger<RabbitMqConsumer> logger)
    {
        _bus = bus;
        _repository = repository;
        _logger = logger;
    }

    public void StartConsuming()
    {
        _bus.PubSub.SubscribeAsync<CreateClientMessage>("client.create", async message =>
        {
            _logger.LogInformation("SubscribeAsync for CreateClientMessage called.");
            var client = new Client
            {
                FirstName = message.FirstName,
                LastName = message.LastName,
                Address = message.Address,
                Phone = message.Phone
            };
            _logger.LogInformation($"Client created for {client.FirstName} {client.LastName}");

            await _repository.CreateClientAsync(client);
        });

        _bus.PubSub.SubscribeAsync<UpdateClientMessage>("client.update", async message =>
        {
            var client = new Client
            {
                Id = message.Id,
                FirstName = message.FirstName,
                LastName = message.LastName,
                Address = message.Address,
                Phone = message.Phone
            };
            await _repository.UpdateClientAsync(client);
        });

        _bus.PubSub.SubscribeAsync<DeleteClientMessage>("client.delete", async message =>
        {
            await _repository.DeleteClientAsync(message.Id);
        });
    }
    
    public async Task HandleCreateClientMessageAsync(CreateClientMessage message)
    {
        _logger.LogInformation("Handling CreateClientMessage: {Message}", message);
        var client = new Client
        {
            FirstName = message.FirstName,
            LastName = message.LastName,
            Address = message.Address,
            Phone = message.Phone
        };
        await _repository.CreateClientAsync(client);
        _logger.LogInformation("Client created successfully in the database.");
    }

}