using ExamSystem.Web.Auth;
using ExamSystem.Web.Dtos;
using ExamSystem.Web.Exceptions;
using ExamSystem.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExamSystem.Web.Controllers.Api;

[ApiController]
[Route("api/sessions")]
[Authorize(Policy = "ApiOrSaml")]
public class SessionsController(SessionService sessions, SamlUserService samlUserService) : ControllerBase
{
    // SAML only (browser confirm button) — not callable by machine clients with just an API key.
    [HttpPost("confirm")]
    [Authorize(Policy = "SamlOnly")]
    public async Task<IActionResult> Confirm([FromBody] SessionConfirmRequest request)
    {
        var student = await samlUserService.ResolveStudentAsync(User);
        var session = await sessions.ConfirmAsync(student.StudentId, request.DeviceId, request.GroupNo);
        return Ok(SessionService.ToStateResponse(session));
    }

    [HttpPost("active")]
    public async Task<IActionResult> Active([FromBody] SessionActiveRequest request)
    {
        var session = !string.IsNullOrEmpty(request.SessionId)
            ? await sessions.FindBySessionIdAsync(request.SessionId)
            : await sessions.FindActiveByStudentAsync(
                request.StudentId ?? throw new NoActiveSessionException("unknown"));
        return Ok(SessionService.ToStateResponse(session));
    }

    [HttpPost("end")]
    public async Task<IActionResult> End([FromBody] SessionEndRequest request)
    {
        var session = await sessions.EndAsync(request.SessionId);
        return Ok(new SessionEndResponse(session.SessionId, session.Status.ToString()));
    }
}
