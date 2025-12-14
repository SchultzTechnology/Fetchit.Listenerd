using System.Diagnostics;

namespace Fetchit.WebPage.Services;

public class SupervisorService
{
    private readonly ILogger<SupervisorService> _logger;

    public SupervisorService(ILogger<SupervisorService> logger)
    {
        _logger = logger;
    }

    public async Task<bool> RestartListenerdAsync()
    {
        try
        {
            // Check if running in Docker (supervisor is available)
            var isDocker = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true" 
                          || File.Exists("/.dockerenv");

            if (!isDocker)
            {
                _logger.LogWarning("Not running in Docker container, skipping supervisor restart");
                return false;
            }

            // Use supervisorctl to restart the listenerd service
            var processInfo = new ProcessStartInfo
            {
                FileName = "/usr/bin/supervisorctl",
                Arguments = "restart fetchit-listenerd",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process == null)
            {
                _logger.LogError("Failed to start supervisorctl process");
                return false;
            }

            await process.WaitForExitAsync();
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();

            if (process.ExitCode == 0)
            {
                _logger.LogInformation("Listenerd service restarted successfully: {Output}", output);
                return true;
            }
            else
            {
                _logger.LogError("Failed to restart listenerd service. Exit code: {ExitCode}, Error: {Error}", 
                    process.ExitCode, error);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restarting listenerd service");
            return false;
        }
    }
}
