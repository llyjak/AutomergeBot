using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Options;
using Octokit;
using PerfectGym.AutomergeBot.Models;
using PerfectGym.AutomergeBot.RepositoryConnection;

namespace PerfectGym.AutomergeBot.AutomergeBot
{
    public interface IPullRequestMergeRetryier
    {
        void RetryMergePullRequestsCreatedBefore(PushInfoModel pushInfo, IRepositoryConnectionContext repoContext);
    }

    public class PullRequestMergeRetryier : IPullRequestMergeRetryier
    {
        private readonly AutomergeBotConfiguration _cfg;
        private readonly IMergePerformer _mergePerformer;

        public PullRequestMergeRetryier(
            IOptionsMonitor<AutomergeBotConfiguration> cfg,
            IMergePerformer mergePerformer)
        {
            _cfg = cfg.CurrentValue;
            _mergePerformer = mergePerformer;
        }

        public void RetryMergePullRequestsCreatedBefore(PushInfoModel pushInfo, IRepositoryConnectionContext repoContext)
        {
            var openPullRequestsTargetingBranch = GetOpenPullRequestsTargetingBranch(pushInfo, repoContext);
            if (Enumerable.Any<PullRequest>(openPullRequestsTargetingBranch))
            {
                foreach (var pullRequest in openPullRequestsTargetingBranch)
                {
                    _mergePerformer.TryMergeExistingPullRequest(pullRequest, repoContext);
                }
            }
        }

        private List<PullRequest> GetOpenPullRequestsTargetingBranch(PushInfoModel pushInfo, IRepositoryConnectionContext repoContext)
        {
            var targetBranchName = pushInfo.GetPushedBranchName().Name;
            var openPullRequestsTargetingBranch = repoContext
                .GetOpenPullRequests()
                .Where(pr => pr.Base.Ref == targetBranchName)
                .Where(pr => pr.User.Login == _cfg.AutomergeBotGitHubUserName);

            return openPullRequestsTargetingBranch.ToList();
        }
    }
}