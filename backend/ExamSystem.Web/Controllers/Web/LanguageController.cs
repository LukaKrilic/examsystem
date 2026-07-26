using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace ExamSystem.Web.Controllers.Web;

public class LanguageController : Controller
{
    [AllowAnonymous]
    [HttpGet("/set-language")]
    public IActionResult SetLanguage(string lang, string returnUrl = "/")
    {
        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(lang)),
            new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) });
        return LocalRedirect(returnUrl);
    }
}
