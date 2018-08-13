using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Octokit;
using SlackClientStandard;

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
        private readonly ISlackClientProvider _clientProvider;

        public PullRequestsGovernor(
            ILogger<PullRequestsGovernor> logger,
            IOptionsMonitor<AutomergeBotConfiguration> cfg,
            ISlackClientProvider clientProvider)
        {
            _logger = logger;
            _clientProvider = clientProvider;
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
            using (var context = new RepositoryConnectionContext.RepositoryConnectionContext(
                _logger,
                _cfg.RepositoryName,
                _cfg.RepositoryOwner,
                _cfg.AuthToken))
            {
                var openPullRequests = context.GetOpenPullRequests();

                var filteredPullRequests = FilterPullRequests(openPullRequests);

                foreach (var pullRequest in filteredPullRequests)
                {
                    NotifyAssignedUsersBySlack(pullRequest);
                }
            }
        }

        private void NotifyAssignedUsersBySlack(PullRequest pullRequest)
        {
            var assignees = pullRequest.Assignees;
            var pullRequestUrl = pullRequest.HtmlUrl;
            using (var client = CreateSlackClient())
            {
                foreach (var assignee in assignees)
                {
                    var contact = assignee.Email ?? assignee.Login;
                    client.NotifyUserAboutPendingPullRequest(
                        contact,
                        pullRequestUrl);
                }
            }
        }

        private ISlackClient CreateSlackClient()
        {
            return _clientProvider.CreateClient(
                _cfg.PullRequestGovernorConfiguration.SlackToken,
                _cfg.PullRequestGovernorConfiguration.SlackChannels,
                _cfg.AutomergeBotGitHubUserName);
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