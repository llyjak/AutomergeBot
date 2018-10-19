using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Octokit;
using PerfectGym.AutomergeBot.Models;

namespace PerfectGym.AutomergeBot.RepositoryConnection
{
    public class RepositoryConnectionContextExceptionHandlingWrapper : IRepositoryConnectionContext
    {
        private readonly ILogger _logger;
        private readonly IRepositoryConnectionContext _repositoryConnectionContext;

        public RepositoryConnectionContextExceptionHandlingWrapper(
            ILogger logger,
            IRepositoryConnectionContext repositoryConnectionContext)
        {
            _logger = logger;
            _repositoryConnectionContext = repositoryConnectionContext;
        }

        private void LogAggregateException(AggregateException exception)
        {
            foreach (var innerException in exception.InnerExceptions)
            {
                if (innerException is ApiException apiException)
                {
                    _logger.LogError(innerException, "Failed performing repository call. GitHub Api error details message: {gitHubErrorMessage}, api errors: {@apiErrors}",
                        apiException.Message, apiException.ApiError?.Errors);
                }
                else
                {
                    LogException(innerException);
                }
            }
        }

        private void LogException(Exception innerException)
        {
            _logger.LogError(innerException, "Failed performing repository call");
        }

        private T Exec<T>(Func<IRepositoryConnectionContext, T> action)
        {
            try
            {
                return action(_repositoryConnectionContext);
            }
            catch (AggregateException e)
            {
                LogAggregateException(e);
                throw;

            }
            catch (Exception e)
            {
                LogException(e);
                throw;
            }
        }

        private void Exec(Action<IRepositoryConnectionContext> action)
        {
            try
            {
                action(_repositoryConnectionContext);
            }
            catch (AggregateException e)
            {
                LogAggregateException(e);
                throw;

            }
            catch (Exception e)
            {
                LogException(e);
                throw;
            }
        }

        public void Dispose()
        {
            _repositoryConnectionContext.Dispose();
        }

        public bool MergePullRequest(int pullRequestNumber, string mergeCommitMessage)
        {
            return Exec(r => r.MergePullRequest(pullRequestNumber, mergeCommitMessage));
        }

        public bool IsMonitoredRepository(int repositoryId)
        {
            return Exec(r => r.IsMonitoredRepository(repositoryId));
        }

        public PullRequest CreatePullRequest(BranchName sourceBranch, BranchName destinationBranch, string title, string body)
        {
            return Exec(r => r.CreatePullRequest(sourceBranch, destinationBranch, title, body));
        }

        public void CreateBranch(BranchName branchName, string commitSha)
        {
            Exec(r => r.CreateBranch(branchName, commitSha));
        }

        public void RemoveBranch(BranchName branchName)
        {
            Exec(r => r.RemoveBranch(branchName));
        }

        public void AddReviewerToPullRequest(int pullRequestNumber, string[] userNames)
        {
            Exec(r => r.AddReviewerToPullRequest(pullRequestNumber, userNames));
        }

        public void AssignUsersToPullRequest(int pullRequestNumber, string[] userNames)
        {
            Exec(r => r.AssignUsersToPullRequest(pullRequestNumber, userNames));
        }

        public string GetCommitMessage(string pushInfoHeadCommitSha)
        {
            return Exec(r => r.GetCommitMessage(pushInfoHeadCommitSha));
        }

        public void AddPullRequestComment(int pullRequestNumber, string comment)
        {
            Exec(r => r.AddPullRequestComment(pullRequestNumber, comment));
        }

        public IReadOnlyList<Branch> GetAllBranches()
        {
            return Exec(r => r.GetAllBranches());
        }

        public IEnumerable<GitReference> GetCommitParents(string pushInfoHeadCommitSha)
        {
            return Exec(r => r.GetCommitParents(pushInfoHeadCommitSha));
        }

        public IReadOnlyList<PullRequest> GetOpenPullRequests()
        {
            return Exec(r => r.GetOpenPullRequests());
        }
    }
}