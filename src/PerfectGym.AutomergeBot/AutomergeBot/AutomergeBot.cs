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
            using (_logger.BeginScope("{@pushNotificationContext}", pushInfo))
            {
                _logger.LogInformation("Started processing push notification");
                try
                {
                    using (var repoContext =
                        new RepositoryConnectionContext.RepositoryConnectionContext(_logger, _cfg.RepositoryName, _cfg.RepositoryOwner, _cfg.AuthToken))
                    {
                        if (!IsMonitoredRepository(pushInfo, repoContext)) return;
                        if (!IsPushAddingNewCommits(pushInfo)) return;
                        if (IsPushedToIgnoredBranch(pushInfo)) return;
                        if (IsContainingRedundantBranches(pushInfo, repoContext, out var parents))
                        {
                            DeleteRedundantBranches(parents, repoContext);
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

        private bool IsContainingRedundantBranches(PushInfoModel pushInfo,IRepositoryConnectionContext repoContext,out List<BranchName> parents)
        {
            var _parents = repoContext.GetBranchesForMergeCommit(pushInfo.HeadCommitSha);
            parents = _parents;
            return _parents.Count != 0;
        }

        private void DeleteRedundantBranches(List<BranchName> branches, IRepositoryConnectionContext repoContext)
        {
            foreach(var branch in branches)
            {
                if (branch.Name.StartsWith(_cfg.CreatedBranchesPrefix))
                {
                    var branchName = branch.Name;
                    _logger.LogInformation("Branch {branchName} was marked as redundant. Removing branch from repository", branchName);
                    repoContext.RemoveBranch(branch);
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
                    _logger.LogError(innerException, "Error during processing push notification. GitHub Api error details message: {gitHubErrorMessage}",
                        apiException.ApiError.FirstErrorMessageSafe());
                }
                else
                {
                    _logger.LogError(innerException, "Error during processing push notification");
                }
            }
        }
    }
}
