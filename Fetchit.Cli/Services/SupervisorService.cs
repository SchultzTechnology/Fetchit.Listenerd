using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Fetchit.Cli.Services
{
    public class SupervisorService
    {
        public async Task<bool> RestartListenerdAsync()
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName               = "/usr/bin/supervisorctl",
                    Arguments              = "restart fetchit-listenerd",
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true
                };

                using var process = Process.Start(processInfo);
                if (process == null)
                {
                    Console.WriteLine("Warning: Could not start supervisorctl process.");
                    return false;
                }

                await process.WaitForExitAsync();
                var output = await process.StandardOutput.ReadToEndAsync();
                var error  = await process.StandardError.ReadToEndAsync();

                if (process.ExitCode == 0)
                {
                    Console.WriteLine($"Listenerd service restarted successfully. {output}".Trim());
                    return true;
                }
                else
                {
                    Console.WriteLine($"Warning: Service restart failed (exit code {process.ExitCode}): {error}".Trim());
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Note: Could not auto-restart service ({ex.Message}).");
                Console.WriteLine("Please restart the fetchit-listenerd service manually.");
                return false;
            }
        }
    }
}
