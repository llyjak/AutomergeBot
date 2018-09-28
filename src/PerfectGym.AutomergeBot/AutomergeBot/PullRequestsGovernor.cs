using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Octokit;
using PerfectGym.AutomergeBot.RepositoryConnection;

namespace PerfectGym.AutomergeBot.AutomergeBot
{
    /// <summary>
    /// Used to check if there are any open pull requests that should be closed (i.e. PRs created by AutoMergeBot)
    /// If any PRs are found, notifications will be send via Slack, requesting that these PRs are closed
    /// </summary>
    public class PullRequestsGovernor
    {
        private readonly AutomergeBotConfiguration _cfg;
        private readonly ILogger<PullRequestsGovernor> _logger;
        private readonly IUserNotifier _userNotifier;
        private readonly IRepositoryConnectionProvider _repositoryConnectionProvider;
        
        public PullRequestsGovernor(
            ILogger<PullRequestsGovernor> logger,
            IOptionsMonitor<AutomergeBotConfiguration> cfg, 
            IUserNotifier userNotifier, 
            IRepositoryConnectionProvider repositoryConnectionProvider)
        {
            _logger = logger;
            _userNotifier = userNotifier;
            _repositoryConnectionProvider = repositoryConnectionProvider;
            _cfg = cfg.CurrentValue;
        }

        public void StartNewWorker()
        {
            var thread = new Thread(Work) {IsBackground = true};
            thread.Start();
        }

        private void Work()
        {
            while (true)
            {
                CheckForPullRequestsAndNotifyUsers();
                Thread.Sleep(_cfg.PullRequestGovernorConfiguration.ParsedCheckFrequency);
            }
        }

        private void CheckForPullRequestsAndNotifyUsers()
        {
            using (var repoContext = _repositoryConnectionProvider.GetRepositoryConnection())
            {
                var openPullRequests = repoContext.GetOpenPullRequests();

                var filteredPullRequests = FilterPullRequests(openPullRequests);

                _userNotifier.NotifyAboutOpenPullRequests(filteredPullRequests);
            }
        }

        private IEnumerable<PullRequest> FilterPullRequests(IReadOnlyList<PullRequest> openPullRequests)
        {
            var timeLimit = DateTimeOffset.Now.Add(-_cfg.PullRequestGovernorConfiguration.ParsedPullRequestTimeLimit);

            var pullRequests = openPullRequests
                .Where(pr => pr.Title.StartsWith(Consts.AutomergeBotPullRequestTitlePrefix) &&
                             pr.CreatedAt < timeLimit &&
                             pr.User.Login == _cfg.AutomergeBotGitHubUserName);

            return pullRequests;
        }
    }
}