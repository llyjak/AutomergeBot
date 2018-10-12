namespace PerfectGym.AutomergeBot
{
    public class Consts
    {
        public const string GitHubPushEventName = "push";
        public const string GitHubPingEventName = "ping";
        public const string GitHubPullRequestEventName = "pull_request";
        public const string GitHubEventRequestHeaderName = "X-GitHub-Event";
        public const string GitHubSignatureRequestHeaderName = "X-Hub-Signature";
        public const string GitHubDeliveryRequestHeaderName = "X-GitHub-Delivery";
        public const string RefsHeads = "refs/heads/";
        public const string AutomergeBotPullRequestTitlePrefix = "Automerge";
        public const string SuccessfulMergeComment = "Your changes have been successfully merged";
    }
}