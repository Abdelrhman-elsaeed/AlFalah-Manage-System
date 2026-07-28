using AlFalah.Application.DTOs.ParentSurveys;
using AlFalah.Application.Interfaces;
using AlFalah.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AlFalah.Api.Controllers;

[ApiController]
[Route("api/v1/public/parent-surveys")]
[AllowAnonymous]
[EnableRateLimiting("public-surveys")]
public class PublicParentSurveysController : ControllerBase
{
    private readonly IParentSurveyService _service;

    public PublicParentSurveysController(IParentSurveyService service)
    {
        _service = service;
    }

    [HttpGet("{publicToken}")]
    public async Task<IActionResult> Get(string publicToken, CancellationToken cancellationToken)
    {
        var result = await _service.GetPublicAsync(publicToken, cancellationToken);
        return Ok(ApiResponse<PublicParentSurveyDto>.Success(result));
    }

    [HttpPost("{publicToken}/submissions")]
    public async Task<IActionResult> Submit(
        string publicToken,
        [FromBody] SubmitParentSurveyRequestDto request,
        CancellationToken cancellationToken)
    {
        await _service.SubmitAsync(publicToken, request, cancellationToken);
        return Ok(ApiResponse.Success("شكرًا لك، تم استلام تقييمك بنجاح."));
    }
}
