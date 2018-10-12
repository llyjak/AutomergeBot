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
            string headBranchRef,
            string headSha,
            string baseBranchRef,
            string baseSha)
        {
            RepositoryId = repositoryId;
            Action = action;
            Number = number;
            Merged = merged;
            HeadBranchRef = headBranchRef;
            HeadSha = headSha;
            BaseBranchRef = baseBranchRef;
            BaseSha = baseSha;
        }

        public int RepositoryId { get; }
        public string Action { get; }
        public bool IsClosedAction => Action.ToLowerInvariant() == "closed";
        public int Number { get; }
        public bool Merged { get; }
        public string HeadBranchRef { get; }
        public string HeadSha { get; }
        public string BaseBranchRef { get; }
        public string BaseSha { get; }


        public BranchName GetHeadBranchName()
        {
            return BranchName.CreateFromRef(HeadBranchRef);
        }

        public BranchName GetBaseBranchName()
        {
            return BranchName.CreateFromRef(HeadBranchRef);
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
