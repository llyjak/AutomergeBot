using System.Linq;
using Octokit;

namespace PerfectGym.AutomergeBot
{
    public static class ApiValidationExceptionExtensions
    {
        public static string FirstErrorMessageSafe(this ApiValidationException exception)
        {
            return FirstErrorMessageSafe(exception.ApiError);
        }

        public static string FirstErrorMessageSafe(this ApiError apiError)
        {
            if (apiError == null) return null;
            if (apiError.Errors == null) return apiError.Message;
            var firstError = apiError.Errors.FirstOrDefault();
            return firstError == null ? null : firstError.Message;
        }

    }
}