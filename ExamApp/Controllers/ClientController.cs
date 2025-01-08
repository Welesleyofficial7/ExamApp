using Serilog;

namespace ExamApp;

using Grpc.Net.Client;
using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;
using System.Text.Json;
using System.Threading.Tasks;

[ApiController]
[Route("api/client")]
public class ClientController : ControllerBase
{
    private readonly ClientService.ClientServiceClient _grpcClient;
    private readonly IDatabase _redisDatabase;

    public ClientController(IConfiguration configuration, IConnectionMultiplexer redis)
    {
        var grpcChannel = GrpcChannel.ForAddress("http://domainservice:7014", new GrpcChannelOptions
        {
            HttpHandler = new HttpClientHandler
            {
                UseProxy = false,
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            }
        });

        _grpcClient = new ClientService.ClientServiceClient(grpcChannel);

        _redisDatabase = redis.GetDatabase();
    }

    [HttpGet("all")]
    public async Task<IActionResult> GetAllClients()
    {
        try
        {
            Log.Information("Sending gRPC request GetAllClients to DomainService");
            var response = await _grpcClient.GetAllClientsAsync(new Empty());
            Log.Information("Received gRPC response: {@Response}", response);

            return Ok(new 
            {
                Source = "gRPC",
                Data = response.Clients
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occurred in GetAllClients");
            return StatusCode(500, new 
            { 
                Message = ex.Message, 
                StackTrace = ex.StackTrace 
            });
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetClient(string id)
    {
        try
        {
            var cachedClient = await _redisDatabase.StringGetAsync(id);
            if (cachedClient.HasValue)
            {
                Log.Information("Cache hit for client ID: {Id}", id);
                return Ok(new
                {
                    Source = "Cache",
                    Data = cachedClient.ToString()
                });
            }

            Log.Information("Cache miss for client ID: {Id}. Simulating delay...", id);
            await Task.Delay(2000);

            var grpcUrl = _grpcClient.GetType().GetProperty("Channel")?.GetValue(_grpcClient)?.ToString();
            Log.Information("Sending gRPC request to DomainService: {Url}", grpcUrl);

            var request = new GetClientRequest { Id = id };
            var response = await _grpcClient.GetClientAsync(request);
            Log.Information("Received gRPC response: {@Response}", response);

            await _redisDatabase.StringSetAsync(
                id,
                JsonSerializer.Serialize(response),
                TimeSpan.FromMinutes(10)
            );

            return Ok(new
            {
                Source = "gRPC",
                Data = response
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occurred in GetClient");
            return StatusCode(500, new { Message = ex.Message, StackTrace = ex.StackTrace });
        }
    }

    [HttpPost("create")]
    public async Task<IActionResult> CreateClient([FromBody] CreateClientRequest request)
    {
        try
        {
            await _grpcClient.CreateClientAsync(request);
            return Ok(new { Message = "Client created successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = ex.Message });
        }
    }

    [HttpPut("update/{id}")]
    public async Task<IActionResult> UpdateClient(string id, [FromBody] UpdateClientRequest request)
    {
        try
        {
            request.Id = id;
            await _grpcClient.UpdateClientAsync(request);

            await _redisDatabase.KeyDeleteAsync(id);

            return Ok(new { Message = "Client updated successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = ex.Message });
        }
    }

    [HttpDelete("delete/{id}")]
    public async Task<IActionResult> DeleteClient(string id)
    {
        try
        {
            var request = new DeleteClientRequest { Id = id };
            await _grpcClient.DeleteClientAsync(request);

            await _redisDatabase.KeyDeleteAsync(id);

            return Ok(new { Message = "Client deleted successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = ex.Message });
        }
    }
}
