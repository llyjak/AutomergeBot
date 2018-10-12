using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PerfectGym.AutomergeBot.MergingBranches;
using SlackClientStandard;

namespace PerfectGym.AutomergeBot.SlackClient
{
    public class SlackClientProvider
    {
        private readonly ILogger<UserNotifier> _logger;
        private readonly SlackClientStandard.SlackClientProvider _slackClientProvider = new SlackClientStandard.SlackClientProvider();
        private readonly AutomergeBotConfiguration _cfg;

        public SlackClientProvider(
            ILogger<UserNotifier> logger,
            IOptionsMonitor<AutomergeBotConfiguration> cfg)
        {
            _logger = logger;
            _cfg = cfg.CurrentValue;
        }

        public ISlackClient Create()
        {
            if (IsSlackConfigured())
            {
                return CreateSlackClient();
            }

            _logger.LogDebug($"Use {nameof(NullSlackClient)} because Slack configuration is missing or incomplete");
            return new NullSlackClient();
        }

        private bool IsSlackConfigured()
        {
            return !string.IsNullOrWhiteSpace(_cfg.PullRequestGovernorConfiguration?.SlackToken) &&
                   !string.IsNullOrWhiteSpace(_cfg.PullRequestGovernorConfiguration?.SlackChannels);
        }


        private ISlackClient CreateSlackClient()
        {
            return _slackClientProvider.CreateClient(
                _cfg.PullRequestGovernorConfiguration.SlackToken,
                _cfg.PullRequestGovernorConfiguration.SlackChannels,
                _cfg.AutomergeBotGitHubUserName);
        }
    }
}