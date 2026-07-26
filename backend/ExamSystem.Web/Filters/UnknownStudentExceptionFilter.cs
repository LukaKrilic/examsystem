using ExamSystem.Web.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ExamSystem.Web.Filters;

// Turns an UnknownStudentException into the friendly localized error page (CLAUDE.md edge cases
// 1-2) instead of a stack trace or the developer exception page. Registered globally so it applies
// no matter which controller resolved the SAML claims.
public class UnknownStudentExceptionFilter(ILogger<UnknownStudentExceptionFilter> logger) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not UnknownStudentException)
            return;

        var claims = string.Join(", ", context.HttpContext.User.Claims.Select(c => $"{c.Type}={c.Value}"));
        logger.LogWarning(context.Exception, "SAML login resolved to no matching student. Claims: {Claims}", claims);

        context.Result = new ViewResult { ViewName = "~/Views/Shared/UnknownStudent.cshtml" };
        context.ExceptionHandled = true;
    }
}
