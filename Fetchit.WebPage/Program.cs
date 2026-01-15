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

var dataPath = Directory.Exists("/app/data") ? "/app/data" : "./data";
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
    
    try
    {
        // Ensure database is deleted and recreated to apply current schema
        dbContext.Database.EnsureDeleted();
        dbContext.Database.EnsureCreated();
        
        // Add default admin user
        dbContext.Users.Add(new User
        {
            Username = "admin",
            PasswordHash = HashPassword("Welcome123!")
        });
        dbContext.SaveChanges();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while initializing the database.");
        throw;
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
