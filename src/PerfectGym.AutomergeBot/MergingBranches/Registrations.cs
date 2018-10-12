using Microsoft.Extensions.DependencyInjection;

namespace PerfectGym.AutomergeBot.MergingBranches
{
    public static class Registrations
    {
        public static void ConfigureServices(IServiceCollection services)
        {
            services.AddTransient<PushHandler>();
            services.AddTransient<IProcessPushPredicate, ProcessPushPredicate>();
            services.AddTransient<IMergePerformer, MergePerformer>();
            services.AddTransient<IPullRequestMergeRetryier, PullRequestMergeRetryier>();
            services.AddTransient<IUserNotifier, UserNotifier>();

            services.AddTransient<PullRequestsGovernor>();

            var mergeDirectionsProviderInstance = new MergeDirectionsProvider();
            services.AddSingleton<IMergeDirectionsProviderConfigurator>(mergeDirectionsProviderInstance);
            services.AddSingleton<IMergeDirectionsProvider>(mergeDirectionsProviderInstance);
        }
    }
}
