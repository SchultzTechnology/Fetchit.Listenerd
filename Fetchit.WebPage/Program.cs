using Microsoft.EntityFrameworkCore;
using Fetchit.WebPage.Data;
using Fetchit.WebPage.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Fetchit.WebPage.Models;
using System.Security.Cryptography;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
    });

builder.Services.AddAuthorization();

var dataPath = Directory.Exists("/app/data") ? "/app/data" : "../data";
Directory.CreateDirectory(dataPath);

var dbPath = Path.Combine(dataPath, "mqttconfig.db");
builder.Services.AddDbContext<MqttConfigContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

builder.Services.AddScoped<MqttConfigService>();
builder.Services.AddSingleton<SupervisorService>();
builder.Services.AddSingleton<ConnectionSecretService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<MqttConfigContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    var maxRetries = 5;
    var retryDelay = TimeSpan.FromSeconds(2);

    for (int retry = 0; retry < maxRetries; retry++)
    {
        try
        {
            // Ensure database exists and apply any pending migrations
            dbContext.Database.EnsureCreated();

            // Add default admin user only if no users exist
            if (!dbContext.Users.Any())
            {
                logger.LogInformation("No users found. Creating default admin user.");
                dbContext.Users.Add(new User
                {
                    Username = "admin",
                    PasswordHash = HashPassword("admin!")
                });
                dbContext.SaveChanges();
                logger.LogInformation("Default admin user created successfully.");
            }

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
            logger.LogError(ex, "An error occurred while initializing the database after {MaxRetries} attempts.", maxRetries);
            throw;
        }
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapStaticAssets();
app.MapControllerRoute(
        name: "default",
        pattern: "{controller=MqttConfig}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

static string HashPassword(string password)
{
    using var sha = SHA256.Create();
    return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(password)));
}
