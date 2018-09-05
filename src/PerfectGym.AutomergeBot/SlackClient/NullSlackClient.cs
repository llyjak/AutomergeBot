using SlackClientStandard;

namespace PerfectGym.AutomergeBot.SlackClient
{
    internal class NullSlackClient : ISlackClient
    {
        public void Dispose()
        {
            //nop
        }

        public void NotifyUserAboutScriptRejection(string author, string reviewer, string comment)
        {
            //nop
        }

        public void NotifyUserAboutScriptExecution(string author, string result, string error)
        {
            //nop
        }

        public void NotifyUserAboutPendingPullRequest(string email, string repositoryUrl)
        {
            //nop
        }
    }
}