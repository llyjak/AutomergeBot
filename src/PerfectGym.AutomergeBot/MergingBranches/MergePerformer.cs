using System;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Octokit;
using PerfectGym.AutomergeBot.Models;
using PerfectGym.AutomergeBot.RepositoryConnection;

namespace PerfectGym.AutomergeBot.MergingBranches
{
    public interface IMergePerformer
    {
        void TryMergePushedChanges(PushInfoModel pushInfo, BranchName destinationBranchName, IRepositoryConnectionContext repoContext);
        void TryMergeExistingPullRequest(PullRequest pullRequest, IRepositoryConnectionContext repoContext);
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

            var createPullRequestSucceeded = TryCreatePullRequest(branchForPullRequest, destinationBranchName, repoContext, out var pullRequest, pushInfo, changesOriginalAuthor);
            if (!createPullRequestSucceeded)
            {
                _logger.LogDebug("Removing temp branch {branchName} because the pull request has not been created", branchForPullRequest);
                TryRemoveBranch(branchForPullRequest, repoContext);
                return;
            }

            var mergeCommitMessage = CreateMergeCommitMessage(pullRequest, coAuthorString);

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

        public void TryMergeExistingPullRequest(
            PullRequest pullRequest,
            IRepositoryConnectionContext repoContext)
        {
            var coAuthorString = CreateCoAuthoredByMessageForExistingPullRequest(pullRequest);
            var mergeCommitMessage = CreateMergeCommitMessage(pullRequest, coAuthorString);

            if (repoContext.MergePullRequest(pullRequest.Number, mergeCommitMessage))
            {
                _logger.LogInformation(
                    "Successfully merged pull request #{pullRequestNumber} into {branchName}",
                    pullRequest.Number,
                    pullRequest.Base.Ref);

                repoContext.AddPullRequestComment(pullRequest.Number, Consts.SuccessfulMergeComment);


            }
            else
            {
                _logger.LogInformation(
                    "Merging pull request #{pullRequestNumber} into {branchName} cannot be done automatically",
                    pullRequest.Number,
                    pullRequest.Base.Ref);
            }
        }

        private static string CreateCoAuthoredByMessageForExistingPullRequest(PullRequest pullRequest)
        {
            var stringBuilder = new StringBuilder();
            foreach (var author in pullRequest.Assignees)
            {
                stringBuilder.AppendLine($"Co-authored-by: {author.Login} <{author.Email ?? CreateGitHubNoReplyEmail(author)}>");
            }

            return stringBuilder.ToString();
        }

        private static string CreateCoAuthoredByMessageForNewPullRequest(PushInfoModel pushInfo)
        {
            return $"Co-authored-by: {pushInfo.HeadCommitAuthorUserName} <{pushInfo.HeadCommitAuthorEmail}>";
        }

        private static string CreateMergeCommitMessage(PullRequest pullRequest, string coAuthorString)
        {
            return $"{pullRequest.Title}\r\n\r\n{coAuthorString}";
        }

        private static string CreateGitHubNoReplyEmail(Account user)
        {
            return $"{user.Id}+{user.Login}@users.noreply.github.com";
        }

        private bool TryCreatePullRequest(BranchName sourceBranch,
            BranchName destinationBranchName,
            IRepositoryConnectionContext repoContext,
            out PullRequest pullRequest,
            PushInfoModel pushInfo,
            string changesOriginalAuthor)
        {
            var title = $"{Consts.AutomergeBotPullRequestTitlePrefix} {pushInfo.GetPushedBranchName()} @{pushInfo.GetHeadCommitShaShort()} -> {destinationBranchName}";
            var body = $"Last change author: {changesOriginalAuthor}";

            try
            {
                pullRequest = repoContext.CreatePullRequest(sourceBranch, destinationBranchName, title, body);
                return true;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Creating pull request failed.");
                pullRequest = null;
                return false;
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

            coAuthorString = CreateCoAuthoredByMessageForNewPullRequest(pushInfo);
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