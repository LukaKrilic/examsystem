using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ExamSystem.Web.Exceptions;

// Produces the CLAUDE.md {error, message} shape for every /api/** exception. Returns false for
// non-API paths so those requests fall through to the MVC "/Home/Error" fallback instead.
public class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext ctx, Exception ex, CancellationToken ct)
    {
        // UseExceptionHandler's ExceptionHandlingPath rewrite happens BEFORE registered
        // IExceptionHandlers run, so ctx.Request.Path is already "/Home/Error" here — the original
        // path survives only on this feature.
        var originalPath = ctx.Features.Get<IExceptionHandlerPathFeature>()?.Path;
        if (originalPath is null || !originalPath.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
            return false;

        var (status, code) = ex switch
        {
            StudentNotFoundException => (404, "STUDENT_NOT_FOUND"),
            ExamNotFoundException => (404, "EXAM_NOT_FOUND"),
            ExamLockedException => (409, "EXAM_LOCKED"),
            SessionAlreadyConfirmedException => (409, "SESSION_ALREADY_CONFIRMED"),
            InvalidStepTransitionException => (409, "INVALID_STEP_TRANSITION"),
            NoActiveSessionException => (404, "NO_ACTIVE_SESSION"),
            DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 } }
                => (409, "SESSION_ALREADY_CONFIRMED"),
            _ => (500, "INTERNAL_ERROR")
        };

        if (status == 500)
            logger.LogError(ex, "Unhandled exception on {Path}", ctx.Request.Path);

        ctx.Response.StatusCode = status;
        await ctx.Response.WriteAsJsonAsync(new { error = code, message = ex.Message }, ct);
        return true;
    }
}
