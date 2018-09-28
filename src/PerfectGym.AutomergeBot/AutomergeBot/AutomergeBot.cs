using System;
using System.Linq;
using PerfectGym.AutomergeBot.Models;
using Microsoft.Extensions.Logging;
using Octokit;
using PerfectGym.AutomergeBot.RepositoryConnection;

namespace PerfectGym.AutomergeBot.AutomergeBot
{
    public class AutomergeBot
    {
        private readonly ILogger<AutomergeBot> _logger;
        private readonly IMergeDirectionsProvider _mergeDirectionsProvider;
        private readonly IMergePerformer _mergePerformer;
        private readonly IProcessPushPredicate _processPushPredicate;
        private readonly ITempBranchesRemover _tempBranchesRemover;
        private readonly IPullRequestMergeRetryier _pullRequestMergeRetryier;
        private readonly IRepositoryConnectionProvider _repositoryConnectionProvider;


        public AutomergeBot(ILogger<AutomergeBot> logger,
            IMergeDirectionsProvider mergeDirectionsProvider,
            IMergePerformer mergePerformer,
            IProcessPushPredicate processPushPredicate,
            ITempBranchesRemover tempBranchesRemover,
            IPullRequestMergeRetryier pullRequestMergeRetryier,
            IRepositoryConnectionProvider repositoryConnectionProvider)
        {
            _logger = logger;
            _mergeDirectionsProvider = mergeDirectionsProvider;
            _mergePerformer = mergePerformer;
            _processPushPredicate = processPushPredicate;
            _tempBranchesRemover = tempBranchesRemover;
            _pullRequestMergeRetryier = pullRequestMergeRetryier;
            _repositoryConnectionProvider = repositoryConnectionProvider;
        }

        public void Handle(PushInfoModel pushInfo)
        {
            _logger.LogInformation("Started processing push notification {@pushNotificationContext}", pushInfo);

            try
            {
                if (!CanProcess(pushInfo))
                {
                    return;
                }

                DoCriticalTasks(pushInfo);

                DoNonCriticalTasks(pushInfo);
            }
            finally
            {
                _logger.LogInformation("Finished processing push notification {@pushNotificationContext}", pushInfo);
            }
        }

        private bool CanProcess(PushInfoModel pushInfo)
        {
            using (var repoContext = _repositoryConnectionProvider.GetRepositoryConnection())
            {
                if (!_processPushPredicate.CanProcessPush(pushInfo, repoContext))
                {
                    return false;
                }
            }

            return true;
        }

        private void DoCriticalTasks(PushInfoModel pushInfo)
        {
            try
            {
                using (var repoContext = _repositoryConnectionProvider.GetRepositoryConnection())
                {
                    TryDoMerges(pushInfo, repoContext);
                }
            }
            catch (Exception e)
            {
                _logger.LogCritical(e, "Failed performing critical tasks when processing push notification");
            }
        }

        private void TryDoMerges(PushInfoModel pushInfo, IRepositoryConnectionContext repoContext)
        {
            if (!TryGetMergeDestinationBranches(pushInfo.GetPushedBranchName(), out var destinationBranchNames))
            {
                return;
            }

            _logger.LogInformation("Merging to {destinationBranchesCount} branches: {destinationBranchNames}",
                destinationBranchNames.Length, destinationBranchNames);

            foreach (var destinationBranchName in destinationBranchNames)
            {
                try
                {
                    _mergePerformer.TryMergePushedChanges(pushInfo, destinationBranchName, repoContext);
                }
                catch (Exception e)
                {
                    _logger.LogCritical(e, "Failed merging {commitSha} to {branchName}", pushInfo.HeadCommitSha, pushInfo.GetPushedBranchName());
                }
            }

        }

        private void DoNonCriticalTasks(PushInfoModel pushInfo)
        {
            try
            {
                using (var repoContext = _repositoryConnectionProvider.GetRepositoryConnection())
                {
                    _tempBranchesRemover.TryDeleteNoLongerNeededTempBranches(pushInfo, repoContext);
                    _pullRequestMergeRetryier.RetryMergePullRequestsCreatedBefore(pushInfo, repoContext);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed performing non-critical tasks when processing push notification");
            }
        }


        private bool TryGetMergeDestinationBranches(BranchName branchName, out BranchName[] destinationBranchNames)
        {
            destinationBranchNames = _mergeDirectionsProvider.Get().GetMergeDestinationBranchNames(branchName.Name)
                .Select(x => new BranchName(x))
                .ToArray();

            if (destinationBranchNames.Any())
            {
                return true;
            }

            _logger.LogInformation("Processing dismissed. There is no configured merge directions for {branchName} branch", branchName);
            return false;

        }

      
    }
}
