using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PerfectGym.AutomergeBot.Models;
using PerfectGym.AutomergeBot.RepositoryConnection;

namespace PerfectGym.AutomergeBot.TempBranchesRemoving
{
    public interface ITempBranchesRemoverPullRequestHandler
    {
        void Handle(PullRequestInfoModel pullRequestInfoModel);
    }

    public class TempBranchesRemoverPullRequestHandlerPullRequestHandler : ITempBranchesRemoverPullRequestHandler
    {
        private readonly IRepositoryConnectionProvider _repositoryConnectionProvider;
        private readonly ILogger<TempBranchesRemoverPullRequestHandlerPullRequestHandler> _logger;
        private readonly AutomergeBotConfiguration _cfg;

        public TempBranchesRemoverPullRequestHandlerPullRequestHandler(
            IRepositoryConnectionProvider repositoryConnectionProvider,
            ILogger<TempBranchesRemoverPullRequestHandlerPullRequestHandler> logger,
            IOptionsMonitor<AutomergeBotConfiguration> cfg)
        {
            _repositoryConnectionProvider = repositoryConnectionProvider;
            _logger = logger;
            _cfg = cfg.CurrentValue;
        }

        public void Handle(PullRequestInfoModel pullRequestInfoModel)
        {
            if (!IsPullRequestClosedByMerge(pullRequestInfoModel))
            {
                return;
            }

            var headBranchName = pullRequestInfoModel.GetHeadBranchName();
            if (IsAutomergeBotTempBranch(headBranchName))
            {
                _logger.LogInformation("Removing temporary branch {branchName} merged by pull request {pullRequestNumber}", headBranchName);
                RemoveBranch(headBranchName);
            }
        }

        private static bool IsPullRequestClosedByMerge(PullRequestInfoModel pullRequestInfoModel)
        {
            return pullRequestInfoModel.IsClosedAction && pullRequestInfoModel.Merged;
        }

        private bool IsAutomergeBotTempBranch(BranchName headBranchName)
        {
            return headBranchName.Name.StartsWith(_cfg.CreatedBranchesPrefix);
        }

        private void RemoveBranch(BranchName headBranchName)
        {
            using (var repoContext = _repositoryConnectionProvider.GetRepositoryConnection())
            {
                _logger.LogDebug("Removing temporary {branchName} branch from repository", headBranchName);
                try
                {
                    repoContext.RemoveBranch(headBranchName);
                }
                catch (Exception e)
                {
                    _logger.LogWarning(e, "Error when removing branch '{branchName}'. It could be deleted by human", headBranchName);
                }
            }
        }
    }
}