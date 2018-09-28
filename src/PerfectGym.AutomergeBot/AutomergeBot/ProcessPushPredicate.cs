using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PerfectGym.AutomergeBot.Models;
using PerfectGym.AutomergeBot.RepositoryConnection;

namespace PerfectGym.AutomergeBot.AutomergeBot
{
    public interface IProcessPushPredicate
    {
        bool CanProcessPush(PushInfoModel pushInfo, IRepositoryConnectionContext repoContext);
    }

    public class ProcessPushPredicate : IProcessPushPredicate
    {
        private readonly ILogger<ProcessPushPredicate> _logger;
        private readonly AutomergeBotConfiguration _cfg;

        public ProcessPushPredicate(
            ILogger<ProcessPushPredicate> logger,
            IOptionsMonitor<AutomergeBotConfiguration> cfg)
        {
            _logger = logger;
            _cfg = cfg.CurrentValue;
        }

        public bool CanProcessPush(PushInfoModel pushInfo, IRepositoryConnectionContext repoContext)
        {
            if (!IsMonitoredRepository(pushInfo, repoContext)) return false;
            if (!IsPushAddingNewCommits(pushInfo)) return false;
            if (IsPushedToIgnoredBranch(pushInfo)) return false;
            if (!IsAutomergeEnabledForAuthorOfLastestCommit(pushInfo)) return false;

            return true;
        }

        private bool IsMonitoredRepository(PushInfoModel pushInfo, IRepositoryConnectionContext repoContext)
        {
            if (repoContext.IsMonitoredRepository(pushInfo.RepositoryId))
            {
                return true;
            }

            _logger.LogDebug("Push is not from monitored repository ({RepositoryOwner}/{RepositoryName})",
                _cfg.RepositoryOwner, _cfg.RepositoryName);
            return false;
        }

        private bool IsPushAddingNewCommits(PushInfoModel pushInfo)
        {
            if (!pushInfo.Deleted && !pushInfo.Forced && pushInfo.Ref.StartsWith(Consts.RefsHeads))
            {
                return true;
            }

            _logger.LogDebug("Push does not add new commits. Perhaps it is force push, tags push etc.");
            return false;
        }

        private bool IsPushedToIgnoredBranch(PushInfoModel pushInfo)
        {
            var branchName = pushInfo.GetPushedBranchName();
            if (branchName.Name.StartsWith(_cfg.CreatedBranchesPrefix))
            {
                _logger.LogDebug("Push is on branch {branchName} which is created by us", branchName);
                return true;
            }

            return false;

        }

        private bool IsAutomergeEnabledForAuthorOfLastestCommit(PushInfoModel pushInfo)
        {
            if (!(_cfg.AutomergeOnlyForAuthors ?? new List<string>()).Any())
                return true;

            var headCommitAuthor = pushInfo.HeadCommitAuthorUserName.Trim();

            if (headCommitAuthor == _cfg.AutomergeBotGitHubUserName)
                return true;

            var enabledForAuthor = _cfg.AutomergeOnlyForAuthors.Any(x => x.Trim() == headCommitAuthor);
            if (!enabledForAuthor)
            {
                _logger.LogWarning("Automerging is not enabled for commit author {commitAuthorUserName}", pushInfo.HeadCommitAuthorUserName);
            }
            return enabledForAuthor;
        }
    }
}