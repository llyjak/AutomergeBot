using System.Collections.Generic;
using System.Linq;
using Octokit;
using PerfectGym.AutomergeBot.Models;

namespace PerfectGym.AutomergeBot.AutomergeBot
{
    public class BranchesFilter
    {
        private readonly IReadOnlyList<Branch> _allBranches;
        private readonly AutomergeBotConfiguration _cfg;

        public BranchesFilter(IReadOnlyList<Branch> allBranches, AutomergeBotConfiguration cfg)
        {
            _allBranches = allBranches;
            _cfg = cfg;
        }

        public List<BranchName> GetAllBranchesToDelete(List<string> commitsShas)
        {
            var branchesToDelete = new List<BranchName>();

            foreach (var commitSha in commitsShas)
            {
                if (IsBranchHead(commitSha, out var branch) &&
                    branch.Name.StartsWith(_cfg.CreatedBranchesPrefix))
                {
                    branchesToDelete.Add(new BranchName(branch.Name));
                }
            }

            return branchesToDelete;
        }

        private bool IsBranchHead(string commitSha, out Branch branch)
        {
            branch = _allBranches.FirstOrDefault(b => b.Commit.Sha == commitSha);
            return branch != null;
        }
    }
}