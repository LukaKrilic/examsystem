using ExamSystem.Web.Dtos;
using ExamSystem.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExamSystem.Web.Controllers.Api;

[ApiController]
[Route("api/student")]
[Authorize(Policy = "ApiOrSaml")]
public class StudentController(ExamQueryService examQuery, ExamDetailsService examDetails) : ControllerBase
{
    [HttpPost("exams/active")]
    public async Task<IActionResult> ExamsActive([FromBody] ActiveExamsRequest request)
        => Ok(await examQuery.ActiveExamsAsync(request.StudentId, request.Timestamp));

    [HttpPost("exam-details")]
    public async Task<IActionResult> ExamDetails([FromBody] ExamDetailsRequest request)
        => Ok(await examDetails.GetDetailsAsync(request.StudentId, request.CourseId));
}
