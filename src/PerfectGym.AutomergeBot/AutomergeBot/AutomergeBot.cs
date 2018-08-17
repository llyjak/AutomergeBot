using System;
using System.Collections.Generic;
using System.Linq;
using PerfectGym.AutomergeBot.Models;
using PerfectGym.AutomergeBot.RepositoryConnectionContext;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Octokit;

namespace PerfectGym.AutomergeBot.AutomergeBot
{
    public class AutomergeBot
    {
        private readonly ILogger<AutomergeBot> _logger;
        private readonly IMergeDirectionsProvider _mergeDirectionsProvider;
        private readonly AutomergeBotConfiguration _cfg;
        private readonly IMergePerformer _mergePerformer;


        public AutomergeBot(
            ILogger<AutomergeBot> logger,
            IMergeDirectionsProvider mergeDirectionsProvider,
            IMergePerformer mergePerformer,
            IOptionsMonitor<AutomergeBotConfiguration> cfg)
        {
            _logger = logger;
            _mergePerformer = mergePerformer;
            _mergeDirectionsProvider = mergeDirectionsProvider;
            _cfg = cfg.CurrentValue;
        }

        public void Handle(PushInfoModel pushInfo)
        {
            _logger.LogInformation("Started processing push notification {@pushNotificationContext}", pushInfo);
            try
            {

                using (var repoContext =
                    new RepositoryConnectionContext.RepositoryConnectionContext(_logger, _cfg.RepositoryName, _cfg.RepositoryOwner, _cfg.AuthToken))
                {
                    if (!IsMonitoredRepository(pushInfo, repoContext)) return;
                    if (!IsPushAddingNewCommits(pushInfo)) return;
                    if (IsPushedToIgnoredBranch(pushInfo)) return;
                    if (IsContainingTempBranches(pushInfo, repoContext, out var tempBranches) &&
                        IsPushedToOneOfTheTargetBranches(pushInfo))
                    {
                        var branchesFilter = new BranchesFilter(repoContext.GetAllBranches(), _cfg);
                        var branchesToDelete = branchesFilter.GetAllBranchesToDelete(pushInfo.CommitsShas);
                        DeleteBranches(branchesToDelete.Union(tempBranches), repoContext);
                    }
                    if (!TryGetMergeDestinationBranches(pushInfo.GetPushedBranchName(), out var destinationBranchNames)) return;
                    if (!IsAutomergeEnabledForAuthorOfLastestCommit(pushInfo)) return;

                    _logger.LogInformation("Will perform merging to {destinationBranchesCount} branches: {destinationBranchNames}",
                        destinationBranchNames.Length, destinationBranchNames);
                    foreach (var destinationBranchName in destinationBranchNames)
                    {
                        _mergePerformer.TryMergePushedChanges(pushInfo, destinationBranchName, repoContext);
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

        private bool IsMonitoredRepository(PushInfoModel pushInfo, IRepositoryConnectionContext repoContext)
        {
            if (repoContext.IsMonitoredRepository(pushInfo.RepositoryId))
            {
                return true;
            }

            _logger.LogWarning("Processing dismissed. Push is not from monitored repository ({RepositoryOwner}/{RepositoryName})",
                _cfg.RepositoryOwner, _cfg.RepositoryName);
            return false;
        }

        private bool IsPushAddingNewCommits(PushInfoModel pushInfo)
        {
            if (!pushInfo.Deleted && !pushInfo.Forced && pushInfo.Ref.StartsWith(Consts.RefsHeads))
            {
                return true;
            }

            _logger.LogInformation("Processing dismissed. Only push adding new commits is accepted. Force pushes / tags pushes etc. are rejected.");
            return false;
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
            if (mergedFromCommit == null) return null;
            var automergeBotBranch = allBranchesFromRepo
                .Where(branch => branch.Commit.Sha == mergedFromCommit.Sha)
                .SingleOrDefault(br => br.Name.StartsWith(_cfg.CreatedBranchesPrefix));
            return automergeBotBranch == null ? null : new List<BranchName> { new BranchName(automergeBotBranch.Name) };
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

        private bool IsPushedToIgnoredBranch(PushInfoModel pushInfo)
        {
            var branchName = pushInfo.GetPushedBranchName();
            if (branchName.Name.StartsWith(_cfg.CreatedBranchesPrefix))
            {
                _logger.LogDebug("Processing dismissed. Push is on branch {branchName} which is created by us", branchName);
                return true;
            }

            return false;

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
