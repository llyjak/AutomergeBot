using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PerfectGym.AutomergeBot.RepositoryConnection;
using PerfectGym.AutomergeBot.SlackClient;
using Serilog;

namespace PerfectGym.AutomergeBot
{
    public class Startup : StartupBase
    {
        private readonly IConfiguration _configuration;

        public Startup(IHostingEnvironment hostingEnvironment, IConfiguration configuration)
        {
            _configuration = configuration;
            Logging.EnsureLoggingInitialized(hostingEnvironment.ContentRootPath);
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public override void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging(loggingBuilder => loggingBuilder
                .ClearProviders()
                .SetMinimumLevel(LogLevel.Trace)
                .AddSerilog(dispose: true));
            services.Configure<AutomergeBotConfiguration>(_configuration);

            services.AddTransient<IRepositoryConnectionProvider, RepositoryConnectionProvider>();
            services.AddTransient<IGitHubEventHttpRequestHandler, GitHubEventHttpRequestHandler>();
            services.AddTransient<SlackClientProvider>();

            MergingBranches.Registrations.ConfigureServices(services);
            TempBranchesRemoving.Registrations.ConfigureServices(services);
        }

        public override void Configure(IApplicationBuilder app)
        {
            var logger = app.ApplicationServices.GetRequiredService<ILogger<Startup>>();
            logger.LogInformation("Starting...");

            var env = app.ApplicationServices.GetService<IHostingEnvironment>();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            UpdateMergeDirectionsProviderConfiguration(app.ApplicationServices);

            RegisterConfigurationChangedHandler(app);
            LogConfigurationUsed(app.ApplicationServices, logger);

            StartPullRequestsGovernor(app);
            app.Run(HandleRequest);
            logger.LogInformation("Started");
        }

        private static void StartPullRequestsGovernor(IApplicationBuilder app)
        {
            var pullRequestGovernor = app.ApplicationServices.GetRequiredService<MergingBranches.PullRequestsGovernor>();
            pullRequestGovernor.StartNewWorker();
        }

        private void UpdateMergeDirectionsProviderConfiguration(IServiceProvider serviceProvider)
        {
            var cfg = serviceProvider.GetRequiredService<IOptionsMonitor<AutomergeBotConfiguration>>().CurrentValue;
            var mergeDirectionsProviderConfigurator = serviceProvider.GetRequiredService<MergingBranches.IMergeDirectionsProviderConfigurator>();

            mergeDirectionsProviderConfigurator.UpdateMergeDirections(cfg.MergeDirectionsParsed);
        }

        private void RegisterConfigurationChangedHandler(IApplicationBuilder app)
        {
            var cfg = app.ApplicationServices.GetRequiredService<IOptionsMonitor<AutomergeBotConfiguration>>();
            cfg.OnChange((a, b) => { OnConfigurationChanged(app.ApplicationServices); });
        }

        private void OnConfigurationChanged(IServiceProvider serviceProvider)
        {
            var logger = serviceProvider.GetRequiredService<ILogger<Startup>>();
            logger.LogInformation("Configuration has been changed");
            LogConfigurationUsed(serviceProvider, logger);
            UpdateMergeDirectionsProviderConfiguration(serviceProvider);
        }

        private static async Task HandleRequest(HttpContext context)
        {
            var logger = context.RequestServices.GetRequiredService<ILogger<Startup>>();
            using (logger.BeginScope("{TraceIdentifier}", context.TraceIdentifier))
            {
                var request = context.Request;
                logger.LogInformation("Received HTTP request. Method: {method}, Path: {path}, QueryString: {queryString}",
                    request.Method,
                    request.Path,
                    request.QueryString);

                try
                {
                    var requestHandler = context.RequestServices.GetRequiredService<IGitHubEventHttpRequestHandler>();
                    await requestHandler.ProcessRequest(context);
                }
                catch (Exception e)
                {
                    logger.LogCritical(e, "Unhandled exception occured during processing request");
                }
            }
        }

        private static void LogConfigurationUsed(IServiceProvider serviceProvider, ILogger<Startup> logger)
        {
            var cfg = serviceProvider.GetRequiredService<IOptionsMonitor<AutomergeBotConfiguration>>().CurrentValue;
            var mergeDirectionsProvider = serviceProvider.GetRequiredService<MergingBranches.IMergeDirectionsProvider>();


            logger.LogInformation("Working with repository: {repositoryOwner}/{repositoryName}", cfg.RepositoryOwner, cfg.RepositoryName);
            logger.LogInformation("Using merge directions configuration: {mergeDirections}", mergeDirectionsProvider.Get().GetMergingConfigurationInfo());
            if ((cfg.AutomergeOnlyForAuthors ?? new List<string>()).Any())
            {
                logger.LogWarning("Automerging only pushes from authors: {automergeOnlyForAuthors}", cfg.AutomergeOnlyForAuthors);
            }
        }
    }
}