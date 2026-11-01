using System.Text;
using System.Text.Json;

using Microsoft.AspNetCore.Mvc;

using Revathryn.Contracts.Github;
using Revathryn.Internal;

using static Revathryn.Contracts.Consts;

namespace Revathryn.Controllers
{
    [ApiController]
    [ValidateGitHubSignature]
    public sealed class GitHubWebhookController : ControllerBase
    {
        private const string EventHeader = "X-GitHub-Event";
        private const string GitEventNamePing = "ping";
        private const string GitEventNameProject = "project";
        private const string GitEventNameLabel = "label";
        private const string GitEventNameMilestone = "milestone";
        private const string GitEventNamePublic = "public";
        private const string GitEventNameRepository = "repository";
        private const string GitEventNameMembership = "membership";
        private const string GitEventNameMember = "member";
        private const string GitEventNameStatus = "status";
        private const string GitEventNameDeployStatus = "deployment_status";
        private const string GitEventNameDeployment = "deployment";
        private const string GitEventNameCheckSuite = "check_suite";
        private const string GitEventNameCheckRun = "check_run";
        private const string GitEventNameWorkflowJob = "workflow_job";
        private const string GitEventNameWorkflowRun = "workflow_run";
        private const string GitEventNameWatch = "watch";
        private const string GitEventNameStar = "star";
        private const string GitEventNameFork = "fork";
        private const string GitEventNameReleast = "release";
        private const string GitEventNameDelete = "delete";
        private const string GitEventNameCreate = "create";
        private const string GitEventNameCommitComment = "commit_comment";
        private const string GitEventNameIssueComment = "issue_comment";
        private const string GitEventNameIssues = "issues";
        private const string GitEventNamePRComment = "pull_request_review_comment";
        private const string GitEventNamePRReview = "pull_request_review";
        private const string GitEventNamePullRequest = "pull_request";
        private const string GitEventNamePush = "push";
        private const string GitBodyPayload = "payload=";
        private const string ApplicationFormUrlEncoded = "application/x-www-form-urlencoded";
        private readonly PluginManager.Abstractions.ILogger _logger;
        private readonly IWebhookQueueService _queue;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public GitHubWebhookController(PluginManager.Abstractions.ILogger logger,
            IWebhookQueueService queue)
        {
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(queue);

            _logger = logger;
            _queue = queue;
        }

        /// <summary>
        /// Receives all GitHub webhook events for a given repository.
        /// Configure your GitHub webhook URL as: https://&lt;host&gt;/ai/{repositoryName}
        /// </summary>
        [HttpPost("git/{repositoryName}")]
        [HttpPost("api/{repositoryName}")]
        public async Task<IActionResult> ReceiveWebhook(string repositoryName)
        {
            if (!Request.Headers.TryGetValue(EventHeader, out var eventType) || string.IsNullOrEmpty(eventType))
            {
                _logger.AddToLog(PluginManager.LogLevel.Warning, $"Webhook received for '{repositoryName}' with no {EventHeader} header.");
                return BadRequest($"Missing {EventHeader} header.");
            }

            var eventName = eventType.ToString();

            Request.Body.Position = 0;
            using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
            var rawBody = await reader.ReadToEndAsync();

            var body = Request.ContentType != null && Request.ContentType.StartsWith(ApplicationFormUrlEncoded, StringComparison.OrdinalIgnoreCase)
                ? Uri.UnescapeDataString(rawBody.StartsWith(GitBodyPayload, StringComparison.OrdinalIgnoreCase) ? rawBody[GitBodyPayload.Length..] : rawBody).Replace(CharPlus, CharSpace)
                : rawBody;

            _logger.AddToLog(PluginManager.LogLevel.Information, $"GitHub event '{eventName}' received for repository '{repositoryName}'.");

            var payload = DeserializePayload(eventName, body);

            if (payload is null)
            {
                _logger.AddToLog(PluginManager.LogLevel.Warning, $"Unrecognised event type '{eventName}' for repository '{repositoryName}'. Raw body stored for reference.");
                return Ok(new { received = true, @event = eventName, repository = repositoryName, handled = false });
            }

            // Phase 2: enqueue for Phase 3 processing (skip ping — it needs no further work).
            if (eventName != GitEventNamePing)
            {
                await _queue.EnqueueAsync(repositoryName, eventName, body, HttpContext.RequestAborted);
                _logger.AddToLog(PluginManager.LogLevel.Information, $"Event '{eventName}' for '{repositoryName}' queued for processing.");
            }

            if (eventName == GitEventNamePing)
            {
                var ping = (PingPayload)payload;
                return Ok(new { received = true, @event = eventName, repository = repositoryName, zen = ping.Zen });
            }

            return Ok(new { received = true, @event = eventName, repository = repositoryName, handled = true });
        }

        private static GitHubWebhookPayload DeserializePayload(string eventName, string body)
        {
            return eventName switch
            {
                GitEventNamePing => Deserialize<PingPayload>(body),
                GitEventNamePush => Deserialize<PushPayload>(body),
                GitEventNamePullRequest => Deserialize<PullRequestPayload>(body),
                GitEventNamePRReview => Deserialize<PullRequestReviewPayload>(body),
                GitEventNamePRComment => Deserialize<PullRequestReviewCommentPayload>(body),
                GitEventNameIssues => Deserialize<IssuesPayload>(body),
                GitEventNameIssueComment => Deserialize<IssueCommentPayload>(body),
                GitEventNameCommitComment => Deserialize<CommitCommentPayload>(body),
                GitEventNameCreate => Deserialize<CreatePayload>(body),
                GitEventNameDelete => Deserialize<DeletePayload>(body),
                GitEventNameReleast => Deserialize<ReleasePayload>(body),
                GitEventNameFork => Deserialize<ForkPayload>(body),
                GitEventNameStar => Deserialize<StarPayload>(body),
                GitEventNameWatch => Deserialize<WatchPayload>(body),
                GitEventNameWorkflowRun => Deserialize<WorkflowRunPayload>(body),
                GitEventNameWorkflowJob => Deserialize<WorkflowJobPayload>(body),
                GitEventNameCheckRun => Deserialize<CheckRunPayload>(body),
                GitEventNameCheckSuite => Deserialize<CheckSuitePayload>(body),
                GitEventNameDeployment => Deserialize<DeploymentPayload>(body),
                GitEventNameDeployStatus => Deserialize<DeploymentStatusPayload>(body),
                GitEventNameStatus => Deserialize<StatusPayload>(body),
                GitEventNameMember => Deserialize<MemberPayload>(body),
                GitEventNameMembership => Deserialize<MembershipPayload>(body),
                GitEventNameRepository => Deserialize<RepositoryPayload>(body),
                GitEventNamePublic => Deserialize<PublicPayload>(body),
                GitEventNameMilestone => Deserialize<MilestonePayload>(body),
                GitEventNameLabel => Deserialize<LabelPayload>(body),
                GitEventNameProject => Deserialize<ProjectPayload>(body),
                _ => null
            };
        }

        private static T Deserialize<T>(string body) where T : GitHubWebhookPayload
            => JsonSerializer.Deserialize<T>(body, JsonOptions);
    }
}
