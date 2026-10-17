using AlFalah.Application.IntelligentTimetable;
using AlFalah.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlFalah.Api.Controllers.IntelligentTimetable;

[ApiController,Authorize,Route("api/v1/timetable-coverage/register")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class CoverageRegisterController(CoverageRegisterService service):ControllerBase
{
    [HttpGet] public Task<IActionResult> Context(DateOnly date,CancellationToken ct,int page=1,int pageSize=10)=>Respond(()=>service.ContextAsync(date,page,pageSize,ct));
    [HttpPost] public Task<IActionResult> Issue(IssueCoverageRegisterRequest request,CancellationToken ct)=>Respond(()=>service.IssueAsync(request,ct));
    [HttpGet("{id:int}/content")]
    public async Task<IActionResult> Content(int id,CancellationToken ct)
    {
        try{
            var file=await service.ContentAsync(id,ct);Response.Headers.CacheControl="private, no-store";
            Response.Headers.ETag="\""+file.Sha256+"\"";Response.Headers["X-Document-SHA256"]=file.Sha256;
            return File(file.Bytes,"application/pdf",file.FileName);
        }catch(UnauthorizedAccessException){return StatusCode(403,ApiResponse<object>.Fail("عرض سجل الانتظار متاح لمسؤول الجدول فقط."));}
        catch(KeyNotFoundException){return NotFound(ApiResponse<object>.Fail("النسخة غير موجودة في المدرسة النشطة."));}
    }
    private async Task<IActionResult> Respond<T>(Func<Task<T>> action)
    {
        try{return Ok(ApiResponse<T>.Success(await action()));}
        catch(UnauthorizedAccessException){return StatusCode(403,ApiResponse<T>.Fail("إصدار سجل الانتظار وعرضه متاح لمسؤول الجدول فقط."));}
        catch(KeyNotFoundException){return NotFound(ApiResponse<T>.Fail("السجل غير موجود في المدرسة النشطة."));}
        catch(BellScheduleConflictException ex){return Conflict(ApiResponse<T>.Fail(ex.Message));}
        catch(ArgumentException ex){return BadRequest(ApiResponse<T>.Fail(ex.Message));}
    }
}
