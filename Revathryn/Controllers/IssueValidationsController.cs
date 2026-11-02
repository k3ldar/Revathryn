using Microsoft.AspNetCore.Mvc;

using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Services;
using Revathryn.Models.IssueValidations;

namespace Revathryn.Controllers
{
    /// <summary>
    /// JSON API for independent AI issue validation. Requests only queue work; execution happens in the
    /// background worker. Responses never include prompts, raw provider output or credentials.
    /// </summary>
    [ApiController]
    [Route("api/issues/{findingId:long}/validations")]
    public sealed class IssueValidationsController : ControllerBase
    {
        private readonly IIssueValidationService _validationService;

        /// <summary>Initialises a new controller.</summary>
        public IssueValidationsController(IIssueValidationService validationService)
        {
            _validationService = validationService;
        }

        /// <summary>Queues a new validation, or returns the one already running for the issue.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Start(long findingId, CancellationToken cancellationToken)
        {
            IssueValidationRequestResult result = await _validationService.RequestAsync(findingId, cancellationToken);
            return await ToResponseAsync(result, cancellationToken);
        }

        /// <summary>Lists the validation history for an issue, newest first.</summary>
        [HttpGet]
        public async Task<IActionResult> List(long findingId, CancellationToken cancellationToken)
        {
            IReadOnlyList<IssueValidation> validations = await _validationService.GetByFindingAsync(findingId, cancellationToken);

            if (validations.Count == 0)
            {
                return Ok(Array.Empty<IssueValidationDto>());
            }

            long latestId = validations.Max(validation => validation.Id);
            return Ok(validations.Select(validation => IssueValidationDto.FromModel(validation, validation.Id == latestId)).ToList());
        }

        /// <summary>Returns the most recent validation of an issue.</summary>
        [HttpGet("latest")]
        public async Task<IActionResult> Latest(long findingId, CancellationToken cancellationToken)
        {
            IssueValidation validation = await _validationService.GetLatestAsync(findingId, cancellationToken);
            return validation is null ? NotFound() : Ok(IssueValidationDto.FromModel(validation, true));
        }

        /// <summary>Returns a single validation, preserving its original result.</summary>
        [HttpGet("{validationId:long}")]
        public async Task<IActionResult> Get(long findingId, long validationId, CancellationToken cancellationToken)
        {
            IssueValidation validation = await _validationService.GetByIdAsync(validationId, cancellationToken);

            if (validation is null || validation.FindingId != findingId)
            {
                return NotFound();
            }

            IssueValidation latest = await _validationService.GetLatestAsync(findingId, cancellationToken);
            return Ok(IssueValidationDto.FromModel(validation, latest?.Id == validation.Id));
        }

        /// <summary>Retries a failed validation by queueing a new historical record.</summary>
        [HttpPost("{validationId:long}/retry")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Retry(long findingId, long validationId, CancellationToken cancellationToken)
        {
            IssueValidation existing = await _validationService.GetByIdAsync(validationId, cancellationToken);

            if (existing is null || existing.FindingId != findingId)
            {
                return NotFound();
            }

            IssueValidationRequestResult result = await _validationService.RetryAsync(validationId, cancellationToken);
            return await ToResponseAsync(result, cancellationToken);
        }

        private async Task<IActionResult> ToResponseAsync(IssueValidationRequestResult result, CancellationToken cancellationToken)
        {
            if (result.Validation is not null)
            {
                IssueValidation latest = await _validationService.GetLatestAsync(result.Validation.FindingId, cancellationToken);
                IssueValidationDto dto = IssueValidationDto.FromModel(result.Validation, latest?.Id == result.Validation.Id);

                return result.Created
                    ? Accepted(Url.Action(nameof(Get), new { findingId = dto.FindingId, validationId = dto.Id }), dto)
                    : Ok(dto);
            }

            return result.Rejection switch
            {
                IssueValidationRejection.IssueNotFound or IssueValidationRejection.RepositoryNotFound => NotFound(new { message = result.Message }),
                IssueValidationRejection.NotCodeIssue => UnprocessableEntity(new { message = result.Message }),
                _ => Conflict(new { message = result.Message })
            };
        }
    }
}
