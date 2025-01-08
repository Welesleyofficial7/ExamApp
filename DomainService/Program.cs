using EasyNetQ;
using ExamApp;
using ExamApp.Models;
using ExamApp.Repositories;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Prometheus;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Настройка MongoDB
var mongoConnectionString = "mongodb://mongo:27017";
var mongoDatabaseName = "customerdatabase";

builder.Services.AddSingleton<MongoDbContext>(provider =>
{
    return new MongoDbContext(mongoConnectionString, mongoDatabaseName);
});

builder.Services.AddScoped<IClientRepository, MongoClientRepository>();

// Настройка RabbitMQ
var rabbitConnectionString = "amqp://guest:guest@rabbitmq:5672";
builder.Services.AddSingleton<IBus>(_ => RabbitHutch.CreateBus(rabbitConnectionString));
builder.Services.AddSingleton<RabbitMqConsumer>();

// Включаем поддержку HTTP/2
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

// Настройка Kestrel
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(7014, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http2;
    });
    
    options.ListenAnyIP(7015, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http1; // Для метрик Prometheus
    });
});

// Настройка Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .CreateLogger();

builder.Logging.ClearProviders(); // Убираем дефолтных провайдеров
builder.Logging.AddSerilog();    // Добавляем Serilog
builder.Services.AddGrpc();

var app = builder.Build();

// Запуск RabbitMQ Consumer
var bus = app.Services.GetRequiredService<IBus>();
var rabbitConsumer = app.Services.GetRequiredService<RabbitMqConsumer>();
try
{
    var subscriberId = $"ExamApp@{Environment.MachineName}";

    // Повторные попытки подписки
    for (int i = 0; i < 5; i++)
    {
        try
        {
            await bus.PubSub.SubscribeAsync<CreateClientMessage>(subscriberId, message => HandleCreateClientMessage(message, app.Services));
            await bus.PubSub.SubscribeAsync<UpdateClientMessage>(subscriberId, message => HandleUpdateClientMessage(message, app.Services));
            await bus.PubSub.SubscribeAsync<DeleteClientMessage>(subscriberId, message => HandleDeleteClientMessage(message, app.Services));
            
            Log.Information("Successfully subscribed to RabbitMQ messages.");
            break;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Attempt {Attempt} to subscribe to RabbitMQ failed.", i + 1);
            await Task.Delay(TimeSpan.FromSeconds(5));
        }
    }
}
catch (Exception ex)
{
    Log.Error(ex, "Failed to subscribe to RabbitMQ messages.");
    throw;
}




// Завершаем работу RabbitMQ при остановке приложения
app.Lifetime.ApplicationStopping.Register(() =>
{
    Log.Information("Shutting down RabbitMQ...");
    bus.Dispose();
    Log.Information("RabbitMQ shut down.");
});

// Настройка gRPC
app.MapGrpcService<ClientServiceImpl>();

static async Task HandleCreateClientMessage(CreateClientMessage message, IServiceProvider services)
{
    using var scope = services.CreateScope();
    var repository = scope.ServiceProvider.GetRequiredService<IClientRepository>();

    var client = new Client
    {
        FirstName = message.FirstName,
        LastName = message.LastName,
        Address = message.Address,
        Phone = message.Phone
    };

    await repository.CreateClientAsync(client);
    Console.WriteLine($"Processed CreateClientMessage for {client.FirstName} {client.LastName}");
}

static async Task HandleUpdateClientMessage(UpdateClientMessage message, IServiceProvider services)
{
    using var scope = services.CreateScope();
    var repository = scope.ServiceProvider.GetRequiredService<IClientRepository>();

    var client = new Client
    {
        Id = message.Id,
        FirstName = message.FirstName,
        LastName = message.LastName,
        Address = message.Address,
        Phone = message.Phone
    };

    await repository.UpdateClientAsync(client);
    Console.WriteLine($"Processed UpdateClientMessage for ID: {client.Id}");
}

static async Task HandleDeleteClientMessage(DeleteClientMessage message, IServiceProvider services)
{
    using var scope = services.CreateScope();
    var repository = scope.ServiceProvider.GetRequiredService<IClientRepository>();

    await repository.DeleteClientAsync(message.Id);
    Console.WriteLine($"Processed DeleteClientMessage for ID: {message.Id}");
}


rabbitConsumer.StartConsuming(); // Регистрируем обработчики для сообщений

// Включаем HTTPS и запускаем приложение
app.UseHttpMetrics();  // Регистрирует метрики для HTTP-запросов
app.MapMetrics("/metrics"); // Регистрирует путь для метрик Prometheus
app.UseHttpsRedirection();
app.Run();