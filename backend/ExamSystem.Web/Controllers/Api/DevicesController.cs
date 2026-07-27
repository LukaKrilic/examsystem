using ExamSystem.Web.Dtos;
using ExamSystem.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExamSystem.Web.Controllers.Api;

[ApiController]
[Route("api/devices")]
[Authorize(Policy = "ApiKeyOnly")]
public class DevicesController(DeviceService devices) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] DeviceRegisterRequest request)
        => Ok(await devices.RegisterAsync(request));
}
