using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Octokit;
using PerfectGym.AutomergeBot.Models;

namespace PerfectGym.AutomergeBot.RepositoryConnection
{
    public class RepositoryConnectionContext : IRepositoryConnectionContext
    {
        private readonly ILogger _logger;
        private readonly string _repositoryName;
        private readonly string _repositoryOwner;
        private readonly string _authToken;

        public RepositoryConnectionContext(ILogger logger, string repositoryName, string repositoryOwner, string authToken)
        {
            _logger = logger;
            _repositoryName = repositoryName;
            _repositoryOwner = repositoryOwner;
            _authToken = authToken;
        }

        private GitHubClient CreateGitHubClient()
        {
            var client = new GitHubClient(new ProductHeaderValue($"{_repositoryOwner}_{_repositoryName}"));

            var tokenAuth = new Credentials(_authToken);
            client.Credentials = tokenAuth;
            return client;
        }

        public bool MergePullRequest(int pullRequestNumber, string mergeCommitMessage)
        {
            try
            {
                var client = CreateGitHubClient();
                _logger.LogDebug("Trying to merge pull request number {pullRequestNumber}", pullRequestNumber);
                var mergePullRequestModel = new MergePullRequest()
                {
                    MergeMethod = PullRequestMergeMethod.Merge,
                    CommitMessage = mergeCommitMessage
                };
                var pullRequestMerge = client.PullRequest.Merge(_repositoryOwner, _repositoryName, pullRequestNumber, mergePullRequestModel).Result;
                _logger.LogDebug("Pull request {pullRequestNumber} successfully merged", pullRequestNumber);
                return true;
            }
            catch (AggregateException e) when (e.InnerExceptions.OfType<PullRequestNotMergeableException>().Any())
            {
                var ex = e.InnerExceptions.OfType<PullRequestNotMergeableException>().First();

                _logger.LogDebug("Pull request {pullRequestNumber} has not been merged because it is not mergeable. GitHub error message: {gitHubErrorMessage} {apiErrors}",
                    pullRequestNumber,
                    ex.Message,
                    ex.ApiError?.Errors);
                return false;
            }
        }

        public bool IsMonitoredRepository(int repositoryId)
        {
            var client = CreateGitHubClient();
            return client.Repository.Get(_repositoryOwner, _repositoryName).Result.Id == repositoryId;
        }

        public PullRequest CreatePullRequest(BranchName sourceBranch, BranchName destinationBranch, string title, string body)
        {
            _logger.LogDebug("Creating pull request from {sourceBranchName} to {destinationBranchName}", sourceBranch, destinationBranch);

            var client = CreateGitHubClient();

            var newPullRequestModel = new NewPullRequest(title, sourceBranch.GitRef, destinationBranch.GitRef)
            {
                Body = body
            };
            var pullRequest = client.PullRequest.Create(_repositoryOwner, _repositoryName, newPullRequestModel).Result;
            _logger.LogTrace("Created pull request {pullRequestNumber} from {sourceBranchName} to {destinationBranchName}", pullRequest.Number, sourceBranch, destinationBranch);

            return pullRequest;
        }

        public void CreateBranch(BranchName branchName, string commitSha)
        {
            var newReference = new NewReference(branchName.GitRef, commitSha);
            _logger.LogDebug("Creating branch {branchName}", branchName);

            var client = CreateGitHubClient();
            client.Git.Reference.Create(_repositoryOwner, _repositoryName, newReference).Wait();
        }

        public void RemoveBranch(BranchName branchName)
        {
            _logger.LogDebug("Deleting branch {branchName}", branchName);
            CreateGitHubClient().Git.Reference.Delete(_repositoryOwner, _repositoryName, RemoveRefsPrefix(branchName.GitRef)).Wait();
        }

        public void AddReviewerToPullRequest(int pullRequestNumber, string[] userNames)
        {
            _logger.LogDebug("Adding reviewers to pull request {pullRequestNumber}, user names: {userNames}", pullRequestNumber, userNames);
            var pullRequestReviewRequestModel = new PullRequestReviewRequest(userNames);
            CreateGitHubClient().PullRequest.ReviewRequest.Create(_repositoryOwner, _repositoryName, pullRequestNumber, pullRequestReviewRequestModel);
        }

        public void AssignUsersToPullRequest(int pullRequestNumber, string[] userNames)
        {
            _logger.LogDebug("Adding assignee to pull request {pullRequestNumber}, user names: {userNames}", pullRequestNumber, userNames);
            var assigneesUpdateModel = new AssigneesUpdate(userNames);
            CreateGitHubClient().Issue.Assignee.AddAssignees(_repositoryOwner, _repositoryName, pullRequestNumber, assigneesUpdateModel);
        }

        public string GetCommitMessage(string pushInfoHeadCommitSha)
        {
            _logger.LogDebug("Getting commit {commitSha} message", pushInfoHeadCommitSha);
            var headCommit = CreateGitHubClient().Git.Commit.Get(_repositoryOwner, _repositoryName, pushInfoHeadCommitSha).Result;
            return headCommit.Message;
        }

        public void AddPullRequestComment(int pullRequestNumber, string comment)
        {
            _logger.LogDebug("Adding pull request {pullRequestNumber} comment", pullRequestNumber);
            CreateGitHubClient().Issue.Comment.Create(_repositoryOwner, _repositoryName, pullRequestNumber, comment).Wait();
        }


        public IReadOnlyList<Branch> GetAllBranches()
        {
            _logger.LogDebug("Getting all branches from {_repositoryName}", _repositoryName);
            return CreateGitHubClient().Repository.Branch.GetAll(_repositoryOwner, _repositoryName).Result;
        }


        public IEnumerable<GitReference> GetCommitParents(string pushInfoHeadCommitSha)
        {
            _logger.LogDebug("Getting parents for commit {mergeCommitSha}", pushInfoHeadCommitSha);
            var headCommit = CreateGitHubClient().Git.Commit.Get(_repositoryOwner, _repositoryName, pushInfoHeadCommitSha).Result;
            return headCommit.Parents;
        }

        public IReadOnlyList<PullRequest> GetOpenPullRequests()
        {
            var client = CreateGitHubClient();
            var request = new PullRequestRequest
            {
                State = ItemStateFilter.Open,
            };
            var openPullRequests = client.PullRequest.GetAllForRepository(_repositoryOwner, _repositoryName, request)
                .Result;
            return openPullRequests;
        }

        /// <summary>
        /// Removes "refs/" prefix from git ref string.
        /// Probably there is bug in a Octokit's Delete method.
        /// </summary>
        /// <param name="branchNameGitRef"></param>
        /// <returns></returns>
        private static string RemoveRefsPrefix(string branchNameGitRef)
        {
            return branchNameGitRef.Substring("refs/".Length);
        }

        public void Dispose()
        {
            //noop
        }
    }
}