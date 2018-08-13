using System;
using System.Collections.Generic;
using System.Text;

namespace PerfectGym.AutomergeBot.Models
{
    public struct PullRequestGovernorConfiguration
    {
        public string SlackToken { get; set; }
        public string SlackChannels { get; set; }
        public string PullRequestTimeLimit { get; set; }
        public string CheckFrequency { get; set; }

        public TimeSpan ParsedPullRequestTimeLimit => TimeSpan.TryParse(PullRequestTimeLimit, out var result) ? result : new TimeSpan(0, 15, 0);
        public TimeSpan ParsedCheckFrequency => TimeSpan.TryParse(CheckFrequency, out var result) ? result : new TimeSpan(0, 30, 0);
    }
}
