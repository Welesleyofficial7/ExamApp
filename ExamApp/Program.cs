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


builder.Host.UseSerilog();



builder.Logging.ClearProviders();
builder.Logging.AddSerilog();

AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
builder.Services.AddGrpcClient<ClientService.ClientServiceClient>(options =>
{
    var domainServiceUrl = builder.Configuration["GrpcSettings:DomainServiceUrl"];
    if (string.IsNullOrEmpty(domainServiceUrl))
    {
        throw new ArgumentNullException(nameof(domainServiceUrl), "gRPC service URL is missing in configuration.");
    }

    AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

    options.Address = new Uri(domainServiceUrl);
});
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.EnableAnnotations(); 
});

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(8080, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
    });
});


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

//
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

var app = builder.Build();


    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1");
    });


app.UseHttpsRedirection();
app.UseHttpMetrics();
app.UseSerilogRequestLogging();
app.MapMetrics();
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
