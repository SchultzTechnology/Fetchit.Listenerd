using Fetchit.Listenerd;
using Fetchit.Listenerd.Options;
using Fetchit.Listenerd.Service;
using Fetchit.Listenerd.Data;
using Microsoft.EntityFrameworkCore;
using static MQTTClient;

var builder = Host.CreateApplicationBuilder(args);

// Configure settings
builder.Services.Configure<PacketCaptureSettings>(
    builder.Configuration.GetSection("PacketCaptureSettings"));

// Configure SQLite database - use shared database with WebPage
// Use /app/data in Docker, ./data locally
var dataPath = Directory.Exists("/app/data") ? "/app/data" : "../data";
Directory.CreateDirectory(dataPath); // Ensure directory exists

var dbPath = Path.Combine(dataPath, "mqttconfig.db");
builder.Services.AddDbContext<MqttConfigContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

// Register services
builder.Services.AddSingleton<MqttConfigService>();
builder.Services.AddSingleton<MQTTClient>();
builder.Services.AddSingleton<PacketCaptureService>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();

// Ensure database is created with retry logic (both listenerd and webpage try to init simultaneously)
using (var scope = host.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<MqttConfigContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    var maxRetries = 5;
    var retryDelay = TimeSpan.FromSeconds(2);

    for (int retry = 0; retry < maxRetries; retry++)
    {
        try
        {
            dbContext.Database.EnsureCreated();
            logger.LogInformation("Database initialized successfully.");
            break;
        }
        catch (Exception ex) when (retry < maxRetries - 1)
        {
            logger.LogWarning(ex, "Database initialization attempt {Retry} failed. Retrying in {Delay} seconds...", retry + 1, retryDelay.TotalSeconds);
            Thread.Sleep(retryDelay);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database initialization failed after {MaxRetries} attempts.", maxRetries);
            throw;
        }
    }
}

host.Run();
