using System;
using System.IO;
using System.Threading.Tasks;
using Fetchit.Cli.Commands;
using Fetchit.Listenerd.Data;
using Microsoft.EntityFrameworkCore;

namespace Fetchit.Cli
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            Console.WriteLine("==========================================");
            Console.WriteLine("  Fetchit Headless Setup CLI");
            Console.WriteLine("==========================================");
            Console.WriteLine();

            var dbPath = GetArg(args, "--db");
            var resolvedDbPath = string.Empty;

            if (dbPath == null)
            {
                var autoDataPath = Directory.Exists("/app/data") ? "/app/data" : "../data";
                Directory.CreateDirectory(autoDataPath);
                resolvedDbPath = Path.Combine(autoDataPath, "mqttconfig.db");
            }
            else
            {
                resolvedDbPath = dbPath;
                var dbDir = Path.GetDirectoryName(resolvedDbPath);
                if (!string.IsNullOrEmpty(dbDir) && !Directory.Exists(dbDir))
                    Directory.CreateDirectory(dbDir);
            }

            Console.WriteLine($"Database path: {resolvedDbPath}");
            Console.WriteLine();

            var optionsBuilder = new DbContextOptionsBuilder<MqttConfigContext>();
            optionsBuilder.UseSqlite($"Data Source={resolvedDbPath}");
            using var context = new MqttConfigContext(optionsBuilder.Options);

            if (args.Contains("--show"))
                return await new ShowCommand(context).RunAsync();

            if (args.Contains("--delete"))
                return await new DeleteCommand(context).RunAsync();

            return await new SaveCommand(context, args).RunAsync();
        }

        static string? GetArg(string[] args, string key)
        {
            var index = Array.IndexOf(args, key);
            if (index >= 0 && index < args.Length - 1)
                return args[index + 1];
            return null;
        }
    }
}
