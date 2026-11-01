using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Revathryn.Contracts.Github;
using Revathryn.Controllers;

namespace Revathryn.Tests
{
    [TestClass]
    [ExcludeFromCodeCoverage]
    public class GitHubWebhookControllerTests
    {
        [TestMethod]
        public void Constructor_ThrowsWhenLoggerIsNull()
        {
            FakeWebhookQueue queue = new();

            Assert.ThrowsExactly<ArgumentNullException>(() =>
            {
                _ = new GitHubWebhookController(null, queue);
            });
        }

        [TestMethod]
        public void Constructor_ThrowsWhenQueueIsNull()
        {
            FakeLogger logger = new();

            Assert.ThrowsExactly<ArgumentNullException>(() =>
            {
                _ = new GitHubWebhookController(logger, null);
            });
        }

        [TestMethod]
        public async Task ReceiveWebhook_ReturnsBadRequestWhenEventHeaderMissing()
        {
            FakeLogger logger = new();
            FakeWebhookQueue queue = new();

            GitHubWebhookController controller = CreateController(logger, queue, out DefaultHttpContext httpContext);
            SetBody(httpContext, "{}", null);

            IActionResult result = await controller.ReceiveWebhook("repo");

            Assert.IsInstanceOfType<BadRequestObjectResult>(result);
            Assert.AreEqual(0, queue.EnqueueCallCount);
        }

        [TestMethod]
        public async Task ReceiveWebhook_PingReturnsOkWithoutEnqueueing()
        {
            FakeLogger logger = new();
            FakeWebhookQueue queue = new();

            GitHubWebhookController controller = CreateController(logger, queue, out DefaultHttpContext httpContext);
            httpContext.Request.Headers["X-GitHub-Event"] = "ping";
            SetBody(httpContext, "{\"zen\":\"Keep it logically awesome.\"}", "application/json");

            IActionResult result = await controller.ReceiveWebhook("repo");

            OkObjectResult okResult = result as OkObjectResult;
            Assert.IsNotNull(okResult);
            Assert.AreEqual(0, queue.EnqueueCallCount);

            string json = JsonSerializer.Serialize(okResult.Value);
            Assert.IsTrue(json.Contains("Keep it logically awesome.", StringComparison.Ordinal));
        }

        [TestMethod]
        public async Task ReceiveWebhook_PushEnqueuesAndReturnsHandled()
        {
            FakeLogger logger = new();
            FakeWebhookQueue queue = new();

            GitHubWebhookController controller = CreateController(logger, queue, out DefaultHttpContext httpContext);
            httpContext.Request.Headers["X-GitHub-Event"] = "push";
            SetBody(httpContext, "{\"ref\":\"refs/heads/main\"}", "application/json");

            IActionResult result = await controller.ReceiveWebhook("my-repo");

            OkObjectResult okResult = result as OkObjectResult;
            Assert.IsNotNull(okResult);
            Assert.AreEqual(1, queue.EnqueueCallCount);
            Assert.AreEqual("my-repo", queue.LastRepositoryName);
            Assert.AreEqual("push", queue.LastEventType);
            Assert.AreEqual("{\"ref\":\"refs/heads/main\"}", queue.LastRawBody);

            string json = JsonSerializer.Serialize(okResult.Value);
            Assert.IsTrue(json.Contains("\"handled\":true", StringComparison.Ordinal));
        }

        [TestMethod]
        public async Task ReceiveWebhook_UnknownEventReturnsHandledFalseWithoutEnqueueing()
        {
            FakeLogger logger = new();
            FakeWebhookQueue queue = new();

            GitHubWebhookController controller = CreateController(logger, queue, out DefaultHttpContext httpContext);
            httpContext.Request.Headers["X-GitHub-Event"] = "custom_event";
            SetBody(httpContext, "{}", "application/json");

            IActionResult result = await controller.ReceiveWebhook("repo");

            OkObjectResult okResult = result as OkObjectResult;
            Assert.IsNotNull(okResult);
            Assert.AreEqual(0, queue.EnqueueCallCount);

            string json = JsonSerializer.Serialize(okResult.Value);
            Assert.IsTrue(json.Contains("\"handled\":false", StringComparison.Ordinal));
        }

        [TestMethod]
        public async Task ReceiveWebhook_DecodesFormUrlEncodedBodyBeforeEnqueueing()
        {
            FakeLogger logger = new();
            FakeWebhookQueue queue = new();

            GitHubWebhookController controller = CreateController(logger, queue, out DefaultHttpContext httpContext);
            httpContext.Request.Headers["X-GitHub-Event"] = "push";
            SetBody(httpContext, "payload=%7B%22ref%22%3A%22refs%2Fheads%2Fmain%22%7D", "application/x-www-form-urlencoded");

            IActionResult result = await controller.ReceiveWebhook("repo");

            OkObjectResult okResult = result as OkObjectResult;
            Assert.IsNotNull(okResult);
            Assert.AreEqual(1, queue.EnqueueCallCount);
            Assert.AreEqual("{\"ref\":\"refs/heads/main\"}", queue.LastRawBody);
        }

        private static GitHubWebhookController CreateController(FakeLogger logger, FakeWebhookQueue queue, out DefaultHttpContext httpContext)
        {
            ControllerContext context = ControllerTestSupport.CreateControllerContext(out httpContext);

            return new GitHubWebhookController(logger, queue)
            {
                ControllerContext = context
            };
        }

        private static void SetBody(DefaultHttpContext httpContext, string body, string contentType)
        {
            httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
            httpContext.Request.ContentType = contentType;
        }

        private sealed class FakeLogger : PluginManager.Abstractions.ILogger
        {
            public List<string> Entries { get; } = [];

            public void AddToLog(in PluginManager.LogLevel logLevel, in string data)
            {
                Entries.Add($"{logLevel}: {data}");
            }

            public void AddToLog(in PluginManager.LogLevel logLevel, in Exception exception)
            {
                Entries.Add($"{logLevel}: {exception}");
            }

            public void AddToLog(in PluginManager.LogLevel logLevel, in Exception exception, string data)
            {
                Entries.Add($"{logLevel}: {data}");
            }

            public void AddToLog(in PluginManager.LogLevel logLevel, in string moduleName, in string data)
            {
                Entries.Add($"{logLevel} [{moduleName}]: {data}");
            }

            public void AddToLog(in PluginManager.LogLevel logLevel, in string moduleName, in Exception exception)
            {
                Entries.Add($"{logLevel} [{moduleName}]: {exception}");
            }

            public void AddToLog(in PluginManager.LogLevel logLevel, in string moduleName, in Exception exception, string data)
            {
                Entries.Add($"{logLevel} [{moduleName}]: {data}");
            }
        }

        private sealed class FakeWebhookQueue : IWebhookQueueService
        {
            public int EnqueueCallCount { get; private set; }

            public string LastRepositoryName { get; private set; }

            public string LastEventType { get; private set; }

            public string LastRawBody { get; private set; }

            public Task EnqueueAsync(string repositoryName, string eventType, string rawBody, CancellationToken cancellationToken = default)
            {
                EnqueueCallCount++;
                LastRepositoryName = repositoryName;
                LastEventType = eventType;
                LastRawBody = rawBody;
                return Task.CompletedTask;
            }

            public IReadOnlyList<WebhookQueueEntry> GetAll()
            {
                return [];
            }

            public IReadOnlyList<WebhookQueueEntry> GetPending()
            {
                return [];
            }

            public Task<bool> MarkProcessedAsync(Guid id, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(false);
            }

            public Task<int> PurgeProcessedAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(0);
            }
        }
    }
}
