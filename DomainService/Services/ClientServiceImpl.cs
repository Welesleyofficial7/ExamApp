using Grpc.Core;
using EasyNetQ;
using ExamApp.Models;
using ExamApp.Repositories;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Threading.Tasks;

namespace ExamApp;

public class ClientServiceImpl : ClientService.ClientServiceBase
{
    private readonly IBus _bus;
    private readonly IClientRepository _repository;
    private readonly ILogger<ClientServiceImpl> _logger;

    public ClientServiceImpl(IBus bus, IClientRepository repository, ILogger<ClientServiceImpl> logger)
    {
        _bus = bus;
        _repository = repository;
        _logger = logger;
    }

    public override async Task<GetAllClientsResponse> GetAllClients(Empty request, ServerCallContext context)
    {
        _logger.LogInformation("Received GetAllClients request.");

        var clients = await _repository.GetAllAsync();
        
        var response = new GetAllClientsResponse();
        response.Clients.AddRange(
            clients.Select(c => new GetClientResponse
            {
                Id = c.Id,
                FirstName = c.FirstName,
                LastName = c.LastName,
                Address = c.Address,
                Phone = c.Phone
            })
        );

        _logger.LogInformation("Returning {Count} clients.", clients);
        return response;
    }

    public override async Task<GetClientResponse> GetClient(GetClientRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Received GetClient request for ID: {Id}", request.Id);

        var client = await _repository.GetClientByIdAsync(request.Id);
        if (client == null)
        {
            _logger.LogWarning("Client with ID {Id} not found.", request.Id);
            throw new RpcException(new Status(StatusCode.NotFound, $"Client with ID {request.Id} not found"));
        }

        _logger.LogInformation("Returning client with ID: {Id}", client.Id);
        return new GetClientResponse
        {
            Id = client.Id,
            FirstName = client.FirstName,
            LastName = client.LastName,
            Address = client.Address,
            Phone = client.Phone
        };
    }

    public override async Task<Empty> CreateClient(CreateClientRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Received CreateClient request for {FirstName} {LastName}", request.FirstName, request.LastName);

        var message = new CreateClientMessage
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            Address = request.Address,
            Phone = request.Phone
        };

        await _bus.PubSub.PublishAsync(message);
        _logger.LogInformation("Published CreateClientMessage for {FirstName} {LastName}", request.FirstName, request.LastName);

        return new Empty();
    }

    public override async Task<Empty> UpdateClient(UpdateClientRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Received UpdateClient request for ID: {Id}", request.Id);

        var message = new UpdateClientMessage
        {
            Id = request.Id,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Address = request.Address,
            Phone = request.Phone
        };

        await _bus.PubSub.PublishAsync(message);
        _logger.LogInformation("Published UpdateClientMessage for ID: {Id}", request.Id);

        return new Empty();
    }

    public override async Task<Empty> DeleteClient(DeleteClientRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Received DeleteClient request for ID: {Id}", request.Id);

        var message = new DeleteClientMessage { Id = request.Id };
        await _bus.PubSub.PublishAsync(message);

        _logger.LogInformation("Published DeleteClientMessage for ID: {Id}", request.Id);
        return new Empty();
    }
}
