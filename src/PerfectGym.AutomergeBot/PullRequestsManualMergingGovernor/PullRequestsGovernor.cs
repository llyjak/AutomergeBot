using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Octokit;
using PerfectGym.AutomergeBot.RepositoryConnection;

namespace PerfectGym.AutomergeBot.PullRequestsManualMergingGovernor
{
    /// <summary>
    /// Used to check if there are any open pull requests that should be closed (i.e. PRs created by AutoMergeBot)
    /// If any PRs are found, notifications will be send via Slack, requesting that these PRs are closed
    /// </summary>
    public class PullRequestsGovernor
    {
        private readonly IOptionsMonitor<AutomergeBotConfiguration> _cfg;
        private readonly ILogger<PullRequestsGovernor> _logger;
        private readonly IUserNotifier _userNotifier;
        private readonly IRepositoryConnectionProvider _repositoryConnectionProvider;
        private bool _started;

        public PullRequestsGovernor(
            ILogger<PullRequestsGovernor> logger,
            IOptionsMonitor<AutomergeBotConfiguration> cfg,
            IUserNotifier userNotifier,
            IRepositoryConnectionProvider repositoryConnectionProvider)
        {
            _logger = logger;
            _userNotifier = userNotifier;
            _repositoryConnectionProvider = repositoryConnectionProvider;
            _cfg = cfg;
        }

        public void StartWorker()
        {
            if (_started)
            {
                throw new Exception("Already started");
            }
            _started = true;
            var thread = new Thread(Work) { IsBackground = true };
            thread.Start();
        }

        private void Work()
        {
            while (true)
            {
                CheckForPullRequestsAndNotifyUsers();
                Thread.Sleep(_cfg.CurrentValue.PullRequestGovernorConfiguration.ParsedCheckFrequency);
            }
        }

        private void CheckForPullRequestsAndNotifyUsers()
        {
            _logger.LogInformation("CheckForPullRequestsAndNotifyUsers");
            using (var repoContext = _repositoryConnectionProvider.GetRepositoryConnection())
            {
                var openPullRequests = repoContext.GetOpenPullRequests();

                var filteredPullRequests = FilterPullRequests(openPullRequests);
                if (filteredPullRequests.Count > 0)
                {
                    _logger.LogInformation("Notifying users there is still open {count} pull requests");
                    _userNotifier.NotifyAboutOpenPullRequests(filteredPullRequests);
                }
            }
        }

        private List<PullRequest> FilterPullRequests(IReadOnlyList<PullRequest> openPullRequests)
        {
            var timeLimit = DateTimeOffset.Now.Add(-_cfg.CurrentValue.PullRequestGovernorConfiguration.ParsedPullRequestTimeLimit);

            var pullRequests = openPullRequests
                .Where(pr => pr.Title.StartsWith(Consts.AutomergeBotPullRequestTitlePrefix) &&
                             pr.CreatedAt < timeLimit &&
                             pr.User.Login == _cfg.CurrentValue.AutomergeBotGitHubUserName);

            return pullRequests.ToList();
        }
    }
}