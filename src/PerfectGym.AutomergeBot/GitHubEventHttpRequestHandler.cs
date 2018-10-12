using System.IO;
using System.Threading.Tasks;
using PerfectGym.AutomergeBot.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PerfectGym.AutomergeBot.MergingBranches;

namespace PerfectGym.AutomergeBot
{
    public interface IGitHubEventHttpRequestHandler
    {
        Task ProcessRequest(HttpContext context);
    }

    public class GitHubEventHttpRequestHandler : IGitHubEventHttpRequestHandler
    {
        private readonly ILogger<GitHubEventHttpRequestHandler> _logger;
        private readonly SecretValidator _secretValidator;

        public GitHubEventHttpRequestHandler(IOptionsMonitor<AutomergeBotConfiguration> configuration, ILogger<GitHubEventHttpRequestHandler> logger)
        {
            _logger = logger;
            _secretValidator = new SecretValidator(configuration.CurrentValue.WebHookSecret);
        }

        public async Task ProcessRequest(HttpContext context)
        {
            using (_logger.BeginScope("{X-GitHub-Delivery}", context.Request.Headers[Consts.GitHubDeliveryRequestHeaderName]))
            {
                var body = GetRequestBodyAsString(context.Request);

                if (!IsRequestAuthorized(context, body))
                {
                    context.Response.StatusCode = 403;
                    await context.Response.WriteAsync("BAD");
                    return;
                }

                if (!TryExecuteHandler(context, body, out var eventName))
                {
                    _logger.LogWarning("There is no implemented handler for GitHub Event {eventName}", eventName);

                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsync("BAD");
                    return;
                }

                await context.Response.WriteAsync("OK");
            }
        }

        private bool IsRequestAuthorized(HttpContext context, string body)
        {
            var isRequestAuthorized = _secretValidator.VerifySecret(context.Request.Headers[Consts.GitHubSignatureRequestHeaderName], body);
            if (!isRequestAuthorized)
            {
                _logger.LogError("Request will not be processed. Request signature does not match configured webhook secret.");
            }
            return isRequestAuthorized;
        }

        private string GetRequestBodyAsString(HttpRequest request)
        {
            string body;
            using (var streamReader = new StreamReader(request.Body))
            {
                body = streamReader.ReadToEnd();
            }
            _logger.LogTrace("Received HTTP request. Body: {body}", body);
            return body;
        }

        private static bool TryExecuteHandler(HttpContext context, string requestBody, out string eventName)
        {
            eventName = context.Request.Headers[Consts.GitHubEventRequestHeaderName];
            if (eventName == Consts.GitHubPushEventName)
            {
                HandlePushNotification(context, requestBody);
                return true;
            }
            if (eventName == Consts.GitHubPingEventName)
            {
                return true;
            }

            return false;
        }

        private static void HandlePushNotification(HttpContext context, string payloadJson)
        {
            var pushPayload = JsonConvert.DeserializeObject<JObject>(payloadJson);
            var pushHandler = context.RequestServices.GetRequiredService<MergingBranchesPushHandler>();
            var pushInfoModel = PushInfoModel.CreateFromPayload(pushPayload);

            pushHandler.Handle(pushInfoModel);
        }

        
    }
}