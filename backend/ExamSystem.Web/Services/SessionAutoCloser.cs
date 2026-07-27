using ExamSystem.Web.Data;
using ExamSystem.Web.Enums;
using Microsoft.EntityFrameworkCore;

namespace ExamSystem.Web.Services;

// Flips every ACTIVE session to AUTO_CLOSED at midnight Europe/Zagreb. Exam:AutoCloseEverySeconds
// (dev-only config) overrides the delay so the behavior can be demoed/tested without waiting for
// real midnight.
public class SessionAutoCloser(IServiceScopeFactory scopes, IConfiguration config, ILogger<SessionAutoCloser> log)
    : BackgroundService
{
    private static readonly TimeZoneInfo Zagreb = TimeZoneInfo.FindSystemTimeZoneById("Europe/Zagreb");

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var everySeconds = config.GetValue<int?>("Exam:AutoCloseEverySeconds");
            var delay = everySeconds is > 0 ? TimeSpan.FromSeconds(everySeconds.Value) : DelayUntilNextZagrebMidnight();
            await Task.Delay(delay, ct);

            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ExamDbContext>();
            var closed = await db.ExamSessions
                .Where(s => s.Status == SessionStatus.ACTIVE)
                .ExecuteUpdateAsync(u => u
                    .SetProperty(s => s.Status, SessionStatus.AUTO_CLOSED)
                    .SetProperty(s => s.EndedAt, DateTimeOffset.UtcNow), ct);
            log.LogInformation("Auto-closed {Count} sessions", closed);
        }
    }

    internal static TimeSpan DelayUntilNextZagrebMidnight()
    {
        var nowZg = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zagreb);
        var nextMidnight = nowZg.Date.AddDays(1);
        return nextMidnight - nowZg;   // handles DST because both sides are Zagreb-local
    }
}
