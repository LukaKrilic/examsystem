using ExamSystem.Web.Dtos;
using ExamSystem.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExamSystem.Web.Controllers.Api;

[ApiController]
[Route("api/screenshots")]
[Authorize(Policy = "ApiKeyOnly")]
public class ScreenshotsController(ScreenshotService screenshots) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Post([FromBody] ScreenshotRequest request)
        => Ok(await screenshots.SaveAsync(request));
}
