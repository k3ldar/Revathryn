using System.Security.Cryptography;
using System.Text;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Primitives;

namespace Revathryn.Internal
{
    /// <summary>
    /// Action filter that validates the GitHub webhook HMAC-SHA256 signature
    /// supplied in the <c>X-Hub-Signature-256</c> request header.
    /// The shared secret is read from <c>GitHub:WebhookSecret</c> in configuration.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    internal sealed class ValidateGitHubSignatureAttribute : Attribute, IAsyncActionFilter
    {
        private const string SignatureHeader = "X-Hub-Signature-256";
        private const string SignaturePrefix = "sha256=";
        private const string GitHubWebhookSecret = "GitHub:WebhookSecret";
        private const string MissingSignature = "Missing GitHub signature header.";
        private const string InvalidSignature = "Invalid GitHub signature format.";
        private const string SignatureValidationFailed = "GitHub signature validation failed.";

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            IConfiguration config = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var secret = config[GitHubWebhookSecret];

            if (string.IsNullOrWhiteSpace(secret))
            {
                // No secret configured — skip validation (useful during local development)
                await next();
                return;
            }

            if (!context.HttpContext.Request.Headers.TryGetValue(SignatureHeader, out StringValues signatureHeader)
                || string.IsNullOrEmpty(signatureHeader))
            {
                context.Result = new UnauthorizedObjectResult(MissingSignature);
                return;
            }

            var receivedSignature = signatureHeader.ToString();

            if (!receivedSignature.StartsWith(SignaturePrefix, StringComparison.OrdinalIgnoreCase))
            {
                context.Result = new UnauthorizedObjectResult(InvalidSignature);
                return;
            }

            // Raw body has already been buffered via EnableBuffering() in Program.cs.
            context.HttpContext.Request.Body.Position = 0;
            using var reader = new StreamReader(context.HttpContext.Request.Body, Encoding.UTF8, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            context.HttpContext.Request.Body.Position = 0;

            var secretBytes = Encoding.UTF8.GetBytes(secret);
            var bodyBytes = Encoding.UTF8.GetBytes(body);

            var computedHash = HMACSHA256.HashData(secretBytes, bodyBytes);
            var computedSignature = SignaturePrefix + Convert.ToHexString(computedHash).ToLowerInvariant();

            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(computedSignature),
                    Encoding.UTF8.GetBytes(receivedSignature)))
            {
                context.Result = new UnauthorizedObjectResult(SignatureValidationFailed);
                return;
            }

            await next();
        }
    }
}
