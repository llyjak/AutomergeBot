using System;
using System.Linq;
using PerfectGym.AutomergeBot.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Octokit;
using PerfectGym.AutomergeBot.RepositoryConnection;

namespace PerfectGym.AutomergeBot.AutomergeBot
{
    public class AutomergeBot
    {
        private readonly ILogger<AutomergeBot> _logger;
        private readonly IMergeDirectionsProvider _mergeDirectionsProvider;
        private readonly AutomergeBotConfiguration _cfg;
        private readonly IMergePerformer _mergePerformer;
        private readonly IProcessPushPredicate _processPushPredicate;
        private readonly ITempBranchesRemover _tempBranchesRemover;
        private readonly IPullRequestMergeRetryier _pullRequestMergeRetryier;


        public AutomergeBot(ILogger<AutomergeBot> logger,
            IMergeDirectionsProvider mergeDirectionsProvider,
            IOptionsMonitor<AutomergeBotConfiguration> cfg,
            IMergePerformer mergePerformer,
            IProcessPushPredicate processPushPredicate,
            ITempBranchesRemover tempBranchesRemover, 
            IPullRequestMergeRetryier pullRequestMergeRetryier)
        {
            _logger = logger;
            _mergeDirectionsProvider = mergeDirectionsProvider;
            _mergePerformer = mergePerformer;
            _processPushPredicate = processPushPredicate;
            _tempBranchesRemover = tempBranchesRemover;
            _pullRequestMergeRetryier = pullRequestMergeRetryier;
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
            using (var repoContext = new RepositoryConnectionContext(_logger, _cfg.RepositoryName, _cfg.RepositoryOwner, _cfg.AuthToken))
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
                using (var repoContext = new RepositoryConnectionContext(_logger, _cfg.RepositoryName, _cfg.RepositoryOwner, _cfg.AuthToken))
                {
                    TryDoMerges(pushInfo, repoContext);
                }
            }
            catch (Exception e)
            {
                _logger.LogCritical(e, "Failed performing critical tasks when processing push notification");
            }
        }

        private void TryDoMerges(PushInfoModel pushInfo, RepositoryConnectionContext repoContext)
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
                using (var repoContext = new RepositoryConnectionContext(_logger, _cfg.RepositoryName, _cfg.RepositoryOwner, _cfg.AuthToken))
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

        private void LogAggregateException(AggregateException exception)
        {
            foreach (var innerException in exception.InnerExceptions)
            {
                if (innerException is ApiException apiException)
                {
                    _logger.LogError(innerException, "Error during processing push notification. GitHub Api error details message: {gitHubErrorMessage} {apiErrors}",
                        apiException.Message, apiException.ApiError?.Errors);
                }
                else
                {
                    _logger.LogError(innerException, "Error during processing push notification");
                }
            }
        }
    }
}
