using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Octokit;
using PerfectGym.AutomergeBot.Models;
using PerfectGym.AutomergeBot.RepositoryConnection;

namespace PerfectGym.AutomergeBot.MergingBranches
{
    public interface IPullRequestMergeRetryier
    {
        void RetryMergePullRequestsCreatedBefore(PushInfoModel pushInfo, IRepositoryConnectionContext repoContext);
    }

    public class PullRequestMergeRetryier : IPullRequestMergeRetryier
    {
        private readonly ILogger<PullRequestMergeRetryier> _logger;
        private readonly AutomergeBotConfiguration _cfg;
        private readonly IMergePerformer _mergePerformer;

        public PullRequestMergeRetryier(
            ILogger<PullRequestMergeRetryier> logger,
            IOptionsMonitor<AutomergeBotConfiguration> cfg,
            IMergePerformer mergePerformer)
        {
            _logger = logger;
            _cfg = cfg.CurrentValue;
            _mergePerformer = mergePerformer;
        }

        public void RetryMergePullRequestsCreatedBefore(PushInfoModel pushInfo, IRepositoryConnectionContext repoContext)
        {
            var targetBranchName = pushInfo.GetPushedBranchName().Name;
            _logger.LogInformation("Retrying merging pull requests created by AutomergeBot before and not merged yet to branch {targetBranch}", targetBranchName);

            var openPullRequestsTargetingBranch = GetOpenPullRequestsTargetingBranch(repoContext, targetBranchName);
            if (openPullRequestsTargetingBranch.Any())
            {
                _logger.LogDebug("There is {openPullRequestsCount} pull requests which potentially could be merged", openPullRequestsTargetingBranch.Count);
                foreach (var pullRequest in openPullRequestsTargetingBranch)
                {
                    _mergePerformer.TryMergeExistingPullRequest(pullRequest, repoContext);
                }
            }
        }

        private List<PullRequest> GetOpenPullRequestsTargetingBranch(IRepositoryConnectionContext repoContext, string targetBranchName)
        {
            var openPullRequestsTargetingBranch = repoContext
                .GetOpenPullRequests()
                .Where(pr => pr.Base.Ref == targetBranchName)
                .Where(pr => pr.User.Login == _cfg.AutomergeBotGitHubUserName);

            return openPullRequestsTargetingBranch.ToList();
        }
    }
}