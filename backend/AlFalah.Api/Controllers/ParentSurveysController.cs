using AlFalah.Application.DTOs.ParentSurveys;
using AlFalah.Application.Interfaces;
using AlFalah.Domain.Enums;
using AlFalah.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlFalah.Api.Controllers;

[ApiController]
[Route("api/v1/parent-surveys")]
[Authorize]
public class ParentSurveysController : ControllerBase
{
    private readonly IParentSurveyService _service;
    private readonly ICurrentUserService _currentUser;

    public ParentSurveysController(IParentSurveyService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] bool templates = false,
        [FromQuery] int? schoolId = null,
        CancellationToken cancellationToken = default)
    {
        if (!CanManage())
            return StatusCode(403, ApiResponse.Fail("ليس لديك صلاحية إدارة استبيانات أولياء الأمور."));
        var result = await _service.ListAsync(templates, schoolId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ParentSurveyDto>>.Success(result));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken cancellationToken)
    {
        if (!CanManage())
            return StatusCode(403, ApiResponse.Fail("ليس لديك صلاحية إدارة استبيانات أولياء الأمور."));
        var result = await _service.GetAsync(id, cancellationToken);
        return Ok(ApiResponse<ParentSurveyDto>.Success(result));
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] SaveParentSurveyRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!CanManage())
            return StatusCode(403, ApiResponse.Fail("ليس لديك صلاحية إدارة استبيانات أولياء الأمور."));
        var result = await _service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = result.Id },
            ApiResponse<ParentSurveyDto>.Success(result, request.IsTemplate ? "تم حفظ القالب." : "تم إنشاء النموذج."));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] SaveParentSurveyRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!CanManage())
            return StatusCode(403, ApiResponse.Fail("ليس لديك صلاحية إدارة استبيانات أولياء الأمور."));
        var result = await _service.UpdateAsync(id, request, cancellationToken);
        return Ok(ApiResponse<ParentSurveyDto>.Success(result, "تم حفظ التعديلات."));
    }

    [HttpPost("{id:int}/publish")]
    public async Task<IActionResult> Publish(int id, CancellationToken cancellationToken)
    {
        if (!CanManage())
            return StatusCode(403, ApiResponse.Fail("ليس لديك صلاحية إدارة استبيانات أولياء الأمور."));
        var result = await _service.PublishAsync(id, cancellationToken);
        return Ok(ApiResponse<PublishParentSurveyDto>.Success(result, "تم إنشاء رابط النموذج."));
    }

    [HttpPost("{id:int}/close")]
    public async Task<IActionResult> Close(int id, CancellationToken cancellationToken)
    {
        if (!CanManage())
            return StatusCode(403, ApiResponse.Fail("ليس لديك صلاحية إدارة استبيانات أولياء الأمور."));
        await _service.CloseAsync(id, cancellationToken);
        return Ok(ApiResponse.Success("تم إغلاق النموذج."));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        if (!CanManage())
            return StatusCode(403, ApiResponse.Fail("ليس لديك صلاحية إدارة استبيانات أولياء الأمور."));
        await _service.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponse.Success("تم حذف النموذج."));
    }

    [HttpGet("{id:int}/submissions")]
    public async Task<IActionResult> ListSubmissions(int id, CancellationToken cancellationToken)
    {
        if (!CanManage())
            return StatusCode(403, ApiResponse.Fail("ليس لديك صلاحية عرض ردود أولياء الأمور."));
        var result = await _service.ListSubmissionsAsync(id, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ParentSurveySubmissionListItemDto>>.Success(result));
    }

    [HttpGet("{id:int}/submissions/{submissionId:int}")]
    public async Task<IActionResult> GetSubmission(int id, int submissionId, CancellationToken cancellationToken)
    {
        if (!CanManage())
            return StatusCode(403, ApiResponse.Fail("ليس لديك صلاحية عرض ردود أولياء الأمور."));
        var result = await _service.GetSubmissionAsync(id, submissionId, cancellationToken);
        return Ok(ApiResponse<ParentSurveySubmissionDto>.Success(result));
    }

    private bool CanManage() =>
        _currentUser.HasPermission(PermissionNames.ParentSurveyManage)
        && (_currentUser.IsInRole(RoleNames.SchoolManager)
            || _currentUser.IsInRole(RoleNames.Moderator)
            || _currentUser.IsInRole(RoleNames.SuperAdmin));
}
