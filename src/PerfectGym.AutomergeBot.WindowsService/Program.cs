using System.Diagnostics;
using System.IO;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.WindowsServices;

namespace PerfectGym.AutomergeBot.WindowsService
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Logging.EnsureLoggingInitialized(GetPathToContentRoot());
            StartService(args);
        }

        private static void StartService(string[] args)
        {
            BuildWebHost(args).RunAsService();
        }

        public static IWebHost BuildWebHost(string[] args)
        {
            var pathToContentRoot = GetPathToContentRoot();

            return WebHost.CreateDefaultBuilder(args)
                .UseUrls("http://*:7654")
                .UseStartup<Startup>()
                .UseContentRoot(pathToContentRoot)
                .Build();
        }

        private static string GetPathToContentRoot()
        {
            var pathToExe = Process.GetCurrentProcess().MainModule.FileName;
            var pathToContentRoot = Path.GetDirectoryName(pathToExe);
            return pathToContentRoot;
        }
    }
}
