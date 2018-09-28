using System;
using System.Collections.Generic;
using System.Linq;
using PerfectGym.AutomergeBot.Models;

namespace PerfectGym.AutomergeBot
{
    public class AutomergeBotConfiguration
    {
        public string CreatedBranchesPrefix { get; set; } = "AutomergeBot/";
        public string RepositoryName { get; set; } = "";
        public string RepositoryOwner { get; set; } = "";
        public string AuthToken { get; set; } = "";
        public string WebHookSecret { get; set; }
        public List<string> MergeDirections { get; set; }
        public List<string> AutomergeOnlyForAuthors { get; set; }
        public string AutomergeBotGitHubUserName { get; set; }
        public PullRequestGovernorConfiguration PullRequestGovernorConfiguration { get; set; }


        public (string from, string to)[] MergeDirectionsParsed => ParseMergeDirections();

        private (string from, string to)[] ParseMergeDirections()
        {
            try
            {
                return (MergeDirections ?? new List<string>())
                    .Select(x =>
                    {
                        var strings = x.Split(new[] { "->" }, StringSplitOptions.None);
                        return (strings[0].Trim(), strings[1].Trim());
                    })
                    .ToArray();

            }
            catch (Exception e)
            {
                throw new Exception($"Could parse {nameof(MergeDirections)} provided in appsettings json.", e);
            }
        }
    }
}
