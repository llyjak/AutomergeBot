using System;
using System.Collections.Generic;
using PerfectGym.AutomergeBot.Models;
using Octokit;

namespace PerfectGym.AutomergeBot.RepositoryConnectionContext
{
    public interface IRepositoryConnectionContext : IDisposable
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pullRequestNumber"></param>
        /// <param name="mergeCommitMessage"></param>
        /// <returns><c>true</c> if successfully merged</returns>
        bool MergePullRequest(int pullRequestNumber, string mergeCommitMessage);

        bool IsMonitoredRepository(int repositoryId);
        PullRequest CreatePullRequest(BranchName sourceBranch, BranchName destinationBranch, string title, string body);
        void CreateBranch(BranchName branchName, string commitSha);
        void RemoveBranch(BranchName branchName);
        void AddReviewerToPullRequest(int pullRequestNumber, string[] userNames);
        void AssignUsersToPullRequest(int pullRequestNumber, string[] userNames);
        string GetCommitMessage(string pushInfoHeadCommitSha);
        void AddPullRequestComment(int pullRequestNumber, string comment);
        IReadOnlyList<Branch> GetAllBranches();
        IEnumerable<GitReference> GetCommitParents(string pushInfoHeadCommitSha);
    }
}