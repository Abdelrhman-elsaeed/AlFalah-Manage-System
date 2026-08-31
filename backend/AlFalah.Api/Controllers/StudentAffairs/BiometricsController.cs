using AlFalah.Application.Interfaces;
using AlFalah.Application.StudentAffairs.Biometrics;
using AlFalah.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AlFalah.Api.Controllers.StudentAffairs;

[Route("api/v1/student-affairs/biometrics")]
public sealed class BiometricsController : StudentAffairsControllerBase
{
    public BiometricsController(IMediator mediator, ICurrentUserService currentUser)
        : base(mediator, currentUser) { }

    [HttpPost("zajel/import")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<IActionResult> ImportZajel(
        [FromForm] IFormFile file,
        CancellationToken cancellationToken)
    {
        if (!HasAnyPermission(PermissionNames.BiometricImport)) return PermissionDenied();
        if (file.Length == 0) return BadRequest("An Excel workbook is required");

        await using var content = file.OpenReadStream();
        var response = await Mediator.Send(
            new ImportZajelBiometricCommand(content, file.FileName),
            cancellationToken);
        return response.IsSuccess ? Ok(response) : BadRequest(response);
    }
}
