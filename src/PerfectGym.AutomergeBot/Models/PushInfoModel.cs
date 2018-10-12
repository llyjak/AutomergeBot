using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Octokit;

namespace PerfectGym.AutomergeBot.Models
{
    public class PushInfoModel : InfoModelBase
    {
        public PushInfoModel(
            int repositoryId,
            string @ref,
            string headCommitSha,
            bool created,
            bool forced,
            bool deleted,
            string headCommitCommitterUserName,
            string headCommitAuthorUserName,
            string headCommitAuthorEmail,
            List<string> commitsShas)
        {
            RepositoryId = repositoryId;
            Ref = @ref;
            HeadCommitSha = headCommitSha;
            Created = created;
            Forced = forced;
            Deleted = deleted;
            HeadCommitCommitterUserName = headCommitCommitterUserName;
            HeadCommitAuthorUserName = headCommitAuthorUserName;
            HeadCommitAuthorEmail = headCommitAuthorEmail;
            CommitsShas = commitsShas;
        }

        public int RepositoryId { get; }
        public string Ref { get; }
        public string HeadCommitSha { get; }
        public bool Created { get; }
        public bool Forced { get; }
        public bool Deleted { get; }
        public string HeadCommitCommitterUserName { get; }
        public string HeadCommitAuthorUserName { get; }
        public string HeadCommitAuthorEmail { get; }
        public List<string> CommitsShas { get; }

        public BranchName GetPushedBranchName()
        {
            return BranchName.CreateFromRef(Ref);
        }

        public string GetHeadCommitShaShort()
        {
            return HeadCommitSha.Substring(0, 8);
        }

        public static PushInfoModel CreateFromPayload(JObject pushPayload)
        {
            return new PushInfoModel(
                SafeGet<int>(pushPayload, "repository.id"),
                SafeGet<string>(pushPayload, "ref"),
                SafeGet<string>(pushPayload, "head_commit.id"),
                pushPayload["created"].Value<bool>(),
                pushPayload["forced"].Value<bool>(),
                pushPayload["deleted"].Value<bool>(),
                SafeGet<string>(pushPayload, "head_commit.committer.username") ?? SafeGet<string>(pushPayload, "head_commit.committer.name"),
                SafeGet<string>(pushPayload, "head_commit.author.username") ?? SafeGet<string>(pushPayload, "head_commit.author.name"),
                SafeGet<string>(pushPayload, "head_commit.author.email"),
                GetCommitsShas(pushPayload, "commits")
                );
        }

        private static List<string> GetCommitsShas(JObject jObject, string path)
        {
            var shas = new List<string>();

            if (jObject == null || !jObject.TryGetValue(path, out var commits)) return shas;
            if (!(commits is JArray jArray)) return null;

            foreach (var commit in jArray)
            {
                var token = commit as JObject;
                var sha = SafeGet<string>(token, "id");
                shas.Add(sha);
            }

            return shas;
        }

    }
}