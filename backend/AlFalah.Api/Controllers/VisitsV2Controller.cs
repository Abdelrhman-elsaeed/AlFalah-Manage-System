using AlFalah.Application.DTOs.Visits;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlFalah.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v2/visits")]
public sealed class VisitsV2Controller(
    IVisitV2Service visits,
    IVisitService legacyWorkflow,
    ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet("availability")]
    public IActionResult Availability() => Ok(ApiResponse<VisitV2AvailabilityDto>.Success(visits.GetAvailability()));

    [HttpGet("observation-card")]
    public async Task<IActionResult> ObservationCard(CancellationToken cancellationToken) =>
        Ok(ApiResponse<VisitV2ObservationCardDto>.Success(await visits.GetObservationCardAsync(cancellationToken)));

    [HttpPost]
    public async Task<IActionResult> Create(CreateVisitV2RequestDto request, CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(PermissionNames.VisitCreate)) return Forbidden("ليس لديك صلاحية لإنشاء زيارة.");
        var result = await visits.CreateAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<VisitV2DetailDto>.Success(result, "تم إنشاء زيارة V2 بنجاح."));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateVisitV2RequestDto request, CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(PermissionNames.VisitEdit)) return Forbidden("ليس لديك صلاحية لتعديل الزيارة.");
        return Ok(ApiResponse<VisitV2DetailDto>.Success(
            await visits.UpdateAsync(id, request, cancellationToken), "تم حفظ بطاقة الزيارة."));
    }

    [HttpPost("{id:int}/finalize")]
    public async Task<IActionResult> Finalize(int id, CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(PermissionNames.VisitSubmit) && !currentUser.HasPermission(PermissionNames.VisitEdit))
            return Forbidden("ليس لديك صلاحية لإنهاء الزيارة.");
        return Ok(ApiResponse<VisitV2DetailDto>.Success(
            await visits.FinalizeAsync(id, cancellationToken), "تم إنهاء الزيارة وإرسالها للاعتماد."));
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] VisitV2ArchiveQuery query, CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(PermissionNames.VisitView) && !currentUser.IsInRole(RoleNames.Instructor))
            return Forbidden("ليس لديك صلاحية لعرض الزيارات.");
        return Ok(ApiResponse<VisitV2ArchiveResultDto>.Success(await visits.ListAsync(query, cancellationToken)));
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(PermissionNames.VisitView)) return Forbidden("ليس لديك صلاحية لعرض لوحة الزيارات.");
        return Ok(ApiResponse<VisitV2DashboardDto>.Success(await visits.GetDashboardAsync(cancellationToken)));
    }

    [HttpGet("export/csv")]
    public async Task<IActionResult> ExportCsv([FromQuery] VisitV2ArchiveQuery query, CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(PermissionNames.VisitView)) return Forbidden("ليس لديك صلاحية لتصدير الزيارات.");
        var file = await visits.ExportCsvAsync(query, cancellationToken);
        return File(file.Content, "text/csv; charset=utf-8", file.FileName);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken cancellationToken) =>
        Ok(ApiResponse<VisitV2DetailDto>.Success(await visits.GetAsync(id, cancellationToken)));

    [HttpGet("{id:int}/report/pdf")]
    public async Task<IActionResult> Pdf(int id, CancellationToken cancellationToken)
    {
        var file = await visits.ExportPdfAsync(id, cancellationToken);
        return File(file.Content, "application/pdf", file.FileName);
    }

    [HttpPut("{id:int}/treatment-recommendations")]
    public async Task<IActionResult> UpdateTreatments(
        int id,
        UpdateVisitV2TreatmentsDto request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(PermissionNames.VisitEdit)) return Forbidden("ليس لديك صلاحية لتعديل الخطة العلاجية.");
        return Ok(ApiResponse<IReadOnlyList<VisitV2TreatmentDto>>.Success(
            await visits.UpdateTreatmentsAsync(id, request, cancellationToken), "تم حفظ الخطة العلاجية."));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(PermissionNames.VisitDelete)) return Forbidden("ليس لديك صلاحية لحذف الزيارة.");
        await visits.SoftDeleteAsync(id, cancellationToken);
        return Ok(ApiResponse.Success("تم حذف الزيارة بأمان."));
    }

    [HttpPost("{id:int}/approve")]
    public async Task<IActionResult> Approve(int id, CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(PermissionNames.VisitApprove)) return Forbidden("ليس لديك صلاحية اعتماد الزيارة.");
        await legacyWorkflow.ApproveAsync(id, cancellationToken);
        return Ok(ApiResponse<VisitV2DetailDto>.Success(await visits.GetAsync(id, cancellationToken), "تم اعتماد الزيارة."));
    }

    [HttpPost("{id:int}/reject")]
    public async Task<IActionResult> Reject(int id, RejectVisitRequestDto request, CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(PermissionNames.VisitApprove)) return Forbidden("ليس لديك صلاحية رفض الزيارة.");
        await legacyWorkflow.RejectAsync(id, request.Reason, cancellationToken);
        return Ok(ApiResponse<VisitV2DetailDto>.Success(await visits.GetAsync(id, cancellationToken), "تمت إعادة الزيارة للتعديل."));
    }

    [HttpPost("{id:int}/reopen")]
    public async Task<IActionResult> Reopen(int id, ReopenVisitRequestDto request, CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(PermissionNames.VisitReopen)) return Forbidden("ليس لديك صلاحية إعادة فتح الزيارة.");
        await legacyWorkflow.ReopenAsync(id, request.Reason, cancellationToken);
        return Ok(ApiResponse<VisitV2DetailDto>.Success(await visits.GetAsync(id, cancellationToken), "تمت إعادة فتح الزيارة."));
    }

    private ObjectResult Forbidden(string message) => StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail(message));
}
