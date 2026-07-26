using ExamSystem.Web.Auth;
using Microsoft.AspNetCore.Mvc;

namespace ExamSystem.Web.Controllers.Web;

// Temporary stub: proves the SAML -> Student resolution round-trip end to end (login -> cookie ->
// this page -> SamlUserService -> UnknownStudentException on mismatch). Replaced by the real wizard
// exam list in Phase 5.
public class ExamsController(SamlUserService samlUserService) : Controller
{
    [HttpGet("/exams")]
    public async Task<IActionResult> Index()
    {
        var student = await samlUserService.ResolveStudentAsync(User);
        return View(student);
    }
}
