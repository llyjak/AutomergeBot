using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Octokit;
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
            //using (var repoContext = _repositoryConnectionProvider.GetRepositoryConnection())
            //{
            //    repoContext.GetAllBranches().Where(b=>b.Commit.Sha==pullRequestInfoModel.HeadSha).FirstOrDefault()
            //}

            var headBranchName = pullRequestInfoModel.GetHeadBranchName();
            _logger.LogInformation("Deleting branch {branchName} merged by pull request {pullRequestNumber}", headBranchName);


            return;
            //if (IsContainingTempBranches(pushInfo, repoContext, out var tempBranches) &&
            //    IsPushedToOneOfTheTargetBranches(pushInfo))
            //{
            //    var allBranches = repoContext.GetAllBranches();

            //    var branchesToDelete = new List<BranchName>();

            //    foreach (var commitSha in pushInfo.CommitsShas)
            //    {
            //        if (FindBranchWithGivenHead(allBranches, commitSha, out var branch)
            //            && branch.Name.StartsWith(_cfg.CreatedBranchesPrefix))
            //        {
            //            branchesToDelete.Add(new BranchName(branch.Name));
            //        }
            //    }

            //    DeleteTempBranches(branchesToDelete.Union(tempBranches), repoContext);
            //}
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


        public static bool FindBranchWithGivenHead(IReadOnlyList<Branch> allBranches, string branchHeadCommitSha, out Branch branch)
        {
            branch = allBranches.FirstOrDefault(b => b.Commit.Sha == branchHeadCommitSha);
            return branch != null;
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
    }
}