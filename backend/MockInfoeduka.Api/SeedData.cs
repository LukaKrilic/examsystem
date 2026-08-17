namespace MockInfoeduka.Api;

// Stands in for the university's Infoeduka system of record. Every value here is ported verbatim
// from the seed section of database/Database.sql — same students, JMBAGs, AAI principals (they must
// keep matching the Keycloak dev realm's test users), course IDs and names, exam IDs, classrooms,
// registrations and point numbers. Nothing here may be "tidied": ExamSystem.Tests asserts on these
// exact values.
public record Student(string StudentId, string FullName, string Jmbag, string AaiPrincipal);

public record Course(string CourseId, string NameHr, string NameEn);

public record Exam(string ExamId, string CourseId, DateTimeOffset ExamDateTime, string Classroom);

public record Registration(string StudentId, string ExamId);

// Points are per STUDENT per outcome, not per course: Database.sql's StudentOutcomePoints is keyed
// (StudentId, LearningOutcomeId), and Ivan and Ana genuinely hold different points on the same
// outcome of Matematika 1. The endpoint is /students/{studentId}/courses/{courseId}/outcomes, so the
// student dimension has to survive the port.
public record Outcome(
    string StudentId,
    string CourseId,
    string OutcomeCode,
    decimal TotalEarned,
    decimal TotalMax,
    decimal ExamEarned,
    decimal ExamMax);

public static class SeedData
{
    // Both exams share ONE instant — that identity is the same-term lockout testbed and must not be
    // broken. Computed once as the seed loads, mirroring Database.sql's
    // `DATEADD(MINUTE, 10, SYSDATETIMEOFFSET())`, which likewise fixes the value when the script runs:
    // restart this process to refresh the window, exactly as you would re-run the script.
    private static readonly DateTimeOffset ExamTime = DateTimeOffset.UtcNow.AddMinutes(10);

    public static readonly List<Student> Students =
    [
        new("2023001234", "Ivan Horvat", "0036512345", "ivan.horvat@algebra.hr"),
        new("2023001235", "Ana Kovač",   "0036512346", "ana.kovac@algebra.hr"),
        new("2023001236", "Marko Marić", "0036512347", "marko.maric@algebra.hr"),
    ];

    public static readonly List<Course> Courses =
    [
        new("2876", "Matematika 1",    "Mathematics 1"),
        new("2888", "Programiranje 1", "Programming 1"),
    ];

    public static readonly List<Exam> Exams =
    [
        new("EXAM-1001", "2876", ExamTime, "A101"),
        new("EXAM-1002", "2888", ExamTime, "B203"),   // same term as EXAM-1001 → lockout testbed
    ];

    // Ivan is registered for BOTH same-term exams (the graded lockout scenario); Ana and Marko for one each.
    public static readonly List<Registration> Registrations =
    [
        new("2023001234", "EXAM-1001"),
        new("2023001234", "EXAM-1002"),
        new("2023001235", "EXAM-1001"),
        new("2023001236", "EXAM-1002"),
    ];

    // Green card rule is TotalEarned >= 0.5 * TotalMax (decimal, >=). Four rows sit EXACTLY at 50% —
    // the boundary case the outcome-card tests rely on; they are marked below.
    public static readonly List<Outcome> Outcomes =
    [
        // Ivan, Matematika 1
        new("2023001234", "2876", "I1",  50.00m, 100.00m, 10.00m, 20.00m),   // exactly 50% → green boundary
        new("2023001234", "2876", "I2",  75.00m, 100.00m, 15.00m, 20.00m),   // green
        new("2023001234", "2876", "I3",  40.00m, 100.00m,  8.00m, 20.00m),   // not green
        new("2023001234", "2876", "I4",  60.50m, 100.00m, 12.00m, 20.00m),   // green
        new("2023001234", "2876", "I5",  30.00m, 100.00m,  6.00m, 20.00m),   // not green
        // Ivan, Programiranje 1
        new("2023001234", "2888", "I1",  55.00m, 100.00m, 11.00m, 20.00m),   // green
        new("2023001234", "2888", "I2",  50.00m, 100.00m, 10.00m, 20.00m),   // exactly 50% → green boundary
        new("2023001234", "2888", "I3",  20.00m, 100.00m,  4.00m, 20.00m),   // not green
        new("2023001234", "2888", "I4",  90.00m, 100.00m, 18.00m, 20.00m),   // green
        new("2023001234", "2888", "I5",  45.00m, 100.00m,  9.00m, 20.00m),   // not green
        // Ana, Matematika 1
        new("2023001235", "2876", "I1",  80.00m, 100.00m, 16.00m, 20.00m),
        new("2023001235", "2876", "I2",  50.00m, 100.00m, 10.00m, 20.00m),   // exactly 50%
        new("2023001235", "2876", "I3",  65.00m, 100.00m, 13.00m, 20.00m),
        new("2023001235", "2876", "I4",  30.00m, 100.00m,  6.00m, 20.00m),
        new("2023001235", "2876", "I5", 100.00m, 100.00m, 20.00m, 20.00m),
        // Marko, Programiranje 1
        new("2023001236", "2888", "I1",  25.00m, 100.00m,  5.00m, 20.00m),
        new("2023001236", "2888", "I2",  60.00m, 100.00m, 12.00m, 20.00m),
        new("2023001236", "2888", "I3",  50.00m, 100.00m, 10.00m, 20.00m),   // exactly 50%
        new("2023001236", "2888", "I4",  70.00m, 100.00m, 14.00m, 20.00m),
        new("2023001236", "2888", "I5",  40.00m, 100.00m,  8.00m, 20.00m),
    ];
}
