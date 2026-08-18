namespace ExamSystem.Web.Infoeduka;

// The ONLY door to student/course/exam/registration/outcome-points data. Every service that needs it
// injects this interface — never a local DbSet, never an HttpClient of its own. Swapping the mock for
// the university's real Infoeduka later is a config change plus, at most, one new implementation.
public interface IInfoedukaClient
{
    Task<InfoedukaStudent?> GetStudentAsync(string studentId, CancellationToken ct = default);

    // AAI identity → student record; principal wins, JMBAG is the fallback. A null result means the
    // identity is unknown to Infoeduka — an expected outcome, not a failure (edge case 1).
    Task<InfoedukaStudent?> ResolveStudentByIdentityAsync(
        string? aaiPrincipal, string? jmbag, CancellationToken ct = default);

    // from/to omitted → the student's FULL registration list. The same-term lockout needs it that way:
    // it must see exams sharing the chosen exam's ExamDateTime even when they sit outside the ±30 min window.
    Task<IReadOnlyList<InfoedukaRegistration>> GetRegistrationsAsync(
        string studentId, DateTimeOffset? from = null, DateTimeOffset? to = null, CancellationToken ct = default);

    Task<InfoedukaCourseOutcomes?> GetCourseOutcomesAsync(
        string studentId, string courseId, CancellationToken ct = default);
}
