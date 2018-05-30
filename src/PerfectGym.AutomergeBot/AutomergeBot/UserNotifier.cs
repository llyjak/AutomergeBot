using PerfectGym.AutomergeBot.RepositoryConnectionContext;
using Microsoft.Extensions.Logging;

namespace PerfectGym.AutomergeBot.AutomergeBot
{
    public interface IUserNotifier
    {
        void NotifyUserAboutPullRequestWithUnresolvedConflicts(
            int pullRequestNumber,
            string gitHubUserName,
            IRepositoryConnectionContext repoContext,
            string pullRequestBranchName,
            string destinationBranch, string pullRequestUrl);
    }

    public class UserNotifier : IUserNotifier
    {
        private readonly ILogger<UserNotifier> _logger;

        public UserNotifier(ILogger<UserNotifier> logger)
        {
            _logger = logger;
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
                          + $"   git fetch; git checkout {destinationBranch}; " + "git reset --hard @{u}\r\n"
                          + "   ```\r\n"
                          + $"2. Merge 'origin/{pullRequestBranchName}' branch and resolve conflicts\r\n"
                          + "   ```\r\n"
                          + $"   git merge --no-ff origin/{pullRequestBranchName}\r\n"
                          + "   ```\r\n"
                          + $"4. Approve [pull request]({pullRequestUrl}) review\r\n"
                          + $"5. Push changes to {destinationBranch}\r\n"
                          + "   ```\r\n"
                          + $"   git push origin {destinationBranch}\r\n"
                          + "   ```\r\n"
                          + $"6. Delete [pull request's]({pullRequestUrl}) source branch\r\n"
                          + "   Click 'Delete branch' button \r\n";

            repoContext.AddPullRequestComment(pullRequestNumber, comment);
            repoContext.AddReviewerToPullRequest(pullRequestNumber, new[] { gitHubUserName });
            repoContext.AssignUsersToPullRequest(pullRequestNumber, new[] { gitHubUserName });
        }
    }
}