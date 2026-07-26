using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ExamSystem.Web.Models;

namespace ExamSystem.Web.Controllers;

// Only the error action remains — it's the fallback target for UseExceptionHandler and
// ApiExceptionHandler's non-API path. "/" now redirects straight to /login (Program.cs).
public class HomeController : Controller
{
    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
