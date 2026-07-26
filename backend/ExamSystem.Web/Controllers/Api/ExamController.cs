using ExamSystem.Web.Dtos;
using ExamSystem.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExamSystem.Web.Controllers.Api;

[ApiController]
[Route("api/exam")]
[Authorize(Policy = "ApiKeyOnly")]
public class ExamController(AccessCodeService accessCodes, InfoedukaExportService infoeduka) : ControllerBase
{
    [HttpPost("access-codes")]
    public async Task<IActionResult> AccessCodes([FromBody] AccessCodesRequest request)
        => Ok(await accessCodes.GetAsync(request.ExamId));

    [HttpPost("students")]
    public async Task<IActionResult> Students([FromBody] ExamStudentsRequest request)
        => Ok(await infoeduka.GetStudentsAsync(request.ExamId));
}
