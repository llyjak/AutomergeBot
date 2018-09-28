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


        public AutomergeBot(
            ILogger<AutomergeBot> logger,
            IMergeDirectionsProvider mergeDirectionsProvider,
            IMergePerformer mergePerformer,
            IOptionsMonitor<AutomergeBotConfiguration> cfg,
            IProcessPushPredicate processPushPredicate)
        {
            _logger = logger;
            _mergePerformer = mergePerformer;
            _processPushPredicate = processPushPredicate;
            _mergeDirectionsProvider = mergeDirectionsProvider;
            _cfg = cfg.CurrentValue;
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
                    if (IsContainingTempBranches(pushInfo, repoContext, out var tempBranches) &&
                        IsPushedToOneOfTheTargetBranches(pushInfo))
                    {
                        var branchesFilter = new BranchesFilter(repoContext.GetAllBranches(), _cfg);
                        var branchesToDelete = branchesFilter.GetAllBranchesToDelete(pushInfo.CommitsShas);
                        DeleteTempBranches(branchesToDelete.Union(tempBranches), repoContext);
                    }
                    if (!TryGetMergeDestinationBranches(pushInfo.GetPushedBranchName(), out var destinationBranchNames)) return;
                    if (!IsAutomergeEnabledForAuthorOfLastestCommit(pushInfo)) return;

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

        private bool IsContainingTempBranches(PushInfoModel pushInfo, IRepositoryConnectionContext repoContext, out List<BranchName> tempBranches)
        {
            tempBranches = GetMergedTempBranchNameOrNull(pushInfo.HeadCommitSha, repoContext);
            return tempBranches != null;
        }

        private List<BranchName> GetMergedTempBranchNameOrNull(string mergeCommitSha, IRepositoryConnectionContext repoContext)
        {
            _logger.LogDebug("Getting temp merged branches by merge commit {mergeCommitSha}", mergeCommitSha);

            var allBranchesFromRepo = repoContext.GetAllBranches();

            var mergedFromCommit = repoContext.GetCommitParents(mergeCommitSha).ElementAtOrDefault(1);
            if (mergedFromCommit == null)
            {
                return null;
            }
            var automergeBotBranches = allBranchesFromRepo
                .Where(branch => branch.Commit.Sha == mergedFromCommit.Sha)
                .Where(br => br.Name.StartsWith(_cfg.CreatedBranchesPrefix))
                .Select(br => new BranchName(br.Name))
                .ToList();
            return automergeBotBranches;
        }

        private bool IsPushedToOneOfTheTargetBranches(PushInfoModel pushInfo)
        {
            return _cfg.MergeDirectionsParsed
                .Any(direction => direction.to.Equals(pushInfo.GetPushedBranchName().Name));
        }

        private void DeleteTempBranches(IEnumerable<BranchName> branches, IRepositoryConnectionContext repoContext)
        {
            foreach (var branch in branches)
            {
                _logger.LogDebug("Removing temporary {branchName} branch from repository as it is no longer used", branch.Name);
                try
                {
                    repoContext.RemoveBranch(branch);
                }
                catch (Exception)
                {
                    _logger.LogWarning(
                        "Temporary branch '{branchName}' no longer exists in the repository. Presumably it has been deleted manually by user",
                        branch.Name);
                }

            }
        }

        private bool IsAutomergeEnabledForAuthorOfLastestCommit(PushInfoModel pushInfo)
        {
            if (!(_cfg.AutomergeOnlyForAuthors ?? new List<string>()).Any())
                return true;

            var headCommitAuthor = pushInfo.HeadCommitAuthorUserName.Trim();

            if (headCommitAuthor == _cfg.AutomergeBotGitHubUserName)
                return true;

            var enabledForAuthor = _cfg.AutomergeOnlyForAuthors.Any(x => x.Trim() == headCommitAuthor);
            if (!enabledForAuthor)
            {
                _logger.LogWarning("Processing dismissed. Automerging is not enabled for commit author {commitAuthorUserName}", pushInfo.HeadCommitAuthorUserName);
            }
            return enabledForAuthor;
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
