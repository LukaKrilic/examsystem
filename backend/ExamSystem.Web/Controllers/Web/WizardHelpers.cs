using ExamSystem.Web.Domain;
using ExamSystem.Web.Enums;

namespace ExamSystem.Web.Controllers.Web;

// Shared by every wizard controller: where a session's persisted WizardStep should redirect to
// (used both for resume-after-login and for blocking forward deep-links), and Zagreb-local display
// conversions for exam date/times pulled straight off the entity (UTC in the DB).
internal static class WizardHelpers
{
    private static readonly TimeZoneInfo Zagreb = TimeZoneInfo.FindSystemTimeZoneById("Europe/Zagreb");

    public static string RouteFor(ExamSession session) => session.WizardStep switch
    {
        WizardStep.OUTCOMES => $"/exams/{session.Exam.ExamId}/outcomes",
        WizardStep.INSTRUCTIONS => $"/exams/{session.Exam.ExamId}/instructions",
        WizardStep.CONFIRM => $"/exams/{session.Exam.ExamId}/confirm",
        WizardStep.IN_EXAM => "/session/instructions",
        _ => "/exams"
    };

    public static DateTime NowZagreb() => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zagreb);

    public static DateTime ToZagrebLocal(DateTimeOffset value) =>
        TimeZoneInfo.ConvertTimeFromUtc(value.UtcDateTime, Zagreb);
}
