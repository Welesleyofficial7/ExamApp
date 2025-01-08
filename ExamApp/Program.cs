using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Prometheus;
using Serilog;
using StackExchange.Redis;
using ExamApp;
using System;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Serilog.Sinks.Network;

var builder = WebApplication.CreateBuilder(args);

// Добавление конфигурации
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

var logstashUrl = builder.Configuration["Logstash:Url"];
var logstashEnabled = builder.Configuration.GetValue<bool>("Logstash:Enabled");

var loggerConfig = new LoggerConfiguration()
    .WriteTo.Console();

if (logstashEnabled)
{
    loggerConfig.WriteTo.Http(
        logstashUrl,
        queueLimitBytes: null,
        textFormatter: new Serilog.Formatting.Json.JsonFormatter()
    );
}

Log.Logger = loggerConfig.CreateLogger();


// Настраиваем Serilog как логгер для приложения
builder.Host.UseSerilog();



builder.Logging.ClearProviders();
builder.Logging.AddSerilog();

AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
// Регистрация gRPC клиента
builder.Services.AddGrpcClient<ClientService.ClientServiceClient>(options =>
{
    var domainServiceUrl = builder.Configuration["GrpcSettings:DomainServiceUrl"];
    if (string.IsNullOrEmpty(domainServiceUrl))
    {
        throw new ArgumentNullException(nameof(domainServiceUrl), "gRPC service URL is missing in configuration.");
    }

    // Включаем поддержку HTTP/2 без шифрования (для локального тестирования)
    AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

    options.Address = new Uri(domainServiceUrl);
});
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

// Добавление контроллеров
builder.Services.AddControllers(); // Если вам нужно работать с JSON через Newtonsoft

// Добавление Swagger/OpenAPI (опционально)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.EnableAnnotations(); 
});

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(8080, listenOptions =>
    {
        // Разрешаем одновременно HTTP/1.1 и HTTP/2
        listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
    });
});


// Подключение Redis
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    // var redisConnectionString = builder.Configuration["Redis:ConnectionString"];
    // if (string.IsNullOrEmpty(redisConnectionString))
    // {
    //     Log.Error("Redis connection string is null or empty.");
    //     throw new ArgumentNullException("Redis:ConnectionString", $"Redis connection string is missing in configuration. {redisConnectionString}");
    // }
    Log.Information("Redis connection string: {RedisConnectionString}", "redis:6379");
    return ConnectionMultiplexer.Connect("redis:6379");
});

// // Проверка подключения к Redis
// try
// {
//     var redis = ConnectionMultiplexer.Connect(builder.Configuration["Redis:ConnectionString"]);
//     Log.Information("Connected to Redis successfully.");
// }
// catch (Exception ex)
// {
//     Log.Error(ex, "Failed to connect to Redis.");
//     throw;
// }

// Настройка приложения
var app = builder.Build();


    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1");
    });


// Использование HTTPS
app.UseHttpsRedirection();
app.UseHttpMetrics();
app.UseSerilogRequestLogging();
app.MapMetrics();
// Настройка маршрутов
app.UseAuthorization();
app.MapControllers();

try
{
    Log.Information("Starting the application...");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "The application failed to start.");
}
finally
{
    Log.CloseAndFlush();
}
