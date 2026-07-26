using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sustainsys.Saml2.AspNetCore2;

namespace ExamSystem.Web.Controllers.Web;

public class AccountController : Controller
{
    [AllowAnonymous]
    [HttpGet("/login")]
    public IActionResult Login() => View();

    [AllowAnonymous]
    [HttpGet("/auth/aai")]
    public IActionResult LoginWithAai()
        => Challenge(new AuthenticationProperties { RedirectUri = "/exams" }, Saml2Defaults.Scheme);
}
