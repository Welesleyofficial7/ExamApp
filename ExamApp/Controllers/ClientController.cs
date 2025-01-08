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
        // Создаём gRPC-канал и клиента
        var grpcChannel = GrpcChannel.ForAddress("http://domainservice:7014", new GrpcChannelOptions
        {
            HttpHandler = new HttpClientHandler
            {
                UseProxy = false,
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            }
        });

        _grpcClient = new ClientService.ClientServiceClient(grpcChannel);

        // Подключаемся к Redis
        _redisDatabase = redis.GetDatabase();
    }
    
    /// <summary>
    /// Получить список всех клиентов.
    /// </summary>
    [HttpGet("all")]
    public async Task<IActionResult> GetAllClients()
    {
        try
        {
            // Вызов gRPC-метода, который не принимает аргументов, а возвращает список клиентов
            Log.Information("Sending gRPC request GetAllClients to DomainService");
            var response = await _grpcClient.GetAllClientsAsync(new Empty());
            Log.Information("Received gRPC response: {@Response}", response);

            // Если хотите кешировать список в Redis, можно сделать так же, как в GetClient
            // (но это опционально)
            // await _redisDatabase.StringSetAsync("all_clients", JsonSerializer.Serialize(response), TimeSpan.FromMinutes(10));

            return Ok(new 
            {
                Source = "gRPC",
                Data = response.Clients // список клиентов
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

    /// <summary>
    /// Получить информацию о клиенте по ID.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetClient(string id)
    {
        try
        {
            // Проверяем наличие данных в кеше Redis
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

            // Симулируем задержку для некешированных данных
            Log.Information("Cache miss for client ID: {Id}. Simulating delay...", id);
            await Task.Delay(2000); // 2-секундная задержка

            // Выполняем запрос к gRPC
            var grpcUrl = _grpcClient.GetType().GetProperty("Channel")?.GetValue(_grpcClient)?.ToString();
            Log.Information("Sending gRPC request to DomainService: {Url}", grpcUrl);

            var request = new GetClientRequest { Id = id };
            var response = await _grpcClient.GetClientAsync(request);
            Log.Information("Received gRPC response: {@Response}", response);

            // Сохраняем данные в кэше Redis с TTL (например, 10 минут)
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
            // Логирование ошибки
            Log.Error(ex, "An error occurred in GetClient");
            return StatusCode(500, new { Message = ex.Message, StackTrace = ex.StackTrace });
        }
    }

    /// <summary>
    /// Создать нового клиента.
    /// </summary>
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

    /// <summary>
    /// Обновить данные клиента по ID.
    /// </summary>
    [HttpPut("update/{id}")]
    public async Task<IActionResult> UpdateClient(string id, [FromBody] UpdateClientRequest request)
    {
        try
        {
            request.Id = id;
            await _grpcClient.UpdateClientAsync(request);

            // Удаляем кэш после обновления
            await _redisDatabase.KeyDeleteAsync(id);

            return Ok(new { Message = "Client updated successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = ex.Message });
        }
    }

    /// <summary>
    /// Удалить клиента по ID.
    /// </summary>
    [HttpDelete("delete/{id}")]
    public async Task<IActionResult> DeleteClient(string id)
    {
        try
        {
            var request = new DeleteClientRequest { Id = id };
            await _grpcClient.DeleteClientAsync(request);

            // Удаляем кэш после удаления
            await _redisDatabase.KeyDeleteAsync(id);

            return Ok(new { Message = "Client deleted successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = ex.Message });
        }
    }
}
