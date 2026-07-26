using ExamSystem.Web.Dtos;
using ExamSystem.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExamSystem.Web.Controllers.Api;

[ApiController]
[Route("api/instructions")]
[Authorize(Policy = "ApiOrSaml")]
public class InstructionsController(InstructionService instructions) : ControllerBase
{
    [HttpPost("html")]
    public async Task<IActionResult> Html([FromBody] InstructionsHtmlRequest request)
    {
        var response = await instructions.GetHtmlAsync(request.InstructionId);
        return response is null ? NotFound() : Ok(response);
    }
}
