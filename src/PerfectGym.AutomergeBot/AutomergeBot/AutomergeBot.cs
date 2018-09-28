using System;
using System.Collections.Generic;
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


        public AutomergeBot(ILogger<AutomergeBot> logger,
            IMergeDirectionsProvider mergeDirectionsProvider,
            IOptionsMonitor<AutomergeBotConfiguration> cfg,
            IMergePerformer mergePerformer,
            IProcessPushPredicate processPushPredicate, 
            ITempBranchesRemover tempBranchesRemover)
        {
            _logger = logger;
            _mergeDirectionsProvider = mergeDirectionsProvider;
            _cfg = cfg.CurrentValue;
            _mergePerformer = mergePerformer;
            _processPushPredicate = processPushPredicate;
            _tempBranchesRemover = tempBranchesRemover;
        }

        public void Handle(PushInfoModel pushInfo)
        {
            _logger.LogInformation("Started processing push notification {@pushNotificationContext}", pushInfo);
            try
            {

                using (var repoContext = new RepositoryConnectionContext(_logger, _cfg.RepositoryName, _cfg.RepositoryOwner, _cfg.AuthToken))
                {
                    if (!_processPushPredicate.CanProcessPush(pushInfo, repoContext))
                        return;
                    
                    _tempBranchesRemover.TryDeleteNoLongerNeededTempBranches(pushInfo, repoContext);

                    if (!TryGetMergeDestinationBranches(pushInfo.GetPushedBranchName(), out var destinationBranchNames)) return;

                    _logger.LogInformation("Will perform merging to {destinationBranchesCount} branches: {destinationBranchNames}",
                        destinationBranchNames.Length, destinationBranchNames);
                    foreach (var destinationBranchName in destinationBranchNames)
                    {
                        _mergePerformer.TryMergePushedChanges(pushInfo, destinationBranchName, repoContext);
                    }

                    var openPullRequestsTargetingBranch = GetOpenPullRequestsTargetingBranch(pushInfo, repoContext);
                    if (openPullRequestsTargetingBranch.Any())
                    {
                        foreach (var pullRequest in openPullRequestsTargetingBranch)
                        {
                            _mergePerformer.TryMergeExistingPullRequest(pullRequest, repoContext);
                        }
                    }
                }
            }
            catch (AggregateException e)
            {
                LogAggregateException(e);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error during processing push notification", pushInfo);
            }
            finally
            {
                _logger.LogInformation("Finished processing push notification");
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
