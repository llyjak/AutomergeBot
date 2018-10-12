using Microsoft.Extensions.DependencyInjection;

namespace PerfectGym.AutomergeBot.TempBranchesRemoving
{
    public static class Registrations
    {
        public static void ConfigureServices(IServiceCollection services)
        {
            services.AddTransient<ITempBranchesRemoverPullRequestHandler, TempBranchesRemoverPullRequestHandlerPullRequestHandler>();
        }
    }
}
