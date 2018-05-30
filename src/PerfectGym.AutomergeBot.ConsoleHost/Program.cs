using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;

namespace PerfectGym.AutomergeBot.ConsoleHost
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Logging.EnsureLoggingInitialized(null);
            StartServer(args);
        }

        private static void StartServer(string[] args)
        {
            BuildWebHost(args).Run();
        }

        public static IWebHost BuildWebHost(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseStartup<Startup>()
                .Build();
    }
}