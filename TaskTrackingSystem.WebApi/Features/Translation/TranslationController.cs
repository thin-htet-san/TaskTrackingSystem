using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskTrackingSystem.Shared.Localization;

namespace TaskTrackingSystem.WebApi.Features.Translation;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class TranslationController : ControllerBase
{
    private readonly IContentTranslationService translationService;

    public TranslationController(IContentTranslationService translationService)
    {
        this.translationService = translationService;
    }

    [HttpPost("generate")]
    public async Task<ActionResult<TranslationResult>> Generate(
        [FromBody] TranslationRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
        {
            return BadRequest(new TranslationResult(false, null, "Text is required.", "none", false));
        }

        var result = request.IsName
            ? await translationService.TransliterateNameAsync(request.Text, request.SourceLanguage, request.TargetLanguage, cancellationToken)
            : await translationService.TranslateAsync(request.Text, request.SourceLanguage, request.TargetLanguage, cancellationToken);

        return Ok(result);
    }
}
