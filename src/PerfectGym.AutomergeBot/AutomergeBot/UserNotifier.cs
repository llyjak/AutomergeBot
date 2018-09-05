using System.Collections.Generic;
using PerfectGym.AutomergeBot.RepositoryConnectionContext;
using Microsoft.Extensions.Logging;
using Octokit;
using PerfectGym.AutomergeBot.SlackClient;

namespace PerfectGym.AutomergeBot.AutomergeBot
{
    public interface IUserNotifier
    {
        void NotifyUserAboutPullRequestWithUnresolvedConflicts(
            int pullRequestNumber,
            string gitHubUserName,
            IRepositoryConnectionContext repoContext,
            string pullRequestBranchName,
            string destinationBranch,
            string pullRequestUrl);

        void NotifyAboutOpenPullRequests(IEnumerable<PullRequest> filteredPullRequests);
    }

    public class UserNotifier : IUserNotifier
    {
        private readonly ILogger<UserNotifier> _logger;
        private readonly SlackClientProvider _clientProvider;

        public UserNotifier(
            ILogger<UserNotifier> logger,
            SlackClientProvider clientProvider)
        {
            _logger = logger;
            _clientProvider = clientProvider;
        }

        public void NotifyUserAboutPullRequestWithUnresolvedConflicts(
            int pullRequestNumber,
            string gitHubUserName,
            IRepositoryConnectionContext repoContext,
            string pullRequestBranchName,
            string destinationBranch,
            string pullRequestUrl)
        {
            var comment = $"Cannot merge automatically. @{gitHubUserName} please resolve conflicts manually, approve review and merge pull request."
                          + "\r\n\r\n"
                          + "How to do it (using the GIT command line):\r\n"
                          + $"1. Fetch changes from server and checkout '{destinationBranch}' branch\r\n"
                          + "   ```\r\n"
                          + $"   git fetch && git checkout {destinationBranch} && " + "git reset --hard @{u}\r\n"
                          + "   ```\r\n"
                          + $"2. Merge 'origin/{pullRequestBranchName}' branch and resolve conflicts\r\n"
                          + "   ```\r\n"
                          + $"   git merge --no-ff origin/{pullRequestBranchName}\r\n"
                          + "   ```\r\n"
                          + $"4. Approve [pull request]({pullRequestUrl}/files#submit-review) review\r\n"
                          + $"5. Push changes to {destinationBranch}\r\n"
                          + "   ```\r\n"
                          + $"   git push origin {destinationBranch}\r\n"
                          + "   ```\r\n";

            repoContext.AddPullRequestComment(pullRequestNumber, comment);
            repoContext.AddReviewerToPullRequest(pullRequestNumber, new[] { gitHubUserName });
            repoContext.AssignUsersToPullRequest(pullRequestNumber, new[] { gitHubUserName });
        }

        public void NotifyAboutOpenPullRequests(IEnumerable<PullRequest> filteredPullRequests)
        {
            using (var client = _clientProvider.Create())
            {
                foreach (var pullRequest in filteredPullRequests)
                {
                    NotifyAssignedUsersBySlack(pullRequest, client);
                }
            }
        }

        private static void NotifyAssignedUsersBySlack(PullRequest pullRequest, SlackClientStandard.ISlackClient client)
        {
            var assignees = pullRequest.Assignees;
            var pullRequestUrl = pullRequest.HtmlUrl;

            foreach (var assignee in assignees)
            {
                var contact = assignee.Email ?? assignee.Login;
                client.NotifyUserAboutPendingPullRequest(contact, pullRequestUrl);
            }
        }
    }
}