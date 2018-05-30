using System;
using System.Linq;
using System.Text.RegularExpressions;
using PerfectGym.AutomergeBot.Models;
using PerfectGym.AutomergeBot.RepositoryConnectionContext;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Octokit;

namespace PerfectGym.AutomergeBot.AutomergeBot
{
    public interface IMergePerformer
    {
        void TryMergePushedChanges(PushInfoModel pushInfo, BranchName destinationBranchName, IRepositoryConnectionContext repoContext);
    }

    public class MergePerformer : IMergePerformer
    {
        private readonly ILogger<MergePerformer> _logger;
        private readonly AutomergeBotConfiguration _cfg;
        private readonly IUserNotifier _userNotifier;

        public MergePerformer(
            ILogger<MergePerformer> logger,
            IOptionsMonitor<AutomergeBotConfiguration> cfg,
            IUserNotifier userNotifier)
        {
            _logger = logger;
            _cfg = cfg.CurrentValue;
            _userNotifier = userNotifier;
        }

        public void TryMergePushedChanges(
            PushInfoModel pushInfo,
            BranchName destinationBranchName,
            IRepositoryConnectionContext repoContext)
        {
            var branchForPullRequest = CreateBranchNameForPush(pushInfo.GetPushedBranchName(), pushInfo.HeadCommitSha, destinationBranchName);
            repoContext.CreateBranch(branchForPullRequest, pushInfo.HeadCommitSha);

            var changesOriginalAuthor = RetrieveChangesOriginalAuthorFromPush(pushInfo, repoContext, out var coAuthorString);

            var title = $"Automerge {pushInfo.GetPushedBranchName()} @{pushInfo.HeadCommitSha.Substring(0, 8)} -> {destinationBranchName}";
            var body = $"Last change author: {changesOriginalAuthor}";

            if (!TryCreatePullRequest(branchForPullRequest, destinationBranchName, repoContext, title, body, out var pullRequest))
            {
                TryRemoveBranch(branchForPullRequest, repoContext);
                return;
            }

            var mergeCommitMessage = pullRequest.Title + "\r\n\r\n" + coAuthorString;

            if (repoContext.MergePullRequest(pullRequest.Number, mergeCommitMessage))
            {
                _logger.LogInformation("Pull request {pullRequestNumber} created and merged", pullRequest.Number);
                TryRemoveBranch(branchForPullRequest, repoContext);
            }
            else
            {
                _logger.LogInformation("Pull request {pullRequestNumber} created but could not be merged automatically", pullRequest.Number);

                _userNotifier.NotifyUserAboutPullRequestWithUnresolvedConflicts(
                    pullRequest.Number,
                    changesOriginalAuthor,
                    repoContext,
                    branchForPullRequest.Name,
                    destinationBranchName.Name,
                    pullRequest.HtmlUrl);
            }
        }

        private bool TryCreatePullRequest(BranchName sourceBranch,
            BranchName destinationBranchName,
            IRepositoryConnectionContext repoContext,
            string title,
            string body,
            out PullRequest pullRequest)
        {
            try
            {
                pullRequest = repoContext.CreatePullRequest(sourceBranch, destinationBranchName, title, body);
                return true;
            }
            catch (AggregateException e) when (e.InnerExceptions.OfType<ApiValidationException>().Any())
            {
                var apiValidationException = e.InnerExceptions.OfType<ApiValidationException>().FirstOrDefault();
                if (apiValidationException != null)
                {
                    pullRequest = null;
                    _logger.LogError("Could not create pull request. Error: {gitHubApiErrorMessage}", apiValidationException.FirstErrorMessageSafe());
                    return false;
                }

                throw;
            }
        }

        private string RetrieveChangesOriginalAuthorFromPush(
            PushInfoModel pushInfo,
            IRepositoryConnectionContext repoContext,
            out string coAuthorString
            )
        {
            var coAuthoredLineRegex = new Regex(@"^Co-authored-by: (?<author>.+)", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            if (pushInfo.HeadCommitAuthorUserName == _cfg.AutomergeBotGitHubUserName)
            {
                var headCommitMessage = repoContext.GetCommitMessage(pushInfo.HeadCommitSha);
                var match = coAuthoredLineRegex.Match(headCommitMessage);
                if (match.Success)
                {
                    coAuthorString = match.Value;
                    var r = new Regex(@"^Co-authored-by: (?<author>[\w\.\-]+)");
                    return r.Match(match.Value).Groups["author"].Value;
                }

                _logger.LogError(
                    "Pushed changes head commit is made by us ({automergeBotGitHubUserName}) but does not contain \"Co-authored-by\" in the message. " +
                    "Merge commit will be missing original author of the changes.", _cfg.AutomergeBotGitHubUserName);
            }

            coAuthorString = $"Co-authored-by: {pushInfo.HeadCommitAuthorUserName} <{pushInfo.HeadCommitAuthorEmail}>";
            return pushInfo.HeadCommitAuthorUserName;
        }

        private BranchName CreateBranchNameForPush(
            BranchName sourceBranch,
            string sourceBranchCommitSha,
            BranchName destinationBranchName)
        {
            var prefix = _cfg.CreatedBranchesPrefix;
            var sourceCommitShaShort = sourceBranchCommitSha.Substring(0, 8);
            var sanitizedName = BranchName.Sanitize($"{sourceBranch}_{sourceCommitShaShort}_to_{destinationBranchName}");
            return new BranchName($"{prefix}{sanitizedName}");
        }

        private void TryRemoveBranch(BranchName branchForPullRequest, IRepositoryConnectionContext repoContext)
        {
            try
            {
                repoContext.RemoveBranch(branchForPullRequest);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Could not remove branch {branchName}. It must be removed manually.", branchForPullRequest);
            }
        }
    }
}