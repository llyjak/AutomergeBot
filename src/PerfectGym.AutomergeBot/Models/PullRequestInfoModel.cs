using Newtonsoft.Json.Linq;

namespace PerfectGym.AutomergeBot.Models
{
    public class PullRequestInfoModel : InfoModelBase
    {
        public PullRequestInfoModel(
            int repositoryId,
            string action,
            int number,
            bool merged,
            string headBranch,
            string headSha,
            string baseBranch,
            string baseSha)
        {
            RepositoryId = repositoryId;
            Action = action;
            Number = number;
            Merged = merged;
            HeadBranch = headBranch;
            HeadSha = headSha;
            BaseBranch = baseBranch;
            BaseSha = baseSha;
        }

        public int RepositoryId { get; }
        public string Action { get; }
        public bool IsClosedAction => Action.ToLowerInvariant() == "closed";
        public int Number { get; }
        public bool Merged { get; }
        public string HeadBranch { get; }
        public string HeadSha { get; }
        public string BaseBranch { get; }
        public string BaseSha { get; }


        public BranchName GetHeadBranchName()
        {
            return new BranchName(HeadBranch);
        }

        public BranchName GetBaseBranchName()
        {
            return new BranchName(BaseBranch);
        }

        public static PullRequestInfoModel CreateFromPayload(JObject pushPayload)
        {
            return new PullRequestInfoModel(
                SafeGet<int>(pushPayload, "repository.id"),
                SafeGet<string>(pushPayload, "action"),
                SafeGet<int>(pushPayload, "number"),
                SafeGet<bool>(pushPayload, "pull_request.merged"),
                SafeGet<string>(pushPayload, "pull_request.head.ref"),
                SafeGet<string>(pushPayload, "pull_request.head.sha"),
                SafeGet<string>(pushPayload, "pull_request.base.ref"),
                SafeGet<string>(pushPayload, "pull_request.base.sha")
            );
        }
    }
}
